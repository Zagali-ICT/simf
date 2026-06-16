# E2E test catalogue — `Speaker profile` (`speaker-profile`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> profile **read** is already built and **anonymous** (guest+, D-199); the
> **Request-meeting** action is the owner's D-269 addition and is **login-only**
> (an approved Visitor). The dedicated `SpeakerMeetingRequest` entity is separate
> from the session-scoped MeetingRequest (mockup screen 27). API implementation
> lives in `tests/SIMF.Api.Tests/PublicSpeakersTests.cs` (reads) and
> `tests/SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs` (meeting request).
> The **Flutter screen is built (D-303)** — widget tests in
> `src/Mobile/simf_app/test/features/speakers/speaker_profile_screen_test.dart`
> (profile+CV+sessions, guest→sign-in, signed-in submit→toast, button hidden when
> opted out, 404). Interim UI: avatar = initials, CV as stacked sections (not
> tabs), social links copy-to-clipboard (URL-launch deferred to SIMF-VID-001).

| | |
|--|--|
| **Page** | [`Page_020`](../../App/Page_020/README.md) (App page docs) |
| **Route** | `GET /api/v1/app/speakers/{id}` (profile, **anonymous**) · `POST /api/v1/app/speakers/{id}/meeting-requests` (**login-only**, approved visitor) · app screen #20 `RouteNames.speakerProfile` → `/speakers/:speakerId` (guest reads; meeting action requires login) |
| **Surface** | Mobile (Flutter) + App API |
| **Test runner** | xUnit + `WebApplicationFactory` (API) · Flutter widget/integration test (screen) |
| **Auth setup** | **Anonymous** for the profile read; an **approved Visitor** token for the meeting request; an **Admin** token only to seed the speakers (and set `allowsMeetingRequests` / `allowsDataSharing`). **No literal secrets** (admin TOTP via the `Get-Totp` helper). |
| **Last reviewed** | 2026-06-05 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB020-001 | Anonymous profile renders name/rank + the 4 CV tabs + sessions | happy | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB020-002 | Social links shown only when `allowsDataSharing` | edge | P1 | authored ✓ (screen) |
| E2E-MOB020-003 | Unknown / soft-deleted speaker → 404 `SPEAKER_NOT_FOUND` | edge | P0 | authored ✓ (`PublicSpeakersTests`) |
| E2E-MOB020-004 | Request-meeting button hidden when `allowsMeetingRequests` is false | edge | P0 | authored ✓ (screen) |
| E2E-MOB020-005 | Approved visitor submits to a speaker that allows meetings → 200 Pending | happy | P0 | authored ✓ (`Submit_to_a_speaker_that_allows_meetings_returns_pending`) |
| E2E-MOB020-006 | Speaker does not accept meetings → 409 `SPEAKER_MEETING_REQUESTS_NOT_ALLOWED` | edge | P0 | authored ✓ (`Submit_to_a_speaker_that_does_not_accept_meetings_is_409`) |
| E2E-MOB020-007 | Guest / unauthenticated submit → 401 | auth | P0 | authored ✓ (`Submit_requires_login`) |
| E2E-MOB020-008 | Empty subject → 400 `SPEAKER_MEETING_REQUEST_INVALID` | validation | P0 | authored ✓ (`Submit_with_empty_subject_is_invalid`) |
| E2E-MOB020-009 | Tap a speaker session → Session detail (17) | happy | P1 | authored ✓ (screen) |
| E2E-MOB020-010 | RTL render; hero, back chevron and tabs right-to-left | i18n | P1 | authored ✓ (screen) |
| E2E-MOB020-011 | 125px gold-ringed avatar renders the speaker initials | happy | P1 | _to author_ |
| E2E-MOB020-012 | Tapping a CV tab pill swaps the navy bio card content | happy | P0 | _to author_ |
| E2E-MOB020-013 | Active tab pill is gold-filled; the rest are navy with a beige hairline | happy | P1 | _to author_ |
| E2E-MOB020-014 | Only CV sections with content render a pill (1–4 pills) | edge | P1 | _to author_ |
| E2E-MOB020-015 | Speaker with no CV content shows no tabs and no bio card | edge | P1 | _to author_ |

## Scenarios

### E2E-MOB020-001 — Anonymous profile renders the CV + sessions

```gherkin
Feature: Speaker profile (read)
  As any visitor (guest or signed-in)
  I want to read a speaker's profile, CV and sessions
  So that I can learn about them before the forum

Scenario: The profile returns the speaker, the four CV tabs and the sessions
  Given a seeded, active speaker with bio, qualifications, training and awards
  And the speaker is linked to one published session
  When an anonymous client calls GET /api/v1/app/speakers/{id} with no token
  Then the response is 200
  And name, nameArabic and rank are returned
  And bio/bioArabic, qualifications/qualificationsArabic, trainingExperience/trainingExperienceArabic and awards/awardsArabic are returned
  And the four tabs نبذة عنه / المؤهلات العلمية / الخبرات التدريبية / الجوائز render from those fields
  And sessions[] contains the session (id, code, title, hallName, startUtc, endUtc)
```

**Evidence:** `PublicSpeakersTests` (green).

### E2E-MOB020-002 — Social links gated by data-sharing

```gherkin
Scenario: Social links appear only when the speaker allows data sharing
  Given a speaker with facebookUrl, linkedInUrl and xUrl set
  When the profile is fetched and allowsDataSharing is true
  Then the Facebook, LinkedIn and X links are shown
  And when allowsDataSharing is false the social links are hidden
```

### E2E-MOB020-003 — Unknown / soft-deleted speaker

```gherkin
Scenario: An unknown or soft-deleted speaker id is not found
  Given a speaker id that does not exist or has been soft-deleted
  When an anonymous client calls GET /api/v1/app/speakers/{id}
  Then the response is 404
  And the error code is SPEAKER_NOT_FOUND
```

**Evidence:** `PublicSpeakersTests` (green).

### E2E-MOB020-004 — Request-meeting button hidden

```gherkin
Scenario: The Request-meeting affordance is hidden when meetings are not allowed
  Given a speaker whose allowsMeetingRequests is false
  When the profile renders
  Then the "طلب مقابلة" (Request meeting) button is not shown
  And when allowsMeetingRequests is true the button is shown (login-gated)
```

### E2E-MOB020-005 — Approved visitor submits a meeting request

```gherkin
Scenario: A signed-in approved visitor requests a meeting with an allowing speaker
  Given a speaker whose allowsMeetingRequests is true
  And an approved Visitor token
  When the visitor calls POST /api/v1/app/speakers/{speakerId}/meeting-requests
  And the body has requesterName "Faisal" and subject "Discuss naval logistics"
  Then the response is 200
  And the result has speakerId, status "Pending" and createdAt
  And the request is created Pending for an admin to review on the CP desk
```

**Evidence:** `SpeakerMeetingRequestsTests.Submit_to_a_speaker_that_allows_meetings_returns_pending` (green).

### E2E-MOB020-006 — Speaker does not accept meetings

```gherkin
Scenario: Submitting to a speaker that does not accept meetings is rejected
  Given an active speaker whose allowsMeetingRequests is false
  And an approved Visitor token
  When the visitor calls POST /api/v1/app/speakers/{speakerId}/meeting-requests
  Then the response is 409
  And the error code is SPEAKER_MEETING_REQUESTS_NOT_ALLOWED
```

**Evidence:** `SpeakerMeetingRequestsTests.Submit_to_a_speaker_that_does_not_accept_meetings_is_409` (green).

### E2E-MOB020-007 — Login required

```gherkin
Scenario: A guest cannot submit a meeting request
  Given a speaker whose allowsMeetingRequests is true
  When an anonymous client calls POST /api/v1/app/speakers/{speakerId}/meeting-requests with no token
  Then the response is 401
  And the screen routes the user to sign in before the action
```

**Evidence:** `SpeakerMeetingRequestsTests.Submit_requires_login` (green).

### E2E-MOB020-008 — Empty subject is invalid

```gherkin
Scenario: A meeting request with an empty subject is rejected
  Given a speaker whose allowsMeetingRequests is true
  And an approved Visitor token
  When the visitor calls POST /api/v1/app/speakers/{speakerId}/meeting-requests with an empty subject
  Then the response is 400
  And the error code is SPEAKER_MEETING_REQUEST_INVALID
```

**Evidence:** `SpeakerMeetingRequestsTests.Submit_with_empty_subject_is_invalid` (green).

### E2E-MOB020-009 — Navigate to a session

```gherkin
Scenario: Tapping a speaker's session opens its detail page
  Given the profile shows a session in sessions[]
  When the user taps that session
  Then the Session detail (17) opens for that session id
```

### E2E-MOB020-010 — RTL render

```gherkin
Scenario: The speaker profile renders right-to-left in Arabic
  Given the device locale is Arabic
  When the profile renders
  Then the hero (back chevron, rank, name) and the avatar are right-to-left
  And the four tabs نبذة عنه / المؤهلات العلمية / الخبرات التدريبية / الجوائز render right-to-left
  And the active-tab CV content renders right-to-left
```

### E2E-MOB020-011 — Gold-ringed avatar

```gherkin
Scenario: The 125px white circle ringed gold renders the speaker initials
  Given a seeded active speaker named "Faisal Al-Otaibi" (Arabic "فيصل العتيبي")
  When the profile renders on the navy KSA shell (frame 908:2110)
  Then a 125x125 white circle with a 2.77px gold ring is centred above the CV
  And it shows the speaker's navy initials (Latin "FA" / Arabic "فا" per locale)
  And there is no broken-image placeholder (the photo asset pass is SIMF-VID-001)
```

### E2E-MOB020-012 — CV tab pill swaps the bio card

```gherkin
Scenario: Tapping a CV tab pill swaps the navy bio card content in place
  Given a speaker with bio, qualifications, training and awards all populated
  And the four pills نبذة عنه / المؤهلات العلمية / الخبرات التدريبية / الجوائز render
  And the active pill is نبذة عنه showing the bio body in the navy #192B41 card
  When the user taps المؤهلات العلمية
  Then the navy card now shows the qualifications body (right-aligned white text)
  And the bio body is no longer shown
  And no new screen is pushed (the card swaps inline)
  When the user then taps الجوائز
  Then the navy card shows the awards body
```

### E2E-MOB020-013 — Active vs inactive pill styling

```gherkin
Scenario: The active pill is gold-filled white-text; the rest are navy beige-text
  Given the four CV tab pills render in one full-width row
  When نبذة عنه is the active tab
  Then the نبذة عنه pill is filled gold (accent) with white text
  And the other three pills are navy #192B41 with a beige hairline and beige text
  When the user taps الخبرات التدريبية
  Then الخبرات التدريبية becomes the gold-filled white-text pill
  And نبذة عنه returns to the navy beige-text style
```

### E2E-MOB020-014 — Only populated CV sections get a pill

```gherkin
Scenario: A pill renders only for a CV section that carries content
  Given a speaker with bio and awards populated but no qualifications and no training
  When the profile renders
  Then exactly two pills render: نبذة عنه and الجوائز
  And المؤهلات العلمية and الخبرات التدريبية pills are not shown
  And the first populated section (نبذة عنه) is the active tab on first render
```

### E2E-MOB020-015 — Speaker with no CV content

```gherkin
Scenario: A speaker with no CV text shows neither the tab strip nor the bio card
  Given a speaker whose bio, qualifications, training and awards are all empty
  When the profile renders
  Then no CV tab pills are shown
  And no navy bio card is shown
  And the avatar, the request-meeting affordance and the sessions list still render per their own rules
```

---

> **Figma parity (2026-06-16):** the screen was re-skinned to the KSA-Project
> frame **908:2110 "About Speaker"** — the two-line header (white name over the
> beige rank + circled back chevron), the 125px gold-ringed avatar (`912:2270`),
> the inline CV tab-pill row (`912:2312`, active pill gold) that swaps the navy
> `#192B41` bio card (`912:2331`). Request-meeting, social links and the sessions
> list keep their prior behaviour below the frame's minimal content.
>
> **P3 per-element polish (2026-06-16):** the CV tab-pill inter-gap was set to
> 18px so the equal pills resolve to the frame's 72px width (4×72 + 3×18 = 343),
> and the tab-strip→bio-card gap to 24px (frame y 353→377). The CV-tab RTL order
> (نبذة عنه / Bio first → right-most, الجوائز / Awards → left-most) is now locked
> by a deterministic `Locale('ar')` position test (`CV tabs lay out Bio (first)
> right-most in Arabic`).

---

_Last reviewed:_ `2026-06-16` by `SIMF Team`.
