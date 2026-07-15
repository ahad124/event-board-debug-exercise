# Debugging Journal — Community Event Board

This journal documents a debugging exercise on the Community Event Board
(ASP.NET Core 8 Web API + EF Core + React/Vite). Ten intentional backend bugs and one
broken-NuGet-dependency scenario were injected into a working baseline, then located and
fixed with AI assistance until the whole suite was green again.

- **Repo:** https://github.com/ahad124/event-board-debug-exercise
- **Baseline:** `dotnet build` clean, **22/22** tests pass, NuGet restores.
- **After adding regression tests:** **29** tests (22 original + 7 new).
- **Broken state:** restore fails (NuGet), then **12** test failures across **10** bugs.
- **Final state:** `dotnet build` clean, **29/29** tests pass.

## How the exercise is structured (git history is the journal)

```
7c08bbb  chore: initial import (working baseline, 22 tests green)
72816fe  test: add regression tests encoding correct behavior (29 tests green)
272e830  chore: seed debugging exercise — inject 10 bugs + break NuGet   <-- BROKEN
35a46a3  fix(nuget): pin EntityFrameworkCore.SqlServer back to 8.0.0
2a41409  fix(bug-01): restore inverted BCrypt password check in LoginAsync
b7a93b6  fix(bug-02): correct inverted IsActive check in LoginAsync
7314389  fix(bug-03): issue JWTs with a future expiry (AddHours(1), not -1)
d973a20  fix(bug-04+05): restore EventService validation guards
5064856  fix(bug-06): reject id 0 in GetEventById (id <= 0, not id < 0)
68bff24  fix(bug-07): unswap RSVP Yes/No tallies in MapToEventDto
49e1319  fix(bug-08): scope RSVP upsert lookup to the current user
2166f54  fix(bug-09): restrict DeleteEvent to Admin role
d7ecba9  fix(bug-10): count Yes RSVPs (not No) in admin stats
```

`git diff 272e830~1 272e830` shows every injected defect at once; each `fix(...)` commit
shows one repair. Full logs are in [`debug-artifacts/`](debug-artifacts/):
`before-restore.log`, `before-tests.log`, `after-tests.log`.

## Before / after at a glance

| Stage | Command | Result |
|-------|---------|--------|
| Broken — restore | `dotnet restore` | ❌ `NU1603` + `NU1605` (see NuGet section) |
| Broken — tests (NuGet fixed) | `dotnet test` | ❌ 17 passed / **12 failed** |
| Fixed | `dotnet build` + `dotnet test` | ✅ 0 errors / **29 passed, 0 failed** |

---

## The NuGet dependency scenario

**Injected:** `EventBoard.Api.csproj` requested a non-existent package version
`Microsoft.EntityFrameworkCore.SqlServer` **8.0.99**.

**Before (`debug-artifacts/before-restore.log`):**

```
warning NU1603: EventBoard.Api depends on Microsoft.EntityFrameworkCore.SqlServer
  (>= 8.0.99) but ...SqlServer 8.0.99 was not found. ...SqlServer 9.0.0 was resolved instead.
error NU1605: Warning As Error: Detected package downgrade:
  Microsoft.EntityFrameworkCore from 9.0.0 to 8.0.0. ...
  EventBoard.Api -> ...Design 8.0.0 -> ...Relational 9.0.0 -> ...EntityFrameworkCore (>= 9.0.0)
  EventBoard.Api -> Microsoft.EntityFrameworkCore (>= 8.0.0)
restore exit=1
```

**Diagnosis:** NuGet could not find 8.0.99, so it floated the SqlServer package *up* to
9.0.0. That dragged EF Core 9.0.0 into the graph while the rest of the project pins the
8.0.0 EF Core stack — a **package downgrade** conflict (`NU1605`), which is treated as an
error. Nothing compiled until this was resolved.

**Fix (`fix(nuget)` commit):** pin the SqlServer package back to **8.0.0** so all EF Core
packages align. Full step-by-step recovery is in **[NUGET-RECOVERY.md](NUGET-RECOVERY.md)**.

**After:** `dotnet restore` → `Restored ... (in 531 ms)`, exit 0.

---

## The 10 bugs

Each entry: file, symptom, before evidence, root cause, the AI prompt used, and the fix.
Fix commits are labelled `bug-01 … bug-10` in git; the two EventService guards share the
`bug-04+05` commit.

### Bug 1 — Inverted password verification (auth bypass)
- **File:** `EventBoard.Api/Services/AuthService.cs` (`LoginAsync`)
- **Symptom:** correct passwords rejected; wrong passwords accepted.
- **Before:**
  `AuthServiceTests.LoginAsync_ValidCredentials_ReturnsToken` → `Assert.NotNull() Failure: Value is null`.
- **Root cause:** `if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))` — the `!`
  was dropped, so the "invalid password" branch fired on *valid* passwords.
- **AI prompt:** *"AuthServiceTests.LoginAsync_ValidCredentials_ReturnsToken now returns null for a correct password while a wrong password logs in. Look at the BCrypt.Verify condition in LoginAsync and tell me if the boolean is inverted."*
- **Fix:** restore `if (!BCrypt.Net.BCrypt.Verify(...))`.

### Bug 2 — Inverted `IsActive` check (disabled accounts allowed in)
- **File:** `EventBoard.Api/Services/AuthService.cs` (`LoginAsync`)
- **Symptom:** disabled accounts could log in; active accounts were blocked.
- **Before:**
  `LoginAsync_DisabledAccount_ThrowsAndDoesNotIssueToken` → `Assert.Throws() Failure: No exception was thrown`.
- **Root cause:** `if (user.IsActive) throw ...` rejected the wrong set of users.
- **AI prompt:** *"The disabled-account test expects an InvalidOperationException but none is thrown, and active users are being blocked. Check the IsActive guard direction in LoginAsync."*
- **Fix:** restore `if (!user.IsActive)`.

### Bug 3 — Expired JWTs (every request 401)
- **File:** `EventBoard.Api/Services/JwtTokenService.cs` (`GenerateToken`)
- **Symptom:** every authenticated request returned 401 immediately after login.
- **Before:** all integration tests that log in →
  `HttpRequestException : Response status code does not indicate success: 401 (Unauthorized)`.
- **Root cause:** `var expiresAt = DateTime.UtcNow.AddHours(-1);` — tokens were born
  already expired, failing JWT lifetime validation (`IDX10223`).
- **AI prompt:** *"Login succeeds and returns a token, but using it immediately gives 401 with lifetime validation failing. Inspect how expiresAt is computed in JwtTokenService."*
- **Fix:** `AddHours(1)`.

### Bug 4 — Missing null-argument guard in `CreateEventAsync`
- **File:** `EventBoard.Api/Services/EventService.cs`
- **Symptom:** a null event was forwarded to the repository instead of failing fast.
- **Before:** `CreateEvent_NullEvent_ThrowsArgumentNullException` →
  `Assert.Throws() Failure: No exception was thrown; Expected: typeof(System.ArgumentNullException)`.
- **Root cause:** the `if (evt == null) throw new ArgumentNullException(...)` guard was deleted.
- **AI prompt:** *"CreateEvent_NullEvent_ThrowsArgumentNullException expects an ArgumentNullException but nothing is thrown. What guard is missing in EventService.CreateEventAsync?"*
- **Fix:** re-add the null guard. *(Committed together with Bug 5 as `bug-04+05`.)*

### Bug 5 — `DeleteEventAsync` reports success for a missing id
- **File:** `EventBoard.Api/Services/EventService.cs`
- **Symptom:** deleting a non-existent event returned `true`.
- **Before:** `DeleteEvent_NonExistingId_ReturnsFalse` → `Assert.False() Failure — Expected: False, Actual: True`.
- **Root cause:** the `if (existing == null) return false;` existence check was removed, so
  the method always called `DeleteAsync` and returned `true`.
- **AI prompt:** *"DeleteEvent_NonExistingId_ReturnsFalse fails because delete returns true for an id that doesn't exist. What check is missing after GetByIdAsync in DeleteEventAsync?"*
- **Fix:** re-add the existence check.

### Bug 6 — Off-by-one id validation in `GetEventById`
- **File:** `EventBoard.Api/Controllers/EventsController.cs`
- **Symptom:** `GET /api/events/0` returned 404 instead of 400.
- **Before:** `GetEventById_InvalidId_ReturnsBadRequest` →
  `Assert.Equal() Failure — Expected: BadRequest, Actual: NotFound`.
- **Root cause:** `if (id < 0)` let `id == 0` fall through to a repository lookup.
- **AI prompt:** *"GET /api/events/0 should be a 400 but returns 404. Check the boundary condition on the id guard in GetEventById."*
- **Fix:** `if (id <= 0)`.

### Bug 7 — Swapped RSVP Yes/No tallies
- **File:** `EventBoard.Api/Controllers/EventsController.cs` (`MapToEventDto`)
- **Symptom:** event detail pages showed Yes and No counts swapped.
- **Before:** `Rsvp_Yes_ShowsInYesCount` → expected `RsvpYesCount == 1` but got `0`.
- **Root cause:** `RsvpYesCount` counted `BookingStatus.No` and `RsvpNoCount` counted `BookingStatus.Yes`.
- **AI prompt:** *"After one 'Yes' RSVP, rsvpYesCount is 0 and rsvpNoCount is 1. Look at the Count predicates in MapToEventDto — are Yes and No swapped?"*
- **Fix:** point each count at its matching status.

### Bug 8 — RSVP upsert lookup ignores the user
- **File:** `EventBoard.Api/Repositories/BookingRepository.cs` (`GetByUserAndEventAsync`)
- **Symptom:** a second user's RSVP overwrote the first user's row on the same event.
- **Before:** `Rsvp_TwoUsers_KeepIndependentResponses` → expected total RSVPs `2`, got `1`.
- **Root cause:** the predicate was `b => b.EventId == eventId` (the `UserId` filter was
  dropped), so the upsert found *whichever* booking existed for the event.
- **AI prompt:** *"When a second user RSVPs to an event that already has an RSVP from someone else, the first row gets overwritten instead of a new one created. Check the predicate in GetByUserAndEventAsync — is it filtering on userId?"*
- **Fix:** `b => b.UserId == userId && b.EventId == eventId`.

### Bug 9 — Broken role authorization on delete (privilege escalation)
- **File:** `EventBoard.Api/Controllers/EventsController.cs` (`DeleteEvent`)
- **Symptom:** admins got 403 on delete; ordinary users could delete any event.
- **Before:** `DeleteEvent_AsAdmin_Returns204` and `DeleteEvent_AsNormalUser_Returns403` both failed.
- **Root cause:** `[Authorize(Roles = "User")]` instead of `Roles = "Admin"`.
- **AI prompt:** *"The admin-only Delete endpoint now 403s for admins and lets normal users through. Check the Roles value on the [Authorize] attribute for DeleteEvent."*
- **Fix:** `[Authorize(Roles = "Admin")]`.

### Bug 10 — Admin stats count "No" as "Yes"
- **File:** `EventBoard.Api/Controllers/ReportsController.cs` (`GetStats`)
- **Symptom:** the admin dashboard's "Yes" RSVP total was wrong.
- **Before:** `Stats_AsAdmin_ReturnsCorrectSeedBreakdown` → expected `YesRsvps == 2`, got `1`.
- **Root cause:** `YesRsvps = CountAsync(b => b.Status == BookingStatus.No)` — copy-paste of
  the wrong enum value.
- **AI prompt:** *"The stats endpoint reports the wrong Yes RSVP count against known seed data. Check which BookingStatus YesRsvps is counting in GetStats."*
- **Fix:** count `BookingStatus.Yes`.

---

## Bug → failing test map

| Bug | File | Fix commit | Primary failing test(s) |
|-----|------|-----------|--------------------------|
| 1 Inverted password | AuthService | bug-01 | LoginAsync_ValidCredentials_ReturnsToken, _InvalidPassword_ReturnsNull |
| 2 Inverted IsActive | AuthService | bug-02 | LoginAsync_DisabledAccount_ThrowsAndDoesNotIssueToken |
| 3 Expired JWT | JwtTokenService | bug-03 | Login_IssuesUsableToken_CanCreateEvent (+ all authed integration tests) |
| 4 Missing null guard | EventService | bug-04+05 | CreateEvent_NullEvent_ThrowsArgumentNullException |
| 5 Delete returns true | EventService | bug-04+05 | DeleteEvent_NonExistingId_ReturnsFalse |
| 6 Id off-by-one | EventsController | bug-06 | GetEventById_InvalidId_ReturnsBadRequest |
| 7 Swapped tallies | EventsController | bug-07 | Rsvp_Yes_ShowsInYesCount |
| 8 Upsert ignores user | BookingRepository | bug-08 | Rsvp_TwoUsers_KeepIndependentResponses |
| 9 Broken RBAC | EventsController | bug-09 | DeleteEvent_AsAdmin_Returns204, _AsNormalUser_Returns403 |
| 10 Stats wrong count | ReportsController | bug-10 | Stats_AsAdmin_ReturnsCorrectSeedBreakdown |

> Note: bug 3 (expired JWT) and bug 1 (login) gate the whole authenticated integration
> suite — those tests only go green once the auth chain is repaired, which is why the fix
> order started with the auth bugs.

## Reproduce it yourself

```bash
git clone https://github.com/ahad124/event-board-debug-exercise
cd event-board-debug-exercise

# See the broken state:
git checkout 272e830
dotnet restore          # NU1603/NU1605 failure
git checkout 35a46a3    # NuGet fixed
dotnet test             # 12 failures

# See the repaired state:
git checkout main
dotnet test             # 29/29 pass
```
