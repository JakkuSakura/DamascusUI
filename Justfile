set shell := ["zsh", "-cu"]

default:
    @just --list

# ── Setup ──────────────────────────────────────────────────────────────────────

setup:
    cd ui && pnpm install

# ── Build ──────────────────────────────────────────────────────────────────────

build:
    cargo build -p damascus

build-tokio:
    cargo build -p damascus --no-default-features -F tokio

build-fe:
    cd ui && pnpm build

# Build shell with optional per-app config
build-shell config="shell/damascus.json":
    cp {{config}} shell/damascus.json
    cd shell && dotnet publish -c Release -o publish --self-contained -r osx-arm64

build-all: build-fe
    cargo build -p damascus --no-default-features -F tokio
    just build-shell

# ── Dev ────────────────────────────────────────────────────────────────────────

dev-fe:
    cd ui && pnpm dev

# ── Codegen ────────────────────────────────────────────────────────────────────

codegen file:
    cargo run -p damascus -- codegen {{file}}

# ── Lint ───────────────────────────────────────────────────────────────────────

fmt:
    cargo fmt --all

lint:
    cargo clippy --all-targets -- -D warnings

# ── Clean ──────────────────────────────────────────────────────────────────────

clean:
    cargo clean

clean-fe:
    rm -rf ui/dist

clean-all: clean clean-fe
