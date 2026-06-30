type MenuItem = { kind: string; label?: string; id?: string; items?: MenuItem[] };
type EventHandler = (payload: unknown, fromWindowId: number) => void;
type PendingMessage = Record<string, unknown>;

let socket: WebSocket | null = null;
let registeredWindowId: number | null = null;
let reconnectTimer: number | null = null;
let isConnected = false;
const pendingMessages: PendingMessage[] = [];
const subscriptions = new Map<string, Set<EventHandler>>();
const subscribedTopics = new Set<string>();

function currentWindowId(): number | null {
  if (registeredWindowId !== null) {
    return registeredWindowId;
  }

  const params = new URLSearchParams(window.location.search);
  const raw = params.get("shellWindowId");
  if (!raw) {
    return null;
  }

  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function shellPort(): number {
  const params = new URLSearchParams(window.location.search);
  const raw = params.get("shellPort");
  if (!raw) return 45769;
  const p = Number.parseInt(raw, 10);
  return Number.isFinite(p) ? p : 45769;
}

const VIEWER_WS_BASE = `ws://127.0.0.1:`;

function connect() {
  if (socket || typeof window === "undefined") {
    return;
  }

  const wsUrl = `${VIEWER_WS_BASE}${shellPort()}/ws`;
  try {
    socket = new WebSocket(wsUrl);
  } catch {
    socket = null;
    return;
  }

  socket.addEventListener("open", () => {
    isConnected = true;
    sendRaw({ type: "register", windowId: currentWindowId() });
    for (const topic of subscribedTopics) {
      sendRaw({ type: "subscribe", topic });
    }
    flushPending();
  });

  socket.addEventListener("message", (event) => {
    const data = JSON.parse(String(event.data)) as {
      type?: string;
      topic?: string;
      payload?: unknown;
      fromWindowId?: number;
      windowId?: number;
    };

    if (data.type === "registered" && typeof data.windowId === "number") {
      registeredWindowId = data.windowId;
      return;
    }

    if (data.type !== "event" || !data.topic) {
      return;
    }

    const handlers = subscriptions.get(data.topic);
    if (!handlers) {
      return;
    }

    for (const handler of handlers) {
      handler(data.payload, data.fromWindowId ?? 0);
    }
  });

  socket.addEventListener("close", () => {
    isConnected = false;
    socket = null;
    scheduleReconnect();
  });

  socket.addEventListener("error", () => {
    if (socket) {
      socket.close();
    }
  });
}

function scheduleReconnect() {
  if (reconnectTimer !== null) {
    return;
  }

  reconnectTimer = window.setTimeout(() => {
    reconnectTimer = null;
    connect();
  }, 1000);
}

function flushPending() {
  while (pendingMessages.length > 0) {
    const message = pendingMessages.shift();
    if (message) {
      sendRaw(message);
    }
  }
}

function sendRaw(message: PendingMessage) {
  if (!socket || socket.readyState !== WebSocket.OPEN || !isConnected) {
    pendingMessages.push(message);
    return;
  }

  socket.send(JSON.stringify(message));
}

function send(type: string, payload: Record<string, unknown>) {
  connect();
  sendRaw({ type, ...payload });
}

export function setWindowTitle(title: string) {
  send("set-title", { title });
}

export function setWindowProps(opts: { transparent?: boolean; decorations?: boolean }) {
  send("set-window-props", {
    transparent: opts.transparent,
    decorations: opts.decorations,
  });
}

export function openWindow(title: string, url: string) {
  send("open-window", { title, url });
}

export function closeWindow() {
  send("close-window", {});
}

export function minimizeWindow() {
  send("minimize-window", {});
}

export function toggleMaximize() {
  send("toggle-maximize", {});
}

export function setMenuBar(items: MenuItem[]) {
  send("set-menu", { items });
}

export function setDockBadge(text: string) {
  send("set-dock-badge", { text });
}

export function setDockIcon(base64Image: string) {
  send("set-dock-icon", { icon: base64Image });
}

export function showSystemNotification(
  title: string,
  body: string,
  options?: { topic?: string; payload?: unknown },
) {
  send("show-notification", {
    title,
    body,
    topic: options?.topic,
    payload: options?.payload,
  });
}

export function subscribe(topic: string, handler: EventHandler): () => void {
  connect();
  subscribedTopics.add(topic);
  send("subscribe", { topic });

  const handlers = subscriptions.get(topic) ?? new Set<EventHandler>();
  handlers.add(handler);
  subscriptions.set(topic, handlers);

  return () => {
    const currentHandlers = subscriptions.get(topic);
    if (!currentHandlers) {
      return;
    }

    currentHandlers.delete(handler);
    if (currentHandlers.size === 0) {
      subscriptions.delete(topic);
      subscribedTopics.delete(topic);
      send("unsubscribe", { topic });
    }
  };
}

export function publish(
  topic: string,
  payload: unknown,
  options?: { scope?: "broadcast" | "except-self" | "self" | "window"; targetWindowId?: number },
) {
  send("publish", {
    topic,
    payload,
    scope: options?.scope ?? "broadcast",
    targetWindowId: options?.targetWindowId,
  });
}

connect();
