# DamascusUI

Cross-platform UI framework: Rust data backend, SolidJS primary UI, Avalonia WebView desktop viewer.

## Stack

| Layer      | Technology                          |
|------------|-------------------------------------|
| Backend    | Rust, axum (tokio or compio runtime)|
| Web UI     | TypeScript, SolidJS, Vite, Tailwind |
| Desktop    | C# 13, Avalonia 12, WebView shell   |
| Protocols  | WIT (codegen source of truth)       |
| Package mgr| Cargo (Rust), pnpm (TS), NuGet (C#) |

## Commands

```bash
# Rust — compio runtime (default)
cd rs && cargo check
cd rs && cargo check --no-default-features -F tokio

# Example app
cd examples/todos && cargo check -p todos-backend

# Codegen — generates Rust/TS/C# from WIT
cargo run -- codegen examples/todos/wit/todos.wit

# TS frontend
cd ts && pnpm install && pnpm dev

# C# desktop viewer
cd csharp && dotnet build
```

## Conventions

- Commit messages: [Conventional Commits](https://www.conventionalcommits.org/)
- Rust: edition 2024, no `unsafe`
- TypeScript: strict mode, use `~/` path alias for `src/`
- C#: pure code (no XAML), nullable enabled
- WIT: interfaces describe API schemas; `damascus codegen` produces typed clients

## Architecture

```
Browser ───┐
           ├── SolidJS UI ── WebSocket/HTTP ── Rust Backend
Avalonia ──┘   (webview)
```

- Rust backend is a standard data server (REST + WebSocket)
- SolidJS is the full UI layer
- Avalonia is a thin desktop shell embedding the same SolidJS app via WebView
- WIT protocol files live at `wit/` (framework) and `examples/*/wit/` (per-app)
- The `damascus` binary provides `codegen` and `serve` subcommands
- `feature = "compio"` uses `cyper_axum::serve`, `feature = "tokio"` uses `axum::serve`

## Key Files

| Path | Purpose |
|------|---------|
| `rs/src/lib.rs` | Framework crate root |
| `rs/src/main.rs` | CLI binary (codegen + serve) |
| `rs/src/app.rs` | `App` / `AppBuilder` |
| `rs/src/prelude.rs` | Re-exports for user code |
| `rs/src/protocol.rs` | Shared protocol types |
| `rs/src/codegen.rs` | WIT → Rust/TS/C# generator |
| `ts/src/codegen/` | Generated TS API clients |
| `csharp/codegen/` | Generated C# API clients |
| `wit/` | Framework WIT protocols |
