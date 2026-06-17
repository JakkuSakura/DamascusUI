import { createSignal, createResource, For, Show, onMount, createEffect, onCleanup } from "solid-js";
import { setWindowTitle, setMenuBar, openWindow, setDockBadge, publish, showSystemNotification, subscribe } from "../../../../ui/src/bridge";

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
  const [t, setT] = createSignal("");
  const [notificationWarning, setNotificationWarning] = createSignal("");

  onMount(() => {
    setWindowTitle("Todos");
    setMenuBar([
      { kind: "action", label: "New Window", id: "new" },
      { kind: "separator" },
      { kind: "action", label: "About Todos", id: "about" },
    ]);

    const unsubscribe = subscribe("todos.window-opened", () => {
      refetch();
    });
    const unsubscribeMenuNew = subscribe("menu.new-window", () => {
      refetch();
    });
    const unsubscribeMenuAbout = subscribe("menu.about", (payload) => {
      if (payload && typeof payload === "object" && "message" in payload) {
        const message = String((payload as { message: unknown }).message);
        window.alert(message);
      }
    });
    const unsubscribeNotification = subscribe("notification.clicked", (payload) => {
      if (payload && typeof payload === "object" && "title" in payload) {
        const title = String((payload as { title: unknown }).title);
        window.alert(`Notification clicked: ${title}`);
      }
    });
    const unsubscribeNotificationWarning = subscribe("notification.warning", (payload) => {
      if (payload && typeof payload === "object" && "message" in payload) {
        setNotificationWarning(String((payload as { message: unknown }).message));
      }
    });
    onCleanup(unsubscribe);
    onCleanup(unsubscribeMenuNew);
    onCleanup(unsubscribeMenuAbout);
    onCleanup(unsubscribeNotification);
    onCleanup(unsubscribeNotificationWarning);
  });

  createEffect(() => {
    const list = todos();
    if (list) {
      const pending = list.filter((t: Todo) => !t.done).length;
      setDockBadge(pending > 0 ? String(pending) : "");
    }
  });

  async function addTodo(e: SubmitEvent) {
    e.preventDefault();
    const text = t().trim();
    if (!text) return;
    await fetch(API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title: text }),
    });
    setT("");
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

  const pending = () => {
    const list = todos();
    return list ? list.filter((t: Todo) => !t.done) : [];
  };
  const done = () => {
    const list = todos();
    return list ? list.filter((t: Todo) => t.done) : [];
  };

  return (
    <div class="h-screen flex flex-col p-3 gap-3 select-none window-glass" style="border-radius: 16px; overflow: hidden;">
      {/* Title bar — draggable */}
      <div class="titlebar glass-sm px-4 py-3 flex items-center shrink-0">
        <h1 class="text-sm font-semibold tracking-wide text-gray-300 flex-1">Todos</h1>
        <span class="text-xs text-gray-500 tabular-nums">
          {pending().length} remaining
        </span>
      </div>

      {/* Add form */}
      <div class="shrink-0">
        <form onSubmit={addTodo} class="flex gap-2">
          <input
            type="text"
            value={t()}
            onInput={(e) => setT(e.currentTarget.value)}
            placeholder="What needs to be done?"
            class="flex-1 px-4 py-2.5 glass-input text-sm"
          />
          <button type="submit"
            class="px-5 py-2.5 glass text-sm font-medium text-gray-300 hover:text-gray-100 hover-glass cursor-pointer">
            Add
          </button>
        </form>
      </div>

      {/* Todo list */}
      <div class="flex-1 overflow-y-auto space-y-1.5 pr-0.5">
        <Show when={!todos.loading} fallback={
          <div class="glass-sm px-4 py-8 text-center text-sm text-gray-500">Loading...</div>
        }>
          <Show when={todos() && (pending().length > 0 || done().length > 0)} fallback={
            <div class="glass-sm px-4 py-12 text-center text-sm text-gray-500">
              No todos yet. Add one above!
            </div>
          }>
            <For each={pending()}>
              {(todo) => <TodoItem todo={todo} onToggle={toggleTodo} onDelete={deleteTodo} />}
            </For>
            <Show when={done().length > 0 && pending().length > 0}>
              <div class="py-2 px-4">
                <div class="border-t border-gray-700/30" />
              </div>
            </Show>
            <For each={done()}>
              {(todo) => <TodoItem todo={todo} onToggle={toggleTodo} onDelete={deleteTodo} />}
            </For>
          </Show>
        </Show>
      </div>

      {/* Actions */}
      <div class="shrink-0 flex gap-2">
        <button onClick={() => {
          openWindow("Todos", window.location.href);
          publish("todos.window-opened", { openedAt: Date.now() }, { scope: "except-self" });
        }}
          class="flex-1 py-2.5 glass-sm text-xs font-medium text-gray-400 hover:text-gray-200 hover-glass cursor-pointer
                 text-center">
          New Window
        </button>
        <button onClick={() => {
          showSystemNotification("Todos", "Pending todos are waiting", {
            topic: "notification.clicked",
            payload: { title: "Todos", source: "todos-demo" },
          });
        }}
          class="flex-1 py-2.5 glass-sm text-xs font-medium text-gray-400 hover:text-gray-200 hover-glass cursor-pointer
                 text-center">
          Notify
        </button>
      </div>

      <Show when={notificationWarning()}>
        <div class="glass-sm px-4 py-3 text-xs text-amber-300/80 shrink-0">
          {notificationWarning()}
        </div>
      </Show>
      {/* Resize handles for frameless window */}
      <ResizeHandles />
    </div>
  );
}

function TodoItem(props: {
  todo: Todo;
  onToggle: (t: Todo) => void;
  onDelete: (id: number) => void;
}) {
  return (
    <div class={`glass-sm px-3 py-2.5 flex items-center gap-3 group hover-glass cursor-pointer
      ${props.todo.done ? "opacity-50" : ""}`}
      onClick={() => props.onToggle(props.todo)}>
      <div class={`w-4 h-4 rounded-full border-2 flex-shrink-0 flex items-center justify-center
        transition-all duration-200
        ${props.todo.done
          ? "border-blue-400/60 bg-blue-400/20"
          : "border-gray-600 group-hover:border-gray-400"}`}>
        <Show when={props.todo.done}>
          <svg viewBox="0 0 24 24" fill="none" stroke="rgb(107 140 255)" stroke-width="3"
            class="w-2.5 h-2.5">
            <path d="M5 13l4 4L19 7" />
          </svg>
        </Show>
      </div>
      <span class={`flex-1 text-sm transition-all duration-200
        ${props.todo.done ? "line-through text-gray-600" : "text-gray-300"}`}>
        {props.todo.title}
      </span>
      <button onClick={(e) => { e.stopPropagation(); props.onDelete(props.todo.id); }}
        class="opacity-0 group-hover:opacity-100 text-gray-600 hover:text-red-400
               transition-all duration-200 cursor-pointer text-xs w-5 h-5 flex items-center justify-center
               rounded-full hover:bg-gray-700/50">
        ✕
      </button>
    </div>
  );
}

function ResizeHandles() {
  const edges = ["n", "s", "e", "w", "ne", "nw", "se", "sw"] as const;
  return (
    <>
      {edges.map((e) => (
        <div class={`resize-handle resize-${e}`} />
      ))}
    </>
  );
}
