# E2E test catalogue — Themes & pillars (`/admin/themes`)

| | |
|--|--|
| **Page** | [`cp/admin-themes.md`](../../pages/cp/admin-themes.md) |
| **Authored** | D-134 Sprint B / D-135 (2026-05-29) |
| **Last reviewed** | 2026-05-29 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-THM-001 | Create theme golden | P0 |
| E2E-THM-002 | Duplicate code → 409 ThemeCodeDuplicate | P0 |
| E2E-THM-003 | Edit theme | P1 |
| E2E-THM-004 | Deactivate theme (soft-delete) | P1 |
| E2E-THM-005 | Details modal renders all fields | P1 |
| E2E-THM-006 | Auth: non-admin → /not-permitted | P0 |
| E2E-THM-007 | RTL render | P1 |

## Scenarios

### E2E-THM-001 — Create golden

```gherkin
Scenario: Administrator creates a programme theme
  Given an Administrator is signed in on /admin/themes (empty list)
  When they click "Add theme"
  And the Add modal opens with Code, Name, Name (Arabic), Description (EN/AR),
      Display order, Page color fields (8 fields incl. helper text)
  And they fill Code="DEF", Name="Defence", Name (Arabic)="الدفاع",
      Display order="10", Page color="#244A77"
  And click "Create theme"
  Then POST /admin/themes returns 200
  And the modal closes
  And a green toast reads "Theme \"Defence\" was created."
  And the grid shows a new row Code="DEF", Active pill
  And the audit log records Theme.Created with the actor
```

### E2E-THM-002 — Duplicate code

```gherkin
Scenario: Duplicate code returns 409
  Given a theme with Code="DEF" exists
  When the admin opens Add and submits Code="def" (case-insensitive clash)
  Then HTTP 409 with ApiResult.Error.Code="THEME_CODE_DUPLICATE"
  And the bilingual server message surfaces in the modal SimfAlert
  And the modal stays open
```

### E2E-THM-003 — Edit

```gherkin
Scenario: Edit an existing theme
  Given a theme "Defence" exists
  When the admin clicks Edit on its row
  And changes Display order=5 + Page color="#FFD700"
  And clicks "Save changes"
  Then PUT /admin/themes/{id} returns 200
  And the toast reads "Theme \"Defence\" was updated."
  And the grid reflects the new order + color
```

### E2E-THM-004 — Deactivate

```gherkin
Scenario: Deactivate (soft-delete) a theme
  Given an active theme exists
  When the admin clicks the Deactivate icon
  Then DELETE /admin/themes/{id} returns 200
  And the row's Status pill flips from Active (green) to Inactive (grey)
  And audit Theme.Deactivated is written
```

### E2E-THM-005 — Details

```gherkin
Scenario: Details modal shows every field
  Given a theme with Description (EN/AR) populated exists
  When the admin clicks Details
  Then the modal renders Code, Name, Name (Arabic), Description,
       Description (Arabic), Order, Color, Active in a simf-dl
  And missing optional fields show "—"
```

### E2E-THM-006 — Auth gate

```gherkin
Scenario: Non-admin user denied
  Given a Visitor account is signed in
  When they navigate to /admin/themes
  Then they land on /not-permitted with HTTP 200
```

### E2E-THM-007 — RTL

```gherkin
Scenario: Arabic toggle mirrors page + modal
  Given the admin is on /admin/themes
  When they click "العربية"
  Then <html dir="rtl" lang="ar"> is set
  And the banner reads "المحاور والمواضيع"
  And the toolbar + column headers flip
  And the Add modal renders RTL with Arabic field labels + helper text
```

_Last reviewed:_ 2026-05-29 by Claude (D-134 Sprint B / D-135).
