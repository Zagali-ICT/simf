# Page 017 — Function (تفاصيل الجلسة · Session detail)

What the user does on this screen. Grounded in `Mockup.html` screen 17 (line
~1230) and the Screen Guide SCREEN17 ("Full information about one agenda session.
Users can read the full description and add the session to their calendar").

## Privilege / auth gate
- **The session detail is anonymous — Guest and above.** A not-logged-in guest
  who taps a session in the agenda can read it (Screen Guide Journey C/E). The
  route `/sessions/:sessionId` is **not** auth-gated.
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
4. **المتحدثون (Speakers)** — the ordered speaker cards (photo avatar · name ·
   rank/role · **country flag**; the host is marked `المضيف`). Each card is
   tappable. The avatar is the speaker **photo** and the flag is rendered from the
   speaker's **country id** (D-271).
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
   Page_018)** (`/sessions/:sessionId/my-seat`) — the visual hall plan with the
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
  from the **cached programme** — no per-open round-trip is required. Each speaker
  carries its **country (id + EN/AR name) + photo** (D-271); the card shows the
  **flag from the country id** and the **avatar from the photo path**.
- The **my-seat card appears only** for a signed-in approved account that holds an
  active reservation, and shows the correct **row label + seat number**; it is
  **absent** for guest / pending / no-reservation.
- `عرض ←` opens My Seat map (18); each speaker card opens Speaker profile (20).
- `أضف إلى تقويمي` produces a standard calendar event for **this** session;
  `تذكير` schedules a local reminder. Both work **offline** (no server call).
- Each speaker card shows the speaker's **photo avatar** and **country flag**
  (D-271).

## Related session surfaces (links out — D-271)
The detail is the launch point for the live session experience (full contracts on
their own screens — Page_017_Logic L-9):
- **Ask a question →** the Q&A surface (**screen 26**) — only open **from 5 minutes
  before the session start until its end**, and only for an **arrived** attendee
  (else **400 SESSION_NOT_LIVE_FOR_QUESTIONS**).
- **Watch live / replay + summary →** the live player (**screen 25**) — a **LIVE**
  player when the session is broadcasting (`liveStreamUrl` set), otherwise the
  recording + the AI **محضر** summary; the optional **لغة الإشارة** sign-language
  feed rides the same screen.
- **Comments** — the standalone comments screen (28) is **removed**; comments now
  live **inside** the session / live screen (25), passing the AI-filter +
  admin-moderation pipeline.

## Where it fits in the journey
Middle of **Journey E — Agenda planning**: Home (13) → **Sessions (16)** →
**Session detail (17)** → add to calendar / reminder / **My Seat (18)** / **Speaker
profile (20)** → (live) **Live / Q&A (25/26)**. The same session also surfaces in
**My Area (14)** under "today's schedule".
