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
| Mockup page | **26** (`Mockup.html`) — the live-mode **question composer**; a standalone auth-gated route in the app (not an in-frame tab) |
| Route | `RouteNames.sendQuestion` (the live-mode question composer, reached over the session/live screen) — **auth-gated** |
| Titles | AR **إرسال سؤال** · EN **Send a question** |
| Section | D — Conference-day live mode |
| Nature | **Live Q&A composer** — pick a recipient (Speaker / Host), write a question, submit it; questions are reviewed before going on air |
| App privilege | **Visitor (approved) — login-only.** The submit endpoint requires an approved account (`RequireApprovedAccount`); a guest / pending account cannot submit. |
| Status | API **BUILT** — `POST /api/v1/app/sessions/{id}/questions` (D-169, +D-174 recipient, +D-212 committee, +D-236 advisory AI, +D-242 arrival gate); **Flutter screen BUILT** (recipient SegmentedButton + multiline question + submit; not-open / generic error toasts) |

## Flutter screen (as built)
The app screen takes an optional `sessionId` from the query string
(`/live/question?sessionId={id}`). With no id it shows an "open from a live
session" empty state; with an id it shows the form — a **SegmentedButton**
recipient choice (Speaker / Host → wire int 0 / 1), a multiline question field
(`maxLength` 500), and a Submit. An empty question shows an inline prompt (no
call). On success → a "question sent" toast + the field clears. A 400
(`SESSION_NOT_LIVE_FOR_QUESTIONS`) / 404 → the "questions are only open around the
session" toast; any other failure → a generic error toast. `isAtVenue` is sent
`true` (D-171 self-assert; the server is authoritative when the hall has a
geofence, D-242). UI is interim — final visuals from SIMF-VID-001.

- Screen: `src/Mobile/simf_app/lib/features/questions/send_question_screen.dart`
  (+ `data/questions_repository.dart`).
- Widget tests: `src/Mobile/simf_app/test/features/questions/send_question_screen_test.dart`
  (no-id empty state, empty-question inline prompt, submit success + clear,
  400 not-open toast, generic error toast).
- E2E: [`docs/tests/e2e/mobile-send-question.md`](../../tests/e2e/mobile-send-question.md).

## Sources of truth (read first)
`Mockup.html` screen 26 (the visual) · `SIMF_Screen_Guide_and_User_Journey`
SCREEN26 (the narrative — "Composer for sending a question to the speaker or host
during the live broadcast … questions are reviewed before going on air") ·
SIMF-MOB-API-001 (shared API conventions + auth) · `DECISIONS_LOG` **D-169**
(public submission + moderator surface), **D-171** (the `IsAtVenue` self-assert),
**D-174** (the Speaker / Host recipient pills), **D-212** (the Scientific-Committee
central queue — every question lands `Pending`), **D-236** (the advisory AI filter
on submit), **D-242** (the arrival-at-the-hall gate) and **D-271** (this wave —
the question open/close window).

## Headline
> Screen 26 is the live-mode **question composer**, layered over the live video
> frame in the mockup and shipped as a standalone auth-gated route in the app. The
> user picks a **recipient** — **المتحدث (Speaker)** or **المضيف (Host)** — writes a question
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
- **Request interview (27) — REMOVED (D-278).** The session-scoped "request
  interview" flow (`طلب مقابلة`) was **permanently removed** — entity, endpoints,
  CP desk and the Flutter route 27 all deleted. It is no longer a live-mode tab.
- **Audience comments (28) — a standalone screen, BUILT (D-319).** The
  audience-comments feed is its own auth-gated route (`/live/comments`,
  [Page_028](../Page_028/README.md)), reusing the already-built per-session comment
  + like endpoints (D-223). It is **not** folded into this composer. _(Earlier
  Page-026 text said screen 28 was "removed per D-271"; that was wrong on both
  counts — D-271 never decided it and the screen later shipped at D-319.)_ See
  [Page_026_Logic.md](Page_026_Logic.md) L-6.
- **Sessions (16)** — the `الأجندة / Agenda` screen renamed to `الجلسات / Sessions`
  (D-271, title + bottom-nav + filter pills); no API change.
- **CP moderation desks** — the Scientific-Committee queue and the per-session
  moderator desk (`…/questions/moderate`) where a question is approved + pushed
  live; audience comments are approved/hidden via `/admin/comments-moderation`.
