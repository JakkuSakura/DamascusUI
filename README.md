# DamascusUI

Cross-platform UI framework. Rust backend + SolidJS full frontend + Avalonia desktop webview viewer.

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
├── core/          # Rust core framework (compio or tokio runtime)
├── ui/          # SolidJS web frontend (primary UI)
├── viewer/      # Avalonia desktop viewer (WebView shell)
├── wit/         # WIT protocol definitions (types, render, input, lifecycle)
├── examples/    # Example applications
│   └── todos/   # Full-stack todo app (Rust API + SolidJS + TailwindCSS)
├── scripui/     # Build and codegen scripts
└── docs/        # Architecture and design docs
```

## Quick Start

### Rust backend

```bash
# compio runtime (default)
cargo run -p damascus-core

# tokio runtime
cargo run -p damascus-core --no-default-features -F tokio
```

### SolidJS frontend (primary UI)

```bash
cd ts
pnpm install
pnpm dev        # → http://127.0.0.1:3000
```

### Avalonia desktop viewer

```bash
cd csharp
dotnet run      # loads the SolidJS app in a native window
```

### Example: Todos app

```bash
# Terminal 1 — Rust API backend
cargo run -p todos-backend        # → http://127.0.0.1:3001

# Terminal 2 — SolidJS frontend
cd examples/todos/frontend
pnpm install
pnpm dev                          # → http://127.0.0.1:5173
```

## Features

- **Dual async runtime**: `compio` (IOCP/io_uring) or `tokio` (work-stealing)
- **Protocol-first**: WIT definitions shared across stacks
- **SolidJS primary UI**: fine-grained reactive web frontend
- **Avalonia desktop viewer**: thin WebView wrapper, pure code (no XAML)
