# Page 014 — API (منطقتي · My Area)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Counter and
schedule rules are in [Page_014_Logic.md](Page_014_Logic.md).

> **Status:** **BUILT (D-249).** Three additive, read-only aggregates over existing
> App-DB tables — no schema change, no enum change, no migration. Implemented as
> `IMyAreaService` (`MyAreaService`) behind `MyAreaEndpoints`; covered by
> `tests/SIMF.Api.Tests/MyAreaDashboardTests.cs` (6 tests).
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split,
> D-247) — so the routes below are `GET /api/v1/app/account/dashboard`,
> `/api/v1/app/account/calendar.ics`, `/api/v1/app/account/contact-card.vcf`.
>
> **App consumption (D-297/D-378):** the dashboard is decoded from the
> `ApiResult` envelope (`MyAreaRepository.getDashboard`); the two exports are
> fetched as **raw text** (`SimfApiClient.getText`) and handed to the native
> share sheet (temp file on Android/iOS/desktop; raw text on web).

## E1 — `GET /app/account/dashboard`  **(BUILT — D-249)**
| | |
|---|---|
| Full route | `GET /api/v1/app/account/dashboard` |
| Access | Approved account (`Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))`); own `sub`. No new permission code (app self-read). |
| App privilege | Visitor and above |
| Returns | `ApiResult<MyAreaDashboard>` |

```jsonc
// MyAreaDashboard
{
  "identity": {
    "fullNameAr": "string",   // UserProfile.ArabicName
    "fullNameEn": "string",   // UserProfile.EnglishName
    "qrId":       "string?",  // UserProfile.QrId  ← card reference (null until Approved)
    "avatarUrl":  "string?",  // resolved from the account (IAccountService) — Identity side, on read
    "tierNameEn": "string?",  // ProfileType.Name        (null if no ProfileType assigned)
    "tierNameAr": "string?",  // ProfileType.NameArabic
    "pageColor":  "string?"   // ProfileType.PageColor
  },
  "counters": {
    "bookedSessionsCount": 0, // held SeatReservation — Page_014_Logic L-2
    "meetingsCount":       0  // accepted speaker meetings ∪ confirmed business meetings — L-3
  },
  "todaySchedule": [          // merged, time-ordered, today only (event TZ = AST/UTC+3) — L-4
    {
      "kind": "Session",      // "Session" | "Meeting"
      "start": "2026-09-13T08:00:00Z",
      "end":   "2026-09-13T09:00:00Z", // null only if the source carries no end
      "titleEn": "string",    // session title; empty for a business meeting
      "titleAr": "string",
      "hallNameEn": "string?",// Session.Hall.Name / MeetingTable.Hall.Name
      "hallNameAr": "string?",// Session.Hall.NameArabic / MeetingTable.Hall.NameArabic
      "subject":  "string?",  // meeting topic (speaker request subject / business-meeting note)
      "status":   "string",   // BookingStatus / "Accepted" / "Confirmed"
      "sessionId":"guid?",    // set for Session items + speaker meetings; null for business meetings
      "meetingId":"guid?"     // set for Meeting items; null for Session items
    }
  ]
}
```

### Counter / union rules (built)
- **`bookedSessionsCount`** = held `SeatReservation` for the caller
  (`ReservedForUserId == sub`, `Kind ∈ {UserBooking, RandomAssignment}`,
  `ReleasedAt IS NULL`, active session). Page_014_Logic L-2.
- **`meetingsCount`** = accepted speaker meetings (`MeetingRequest.Status == Accepted`,
  active session) **∪** confirmed business meetings (the caller is a
  `BusinessMeetingParticipant` with `Kind == Visitor` and the meeting
  `Status == Confirmed`, D-248). L-3.
- **Today's window** is the **Arabia Standard Time** (UTC+3, the Riyadh venue, no DST)
  calendar day, computed from the injected `TimeProvider` — so an evening session
  stays on today's card. L-4.

## E2 — `GET /app/account/calendar.ics`  (Share → my full calendar)  **(BUILT)**
| | |
|---|---|
| Full route | `GET /api/v1/app/account/calendar.ics` |
| Access | Approved account; own `sub` |
| Returns | `text/calendar; charset=utf-8` (RFC 5545), `Content-Disposition: attachment; filename="simf.ics"`. One **VEVENT per item across all days** — every held booked session + every accepted speaker meeting + every confirmed business meeting. `DTSTART`/`DTEND` from the item (UTC; `DTEND` omitted when the source carries no end), `DTSTAMP` = now, `SUMMARY` = session title or meeting subject, `LOCATION` = hall name, `UID` = `{itemId:N}@simf`. Text fields RFC-5545-escaped. |

App fetches and hands to the native **share intent** / add-to-calendar.

## E3 — `GET /app/account/contact-card.vcf`  (Share → my data, QR-contact standard)  **(BUILT)**
| | |
|---|---|
| Full route | `GET /api/v1/app/account/contact-card.vcf` |
| Access | Approved account; own `sub` |
| Returns | `text/vcard; charset=utf-8` (vCard 3.0), `Content-Disposition: attachment; filename="simf.vcf"` — `FN`/`N` (name, EN preferred then AR), `TITLE` (`UserProfile.JobTitle`), `ORG` (`Organisation.NameEn ?? NameAr`), `UID` = `QrId` and `NOTE` = `SIMF {QrId}` (the badge's unique key; both omitted when `QrId` is null). |

App hands to the native **share intent** — from BOTH the identity-card **مشاركة**
button and the **مشاركة جهة اتصال** tile. No badge-image endpoint — the QR is
rendered client-side from `qrId`.

## E4 — `POST /app/auth/sign-out`  (تسجيل الخروج row, D-373)  **(BUILT — pre-existing auth endpoint)**
| | |
|---|---|
| Full route | `POST /api/v1/app/auth/sign-out` |
| Access | Any authenticated caller (valid access token; rate-limited `auth` policy) |
| Returns | `ApiResult<SignOutResponse>` — ends **every** session for the account (SIMF-API-001 §12.4) |

Called via `AuthController.signOut()` after the confirm dialog. The client sends
`{ "refreshToken": ... }` in the body (the endpoint takes no request — revocation
is account-wide off the token's `sub`); the call is **best-effort**: the local
session is cleared and the app lands on `/sign-in` even if the wire call fails.

## Reused existing reads (no contract change)
| Source | Used for |
|---|---|
| `IAccountService.GetProfileAsync` (Identity side) | `avatarUrl` — resolved on read, a second query on the other context (D-157, no cross-DB join) |
| App-DB `UserProfiles` + `ProfileType` | names, `QrId`, job title, organisation, tier name + colour |
| App-DB `SeatReservations` / `MeetingRequests` / `BusinessMeetingParticipants` | the counters + schedule union |

## Error responses
| HTTP | When |
|------|------|
| 401 | Missing / invalid token |
| 403 | Authenticated but **not Approved** (`RequireApprovedAccount`) — a pending/rejected user shows the limited card from cached identity instead |
| 404 | (n/a — the aggregate degrades gracefully: a user with no profile returns empty names + null `qrId`, zero counters, empty schedule) |
