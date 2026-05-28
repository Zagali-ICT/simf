# E2E test catalogue — Print badge desk (`/admin/print-bag`)

| | |
|--|--|
| **Page** | [`cp/admin-print-bag.md`](../../pages/cp/admin-print-bag.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-PRT-001 | Lookup known visitor → badge renders + Print + Reset | P0 |
| E2E-PRT-002 | Lookup unknown QR id → 404 toast | P0 |
| E2E-PRT-003 | Reset clears + refocuses input | P2 |
| E2E-PRT-004 | Print fires window.print() with @media print CSS isolating badge | P1 |
| E2E-PRT-005 | RTL: badge mirrors correctly | P2 |
| E2E-PRT-006 | Non-admin → /not-permitted | P0 |

## Scenarios

### E2E-PRT-001 — Lookup + reprint

```gherkin
Scenario: Reprint a known visitor's badge
  Given a visitor with QrId="ABC123XYZ789" + DisplayName="Mohammed A." exists
  And the desk operator is signed in on /admin/print-bag
  When they type "ABC123XYZ789" into the QR id input
  And click "Search"
  Then GET /account/api/admin/qr-lookup/ABC123XYZ789 returns 200
  And the page renders the badge:
    - profile-type color stripe (from ProfileType.PageColor)
    - "Mohammed A." as DisplayName
    - server-rendered SVG QR (QRCoder, navy #0B2545)
    - the QR id below
  When they click "Print"
  Then window.print() fires
  And the print preview shows only .simf-walkin-badge (everything else hidden by @media print)
  When they click "Reset"
  Then the input clears + autofocuses
```

### E2E-PRT-002 — Unknown QR

```gherkin
Scenario: Unknown QR id shows error
  Given the desk types an unknown id "ZZZZZZZZZZZZ"
  And clicks "Search"
  Then the server returns HTTP 404 + ApiResult.Error.Code="NotFound"
  And the toast surfaces Admin.PrintBag.Error.NotFound (bilingual)
  And no badge renders
```

### E2E-PRT-003 — Reset

```gherkin
Scenario: Reset clears + refocuses
  Given a badge is currently rendered
  When the desk clicks "Reset"
  Then the QR id input clears
  And focus moves back to the input (next scan goes straight in)
  And the rendered badge area disappears
```

### E2E-PRT-004 — Print isolation

```gherkin
Scenario: Print CSS isolates the badge
  Given a badge is rendered
  When the desk clicks "Print"
  Then window.print() fires
  And the print preview (Chrome / Edge):
    - hides the SimfAppShell nav rail + top bar
    - hides the page banner + form
    - shows only .simf-walkin-badge
  And the printed output is a single-card badge
```

### E2E-PRT-005 — RTL

```gherkin
Scenario: Badge mirrors in Arabic
  Given the operator toggles "العربية"
  When they look up the same QR id
  Then the badge content reads RTL (Arabic profile-type label, Arabic name)
  And the QR id stays LTR (it's a 12-char ASCII identifier)
```

### E2E-PRT-006 — Auth gate

```gherkin
Scenario: Non-admin user denied
  Given a Visitor signs in to the CP
  When they navigate to /admin/print-bag
  Then they land on /not-permitted with HTTP 200
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
