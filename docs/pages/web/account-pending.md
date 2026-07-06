# Pending approval (Web) — `/account/pending`

| | |
|--|--|
| **Route** | `/account/pending` |
| **Audience** | Visitor whose registration is awaiting approval |
| **Auth** | `[Authorize]` (AccountState=PendingApproval) |
| **Status** | ✅ Real |
| **Source** | [`PendingApproval.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/PendingApproval.razor) + [`PendingApproval.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Account/PendingApproval.razor.cs) |
| **Last reviewed** | 2026-07-06 |

State page for `PendingApproval` visitors. Friendly explanation + sign-out
button. Once an admin approves the visitor (via `/admin/visitors/pending`),
their next visit routes past this page to `/account/profile`.

## Tests

- bUnit: [`AccountStateBannerTests`](../../../tests/SIMF.Web.Tests/AccountStateBannerTests.cs)
  — the PendingApproval render + the Approved/Rejected redirects.

## Changelog

- 2026-07-06 (D-631) — C# moved to a `PendingApproval.razor.cs` code-behind
  partial (Website clean-code, Phase 5); behaviour unchanged.

_Last reviewed:_ 2026-07-06 by Claude (D-631).
