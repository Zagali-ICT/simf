# VIP registration — `/admin/visitors/vip`

| | |
|--|--|
| **Route** | `/admin/visitors/vip` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator |
| **Auth** | `[RequirePermission(Visitors.RegisterOnsite)]` + Approved |
| **Pattern** | D-429 (V-2). Hosts the shared `WalkInRegistrationForm` in a new `VipMode` (reuses the full walk-in pipeline; adds the موج fields + a separate VIP photo and restricts the tier picker to VVIP/VIP). |
| **Status** | ✅ Real |
| **Backend** | `POST /account/api/admin/visitors/register-onsite` (the 3 موج fields appended to `AdminWalkInRegistrationRequest`), `POST /account/api/admin/visitors/{id}/vip-photo` (separate VIP photo), plus the existing profile-types / countries / organisations / interests lookups |
| **Source** | [`VipRegistration.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipRegistration.razor) → [`CreateVisitorForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateVisitorForm.razor) → [`WalkInRegistrationForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInRegistrationForm.razor) |
| **E2E** | [`cp-vip-registration.md`](../../tests/e2e/cp-vip-registration.md) (E2E-VIPR-001..007) |
| **Last reviewed** | 2026-06-15 |

## 1. Purpose

Register a **VVIP** or **VIP** guest for the موج (Mawj) welcome-message
integration. The page is the regular on-site walk-in registration form running
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
