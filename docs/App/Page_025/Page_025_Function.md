# Page 025 — Function (البث المباشر · Live broadcast)

What the user does on this screen. Grounded in `Mockup.html` screen 25/26 (the live
broadcast) and the §8 live stub shipped this wave (D-271).

## Privilege / auth gate
**Anonymous (read).** The live state rides on the **public, anonymous** session
detail (`GET /app/programme/sessions/{id}`, `AllowAnonymous`) — any visitor can open
the screen and watch. The one **login-only** action that pairs with this screen is
**Send a question**, which is a separate endpoint and a separate page
([Page_026](../Page_026/README.md)).

## Elements (top → bottom, from the mockup)
1. **Header** — back chevron + title `البث المباشر`.
2. **Player** — the video surface. When the session is **LIVE** (a non-null
   `LiveStreamUrl`) it plays the live feed with a **LIVE badge**; otherwise it shows
   the **recorded** stream (when `HasRecording`) or a "not yet live" placeholder.
3. **لغة الإشارة (sign-language) toggle** — shown only when the session has a
   `LiveSignLanguageUrl`; switches the player between the main live feed and the
   sign-language interpretation feed.
4. **الترجمة الفورية (live captions)** — a **client / future** overlay of live
   captions over the player (no server field yet — see Page_025_Logic L-4).
5. **Recorded / summary fallbacks** — when there is no live feed: the **recorded**
   session and the **AI summary / محضر** (cross-refs, both already built).
6. **Send-question entry** — the link/button that opens the login-only Send-question
   surface ([Page_026](../Page_026/README.md)).
7. **Bottom nav** — the five-slot bar (Sessions active).

## What the user does
1. **Watch the LIVE feed** — when `LiveStreamUrl` is non-null the app shows the live
   player + the LIVE badge and streams the broadcast (Page_025_Logic L-1).
2. **Toggle sign-language** → `لغة الإشارة` → swaps the player to the
   `LiveSignLanguageUrl` interpretation feed; the toggle is hidden when that URL is
   null (Page_025_Logic L-3).
3. **(Future) read live captions** → `الترجمة الفورية` → a client-side caption
   overlay; not driven by any current server field (Page_025_Logic L-4).
4. **Fall back to recorded / summary** — when the session is not live the user
   watches the **recorded** stream (`HasRecording`, D-232) and/or reads the **AI
   summary / محضر** (`GET …/summary`, D-237/238). Both are already built — this
   screen cross-references them (Page_025_Logic L-5).
5. **Send a question** — the user opens the login-only Send-question surface
   ([Page_026](../Page_026/README.md)); that action is gated (arrival + the
   open/close window) and is **not** part of this anonymous read.

## Acceptance criteria
- The screen is reachable **without** signing in (the live read is anonymous).
- A session with a **non-null** `LiveStreamUrl` renders the **LIVE player + badge**;
  a session with a **null** `LiveStreamUrl` renders the recorded/scheduled state
  (no live player, no badge).
- The **لغة الإشارة** toggle appears **only** when `LiveSignLanguageUrl` is non-null
  and switches the player to that feed.
- When there is no live feed, the recorded stream (`HasRecording`) and the AI
  summary (`…/summary`) are offered as the fallback (already built — cross-refs).
- The live + sign-language URLs are set by an admin in the CP Session form
  (`/admin/sessions`); the app only **reads** them on the session detail.

## Where it fits in the journey
**Journey — watch a session live**: Sessions (16, renamed from Agenda) → Session
detail (17) → **Live broadcast (25/26)**. From here the user can open the login-only
**Send question** ([Page_026](../Page_026/README.md)), or — when the session is not
live — fall back to the recorded stream and the AI summary / محضر.
