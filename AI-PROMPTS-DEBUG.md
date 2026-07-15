# AI Prompt Log — Debugging Session

The prompts used with the AI pair-programmer to locate and fix each defect, in the order
they were used. The workflow for every bug was the same: run the suite, feed the failing
test name + assertion message to the AI, ask it to localize the fault to a specific
condition/line, confirm against the code, apply a minimal fix, re-run.

## 0. Orientation

> "Here's an ASP.NET Core 8 + EF Core solution. `dotnet restore` fails and then a bunch of
> tests fail. Walk me through triaging this: what should I fix first, restore or tests?"

> "Run order matters — the integration tests all 401. Which bugs are likely gating the
> others, so I fix the auth chain before the feature-level failures?"

## 1. NuGet restore failure

> "`dotnet restore` fails with NU1603 saying SqlServer 8.0.99 wasn't found and 9.0.0 was
> resolved instead, then NU1605 'package downgrade' for EntityFrameworkCore 9.0.0 → 8.0.0.
> Explain the chain and tell me the minimal csproj change to fix it."

> "Confirm: all the other Microsoft.EntityFrameworkCore.* packages are 8.0.0, so I should
> pin SqlServer to 8.0.0 too rather than bump everything to 9.0.0?"

## 2. Auth bugs (fix these first — they gate the integration tests)

**Bug 1 — inverted password check**
> "AuthServiceTests.LoginAsync_ValidCredentials_ReturnsToken now returns null for a correct
> password while a wrong password logs in. Look at the BCrypt.Verify condition in
> LoginAsync and tell me if the boolean is inverted."

**Bug 2 — inverted IsActive check**
> "The disabled-account test expects an InvalidOperationException but none is thrown, and
> active users are being blocked. Check the IsActive guard direction in LoginAsync."

**Bug 3 — expired JWT**
> "Login succeeds and returns a token, but using it immediately gives 401 with lifetime
> validation failing (IDX10223). Inspect how expiresAt is computed in JwtTokenService."

## 3. Service / controller validation bugs

**Bug 4 — missing null guard**
> "CreateEvent_NullEvent_ThrowsArgumentNullException expects an ArgumentNullException but
> nothing is thrown. What guard is missing in EventService.CreateEventAsync?"

**Bug 5 — delete returns true for missing id**
> "DeleteEvent_NonExistingId_ReturnsFalse fails because delete returns true for an id that
> doesn't exist. What check is missing after GetByIdAsync in DeleteEventAsync?"

**Bug 6 — id off-by-one**
> "GET /api/events/0 should be a 400 but returns 404. Check the boundary condition on the
> id guard in GetEventById."

## 4. RSVP / data bugs

**Bug 7 — swapped tallies**
> "After one 'Yes' RSVP, rsvpYesCount is 0 and rsvpNoCount is 1. Look at the Count
> predicates in MapToEventDto — are Yes and No swapped?"

**Bug 8 — upsert ignores user**
> "When a second user RSVPs to an event that already has an RSVP from someone else, the
> first row gets overwritten instead of a new one created. Check the predicate in
> GetByUserAndEventAsync — is it filtering on userId?"

## 5. Authorization / reporting bugs

**Bug 9 — broken RBAC**
> "The admin-only Delete endpoint now 403s for admins and lets normal users through. Check
> the Roles value on the [Authorize] attribute for DeleteEvent."

**Bug 10 — stats miscount**
> "The stats endpoint reports the wrong Yes RSVP count against known seed data. Check which
> BookingStatus YesRsvps is counting in GetStats."

## 6. Verification

> "All 29 tests pass and the build is clean. Draft concise conventional-commit messages
> (one per bug) that state the root cause and the test that proves the fix."

> "Write a short debugging journal and a NuGet recovery doc from these fixes, with the
> before/after logs I captured."
