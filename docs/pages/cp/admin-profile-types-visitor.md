# Visitor profile types — `/admin/profile-types/visitor`

| | |
|--|--|
| **Route** | `/admin/profile-types/visitor` |
| **Audience** | Administrator |
| **Pattern** | D-117 + D-132 canonical CRUD (admin-managed lookup table — no deep-link fallback per D-118). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/profile-types?userType=Visitor` (list), `POST /admin/profile-types` (create — body carries `UserType`), `PUT /admin/profile-types/{id}`, `DELETE /admin/profile-types/{id}` |
| **Source** | [`VisitorProfileTypesList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/VisitorProfileTypesList.razor) + reusable child [`ProfileTypeForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ProfileTypes/ProfileTypeForm.razor) |
| **Last reviewed** | 2026-05-28 |

## 1. Purpose

Visitor profile types drive the **colour-coded tile picker** in the walk-in
registration wizard (D-127). Each row has a bilingual name, a `PageColor`
(hex / CSS variable; rendered as a swatch on the wizard tile), and an
Active flag. Adding a new profile-type immediately makes it available at
the registration desk.

## 4.2 Toolbar

Identical canonical D-117/D-132 shape. Add + Edit modals host
`ProfileTypeForm.razor` (Initial=null vs Initial=row drives create vs
update). Details + Delete (soft-delete via Deactivate) per row.

## 4.5 Form fields (Add + Edit modal)

| Field | Type | Required | MaxLength | Notes |
|-------|------|----------|-----------|-------|
| Name (English) | text | yes | 128 | unique per UserType |
| Name (Arabic) | text | yes | 128 | |
| PageColor | text + `<input type="color">` paired swatch (D-120) | yes | 32 | accepts `#rrggbb`, 3-digit hex, `var(--brand-blue)` CSS variables |
| Active | checkbox | no | — | Edit-only |
| Show in the app sign-up picker | checkbox | no | — | default on; off = a CP-only type an admin assigns rather than a customer picks |
| VIP tier | checkbox | no | — | default OFF; audience form only. See below. |

**VIP tier.** Ticking it marks the type as a VIP audience tier, which does two
things and only two: its holders may **self-reserve a VIP-tier seat**, and the
app reports them as `isVip`. It does **not** grant a meeting — speaker-meeting
eligibility is the per-user `AllowsSpeakerMeeting` flag on the account, set from
the account edit form, not from here (D-760).

The checkbox exists because the flag previously had no admin write path at all:
the identity seeder was the only writer anywhere, so it could only ever be true
on the seeded VVIP and VIP rows, and a type created from this page could never
be VIP however the form was filled. It is deliberately absent from the Other
(partner) form — a Sponsor or Exhibitor type is never a VIP tier.

PageColor uses the paired text+swatch (D-120): the text is the source of
truth (accepts the full free-text contract), the swatch is a visual
shortcut that writes `#rrggbb` back. When the text isn't a canonical
6-hex value, the swatch falls back to navy `#244A77` for display.

## 7. Edge cases

- **In-use deletion** — deactivating a profile-type that's already linked
  to visitor profiles is allowed; existing visitors keep the link, new
  walk-ins don't see the deactivated type in the tile picker. To delete
  permanently, no visitor must reference it — server returns 409
  `ProfileTypeInUse` if any do. The bilingual error surfaces verbatim.
- **Duplicate name within the same UserType** → 409.
- **UserType pinned** — this page filters to `UserType = Visitor`; the form
  passes the same UserType when creating so a Visitor profile-type can't
  smuggle into the Other pool.

## 11. E2E

| Scenario | ID |
|----------|----|
| Add → tile appears in walk-in wizard | E2E-VPT-001 |
| Edit name + color → wizard picks up new color | E2E-VPT-002 |
| Deactivate in-use → 409 `ProfileTypeInUse` toast (bilingual) | E2E-VPT-003 |
| Cross-UserType id rejection | E2E-VPT-004 |
| VIP tier: create ticked, survives an unrelated edit, can be cleared, absent on the Other form | E2E-VPT-015 |

## 12. Related

- Per-page CP documentation set (4-aspect + README, D-380):
  [`../../CP/admin-profile-types-visitor/README.md`](../../CP/admin-profile-types-visitor/README.md)
  (Function / Logic / API / Design).
- Sibling: [`admin-profile-types-other.md`](admin-profile-types-other.md)
- Consumer: [`admin-visitors.md`](admin-visitors.md) walk-in wizard
- Decisions: D-115 (backend), D-118 (CP pages), D-120 (PageColor picker).

_Last reviewed:_ 2026-08-14 by Claude — added the **VIP tier** toggle (the flag
had no admin write path; the seeder was its only writer) and documented the two
form checkboxes the field table had been missing. Prior: 2026-05-28 (D-133 slice 3).
