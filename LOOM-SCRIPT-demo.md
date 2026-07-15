# Loom Script #2 — Non-Technical Demo (Working App)

**Audience:** non-technical / stakeholder. **Target length:** 4–6 min. **Screen:** browser at http://localhost (no code, no terminal after startup).

**Prerequisite:** Docker Desktop running. One-time startup before recording:

```bash
cd event-board-debug-exercise
docker-compose up --build      # wait until the API logs "seeded successfully"
# open http://localhost
```

Seeded accounts: **admin@eventboard.com / Admin123!** · **alice@example.com / Alice123!**

Record in this order. Bracketed lines are **[actions]**; quoted lines are what to **say**.
Keep it plain-language — describe *what the user sees*, not the code.

---

## 0:00 — What this is (30s)
> "This is the Community Event Board — a place to discover local events, RSVP to them, and,
> if you're an organizer, create your own. I recently fixed a batch of issues in it, so
> here's the app working end to end."

## 0:30 — Browse events (45s)
**[action]** Land on the home page; scroll the event grid; click a category filter.
> "Anyone can browse without logging in. Each card shows the title, date, location and a
> thumbnail. I can filter by category — conferences, workshops, meetups, and so on."

**[action]** Open one event's detail page.
> "The detail page shows the full description and a live weather panel for the event's
> city, plus the current RSVP counts — how many people said yes, maybe, or no."

## 1:15 — Register & log in (45s)
**[action]** Click Register; create a new account; then log in (or log in as alice@example.com).
> "I'll create an account… and I'm in. Notice the earlier bug where valid passwords were
> rejected is gone — login just works now, and the account stays signed in."

## 2:00 — RSVP (1 min)
**[action]** On an event, click **Yes**; watch the Yes count increase. Then click **No**; watch it move.
> "I'll RSVP 'Yes' — the count updates instantly. If I change my mind to 'No', it moves my
> response over rather than double-counting. Each person has exactly one RSVP per event."

**[action]** Open the dashboard / "My RSVPs".
> "And everything I've responded to is listed here under my RSVPs."

## 3:00 — Create an event (1 min)
**[action]** Go to Create Event; fill the form; upload an image; submit; find it in the list.
> "As a signed-in user I can create an event, upload an image, and it appears in the public
> list right away, organized under my account."

## 4:00 — Admin panel (1 min)
**[action]** Log out; log in as **admin@eventboard.com**; open the admin dashboard.
> "Now as an administrator. The dashboard shows summary stats — total users, events, and
> the RSVP breakdown — and those numbers are now correct after the fixes."
**[action]** Show Manage Users (promote/disable) and delete an event.
> "Admins can manage users and remove events. Importantly, only admins can do this — a
> regular user can't, which was one of the security issues I closed."

## Close (20s)
> "That's the full flow — browse, sign in, RSVP, create, and admin — all working. Thanks
> for watching."

---

## Fallback — if you don't want to run the UI (recovery summary, ~2 min)

If Docker isn't available, record a short summary instead of the UI demo:

> "This project is an event-board web app. It had ten issues plus a broken dependency that
> stopped it building. I fixed all of them and the automated test suite — 29 tests — now
> passes completely."

**[action]** Show `dotnet test` printing **Passed! 29/29** and the green GitHub repo page.
> "The big ones were security-related: login accepted wrong passwords, disabled accounts
> could still get in, and any user could delete events. Those are all closed, and the app
> builds and runs cleanly again."

---
### Tips
- Do a dry run first: `docker-compose up --build` can take a couple of minutes on first
  build while it pulls SQL Server and builds the frontend.
- If the weather panel shows "unavailable," add a free OpenWeatherMap key to `.env`
  (`OPENWEATHER_API_KEY=...`) before starting — the app works either way.
- Reset to clean seed data anytime with `docker-compose down -v` then `up --build`.
