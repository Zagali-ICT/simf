# Staff — create visitor profile (إنشاء ملف زائر) — mobile `/staff/register-visitor`

| Field | Value |
|---|---|
| Route | `RouteNames.staffRegisterVisitor` · `/staff/register-visitor` (app screen #114) · reached from the staff-only **More** drawer entry |
| Surface | Mobile (Flutter) — **tablet two-column**, phone single-column |
| Screen | `lib/features/staff/register_visitor_screen.dart` (`StaffRegisterVisitorScreen`) |
| Figma node | `1467:12357` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; iPad Pro 12.9″ 1024×1314; D-509) |
| Shell | Custom navy `Scaffold` — hand-rolled tablet header (`_buildHeader`: circular back + gold globe + forum crest, larger than the shared `SimfFormScaffold` per this frame) over the beige "Input" card |
| Providers | `profileRepositoryProvider` (`getCountries` / `getProfileTypes(isVisitor:true)` / `searchOrganisations`) · `staffRepositoryProvider` (`registerVisitor`, `uploadIdImage`, `uploadAvatar`) · `localeControllerProvider` (globe toggle) |
| Role / gate | App: `AppRole.staff`+ (router role-gate). Server: `Visitors.RegisterOnsite` permission + `RequireApprovedAccount` (403 otherwise) |
| Tests | `test/features/staff/register_visitor_screen_test.dart` (widget, 5 cases) · golden `test/golden/staff_register_visitor_golden_test.dart` (`goldens/staff_register_visitor_1467-12357.png`) · E2E [`mobile-staff-register-visitor.md`](../../../tests/e2e/mobile-staff-register-visitor.md) |
| Status | ✅ Real — D-509 (Figma frame 1467:12357, PendingApproval walk-in, D-425) → **clean-code reviewed + frozen (D-559, 2026-06-30)** |

## 1. Purpose
Staff at the exhibition register a **walk-in** visitor on a tablet. The form posts
`POST /app/staff/visitors/register-onsite` (the same on-site provisioning service
the CP desk uses), which creates a **PendingApproval** visitor with **no QR** — an
admin approves it from the pending-visitors queue, which mints the badge (D-425).
The optional ID-document image + personal photo are uploaded **after** the create
by the new visitor's id; a failed upload does not undo the (already-created)
registration.

## 2. Audience & access
Staff-only. The **More** drawer shows the "تسجيل زائر" entry only when
`routeAllowsRole(staffRegisterVisitor, role)` is true; the router redirects a
non-staff user home. The server independently enforces `Visitors.RegisterOnsite`.

## 3. UI & behaviour (top → bottom)
A two-column tablet form (single column < 600px) — deliberately **uncapped** width
(the §13.7 exception for a wide tablet form; `MaxWidthBody(560)` would force single
column on the tablet).
1. **Navy header** (`_buildHeader`, Figma 1467:12565) — forced-LTR circular back
   chevron + gold globe language toggle; centred forum title + crest.
2. **Beige "Input" card** — title "إنشاء ملف زائر" + person-avatar tile.
3. **Two-column field grid** (`_twoCol`; RTL: first arg → right column):
   email | phone · Arabic name | English name · gender toggle | nationality ·
   document section (Saudi → national-ID field; non-Saudi → الإقامة/جواز toggle +
   document number) · job title | **Arabic job title (المسمى الوظيفي بالعربية,
   optional, backlog #37)** · organisation (own full-width row) · two attachment
   pickers.
4. **Terms link** "الموافقة على الشروط والأحكام؟" → terms screen.
5. **Gold CTA "التالي"** — full-width, h56; busy spinner while posting.
On success: a bilingual "تم تسجيل الزائر — بانتظار الاعتماد" SnackBar + the form
resets for the next walk-in.

## 4. Data / API (wire contract D-219 frozen)
- `StaffWalkInRequest.toJson` → `POST /app/staff/visitors/register-onsite`
  (mirrors the backend `AdminWalkInRegistrationRequest`). Backlog #37 added the
  optional `jobTitleArabic` key (additive, D-219-safe — only sent when filled);
  the backend `AdminWalkInRegistrationRequest.JobTitleArabic` already existed.
- `StaffWalkInResult.fromJson` ← the response (empty `qrId` = PendingApproval).
- Optional `…/{id}/id-document` + `…/{id}/avatar` multipart uploads.
- The **classification (ProfileType)** is not in the frame → auto-assigned to the
  seeded "Normal" audience tier (server re-validates).

## 5. Validation & edge cases
- Light client checks (the server re-validates the full rule set): Arabic + English
  name required (filtered to the right script); phone required + standard
  Saudi/international shape; document number required; nationality + organisation +
  gender selected. Empty/invalid submit → a "أكمل بيانات الزائر المطلوبة" SnackBar,
  **no** request sent.
- Every text input declares a `maxLength` so over-long input can't reach the server.
- Switching nationality clears the stale national-ID / document number.
- Lookup load failure → an error + **Retry** surface.
- A server 400/403 → the bilingual server message in a SnackBar; the form keeps its
  values.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`. The header
back/globe row is forced LTR so the chevron + globe sides match the frame under RTL.
Brand font applied once in the theme.

## 7. Testing
- **Widget** (`register_visitor_screen_test.dart`, 5 cases): renders after the
  lookups load, the load-failure Retry surface, the empty-submit guard (no API
  call), every input caps its length, a filled form posts the walk-in payload
  (Saudi path → `saudiMobile`, `isSaudi`, org + Normal profile type, plus the
  optional `jobTitleArabic`, #37) and shows the pending-approval toast.
- **Golden** (`staff_register_visitor_golden_test.dart`):
  `goldens/staff_register_visitor_1467-12357.png` @1024×1314 RTL (Saudi default,
  empty state) — locks the frozen two-column tablet parity. `pumpAndSettle` is safe
  (the eager lookup futures resolve; no periodic timer).
- **E2E**: [`docs/tests/e2e/mobile-staff-register-visitor.md`](../../../tests/e2e/mobile-staff-register-visitor.md)
  (E2E-MOBSTAFFREG-001..004 + backend `WalkInRegistrationTests.cs`).

## 8. Clean-code DoD (D-559 freeze — 2026-06-30)
- [x] Already clean: `SimfTokens` direct (0 raw `Color(0x…)`), no `Ksa*`, strings
      via `AppL10n`, repo-backed, lazy (no list) — **no behaviour/render change**
- [x] Structural: extracted the ~90-line inline navy header to `_buildHeader`;
      added the §9 structured doc tail
- [x] **Kept local** `_FieldLabel` / `_Pill` / `_decoration` + `_inputStyle` — the
      frame's tablet typography (22px end label, 18px filled inputs, segmented
      gold/navy toggle) differs from the shared 12–14px start-aligned primitives;
      swapping would change a render already at D-509 parity
- [x] **Body uncapped** (no `MaxWidthBody`) — deliberate §13.7 exception for the
      two-column tablet form (`LayoutBuilder ≥600`)
- [x] Figma `1467:12357` parity re-verified (golden vs frame, all rows incl.
      attachments) + locked by the new golden
- [x] widget + golden tests + this doc + E2E catalogue + PAGE-INDEX, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (`StaffWalkInRequest`/`StaffWalkInResult`, D-219) unchanged

## 9. Changelog
- **2026-07-22 (backlog #37):** added the optional Arabic job title input
  (`المسمى الوظيفي (بالعربية)`) paired beside the job title; organisation moved to
  its own full-width row. Adds the additive `jobTitleArabic` key to
  `StaffWalkInRequest.toJson` (only sent when filled — the backend already carried
  `AdminWalkInRegistrationRequest.JobTitleArabic`). Golden
  `staff_register_visitor_1467-12357.png` re-locked (owner-approved); the filled-form
  widget case now asserts `jobTitleArabic`. Wire contract otherwise unchanged.
- **2026-06-30 (Phase 3, D-559):** folded into the clean-code program (owner request);
  reviewed + frozen. Extracted `_buildHeader`; added the §9 doc tail; added the
  `1467:12357` render-lock golden + this per-page doc. No behaviour/render change.
- **D-509:** built to the KSA frame 1467:12357 (staff walk-in, PendingApproval / D-425).
