# Loom Script #2 — Non-Technical Demo (Enhanced Security & Performance)

**Audience:** non-technical / stakeholder. **Target length:** 4–6 min.
**Screen:** browser at http://localhost, plus one small terminal window for two quick checks.

**Prerequisite:** `docker-compose up --build`, then open http://localhost.
Seeded accounts: **admin@eventboard.com / Admin123!** · **alice@example.com / Alice123!**

Bracketed lines are **[actions]**; quoted lines are what to **say**. Keep it plain-language.

---

## 0:00 — What changed (30s)
> "This is the Event Board app after a security and performance tune-up. Same features, but
> it's now faster and safer. Let me show you the differences from a user's point of view."

## 0:30 — Faster browsing (1 min)
**[action]** Load the events page; scroll; open an event; navigate back and around.
> "Pages load quickly and stay responsive. Behind the scenes the app used to fetch the
> entire event list at once; now it loads a page at a time, so it stays fast even as the
> number of events grows — in our test the list endpoint went from about half a second to
> around twenty milliseconds."

## 1:30 — You can't sneak in as an admin (1.5 min)
> "The most important fix is about access. Previously, someone signing up could quietly make
> themselves an administrator. Now that's blocked."
**[action]** Register a brand-new account; log in; show the normal user dashboard (no admin tab).
> "Even if someone tries to request the admin role when signing up, the app ignores it —
> new accounts are always regular users. Only an existing admin can promote someone."
**[action]** Log in as the admin account; show the Manage Users / admin tab appears.
> "Admin tools only show up for actual admins."

## 3:00 — Sign-in is protected (45s)
**[action]** On the login page, deliberately fail login several times quickly.
> "Sign-in is now rate-limited. If someone tries to guess passwords by hammering the login,
> the app temporarily blocks the attempts — that protects everyone's accounts."

## 3:45 — Health & reliability (45s)
**[action]** In the terminal: `curl http://localhost:8080/health`.
> "There's also a built-in health check. Operations teams — or the container platform — can
> hit this to confirm the app and its database are up. If something's wrong, it's noticed
> automatically and the app can be restarted."

## Close (20s)
> "So: faster pages, stronger access control, brute-force protection on login, and better
> monitoring — all without changing how the app looks or feels to use. Thanks for watching."

---

## Fallback — no browser (recovery summary, ~2 min)
If you'd rather not run the UI:
> "I audited the Event Board API and fixed seven security issues and made it faster."
**[action]** Show `SECURITY-PERFORMANCE-AUDIT.md` (the summary table) and
`perf-artifacts/after.md`.
> "The headline: a key page is about 23 times faster, and a serious hole — where anyone
> could sign up as an administrator — is closed. The app also now logs activity in a
> structured way and reports its own health."
**[action]** Show `dotnet test` printing **34 passed**.

---
### Tips
- Do a dry run: first `docker-compose up --build` can take a few minutes.
- `curl http://localhost:8080/health | jq` reads nicely on screen.
- To reset demo data: `docker-compose down -v` then `up --build`.
