set shell := ["zsh", "-cu"]

# List available recipes
default:
    @just --list

# ── Setup ──────────────────────────────────────────────────────────────────────

# Install all dependencies (Rust + frontend)
setup:
    cd ts && pnpm install

# ── Build ──────────────────────────────────────────────────────────────────────

# Build Rust crate (compio runtime, default)
build:
    cargo build -p damascus

# Build Rust crate (tokio runtime)
build-tokio:
    cargo build -p damascus --no-default-features -F tokio

# Build frontend
build-fe:
    cd ts && pnpm build

# Build example todos backend
build-todos:
    cargo build -p todos

# Build example todos frontend
build-todos-fe:
    cd examples/todos/frontend && pnpm install && pnpm build

# Build Avalonia desktop viewer
build-viewer:
    cd viewer && dotnet publish -c Release -o publish

# Build everything (frontend → viewer → Rust)
build-all: build-fe build-viewer
    cargo build -p damascus --no-default-features -F tokio

# ── Check ──────────────────────────────────────────────────────────────────────

# Type-check + compile all targets
check:
    cargo check -p damascus
    cargo check -p damascus --no-default-features -F tokio
    cargo check -p todos

# Type-check frontend
check-fe:
    cd ts && pnpm exec tsc --noEmit

# ── Dev ────────────────────────────────────────────────────────────────────────

# Start frontend dev server (Vite HMR)
dev-fe:
    cd ts && pnpm dev

# Start example todos frontend
dev-todos-fe:
    cd examples/todos/frontend && pnpm dev

# Launch Avalonia desktop viewer (requires backend on :3000)
dev-viewer:
    cd csharp && dotnet run

# Start example todos backend
dev-todos:
    cargo run -p todos

# Full-stack dev: Vite HMR + Rust backend with watch
dev *args=".":
    @echo "==> Starting full-stack dev mode"
    @echo "    Frontend: http://localhost:3000"
    @echo "    Backend:  http://localhost:3001"
    cargo watch -w core/src -x "run -p damascus -- serve" &
    cd ts && pnpm dev

# ── Codegen ────────────────────────────────────────────────────────────────────

# Generate Rust/TS/C# from a WIT file
codegen file:
    cargo run -p damascus -- codegen {{ file }}

# ── Lint / Format ──────────────────────────────────────────────────────────────

# Check Rust formatting
fmt-check:
    cargo fmt --all --check

# Apply Rust formatting
fmt:
    cargo fmt --all

# Run clippy
lint:
    cargo clippy --all-targets -- -D warnings

# ── Clean ──────────────────────────────────────────────────────────────────────

# Remove Rust build artifacts
clean:
    cargo clean

# Remove frontend build output
clean-fe:
    rm -rf ui/dist examples/todos/ui/dist

# Remove all build artifacts
clean-all: clean clean-fe
