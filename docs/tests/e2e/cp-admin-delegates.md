# E2E test catalogue — Delegates (وفد) desk (`/admin/delegates`)

| | |
|--|--|
| **Page** | [`cp/admin-delegates.md`](../../pages/cp/admin-delegates.md) |
| **Route** | `/admin/delegates` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-30 (G2 / D-800: corrected the invited-country fixture — the host country is never `IsInvited`, D-768). Prior: 2026-07-22 (batch-builder redesign; reusable `BulkBadgeGenerator`, also on `/admin/visitors`) |

> **What this page does (grounded in `DelegatesPage.razor`, D-473 / #10).** A delegate
> (وفد) is an **ordinary visitor** with the `IsDelegate` flag set and a nationality
> that is an **invited country** (`Country.IsInvited`). This is a SEPARATE page (a
> copy of the visitor walk-in) so the visitor flow is untouched. Two sections:
> - **Register a delegate** — hosts the shared `WalkInRegistrationForm` with
>   `IsDelegate=true`. On submit the API (`POST …/visitors/register-onsite`) sets
>   `UserProfile.IsDelegate=true` and **rejects a non-invited nationality** with
>   `400 DELEGATE_COUNTRY_NOT_INVITED`. Gated by `Visitors.RegisterOnsite`.
> - **Bulk-generate badges** — the reusable `BulkBadgeGenerator` component, redesigned
>   2026-07-22 into a **batch-builder**: choose a profile type + count and press
>   **Add** to build a batch list (e.g. VIP × 5, Delegate × 3), an "as delegates"
>   toggle, then **Generate** → confirm popup (summary + optional organiser email) →
>   `POST …/visitors/bulk-generate`. Each badge is an Approved visitor with **default
>   data** and a minted QR (placeholders to hand out / fill later). Gated by
>   `Visitors.BulkGenerate` (capped 1000/request). The **same component** is surfaced
>   on `/admin/visitors` via a gated "Bulk add" toolbar button (see `cp-admin-visitors.md`).
>
> **Invited countries** are marked in the Countries admin (`/admin/countries` →
> Add/Edit → "Invited to send a delegation" toggle, `Country.IsInvited`). The **host**
> country (Saudi Arabia) is the OWNER of the forum, not a visiting delegation, so it is
> deliberately **never** flagged `IsInvited` (D-768) — its flagged visitors can still
> request meetings WITH an invited delegation. On the app side the public list also hides
> the **viewer's own** country (G2 / D-800), so an admin marking a country invited here is
> publishing it to every viewer **except** that country's own nationals.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-DLG-001 | Register a delegate from an invited country → Approved-pending, profile `IsDelegate=true` | happy | P0 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-002 | Register a delegate from a NON-invited country → `400 DELEGATE_COUNTRY_NOT_INVITED` | error | P0 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-003 | A plain (non-delegate) visitor is NOT constrained to invited countries | edge | P1 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-004 | Bulk-generate (e.g. 3 VIP) → N Approved badges, each with a QR, flagged per the toggle | happy | P0 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-005 | Bulk-generate with no count picked / empty request → `400` (and the UI keeps the form) | error | P1 | authored ✓ (DelegatesAndBulkBadgesTests, empty-400) |
| E2E-DLG-006 | Bulk-generate over the per-request cap (1000) → `400` | error | P2 | _to author_ (service-capped) |
| E2E-DLG-007 | Auth gate — admin lacking `Visitors.RegisterOnsite` → `/not-permitted`; bulk panel hidden without `Visitors.BulkGenerate` | auth | P0 | _to author_ (gates verified by CpNavigationPermission + PermissionEnforcement) |
| E2E-DLG-008 | Country admin — toggle "Invited to send a delegation" on a country → it becomes a valid delegate nationality | happy | P1 | _to author_ |
| E2E-DLG-009 | RTL / Arabic render — page + both sections mirror | i18n | P1 | _to author_ |
| E2E-DLG-010 | Bulk-generate with an organiser email → one ZIP of all badge PNGs emailed to that address; response `EmailQueued=true` | happy | P1 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-011 | Bulk-generate with an INVALID organiser email → `400 VALIDATION_FAILED`, zero accounts, no email | error | P1 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-012 | Bulk-generate with NO organiser email → badges only, `EmailQueued=false`, nothing enqueued (back-compat) | edge | P2 | authored ✓ (DelegatesAndBulkBadgesTests) |
| E2E-DLG-013 | Batch-builder — Add appends a row; adding the same type merges its count; Remove drops a row; Generate disabled while the batch is empty | happy | P1 | authored ✓ (BulkBadgeGeneratorTests) |
| E2E-DLG-014 | Batch-builder — Add with no type/count shows the "choose a type and a count" message and adds nothing | error | P1 | authored ✓ (BulkBadgeGeneratorTests) |
| E2E-DLG-015 | Same generator surfaced on `/admin/visitors` "Bulk add" dialog posts the identical `bulk-generate` request | happy | P1 | authored ✓ (BulkBadgeGeneratorTests) |
| E2E-DLG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-DLG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-DLG-001 / 002 — Register a delegate (invited vs non-invited country)

```gherkin
Feature: Delegate registration is constrained to invited countries
Background:
  Given an Administrator has signed in to the Control Panel
  And the country "Japan" is marked Invited (Country.IsInvited = true) via /admin/countries
  And the country "United States" is NOT invited
  # NOTE (D-768): the host country (Saudi Arabia) is the OWNER of the forum, not a
  # visiting delegation, so it is deliberately NEVER marked Country.IsInvited. Use a
  # visiting country in this fixture — an earlier draft of this file used "Saudi Arabia"
  # and contradicted the product rule.

Scenario: A delegate from an invited country registers
  Given the administrator is on /admin/delegates
  When they fill the "Register a delegate" form with a Japanese nationality and submit
  Then POST /account/api/admin/visitors/register-onsite returns 200
  And the created UserProfile has IsDelegate = true

Scenario: A delegate from a non-invited country is rejected
  When they fill the form with a United States nationality (IsDelegate desk) and submit
  Then the API returns 400 with code DELEGATE_COUNTRY_NOT_INVITED
  And the form shows the bilingual "a delegate's nationality must be an invited country" error
```

### E2E-DLG-004 / 005 / 013 / 014 — Bulk-generate badges (batch-builder)

```gherkin
Scenario: Bulk-generate badges by building a batch
  Given the administrator is on /admin/delegates with Visitors.BulkGenerate
  When they choose the "VIP" profile type, enter count "3", and click "Add"
  Then a batch row "VIP × 3" appears with a running total of 3 badges
  When they tick "Flag the generated badges as delegates" and click "Generate badges"
  And they confirm the popup ("VIP × 3 → 3 badge(s)") with no organiser email
  Then POST /account/api/admin/visitors/bulk-generate returns 200 with Created = 3
  And 3 Approved UserProfiles of that type are created, each IsDelegate = true with a minted QR
  And a toast reads "3 badge(s) generated."

Scenario: The batch-builder merges a repeated type and removes a row
  When they Add "VIP × 5" and then Add "VIP × 3"
  Then the batch shows a single "VIP × 8" row (merged), total 8
  When they click Remove on that row
  Then the batch is empty and the "Generate badges" button is disabled

Scenario: Add with no type or count is rejected
  When they click "Add" without choosing a type and a count above zero
  Then no batch row is added and an inline message asks to choose a type and a count
```

### E2E-DLG-010 / 011 / 012 - Bulk-generate with an organiser email (D-751)

```gherkin
Feature: The generated QR badges can be emailed to one organiser as a ZIP

Scenario: Bulk-generate and email the QR badges to an organiser
  Given the administrator is on /admin/delegates with Visitors.BulkGenerate
  When they Add "VIP × 2" and "Delegate × 3" to the batch and click "Generate badges"
  Then a confirm dialog opens showing "VIP × 2 + Delegate × 3 → 5 badge(s)"
  When they enter the organiser email "events@simf.example" and confirm
  Then POST /account/api/admin/visitors/bulk-generate returns 200 with Created = 5 and EmailQueued = true
  And exactly one email is enqueued to events@simf.example
  And that email carries a single ZIP attachment named "badges-<yyyyMMdd-HHmm>.zip"
  And the ZIP contains 5 PNG entries, one QR image per badge
  And a toast reads "5 badge(s) generated and emailed to events@simf.example."

Scenario: An invalid organiser email is rejected before anything is created
  When they pick a count, enter the organiser email "not-an-email", and confirm
  Then the API returns 400 with code VALIDATION_FAILED
  And zero badge accounts are created and no email is enqueued

Scenario: No organiser email leaves the badges DB-only (back-compat)
  When they pick a count, leave the organiser email blank, and confirm
  Then the API returns 200 with EmailQueued = false
  And the badges are created but no email is enqueued
```

## Implementation notes

- API coverage: `tests/SIMF.Api.Tests/DelegatesAndBulkBadgesTests.cs` — invited/non-invited
  delegate register-onsite, the non-delegate-unconstrained case, bulk-generate (QR + flag),
  the empty-bulk 400, and (D-751) the organiser-email ZIP path: with email (one ZIP of all
  badge PNGs enqueued, `EmailQueued=true`), invalid email (`400 VALIDATION_FAILED`, zero
  accounts), and no email (back-compat, nothing enqueued). The email is captured with the
  synchronous `FakeEmailQueue` via `BulkBadgeEmailApiFactory`.
- D-751 (#10): the confirm modal (`SimfModal`) carries an optional "Organiser email" field +
  a count summary; the service zips the QR PNGs (QRCoder `PngByteQRCode`, ECC Q) and enqueues
  them via the `BulkBadgeDelivery` email template. No new permission / nav / schema; the email
  is validated before any account is written.
- 2026-07-22 redesign: the bulk panel is now the reusable `BulkBadgeGenerator` component
  (batch-builder: type + count → Add → removable list → Generate → confirm). Same request
  contract (`AdminBulkGenerateBadgesRequest.Batches`). Rendered on both `/admin/delegates`
  (`DefaultIsDelegate=true`) and `/admin/visitors` ("Bulk add" dialog). UI pinned by
  `tests/SIMF.ControlPanel.Tests/BulkBadgeGeneratorTests.cs` (add / merge / pick-type / post).
  A latent D-648 bug (confirm email field missing `ValueExpression`) was fixed in passing.
- Permission gates (HARD RULE): page `[RequirePermission(Visitors.RegisterOnsite)]`; bulk
  endpoint policy `Visitors.BulkGenerate`; bulk panel wrapped in `<AuthorizedAction>`. The
  nav item `Module.AdminDelegates` carries `Visitors.RegisterOnsite`. Backed by
  `CpNavigationPermissionTests` + `PermissionEnforcementTests`.
- No new entity — `UserProfile.IsDelegate` + `Country.IsInvited` are additive columns
  (migration `D473`); the deleted D-174/D-183 Delegations module stays deleted (D-277).

---

_Last reviewed:_ 2026-07-22 by SIMF Team - #10 batch-builder redesign (reusable
`BulkBadgeGenerator`, E2E-DLG-013..015, also on `/admin/visitors`). 2026-07-20 - D-751 (#10)
bulk-generate organiser-email ZIP delivery (E2E-DLG-010..012); D-473 (#10) delegates desk.
