# Page 025 — Design (البث المباشر · Live broadcast)

Flutter screen design. Grounded in `Mockup.html` screen 25/26 (the live broadcast).
RTL, Arabic-primary.

## Layout (top → bottom, from the mockup)
1. **App bar** — back chevron + title `البث المباشر`.
2. **Player** — the video surface filling the top of the screen:
   - **LIVE** (when `liveStreamUrl != null`) — plays the live feed with a **LIVE
     badge** overlaid (a pulsing brass/red `مباشر · LIVE` chip).
   - **Recorded** (when `liveStreamUrl == null` and `hasRecording`) — plays the
     token-gated recording (Page_025_API E2).
   - **Not yet live** (neither) — a "not yet live" placeholder.
3. **لغة الإشارة (sign-language) toggle** — a toggle/segment shown **only** when
   `liveSignLanguageUrl != null`; switches the player source between the main feed
   and the interpretation feed.
4. **الترجمة الفورية (live captions)** — a **client / future** caption overlay /
   toggle over the player (no server field — Page_025_Logic L-4).
5. **Recorded / summary fallbacks** — when not live: a "Watch recording" affordance
   (E2) and a "Read summary / المحضر" affordance to the AI summary (E3).
6. **Send-question entry** — a button/link to the login-only Send-question surface
   ([Page_026](../Page_026/README.md)).
7. **Bottom nav** — the five-slot bar (Sessions active).

## Data binding
- **Player + LIVE badge** bind to `PublicSessionDetail.liveStreamUrl` (Page_025_API
  E1): non-null → live player + badge; null → recorded/scheduled state.
- **لغة الإشارة toggle** binds to `liveSignLanguageUrl`: render the toggle only when
  non-null; on toggle, swap the player source to that feed.
- **الترجمة الفورية** — client-side only; no binding (Page_025_Logic L-4).
- **Recorded fallback** binds to `hasRecording` (D-232) → POST the recording
  stream-token endpoint and play the token-gated URL (Page_025_API E2).
- **Summary fallback** → `GET …/summary` → `PublicSessionSummary` (D-237/238); the
  affordance is hidden / disabled until that read returns (404 = not yet published)
  (Page_025_API E3).
- **Send question** → navigate to the login-only Page_026 surface (not part of this
  read).

## States
- **Loading** — skeleton player while the (anonymous) `GET …/sessions/{id}` runs.
- **LIVE** — live player + LIVE badge; لغة الإشارة toggle when a sign feed exists.
- **Recorded** — `liveStreamUrl` null + `hasRecording` → recording player; "Read
  summary" if the محضر is published.
- **Not yet live** — neither live nor recorded → "not yet live" placeholder + (if
  published) the summary.
- **Feed error** — the manual feed URL fails to load → a player error / retry state
  (the URL is an interim manual value, Page_025_Logic L-2).
- **Removed** — 404 on the detail read → "session removed".

## RTL / localization
- Whole screen mirrored RTL; the back chevron follows RTL.
- The title `البث المباشر`, the **LIVE** badge (`مباشر · LIVE`), the `لغة الإشارة`
  toggle, the `الترجمة الفورية` caption label, and the recorded / summary affordances
  are bilingual per the active locale.
- The mine/live accents (LIVE badge, active toggle) use **theme tokens** (no raw
  colours); the video surface itself is locale-neutral.

## Provider note (D-349)
The live-video **provider is YouTube** (proof of concept) with a direct **HLS/MP4**
stream as a fallback. An admin pastes the URL in the CP Session form
(`/admin/sessions`, `Sessions.Edit`); the player **sniffs** it (a YouTube link →
the IFrame player, otherwise `video_player`). The player source stays pluggable, so
a future **managed** provider can replace YouTube without changing the screen. This
resolves the old D7 deferral.
