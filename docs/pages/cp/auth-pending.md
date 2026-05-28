# Pending approval state banner — `/auth/pending`

| | |
|--|--|
| **Route** | `/auth/pending` |
| **Audience** | Self-registered user awaiting admin approval |
| **Auth** | `[Authorize]` (account in `PendingApproval`) |
| **Status** | ✅ Real |
| **Source** | [`PendingApproval.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/PendingApproval.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Friendly holding page for accounts in `PendingApproval`. Explains that
an administrator must approve the account before sign-in is allowed.
Offers a sign-out button.

## 7. Edge cases

- **Account just got approved** → next sign-in routes past this page.
- **Account got rejected** → server redirects to `/auth/rejected` instead.

## 11. E2E

| Scenario | ID |
|----------|----|
| Pending account sign-in lands here | E2E-APN-PG-001 |
| Sign out button works | E2E-APN-PG-002 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
