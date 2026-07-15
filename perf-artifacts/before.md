# Performance benchmark — before

- Dataset: 500 events, ~4000 bookings (InMemory EF).
- Iterations per endpoint: 30 (p95 reported; warm-up excluded).
- Captured: 2026-07-15 11:26 UTC

| Endpoint | Avg (ms) | p95 (ms) |
|----------|---------:|---------:|
| GET /api/events (list) | 493.02 | 761.54 |
| GET /api/events/11 (detail) | 5.11 | 8.22 |
| GET /api/reports/stats (admin) | 8.99 | 14.51 |

