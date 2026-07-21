# VIP registration — `/admin/visitors/vip`

| | |
|--|--|
| **Route** | `/admin/visitors/vip` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(Visitors.RegisterOnsite)]` + Approved. **New VIP** gated by `Visitors.RegisterOnsite`, per-row **Edit** by `Visitors.Edit` (API enforces the same). |
| **Pattern** | VIP/VVIP **list page** (2026-07-21 — a copy of the visitor page) over the `/admin/vips/list` subset, with a toolbar **New VIP** that opens the D-429 registration wizard (`WalkInRegistrationForm` in `VipMode`) and a per-row **Edit** hosting the shared `EditAccountForm` (`ShowVipPhoto=true`). |
| **Status** | ✅ Real |
| **Backend** | List: `POST /account/api/admin/vips/list`. New VIP: `POST /account/api/admin/visitors/register-onsite` (the 3 موج fields on `AdminWalkInRegistrationRequest`) + `POST /account/api/admin/visitors/{id}/vip-photo`. Edit (reused, no new endpoint): `PUT /account/api/admin/visitors/{id}` + `POST .../{id}/avatar` \| `/id-document` \| `/vip-photo` (all `Visitors.Edit`-gated). Plus the profile-types / countries / organisations / interests lookups. |
| **Source** | [`VipRegistration.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipRegistration.razor) → grid + [`CreateVisitorForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateVisitorForm.razor) (New VIP) + [`EditAccountForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/EditAccountForm.razor) (Edit) |
| **E2E** | [`cp-vip-registration.md`](../../tests/e2e/cp-vip-registration.md) (E2E-VIPR-001..011) |
| **Last reviewed** | 2026-07-21 |

## 1. Purpose

The **VIP Registration desk** — a copy of the visitor list page scoped to the
VIP guests (VVIP / VIP), added 2026-07-21. The grid lists the VIP accounts
(name / job title / profile type / email from `/admin/vips/list`). Two actions:

- **New VIP** (toolbar, gated `Visitors.RegisterOnsite`) opens the VVIP/VIP
  registration wizard described below (the D-429 flow) as a full-width section;
  on success it toasts "VIP registered." and reloads the grid.
- **Edit** (per row, gated `Visitors.Edit`) opens the shared `EditAccountForm`
  with `ShowVipPhoto=true` — change name / email / **tier** (promote VIP↔VVIP) /
  profile photo / ID image / VIP welcome photo. Reuses the account-id-keyed admin
  endpoints; no new endpoint or permission.

### New VIP — the D-429 registration wizard

Register a **VVIP** or **VIP** guest for the موج (Mawj) welcome-message
integration. The wizard is the regular on-site walk-in registration form running
in **VIP mode**:

- The profile-type picker is **restricted to VVIP / VIP** (the seeded
  audience-side tiers); when only one is seeded it is preselected.
- A **VIP details (موج)** section captures the welcome-message fields, all
  optional: **Mawj system ID** (المعرف في نظام موج, ≤64), **Honorific / title**
  (اللقب, e.g. "Minister", ≤64), **Preferred language** (اللغة المفضلة, ar/en).
  **Job title** (already on the form) doubles as the VIP job title.
- A **separate VIP welcome photo** (صورة واضحة) — a clear high-resolution
  portrait distinct from the account avatar — is uploaded after create to
  `…/vip-photo` (PNG/JPEG/WebP, ≤2 MB, MIME + magic-byte gated).

Everything else (identity, nationality + ID document, contact, organisation,
interests) is the standard walk-in capture. The account is created
**`PendingApproval` with no QR** (D-425) and is approved from the **existing**
pending-visitors queue (`/admin/visitors/pending`), which mints the QR (D-386).

## 2. Permission

Gated by `Visitors.RegisterOnsite` — the VIP page is on-site visitor
registration, so it reuses the walk-in capability rather than inventing a
permission that maps to no distinct API action. The `…/vip-photo` endpoints are
gated `Visitors.Edit` (upload) / `Visitors.View` (read), mirroring the avatar +
ID-document admin endpoints.

## 3. Data

The 3 موج text fields persist to additive nullable `UserProfile` columns
(`MawjId`, `Honorific`, `PreferredLanguage`); the photo path persists to
`VipPhotoRelativePath` (separate VIP-photo store). See decision **D-429**.

## 4. Tests

Unit/integration: `WalkInRegistrationTests.Visitor_walk_in_persists_vip_fields`,
`…Admin_uploads_vip_photo_sets_path`; seeding in
`IdentitySeederTests.SeedAsync_seeds_the_VVIP_and_VIP_visitor_tiers`. E2E
catalogue: `cp-vip-registration.md`.
