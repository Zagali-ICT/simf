# Page 016 — Function (الأجندة · Agenda)

What the user does on this screen. Grounded in `Mockup.html` screen 16 and the
Screen Guide SCREEN16 ("Filterable schedule of all sessions, with day selector
and search").

## Privilege / auth gate
**Anonymous — Guest and above.** A not-logged-in guest can open the agenda
(Screen Guide Journey C: "the guest can open the agenda, speakers list, map and
media gallery"). No token required.

## Elements (top → bottom, from the mockup)
1. **Top filter pills** — `أجندة قادمة` (Upcoming) / `أجندة الفعالية` (Forum / full).
   The two view modes the owner described as "open / all-active".
2. **Day selector strip** — the event days (e.g. SUN 2 … SAT 8), the active day
   highlighted in brass. The owner's "remaining days".
3. **Search bar** — free-text filter over the list.
4. **Vertical session list** — each row shows **time · index number · title ·
   short description**, with the active session highlighted (brass background)
   and a chevron `←` indicating it is tappable.

## What the user does
1. **Switch view** between *Upcoming* (sessions still to come) and *Forum* (the
   whole programme). — handled **client-side** over the cached programme.
2. **Pick a day** from the strip → the list filters to that day. — **client-side,
   inline** (Screen Guide: "Day selector → filters the list inline").
3. **Search** → the list filters by title/description. — **client-side, inline**.
4. **Tap a session** → **Session detail (screen 17, Page_017)** — full
   description, hall + category tags, speakers, my-seat, add-to-calendar / reminder.
   Because the full programme is cached (and now carries body + speakers), the
   detail/preview can render from cache without another fetch.

## Acceptance criteria
- The agenda is reachable and fully readable **without signing in**.
- The app fetches the **whole programme once** (`GET /app/programme/sessions`,
  no `day` filter) and **caches** it; the pills, day strip and search all filter
  the cached list **in the UI** — no per-filter round-trip.
- Each cached item carries **Date, Code, Title, Body, Hall (EN/AR), type
  (category), and the ordered speaker list** so both the list row and the detail
  preview render from the one payload.
- Sessions are time-ordered; today/active sessions are visually distinguished.

## Where it fits in the journey
Start of **Journey E — Agenda planning**: Home (13) → Agenda (16) → tap a session
→ Session detail (17) → add to calendar / reminder / view seat (18). The same
session also surfaces in **My Area (14)** under "today's schedule".
