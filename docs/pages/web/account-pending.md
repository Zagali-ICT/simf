# Pending approval (Web) — `/account/pending`

| | |
|--|--|
| **Route** | `/account/pending` |
| **Audience** | Visitor whose registration is awaiting approval |
| **Auth** | `[Authorize]` (AccountState=PendingApproval) |
| **Status** | ✅ Real |
| **Source** | [`PendingApproval.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/PendingApproval.razor) |
| **Last reviewed** | 2026-05-28 |

State page for `PendingApproval` visitors. Friendly explanation + sign-out
button. Once an admin approves the visitor (via `/admin/visitors/pending`),
their next visit routes past this page to `/account/profile`.

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
