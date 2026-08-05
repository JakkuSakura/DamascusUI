mod routes;

use damascus::{App, Config};

fn main() {
    damascus::tracing_subscriber::fmt::init();

    let app = App::builder()
        .config(Config::new().port(3002).app_name("Todos TSX"))
        .merge(routes::router())
        .build();
    app.run().expect("todos-tsx server failed");
}
