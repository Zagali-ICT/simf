# Visitors CRUD — `/admin/visitors`

| | |
|--|--|
| **Route** | `/admin/visitors` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (event desk staff at run-time) |
| **Auth** | `[Authorize(Roles = "Administrator")]` + Approved account |
| **Pattern** | D-117 canonical CRUD (per-kind variant of `UsersList.razor`) |
| **Status** | ✅ Real |
| **Implements use case(s)** | UC-VIS-LIST, UC-VIS-WALKIN-CREATE (D-127), UC-VIS-DETAILS-WITH-ID-IMAGE (D-129), UC-VIS-DELETE, UC-VIS-DUPLICATE, UC-VIS-IMPORT, UC-VIS-EXPORT _(pending UCS)_ |
| **Backend endpoints** | `POST /account/api/admin/visitors/list`, `POST /admin/visitors/register-onsite` (D-127), `GET /admin/visitors/{id}/profile` (D-126), `POST /bulk-delete`, `POST /duplicate`, `POST /export`, `POST /import`; ID-document upload `POST /admin/visitors/{id}/id-document` (D-129); QR lookup `GET /admin/qr-lookup/{qrId}` (D-130 — also reachable via `/admin/print-bag`) |
| **Source file** | [`VisitorsList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VisitorsList.razor) + child [`CreateVisitorForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateVisitorForm.razor) + walk-in wizard [`WalkInRegistrationForm.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInRegistrationForm.razor) (D-127/D-129/D-131; **2026-07-22 SimfFormSection redesign**) + [`WalkInSuccessModal.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/WalkInSuccessModal.razor) + bulk-add [`BulkBadgeGenerator.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/BulkBadgeGenerator.razor) (#10, shared with `/admin/delegates`) |
| **Deep-link fallback** | `/admin/visitors/new` → [`CreateVisitor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateVisitor.razor) |
| **Tests** | `tests/SIMF.Api.Tests/AdminGridVisitorsTests.cs`, `WalkInRegistrationTests.cs`; `tests/SIMF.ControlPanel.Tests/WalkInRegistrationFormTests.cs`, `BulkBadgeGeneratorTests.cs` |
| **Last reviewed** | 2026-07-22 (create-form SimfFormSection redesign + #10 Bulk-add dialog) |

---

## 1. Purpose

`/admin/visitors` is the event-day workhorse. On the day of the forum, exhibition
staff at the registration desk use it to register walk-in visitors face-to-face
(the **Add** modal opens the full walk-in wizard from D-127). Off-day, admins
use it to audit the visitor roster, view full profiles (including the encrypted
ID-document image — D-129), reprint badges (`/admin/print-bag` from D-130),
and export the attendee list to XLSX for reporting.

## 2. Audience + permissions

- **Who can reach it:** Administrator.
- **Walk-in flow:** the Add modal runs the D-127 wizard; the server registers
  the visitor in `Approved` state (no pending queue — staff verified the
  person in-hand) and mints the QR badge in one transaction. The badge is
  rendered server-side as SVG QR in `WalkInSuccessModal` for immediate print.
- **ID-document image:** captured optionally during walk-in and stored AES-GCM
  encrypted (D-129); admin can view it inline on the Details modal. Streamed
  decrypted via `GET /admin/visitors/{id}/id-document` with a freshness query
  param to bust browser cache.
- **Bulk add (#10):** a toolbar **"Bulk add"** button — gated by
  `Visitors.BulkGenerate` (`<AuthorizedAction>`) — opens the shared
  `BulkBadgeGenerator` dialog (batch-builder: profile type + count → Add → list →
  Generate → confirm popup with an optional organiser email). It posts
  `POST /admin/visitors/bulk-generate`, creating anonymous placeholder badges and
  optionally emailing a ZIP of their QRs. The same component powers the
  `/admin/delegates` bulk section.

## 2b. Walk-in wizard — 2026-07-22 professional redesign

`WalkInRegistrationForm` was regrouped from `<fieldset>/<legend>` sections + a
bespoke 3-column scaffold into the house **numbered `SimfFormSection` cards** on
the responsive `simf-form__grid` (SpeakersAddEdit parity). The gender / preferred
language / nationality / Saudi birth-region controls now use `SimfSelect`, the DOB
uses `SimfDatePicker`, and the ID-document / photo inputs use `SimfFileUpload` —
one consistent field shell. **Fields, the `register-onsite` endpoint (+ deferred
ID/avatar/VIP-photo uploads) and all validation are unchanged**; a single wrapping
`<fieldset disabled>` preserves the submit-time lockout. A latent D-648 gotcha
(the bulk confirm-email field missing `ValueExpression`) was fixed in passing.

## 3. Screenshots

| State | File | Captured |
|-------|------|----------|
| Default (with rows) | `docs/screenshots/audit-admin-visitors.png` | 2026-05-28 |
| Walk-in Add wizard (Saudi branch) | `docs/screenshots/walkin-layout-v2-default.png` | 2026-05-28 |
| Walk-in Add wizard (non-Saudi branch) | `docs/screenshots/walkin-layout-v2-nonsaudi.png` | 2026-05-28 |
| Walk-in success modal + QR badge | _to capture_ | — |
| Details modal with ID-document image | `docs/screenshots/visitor-profile-v2-with-qr.png` | 2026-05-28 |
| Bulk-delete reason modal | _to capture_ | — |
| Empty state | _to capture_ | — |
| RTL | _to capture_ | — |

## 4. UI affordances

Identical canonical toolbar to `admin-admins.md` §4.2; the **Add** modal,
however, hosts the **D-127 walk-in registration wizard** instead of the slim
`CreateAdminForm`:

- Sections (numbered badges per D-131):
  1. **Badge type** — colour-coded profile-type tile picker (driven by D-115 ProfileType).
  2. **Identity** — Name on badge + Date of birth first row, full English/Arabic names second, Place of birth full-width.
  3. **Nationality and ID** — Saudi/Non-Saudi toggle; Saudi → 10-digit national ID (regex `^1\d{9}$`); Non-Saudi → country picker + Iqama (`^2\d{9}$`) **or** Passport (≤20 chars) sub-picker.
  4. **Contact** — Saudi mobile (`+9665XXXXXXXX`) or international mobile + optional email (placeholder `walkin-{guid}@simf.local` if blank).
  5. **ID document** — optional PNG/JPEG/WebP upload, AES-GCM encrypted at rest.
  6. **Interests** — chip multi-select (≤10, Visitor only).
- Submit → `WalkInSuccessModal` shows the printed badge with SVG QR + Done / Print / Register-another.

**Edit form — Photo & ID (2026-07-21, VIP edit).** The shared
`EditAccountForm` now renders a **Photo & ID** section below the
email / display-name / tier fields: a profile-photo input (with the current
avatar previewed when on file) and an ID-document input (with the current ID
image previewed). On Save, the core fields PUT first, then each picked image
uploads to its account-id-keyed endpoint (`/admin/visitors/{id}/avatar`,
`/id-document`, both `Visitors.Edit`-gated). The VVIP/VIP welcome-photo input
is scoped out here (`ShowVipPhoto=false`) — it appears only when the same form
is opened from the VIP desk (`/admin/vips`). See
[`cp/admin-vips.md`](admin-vips.md).

**D-353 presentation toggle / D-356 Excel.** The toolbar now carries the
`CrudPresentationToggle` (Page ↔ Popup): the Add walk-in wizard, the Edit
account form, and the read-only Details form are hosted by the centralized
`CrudShell`, which frames them as a dialog (default) or as a full page per the
admin's choice. The choice persists per-admin in `localStorage`
(`simf.cp.prefs.visitors`, read on init via `CpPreferences.GetPresentationAsync`).
The D-356 "Uniform CRUD" **Excel** treatment is **N/A as a new addition here** —
this account page already shipped its own Excel export/import (toolbar
Export/Import → `/account/api/admin/visitors/export` | `/import`, with the
visitor-specific import-result modal) and does **not** use the generic
`CrudGridExcel` component; that surface is unchanged and stays covered by
E2E-VIS-013/014/015. Visitor delete remains the reason-gated **bulk-delete**
dialog (there is no single-row hard delete), so the View/Delete form is
details-only — it never renders a Delete button or a `SimfConfirm` gate.

## 5. Data flow (walk-in)

```
Desk staff clicks +Add
  → _addOpen
  → <SimfModal><CreateVisitorForm Kind="Visitor"><WalkInRegistrationForm></...>
  → 6 sections filled
  → simfAccount.postJson("/account/api/admin/visitors/register-onsite", AdminWalkInRegistrationRequest)
  → API: POST /api/v1/admin/visitors/register-onsite
  → AdminAccountService.RegisterOnSiteAsync:
      transaction:
        create SimfUser (AccountState = Approved)
        create UserProfile (every field)
        link interests
        QrIdMinter.MintAsync → Approved QR badge
        audit Admin.WalkInRegistered
      commit
  → ApiResult<AdminWalkInRegistrationResponse> { UserId, QrId, … }
  → OnSuccess(response) → CreateVisitorForm shows <WalkInSuccessModal>
  → (optional) upload ID document POST /admin/visitors/{id}/id-document
  → staff clicks Print → window.print() with @media print CSS isolating the badge
```

## 6. Validation + error handling

- **Server-side:** `AdminWalkInRegistrationRequestValidator` enforces every
  rule (regex on Saudi ID + Iqama, NationalityCode 2–3 ISO 3166, mobile
  required, interests ≤ 10, profile-type kind matches Visitor route).
- **Client-side:** mirrors the regex + length checks for UX; server is canonical.
- **Cross-kind smuggle attempt** (e.g. Other profile-type on `/visitors/register-onsite`)
  → 400 `AdminProfileTypeInvalid`.
- **Duplicate email** → 409 `EmailAlreadyExists`.
- **No interest IDs** is fine — the field is optional.

## 7. Edge cases + known limitations

- **No email at the desk** — the server synthesizes `walkin-{guid}@simf.local`
  so Identity has something to anchor. The QR badge is the operative access
  key; the email is internal-only.
- **ID-document upload failure** — silent best-effort; surfaces as
  `HasIdImage = false` on the Details modal. The registration itself stays
  committed.
- **Cross-kind exposure** — `/admin/visitors/{id}/profile` returns 404 for
  Other-typed ids (per D-124's load-bearing 404-for-all-mismatch rule).
- **Reprint after walk-in** — staff uses `/admin/print-bag` (D-130) instead
  of re-doing the walk-in.

## 8–9. i18n + accessibility

Identical canonical shape — see [`admin-interests.md`](admin-interests.md) §8–9.

## 10. Related use cases

| UC ID | Title |
|-------|-------|
| UC-VIS-LIST | List + filter + sort visitors |
| UC-VIS-WALKIN-CREATE | Register a walk-in visitor on-site |
| UC-VIS-DETAILS-WITH-ID-IMAGE | View full profile + ID document inline |
| UC-VIS-DUPLICATE | Duplicate visitor with new email |
| UC-VIS-DELETE | Delete with reason |
| UC-VIS-IMPORT | Bulk-import visitors from XLSX |
| UC-VIS-EXPORT | Export visitors to XLSX |

## 11. Related E2E test scenarios

| Scenario | ID | Coverage |
|----------|----|----------|
| Walk-in Saudi → Approved + QR minted + badge printable | E2E-VIS-001 | D-127 golden |
| Walk-in non-Saudi Passport → Approved | E2E-VIS-002 | D-127 branch |
| Walk-in Saudi ID typo → server 400 + form error | E2E-VIS-003 | validation |
| Details modal renders ID image | E2E-VIS-004 | D-129 |
| Cross-kind id on `/admin/visitors/{otherId}/profile` → 404 | E2E-VIS-005 | D-124 security |
| Bulk-delete with reason → toast + reload | E2E-VIS-006 | bulk |
| Export selected → XLSX downloads | E2E-VIS-007 | export |
| RTL → wizard mirrors correctly | E2E-VIS-008 | i18n |
| Name column shows the visitor's profile-photo thumbnail (initials fallback when none) | E2E-VIS-028 | D-568 photo-in-list |

## 12. Related docs

- Manual: `Admin-Manual.md § 10.5 Visitors` _(pending)_
- Pattern: `SIMF_TABLE_PATTERN.md` + `SIMF-FDS-002 Registration-and-Approval`
- Source: see top of file.
- Decisions: D-114 (canonical adoption), D-127 (walk-in), D-128 (approve-with-review), D-129 (ID validation + image), D-131 (layout polish), D-132 (banner / multiselect sweep — no-op on this page, already canonical).

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| 2026-05-26 | D-114 | Adopted canonical D-117 pattern. |
| 2026-05-28 | D-127 | Full walk-in registration wizard replaces the 2-field stub. |
| 2026-05-28 | D-128 | Approve-with-review on Pending visitors (sibling page). |
| 2026-05-28 | D-129 | Saudi ID regex, non-Saudi Iqama/Passport sub-picker, ID image inline. |
| 2026-05-28 | D-131 | Identity field reorder + numbered section badges + bigger tiles. |
| 2026-06-10 | D-353 / D-356 | Add/Edit/Details moved into `CrudShell` with a Page↔Popup `CrudPresentationToggle` (persisted as `simf.cp.prefs.visitors`). Uniform-CRUD Excel is N/A here — the page keeps its existing visitor-specific Excel export/import (not `CrudGridExcel`); delete stays the reason-gated bulk dialog (View/Delete is details-only). |
| 2026-07-09 | D-728 | **Change type (owner item 9).** The Details view (`VisitorsViewDelete`) now hosts a shared `ChangeAccountTypeBlock` (gated `Accounts.ChangeType`) that flips the visitor into a partner (Other) type via `POST /admin/accounts/{id}/change-type`. The dropdown lists only active partner-scope types (opposite scope); the flip rolls the security stamp + revokes sessions (a partner type may grant Staff/Moderator app perms) and keeps the approval state. E2E-VIS-026. |
| 2026-07-14 | D-568 | **Photo thumbnail in the list.** The name column now renders the shared `SimfIdentityCell` — the visitor's profile-photo thumbnail (streamed from `/account/api/admin/visitors/{id}/avatar` when `AdminUserSummary.HasAvatar`, the `AvatarRelativePath` presence sentinel) or an initials tile when there is no photo (never a broken image) — matching the Speakers/Sponsors/Media-Partners lists. Column stays `Key="displayName"` so server-side sort/filter is unchanged. E2E-VIS-028. |
| 2026-07-26 | B22 | **Nationality on the Edit form.** `UserProfile.NationalityId` was written only by the self-service sign-up upsert, so no admin path could correct it — yet it decides which delegation an account belongs to and therefore gates delegation-meeting confirm/decline. `AdminUpdateVisitorRequest` / `AdminUpdateOtherRequest` gained an **optional** `NationalityCode` (ISO alpha-2, the same wire shape as `AdminUserProfileView.NationalityCode`); `EditAccountForm` renders a country picker prefilled from the profile with a helper note, and an empty pick sends nothing so the stored value is untouched. An unknown / inactive code is the same 400 `PROFILE_NATIONALITY_UNKNOWN` the self-service path raises, thrown before any write. Permission gate unchanged. E2E-VIS-032. |

---

_Last reviewed:_ 2026-07-26 by Claude (B22 — nationality on the Edit account form). Earlier: 2026-07-14 by Claude (D-568 — profile-photo thumbnail in the list). Earlier: 2026-07-09 by SIMF Team (D-728 — change-account-type action); 2026-06-10 (D-356 Phase 5 — D-353 Page↔Popup toggle note; Excel N/A for this account page).
