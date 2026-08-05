# Todos TSX

This example defines the `tsx!` API for a SolidJS + TypeScript frontend.
The macro accepts TSX tokens directly. Application code supplies Rust values as
props; `tsx!` performs the `serde_json` conversion automatically.

The frontend has no separate source project. `build.rs` walks the example
source tree, discovers Rust route `tsx!` blocks and `src/routes/**/*.tsx` modules,
creates its temporary SolidJS/Vite build workspace under Cargo `OUT_DIR`,
compiles it, and embeds the final HTML into the Rust binary.

`AppBuilder` mounts the TSX callback router automatically. Applications only
declare the page route; they do not write callback handlers or callback routes.

`{...}` remains a SolidJS expression. `${...}` interpolates a Rust value or
closure and is serialized or registered automatically by the macro.

```rust
use damascus::prelude::*;
use damascus_tsx::tsx;

pub async fn handler() -> impl IntoResponse {
    tsx! {
        <App
            todos=${load_todos()}
            title=${"Todos from Rust"}
            metrics=${load_metrics()}
        />
    }
}
```

The application does not write `serde_json::to_value` at the call site. The
macro generates the equivalent serialization internally:

```rust
let __todos = serde_json::to_value(&state.todos)?;
let __title = serde_json::to_value(&state.title)?;
let __metrics = serde_json::to_value(&state.metrics)?;
```

Pages are declared by matching files under `src/routes/`. The Rust file owns
the page response and Rust data; the TSX file owns the rendered page:

```text
src/routes/index.rs   -> /
src/routes/index.tsx  -> /
src/routes/about.tsx  -> /about
src/routes/api/health.rs -> /api/health (standalone Rust route)
src/routes/rust_page.rs -> /rust_page (standalone Rust UI route)
```

There is no `routes/mod.rs`, `page.rs`, or manual route registration. The
build script walks the route tree and generates the router included by
`src/main.rs`.

Rust route files do not require a matching TSX file. For example,
`src/routes/api/health.rs` is registered directly as `GET /api/health` and
returns JSON from Rust.

Rust can also serve a page directly with an inline TSX fragment. The
`src/routes/rust_page.rs` example uses `tsx! { ... }` and is compiled into an
inline Solid component; it has no matching `rust_page.tsx` file. Its `${...}`
bindings are serialized into the page bootstrap and read by the generated
component as `props.values.*`.

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

Run the page with:

```bash
cargo run -p todos-tsx
```

It serves the page at `http://127.0.0.1:3002`.
