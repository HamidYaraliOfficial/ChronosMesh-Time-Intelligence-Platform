# ChronosMesh — Test Suites

Each service owns its own tests, run with its native toolchain:

| Service | Location | Command |
|---|---|---|
| Rust Time Engine | `rust-core/tests/` | `cd rust-core && cargo test` |
| Go Scheduler | `scheduler/internal/**/*_test.go` | `cd scheduler && go test ./...` |
| C# Backend | `backend/tests/ChronosMesh.Tests/` | `cd backend && dotnet test` |
| Web App | `web/__tests__/` | `cd web && npm test` |
| Desktop Client | `desktop/tests/` | `cmake --build desktop/build --target test` (or `ctest` in `desktop/build`) |

CI should run all five in parallel; the Rust and Go suites are the most
critical since they contain the timezone/DST/leap-year and concurrency
correctness tests respectively.
