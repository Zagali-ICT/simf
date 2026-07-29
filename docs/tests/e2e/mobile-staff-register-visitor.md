# E2E test catalogue — `Staff register visitor` (`staffRegisterVisitor`)

> **Authority:** SIMF E2E test catalogue (D-133). Mobile catalogue — the staff
> walk-in registration "إنشاء ملف زائر / add a visitor at the exhibition"
> (D-509, Figma 1467:12357). Reached from the staff-only drawer entry. Backend:
> `POST /app/staff/visitors/register-onsite` (+ `…/{id}/id-document`,
> `…/{id}/avatar`), reusing the shared on-site provisioning service; backend
> tests in `tests/SIMF.Api.Tests/WalkInRegistrationTests.cs`
> (`Staff_app_walk_in_*`). App tests:
> `src/Mobile/simf_app/test/features/staff/register_visitor_screen_test.dart`
> (widget, 15 cases) + the render-lock golden
> `test/golden/staff_register_visitor_golden_test.dart`
> (`goldens/staff_register_visitor_1467-12357.png` @1024×1314). Clean-code
> reviewed + frozen (D-559, 2026-06-30), then **rebuilt onto the shared design
> system (BUG-019, 2026-07-26)**; per-page doc
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
> **classification (ProfileType)** is **picked by the operator** from the
> visitor-eligible types, seeded to the "Normal" audience tier (BUG-019 / 19g;
> server re-validates). Attachments are optional, may come from the **camera or a
> file**, and are uploaded after the create by the new visitor's id; a failed
> upload does not undo the registration.

---

### E2E-MOBSTAFFREG-001 — Golden walk-in registration

```gherkin
Scenario: Staff register a walk-in visitor
  Given a signed-in staff member opens "تسجيل زائر" from the drawer
  And the lookups load (countries, visitor profile types, organisations)
  And the classification picker (الفئة) is seeded to "Normal / عادي"
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

Scenario: A pristine submit reveals every error and scrolls to the first
  Given a freshly-loaded form where no field has been touched
  When the staff member scrolls to the bottom and taps "التالي"
  Then EVERY required field shows its inline "هذا الحقل مطلوب / This field is
    required" error at once
  And the view scrolls so the FIRST invalid field (the Arabic name) is on screen
    — the CTA sits at the bottom of a long form, so before BUG-019 / 19l every
    error was rendered off-screen above and the operator saw only the toast

Scenario: Client-side ID shape + Luhn check (D-700)
  Given the staff member types a national ID with the right shape (^1\d{9}$)
    but a bad Luhn checksum (e.g. 1012345678)
  Then the field shows the inline "رقم الهوية الوطنية غير صحيح (10 أرقام تبدأ بـ
    1 مع رقم تحقق صحيح) / Invalid national ID (10 digits starting with 1, with a
    valid check digit)" — the message names the check digit (BUG-019 / 19m),
    because 1122334455 matches the stated shape and is still rejected while
    1122334459 is accepted
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
  And no block is pinned left-to-right except the shared top bar (back chevron
    left, language pill right) and the genuinely-LTR inputs (email, mobile,
    national ID, document number)
```

### E2E-MOBSTAFFREG-005 — Design-system parity with Create-profile (BUG-019)

```gherkin
Scenario: The screen uses the shared field system, not a page-local copy
  Given the staff member opens "تسجيل زائر"
  Then the top bar shows the shared EN/ع language pill (SimfLanguageToggle),
    not a gold globe icon button
  And every input renders the shared unfilled bordered field (beige resting
    border, gold focus border) — NOT the old white-filled box
  And the nationality, organisation and classification fields are searchable
    pickers that open the shared type-to-filter sheet, not raw dropdowns
  And the attachment captions are one line ("صورة الهوية / ID document" and
    "الصورة الشخصية / Personal photo"), with the long
    "الهوية الوطنية أو الإقامة أو جواز السفر" detail shown as a hint below

Scenario: Responsive card
  Given the screen is opened on a phone-width window
  Then the fields stack in one column and nothing overflows horizontally
  Given the screen is opened on the 1024×1314 tablet panel
  Then the paired fields sit side-by-side on one row and nothing overflows
```

### E2E-MOBSTAFFREG-006 — Attachment source + classification (BUG-019)

```gherkin
Scenario: Attach from the camera
  When the staff member taps "إرفاق ملف / Attach file"
  Then a sheet offers "التقاط بالكاميرا / Take a photo" AND
    "اختيار ملف / Choose a file"
  When they choose the camera
  Then the device camera opens and the captured image becomes the attachment
  And the same two options are offered for the personal photo

Scenario: Operator picks the visitor classification
  Given the classification picker is seeded to the "Normal / عادي" tier
  When the staff member opens it and picks a different visitor-eligible type
  Then the registration payload carries that profileTypeId
```

### E2E-MOBSTAFFREG-007 — Name length matches the server (DEF-STF-003)

```gherkin
Scenario: The name inputs cap where the server caps
  Given the staff walk-in form is open
  When the staff member types 80 characters into "الاسم بالعربية / Arabic name"
  Then only 50 characters are kept
  And the same holds for "الاسم بالإنجليزية / English name"
# UserProfile.Name / NameArabic are nvarchar(50) and
# AdminWalkInRegistrationRequestValidator caps both at 50; the inputs used to
# accept 100, so a long name round-tripped into a 400 with nothing highlighted.

Scenario: A server field rejection lands ON the field
  Given the server rejects EnglishName with
    "English name must be at most 50 characters." /
    "يجب ألا يتجاوز الاسم بالإنجليزية 50 حرفًا."
  When the staff member submits
  Then POST /app/staff/visitors/register-onsite returns 400 VALIDATION_FAILED
  And that message is shown UNDER the English-name field (locale-appropriate)
  And the first rejected field is scrolled into view
  When the staff member edits that field
  Then the server message clears without another round-trip
```

**Evidence:** `register_visitor_screen_test` — "DEF-STF-003 — the name inputs cap
at the server's 50, not 100", "DEF-STF-003 — a server field rejection paints ON
the field, and clears when it is edited".

### E2E-MOBSTAFFREG-008 — A failed attachment upload is retryable (DEF-STF-004)

```gherkin
Scenario: The ID-document upload fails after the visitor was created
  Given the staff member attached an ID document
  And POST /app/staff/visitors/register-onsite succeeded (PendingApproval)
  When POST /app/staff/visitors/{userId}/id-document fails
  Then a modal says "تم تسجيل الزائر — تعذّر رفع المرفقات /
      Visitor registered — attachments not uploaded"
  And it names which attachment did not land ("صورة الهوية / ID document")
  And the form is NOT cleared yet
  When the staff member taps "إعادة رفع المرفقات / Retry upload"
  Then ONLY the upload is re-sent, against the SAME userId
  And registration is NOT called a second time (no duplicate visitor)
  And on success a toast confirms "تم رفع المرفقات. /
      The attachments were uploaded." and the form resets

Scenario: The operator chooses to continue without the attachment
  When they tap "المتابعة بدون المرفقات / Continue without them"
  Then the success toast is shown and the form resets
  And the document can be attached later from the Control Panel
# The failure used to be swallowed by an empty catch, so the operator was told
# everything succeeded while the document was missing.
```

**Evidence:** `register_visitor_screen_test` — "DEF-STF-004 — a failed attachment
upload is surfaced and can be retried WITHOUT registering the visitor again".

### E2E-MOBSTAFFREG-009 — An empty classification lookup explains itself (DEF-STF-007)

```gherkin
Scenario: No active visitor classification exists
  Given GET the visitor profile-types lookup returns an EMPTY list
  When the staff walk-in form opens
  Then the classification field reads
      "تعذّر تحميل تصنيف الزائر. / Could not load the visitor classification."
  And under it: "لا توجد تصنيفات زوار مفعّلة. اطلب من مسؤول لوحة التحكم إضافة
      تصنيف زائر ثم أعد المحاولة." / "No active visitor classifications exist.
      Ask a Control Panel administrator to add one, then retry."
  And the picker is inert (an empty sheet would waste the operator's time)
  And no registration call is made
# The operator-chosen classification (no longer pinned to the row literally
# named "Normal") shipped with E2E-MOBSTAFFREG-006; this is the remaining
# unavailable case, which used to block submit with no correctable field.
```

**Evidence:** `register_visitor_screen_test` — "DEF-STF-007 — an empty
classification lookup explains itself instead of silently blocking submit".

---

_Last reviewed:_ `2026-07-27` by `SIMF Team` — added E2E-MOBSTAFFREG-007..009 for
the deferred walk-in items (DEF-STF-003 name-length alignment + server field
errors, DEF-STF-004 retryable attachment upload, DEF-STF-007 the unavailable
classification). Earlier: `2026-07-26`.

## Element sweep (WS1)

Generated contract — see `tools/qa/element-sweep.js` and
`docs/tests/element-sweeps/`.

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBSTAFFREG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOBSTAFFREG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

