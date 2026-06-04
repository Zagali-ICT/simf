# Page 019 — Function (المتحدثون · Speakers list)

What the user does on this screen. Grounded in `Mockup.html` screen 19 (line
~1334) and the Screen Guide SCREEN19 ("Directory of forum speakers — a list of
cards leading to each speaker's profile").

## Privilege / auth gate
**Guest+ (anonymous).** The list endpoint is `AllowAnonymous` and the route
`/speakers` is **not** gated (D-199). Any visitor — guest, pending or approved —
can browse the speakers list with no sign-in. (The only login-gated action in the
speaker feature is the **meeting request**, and it lives on the **profile** page
20 — not here.)

## Elements (top → bottom, from the mockup)
1. **Header** — title `المتحدثون`.
2. **Speakers list** (`sp-list`) — a **vertical** stack of speaker cards
   (`sp-card`), one per speaker, in `displayOrder`. Each card holds:
   - an **avatar** (`⚓` / `★`) — the speaker photo when present, else a placeholder,
   - a **rank line** (e.g. `القبطان البحري`) — the speaker's `rank`,
   - the speaker's **name** (Arabic primary),
   - a **`المزيد` / More** link → opens that speaker's profile.
3. **Bottom nav** — the five-slot bar.

## What the user does
1. **Browse the speakers** — scroll the vertical list; every speaker drawn from the
   one `PublicSpeakers` list (Page_019_Logic L-1), ordered by `displayOrder` then
   name (L-2).
2. **Read each card at a glance** — avatar + rank line + name identify the speaker
   without opening the profile.
3. **Tap through to a profile** — tapping a card / its `المزيد` link navigates to
   **Speaker profile (20, [Page_020](../Page_020/README.md))** for that speaker's id
   (Page_019_Logic L-3), where the four-tab CV and (when allowed) the meeting
   request live.

## Acceptance criteria
- The screen opens for **any** visitor (guest / pending / approved) with **no**
  sign-in — it is anonymous.
- The list renders **every** active (non-soft-deleted) speaker as a card, ordered
  by `displayOrder` then name, from **one** `GET /app/speakers` call.
- Each card shows the avatar (photo or placeholder), the rank line and the name;
  `المزيد` taps through to the correct speaker's profile (20).
- When there are **no** speakers, the screen shows an empty state (no error).
- The screen renders RTL in Arabic.

## Where it fits in the journey
**Journey — Speakers**: Home (13) → **Speakers list (19)** → Speaker profile (20).
Reached from the Home **المتحدثون** tile (shown **unlocked**, anonymous) — the
companion **طلب مقابلة** tile is shown **locked 🔒** because the meeting **request**
(not the read) requires login (handled on the profile, page 20).
