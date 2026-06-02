# Page 014 — API (منطقتي · My Area)

Authoritative backend contract for this page. Inherits the `ApiResult<T>` envelope,
headers, error model and auth from SIMF-API-001 + SIMF-MOB-API-001 §3–§4. Counter and
schedule rules are in [Page_014_Logic.md](Page_014_Logic.md).

> **Status:** spec drafted, **not built**. All three endpoints are **additive,
> read-only aggregates over existing tables** — no schema change, no enum change, no migration.
>
> **Path-prefix note:** App routes are under **`/api/v1/app/*`** (App↔CP split shipped,
> D-247) — so the routes below are `GET /api/v1/app/account/dashboard`,
> `/api/v1/app/account/calendar.ics`, `/api/v1/app/account/contact-card.vcf`.

## E1 — `GET /account/dashboard`
| | |
|---|---|
| Access | Approved account (`RequireApprovedAccount`); own `sub`. No new permission code. |
| App privilege | Visitor and above |
| Returns | `ApiResult<MyAreaDashboard>` |

```jsonc
// MyAreaDashboard
{
  "identity": {
    "fullNameAr": "string",   // UserProfile.ArabicName
    "fullNameEn": "string",   // UserProfile.EnglishName
    "qrId":       "string",   // UserProfile.QrId  ← card reference (null until Approved)
    "avatarUrl":  "string?",  // existing GET /account/avatar/{userId}
    "tierNameEn": "string",   // ProfileType.Name        (resolved from UserProfile.ProfileTypeId)
    "tierNameAr": "string",   // ProfileType.NameArabic
    "pageColor":  "string"    // ProfileType.PageColor
  },
  "counters": {
    "bookedSessionsCount": 0, // held SeatReservation — Page_014_Logic L-2
    "meetingsCount":       0  // speaker ∪ B2B/B2C meetings — Page_014_Logic L-3
  },
  "todaySchedule": [          // merged, time-ordered, today only — Page_014_Logic L-4
    {
      "kind": "Session",      // "Session" | "Meeting"
      "startUtc": "2026-09-13T08:00:00Z",
      "endUtc":   "2026-09-13T09:00:00Z", // null for meetings
      "titleEn": "string",
      "titleAr": "string",
      "hallName": "string?",  // Session.Hall.Name
      "subject":  "string?",  // meeting topic
      "status":   "string",   // BookingStatus / MeetingRequestStatus
      "sessionId":"guid",
      "meetingId":"guid?"
    }
  ]
}
```

## E2 — `GET /account/calendar.ics`  (Share → my full calendar)
| | |
|---|---|
| Route | `GET /api/v1/account/calendar.ics` (or `/account/calendar` + `Accept: text/calendar`) |
| Access | Approved account; own `sub` |
| Returns | `text/calendar` (RFC 5545). One **VEVENT per item across all days** — every held booked session + every accepted/arranged meeting. `DTSTART = Session.StartUtc`, `DTEND = Session.EndUtc` (sessions), `SUMMARY = title`, `LOCATION = hall`. |

App fetches and hands to the native **share intent** / add-to-calendar.

## E3 — `GET /account/contact-card.vcf`  (Share → my data, QR-contact standard)
| | |
|---|---|
| Route | `GET /api/v1/account/contact-card.vcf` (or `Accept: text/vcard`) |
| Access | Approved account; own `sub` |
| Returns | `text/vcard` — `FN` (name EN/AR), `TITLE` (job title), `ORG` (organisation), `QrId` as the unique key. Same data the badge QR encodes. |

App hands to the native **share intent**. No badge-image endpoint — QR rendered client-side from `qrId`.

## Reused existing endpoints (no contract change)
The dashboard service composes server-side from these shipped, App-facing reads:
| Existing | Used for |
|---|---|
| `GET /account/profile` | display name, `avatarUrl`, roles |
| `GET /account/user-profile` | `ArabicName`, `EnglishName`, `QrId`, `ProfileTypeId`, job title, organisation |
| `GET /account/avatar/{userId}` | the avatar image stream |

## Build dependencies
- **B2B/B2C meeting source** (Page_014_Logic L-7) is not built — until it ships,
  `meetingsCount`/Meeting schedule items reflect **speaker meetings only**.
