# Rejected (Web) — `/account/rejected`

| | |
|--|--|
| **Route** | `/account/rejected` |
| **Audience** | Visitor whose registration was rejected |
| **Auth** | `[Authorize]` (AccountState=Rejected) |
| **Status** | ✅ Real |
| **Source** | [`Rejected.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/Rejected.razor) |
| **Last reviewed** | 2026-05-28 |

State page for `Rejected` visitors. Shows the verbatim bilingual reason
the admin wrote (10–500 chars) + the rejection timestamp + a sign-out
button. The visitor can read the reason and reach out offline.

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 5).
