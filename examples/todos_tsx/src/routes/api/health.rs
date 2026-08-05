use damascus::prelude::*;

// A standalone Rust route does not need a matching TSX file.
pub async fn handler() -> impl IntoResponse {
    Json(serde_json::json!({ "status": "ok", "source": "rust-route" }))
}
