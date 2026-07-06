# Website home — `/account`

| | |
|--|--|
| **Route** | `/account` |
| **Layout** | `MainLayout` (no nav menu per D-064) |
| **Audience** | Any signed-in visitor |
| **Auth** | `[Authorize]` |
| **Status** | ✅ Real |
| **Source** | [`Home.razor`](../../../src/Website/SIMF.Web/Components/Pages/Home.razor) + [`Home.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Home.razor.cs) |
| **Last reviewed** | 2026-07-06 |

## 1. Purpose

Post-sign-in landing page on the Website. The Website's audience is the
visitor — the page is a minimal landing surface that routes them onward
(profile completion, notifications, etc.). Per D-064, the Website
deliberately omits a heavy nav menu — every page is reached via direct
URL or auth redirect.

## 7. Edge cases

- **Unauthenticated** → redirect to `/login`.
- **PendingApproval** → redirect to `/account/pending`.
- **Rejected** → redirect to `/account/rejected`.

## 11. E2E

| Scenario | ID |
|----------|----|
| Signed-in visitor lands on /account | E2E-WEB-HM-001 |
| State-banner redirects fire correctly | E2E-WEB-HM-002 |

## Tests

- bUnit: [`HomePageTests`](../../../tests/SIMF.Web.Tests/HomePageTests.cs) — the
  redirect-when-signed-out branch + the signed-in landing.

## Changelog

- 2026-07-06 (D-630) — C# moved to a `Home.razor.cs` code-behind partial
  (Website clean-code, Phase 5); added bUnit coverage. Behaviour unchanged.

_Last reviewed:_ 2026-07-06 by Claude (D-630).
