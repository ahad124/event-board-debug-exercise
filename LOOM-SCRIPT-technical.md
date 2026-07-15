# Loom Script #1 — Technical Debugging Walkthrough

**Audience:** engineers / reviewer. **Target length:** 9–12 min. **Screen:** editor + terminal side by side, repo open at https://github.com/ahad124/event-board-debug-exercise.

Record in this order. Bracketed lines are **[actions]**; quoted lines are what to **say**.

---

## 0:00 — Intro (30s)
> "This is the Community Event Board — an ASP.NET Core 8 Web API with EF Core and a React
> frontend. It shipped working: clean build, 22 passing tests. For this exercise I injected
> 10 backend bugs plus a broken NuGet dependency, then debugged it back to green. The git
> history is the story — let me walk you through it."

**[action]** `git log --oneline` — point at baseline → tests → **broken seed** → nuget fix → 10 fix commits.

## 0:30 — The broken state (1 min)
**[action]** `git checkout 272e830` then `dotnet restore`.
> "Straight away it won't even restore. NuGet says SqlServer 8.0.99 wasn't found, floats up
> to 9.0.0, and that collides with the pinned EF Core 8 stack — an NU1605 downgrade error.
> Classic version-family mismatch."

## 1:30 — NuGet recovery (1 min)
**[action]** Open `NUGET-RECOVERY.md`; show the one-line csproj diff (`8.0.99` → `8.0.0`).
**[action]** `git checkout 35a46a3` then `dotnet restore` → restored, exit 0.
> "Pin the provider back to 8.0.0 so the whole EF Core family aligns. Restore is green."

## 2:30 — See all failures at once (1 min)
**[action]** `dotnet test` → 17 passed / 12 failed.
> "With NuGet fixed, 12 tests fail across 10 bugs. Notice the integration tests all 401 —
> that's a signal the auth chain is broken and gating everything, so I fix auth first."

## 3:30 — Bug-by-bug (about 40s each)
For each, **[action]** `git show <hash>` to display the one-line diff, then say the line.

- **bug-01 (2a41409) inverted password** — > "The `!` was dropped on `BCrypt.Verify`, so correct passwords were rejected and wrong ones accepted. An auth bypass. Restore the negation."
- **bug-02 (b7a93b6) inverted IsActive** — > "`if (user.IsActive) throw` blocked active users and let disabled accounts in. Flip it back to `!IsActive`."
- **bug-03 (7314389) expired JWT** — > "Tokens were stamped `AddHours(-1)` — expired at birth, so every request failed lifetime validation with 401. Make it `+1`." **[action]** re-run tests → integration tests now unblocked (21 pass).
- **bug-04+05 (d973a20) EventService guards** — > "Two guards were deleted: the null-argument check in CreateEvent, and the existence check in Delete that stops it returning true for a missing id."
- **bug-06 (5064856) id off-by-one** — > "`id < 0` let id 0 through to the DB and returned 404; it should be `<= 0` → 400."
- **bug-07 (68bff24) swapped tallies** — > "Yes counted No and No counted Yes in the DTO mapping — event pages showed mirrored RSVP counts."
- **bug-08 (49e1319) upsert ignores user** — > "The booking lookup filtered on EventId only, so a second user's RSVP overwrote the first. Add the `UserId` filter back."
- **bug-09 (2166f54) broken RBAC** — > "Delete was `[Authorize(Roles=\"User\")]` — admins locked out, normal users could delete anything. Back to Admin."
- **bug-10 (d7ecba9) stats miscount** — > "YesRsvps counted `BookingStatus.No`. A copy-paste slip that skewed the admin dashboard."

## ~10:00 — Green (1 min)
**[action]** `git checkout main` then `dotnet build` + `dotnet test` → 0 errors, **29/29 pass**.
> "Build clean, all 29 tests pass — the 22 originals plus 7 regression tests I added that
> pin each fix. Each bug maps to a named failing test, so this can't silently regress."

## Close (20s)
> "Everything's documented: DEBUGGING-JOURNAL.md has the before/after per bug with the AI
> prompts I used, and debug-artifacts/ has the raw logs. Thanks for watching."

---
### Tips
- Pre-run `dotnet build` once before recording so restore noise doesn't stall you.
- Keep `DEBUGGING-JOURNAL.md` open in a tab to read the exact assertion messages.
- Use `git show <hash> -- <file>` to keep diffs tight to the relevant file.
