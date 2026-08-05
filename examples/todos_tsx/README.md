# Todos TSX

This example defines the intended `tsx!` API for a SolidJS + TypeScript frontend.
The macro accepts TSX tokens directly. Application code supplies Rust values as
props; `tsx!` performs the `serde_json` conversion automatically.

`{...}` remains a SolidJS expression. `${...}` interpolates a Rust value or
closure and is serialized or registered automatically by the macro.

```rust
use axum::extract::State;
use damascus::prelude::*;

async fn index(State(state): State<AppState>) -> Result<impl IntoResponse, AppError> {
    Ok(tsx! {
        <App
            todos=${state.todos}
            title=${state.title}
            metrics=${state.metrics}
        />
    }?)
}
```

The application does not write `serde_json::to_value` at the call site. The
macro generates the equivalent serialization internally:

```rust
let __todos = serde_json::to_value(&state.todos)?;
let __title = serde_json::to_value(&state.title)?;
let __metrics = serde_json::to_value(&state.metrics)?;
```

The generated page bootstraps SolidJS with JSON data and renders the component
from the embedded TypeScript/SolidJS frontend:

```tsx
import { For } from "solid-js";

type Todo = {
  id: number;
  title: string;
  done: boolean;
};

type AppProps = {
  todos: Todo[];
  title: string;
  metrics: Record<string, number>;
};

export default function App(props: AppProps) {
  return (
    <main>
      <h1>{props.title}</h1>
      <ul>
        <For each={props.todos}>
          {(todo) => <li>{todo.title}</li>}
        </For>
      </ul>
    </main>
  );
}
```

The runtime data flow is:

```text
Rust state
  -> tsx! macro
  -> serde_json bootstrap payload
  -> SolidJS props
  -> TypeScript component
```

## Rust event closures

Rust closures can be passed as event props. The macro registers the closure
with the page, gives the browser an event identifier, and serializes the
closure result back into SolidJS.

```rust
tsx! {
    <button on_click=${move || async move {
        let content = load_content().await?;
        Ok(content)
    }}>
        "Load content"
    </button>
}
```

The generated TypeScript sees a normal async callback:

```tsx
async function loadContent() {
  const content = await props.on_load_content();
  setContent(content);
}
```

The browser does not receive or execute the Rust closure. It sends the event
identifier to Rust, Rust invokes the closure, and the result is returned as
JSON:

```text
button click
  -> event id
  -> Rust closure
  -> serde_json result
  -> SolidJS signal update
```

This directory is a syntax and framework contract until `tsx!` is implemented
in the core macros crate.
