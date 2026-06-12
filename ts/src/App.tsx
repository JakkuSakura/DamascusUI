import { createSignal } from "solid-js";

export default function App() {
  const [count, setCount] = createSignal(0);

  return (
    <main style={{ padding: "2rem", "font-family": "system-ui, sans-serif" }}>
      <h1>DamascusUI</h1>
      <p>SolidJS frontend</p>
      <button onClick={() => setCount(count() + 1)}>
        Count: {count()}
      </button>
    </main>
  );
}
