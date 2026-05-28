# E2E test catalogue — Visitor profile types (`/admin/profile-types/visitor`)

| | |
|--|--|
| **Page** | [`cp/admin-profile-types-visitor.md`](../../pages/cp/admin-profile-types-visitor.md) |
| **Last reviewed** | 2026-05-28 |

## Coverage matrix

| ID | Scenario | Priority |
|----|----------|----------|
| E2E-VPT-001 | Add → tile appears in walk-in wizard | P0 |
| E2E-VPT-002 | Edit name + PageColor → wizard picks up new color | P1 |
| E2E-VPT-003 | Deactivate in-use → 409 ProfileTypeInUse (bilingual) | P0 |
| E2E-VPT-004 | Cross-UserType id rejected (Other id on Visitor route) | P0 |

## Scenarios

### E2E-VPT-001 — Add

```gherkin
Scenario: Add a Visitor profile-type
  Given an admin is signed in on /admin/profile-types/visitor
  When they click "+ Add"
  And fill Name (English)="VIP", Name (Arabic)="كبار الشخصيات"
  And set PageColor via the D-120 paired text+swatch to "#FFD700"
  And click "Create"
  Then the row appears with the gold pill stripe in the Color column
  When they open /admin/visitors and click "+ Add"
  Then the walk-in wizard section 1 shows a new "VIP" tile with the gold stripe
```

### E2E-VPT-002 — Edit

```gherkin
Scenario: Edit name + PageColor; wizard reflects the change
  Given a Visitor profile-type "VIP" exists with color #FFD700
  When the admin edits it to PageColor=#1E90FF
  Then the row + the walk-in wizard tile render with the new color
```

### E2E-VPT-003 — Deactivate in-use → 409

```gherkin
Scenario: Cannot delete a profile-type that is in use
  Given Visitor profile-type "General" is linked to at least one Visitor profile
  When the admin clicks "Deactivate" on "General"
  Then the server returns HTTP 409 + ApiResult.Error.Code="ProfileTypeInUse"
  And the toast surfaces the bilingual server message verbatim
  And the row stays Active in the grid
```

### E2E-VPT-004 — Cross-UserType guard

```gherkin
Scenario: An Other profile-type id on the Visitor edit endpoint is rejected
  Given an Other profile-type "Sponsor staff" with id PT-O1 exists
  When the admin PUTs /account/api/admin/profile-types/PT-O1 with UserType=Visitor
  Then the server returns HTTP 404 + ApiResult.Error.Code="NotFound"
  And no row is updated
```

_Last reviewed:_ 2026-05-28 by Claude (D-133 follow-up).
