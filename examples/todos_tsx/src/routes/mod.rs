mod index;

use damascus::prelude::*;

pub fn router() -> Router {
    Router::new()
        .route("/", get(index::index))
        .route("/about", get(index::index))
}
