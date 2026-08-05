# DamascusUI

Cross-platform UI framework. Rust core + SolidJS full UI + Avalonia desktop webview viewer.

## Architecture

```
Browser ───┐
           ├── SolidJS UI (the full UI layer)
Avalonia ──┘   (webview desktop viewer)
     │
     │  HTTP/WebSocket
     │
Rust Backend (compio or tokio)
```

- **SolidJS** is the primary UI — runs in any browser and inside the Avalonia desktop viewer.
- **Avalonia** is a thin desktop shell — embeds the SolidJS app via WebView in a native window.
- **Rust** backend serves APIs, shared via WIT protocol definitions.

## Structure

```
DamascusUI/
├── core/          # Rust framework and damascus CLI
├── ui/            # SolidJS web frontend
├── shell/         # Avalonia desktop viewer (WebView shell)
├── wit/           # WIT protocol definitions
├── examples/      # Example applications
│   └── todos/     # Full-stack todo app
└── docs/          # Architecture and design docs
```

## Quick Start

### Rust core

```bash
# compio runtime (default)
cargo run -p damascus

# tokio runtime
cargo run -p damascus --no-default-features -F tokio
```

### SolidJS UI (primary UI)

```bash
cd ui
pnpm install
pnpm dev        # → http://127.0.0.1:3000
```

### Avalonia desktop viewer

```bash
cd shell
dotnet run -- --url http://127.0.0.1:3000
```

### Example: Todos app

```bash
# Terminal 1 — Rust API backend
cargo run -p todos        # → http://127.0.0.1:3001

# Terminal 2 — SolidJS UI
cd examples/todos/ui
pnpm install
pnpm dev                          # → http://127.0.0.1:5173
```

## Features

- **Dual async runtime**: `compio` (IOCP/io_uring) or `tokio` (work-stealing)
- **Protocol-first**: WIT definitions shared across stacks
- **SolidJS primary UI**: fine-grained reactive web frontend
- **Avalonia desktop viewer**: thin WebView wrapper, pure code (no XAML)
