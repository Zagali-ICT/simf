# Page 020 — ملف متحدث · تفاصيل المتحدث · Speaker profile

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_020_Function.md](Page_020_Function.md) | What the user does — read the CV (4 tabs), see the speaker's sessions, (login) request a meeting |
| Logic | [Page_020_Logic.md](Page_020_Logic.md) | The 4 CV tabs → bio/qualifications/trainingExperience/awards, the social-URL + meeting-button gates, the login-only meeting flow |
| API | [Page_020_API.md](Page_020_API.md) | The backend endpoints + DTOs this page reads/writes (authoritative contract) — profile read **built (D-199)**, meeting request **built new (D-269)** |
| Design | [Page_020_Design.md](Page_020_Design.md) | Flutter screen design — hero, large avatar, 4 tabs, bio card, social links, sessions, meeting action, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **20** (`Mockup.html`, line ~1355) |
| Route | `RouteNames.speakerProfile` → `/speakers/:speakerId` (guest reads; the meeting action is **login-only**) |
| Titles | AR **ملف متحدث** (تفاصيل المتحدث) · EN **Speaker profile** |
| Section | 2 — Core screens |
| Nature | **Speaker CV detail** — rank/name hero + large avatar + 4 profile tabs (the "CV") + the speaker's sessions; a Visitor can request a meeting |
| App privilege | **Reads: Guest+ (anonymous)** — the profile read is `AllowAnonymous` (D-199). **Request-meeting action: Visitor login-only** — `RequireApprovedAccount` (D-269); a guest is prompted to sign in. |
| Status | Reads API **BUILT** (D-199); meeting request **BUILT — NEW** (D-269) |

## Sources of truth (read first)
`Mockup.html` screen 20 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN20 (the narrative) · SIMF-MOB-API-001 (shared API conventions + auth) ·
`DECISIONS_LOG` **D-199** (Speakers list + profile reads are anonymous) +
**D-269** (this page's owner addition — the dedicated, login-only speaker
meeting request + the CP review desk).

## Headline (owner directive, D-269)
> The Speakers list (19) and this Speaker profile (20) are **read anonymously**
> (the home tile shows speakers **UNLOCKED**). The owner's D-269 addition is a
> **"طلب مقابلة" (request a meeting)** affordance shown **only when the speaker
> allows it** — and **only login** applies to that action: a Visitor must be
> signed in and approved to submit, the result is a **Pending** request, and an
> **admin reviews it** in the Control Panel desk (`/admin/speaker-meeting-requests`).

The whole profile + the four CV tabs + the speaker's sessions + the
`allowsMeetingRequests` / `allowsDataSharing` flags come from **one** anonymous
call (`GET /app/speakers/{id}` → `PublicSpeakerDetail`); the meeting request is a
**separate, new** login-only `POST …/meeting-requests` against a dedicated
`SpeakerMeetingRequest` entity. See [Page_020_Logic.md](Page_020_Logic.md) and
[Page_020_API.md](Page_020_API.md).

## Related pages
- **Speakers list (19)** — `RouteNames.speakers` → `/speakers` (guest) — the grid
  of speaker cards that links into this profile via the `المزيد` / *More* link.
- **CP admin desk** — `/admin/speaker-meeting-requests` (permissions
  `SpeakerMeetingRequests.View` / `Manage`) — where an admin lists, opens and
  responds (Accepted / Rejected) to the requests this page submits.
- **E2E catalogue** — [`docs/tests/e2e/mobile-speaker-profile.md`](../../tests/e2e/mobile-speaker-profile.md).
