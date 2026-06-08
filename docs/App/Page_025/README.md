# Page 025 — البث المباشر · Live broadcast

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_025_Function.md](Page_025_Function.md) | What the user does — watch the LIVE feed, toggle sign-language, read live captions (future), fall back to recorded + the AI summary |
| Logic | [Page_025_Logic.md](Page_025_Logic.md) | The live-vs-recorded distinction (`LiveStreamUrl` non-null = LIVE), the sign-language toggle, captions-as-client, the recorded + محضر cross-refs |
| API | [Page_025_API.md](Page_025_API.md) | The backend endpoints + DTOs this page reads (authoritative contract) — the live read is **append-only** on the session detail |
| Design | [Page_025_Design.md](Page_025_Design.md) | Flutter screen design — player, LIVE badge, لغة الإشارة toggle, captions, recorded/summary fallbacks, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **25 / 26** (`Mockup.html` — live broadcast) |
| Route | `RouteNames.liveBroadcast` → `/sessions/:sessionId/live` |
| Titles | AR **البث المباشر** · EN **Live broadcast** |
| Section | 2 — Core screens |
| Nature | **Live / recorded session player** — the LIVE feed + a LIVE badge, an optional sign-language interpretation feed, client live captions (future), and a recorded + AI-summary fallback |
| App privilege | **Anonymous (read).** The live read rides on the public, anonymous session detail (`GET /app/programme/sessions/{id}`, `AllowAnonymous`). The **Send-question** action is login-only and lives on [Page_026](../Page_026/README.md). |
| Status | API **BUILT** (D-271, additive `LiveStreamUrl` + `LiveSignLanguageUrl`); Flutter player **BUILT** — YouTube (D-349) with an HLS/MP4 fallback + the sign-language toggle |

## Sources of truth (read first)
`Mockup.html` screen 25/26 (the visual) · SIMF-MOB-API-001 (shared API conventions
+ auth) · `DECISIONS_LOG` **D-271** (this wave — the §8 live stub on the session
detail) + **D-232** (recorded streaming, `HasRecording`) + **D-237 / D-238** (the
AI session summary / محضر) + **D-349** (the live-video provider — **YouTube** (POC)
with an HLS/MP4 fallback + the sign-language toggle; resolves the old D-211 D7 deferral).

## Headline (D-271, 2026-06-03)
> A session with a non-null **`LiveStreamUrl`** is broadcasting **LIVE** — the app
> shows the live player + a LIVE badge. A null `LiveStreamUrl` means the session is
> **recorded / scheduled** (no live feed). The optional **`LiveSignLanguageUrl`**
> drives the live screen's **لغة الإشارة** (sign-language) toggle. Both come from
> the **one** anonymous session-detail read (`GET /app/programme/sessions/{id}` →
> `PublicSessionDetail`, append-only); an admin sets both in the CP Session form
> (`/admin/sessions`).

Live captions (**الترجمة الفورية**) are a **client / future** item. When there is no
live feed, the screen falls back to the **recorded** stream (`HasRecording`, D-232)
and the **AI summary / محضر** (`GET …/summary`, D-237/238) — both **already built**.
See [Page_025_Logic.md](Page_025_Logic.md) and [Page_025_API.md](Page_025_API.md).

## As-built — Flutter screen

The Flutter screen is built as an interim mockup against the real anonymous API.

| | |
|---|---|
| Route | `RouteNames.liveBroadcast` → `/live?sessionId=` (optional `sessionId` query param) |
| Screen | `LiveBroadcastScreen` — `src/Mobile/simf_app/lib/features/live/live_broadcast_screen.dart` |
| Data | `LiveRepository` + `LiveSession` — `src/Mobile/simf_app/lib/features/live/data/live_repository.dart` |
| Player | **YouTube** via `youtube_player_iframe` for a YouTube `liveStreamUrl`; `video_player` for an HLS/MP4 URL (fallback). The feed is sniffed by `YoutubeUrl`; the `_LivePlayer` widget owns its controller and is keyed by the active feed URL (D-349) |

Reuses the shipped public session detail read (no new API — D-271):
`GET /api/v1/app/programme/sessions/{id}` → `ApiResult<PublicSessionDetail>`. The
screen decodes only the broadcast slice into `LiveSession`: `title`/`titleArabic`,
`status` (int — frozen `SessionStatus` 0..3), `hasRecording` (bool),
`liveStreamUrl` (string?), `liveSignLanguageUrl` (string?). The slice lives in a
small dedicated `LiveRepository` because the `features/sessions` `SessionDetail`
model does **not** expose the broadcast fields — same wire contract, no duplicate
of the full detail model.

Behaviour:
- **No `sessionId`** (null/blank) → a "no live session selected — open a session
  to watch" empty state. No fetch, no controller (L-1).
- **With a `sessionId`** → read the slice, then branch (L-3):
  - `liveStreamUrl` non-empty → **sniff it** (D-349): a YouTube link plays via the
    `youtube_player_iframe` IFrame player; an HLS/MP4 URL via `video_player` at the
    stream's aspect ratio. Both show a **LIVE** badge. When the session also has a
    `liveSignLanguageUrl`, a `البث / لغة الإشارة` toggle swaps the player source.
  - `liveStreamUrl` null but `hasRecording` → a "recording available" note.
  - neither → a "not live / scheduled" state.
- A `liveSignLanguageUrl` with **no** main feed adds a sign-language-available note.
- A feed that fails to load (unreachable, or a YouTube URL with no playable id) shows
  an **error + Retry** surface (D-349 / L-7), not an endless spinner.
- Loading / empty(no id) / 404→not-found / error→retry states.

UI is interim (final visuals from SIMF-VID-001).

### Tests
- Unit: `src/Mobile/simf_app/test/features/live/youtube_url_test.dart` — the
  YouTube/HLS URL rule + the video-id parser (D-349).
- Widget: `src/Mobile/simf_app/test/features/live/live_broadcast_screen_test.dart`
  (no-id empty, blank-id empty, not-live, recording note, sign-language note,
  both-feeds toggle / single-feed no-toggle, 404, error→retry, Arabic) — the
  **non-video** paths only; real YouTube/HLS playback needs a device/emulator.
- E2E: [`docs/tests/e2e/mobile-live.md`](../../tests/e2e/mobile-live.md).
