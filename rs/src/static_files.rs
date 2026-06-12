use rust_embed::RustEmbed;
use axum::{
    body::Body,
    http::{StatusCode, header},
    response::IntoResponse,
};

#[derive(RustEmbed)]
#[folder = "../ts/dist"]
pub struct Frontend;

pub async fn serve_static(uri: axum::http::Uri) -> impl IntoResponse {
    let path = uri.path().trim_start_matches('/');
    let path = if path.is_empty() { "index.html" } else { path };

    match Frontend::get(path) {
        Some(file) => {
            let mime = mime_guess::from_path(path).first_or_octet_stream();
            let body = Body::from(file.data.into_owned());
            let headers = [(header::CONTENT_TYPE, mime.as_ref())];
            (StatusCode::OK, headers, body).into_response()
        }
        None => {
            // SPA fallback: serve index.html for unknown routes
            match Frontend::get("index.html") {
                Some(file) => {
                    let body = Body::from(file.data.into_owned());
                    let headers = [(header::CONTENT_TYPE, "text/html")];
                    (StatusCode::OK, headers, body).into_response()
                }
                None => StatusCode::NOT_FOUND.into_response(),
            }
        }
    }
}
