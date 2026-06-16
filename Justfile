set shell := ["zsh", "-cu"]

default:
    @just --list

# ── Dev ────────────────────────────────────────────────────────────────────────

dev-fe:
    cd ui && pnpm dev

dev-core *args:
    cargo run -p damascus -- {{args}}

dev-shell url="http://127.0.0.1:3000":
    cd shell && dotnet run -- --url {{url}}

# ── Build (release) ────────────────────────────────────────────────────────────

build-core:
    cargo build -p damascus --no-default-features -F tokio --release

build-fe:
    cd ui && pnpm build

build-shell config="shell/damascus.json":
    cp {{config}} shell/damascus.json
    cd shell && dotnet publish -c Release -o publish --self-contained -r osx-arm64

build: build-fe build-shell build-core

# ── Run (release) ──────────────────────────────────────────────────────────────

run-core:
    cargo run -p damascus --release

run-shell url="http://127.0.0.1:3000":
    shell/publish/DamascusUI --url {{url}}

# ── Package ────────────────────────────────────────────────────────────────────

package app_bin config:
    cp {{config}} shell/damascus.json
    cd shell && dotnet publish -c Release -o publish --self-contained -r osx-arm64
    @echo "Done: shell/publish/DamascusUI + {{app_bin}}"

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
    rm -rf shell/publish ui/dist
