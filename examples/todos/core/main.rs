mod handlers;
mod models;

use rust_embed::RustEmbed;

use damascus::prelude::*;
use damascus::{App, Config};

#[derive(RustEmbed)]
#[folder = "ui/dist"]
struct Frontend;

async fn serve_frontend(uri: damascus::axum::http::Uri) -> impl IntoResponse {
    use damascus::axum::body::Body;
    use damascus::axum::http::{StatusCode, header};
    use damascus::axum::response::IntoResponse;

    let path = uri.path().trim_start_matches('/');
    let path = if path.is_empty() { "index.html" } else { path };

    match Frontend::get(path) {
        Some(file) => {
            let mime = mime_guess::from_path(path).first_or_octet_stream();
            let body = Body::from(file.data.into_owned());
            let headers = [(header::CONTENT_TYPE, mime.as_ref())];
            (StatusCode::OK, headers, body).into_response()
        }
        None => match Frontend::get("index.html") {
            Some(file) => {
                let body = Body::from(file.data.into_owned());
                let headers = [(header::CONTENT_TYPE, "text/html")];
                (StatusCode::OK, headers, body).into_response()
            }
            None => StatusCode::NOT_FOUND.into_response(),
        },
    }
}

fn main() {
    damascus::tracing_subscriber::fmt::init();

    let app = App::builder()
        .config(Config::new().port(3001).app_name("Todos"))
        .route("/api/todos", get(handlers::list).post(handlers::create))
        .route(
            "/api/todos/{id}",
            get(get_one).put(handlers::update).delete(handlers::delete),
        )
        .layer(Extension(models::init_db()))
        .fallback(serve_frontend)
        .build();

    app.run().unwrap();
}

async fn get_one(Path(id): Path<usize>) -> String {
    format!("get todo {id}")
}
