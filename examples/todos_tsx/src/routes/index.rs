use damascus::prelude::*;
use damascus_tsx::tsx;
use serde::Serialize;

#[derive(Debug, Clone, Serialize)]
struct Todo {
    id: u64,
    title: String,
    done: bool,
}

#[derive(Debug, Clone, Serialize)]
struct Metrics {
    total: usize,
    pending: usize,
}

#[derive(Debug, Clone, Serialize)]
struct Content {
    title: String,
    body: String,
}

// This Rust route is paired automatically with src/routes/index.tsx.
pub async fn handler() -> impl IntoResponse {
    let todos = vec![
        Todo {
            id: 1,
            title: "Learn TSX macros".into(),
            done: false,
        },
        Todo {
            id: 2,
            title: "Serve Rust data".into(),
            done: true,
        },
    ];
    let metrics = Metrics {
        total: todos.len(),
        pending: todos.iter().filter(|todo| !todo.done).count(),
    };

    tsx! {
        <App
            title=${"Todos from Rust"}
            todos=${todos}
            metrics=${metrics}
            on_load_content=${|| async {
                Ok::<_, std::convert::Infallible>(Content {
                    title: "Loaded by Rust".into(),
                    body: "This content came from a Rust closure.".into(),
                })
            }}
        />
    }
}
