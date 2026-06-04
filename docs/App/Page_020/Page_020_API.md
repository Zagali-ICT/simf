# Page 020 — API (ملف متحدث · تفاصيل المتحدث · Speaker profile)

Authoritative backend contract for this page. Inherits the `ApiResult<T>`
envelope, headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001
§3–§4. The tab/gate/flow rules are in [Page_020_Logic.md](Page_020_Logic.md).

> **Status:** the **profile read** (E1) is **BUILT** with the Speakers branch
> (D-199, `AllowAnonymous`). The **meeting request** (E2) is **BUILT — NEW**
> (D-269, login-only). The admin review desk (E3) is the CP companion.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/speakers/{id}` etc. The admin
> desk (E3) is under **`/admin/*`** (CP), not the app surface.

## E1 — `GET /app/speakers/{id}`  (the speaker profile + CV + sessions)  **(BUILT — D-199)**
| | |
|---|---|
| Full route | `GET /api/v1/app/speakers/{id:guid}` |
| Access | **`AllowAnonymous`** — guest + (the profile read is open, D-199) |
| Returns | `ApiResult<PublicSpeakerDetail>` — the profile, the four CV tabs, the gate flags + the sessions |

```jsonc
// PublicSpeakerDetail
{
  "id": "guid",
  "name": "Capt. …", "nameArabic": "القبطان …",
  "rank": "القبطان البحري",
  "countryId": 682, "countryNameEn": "Saudi Arabia", "countryNameAr": "السعودية",

  // the four CV tabs (each is a rich-text AR/EN pair):
  "bio": "…",                        "bioArabic": "…",                  // tab 1 — نبذة عنه
  "qualifications": "…",             "qualificationsArabic": "…",       // tab 2 — المؤهلات العلمية
  "trainingExperience": "…",         "trainingExperienceArabic": "…",   // tab 3 — الخبرات التدريبية
  "awards": "…",                     "awardsArabic": "…",               // tab 4 — الجوائز

  "allowsMeetingRequests": true,     // ← gates the طلب مقابلة button (E2)
  "allowsDataSharing": true,         // ← gates the social links below

  "facebookUrl": "https://…",        // only meaningful when allowsDataSharing == true
  "linkedInUrl": "https://…",
  "xUrl": "https://…",

  "photoRelativePath": "speakers/….webp",
  "displayOrder": 10,

  "sessions": [                      // ← the speaker's sessions (PublicSpeakerSession)
    {
      "id": "guid", "code": "S-12",
      "title": "…", "titleArabic": "…",
      "hallId": "guid", "hallName": "Main Hall", "hallNameArabic": "القاعة الرئيسية",
      "startUtc": "2026-09-01T08:00:00Z", "endUtc": "2026-09-01T09:30:00Z"
    }
  ]
}
```

### Tab mapping (client)
| Tab (mockup) | Field pair |
|---|---|
| `نبذة عنه` | `bio` / `bioArabic` |
| `المؤهلات العلمية` | `qualifications` / `qualificationsArabic` |
| `الخبرات التدريبية` | `trainingExperience` / `trainingExperienceArabic` |
| `الجوائز` | `awards` / `awardsArabic` |

Social links are surfaced **only when `allowsDataSharing == true`**; the
`طلب مقابلة` button is shown **only when `allowsMeetingRequests == true`**
(Page_020_Logic L-3 / L-5).

> **List companion (Page 19):** `GET /api/v1/app/speakers` (`AllowAnonymous`) →
> `ApiResult<PublicSpeakers>` (`{ items: PublicSpeakerSummary[] }`, ordered by
> `displayOrder` then `name`) is the list that links into this profile. Both
> reads are tested by `tests/SIMF.Api.Tests/PublicSpeakersTests.cs`.

## E2 — `POST /app/speakers/{speakerId}/meeting-requests`  (request a meeting)  **(BUILT — NEW, D-269)**
| | |
|---|---|
| Full route | `POST /api/v1/app/speakers/{speakerId:guid}/meeting-requests` |
| Access | **`RequireApprovedAccount`** — approved Visitor, **login-only**; **rate-limited** |
| Body | `SubmitSpeakerMeetingRequestRequest = { requesterName, subject }` |
| Returns | `ApiResult<SpeakerMeetingRequestSubmitted>` |

```jsonc
// request body — SubmitSpeakerMeetingRequestRequest
{ "requesterName": "…",   // 1..128
  "subject": "…" }         // 1..1000

// response — SpeakerMeetingRequestSubmitted
{ "id": "guid",
  "speakerId": "guid",
  "status": "Pending",     // always created Pending — an admin reviews it (E3)
  "createdAt": "2026-06-03T10:00:00Z" }
```

### Validation order + errors (E2)
| Check | Failure |
|---|---|
| speaker exists + is active | **404 `SPEAKER_NOT_FOUND`** |
| `allowsMeetingRequests == true` | **409 `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`** |
| `requesterName` 1..128 **and** `subject` 1..1000 | **400 `SPEAKER_MEETING_REQUEST_INVALID`** |

The request lands as a **new, dedicated `SpeakerMeetingRequest`** row (status
`Pending`) — **separate** from the session-scoped `MeetingRequest` (mockup
screen 27 "Request interview"); it is not tied to any session. Audit event
**`SpeakerMeetingRequest.Submitted`** is written. Tested by
`tests/SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs`.

## E3 — admin review desk (CP, not the app surface)  **(BUILT — D-269)**
The Pending request is reviewed by an admin in the Control Panel desk
**`/admin/speaker-meeting-requests`** (permissions `SpeakerMeetingRequests.View`
/ `SpeakerMeetingRequests.Manage`). All `RequireApprovedAccount` + permission.

| Route | Verb | Does |
|---|---|---|
| `/admin/speaker-meeting-requests/list` | POST | list/filter the requests (audit `Admin.SpeakerMeetingRequestsListed`) |
| `/admin/speaker-meeting-requests/{id}` | GET | open one — **adds `requesterEmail` (PII, detail only)** (audit `Admin.SpeakerMeetingRequestViewed`) |
| `/admin/speaker-meeting-requests/{id}/respond` | PUT | respond **Accepted / Rejected** (+ optional note); `Pending → Pending` is **400 `SPEAKER_MEETING_REQUEST_STATUS_INVALID`** (audit `SpeakerMeetingRequest.Responded`) |

The requester's e-mail is exposed **only** on the admin detail (E3), never back
to the app (E1/E2 carry no requester PII). The app does not call E3 — it is the
back-office side of the same `SpeakerMeetingRequest` entity.

## Error responses (app surface)
| HTTP | Code | When |
|------|------|------|
| 200 | — | profile read (E1, anonymous); meeting submitted (E2, approved) |
| 400 | `SPEAKER_MEETING_REQUEST_INVALID` | E2 — `requesterName`/`subject` out of range |
| 401 | — | E2 with no/expired token (guest tapping `طلب مقابلة` is prompted to sign in) |
| 403 | — | E2 with a non-approved account (pending/rejected) |
| 404 | `SPEAKER_NOT_FOUND` | speaker missing / soft-deleted (E1 read **or** E2 submit) |
| 409 | `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` | E2 — the speaker has `allowsMeetingRequests == false` |

## Build dependencies
**Reads:** none — E1 (+ the Page-19 list) ship with D-199 and are tested by
`tests/SIMF.Api.Tests/PublicSpeakersTests.cs`. **Meeting request:** the new
`SpeakerMeetingRequest` entity/table + E2 + the CP desk (E3) ship with **D-269**,
tested by `tests/SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs`; the desk is
permission-gated (`SpeakerMeetingRequests.View` / `Manage`). E2E catalogue:
[`docs/tests/e2e/mobile-speaker-profile.md`](../../tests/e2e/mobile-speaker-profile.md).
