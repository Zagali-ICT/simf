# Page 026 — API (إرسال سؤال · Send a question)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The submit rules + the review pipeline are in
[Page_026_Logic.md](Page_026_Logic.md).

> **Status:** **BUILT.** The composer writes to the existing public submission
> endpoint `POST /app/sessions/{id}/questions` (D-169), with the Speaker / Host
> recipient (D-174), the advisory AI filter on submit (D-236), the
> Scientific-Committee `Pending` landing (D-212) and the arrival gate (D-242). No
> new endpoint in this wave (D-271).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the route below is `POST /api/v1/app/sessions/{id}/questions`.

## E1 — `POST /app/sessions/{sessionId}/questions`  (submit a question)  **(BUILT — D-169 / D-174 / D-212 / D-236 / D-242)**
| | |
|---|---|
| Full route | `POST /api/v1/app/sessions/{sessionId:guid}/questions` |
| Access | **`RequireApprovedAccount`** — approved, signed-in (login-only) |
| Body | `SubmitSessionQuestionRequest` |
| Returns | `ApiResult<SessionQuestionSubmitted>` — the submitter's own queue position |

```jsonc
// SubmitSessionQuestionRequest  (request body)
{
  "questionText": "What about CPS resilience?",   // 1–1000 chars, trimmed
  "recipient": "Speaker",                          // "Speaker"=0 (المتحدث) | "Host"=1 (المضيف); default Speaker
  "isAtVenue": true                                // self-assert "I am at the venue" (used when the hall has no geofence)
}
```

```jsonc
// SessionQuestionSubmitted  (response data)
{
  "id": "guid",
  "sessionId": "guid",
  "order": 0,                 // ← the submitter's OWN queue position (not the whole queue)
  "createdAt": "2026-11-23T10:05:00Z"
}
```

### Submit gate (server — the THREE rules)
| Rule | Pass condition | Fail |
|---|---|---|
| **1. Arrival at the hall** (D-242) | geofenced hall → a `HallAttendance` arrival record; else `isAtVenue == true` | `403 NOT_AT_VENUE` |
| **2. Opens 5 min before start** (`PreStartWindow = 5min`) | `now ≥ StartUtc − 5min` | `400 SESSION_NOT_LIVE_FOR_QUESTIONS` |
| **3. Closes at the end** (`PostEndWindow = 0`) | `now ≤ EndUtc` | `400 SESSION_NOT_LIVE_FOR_QUESTIONS` |

A successful submission lands **`Status = Pending`** (D-212) and is tagged by the
**advisory AI filter** (`AiFilterVerdict`, D-236 — does not change the status). It
reaches the per-session moderator desk only after Committee approval (see the
moderation surface below).

## E2 — moderation surface (admin / granted moderator — context only)  **(BUILT — D-169 / D-212)**
The composer does **not** call these; they are where a submitted question is
reviewed before going on air (Page_026_Logic L-3):
| Route | Verb | Does |
|---|---|---|
| `/api/v1/admin/questions/{id}/approve` | PUT | Scientific-Committee approves a `Pending` question (stage 2) |
| `/api/v1/app/sessions/{id}/questions/moderate` | GET | the per-session moderator queue (approved questions only) → `SessionQuestionModeratorRow[]` |
| `/api/v1/app/sessions/{id}/questions/{qid}/hide` | PUT | hide / unhide one question (`SetQuestionHiddenRequest`, idempotent) |
| `/api/v1/app/sessions/{id}/questions/{qid}/push` | PUT | push the question on air (stamps `PushedAt`, idempotent) |

The moderate queue is gated to an admin **or** a granted per-session
`SessionModerator`; a plain visitor is `403`.

## Error responses
| HTTP | Code | When |
|------|------|------|
| 400 | `SESSION_QUESTION_INVALID` | empty / whitespace `questionText` |
| 400 | `SESSION_NOT_LIVE_FOR_QUESTIONS` | more than 5 min before start, or after the end (rule 2 / 3) |
| 403 | `NOT_AT_VENUE` | no arrival record **and** `isAtVenue = false` (rule 1) |
| 401 | — | no/expired token — the submit is login-only |
| 403 | — | account not approved (pending/rejected) |
| 404 | — | session missing / soft-deleted |

## Build dependencies
**None.** The endpoint exists and is tested
(`tests/SIMF.Api.Tests/SessionQuestionsTests.cs`):
- **The three window tests** —
  `Questions_are_closed_more_than_five_minutes_before_start`,
  `Questions_open_within_five_minutes_before_start`,
  `Questions_are_closed_after_the_session_ends`.
- **The arrival-gate tests** — `Submit_inside_live_window_returns_OK_with_queue_position_zero`
  (and the `IsAtVenue = true` window seeds), `Submit_without_at_venue_flag_is_403_NOT_AT_VENUE`.
- **Pipeline / recipient** — `Submit_lands_pending_with_computed_phase` (lands
  `Pending` + `AiFilterVerdict = "stub-clean"`, D-212/D-236),
  `Submit_with_Host_recipient_round_trips_in_moderator_queue` (D-174),
  `Submit_with_empty_text_is_SESSION_QUESTION_INVALID`, and the moderator-queue +
  hide + push + authorization tests.

No schema change, no migration in this wave (D-271) — the composer is a write to
the shipped session-questions surface.
