mod handlers;
mod models;

use damascus::prelude::*;
use damascus::{App, Config};

fn main() {
    damascus::tracing_subscriber::fmt::init();

    let app = App::builder()
        .config(Config::new().port(3001))
        .route(
            "/api/todos",
            get(handlers::list).post(handlers::create),
        )
        .route(
            "/api/todos/{id}",
            get(get_one)
                .put(handlers::update)
                .delete(handlers::delete),
        )
        .layer(Extension(models::init_db()))
        .build();

    app.run().unwrap();
}

async fn get_one(Path(id): Path<usize>) -> String {
    format!("get todo {id}")
}
