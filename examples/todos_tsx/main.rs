//! Reference example for the future `tsx!` macro.
//!
//! This file intentionally does not build yet. It defines the desired API for
//! serving a SolidJS + TypeScript page from Rust-owned data.

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

struct AppState {
    title: String,
    todos: Vec<Todo>,
    metrics: Metrics,
}

#[derive(Debug, Clone, Serialize)]
struct Content {
    title: String,
    body: String,
}

// The macro serializes Rust expressions with serde_json automatically. The
// application should not call serde_json::to_value at each prop site.
async fn index(state: AppState) -> Result<impl IntoResponse, AppError> {
    Ok(tsx! {
        <App
            title=${state.title}
            todos=${state.todos}
            metrics=${state.metrics}
            on_load_content=${move || async move {
                let content = load_content().await?;
                Ok(content)
            }}
        />
    }?)
}

async fn load_content() -> Result<Content, AppError> {
    // Read from a database, filesystem, or another Rust service here.
    Ok(Content {
        title: "Loaded from Rust".into(),
        body: "This content was returned by a Rust closure.".into(),
    })
}

// The same macro can describe the page component when the frontend source is
// authored in the Rust application. SolidJS remains the browser renderer.
tsx! {
    import { createSignal, For, Show } from "solid-js";

    type Todo = {
        id: number;
        title: string;
        done: boolean;
    };

    type Metrics = {
        total: number;
        pending: number;
    };

    type AppProps = {
        title: string;
        todos: Todo[];
        metrics: Metrics;
        on_load_content: () => Promise<Content>;
    };

    type Content = {
        title: string;
        body: string;
    };

    export default function App(props: AppProps) {
        const [content, setContent] = createSignal<Content>();
        const [loading, setLoading] = createSignal(false);

        async function loadContent() {
            setLoading(true);
            try {
                setContent(await props.on_load_content());
            } finally {
                setLoading(false);
            }
        }

        return (
            <main>
                <h1>{props.title}</h1>
                <p>{props.metrics.pending} pending</p>

                <button onClick={loadContent} disabled={loading()}>
                    {loading() ? "Loading..." : "Load content"}
                </button>

                <Show when={content()}>
                    {(value) => (
                        <article>
                            <h2>{value().title}</h2>
                            <p>{value().body}</p>
                        </article>
                    )}
                </Show>

                <Show when={props.todos.length > 0} fallback={<p>No todos.</p>}>
                    <ul>
                        <For each={props.todos}>
                            {(todo) => <li>{todo.title}</li>}
                        </For>
                    </ul>
                </Show>
            </main>
        );
    }
}

// Reference names for the future generated/runtime API.
trait IntoResponse {}
struct AppError;
