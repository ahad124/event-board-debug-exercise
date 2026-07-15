# Loom Script #1 — Security & Performance (Technical Walkthrough)

**Audience:** engineers / reviewer. **Target length:** 10–13 min. **Screen:** editor +
terminal, repo on branch `security-performance-audit`, `SECURITY-PERFORMANCE-AUDIT.md` open.

Bracketed lines are **[actions]**; quoted lines are what to **say**.

---

## 0:00 — Intro (30s)
> "This is an AI-assisted security and performance audit of the Event Board API. I used an
> AI-generated OWASP checklist — AUDIT-CHECKLIST.md — to drive it, fixed seven security
> findings, optimized the slow endpoints with measured before/after numbers, and added
> structured logging and health checks. Let me walk through it."
**[action]** Show `git log --oneline` — point at the `fix(sec-Sx)`, `perf`, and `feat` commits.

## 0:30 — S1 privilege escalation (the big one) (1.5 min)
**[action]** `git show <sec-S1>` — show `RegisterRequest.Role` removed and the register call.
> "The worst finding: the register endpoint accepted a Role field, and the service honored
> 'Admin'. So anyone could POST register with role Admin and become an administrator."
**[action]** Open `SecurityIntegrationTests.Register_RequestingAdminRole_CreatesStandardUser`.
> "Now public registration always creates a User — this test proves registering *as* Admin
> still yields a User. Elevation is admin-only through UsersController."

## 2:00 — S2–S7 quickly (about 40s each)
For each, **[action]** `git show <hash>` and say the line:
- **S2 CORS** — > "Removed the AllowAnyOrigin fallback; origins come from config now."
- **S3 Swagger** — > "Swagger is Development-only; it was exposing the whole API in Production."
- **S4 rate limiting** — > "Fixed-window limiter on the auth endpoints — 429 after the window. Here's the 429 test." **[action]** show `RateLimitIntegrationTests`.
- **S5 EF logging** — > "EF was logging SQL and parameter values at Debug — dropped to Warning so sensitive data isn't logged."
- **S6 headers** — > "Security headers on every response and the Server header hidden." **[action]** show the headers test.
- **S7 JWT secret** — > "Startup now fails fast if the signing key is missing or too weak, and the real key comes from an env var, not source."

## 6:00 — Performance (3 min)
**[action]** Open `perf-artifacts/before.md` and `after.md` side by side.
> "I built a reproducible benchmark — 500 events, 4000 bookings — that times the endpoints.
> The list endpoint went from 493 milliseconds to 21 — about 23 times faster."
**[action]** `git show <perf commit>` on `EventRepository` + `EventsController`.
> "Two things. The list was loading every event and every booking to count RSVPs in memory,
> with no pagination. Now GetPagedAsync loads only one page with AsNoTracking, and the
> response stays a plain array so the frontend is unchanged — pagination info goes in
> headers."
**[action]** show `ReportsController.GetStats` diff.
> "And the stats endpoint collapsed four count queries into one grouped query."
> "Caveat I call out in the report: the benchmark uses in-memory EF, so these are
> in-process numbers — on real SQL Server the win is bigger. The DOTTRACE-GUIDE.md shows
> how to profile the hot path against the Docker stack."
**[action]** (optional) show dotTrace before/after call trees if recorded.

## 9:00 — Logging & health (2 min)
**[action]** Show Serilog config in `Program.cs`; run the API (or a test) and show JSON logs.
> "Structured logging with Serilog — JSON per request with method, path, status and elapsed
> milliseconds. That elapsed field is also a live timing source."
**[action]** `curl -s http://localhost:8080/health | jq` (or show the health test).
> "And health checks: /health verifies the database is reachable, /health/live is a plain
> liveness probe. docker-compose uses it so the frontend waits for the API to be healthy."

## 11:30 — Green (1 min)
**[action]** `dotnet test --filter "Category!=Benchmark"` → **34 passed**.
> "All 34 tests pass — the originals plus new tests locking in every security fix. It's all
> written up in SECURITY-PERFORMANCE-AUDIT.md with the checklist and metrics. Thanks."

---
### Tips
- Pre-build once so the demo doesn't wait on restore.
- `jq` makes the Serilog/health JSON readable on screen.
- Keep the before/after table on screen while narrating the perf section.
