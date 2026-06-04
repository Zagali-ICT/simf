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

## L-2 Interim manual-URL stub provider
The live URLs are an **interim manual-URL "stub provider"**: an admin pastes a feed
URL into the CP Session form and the app plays it. A real **managed** live provider
(scheduling, ingest, the broadcast lifecycle) **replaces** this later — it is
**deferred** (D-211 D7). Until then, `LiveStreamUrl` being set is a manual signal,
not a provider event.

## L-3 The sign-language toggle = `LiveSignLanguageUrl`
`لغة الإشارة` (sign-language) is shown **only** when `LiveSignLanguageUrl` is
non-null. Toggling it swaps the player source between the main `LiveStreamUrl` feed
and the `LiveSignLanguageUrl` interpretation feed. When `LiveSignLanguageUrl` is
null the toggle is hidden — the broadcast has no interpretation feed.

## L-4 Live captions (الترجمة الفورية) = client / future
`الترجمة الفورية` (live captions / instant translation) is a **client / future**
item. There is **no** server field for it on `PublicSessionDetail` today — nothing
to bind. The screen may render a client-side caption overlay later; this doc records
it as a planned client feature, not a shipped API contract.

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
session back to the recorded/scheduled state.

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
