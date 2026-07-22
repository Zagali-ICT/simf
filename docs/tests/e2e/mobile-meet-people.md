# E2E test catalogue - `Meet people` (`meet-people`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue -
> `GET /api/v1/app/networking/partner-directory` (`RequireApprovedAccount`).
>
> **Build #13 rework (2026-07-22):** this screen is no longer the AI "% match"
> recommender - it is the curated + opt-in **partner directory**. These scenarios
> **supersede** the old recommender scenarios (smart-suggestions header + per-match
> "% تطابق" cards + backend match reason); the retired behaviour is not tested here
> anymore. The directory is the deduped union of curated **Speakers**, **Sponsors**
> and **Booth companies** plus opted-in **"Other"-type members** - **Normal / VIP
> visitors never appear**. Rows are the shared `SimfIdentityCell`; tap routes per
> kind (speaker → speaker profile, sponsor → sponsor detail, booth → exhibitor
> detail; a `person` row is non-tappable). The whole feature is gated by the CP
> switch `OrganizationProfile.PartnerDirectoryEnabled` (off → empty list + the Home
> "Meet People" tile hidden), edited on the CP Site-Settings page
> ([`cp-site-settings.md`](cp-site-settings.md)). Backend tested in
> `tests/SIMF.Api.Tests/PartnerDirectoryServiceTests.cs`; the screen + models in
> `src/Mobile/simf_app/test/features/meet/meet_people_screen_test.dart` +
> `partner_directory_models_test.dart`.

| | |
|--|--|
| **Page** | [`meet-people`](../../pages/mobile/meet-people/README.md) |
| **Route** | `GET /api/v1/app/networking/partner-directory` · app screen #35 `/meet` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Approved account** - `RequireApprovedAccount`. Sign in with `Get-Totp` (never a literal secret); route 35 is auth-gated. |
| **Last reviewed** | 2026-07-22 (Build #13 - partner directory) |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB035-001 | The directory lists sponsors + speakers + booth companies + opted-in members as `SimfIdentityCell` rows | happy | P0 | authored ✓ (screen + `PartnerDirectoryServiceTests`) |
| E2E-MOB035-002 | Normal / VIP visitors never appear (only curated entities + opted-in non-visitor accounts) | filter | P0 | authored ✓ (`PartnerDirectoryServiceTests`) |
| E2E-MOB035-003 | De-dup - a person who is also a curated Speaker appears once, as the speaker | edge | P1 | authored ✓ (`PartnerDirectoryServiceTests`) |
| E2E-MOB035-004 | Tap a speaker row → speaker profile | happy | P0 | authored ✓ (screen) |
| E2E-MOB035-005 | Tap a sponsor row → sponsor detail | happy | P1 | authored ✓ (screen) |
| E2E-MOB035-006 | Tap a booth row → exhibitor detail (routed by booth id) | happy | P1 | authored ✓ (screen) |
| E2E-MOB035-007 | An opted-in person row shows their data and is non-tappable | edge | P1 | authored ✓ (screen) |
| E2E-MOB035-008 | CP flag off → the endpoint returns empty and the Home "Meet People" tile is hidden | config | P0 | authored ✓ (`PartnerDirectoryServiceTests` off→empty) |
| E2E-MOB035-009 | Empty directory → "No one to show yet" empty state (pull-to-refresh preserved) | edge | P1 | authored ✓ (screen) |
| E2E-MOB035-010 | Load failure → "Could not load the directory." error state + retry | resilience | P1 | authored ✓ (screen) |
| E2E-MOB035-011 | The My-interests opt-in checkbox is shown only to "Other"-type members and persists `ShowInMeetLikeYou` | happy | P0 | authored ✓ (`sign_up_interests_screen` edit-mode) |
| E2E-MOB035-012 | RTL render (Arabic) - rows and country tags mirror correctly | i18n | P1 | spec |
| E2E-MOB035-013 | De-dup - a company that is both a Sponsor and a booth exhibitor appears once, as the sponsor | edge | P1 | authored ✓ (`PartnerDirectoryServiceTests`) |

## Scenarios

### E2E-MOB035-001 - Directory lists the four kinds

```gherkin
Feature: Meet people like you (partner directory)
  As an approved member
  I want a directory of sponsors, speakers, exhibition companies and opted-in members
  So that I can find people and organisations to meet at the forum

Scenario: The directory renders the deduped union
  Given the partner directory is enabled in the CP
  And there is 1 active speaker, 1 active sponsor, 1 active booth company and 1 opted-in Other-type member
  When the /meet screen loads GET /app/networking/partner-directory
  Then a SimfIdentityCell row is shown for each of the four entries
  And each row shows the localised name, subtitle (rank / tagline / sector / job title) and its logo / country tag
```

### E2E-MOB035-002 - Normal / VIP visitors never appear

```gherkin
Scenario: Audience visitors are excluded
  Given an Approved Normal visitor and an Approved VIP visitor exist
  And neither is a curated speaker/sponsor/booth company
  When GET /app/networking/partner-directory is called
  Then neither visitor appears in entries
  # person rows require ProfileType.IsForVisitor == false AND ShowInMeetLikeYou == true AND non-Admin/Approved
```

### E2E-MOB035-003 - De-dup a curated speaker

```gherkin
Scenario: A person who is also a curated speaker appears once
  Given an Other-type member opted in (ShowInMeetLikeYou = true)
  And a curated Speaker is linked to that member's UserProfileId
  When GET /app/networking/partner-directory is called
  Then the member appears exactly once, as a speaker entry (kind = "speaker")
  And there is no duplicate person entry for the same profile
```

### E2E-MOB035-013 - De-dup a sponsor that is also a booth company

```gherkin
Scenario: A company that is both a sponsor and a booth exhibitor appears once
  Given an active Sponsor and an active booth Exhibitor are the same company
  And they either share one Contact directory record or carry the same company name
  When GET /app/networking/partner-directory is called
  Then the company appears exactly once, as a sponsor entry (kind = "sponsor")
  And there is no duplicate booth entry for the same company
  # dedup key: shared Contact id (robust) else case-insensitive trimmed name; the sponsor wins
```

### E2E-MOB035-004 / 005 / 006 - Per-kind tap navigation

```gherkin
Scenario: Tapping a speaker opens the speaker profile
  Given the directory shows a speaker entry
  When the user taps its row
  Then the app pushes RouteNames.speakerProfile with the speaker id

Scenario: Tapping a sponsor opens the sponsor detail
  Given the directory shows a sponsor entry
  When the user taps its row
  Then the app pushes RouteNames.sponsorDetail with the sponsor id

Scenario: Tapping a booth company opens the exhibitor detail
  Given the directory shows a booth entry (id = booth id, name = exhibitor company name)
  When the user taps its row
  Then the app pushes RouteNames.exhibitorDetail with the booth id
```

### E2E-MOB035-007 - Opted-in person row is non-tappable

```gherkin
Scenario: A person row shows data but does not navigate
  Given the directory shows a person entry (kind = "person")
  Then the row shows the member's name and job title
  And tapping the row does nothing (onTap is null - no detail screen for a person)
```

### E2E-MOB035-008 - CP flag off hides the directory

```gherkin
Scenario: Disabling the directory in the CP empties it and hides the Home tile
  Given an admin sets PartnerDirectoryEnabled = false on /admin/site-settings
  When the app reads GET /app/site-settings (partnerDirectoryEnabled = false)
  Then the Home "Meet People" tile is hidden
  And GET /app/networking/partner-directory returns an empty entries list
```

### E2E-MOB035-009 / 010 - Empty and error states

```gherkin
Scenario: An empty directory shows the empty notice
  Given the directory endpoint returns no entries
  Then the "No one to show yet" empty state is shown ("لا يوجد أشخاص لعرضهم بعد")
  And pull-to-refresh still works

Scenario: A load failure shows the error state
  Given the directory endpoint fails
  Then "Could not load the directory." is shown ("تعذّر تحميل الدليل.")
  And a retry re-fetches the directory
```

### E2E-MOB035-011 - My-interests opt-in (Other-type only)

```gherkin
Scenario: An Other-type member opts into the directory
  Given an Other-type member (isForVisitor = false) opens the My-interests edit screen
  Then the "Show me in Meet People Like You" checkbox is shown ("هل يظهر علي قابل أشخاص مثلك")
  When they tick it and save the profile
  Then UserProfile.ShowInMeetLikeYou is persisted as true
  And on the next directory load they appear as a person entry

Scenario: A Normal / VIP visitor never sees the opt-in
  Given a visitor (isForVisitor = true) opens the My-interests edit screen
  Then the "Show me in Meet People Like You" checkbox is NOT shown
```

**Evidence:** screen test `src/Mobile/simf_app/test/features/meet/meet_people_screen_test.dart`
(list of kinds, empty, error, per-kind tap, non-tappable person);
`partner_directory_models_test.dart` (model decode + kind predicates + logo URL);
backend `tests/SIMF.Api.Tests/PartnerDirectoryServiceTests.cs` (deduped union,
Normal/VIP excluded, speaker de-dup, flag off → empty); opt-in gating in
`sign_up_interests_screen` (edit-mode + `!isForVisitor`).

---

_Last reviewed:_ `2026-07-22` by `SIMF Team` - Build #13 (partner-directory rework; supersedes the recommender scenarios).
