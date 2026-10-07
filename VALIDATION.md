# Validation results — 7 October 2026

Environment: macOS ARM64, .NET SDK 8.0.422 / runtime 8.0.28, Docker 29.6.1, SQL Server 2022 container using linux/amd64 emulation.

| Check | Result |
|---|---|
| Solution compilation | Passed; all projects compiled in the final `dotnet test` run |
| xUnit / Moq / WebApplicationFactory | **14 passed, 0 failed, 0 skipped** |
| SQL Server migration application | Passed; database and demo users created through startup initialization |
| Real SQL Server API requests | Passed: login, Product creation 201, Item creation 201, paginated Product GET 200, validation 400 |
| Refresh rotation and replay against SQL Server | Passed: rotation succeeds, old token rejected, replacement rejected after family revocation |
| Concurrent SQL Server-backed refresh requests | Passed: one 200 and one 401; winning replacement rejected afterward. This HTTP-level check does not deterministically force both EF reads to occur before either write. |
| Swagger | Passed: concrete `/api/v1/...` paths, public login, protected Products, XML descriptions, successful local request |
| Error middleware / logging | Passed: invalid login returns 401 and is logged as 401; malformed JSON and validation errors use Problem Details |
| Response compression and security headers | Observed in real Swagger GET: Brotli, no-store, nosniff, frame denial, no-referrer, supported API version |
| Dependency advisory check | No known matches among **151 resolved package versions** in NuGet's official advisory feed (updated 6 October 2026) |

Evidence: [test results](artifacts/test-results.json), [dependency versions and advisory report](artifacts/dependency-audit.json), [SQL migration script](artifacts/migrations.sql), [actual local screenshot](docs/screenshots/local-swagger.jpg).

The initial advisory check identified a vulnerable test-only SQLite native package. The SQLite bundle was upgraded to 3.0.3, and the final tests passed with that dependency. The final CLI advisory command stalled; the recorded check compares all resolved package versions directly with the official NuGet vulnerability feed. No known matches is not a guarantee that dependencies have no undiscovered vulnerabilities.

## Boundaries and environment limitations

- The SQL Server Compose service was actually started and used. The full API Docker image and the complete API+database Compose stack were **not built/run** here; the API was executed with the local .NET runtime against containerized SQL Server. Run `docker compose up --build` to verify the full container stack before relying on it for deployment.
- SQLite integration tests exercise relational behavior but do not prove all SQL Server-specific behavior. Separate real SQL Server checks above cover migration/startup and selected authentication/product/item operations.
- Production hosting and TLS deployment have not been verified. HTTPS/TLS, trusted proxy settings, backups, and production migration jobs are described in README.
- Test credentials and generated build outputs are excluded from version control. Use README to start a local instance.
