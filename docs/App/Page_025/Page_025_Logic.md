# Page 025 — Logic (البث المباشر · Live broadcast)

Business rules behind the live broadcast screen. Verified against the §8 live stub
shipped this wave (D-271) on `PublicSessionDetail`, and the already-built recorded
streaming (D-232) + AI summary (D-237/238). The live read adds **no** new endpoint —
it is **append-only** fields on the existing anonymous session-detail read.

## L-1 Live vs recorded is decided by one field — `LiveStreamUrl`
The screen renders from the **single** anonymous read
`GET /app/programme/sessions/{id}` → `PublicSessionDetail`. Two **append-only**
fields (D-271, D-219 append-only) decide the live state:

| Field | Drives |
|-------|--------|
| `LiveStreamUrl` (`string?`) | **non-null** → the session has a **LIVE broadcast** (the app shows the LIVE player + badge); **null** → recorded / scheduled (no live feed) |
| `LiveSignLanguageUrl` (`string?`) | the **optional** sign-language interpretation feed — drives the **لغة الإشارة** toggle; null → no toggle |

There is **no** separate "is live" boolean — `LiveStreamUrl != null` **is** the
live signal. The whole live decision is a client derivation over that one nullable
URL.

## L-2 Provider = YouTube (POC) + HLS/MP4 fallback (D-349)
The live URLs are admin-authored, and the live-video **provider is YouTube** (D-349,
resolving the old D7 deferral): an admin pastes a YouTube watch/live link — or a
direct **HLS/MP4** stream URL — into the CP Session form and the app plays it. The
app **sniffs** each URL: a YouTube link plays via the official **IFrame player**
(`youtube_player_iframe`), anything else via `video_player` (the HLS/MP4 fallback).
Both the CP form and the API validate the URL with the shared `LiveStreamUrlPolicy`
rule (YouTube host OR a `.m3u8`/`.mp4` path, https). A future **managed** provider
can still replace this without changing the screen — the player source stays
pluggable.

## L-3 The sign-language toggle = `LiveSignLanguageUrl` (built, D-349)
`لغة الإشارة` (sign-language) is a `البث / لغة الإشارة` toggle shown **only** when
the session carries **both** `LiveStreamUrl` and `LiveSignLanguageUrl`. Toggling it
swaps the player source between the main feed and the interpretation feed (each is
sniffed independently — either can be a YouTube link or HLS/MP4). When there is a
sign feed but **no** main feed, the screen shows the "sign-language available" note
instead (there is nothing to toggle between). Built D-349 (previously only a note).

## L-4 Live captions (الترجمة الفورية) = YouTube CC (YT path) / future (HLS)
`الترجمة الفورية` (live captions / instant translation) has **no** server field on
`PublicSessionDetail` — nothing to bind. For a **YouTube** feed the IFrame player's
own **CC** control covers it (D-349). For an HLS/MP4 feed it stays a **client /
future** item (a possible client-side caption overlay later), not a shipped API
contract.

## L-5 No live feed → fall back to recorded + the AI summary (already built)
When `LiveStreamUrl` is null the session is recorded / scheduled, and the screen
falls back to two **already-built** surfaces (cross-refs — **no new build**):

- **Recorded streaming** — `PublicSessionDetail.HasRecording` (D-232) is true when
  the published session has a recording the app can stream; the app then POSTs the
  recording stream-token endpoint and plays the token-gated, range-streaming URL.
  See [Page_025_API.md](Page_025_API.md) E2.
- **AI session summary / محضر** — `GET /app/programme/sessions/{id}/summary`
  (D-237/238) returns the published bilingual summary (`PublicSessionSummary`),
  anonymous and gated by the summary's `PublishedAt` (404 until the Committee
  publishes it). See [Page_025_API.md](Page_025_API.md) E3.

## L-6 The live URLs are admin-authored in the CP
An admin sets both URLs in the **CP Session form** (`/admin/sessions` →
`SessionForm.razor`, fields **"Live stream URL (live broadcast)"** + **"Sign-language
stream URL (optional)"**), which reuses the `Sessions.Edit` permission. The app
never writes these — it only **reads** them on the session detail. A blank field is
persisted as **null** (the CP nulls blank input), so clearing the URL takes the
session back to the recorded/scheduled state. Each non-blank URL must be a YouTube
link or an HLS/MP4 stream — validated client-side (the form shows a format hint) and
server-side (`AdminSessionService` → 400 `SESSION_INVALID`) by the shared
`LiveStreamUrlPolicy` (D-349).

## L-7 Edge cases
- **Not live yet** — `LiveStreamUrl` null → no LIVE player / badge; show the
  recorded/scheduled state (recorded stream if `HasRecording`, else "not yet live").
- **Live, no interpretation** — `LiveStreamUrl` set, `LiveSignLanguageUrl` null →
  LIVE player + badge, **no** لغة الإشارة toggle.
- **Session soft-deleted / missing** — the detail read 404s
  (`ErrorCodes.SessionNotFound`) → "session removed" state.
- **Feed URL unreachable** — the player fails to load the manual URL → a player
  error / retry state (the URL is an interim manual value, L-2).

## L-8 Localization
Arabic primary (RTL), English secondary. The title `البث المباشر`, the LIVE badge,
the `لغة الإشارة` toggle, and the `الترجمة الفورية` caption label are bilingual per
the active locale; the player surface itself mirrors RTL chrome while the video is
neutral.
