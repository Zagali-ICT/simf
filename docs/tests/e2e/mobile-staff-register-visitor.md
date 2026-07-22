# E2E test catalogue — `Staff register visitor` (`staffRegisterVisitor`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the staff
> walk-in registration "إنشاء ملف زائر / add a visitor at the exhibition"
> (D-509, Figma 1467:12357). Reached from the staff-only drawer entry. Backend:
> `POST /app/staff/visitors/register-onsite` (+ `…/{id}/id-document`,
> `…/{id}/avatar`), reusing the shared on-site provisioning service; backend
> tests in `tests/SIMF.Api.Tests/WalkInRegistrationTests.cs`
> (`Staff_app_walk_in_*`). App tests:
> `src/Mobile/simf_app/test/features/staff/register_visitor_screen_test.dart`
> (widget, 5 cases) + the render-lock golden
> `test/golden/staff_register_visitor_golden_test.dart`
> (`goldens/staff_register_visitor_1467-12357.png` @1024×1314). Clean-code
> reviewed + frozen (D-559, 2026-06-30); per-page doc
> [`docs/pages/mobile/staff-register-visitor/`](../../pages/mobile/staff-register-visitor/README.md).

| | |
|--|--|
| **Page** | mobile staff walk-in registration (Figma 1467:12357) |
| **Route** | app screen #114 `/staff/register-visitor` |
| **Surface** | Mobile (Flutter); tablet two-column, phone single-column |
| **Role/gate** | App: `AppRole.staff` (router role-gate). Server: `Visitors.RegisterOnsite` permission + `RequireApprovedAccount` (403 otherwise) |
| **Test runner** | Flutter widget/unit test + device manual |

> **Notes:** the account is created **PendingApproval** with **no QR** — an admin
> approves it from the pending-visitors queue, which mints the badge (D-425). The
> **classification (ProfileType)** is not in the frame; it is auto-assigned to the
> seeded "Normal" audience tier (server re-validates). Attachments are optional
> and uploaded after the create by the new visitor's id; a failed upload does not
> undo the registration.

---

### E2E-MOBSTAFFREG-001 — Golden walk-in registration

```gherkin
Scenario: Staff register a walk-in visitor
  Given a signed-in staff member opens "تسجيل زائر" from the drawer
  And the lookups load (countries, visitor profile types, organisations)
  When they fill the Arabic + English name, pick gender, pick nationality,
    enter the document number (national ID for Saudi; Iqama/passport otherwise),
    enter a mobile number, the job title, optionally the Arabic job title
    (المسمى الوظيفي بالعربية — backlog #37), and pick the organisation
  And optionally attach the ID-document image and a personal photo
  And tap "التالي / Next"
  Then POST /app/staff/visitors/register-onsite is sent with the walk-in payload
    (including jobTitleArabic when the Arabic job title was filled — additive key)
  And it returns HTTP 200 with a PendingApproval visitor (empty QR)
  And the optional images are uploaded to /app/staff/visitors/{id}/id-document
    and /avatar
  And a "تم تسجيل الزائر — بانتظار الاعتماد / Visitor registered — pending
    approval" toast shows and the form resets for the next walk-in
```

### E2E-MOBSTAFFREG-002 — Required-field guard

```gherkin
Scenario: An incomplete form is blocked client-side
  When the staff member taps "التالي" without filling the required fields
  Then a "أكمل بيانات الزائر المطلوبة / Complete the visitor's required
    details" SnackBar shows
  And NO registration request is sent

Scenario: Client-side ID shape + Luhn check (D-700)
  Given the staff member types a national ID with the right shape (^1\d{9}$)
    but a bad Luhn checksum (e.g. 1012345678)
  Then the field shows the inline "Invalid national ID (10 digits starting with 1)"
  And NO registration request is sent (the client rejects it before the server)

Scenario: Server validation surfaces the bilingual message
  Given the form passes the client checks but fails a server rule
  When POST …/register-onsite returns 400
  Then the server's bilingual error message is shown in a SnackBar
  And the form keeps its entered values
```

### E2E-MOBSTAFFREG-003 — Nationality drives the document section

```gherkin
Scenario: Saudi nationality shows the national-ID field
  Given the staff member picks "السعودية / Saudi Arabia"
  Then the national-ID field shows (no document-type toggle) and saudiMobile is sent

Scenario: Non-Saudi shows the Iqama/Passport toggle
  Given the staff member picks a non-Saudi nationality
  Then the الإقامة/جواز السفر toggle + document-number field show
  And internationalMobile is sent; switching nationality clears the stale id
```

### E2E-MOBSTAFFREG-004 — Role / authority / failures / RTL

```gherkin
Scenario: Non-staff cannot reach the screen
  Given a signed-in visitor/moderator
  Then the drawer shows no "تسجيل زائر" entry
  And navigation to /staff/register-visitor redirects home (role gate)

Scenario: Staff without the Visitors.RegisterOnsite grant (403)
  Given the user is AppRole.staff but lacks the permission
  When POST …/register-onsite returns 403
  Then the server-error message is shown; no account is created

Scenario: Lookup load failure
  When the countries/profile-types/organisations load fails
  Then the error + Retry surface shows; Retry re-fetches

Scenario: RTL
  Given the app language is Arabic
  Then the header, card, fields, toggles and attachments render right-to-left
```

---

_Last reviewed:_ `2026-06-30` by `SIMF Team`.
