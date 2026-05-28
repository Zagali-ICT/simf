# E2E test catalogue — Visitors CRUD (`/admin/visitors`)

| | |
|--|--|
| **Page** | [`cp/admin-visitors.md`](../../pages/cp/admin-visitors.md) |
| **Surface** | Control Panel |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-VIS-001 | Walk-in Saudi visitor (D-127 wizard) golden | P0 |
| E2E-VIS-002 | Walk-in non-Saudi Passport branch | P0 |
| E2E-VIS-003 | Saudi ID typo (regex fail) → 400 + form error | P1 |
| E2E-VIS-004 | Details modal renders ID image inline (D-129) | P1 |
| E2E-VIS-005 | Cross-kind id on `/admin/visitors/{otherId}/profile` → 404 (D-124) | P0 |
| E2E-VIS-006 | Bulk-delete with reason → toast + reload | P1 |
| E2E-VIS-007 | Export selected → XLSX downloads | P2 |
| E2E-VIS-008 | RTL: walk-in wizard mirrors correctly | P1 |

## Scenarios

### E2E-VIS-001 — Walk-in Saudi golden

```gherkin
Scenario: Register a walk-in Saudi visitor end-to-end
  Given an admin (desk staff) is signed in on /admin/visitors
  And at least one Visitor profile-type "General" exists
  When they click "+ Add"
  Then the D-127 walk-in wizard opens at section 1 (Badge type)
  When they pick the "General" tile
  And in section 2 they fill Name on badge="Mohammed A.", DOB=1990-01-15, English name="Mohammed Ahmed", Arabic name="محمد أحمد", Place of birth="Riyadh"
  And in section 3 they keep the Saudi toggle on
  And fill National ID="1234567890"
  And in section 4 they fill Saudi mobile="+966500000000"
  And leave Email blank
  And in section 5 they skip the ID document upload
  And in section 6 they pick 2 interest chips
  And they click "Register"
  Then the WalkInSuccessModal opens with:
    - the profile-type color stripe
    - "Mohammed A." as the name
    - a server-rendered SVG QR
    - the 12-char QR id below
  And the server has created a SimfUser with AccountState=Approved
  And synthesized email "walkin-{guid}@simf.local"
  And minted the QR via IQrIdMinter
  And audited Admin.WalkInRegistered with the desk staff actor
  When the desk clicks "Print badge"
  Then window.print() fires with the @media print CSS isolating .simf-walkin-badge
```

### E2E-VIS-002 — Non-Saudi Passport

```gherkin
Scenario: Walk-in non-Saudi visitor with Passport
  Given the wizard is open at section 3
  When the desk toggles Saudi off
  Then a country picker appears + Iqama/Passport sub-picker
  When the desk picks Country="United Kingdom"
  And toggles Passport
  And fills Passport number="GB1234567"
  And completes sections 2, 4, 5, 6 as in E2E-VIS-001
  And clicks "Register"
  Then the success modal opens with the badge
  And the server stored NationalityCode="GB" + PassportNumber="GB1234567"
  And NationalId is null + IqamaNumber is null
```

### E2E-VIS-003 — Saudi ID typo

```gherkin
Scenario: Invalid Saudi national ID is rejected
  Given the wizard is open with Saudi toggle on
  When the desk fills National ID="0123456789" (starts with 0, not 1)
  And clicks "Register"
  Then the server returns HTTP 400 with ApiResult.Error.Code="ValidationFailed"
  And the bilingual error reads "Saudi national ID must be 10 digits starting with 1."
  And the wizard stays open with the ID field flagged
```

### E2E-VIS-004 — Details with ID image

```gherkin
Scenario: Details modal shows the ID image inline (D-129)
  Given a visitor row exists with HasIdImage=true
  When the admin clicks the Details icon on that row
  Then a modal opens with every profile field rendered in a description list
  And an <img src="/account/api/admin/visitors/{id}/id-document?v={ticks}"> renders inline
  And the image is the decrypted ID photo (AES-GCM at rest)
```

### E2E-VIS-005 — Cross-kind id security guard

```gherkin
Scenario: Other-typed id on the visitors profile endpoint returns 404
  Given an Other-typed user with id O1 exists
  When the admin GETs /account/api/admin/visitors/O1/profile
  Then the server returns HTTP 404 with ApiResult.Error.Code="NotFound"
  And the response shape is identical to an unknown-id 404 (no enumeration leak)
```

### E2E-VIS-006 — Bulk-delete

```gherkin
Scenario: Bulk-delete visitors with reason
  Same shape as E2E-USR-002 but on /admin/visitors.
  Audit rows are Admin.VisitorDeleted.
```

### E2E-VIS-007 — Export

```gherkin
Scenario: Export selected visitors to XLSX
  Same shape as E2E-USR-006 but on /admin/visitors.
```

### E2E-VIS-008 — RTL wizard

```gherkin
Scenario: Walk-in wizard mirrors correctly in Arabic
  Given the wizard is open in English
  When the desk clicks "العربية" outside the modal first
  And reopens "+ Add"
  Then the wizard renders RTL
  And all 6 section badges + labels + chips are Arabic
  And the numbered section circles still read 1..6
  And submit button is on the leading edge
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
