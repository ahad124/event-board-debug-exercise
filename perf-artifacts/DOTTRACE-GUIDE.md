# Profiling the hot path with dotTrace

The reproducible before/after numbers in this folder come from the benchmark harness
(`EventBoard.Api.Tests/PerfBenchmarks.cs`) and Serilog request timings. For a deeper,
line-level view — and for the technical Loom video — profile the real Docker stack with
**JetBrains dotTrace** (or Rider's built-in profiler).

> Note: dotTrace is a GUI/commercial profiler and was not available in the environment
> where these fixes were made, so it was not run here. These are the steps to capture the
> snapshots yourself.

## Prerequisites
- JetBrains dotTrace (standalone) or JetBrains Rider.
- The API running with a realistic dataset. Easiest: `docker-compose up --build`, then use
  the benchmark seed logic, or hit the endpoints repeatedly.

## Capture the "before" snapshot
1. `git checkout 5ae01d0` (the baseline-benchmark commit, before the perf fixes).
2. Run the API: `dotnet run --project EventBoard.Api` (or attach to the Docker container's
   `dotnet` process).
3. In dotTrace: **Attach to Process** → select `EventBoard.Api` → profiling type
   **Sampling** (or **Timeline** for async/DB waits).
4. Start recording, then drive load at the list endpoint:
   ```bash
   for i in $(seq 1 200); do curl -s http://localhost:8080/api/events > /dev/null; done
   ```
5. Stop and **Get Snapshot**. Save as `before.dtp`.
6. In the call tree, expand `EventsController.GetAllEvents` → note time in
   `EventRepository.GetAllAsync`, `Include(Bookings)` materialization, and
   `MapToEventDto` in-memory `Count`.

## Capture the "after" snapshot
1. `git checkout security-performance-audit`.
2. Repeat steps 2–5, saving as `after.dtp`.
3. Compare: the booking-materialization and mapping frames should shrink dramatically
   because only one page of events is loaded and stats use a grouped query.

## What to show in the Loom video
- Side-by-side call trees (before vs after) for `GET /api/events`.
- The hot frame (`Include`/materialization) dominating "before" and gone "after".
- The wall-clock numbers from `before.md` / `after.md` for the headline figure.
