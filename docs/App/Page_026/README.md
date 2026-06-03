# Page 026 — إرسال سؤال · Send a question (within live)

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_026_Function.md](Page_026_Function.md) | What the user does — pick a recipient (المتحدث / المضيف), write a question, submit it during the live window |
| Logic | [Page_026_Logic.md](Page_026_Logic.md) | The submit pipeline (committee + advisory AI filter), the THREE open/close rules, the moderation note |
| API | [Page_026_API.md](Page_026_API.md) | The backend endpoint + DTOs this page writes (authoritative contract) — **built** |
| Design | [Page_026_Design.md](Page_026_Design.md) | Flutter screen design — live frame, 3 live-mode tabs, recipient picker, text area, brass submit, RTL, states |

## Identity
| | |
|---|---|
| Mockup page | **26** (`Mockup.html`, live-mode tab group 26 / 27 / 28) |
| Route | `RouteNames.sendQuestion` (one of the three live-mode tabs on the session/live screen) — **auth-gated** |
| Titles | AR **إرسال سؤال** · EN **Send a question** |
| Section | D — Conference-day live mode |
| Nature | **Live Q&A composer** — pick a recipient (Speaker / Host), write a question, submit it; questions are reviewed before going on air |
| App privilege | **Visitor (approved) — login-only.** The submit endpoint requires an approved account (`RequireApprovedAccount`); a guest / pending account cannot submit. |
| Status | API **BUILT** — `POST /api/v1/app/sessions/{id}/questions` (D-169, +D-174 recipient, +D-212 committee, +D-236 advisory AI, +D-242 arrival gate); Flutter screen is a mockup |

## Sources of truth (read first)
`Mockup.html` screen 26 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN26 (the narrative — "Composer for sending a question to the speaker or host
during the live broadcast … questions are reviewed before going on air") ·
SIMF-MOB-API-001 (shared API conventions + auth) · `DECISIONS_LOG` **D-169**
(public submission + moderator surface), **D-171** (the `IsAtVenue` self-assert),
**D-174** (the Speaker / Host recipient pills), **D-212** (the Scientific-Committee
central queue — every question lands `Pending`), **D-236** (the advisory AI filter
on submit), **D-242** (the arrival-at-the-hall gate) and **D-271** (this wave —
the open/close window + the screen-28 comments removal).

## Headline
> Screen 26 is one of three **live-mode tabs** (26 إرسال سؤال / 27 طلب مقابلة /
> 28 تعليقات الجمهور) layered over the live video frame. The user picks a
> **recipient** — **المتحدث (Speaker)** or **المضيف (Host)** — writes a question
> and submits it. The question is **reviewed before going on air**: it lands
> `Pending`, is screened by an **advisory AI filter** (does not block), then is
> approved by the **Scientific Committee** before a session moderator can push it
> live.

Submitting is gated by **three** rules: (1) **arrival at the hall** (a geofenced
hall needs a `HallAttendance` arrival record, else the `IsAtVenue` self-assert),
(2) the window **opens 5 minutes before** `StartUtc`, and (3) it **closes at**
`EndUtc`. Outside that window the submit returns `400 SESSION_NOT_LIVE_FOR_QUESTIONS`.
See [Page_026_Logic.md](Page_026_Logic.md) and [Page_026_API.md](Page_026_API.md).

## Related pages
- **Recorded / live session screen (25)** — the live video frame this composer is
  layered over; the **live stream** + sign-language toggle come from the session
  detail's `LiveStreamUrl` / `LiveSignLanguageUrl` (D-271 live stub).
- **Request interview (27)** — `طلب مقابلة`, the second live-mode tab (press/media
  branch).
- **Audience comments (28) — REMOVED.** The standalone audience-comments screen is
  removed per the updated mockup (D-271); comments still exist (2-stage AI-filter +
  admin-approve, already built) but surface **inside** the session/live screen, not
  as a separate screen. See [Page_026_Logic.md](Page_026_Logic.md) L-6.
- **Sessions (16)** — the `الأجندة / Agenda` screen renamed to `الجلسات / Sessions`
  (D-271, title + bottom-nav + filter pills); no API change.
- **CP moderation desks** — the Scientific-Committee queue and the per-session
  moderator desk (`…/questions/moderate`) where a question is approved + pushed
  live; audience comments are approved/hidden via `/admin/comments-moderation`.
