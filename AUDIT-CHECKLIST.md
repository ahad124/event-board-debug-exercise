# Security & Performance Audit Checklist

AI-generated checklist used to drive this audit of the Community Event Board API
(ASP.NET Core 8 + EF Core). Grouped by OWASP-aligned themes plus performance and
observability. Each item is marked with its status in this repo.

Legend: ✅ addressed in this audit · ➖ already OK · ⚠️ documented / partial.

## A01 Broken Access Control
- [x] ✅ Registration cannot self-assign privileged roles (no mass-assignment of `Role`).
- [x] ➖ Admin-only endpoints enforce `[Authorize(Roles="Admin")]` (verified by tests).
- [x] ➖ RSVP/favorite records are owned by the JWT user, not a client-supplied id.
- [x] ➖ Self-demotion / self-disable guarded in `UsersController`.

## A02 Cryptographic Failures / Secrets
- [x] ✅ JWT signing key validated at startup (present + ≥32 bytes outside Development).
- [x] ✅ Secrets sourced from environment (`JWT_KEY`, DB password) — not committed real keys.
- [x] ➖ Passwords hashed with BCrypt.

## A03 Injection
- [x] ➖ EF Core parameterizes all LINQ queries.
- [x] ➖ Raw SQL report uses `SqlParameter` (no string concatenation).

## A04 Insecure Design / Rate Limiting
- [x] ✅ Rate limiting on auth endpoints (brute-force / credential stuffing).
- [x] ➖ File upload validates extension, content-type and size; random server filename.

## A05 Security Misconfiguration
- [x] ✅ CORS does not fall back to `AllowAnyOrigin`.
- [x] ✅ Swagger disabled in Production.
- [x] ✅ Security response headers (`X-Content-Type-Options`, `X-Frame-Options`,
      `Referrer-Policy`, CSP `frame-ancestors`, `X-Permitted-Cross-Domain-Policies`).
- [x] ✅ `Server` header suppressed.
- [x] ⚠️ HTTPS/HSTS handled at the reverse proxy (Nginx) in this deployment.

## A09 Security Logging & Monitoring
- [x] ✅ Structured logging (Serilog JSON) with per-request method/path/status/elapsed.
- [x] ✅ Sensitive data not logged (EF SQL/parameter logging reduced from Debug → Warning).
- [x] ✅ Health endpoints for liveness and DB readiness.

## Performance
- [x] ✅ List endpoint paginated (no unbounded result sets).
- [x] ✅ RSVP tallies not materialized as full booking collections for large lists.
- [x] ✅ Read queries use `AsNoTracking`.
- [x] ✅ Stats endpoint reduces DB round-trips (grouped query).
- [x] ✅ Before/after response times captured (`perf-artifacts/`).

## Observability / Ops
- [x] ✅ `/health` (DB readiness) and `/health/live` (liveness).
- [x] ✅ Container `HEALTHCHECK` wired in docker-compose; frontend waits for API healthy.
