use std::io;

use axum::Router;

use crate::config::Config;
use crate::serve::serve;

/// A Damascus application. Build one with [`App::builder()`].
pub struct App {
    router: Router,
    config: Config,
}

impl App {
    pub fn builder() -> AppBuilder {
        AppBuilder {
            router: Router::new(),
            config: Config::default(),
        }
    }

    /// Start the server. Blocks until shutdown.
    pub fn run(self) -> io::Result<()> {
        serve(self.router, &self.config)
    }
}

/// Builder for constructing a Damascus [`App`].
pub struct AppBuilder {
    router: Router,
    config: Config,
}

impl AppBuilder {
    /// Set the server configuration.
    pub fn config(mut self, config: Config) -> Self {
        self.config = config;
        self
    }

    /// Register a route. The `method_router` comes from `get()`, `post()`, etc.
    ///
    /// ```ignore
    /// use damascus_core::prelude::*;
    ///
    /// App::builder()
    ///     .route("/api/todos", get(list_todos).post(create_todo))
    ///     .build();
    /// ```
    pub fn route(mut self, path: &str, method_router: axum::routing::MethodRouter) -> Self {
        self.router = self.router.route(path, method_router);
        self
    }

    /// Add a tower [`Layer`] to all routes. Use this for middleware
    /// like [`Extension`] state, CORS, tracing, etc.
    ///
    /// ```ignore
    /// use damascus_core::prelude::*;
    ///
    /// let state = std::sync::Arc::new(AppState::new());
    /// App::builder()
    ///     .route("/api/data", get(handler))
    ///     .layer(Extension(state))
    ///     .build();
    /// ```
    pub fn layer<L>(mut self, layer: L) -> Self
    where
        L: tower::Layer<axum::routing::Route> + Clone + Send + Sync + 'static,
        L::Service: tower::Service<axum::http::Request<axum::body::Body>>
            + Clone
            + Send
            + Sync
            + 'static,
        <L::Service as tower::Service<axum::http::Request<axum::body::Body>>>::Response:
            axum::response::IntoResponse + 'static,
        <L::Service as tower::Service<axum::http::Request<axum::body::Body>>>::Error:
            Into<std::convert::Infallible> + 'static,
        <L::Service as tower::Service<axum::http::Request<axum::body::Body>>>::Future: Send,
    {
        self.router = self.router.layer(layer);
        self
    }

    /// Finalize and return the [`App`].
    pub fn build(self) -> App {
        App {
            router: self.router,
            config: self.config,
        }
    }
}

impl Default for AppBuilder {
    fn default() -> Self {
        Self {
            router: Router::new(),
            config: Config::default(),
        }
    }
}
