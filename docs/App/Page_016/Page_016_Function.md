# Page 016 — Function (الأجندة · Sessions agenda)

What the user does on this screen. Grounded in the as-built KSA screen
(`sessions_screen.dart`, frame 215:767, D-378) and the Screen Guide SCREEN16
("Filterable schedule of all sessions, with day selector and search").

Last updated: 2026-06-13 — KSA Wave-2 redesign (D-378).

> **Rename history (D-271 → D-378):** D-271 renamed the screen الأجندة →
> الجلسات; the D-378 KSA rebuild re-titles the header + bottom-nav label back
> to **الأجندة / Agenda** and relabels the pills to the frame copy
> (**أجندة الفعالية / الأجندة القادمة**). Behaviour is unchanged; the API route
> stays `/app/programme/sessions`.

## Privilege / auth gate
**Anonymous — Guest and above.** A not-logged-in guest can open the sessions
list (Screen Guide Journey C: "the guest can open the agenda, speakers list,
map and media gallery"). No token required.

## Elements (top → bottom, as built)
1. **Search field** — bordered, hint **البحث / Search**; free-text filter over
   the list, applied **per keystroke**.
2. **View pills** — **أجندة الفعالية** (Event agenda — the whole programme) /
   **الأجندة القادمة** (Upcoming agenda — sessions still to come; **the
   default**). The two view modes the owner described as "open / all-active".
3. **Day selector strip** — a white strip of the **programme days** (derived
   from the cached sessions), each cell a weekday abbreviation + day number;
   the selected day inverts to navy; Fri/Sat weekday labels are red. The
   owner's "remaining days".
4. **المواعيد / Schedule** section header + the **vertical session list** —
   each row shows **time chip · gold zero-padded index + title · short
   description**, with a forward chevron indicating it is tappable.

## What the user does
1. **Switch view** between *Upcoming* (default; `startUtc >= now`) and *Event
   agenda* (the whole programme) — handled **client-side** over the cached
   programme.
2. **Pick a day** from the strip → the list filters to that (device-local)
   day; **re-tapping the selected day clears the day filter** (there is no
   "all days" pill). — **client-side, inline** (Screen Guide: "Day selector →
   filters the list inline").
3. **Search** → the list filters by title / description / code (both
   languages). — **client-side, inline**.
4. **Tap a session** → **Session detail (screen 17, Page_017)** — full
   description, hall + category tags, speakers, my-seat, add-to-calendar /
   reminder. Because the full programme is cached (and carries body +
   speakers), the detail/preview can render from cache without another fetch.
5. **Retry on failure** — the error state's **إعادة المحاولة / Retry** button
   re-runs the one fetch.

## Acceptance criteria
- The agenda is reachable and fully readable **without signing in**.
- The app fetches the **whole programme once** (`GET /app/programme/sessions`,
  no `day` filter) and **caches** it; the pills, day strip and search all
  filter the cached list **in the UI** — no per-filter round-trip.
- The default view is **Upcoming** (الأجندة القادمة); switching to
  **أجندة الفعالية** reveals past sessions too.
- Each cached item carries **Date, Code, Title, Body, Hall (EN/AR), type
  (category), and the ordered speaker list** so both the list row and the
  detail preview render from the one payload. Each speaker also carries its
  **country (id + EN/AR name) and photo** (D-271) — consumed by the detail
  preview (the list row itself renders no speakers).
- Sessions are time-ordered; each row carries a device-local **two-line time
  chip** and a **zero-padded row number** (the KSA frame has no
  active-session highlight).

## Where it fits in the journey
Start of **Journey E — Agenda planning**: Home (13) → **Sessions agenda (16)**
→ tap a session → Session detail (17) → add to calendar / reminder / view seat
(18). Booked sessions also surface in **My Area (14)**'s schedule list (its
session rows deep-link back to the same detail).
