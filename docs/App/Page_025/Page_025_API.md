# Page 025 — API (البث المباشر · Live broadcast)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. The
live-vs-recorded derivation rules are in [Page_025_Logic.md](Page_025_Logic.md).

> **Status:** **BUILT — §8 live stub (D-271).** The live state is two **append-only**
> fields (`LiveStreamUrl` + `LiveSignLanguageUrl`, append-only per D-219) on the
> existing **anonymous** session detail — **no new endpoint**. The recorded stream
> (`HasRecording`, D-232) and the AI summary (`…/summary`, D-237/238) are
> **already built** and cross-referenced below.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/programme/sessions/{id}` etc.

## E1 — `GET /app/programme/sessions/{id}`  (the session detail + live state)  **(BUILT — D-271 append-only)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions/{id:guid}` |
| Access | **`AllowAnonymous`** — the live read is part of the public session detail |
| Returns | `ApiResult<PublicSessionDetail>` — the full session + the §8 live fields |

The live broadcast is decided entirely by the two appended fields (the rest of
`PublicSessionDetail` is the standard session detail — title, hall, time window,
themes, speakers, seats, status):

```jsonc
// PublicSessionDetail (live-relevant fields only)
{
  "id": "guid",
  "status": "Published",           // SessionStatus
  "hasRecording": true,            // D-232 — a streamable recording exists (recorded fallback)
  "liveStreamUrl": "https://live.example/stream.m3u8",  // §8 D-271 — non-null = LIVE
  "liveSignLanguageUrl": "https://live.example/sign.m3u8" // §8 D-271 — optional sign-language feed
}
```

### Live-state derivation (client)
| State | Rule |
|---|---|
| **LIVE** (player + LIVE badge) | `liveStreamUrl != null` |
| **Sign-language toggle shown** (`لغة الإشارة`) | `liveSignLanguageUrl != null` |
| **Recorded / scheduled** (no live feed) | `liveStreamUrl == null` → fall back to E2 / E3 |
| **Live captions** (`الترجمة الفورية`) | **client / future** — no server field (Page_025_Logic L-4) |

`liveStreamUrl` + `liveSignLanguageUrl` are an **interim manual-URL stub provider**
set by an admin (E4); a real managed provider replaces them later (deferred, D-211
D7).

## E2 — recorded streaming (fallback when not live)  **(BUILT — D-232, cross-ref)**
When `liveStreamUrl` is null and `hasRecording` is true, the app streams the
recording via the **token-gated** recording endpoint (a short-lived stream token
appended on the query string, since an HTML5 `<video>` cannot set an Authorization
header — `RecordingStreamTokenResponse`). This is the **recorded** path documented
with the session-recording surface (D-232) — **no new build for this page**.

## E3 — AI session summary / محضر (fallback when not live)  **(BUILT — D-237/238, cross-ref)**
| | |
|---|---|
| Full route | `GET /api/v1/app/programme/sessions/{id:guid}/summary` |
| Access | **`AllowAnonymous`** — published editorial content, no attendee PII |
| Returns | `ApiResult<PublicSessionSummary>` — the bilingual محضر |

Returns the published AI session summary (key points / recommendations / speakers /
full text, each bilingual + `generatedByAi` + `publishedAt`). **404** until the
Committee publishes it (gated by the summary's `PublishedAt`). Already built
(D-237/238) — **no new build for this page**.

## E4 — admin sets the live URLs (CP, not an app endpoint)
The live + sign-language URLs are authored by an admin in the **CP Session form**
(`/admin/sessions` → `SessionForm.razor`), fields **"Live stream URL (live
broadcast)"** + **"Sign-language stream URL (optional)"** (`Admin.Sessions.Field.
LiveStreamUrl` / `…LiveSignLanguageUrl`; AR `رابط البث المباشر` / `رابط بث لغة
الإشارة (اختياري)`). The form reuses the **`Sessions.Edit`** permission. A blank
field is persisted as **null**. The app **reads** these on E1 — it never writes them.

## E5 — Send a question is a separate, login-only surface
The **Send-question** action paired with a live session is **not** on this anonymous
read — it is the login-only endpoint on [Page_026](../Page_026/README.md)
(`POST /app/sessions/{id}/questions`, `RequireApprovedAccount`), gated by arrival +
the open/close window. Out of scope for Page 025's read.

## Error responses
| HTTP | When |
|------|------|
| 404 | session missing / soft-deleted (`ErrorCodes.SessionNotFound`); or (E3) no published summary yet |
| — | live read is anonymous: no 401/403 on E1 / E3 |

## Build dependencies
**None new for this page.** The §8 live fields shipped this wave (D-271) — additive
nullable `Session.LiveStreamUrl` + `Session.LiveSignLanguageUrl` (migration
`App/D271_AddSessionLiveStream`), surfaced append-only on `PublicSessionDetail`,
tested by
[`tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs`](../../../tests/SIMF.Api.Tests/ProgrammeSessionsTests.cs)
**`Session_detail_carries_live_stream_urls_when_set`** (a session with manually-set
live + sign-language URLs surfaces both on the detail read). The recorded stream
(D-232) and the AI summary (D-237/238) were built earlier and are reused as-is.
