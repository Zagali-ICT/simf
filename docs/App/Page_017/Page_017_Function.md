# Page 017 — Function (تفاصيل الجلسة · Session detail)

What the user does on this screen. Grounded in `Mockup.html` screen 17 (line
~1230) and the Screen Guide SCREEN17 ("Full information about one agenda session.
Users can read the full description and add the session to their calendar").

## Privilege / auth gate
- **The session detail is anonymous — Guest and above.** A not-logged-in guest
  who taps a session in the agenda can read it (Screen Guide Journey C/E). The
  route `/agenda/:sessionId` is **not** auth-gated.
- **The `مقعدي` (my-seat) card is login-only.** It renders **only** for an
  approved account that has an **active reservation** for this session — the
  seat data comes from an endpoint that requires an approved account. A guest,
  a pending account, or an approved account with no booking sees **no** card.

## Elements (top → bottom, from the mockup)
1. **Header band** — index number (e.g. `02`), the date · time window
   (`الإثنين · 03 نوفمبر` · `09:00 — 10:30`), and the **title**.
2. **Tags row** — the **hall** tag (`القاعة الرئيسية · HALL A`) + the **category**
   tag (`جلسة رئيسية` / "Main session") — the same "type" tag as the agenda.
3. **وصف الجلسة (Description)** — the full session body.
4. **المتحدثون (Speakers)** — the ordered speaker cards (avatar · name · rank/role;
   the host is marked `المضيف`). Each card is tappable.
5. **مقعدي (My seat) card** — *login + reservation only* — brass-bordered:
   **الصف B · مقعد 12** (Row B · Seat 12) + a `عرض ←` (View) link.
6. **Two CTAs** — `أضف إلى تقويمي` (Add to my calendar) + `تذكير` (Reminder).
7. **Bottom nav** — the five-slot bar (Agenda active).

## What the user does
1. **Read the session** — title, time, hall, category, description, speakers. —
   rendered from the **cached p16 item** (no extra fetch); the app may refresh the
   live seat count / recording flag from the detail endpoint.
2. **Open a speaker** → tap a speaker card → **Speaker profile (screen 20,
   Page_020)** (`/speakers/:speakerId`).
3. **View my seat** → tap `عرض ←` on the my-seat card → **My Seat map (screen 18,
   Page_018)** (`/agenda/:sessionId/my-seat`) — the visual hall plan with the
   assigned seat highlighted.
4. **Add to calendar** → `أضف إلى تقويمي` → the app builds a calendar event for
   this session and hands it to the **device calendar** (Screen Guide: "→ device
   calendar (system action)").
5. **Set a reminder** → `تذكير` → the app **schedules a local notification**
   before the session starts (Screen Guide: "→ schedules a local push
   notification before the session starts"). Client-local — no server call.

## Acceptance criteria
- The detail is reachable and fully readable **without signing in**.
- The session content (title/time/hall/category/description/speakers) renders
  from the **cached programme** — no per-open round-trip is required.
- The **my-seat card appears only** for a signed-in approved account that holds an
  active reservation, and shows the correct **row label + seat number**; it is
  **absent** for guest / pending / no-reservation.
- `عرض ←` opens My Seat map (18); each speaker card opens Speaker profile (20).
- `أضف إلى تقويمي` produces a standard calendar event for **this** session;
  `تذكير` schedules a local reminder. Both work **offline** (no server call).

## Where it fits in the journey
Middle of **Journey E — Agenda planning**: Home (13) → Agenda (16) → **Session
detail (17)** → add to calendar / reminder / **My Seat (18)** / **Speaker
profile (20)**. The same session also surfaces in **My Area (14)** under "today's
schedule".
