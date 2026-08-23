#!/usr/bin/env bash
# ChronosMesh — one-shot dependency setup for local development.
# Installs/builds each service's dependencies. Run from the repo root:
#   bash scripts/setup.sh
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
echo "==> ChronosMesh setup starting in $ROOT_DIR"

echo "==> [1/5] Rust Secure Core / Time Engine"
if command -v cargo >/dev/null 2>&1; then
  (cd "$ROOT_DIR/rust-core" && cargo build --release)
else
  echo "    cargo not found — install Rust via https://rustup.rs and re-run."
fi

echo "==> [2/5] Go Scheduler Engine"
if command -v go >/dev/null 2>&1; then
  (cd "$ROOT_DIR/scheduler" && go mod download && go build ./...)
else
  echo "    go not found — install Go >= 1.22 from https://go.dev/dl and re-run."
fi

echo "==> [3/5] C# Backend API"
if command -v dotnet >/dev/null 2>&1; then
  (cd "$ROOT_DIR/backend" && dotnet restore ChronosMesh.sln && dotnet build ChronosMesh.sln)
else
  echo "    dotnet not found — install the .NET 8 SDK from https://dotnet.microsoft.com/download and re-run."
fi

echo "==> [4/5] Next.js Web App"
if command -v npm >/dev/null 2>&1; then
  (cd "$ROOT_DIR/web" && npm install)
else
  echo "    npm not found — install Node.js >= 20 from https://nodejs.org and re-run."
fi

echo "==> [5/5] C++/Qt6 Desktop Client"
if command -v cmake >/dev/null 2>&1; then
  mkdir -p "$ROOT_DIR/desktop/build"
  (cd "$ROOT_DIR/desktop/build" && cmake .. -DCMAKE_BUILD_TYPE=Release && cmake --build . --parallel)
else
  echo "    cmake not found, or Qt6 is not installed — install Qt6 (Widgets, Network, Sql, LinguistTools)"
  echo "    from https://www.qt.io/download and CMake >= 3.21, then re-run."
fi

echo "==> Done. Next: copy docker/.env.example to docker/.env and fill in secrets, then run scripts/run-dev.sh"
