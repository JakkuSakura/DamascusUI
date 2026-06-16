set shell := ["zsh", "-cu"]

# List available recipes
default:
    @just --list

# ── Setup ──────────────────────────────────────────────────────────────────────

# Install all dependencies (Rust + ui)
setup:
    cd ts && pnpm install

# ── Build ──────────────────────────────────────────────────────────────────────

# Build Rust crate (compio runtime, default)
build:
    cargo build -p damascus

# Build Rust crate (tokio runtime)
build-tokio:
    cargo build -p damascus --no-default-features -F tokio

# Build ui
build-fe:
    cd ts && pnpm build

# Build example todos core
build-todos:
    cargo build -p todos

# Build example todos ui
build-todos-fe:
    cd examples/todos/ui && pnpm install && pnpm build

# Build Avalonia desktop shell
build-shell:
    cd shell && dotnet publish -c Release -o publish --self-contained -r osx-arm64
    mkdir -p shell/publish/DamascusUI.app/Contents/MacOS
    cp -a shell/publish/* shell/publish/DamascusUI.app/Contents/MacOS/ 2>/dev/null || true
    cp shell/Info.plist shell/publish/DamascusUI.app/Contents/
    codesign --force --deep --entitlements shell/Entitlements.plist -s - shell/publish/DamascusUI.app

# Build everything (ui → shell → Rust)
build-all: build-fe build-shell
    cargo build -p damascus --no-default-features -F tokio

# ── Check ──────────────────────────────────────────────────────────────────────

# Type-check + compile all targets
check:
    cargo check -p damascus
    cargo check -p damascus --no-default-features -F tokio
    cargo check -p todos

# Type-check ui
check-fe:
    cd ts && pnpm exec tsc --noEmit

# ── Dev ────────────────────────────────────────────────────────────────────────

# Start ui dev server (Vite HMR)
dev-fe:
    cd ts && pnpm dev

# Start example todos ui
dev-todos-fe:
    cd examples/todos/ui && pnpm dev

# Launch Avalonia desktop shell (requires core on :3000)
dev-shell:
    cd csharp && dotnet run

# Start example todos core
dev-todos:
    cargo run -p todos

# Full-stack dev: Vite HMR + Rust core with watch
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

# Remove ui build output
clean-fe:
    rm -rf ui/dist examples/todos/ui/dist

# Remove all build artifacts
clean-all: clean clean-fe
