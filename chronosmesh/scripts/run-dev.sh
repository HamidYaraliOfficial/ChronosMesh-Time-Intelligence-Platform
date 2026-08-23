#!/usr/bin/env bash
# ChronosMesh — start the full backend stack (Postgres, Redis, Rust Core,
# Go Scheduler, C# Backend, Next.js Web) via Docker Compose for local
# development. The Desktop Client is run separately (see README).
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/docker/.env"

if [ ! -f "$ENV_FILE" ]; then
  echo "docker/.env not found — copying from docker/.env.example."
  echo "Edit docker/.env with real secrets before running in anything but local dev!"
  cp "$ROOT_DIR/docker/.env.example" "$ENV_FILE"
fi

cd "$ROOT_DIR/docker"
docker compose --env-file .env up --build
