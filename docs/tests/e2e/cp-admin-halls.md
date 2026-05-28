# E2E test catalogue — Halls & seating (`/admin/halls`)

| | |
|--|--|
| **Page** | [`cp/admin-halls.md`](../../pages/cp/admin-halls.md) |
| **Authored** | D-134 Sprint B / D-135 (2026-05-29) |
| **Last reviewed** | 2026-05-29 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-HAL-001 | Create hall golden | P0 |
| E2E-HAL-002 | Duplicate code → 409 HallCodeDuplicate | P0 |
| E2E-HAL-003 | Edit hall | P1 |
| E2E-HAL-004 | Deactivate hall | P1 |
| E2E-HAL-005 | Capacity = 0 accepted | P2 |
| E2E-HAL-006 | Auth: non-admin → /not-permitted | P0 |
| E2E-HAL-007 | RTL render | P1 |

## Scenarios

### E2E-HAL-001 — Create golden

```gherkin
Scenario: Administrator creates a hall
  Given an Administrator is signed in on /admin/halls (empty)
  When they click "Add hall"
  And fill Code="H1", Name="Main Auditorium", Name (Arabic)="القاعة الرئيسية",
      Capacity="500", Floor="Ground"
  And click "Create hall"
  Then POST /admin/halls returns 200
  And a toast reads "Hall \"Main Auditorium\" was created."
  And the grid shows the new row with the green Active pill
  And audit Hall.Created is written
```

### E2E-HAL-002 — Duplicate code

```gherkin
Scenario: Duplicate code returns 409
  Given a hall with Code="H1" exists
  When the admin submits a new hall with Code="h1"
  Then HTTP 409 + ApiResult.Error.Code="HALL_CODE_DUPLICATE"
  And the modal stays open with the bilingual error
```

### E2E-HAL-003 — Edit

```gherkin
Scenario: Edit hall capacity
  Given a hall "Main Auditorium" with Capacity=500 exists
  When the admin Edits and changes Capacity to 600
  And clicks Save changes
  Then PUT returns 200; grid shows Capacity=600; audit Hall.Updated
```

### E2E-HAL-004 — Deactivate

```gherkin
Scenario: Deactivate a hall
  Given an active hall exists
  When the admin clicks Deactivate
  Then DELETE returns 200; status pill flips to Inactive; audit Hall.Deactivated
```

### E2E-HAL-005 — Capacity 0

```gherkin
Scenario: Capacity = 0 is accepted (placeholder hall)
  When the admin creates a hall with Capacity="0"
  Then POST returns 200 and the row shows 0
```

### E2E-HAL-006 — Auth

```gherkin
Scenario: Non-admin denied
  Given a Visitor account signed in
  When they navigate to /admin/halls
  Then they land on /not-permitted
```

### E2E-HAL-007 — RTL

```gherkin
Scenario: RTL render
  Given the admin toggles "العربية"
  Then the banner reads "القاعات والمقاعد"
  And the form labels + column headers are Arabic
```

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint B / D-135).
