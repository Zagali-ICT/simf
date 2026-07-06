# Rejected (Web) — `/account/rejected`

| | |
|--|--|
| **Route** | `/account/rejected` |
| **Audience** | Visitor whose registration was rejected |
| **Auth** | `[Authorize]` (AccountState=Rejected) |
| **Status** | ✅ Real |
| **Source** | [`Rejected.razor`](../../../src/Website/SIMF.Web/Components/Pages/Account/Rejected.razor) + [`Rejected.razor.cs`](../../../src/Website/SIMF.Web/Components/Pages/Account/Rejected.razor.cs) |
| **Last reviewed** | 2026-07-06 |

State page for `Rejected` visitors. Shows the verbatim bilingual reason
the admin wrote (10–500 chars) + the rejection timestamp + a sign-out
button. The visitor can read the reason and reach out offline.

## Tests

- bUnit: [`AccountStateBannerTests`](../../../tests/SIMF.Web.Tests/AccountStateBannerTests.cs)
  — the reason rendering (EN/AR fallback) + the Approved/PendingApproval redirects.

## Changelog

- 2026-07-06 (D-631) — C# moved to a `Rejected.razor.cs` code-behind partial
  (Website clean-code, Phase 5); behaviour unchanged.

_Last reviewed:_ 2026-07-06 by Claude (D-631).
