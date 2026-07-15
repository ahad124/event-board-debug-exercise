# Performance benchmark — after

- Dataset: 500 events, ~4000 bookings (InMemory EF).
- Iterations per endpoint: 30 (p95 reported; warm-up excluded).
- Captured: 2026-07-15 11:34 UTC

| Endpoint | Avg (ms) | p95 (ms) |
|----------|---------:|---------:|
| GET /api/events (list) | 21.28 | 29.04 |
| GET /api/events/11 (detail) | 4.29 | 8.02 |
| GET /api/reports/stats (admin) | 4.99 | 9.48 |

