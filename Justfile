set shell := ["bash", "-cu"]

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
    cd shell && dotnet build -c Release -r osx-arm64 --self-contained
    mkdir -p shell/publish
    cp -R shell/bin/Release/net10.0-macos/osx-arm64/DamascusUI.app shell/publish/DamascusUI.app
    cp shell/damascus.json shell/publish/damascus.json
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/favicon.ico; fi
    if [ -f shell/damascus.json ]; then cp shell/damascus.json shell/publish/DamascusUI.app/Contents/Resources/damascus.json; fi
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/DamascusUI.app/Contents/Resources/favicon.ico; fi

build: build-fe build-shell build-core

# ── Run (release) ──────────────────────────────────────────────────────────────

run config="shell/damascus.json" url="http://127.0.0.1:3000" spawn="":
    cp {{config}} shell/damascus.json
    cd shell && dotnet build -c Release -r osx-arm64 --self-contained
    mkdir -p shell/publish
    cp -R shell/bin/Release/net10.0-macos/osx-arm64/DamascusUI.app shell/publish/DamascusUI.app
    cp shell/damascus.json shell/publish/damascus.json
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/favicon.ico; fi
    if [ -f shell/damascus.json ]; then cp shell/damascus.json shell/publish/DamascusUI.app/Contents/Resources/damascus.json; fi
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/DamascusUI.app/Contents/Resources/favicon.ico; fi
    if [ -n "{{spawn}}" ]; then shell/publish/DamascusUI.app/Contents/MacOS/DamascusUI --url {{url}} --spawn {{spawn}}; else shell/publish/DamascusUI.app/Contents/MacOS/DamascusUI --url {{url}}; fi

run-core:
    cargo run -p damascus --release

run-shell url="http://127.0.0.1:3000":
    shell/publish/DamascusUI.app/Contents/MacOS/DamascusUI --url {{url}}

# ── Package ────────────────────────────────────────────────────────────────────

package app_bin config:
    cp {{config}} shell/damascus.json
    rm -rf shell/publish
    cd shell && dotnet publish -c Release -o publish --self-contained -r osx-arm64
    mkdir -p shell/publish/app
    cp -R shell/bin/Release/net10.0-macos/osx-arm64/DamascusUI.app shell/publish/app/DamascusUI.app
    cp shell/damascus.json shell/publish/app/damascus.json
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/app/favicon.ico; fi
    if [ -f shell/damascus.json ]; then cp shell/damascus.json shell/publish/app/DamascusUI.app/Contents/Resources/damascus.json; fi
    if [ -f shell/favicon.ico ]; then cp shell/favicon.ico shell/publish/app/DamascusUI.app/Contents/Resources/favicon.ico; fi
    @echo "Done: shell/publish/app/DamascusUI.app + shell/publish/DamascusUI-1.0.pkg + {{app_bin}}"

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
