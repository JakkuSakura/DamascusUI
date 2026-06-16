import { createSignal, createResource, For, Show, onMount } from "solid-js";
import { setWindowTitle } from "../../../../ui/src/bridge";

interface Todo {
  id: number;
  title: string;
  done: boolean;
}

const API = "/api/todos";

async function fetchTodos(): Promise<Todo[]> {
  const res = await fetch(API);
  return res.json();
}

export default function App() {
  const [todos, { refetch }] = createResource(fetchTodos);
  const [title, setTitle] = createSignal("");

  onMount(() => setWindowTitle("Todos"));

  async function addTodo(e: SubmitEvent) {
    e.preventDefault();
    const t = title().trim();
    if (!t) return;
    await fetch(API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title: t }),
    });
    setTitle("");
    refetch();
  }

  async function toggleTodo(todo: Todo) {
    await fetch(`${API}/${todo.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ done: !todo.done }),
    });
    refetch();
  }

  async function deleteTodo(id: number) {
    await fetch(`${API}/${id}`, { method: "DELETE" });
    refetch();
  }

  return (
    <div class="max-w-xl mx-auto py-12 px-4">
      <h1 class="text-3xl font-bold mb-8 text-center">Todos</h1>

      <form onSubmit={addTodo} class="flex gap-2 mb-6">
        <input
          type="text"
          value={title()}
          onInput={(e) => setTitle(e.currentTarget.value)}
          placeholder="What needs to be done?"
          class="flex-1 px-4 py-2 rounded-lg bg-gray-800 border border-gray-700
                 text-gray-100 placeholder-gray-500 focus:outline-none
                 focus:border-blue-500 transition-colors"
        />
        <button
          type="submit"
          class="px-6 py-2 rounded-lg bg-blue-600 hover:bg-blue-500
                 font-medium transition-colors cursor-pointer"
        >
          Add
        </button>
      </form>

      <Show when={!todos.loading} fallback={<p class="text-gray-500 text-center">Loading...</p>}>
        <ul class="space-y-2">
          <For each={todos()}>
            {(todo) => (
              <li
                class="flex items-center gap-3 px-4 py-3 rounded-lg bg-gray-800/50
                       border border-gray-800 hover:border-gray-700 transition-colors"
              >
                <button
                  onClick={() => toggleTodo(todo)}
                  class={`w-5 h-5 rounded border-2 flex-shrink-0 transition-colors cursor-pointer
                    ${todo.done
                      ? "bg-blue-500 border-blue-500"
                      : "border-gray-600 hover:border-gray-400"
                    }`}
                >
                  <Show when={todo.done}>
                    <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="3">
                      <path d="M5 13l4 4L19 7" />
                    </svg>
                  </Show>
                </button>

                <span
                  class={`flex-1 transition-colors ${
                    todo.done ? "line-through text-gray-500" : "text-gray-100"
                  }`}
                >
                  {todo.title}
                </span>

                <button
                  onClick={() => deleteTodo(todo.id)}
                  class="text-gray-600 hover:text-red-400 transition-colors cursor-pointer
                         px-2 py-1 text-sm"
                >
                  ✕
                </button>
              </li>
            )}
          </For>
        </ul>
      </Show>

      <Show when={todos() && todos()!.length === 0}>
        <p class="text-gray-500 text-center mt-8">No todos yet. Add one above!</p>
      </Show>
    </div>
  );
}
