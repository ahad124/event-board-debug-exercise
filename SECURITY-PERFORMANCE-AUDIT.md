# Security & Performance Audit — Community Event Board

AI-assisted audit of the Community Event Board API (ASP.NET Core 8 + EF Core). This report
covers the security findings and fixes, before/after performance metrics, and the added
structured logging and health-check endpoints.

- **Repo:** https://github.com/ahad124/event-board-debug-exercise
- **Branch / PR:** `security-performance-audit`
- **Verification:** `dotnet build` clean; **34** tests pass (`--filter "Category!=Benchmark"`).
- **Checklist used:** [AUDIT-CHECKLIST.md](AUDIT-CHECKLIST.md) (AI-generated, OWASP-aligned).

---

## 1. Executive summary

| Area | Result |
|------|--------|
| Security findings fixed | **7** (2 High, 4 Medium, 1 Low/Med) |
| Headline performance win | `GET /events` **493 ms → 21 ms** avg (~23× faster) |
| Structured logging | Serilog JSON + per-request logging |
| Health checks | `/health` (DB readiness) + `/health/live` (liveness) |
| Tests | 29 → **34** (all green) |

---

## 2. Security findings & fixes

Severity uses CVSS-style qualitative bands. Each fix is a focused commit (`fix(sec-Sx)`).

### S1 — Privilege escalation via self-registration (High)
`POST /api/auth/register` accepted a `Role` field that `AuthService.RegisterAsync`
honored against an allow-list including `"Admin"`. Any anonymous user could register as an
administrator.
**Fix:** removed `Role` from `RegisterRequest`; public registration always creates a
`User`. Elevation is admin-only via `UsersController.UpdateRole`.
**Test:** `SecurityIntegrationTests.Register_RequestingAdminRole_CreatesStandardUser`.

### S2 — Overly permissive CORS (High)
When no origins were configured, CORS fell back to
`AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`.
**Fix:** default to a localhost allow-list; real origins come from `Cors:AllowedOrigins`.
Never `AllowAnyOrigin`.

### S3 — Swagger exposed in Production (Medium)
Swagger UI/JSON was mapped in every environment, disclosing the full API surface.
**Fix:** served only in Development or when `EnableSwagger=true`.

### S4 — No rate limiting on auth endpoints (Medium)
Login/registration had no throttling → brute-force / credential stuffing.
**Fix:** built-in fixed-window rate limiter (5 req / 30 s per IP, configurable via
`RateLimiting:AuthPermitLimit`) applied to `AuthController`; returns `429`.
**Test:** `RateLimitIntegrationTests.Login_BeyondLimit_Returns429`.

### S5 — Sensitive data in logs (Medium)
`Microsoft.EntityFrameworkCore: Debug` logged full SQL and parameter values.
**Fix:** base config logs EF/ASP.NET at `Warning`; Development keeps verbose logs locally.

### S6 — Missing security headers (Medium)
No hardening headers; server implementation advertised.
**Fix:** middleware adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer`, `Content-Security-Policy: frame-ancestors 'none'`,
`X-Permitted-Cross-Domain-Policies: none`; Kestrel `Server` header disabled.
**Test:** `SecurityIntegrationTests.Responses_IncludeSecurityHeaders`.

### S7 — JWT secret management (Low/Med)
Signing key lived in committed `appsettings.json` with no validation.
**Fix:** fail-fast at startup outside Development if the key is missing or `<32` bytes.
Production supplies `JWT_KEY` via environment (see `docker-compose.yml`); the appsettings
value is a Development-only placeholder.

> Also reviewed and already sound: BCrypt password hashing, parameterized raw-SQL report,
> per-user ownership of RSVPs/favorites, admin self-demotion guard, and the hardened image
> upload (extension + content-type + size + random filename).

---

## 3. Performance

### Method
A reproducible benchmark (`EventBoard.Api.Tests/PerfBenchmarks.cs`, tagged
`Category=Benchmark`) seeds **~500 events / ~4000 bookings** and times each endpoint over
30 iterations (warm-up excluded), writing `perf-artifacts/before.md` and `after.md`.
Serilog request logging provides a second, in-app source of per-request `Elapsed` ms.

```bash
PERF_LABEL=before dotnet test --filter "Category=Benchmark"   # on the pre-fix commit
PERF_LABEL=after  dotnet test --filter "Category=Benchmark"   # after the fixes
```

### Results

| Endpoint | Before avg (ms) | After avg (ms) | Before p95 | After p95 | Change |
|----------|----------------:|---------------:|-----------:|----------:|--------|
| `GET /api/events` (list) | 493.02 | **21.28** | 761.54 | 29.04 | **~23× faster** |
| `GET /api/events/{id}` (detail) | 5.11 | 4.29 | 8.22 | 8.02 | ~16% faster |
| `GET /api/reports/stats` | 8.99 | 4.99 | 14.51 | 9.48 | ~44% faster |

### Optimizations
- **P1 — `GET /events` pagination + projection.** Was loading every event *and every
  booking* to count RSVPs in memory. Now `GetPagedAsync` materializes only the requested
  page (default 20, cap 100) with `AsNoTracking`. The JSON body stays a plain array
  (frontend-compatible); pagination metadata is returned in `X-Total-Count` / `X-Page` /
  `X-Page-Size` headers.
- **P2 — `/reports/stats` fewer round-trips.** Four separate booking `COUNT` queries
  collapsed into one `GROUP BY` (8 → 5 DB round-trips).
- **P3 — `AsNoTracking`** on read-only queries (list page + detail).

> **Caveat (honest measurement):** the benchmark uses the InMemory EF provider, so the
> numbers reflect in-process work (entity materialization, query count), not real SQL I/O.
> On SQL Server the gains are larger: pagination avoids transferring the entire table, and
> the grouped stats query removes network round-trips. See
> [`perf-artifacts/DOTTRACE-GUIDE.md`](perf-artifacts/DOTTRACE-GUIDE.md) to profile the hot
> path with dotTrace against the real Docker stack.

---

## 4. Structured logging (Serilog)

- `Serilog.AspNetCore` with a JSON console formatter, `Enrich.FromLogContext()`, and
  `UseSerilogRequestLogging()` — one structured line per request with method, path, status
  and elapsed ms. Minimum levels mirror S5 (EF/ASP.NET at `Warning`).
- Example line (fields): `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`.

## 5. Health checks

- `GET /health` — overall status with a **database readiness** check
  (`AddDbContextCheck<AppDbContext>`); JSON body lists each check + duration.
- `GET /health/live` — liveness only (no dependencies).
- `docker-compose.yml` wires a container `HEALTHCHECK` (curl → `/health/live`) and the
  frontend now waits for the API to be **healthy**.

---

## 6. How to verify

```bash
git checkout security-performance-audit
dotnet build EventBoard.sln                      # 0 errors
dotnet test --filter "Category!=Benchmark"       # 34 passing

# Perf (optional):
PERF_LABEL=after dotnet test --filter "Category=Benchmark"
cat perf-artifacts/before.md perf-artifacts/after.md

# Runtime (Docker):
docker-compose up --build
curl -i http://localhost:8080/health             # 200 + JSON
curl -i http://localhost:8080/api/events         # security headers present
# Registering with "role":"Admin" yields a User account (role ignored).
```
