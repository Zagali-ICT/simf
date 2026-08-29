# Print badge desk — `/admin/print-bag`

| | |
|--|--|
| **Route** | `/admin/print-bag` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (print desk staff at run-time) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Attendees.PrintBag)]` + Approved account |
| **Pattern** | Standalone lookup-+-action page (no grid). Page-top `SimfBanner` per D-117/D-132. |
| **Status** | ✅ Real |
| **Implements use case(s)** | UC-PRT-LOOKUP, UC-PRT-REPRINT _(pending UCS)_ |
| **Backend endpoints** | `GET /account/api/admin/qr-lookup/{qrId}` (D-130) |
| **Source file** | [`PrintBag.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PrintBag.razor) |
| **Tests** | _(pending)_ |
| **Last reviewed** | 2026-05-28 |

---

## 1. Purpose

Print-bag is a single-purpose station for **reprinting visitor badges by QR
id**. Visitors who lost / damaged their badge come to the print desk; staff
either scans the existing QR with a USB barcode scanner (which auto-types
the 12-character id into the input) or types the id by hand. The page calls
the D-130 lookup endpoint, renders the exact same badge markup the walk-in
success modal uses (colour-coded stripe, name, QR, QR id), and offers a
**Print** button that triggers `window.print()` with the existing
`@media print` CSS that isolates the badge for clean output.

## 2. Audience + permissions

- **Who can reach it:** Administrator (the print desk operator).
- **Why pinned to Administrator:** the lookup exposes the visitor's name +
  profile-type — a moderate-PII read. The desk operator must be authenticated
  + approved.
- **No data mutation** — purely a read + reprint. No audit row mints because
  D-109's interceptor only fires on writes; the print itself is a client-side
  browser action.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Empty (just signed in) | `docs/screenshots/audit-admin-print-bag.png` | 2026-05-28 |
| After successful lookup | _to capture_ | — |
| Lookup not found error | _to capture_ | — |
| Browser print preview | _to capture_ | — |

## 4. UI affordances

### 4.1 Banner

`<SimfBanner Title="@L[\"Admin.PrintBag.Title\"]" Subtitle="@L[\"Admin.PrintBag.Supporting\"]" />`

### 4.2 Form

| Affordance | Behaviour |
|------------|-----------|
| **QR id** text input | 12 chars (max 32), `autocomplete="off"` so the browser doesn't autofill from cached values; auto-trimmed + upper-cased server-side |
| **Search** button | Loading state with `LoadingLabel`; disables during the round-trip |
| **Reset** button | Appears only after a successful lookup; clears + refocuses for the next scan |
| **Print** button | `window.print()` — fires the existing `@media print` rule that isolates `.simf-walkin-badge` |

### 4.3 Badge output

After a successful lookup, the page renders:

```
┌─────────────────────────────┐
│ Profile type (e.g. General) │ ← colour stripe from ProfileType.PageColor
├─────────────────────────────┤
│ {Display name}              │
│ {Email or "no-email" copy}  │
│ [QR SVG, 6 px/module, navy] │
│ {QR id, e.g. ABC123XYZ789}  │
└─────────────────────────────┘
```

## 5. Data flow

```
Staff scans / types QR id → input fires onsubmit
  → OnSearchAsync → simfAccount.getJson("/account/api/admin/qr-lookup/{qrId}")
  → BFF forwards with bearer
  → API GET /api/v1/admin/qr-lookup/{qrId}
  → AdminApprovalReadService.LookupByQrIdAsync (D-130) — UserProfile.QrId match,
    pairs with SimfUser, returns AdminWalkInRegistrationResponse
  → ApiResult<AdminWalkInRegistrationResponse>
  → page renders badge markup + QR SVG via QRCoder
  → staff clicks Print → window.print()
```

## 6. Validation + error handling

- **Empty QR id** → client-side error `Admin.PrintBag.Error.Required`.
- **Unknown QR id** → server 404 `ErrorCodes.NotFound` → client error
  `Admin.PrintBag.Error.NotFound` (no enumeration leak: the lookup is purely
  by QR id, no cross-kind ambiguity).
- **Server / network failure** → caught, surfaces the same NotFound message
  (sufficient signal for a desk operator).

## 7. Edge cases + known limitations

- **QR id casing** — backend uppercases + trims; the input accepts any case.
- **Visitor with no email** — the badge shows the `Admin.WalkIn.Success.NoEmail`
  copy when the email matches `*@simf.local` (placeholder).
- **Print isolation** — the `@media print` rule in `simf-components.css`
  hides everything except `.simf-walkin-badge` so the printer doesn't render
  the nav rail or banner. Tested on Chrome + Edge; other browsers should
  honour the same CSS.
- **No bulk-reprint** — one badge at a time. A bulk reprint would need a list
  upload + per-row print — out of scope.
- **No audit row for the lookup itself** — D-109 fires on writes only.
  The lookup is a read; the print is client-side. If audit of "X reprinted
  visitor Y's badge" is required, a separate `Admin.BadgeReprinted` event
  would need to land in `AdminApprovalReadService.LookupByQrIdAsync`.

## 8–9. i18n + accessibility

`Admin.PrintBag.*` keys cover every label (EN + AR). Input has a visible
label + helper text + `autocomplete="off"`. Print button has descriptive
label.

## 10. Related use cases

| UC ID | Title |
|-------|-------|
| UC-PRT-LOOKUP | Look up a visitor by QR id |
| UC-PRT-REPRINT | Reprint badge after lookup |

## 11. Related E2E test scenarios

| Scenario | ID | Coverage |
|----------|----|----------|
| Lookup known visitor → badge renders | E2E-PRT-001 | golden |
| Lookup unknown → error message | E2E-PRT-002 | 404 |
| Reset clears + refocuses input | E2E-PRT-003 | UX |
| Print fires `window.print` | E2E-PRT-004 | print |
| RTL → badge mirrors correctly | E2E-PRT-005 | i18n |
| Non-admin user → /not-permitted | E2E-PRT-006 | role gate |

## 12. Related docs

- Manual: `Admin-Manual.md § 4.3 Print badge desk` _(pending chapter)_
- Decisions: D-130 (page + lookup endpoint shipped together).
- Source: `PrintBag.razor`, `AdminApprovalReadService.LookupByQrIdAsync`,
  `QrLookupEndpoint.cs`.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-28 | D-130 | Original implementation: page + lookup endpoint + reuse of walk-in success markup. |

---

_Last reviewed:_ 2026-05-28 by Claude (D-133 slice 2).
