# Rejected state banner — `/auth/rejected`

| | |
|--|--|
| **Route** | `/auth/rejected` |
| **Audience** | User whose registration was rejected |
| **Auth** | `[Authorize]` (account in `Rejected`) |
| **Status** | ✅ Real |
| **Source** | [`Rejected.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/Rejected.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

State page for `Rejected` accounts. Shows the verbatim bilingual rejection
reason the admin wrote (10–500 chars), the timestamp, and a sign-out
button. The user can read the reason and reach out to the admin team
out-of-band.

## 7. Edge cases

- **Reason missing** (legacy rows) → falls back to a generic bilingual
  copy `Account.Rejected.NoReason`.

## 11. E2E

| Scenario | ID |
|----------|----|
| Rejected sign-in lands here | E2E-RJP-001 |
| Reason renders correctly EN + AR | E2E-RJP-002 |

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 4).
