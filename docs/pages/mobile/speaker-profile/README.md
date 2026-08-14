# Speaker profile (ملف المتحدث) — mobile `/speakers/:speakerId`

| Field | Value |
|---|---|
| Route | `/speakers/:speakerId` (`RouteNames.speakerProfile`, page #20) · Guest+ (meeting request: Visitor) |
| Surface | Mobile (Flutter) |
| Screen | `lib/features/speakers/speaker_profile_screen.dart` (`SpeakerProfileScreen`, 272 lines — state + composition) |
| Widgets | `lib/features/speakers/widgets/` — `speaker_profile_header` · `speaker_avatar` · `speaker_cv` (`SpeakerCvSection`/`SpeakerCvTabs`/`SpeakerCvCard`) · `speaker_sessions` (`SpeakerSectionHeading`/`SpeakerSessionRow`) · `meeting_request_sheet` · `meeting_slot_pickers` (`MeetingDayCard`/`MeetingTimeChip`) |
| Figma node | `908:2110` (meeting sheet 1776:4958, day/time pickers 1776:4975/5036) |
| Shell | `SimfPageShell` with a custom `SpeakerProfileHeader` (two-line name/rank + circled back) |
| API | `GET /app/speakers/{id}` · `GET …/meeting-requests/slots` · `POST …/meeting-requests` (D-269/D-474/D-475) · photo `GET /app/assets/SpeakerPhoto/{id}/image` |
| Providers | `speakersRepositoryProvider` · `authControllerProvider` · `simfDataConfigProvider` |
| Tests | `test/features/speakers/speaker_profile_screen_test.dart`; golden `test/golden/speaker_profile_golden_test.dart` (`goldens/speaker_profile_908-2110.png`); E2E [`mobile-speaker-profile.md`](../../../tests/e2e/mobile-speaker-profile.md) |
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
