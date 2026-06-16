// Bridge from the SolidJS frontend to the Avalonia desktop viewer.
// The viewer runs a local REST server on port 45769.

const VIEWER = "http://127.0.0.1:45769";

async function post(path: string, body: string) {
  try {
    await fetch(`${VIEWER}/${path}`, { method: "POST", body });
  } catch {
    // viewer not available (running in browser)
  }
}

export function setWindowTitle(title: string) {
  post("set-title", title);
}

export function openWindow(title: string, url: string) {
  post("open-window", JSON.stringify({ title, url }));
}

export function setMenuBar(items: { kind: string; label?: string; items?: unknown[] }[]) {
  post("set-menu", JSON.stringify(items));
}
