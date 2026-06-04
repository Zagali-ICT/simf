# Website home — `/account`

| | |
|--|--|
| **Route** | `/account` |
| **Layout** | `MainLayout` (no nav menu per D-064) |
| **Audience** | Any signed-in visitor |
| **Auth** | `[Authorize]` |
| **Status** | ✅ Real |
| **Source** | [`Home.razor`](../../../src/Website/SIMF.Web/Components/Pages/Home.razor) |
| **Last reviewed** | 2026-05-28 |

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

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
