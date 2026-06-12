# DamascusUI Design

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     Frontends                                 │
│  ┌──────────────┐  ┌──────────────────────────────────────┐ │
│  │   Browser    │  │  Avalonia Desktop Viewer             │ │
│  │  SolidJS UI  │  │  WebView ── SolidJS UI              │ │
│  └──────┬───────┘  └──────────────┬───────────────────────┘ │
│         │                         │                          │
│         │   WebSocket + JSON      │  (same protocol)         │
│         │   (protocol messages)   │                          │
│         └──────────┬──────────────┘                          │
└────────────────────┼─────────────────────────────────────────┘
                     │
┌────────────────────┼─────────────────────────────────────────┐
│             Rust Backend                                     │
│  ┌─────────────────┴──────────────────────────────────────┐  │
│  │  damascus-core                                         │  │
│  │  App · Config · protocol types                         │  │
│  │  compio / tokio · cyper-axum / axum                    │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

## Protocols (`wit/`)

All communication uses WebSocket with JSON messages carrying a `"type"` discriminator.
WIT files define the canonical schema; Rust/TS/C# mirrors are kept in sync:

| WIT file     | Direction            | Purpose                                   |
|-------------|---------------------|-------------------------------------------|
| `types.wit`  | shared               | Point, Size, Rect, Color, SurfaceId       |
| `window.wit` | backend↔frontend     | Open, close, resize, properties, DPI      |
| `render.wit` | backend → frontend   | Frame delivery with damage tracking       |
| `input.wit`  | frontend → backend   | Mouse, keyboard, touch, clipboard, drag   |
| `menu.wit`   | backend → frontend   | Menu bar, context menu descriptions       |
| `dialog.wit` | backend↔frontend     | File open/save, message boxes             |
| `tray.wit`   | backend↔frontend     | System tray icon, notifications           |
| `world.wit`  | —                    | Ties all interfaces into one world        |

### Message format

Every message is a JSON object with a `"type"` field:

```json
{ "type": "render.frame", "surfaceId": 1, "sequence": 42, ... }
{ "type": "input.key", "surfaceId": 1, "key": "Enter", "pressed": true, ... }
```

## Runtime Selection

| Feature  | Runtime     | serve function | Best for                    |
|----------|-------------|----------------|-----------------------------|
| `compio` | compio      | cyper_axum::serve | Windows IOCP, Linux io_uring|
| `tokio`  | tokio       | axum::serve       | General purpose, ecosystem  |

Default is `compio`. Switch with `--no-default-features -F tokio`.

## Using the framework

```rust
use damascus_core::prelude::*;
use damascus_core::{App, Config};

fn main() {
    let app = App::builder()
        .config(Config::new().port(3001))
        .route("/api/data", get(handler))
        .layer(Extension(state))
        .build();
    app.run().unwrap();
}
```

No tokio/axum/compio details are leaked to user code.
