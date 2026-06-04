# E2E test catalogue — Print badge desk (`/admin/print-bag`)

| | |
|--|--|
| **Page** | [`cp/admin-print-bag.md`](../../pages/cp/admin-print-bag.md) |
| **Route** | `/admin/print-bag` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page permission:** `[RequirePermission(PermissionCatalog.Attendees.PrintBag)]`
> (`"Attendees.PrintBag"`, baseline `AdminOnly`). Note the **API endpoint
> `GET /api/v1/admin/qr-lookup/{qrId}` is gated by `Attendees.View`**, not
> `Attendees.PrintBag` (see `QrLookupEndpoint.Configure()`), so a role granted
> `PrintBag` but not `View` would see the page but get a 403 on the lookup. The
> `Administrator` wildcard (`"*"`) used by the superadmin satisfies both.
> **Read + reprint only — no data mutation, so no `RowAudit` / `OperationLog`
> row mints** (D-109 interceptor fires on writes only).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-PRT-001 | Golden path — look up a known QR id → badge renders → Print | happy | P0 | _to author_ |
| E2E-PRT-002 | Empty / initial state — clean form, no Reset/Print, no network call | happy | P1 | _to author_ |
| E2E-PRT-003 | "Scan another" (Reset) clears the badge + refocuses the input | happy | P1 | _to author_ |
| E2E-PRT-004 | Print button fires `window.print()` (badge-only `@media print`) | happy | P1 | _to author_ |
| E2E-PRT-005 | QR-id casing — lowercase / spaced input still resolves | happy | P2 | _to author_ |
| E2E-PRT-006 | Visitor with placeholder email shows the "no email" copy | happy | P2 | _to author_ |
| E2E-PRT-007 | Auth gate — admin lacking `Attendees.PrintBag` → /not-permitted | auth | P0 | _to author_ |
| E2E-PRT-008 | Validation — blank QR id → client `Required` error, no request | error | P1 | _to author_ |
| E2E-PRT-009 | Not found — unknown QR id → 404 `NotFound` → bilingual error | error | P1 | _to author_ |
| E2E-PRT-010 | Server 500 / network failure → bilingual NotFound fallback | resilience | P2 | _to author_ |
| E2E-PRT-011 | RTL / Arabic render — page + badge mirror, Arabic type label | i18n | P1 | _to author_ |

## Scenarios

### E2E-PRT-001 — Golden path

```gherkin
Feature: Print badge desk golden path
  As a print-desk Administrator
  I want to look up a visitor by their 12-character QR id and reprint the badge
  So that a visitor who lost or damaged their badge can be re-issued one

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp using
      superadmin@zagali-ict.com and a fresh Get-Totp code
  And they have landed on /admin/print-bag

Scenario: Look up a known QR id, render the badge, and print
  Given a visitor exists whose UserProfile.QrId = "V4DF5B8P46YM"
        (mint one first via /admin/walk-in or read an existing QrId from
         the SIMF_App.UserProfiles table)
  And the page shows the SimfBanner titled "Print badge"
        with the supporting line "Scan or enter a 12-character QR id to reprint
        a visitor's or staff member's badge."
  And the form shows a single "QR id" field with helper text
        "12 Crockford-base32 characters (e.g. V4DF5B8P46YM). Case-insensitive."
  And only the "Look up" button is visible (no "Scan another", no "Print")
  When the administrator types "V4DF5B8P46YM" into the QR id field
  And clicks "Look up"
  Then the button briefly shows the loading label "Looking up…"
  And the BFF call GET /account/api/admin/qr-lookup/V4DF5B8P46YM returns HTTP 200
  And the API call GET /api/v1/admin/qr-lookup/V4DF5B8P46YM returns HTTP 200
        with ApiResult.Success = true
  And the badge (.simf-walkin-badge) renders with:
        the profile-type label in the coloured header stripe,
        the visitor's DisplayName,
        the visitor's Email (or the "no email" copy — see E2E-PRT-006),
        a QR SVG (QRCoder, 6 px/module, navy #0B2545 on white),
        and the literal QR id "V4DF5B8P46YM" beneath it
  And the "Scan another" button is now visible
  And a separate "Print" button appears in the form actions
  And no SimfAlert error is shown
  When the administrator clicks "Print"
  Then window.print() is invoked
  And the @media print CSS isolates only .simf-walkin-badge (nav rail + banner hidden)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-print-bag-golden-before.png` (clean form)
- Screenshot after: `docs/screenshots/cp-admin-print-bag-golden-after.png` (rendered badge + Print)
- Screenshot print: `docs/screenshots/cp-admin-print-bag-golden-print.png` (browser print preview, badge only)
- Console errors: 0 expected
- Network: `GET /account/api/admin/qr-lookup/V4DF5B8P46YM` (BFF) and
  `GET /api/v1/admin/qr-lookup/V4DF5B8P46YM` (API) both return 200
- Audit row: **none expected** — the lookup is a read and the print is client-side
  (D-109 interceptor fires on writes only).

### E2E-PRT-002 — Empty / initial state

```gherkin
Scenario: Initial page state is a clean, single-field form
  Given the administrator opens /admin/print-bag and has not searched yet
  Then the QR id field is empty
  And the "Look up" button is the only action button
  And neither "Scan another" nor "Print" is rendered
  And no badge is shown
  And no SimfAlert error is shown
  And no /account/api/admin/qr-lookup/... request has fired
```

### E2E-PRT-003 — "Scan another" (Reset)

```gherkin
Scenario: Scan another clears the badge and refocuses the input
  Given a successful lookup has rendered the badge for "V4DF5B8P46YM"
  And the "Scan another" button is visible
  When the administrator clicks "Scan another"
  Then the badge is removed
  And the "Print" button is removed
  And the "Scan another" button is removed (only "Look up" remains)
  And the QR id field is cleared
  And the QR id field receives focus so the desk can immediately scan the next id
  And no error is shown
```

### E2E-PRT-004 — Print isolation

```gherkin
Scenario: Print button triggers window.print with badge-only output
  Given a successful lookup has rendered the badge
  When the administrator clicks "Print"
  Then the page invokes JS window.print()
  And under @media print only .simf-walkin-badge is visible
  And the SimfBanner, the CP nav rail and the search form are hidden in print preview
```

### E2E-PRT-005 — QR-id casing / whitespace

```gherkin
Scenario: Lowercase and padded input still resolves the badge
  Given a visitor exists whose UserProfile.QrId = "V4DF5B8P46YM"
  When the administrator types "  v4df5b8p46ym  " (lowercase, leading/trailing spaces)
  And clicks "Look up"
  Then the client trims the value before sending
  And the server uppercases + trims the QR id (LookupByQrIdAsync normalises with
      Trim().ToUpperInvariant())
  And the lookup returns HTTP 200 and renders the same badge as E2E-PRT-001
```

### E2E-PRT-006 — Placeholder-email badge

```gherkin
Scenario: Walk-in visitor without an email shows the no-email copy
  Given a visitor exists whose email is a placeholder ending in "@simf.local"
        (the walk-in flow assigns this when no email is captured)
  When the administrator looks up that visitor's QR id
  Then the badge renders the "no email" copy from Admin.WalkIn.Success.NoEmail
        instead of the raw {guid}@simf.local placeholder
```

### E2E-PRT-007 — Auth gate

```gherkin
Scenario: Administrator lacking the Attendees.PrintBag permission is denied
  Given a signed-in admin whose role does NOT include "Attendees.PrintBag"
        (and is not the Administrator wildcard "*")
  When they navigate to /admin/print-bag
  Then they land on /not-permitted with HTTP 200
  And the QR id form does not render
  And no /account/api/admin/qr-lookup/... request fires
```

### E2E-PRT-008 — Validation: blank QR id

```gherkin
Scenario: Submitting a blank QR id shows the client-side required error
  Given the administrator is on /admin/print-bag with an empty QR id field
  When they click "Look up" without typing anything
  Then a SimfAlert error appears reading
      "Enter or scan a QR id." / "أدخل أو امسح رمز QR."
        (resx key Admin.PrintBag.Error.Required)
  And no /account/api/admin/qr-lookup/... request fires
  And no badge renders
```

### E2E-PRT-009 — Not found (unknown QR id)

```gherkin
Scenario: Unknown QR id returns 404 and surfaces the bilingual NotFound message
  Given no UserProfile has QrId = "ZZZZZZZZZZZZ"
  When the administrator types "ZZZZZZZZZZZZ"
  And clicks "Look up"
  Then the BFF call GET /account/api/admin/qr-lookup/ZZZZZZZZZZZZ returns HTTP 404
  And the API responds ApiResult.Success = false with Error.Code = "NotFound"
        and the bilingual message
        "No badge was found for this QR id." / "لم يتم العثور على شارة بهذا الرمز."
  And the page shows a SimfAlert error with the server message
        (falling back to Admin.PrintBag.Error.NotFound when none is present)
  And no badge renders
```

### E2E-PRT-010 — Server 500 / network failure

```gherkin
Scenario: A 500 or transport failure surfaces the NotFound fallback
  Given the API is forced to return 500 (or is unreachable) for
        /api/v1/admin/qr-lookup/{qrId}
  When the administrator looks up any QR id
  Then OnSearchAsync catches the failure
  And shows the fallback SimfAlert error
        "No badge was found for this QR id." / "لم يتم العثور على شارة بهذا الرمز."
        (resx key Admin.PrintBag.Error.NotFound)
  And no badge renders
  And the page does not crash (no unhandled Blazor circuit error)
```

### E2E-PRT-011 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the badge
  Given the administrator is on /admin/print-bag in English
  When they switch the UI culture to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "طباعة الشارة"
  And the supporting line reads
      "امسح أو أدخل رمز QR المكوّن من 12 حرفًا لإعادة طباعة شارة الزائر أو الموظف."
  And the QR id label reads "رمز QR" with helper
      "12 حرفًا بصيغة Crockford-base32 (مثال: V4DF5B8P46YM). غير حساس لحالة الأحرف."
  And the button reads "بحث"
  When the administrator looks up "V4DF5B8P46YM"
  Then the badge type label renders the Arabic profile-type name
        (ProfileTypeNameArabic, via the ar culture branch)
  And the "Scan another" button reads "امسح آخر"
  And the badge mirrors correctly under RTL
```

---

## Implementation notes

- **No dedicated API integration test for the lookup.** A repo search for
  `qr-lookup` / `QrLookup` / `LookupByQrId` under `tests/` returns nothing.
  The closest lower-layer coverage is
  `tests/SIMF.Api.Tests/WalkInRegistrationTests.cs`, which mints a QR id during
  walk-in registration (`Visitor_walk_in_creates_approved_user_with_qr_minted`
  asserts `body.Data.QrId` is non-empty) — that QR id is the input this page
  consumes, so the walk-in test is the natural source of a real `QrId` for the
  E2E setup. Consider adding a `QrLookupTests` case at the API layer (200 for a
  minted id, 404 for an unknown id, `Attendees.View` policy enforcement) to back
  E2E-PRT-001 / -009 / -007 at a lower layer.
- **Permission divergence to verify.** The CP page is gated by
  `Attendees.PrintBag` but the API endpoint by `Attendees.View`
  (`QrLookupEndpoint.Configure()`). E2E-PRT-007 covers the page gate; a future
  API test should also assert the endpoint rejects a token without
  `Attendees.View`.
- **Getting a real QR id for the run.** Either register a walk-in visitor via
  the `/admin/walk-in` flow (the success modal shows the freshly minted QR id),
  or read an existing `QrId` from `SIMF_App.dbo.UserProfiles`. The lookup is
  case-insensitive and trimmed server-side (`Trim().ToUpperInvariant()`), so
  E2E-PRT-005 can reuse the same id in lowercase.
- **Convert to Playwright** when the runner is adopted: each Gherkin scenario
  maps to a `.feature` row + step-definition class under
  `tests/SIMF.E2E.Tests/` (project to be created). The steps are written
  runner-agnostic against Chrome DevTools MCP today.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
