# Speaker profile (ملف المتحدث) — mobile `/speakers/:speakerId`

| Field | Value |
|---|---|
| Route | `/speakers/:speakerId` (`RouteNames.speakerProfile`, page #20) · Guest+ (meeting request: Visitor) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/speakers/speaker_profile_screen.dart` (`SpeakerProfileScreen`, 243 lines — state + composition) |
| Widgets | `lib/features/speakers/widgets/` — `speaker_profile_header` · `speaker_avatar` · `speaker_cv` (`SpeakerCvSection`/`SpeakerCvTabs`/`SpeakerCvCard`) + `cv_tab` · `speaker_sessions` (`SpeakerSectionHeading`/`SpeakerSessionRow`) · `meeting_request_sheet` (`MeetingRequestSheet` — the speaker-side wrapper: it supplies the speaker options, the submit call and its own `_failureText`, then hands the rest to the shared form) · `meeting_request_form` (`MeetingRequestForm<T>` — the parameterised body **shared with the delegation sheet**) and its pieces `meeting_target_picker` (`MeetingTargetPicker<T>`) · `meeting_sheet_fields` (label / hint / spinner / search / subject) · `meeting_slot_section` · `meeting_send_button` · `meeting_slot_pickers` (`MeetingDayCard`/`MeetingTimeChip`) · `speaker_option_tile`. The form's send-time precondition chain is **not** in `widgets/` — it lives at the feature root as `lib/features/speakers/meeting_request_validation.dart` (`meetingRequestError`), a pure helper with no widget and no provider, which is where a feature-local helper belongs per §1 of the app's `CLAUDE.md` |
| Figma node | `908:2110` (meeting sheet 1776:4958, day/time pickers 1776:4975/5036) |
| Shell | `SimfPageShell` with a custom `SpeakerProfileHeader` (two-line name/rank + circled back) |
| API | `GET /app/speakers/{id}` · `GET …/meeting-requests/slots` · `POST …/meeting-requests` (D-269/D-474/D-475) · photo `GET /app/assets/SpeakerPhoto/{id}/image` |
| Providers | `speakersRepositoryProvider` · `authControllerProvider` · `simfDataConfigProvider` |
| Tests | `test/features/speakers/speaker_profile_screen_test.dart`; the sheet `test/features/speakers/meeting_request_sheet_test.dart` (incl. the three non-`ApiFailure` paths) + `meeting_request_sheet_exit_test.dart` (the Send button on a dismissing sheet) + `meeting_request_validation_test.dart` (the precondition chain, without a widget); golden `test/golden/speaker_profile_golden_test.dart` (`goldens/speaker_profile_908-2110.png`); E2E [`mobile-speaker-profile.md`](../../../tests/e2e/mobile-speaker-profile.md) |
| Legacy detail | `docs/App/Page_020/` — retained as the historical spec |
| Status | ✅ Real — D-303 → 908:2110 parity (P3/P4) → D-544 website chip → **clean-code frozen (D-606)** |

## 1. Purpose
The speaker's profile: header (name/rank/flag), gold-ringed photo avatar, the CV
tab pills over the bio card, the opt-in Request-meeting action + social links,
and the speaker's sessions.

## 2. Audience & access
Guest+ for the read; the Request-meeting sheet is **login-only** (a guest is sent
to sign-in) and slot-aware (D-474/D-475). The Request-meeting CTA itself is gated
on the **per-user** `allowsSpeakerMeeting` flag (D-760), which replaced the
VIP-tier gate of D-729 — the audience tier no longer grants it, so a VIP without
the flag does not see the CTA and a Normal-tier visitor with it does. It is driven
by `currentUserMeetingAccessProvider`, which (D-731) makes **no** network call for
a guest and caches the flags across speaker-profile opens (re-fetched only on an
auth transition, not per screen-open), so the browse surface never drains the
shared per-IP "auth" rate-limit bucket. Social links show only when the speaker
`allowsDataSharing`.

## 3. Button / action audit (Level F, 2026-07-04)
| Control | Handler | Backend |
|---|---|---|
| Back | `backOrHome` | — |
| CV tab | select section (setState) | — |
| Request meeting | login-gate → `MeetingRequestSheet` | `POST …/meeting-requests` |
| — day / time pick | select slot (setState) | slots from `GET …/slots` |
| — send | validate → submit | `POST …/meeting-requests` |
| Social chip | copy URL to clipboard | — |
| Session row | push `sessionDetail` #17 | — |
| Retry / pull-to-refresh | `_load()` | `GET /app/speakers/{id}` |

All data repo-backed; no missing API.

## 4. Clean-code freeze (D-606)
**1,098 → 272-line screen** + 6 widget files (all <400; the meeting-request form
and its day/time pickers split apart). Adopted the shared `SimfPullableHost`
(replaced a local `_PullToRefreshState` copy). Render-preserving: the
`speaker_profile_908-2110` golden **locked without `--update`** (parity held
from the June overlay pass); 23 module tests green.

## The request sheet's failure paths (2026-08-20)

The sheet is `MeetingRequestForm<T>`, shared with the delegation sheet in
`delegations/widgets/`, so everything here applies to both.

**What it used to do.** Both of the form's loaders — `_loadTargets` (the picker
roster) and `_loadSlots` (the chosen target's availability) — cleared their busy
flag on exactly two branches: success, and `on ApiFailure`. A keystore
`PlatformException` raised on the API client's 401-refresh path is **not** an
`ApiFailure`, so it escaped both and the busy flag stayed set:

- **Slots:** an endless spinner where the day cards belong, `canSend` false for
  good, and no way out — the Retry hangs off the load-error flag, which that
  path never set.
- **Targets:** worse, because `_loadTargets` is called only from `initState` and
  therefore had **no retry path at all**. The picker spun for the life of the
  sheet, and since the subject field, the slot section and the Send button are
  all gated on a chosen target, none of them ever rendered. The sheet was dead
  until the user closed and reopened it.

**What it does now.** Both loaders catch every failure. The slot loader lands on
the G3 load-error + **Retry** state ("تعذر تحميل القائمة."); the target loader
resolves the picker to its own empty hint (اختر المتحدث / the delegation
equivalent) instead of spinning. This is deliberately not the same landing: the
picker has no Retry to offer, so resolving to a hint is what makes the sheet
usable rather than frozen, and reopening it is the retry.

**G3 is preserved, and is why the catch widened rather than the state changing.**
A **failed** fetch must never present as the target having no availability
(owner, 2026-07-30) — an empty slot list disables Send, so a swallowed error
would tell the user the speaker has no availability and leave them stuck. Every
failure type now reaches the load-error state, so none of them can reach the
no-availability notice.

**Sending.** Two matching fixes to `_submit`:

- A non-`ApiFailure` now sets the inline `meetingRequestFailed` message instead
  of letting the Send button quietly come back with nothing said (and the error
  escape to the zone). An `ApiFailure` still maps through each sheet's own
  `failureText`, which is what keeps the delegation sheet showing the **server's**
  bilingual reason and never the speaker copy (A35, `E2E-DELREQ-011/012`).
- A **successful** send no longer re-enables the button. `mounted` does not mean
  "still on screen": `pop()` only reverses the route animation, and the `State`
  lives out the ~200ms exit, so re-enabling flicked the Send button back to life
  on a sheet already sliding away. The button is re-enabled only for a sheet that
  is staying.

The precondition chain that runs before any of this moved out of the `State` into
the pure `meetingRequestError` helper (feature root). Same order as before —
target, subject, the sheet's extra field, then the slot — and the extra
validator is still passed as a callback so the delegation sheet's عدد الحضور
field is not validated until the checks ahead of it pass.

## Logo / photo boxes (owner 2026-07-26)

Every logo / photo box on this page renders through the shared
[`SimfLogoImage`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_logo_image.dart):
a brand mark FITS its box (`BoxFit.contain`, replacing the crop-happy
`BoxFit.cover`), a portrait still fills its frame (`BoxFit.cover`), and — where
the box is not inside a tappable row — pressing it opens the picture full size
in [`SimfImageViewer`](../../../../src/Mobile/simf_app/lib/app/widgets/simf_image_viewer.dart)
(pinch-zoom, named for a screen reader, close / back to dismiss). The rules and
their scenarios live once in [`e2e/mobile-logo-viewer.md`](../../../tests/e2e/mobile-logo-viewer.md)
(E2E-LOGO-001..008).

## Changelog
- **2026-08-20 (app deep-clean audit):** the shared request sheet could die
  silently — see [The request sheet's failure paths](#the-request-sheets-failure-paths-2026-08-20)
  above for the full account. Both loaders now catch every failure rather than
  `ApiFailure` alone, so a keystore `PlatformException` on the 401-refresh no
  longer leaves the slot list or the speaker picker spinning forever (the
  picker, loaded once from `initState`, had no retry path at all and took the
  subject / slot / Send section down with it). `_submit` surfaces a message on a
  non-`ApiFailure` and stops re-enabling Send on a sheet that is already
  dismissing. The send-time precondition chain moved to the pure
  `meeting_request_validation.dart` helper at the feature root — same checks,
  same order, now testable without pumping a widget. Behaviour on the success
  path, the G3 slot rule and the A35 server-reason rule are all unchanged; no
  wire, schema or copy change. The screen itself was not touched.
- **2026-08-18 (delivery clean-code programme, structure only):** the speaker
  meeting-request sheet and the delegation one were **79% identical** — same
  target picker, same subject field, same slot section, same send button, same
  submit/failure choreography, differing only in what is being requested. They are
  now one parameterised `MeetingRequestForm<T>` (in `speakers/widgets/`), and each
  sheet keeps its own thin wrapper: `MeetingRequestSheet` here (132 lines) and
  `DelegationMeetingRequestSheet` in `delegations/widgets/` (146 lines), each still
  owning its own options, its own submit and its own `_failureText` — so the A35
  rule that a delegation rejection shows the SERVER's reason and never the speaker
  copy is unchanged and still covered by its own scenarios
  (`E2E-DELREQ-011/012`). The screen itself went 272 → **243** lines. Both sheets'
  widget tests and the `speaker_profile_908-2110` golden passed unchanged.
