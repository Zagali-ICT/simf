# Staff — create visitor profile (إنشاء ملف زائر) — mobile `/staff/register-visitor`

| Field | Value |
|---|---|
| Route | `RouteNames.staffRegisterVisitor` · `/staff/register-visitor` (app screen #114) · reached from the staff-only **More** drawer entry |
| Surface | Mobile (Flutter) — **tablet two-column**, phone single-column |
| Screen | `lib/features/staff/register_visitor_screen.dart` (`StaffRegisterVisitorScreen`) |
| Figma node | `1467:12357` (KSA-Project, file `PSXHhY0UVTAPSaIOf9uNKd`; iPad Pro 12.9″ 1024×1314; D-509) |
| Shell | Shared `SimfFormScaffold(pinnedHeader: true)` — back chevron + the shared `SimfLanguageToggle` pill + logo/forum-name header over the beige card (BUG-019; the hand-rolled `_buildHeader` was removed) |
| Providers | `profileRepositoryProvider` (`getCountries` / `getProfileTypes(isVisitor:true)` / `searchOrganisations`) · `staffRepositoryProvider` (`registerVisitor`, `uploadIdImage`, `uploadAvatar`). The language toggle is owned by `SimfFormScaffold` |
| Role / gate | App: `AppRole.staff`+ (router role-gate). Server: `Visitors.RegisterOnsite` permission + `RequireApprovedAccount` (403 otherwise) |
| Tests | `test/features/staff/register_visitor_screen_test.dart` (widget, 15 cases) · golden `test/golden/staff_register_visitor_golden_test.dart` (`goldens/staff_register_visitor_1467-12357.png`) · E2E [`mobile-staff-register-visitor.md`](../../../tests/e2e/mobile-staff-register-visitor.md) |
| Status | ✅ Real — D-509 (PendingApproval walk-in, D-425) → clean-code frozen (D-559) → **design-system rebuild (BUG-019, 2026-07-26)** |

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
A two-column tablet form, single column on a **compact** window — the breakpoint is
the shared `WindowSize` API, not an inline `maxWidth >= 600`. The body is wrapped in
`MaxWidthBody` (560 compact / 840 wide) so the card fills a phone and stays a
readable block on a wide tablet panel.
1. **Shared form chrome** (`SimfFormScaffold`, `pinnedHeader: true`) — the
   forced-LTR back chevron + the shared `SimfLanguageToggle` EN/ع pill, then the
   logo + forum-name header. Identical to Create-profile / sign-in.
2. **Beige card** — title "إنشاء ملف زائر" + the navy person-avatar tile.
3. **Two-column field grid** (`_twoCol`; RTL: first arg → right column). Every
   input is a shared `SimfLabeledTextField` / `MobileField` on
   `simfFieldDecoration()` (unfilled), every lookup a shared `SimfPickerField`
   opening the searchable `LookupSearchSheet`:
   **classification (الفئة)** — full-width, operator-picked, seeded to the
   "Normal" tier · Arabic name | English name · gender pills | nationality ·
   document section (Saudi → national-ID field; non-Saudi → الإقامة/جواز
   `BeigeTabs` + document number) · job title | **Arabic job title (optional,
   backlog #37)** · email | mobile · organisation (own full-width row) · two
   `AttachmentField` pickers (ID document + personal photo).
4. **Terms link + gold CTA "التالي"** — the shared `TermsAndNextButtons`.
On success: a bilingual "تم تسجيل الزائر — بانتظار الاعتماد" SnackBar + the form
resets for the next walk-in.

**Attachment source (BUG-019 / 19f).** Both attach boxes open the shared
`SimfImageSourceSheet` — **camera** or **file** — so a walk-in desk can shoot the
visitor's document without leaving the app (the old screen opened the gallery
only).

## 4. Data / API (wire contract D-219 frozen)
- `StaffWalkInRequest.toJson` → `POST /app/staff/visitors/register-onsite`
  (mirrors the backend `AdminWalkInRegistrationRequest`). Backlog #37 added the
  optional `jobTitleArabic` key (additive, D-219-safe — only sent when filled);
  the backend `AdminWalkInRegistrationRequest.JobTitleArabic` already existed.
- `StaffWalkInResult.fromJson` ← the response (empty `qrId` = PendingApproval).
- Optional `…/{id}/id-document` + `…/{id}/avatar` multipart uploads.
- The **classification (ProfileType)** is **picked by the operator** from the
  visitor-eligible types (BUG-019 / 19g), seeded to the "Normal" audience tier
  when it exists. It used to be pinned silently by the literal name `Normal` with
  no operator control and no defined behaviour when that row is missing. The
  server re-validates.

## 5. Validation & edge cases
- Light client checks (the server re-validates the full rule set): Arabic + English
  name required (filtered to the right script); phone required + standard
  Saudi/international shape; document number required; nationality + organisation +
  classification + gender selected. Empty/invalid submit → a "أكمل بيانات الزائر
  المطلوبة" SnackBar, **no** request sent.
- **Submit reveals everything at once and scrolls to the first problem**
  (BUG-019 / 19l). `FormState.validate()` already sets every field's error, but the
  CTA sits at the bottom of a long form, so the operator saw the toast with all the
  errors off-screen above. The submit now runs `Scrollable.ensureVisible` on the
  first invalid field, in on-screen order.
- The national-ID / Iqama validators apply a **Luhn mod-10 check digit** on top of
  the digit shape; the messages now say so (BUG-019 / 19m) instead of implying that
  any `1` + 9 digits is accepted.
- Every text input declares a `maxLength` so over-long input can't reach the server.
  The two name inputs cap at **50** — the same number as
  `AdminWalkInRegistrationRequestValidator.MaximumLength(50)` and EF's
  `UserProfile.Name` / `NameArabic` `HasMaxLength(50)` (DEF-STF-003; they used to
  accept 100, so a long name round-tripped into a 400 with nothing highlighted).
- **A server field rejection paints ON the field** (DEF-STF-003). The 400's
  `details[]` are mapped from the FluentValidation property name to the matching
  input (`ArabicName`, `EnglishName`/`DisplayName`, `Email`, `JobTitle`,
  `JobTitleArabic`, `NationalId`, `IqamaNumber`/`PassportNumber`,
  `SaudiMobile`/`InternationalMobile`), the form re-validates, and the first
  rejected field is scrolled into view. Each message is held against the exact
  value the server rejected, so editing that field clears it with no listener and
  no second round-trip. A detail the form has no field for stays in the toast.
- **A failed attachment upload is retryable** (DEF-STF-004). The ID-document /
  avatar uploads run AFTER the account exists, so a failure cannot undo the
  registration — but it must not be swallowed either. A modal names the
  attachment that did not land and offers **إعادة رفع المرفقات / Retry upload**
  (re-sends only the failed file against the same `userId`; the person is never
  registered twice) or **المتابعة بدون المرفقات / Continue without them**. The
  form is cleared only once that modal closes.
- **An empty classification lookup explains itself** (DEF-STF-007). When the
  visitor profile-type lookup returns no rows there is nothing to pick and submit
  could never pass, so the field shows `staffProfileTypeUnavailable` plus
  `staffProfileTypeEmptyHelp` ("ask a Control Panel administrator to add one")
  and the picker is inert.
- Switching nationality clears the stale national-ID / document number.
- Lookup load failure → an error + **Retry** surface.
- A server 400/403 → the bilingual server message in a SnackBar; the form keeps its
  values.

## 6. i18n / RTL
Bilingual (ar/en), Arabic-first, RTL-correct. All strings via `AppL10n`.
Directionality drives the layout — the screen no longer pins whole blocks to
`TextDirection.ltr` and no longer hand-forces `TextAlign.end` (BUG-019 / 19b).
Only genuinely-LTR **content** stays explicitly LTR: the email, mobile, national-ID
and document-number inputs. The one remaining forced-LTR row is the shared
`SimfFormScaffold` top bar (back chevron left, language pill right), which every
account screen shares. Brand font applied once in the theme.

## 7. Testing
- **Widget** (`register_visitor_screen_test.dart`, 19 cases): renders after the
  lookups load, the load-failure Retry surface, the empty-submit guard (no API
  call), every input caps its length, a filled form posts the walk-in payload
  (Saudi path → `saudiMobile`, `isSaudi`, org + Normal profile type, plus the
  optional `jobTitleArabic`, #37) and shows the pending-approval toast, the
  national-ID Luhn rejection — plus the BUG-019 regression group: shared
  `simfFieldDecoration` (`filled == false`) on every input, no
  `DropdownButtonFormField` and three `SimfPickerField`s, the shared
  `SimfLanguageToggle`, the operator-picked classification, the pristine-submit
  reveal + scroll, no overflow at 400×900 Arabic and 1024×1314 two-column, the
  camera-or-file source sheet, and the one-line attachment captions — plus the
  deferred-defect group: the 50-char name cap, a server field rejection painted on
  the field (and cleared on edit), the retryable attachment upload that never
  re-registers, and the empty-classification help.
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
- [x] ~~**Kept local** `_FieldLabel` / `_Pill` / `_decoration` + `_inputStyle`~~ —
      **REVERSED by BUG-019 (2026-07-26).** The owner reviewed the live tablet and
      reported the screen as off-design-system; the local `filled: true` copy WAS
      the wrong field background. All four local primitives are deleted in favour
      of the shared `SimfFieldLabel` / `simfFieldDecoration` / `GenderPillsField` /
      `BeigeTabs`.
- [x] ~~**Body uncapped** (no `MaxWidthBody`)~~ — **REVERSED by BUG-019.** The body
      is `MaxWidthBody` (560 compact / 840 wide) and the breakpoint is `WindowSize`,
      so the two-column tablet layout survives the cap.
- [x] Figma `1467:12357` parity re-verified (golden vs frame, all rows incl.
      attachments) + locked by the new golden
- [x] widget + golden tests + this doc + E2E catalogue + PAGE-INDEX, same changeset
- [x] `flutter analyze` clean (baseline info only); full suite green; wire contract
      (`StaffWalkInRequest`/`StaffWalkInResult`, D-219) unchanged

## 9. Changelog
- **2026-07-27 (deferred walk-in defects):** DEF-STF-003 — the Arabic/English name
  inputs cap at the server's 50 (was 100), and a 400's field-level `details[]` now
  paint on the matching input and clear when it is edited; DEF-STF-004 — an
  attachment upload that fails after a successful registration is surfaced in a
  modal that retries the UPLOAD only (never re-registers the person) or continues
  without it; DEF-STF-007 — an EMPTY classification lookup now explains itself
  (`staffProfileTypeUnavailable` + `staffProfileTypeEmptyHelp`, inert picker)
  instead of silently blocking submit. The operator-chosen classification half of
  DEF-STF-007 had already shipped with BUG-019 / 19g. `SimfPickerField.onTap`
  became nullable (an additive shared-widget change; existing callers unaffected)
  so a lookup with nothing to pick can be inert. Wire contract unchanged.
- **2026-07-26 (BUG-019 — design-system + validation rebuild):** the owner reviewed
  the live tablet screen and reported it as off-design-system. The screen now
  composes the same shared pieces as the visitor Create-profile screen:
  `SimfFormScaffold` chrome with the shared `SimfLanguageToggle` (19a, replacing the
  page-local globe `IconButton` + `_toggleLanguage`); `simfFieldDecoration()` +
  `SimfFieldLabel` via `SimfLabeledTextField` / `MobileField` (19i — the deleted
  local `_decoration()` was `filled: true` with a white fill, the wrong background
  the owner saw); `SimfPickerField` + the searchable `LookupSearchSheet` for
  nationality, organisation and the new classification picker (19j / 19g); locale-
  driven direction with only genuinely-LTR inputs pinned (19b); `SimfTokens` +
  `WindowSize` + `MaxWidthBody` instead of raw pixel literals and an inline
  `maxWidth >= 600` (19c); a camera-or-file attachment source sheet (19f);
  submit-time reveal + scroll-to-first-problem (19l); one-line attachment captions
  with the detail moved to a hint (19k); a national-ID message that names the Luhn
  check digit (19m); and explicit accessible names on the inputs, pickers,
  attachments and the CTA (19h). Golden `staff_register_visitor_1467-12357.png`
  re-locked. **Wire contract unchanged** (`StaffWalkInRequest` /
  `StaffWalkInResult`, D-219).
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
