//! Runtime support for the `tsx!` macro.
//!
//! The macro keeps SolidJS expressions as TSX source and uses `${...}` for
//! Rust-owned values and callbacks. Values are serialized with `serde_json`;
//! callbacks remain on the Rust side and are identified in the page metadata.

use std::{
    collections::BTreeMap,
    future::Future,
    pin::Pin,
    sync::{
        Arc, Mutex, OnceLock,
        atomic::{AtomicU64, Ordering},
    },
};

use axum::{
    Json, Router,
    extract::Path,
    http::StatusCode,
    response::{Html, IntoResponse, Response},
    routing::get,
};
use serde::Serialize;
use serde_json::{Map, Value};

extern crate damascus_tsx_proc;

pub mod build;

pub use damascus_tsx_proc::tsx;

pub type CallbackFuture = Pin<Box<dyn Future<Output = Result<Value, TsxError>> + Send>>;

type Callback = Arc<dyn Fn() -> CallbackFuture + Send + Sync>;

static CALLBACKS: OnceLock<Mutex<BTreeMap<String, Callback>>> = OnceLock::new();
static NEXT_CALLBACK_ID: AtomicU64 = AtomicU64::new(1);

#[derive(Debug, thiserror::Error)]
pub enum TsxError {
    #[error("failed to serialize TSX binding `{name}`: {source}")]
    Serialize {
        name: String,
        source: serde_json::Error,
    },

    #[error("TSX binding `{0}` was registered more than once")]
    DuplicateBinding(String),

    #[error("TSX callback `{0}` was not registered")]
    MissingCallback(String),

    #[error("TSX callback failed: {0}")]
    Callback(String),

    #[error("failed to serialize TSX bootstrap data: {0}")]
    Bootstrap(#[from] serde_json::Error),
}

impl IntoResponse for TsxError {
    fn into_response(self) -> Response {
        (StatusCode::INTERNAL_SERVER_ERROR, self.to_string()).into_response()
    }
}

/// A TSX page plus the Rust values and callbacks referenced by its source.
#[derive(Clone)]
pub struct TsxPage {
    document: &'static str,
    source: String,
    values: Map<String, Value>,
    callbacks: BTreeMap<String, String>,
}

impl TsxPage {
    pub fn new(source: impl Into<String>) -> Self {
        Self::with_document("", source)
    }

    pub fn with_document(document: &'static str, source: impl Into<String>) -> Self {
        Self {
            document,
            source: source.into(),
            values: Map::new(),
            callbacks: BTreeMap::new(),
        }
    }

    pub fn source(&self) -> &str {
        &self.source
    }

    pub fn values(&self) -> &Map<String, Value> {
        &self.values
    }

    pub fn callback_names(&self) -> impl Iterator<Item = &str> {
        self.callbacks.keys().map(String::as_str)
    }

    pub fn bind_value<T>(&mut self, name: &str, value: &T) -> Result<(), TsxError>
    where
        T: Serialize + ?Sized,
    {
        self.ensure_binding_available(name)?;
        let value = serde_json::to_value(value).map_err(|source| TsxError::Serialize {
            name: name.to_owned(),
            source,
        })?;
        self.values.insert(name.to_owned(), value);
        Ok(())
    }

    pub fn bind_callback<F, Fut, T, E>(&mut self, name: &str, handler: F) -> Result<(), TsxError>
    where
        F: Fn() -> Fut + Send + Sync + 'static,
        Fut: Future<Output = Result<T, E>> + Send + 'static,
        T: Serialize + 'static,
        E: std::fmt::Display + Send + 'static,
    {
        self.ensure_binding_available(name)?;
        let callback = Arc::new(move || {
            let future = handler();
            Box::pin(async move {
                let value = future
                    .await
                    .map_err(|error| TsxError::Callback(error.to_string()))?;
                serde_json::to_value(value).map_err(TsxError::Bootstrap)
            }) as CallbackFuture
        });
        let id = format!(
            "{name}-{}",
            NEXT_CALLBACK_ID.fetch_add(1, Ordering::Relaxed)
        );
        callbacks().lock().unwrap().insert(id.clone(), callback);
        self.callbacks.insert(name.to_owned(), id);
        Ok(())
    }

    pub async fn invoke_callback(&self, name: &str) -> Result<Value, TsxError> {
        let id = self
            .callbacks
            .get(name)
            .ok_or_else(|| TsxError::MissingCallback(name.to_owned()))?;
        invoke_callback(id).await
    }

    pub fn bootstrap_json(&self) -> Result<String, TsxError> {
        Ok(serde_json::json!({
            "values": self.values,
            "callbacks": self.callbacks,
        })
        .to_string())
    }

    fn render_document(&self) -> Result<String, TsxError> {
        Ok(self
            .document
            .replace("__DAMASCUS_BOOTSTRAP__", &self.bootstrap_json()?))
    }

    fn ensure_binding_available(&self, name: &str) -> Result<(), TsxError> {
        if self.values.contains_key(name) || self.callbacks.contains_key(name) {
            return Err(TsxError::DuplicateBinding(name.to_owned()));
        }
        Ok(())
    }
}

fn callbacks() -> &'static Mutex<BTreeMap<String, Callback>> {
    CALLBACKS.get_or_init(|| Mutex::new(BTreeMap::new()))
}

async fn invoke_callback(id: &str) -> Result<Value, TsxError> {
    let callback = callbacks()
        .lock()
        .unwrap()
        .get(id)
        .cloned()
        .ok_or_else(|| TsxError::MissingCallback(id.to_owned()))?;
    callback().await
}

async fn callback_route(Path(id): Path<String>) -> Response {
    match invoke_callback(&id).await {
        Ok(value) => (StatusCode::OK, Json(value)).into_response(),
        Err(TsxError::MissingCallback(_)) => StatusCode::NOT_FOUND.into_response(),
        Err(error) => (StatusCode::INTERNAL_SERVER_ERROR, error.to_string()).into_response(),
    }
}

pub fn router() -> Router {
    Router::new().route("/__damascus/callback/{id}", get(callback_route))
}

impl IntoResponse for TsxPage {
    fn into_response(self) -> Response {
        match self.render_document() {
            Ok(document) if !document.is_empty() => Html(document).into_response(),
            Ok(_) => Html(self.source).into_response(),
            Err(error) => error.into_response(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn serializes_values_and_lists_callbacks() {
        let mut page = TsxPage::new("<App />");
        page.bind_value("title", &"Todos").unwrap();
        page.bind_callback("refresh", || async {
            Ok::<_, std::convert::Infallible>(true)
        })
        .unwrap();

        assert_eq!(page.values()["title"], Value::String("Todos".into()));
        assert_eq!(page.callback_names().collect::<Vec<_>>(), ["refresh"]);
    }
}
