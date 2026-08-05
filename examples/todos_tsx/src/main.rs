include!(concat!(env!("OUT_DIR"), "/damascus-tsx-routes.rs"));

use damascus::{App, Config};

fn main() {
    damascus::tracing_subscriber::fmt::init();

    let app = App::builder()
        .config(Config::new().port(3002).app_name("Todos TSX"))
        .merge(damascus_tsx_routes())
        .build();
    app.run().expect("todos-tsx server failed");
}
