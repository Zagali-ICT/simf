# Visitor profile (Web) — `/account/profile`

| | |
|--|--|
| **Route** | `/account/profile` (Website) |
| **Layout** | `MainLayout` |
| **Audience** | Approved visitor |
| **Auth** | `[Authorize]` + `InteractiveServerNoPrerender` (interactive island) |
| **Status** | ✅ Real |
| **Backend** | `GET /account/api/profile`, `PUT /account/api/profile`, `POST /account/api/profile/id-document`, `GET /account/api/interests`, `GET /account/api/profile-types/visitor` |
| **Source** | [`UserProfile.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/UserProfile.razor) + [`UserProfile.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Account/UserProfile.razor.cs) |
| **Last reviewed** | 2026-07-06 |

## 1. Purpose

The visitor's self-service profile page. Closes the registration loop:
sign-up → admin approval → land here → fill identity + nationality + ID +
contact + interests → save → get the QR badge for event entry. The page
also renders the QR code as soon as the account is Approved (driven by
D-046a).

## 4. UI

- Header: page title + (D-132) **Notifications** link + Sign out.
- **QR card** — visible only when `QrId` is set (i.e. account is Approved
  and minted). Renders the SVG QR + the QR id below.
- **Profile form** — Identity (Name EN/AR, DisplayName, DOB, place of birth)
  / Nationality + ID (Saudi toggle, ID number) / Contact (mobile, email) /
  Interests (chip multi-select, ≤ 10) / ID document upload.
- **Save** → toast `Account.Profile.Saved`.

## 7. Edge cases

- **Account not yet Approved** → QR card hidden; profile fields still
  editable (visitor can fill while waiting for approval).
- **Bad Saudi ID format** → server validation surfaces bilingual error.
- **ID document > 5 MB** → server rejects.

## 11. E2E

| Scenario | ID |
|----------|----|
| Fill profile + save → toast | E2E-WEB-PRF-001 |
| Approved visitor sees QR card | E2E-WEB-PRF-002 |
| Pending visitor sees no QR | E2E-WEB-PRF-003 |
| Notifications link routes to /account/notifications (D-132 orphan fix) | E2E-WEB-PRF-004 |
| RTL render | E2E-WEB-PRF-005 |

## 12. Related

- Decisions: D-046a (QR minted on approval), D-049 (route rename to `/account/profile`), D-064 (no nav menu), D-132 (Notifications link wired).
- Companion: [`account-notifications.md`](account-notifications.md).

## Tests

- bUnit: [`UserProfilePageTests`](../../../tests/SIMF.Web.Tests/UserProfilePageTests.cs)
  — a load-error smoke (the page composes + shows the fetch-failure state). A
  full happy-path render (four stubbed BFF loads) is a follow-up.

## Changelog

- 2026-07-06 (D-633) — C# moved to a `UserProfile.razor.cs` code-behind partial
  (Website clean-code, Phase 5); behaviour + wire unchanged. Added a smoke test.

_Last reviewed:_ 2026-07-06 by Claude (D-633).
