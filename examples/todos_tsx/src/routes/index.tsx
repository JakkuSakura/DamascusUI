import { createSignal, For, Show } from "solid-js";

type Todo = { id: number; title: string; done: boolean };
type Content = { title: string; body: string };

type AppProps = {
  values: {
    title: string;
    todos: Todo[];
    metrics: { total: number; pending: number };
  };
  on_load_content: () => Promise<Content>;
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
      <p class="eyebrow">DamascusUI / TSX routes</p>
      <h1>{props.values.title}</h1>
      <p class="summary">
        {props.values.metrics.pending} of {props.values.metrics.total} todos pending
      </p>

      <section class="panel">
        <h2>Rust data</h2>
        <ul>
          <For each={props.values.todos}>
            {(todo) => <li classList={{ done: todo.done }}>{todo.title}</li>}
          </For>
        </ul>
      </section>

      <button type="button" onClick={loadContent} disabled={loading()}>
        {loading() ? "Loading..." : "Load content from Rust"}
      </button>

      <Show when={content()}>
        {(value) => (
          <section class="panel result">
            <p class="eyebrow">Callback result</p>
            <h2>{value().title}</h2>
            <p>{value().body}</p>
          </section>
        )}
      </Show>
    </main>
  );
}
