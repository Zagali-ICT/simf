# E2E test catalogue — `Contacts` (`/admin/contacts`)

> **Authority:** SIMF-FDS-014 (D-281 / Slice C2). The shared, de-duplicated
> Contact directory reused by Sponsors / Exhibitors / MediaPartners / Speakers /
> Booth officers via a nullable `ContactId` FK.

| | |
|--|--|
| **Page** | [`contacts.md`](../../pages/cp/contacts.md) |
| **Route** | `/admin/contacts` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell driver _(API layer: `tests/SIMF.Api.Tests/ContactsTests.cs`)_ |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via `Get-Totp` helper |
| **Permission** | `Contacts.View` (page + read); `Contacts.Edit` (create / update / delete) |
| **Last reviewed** | 2026-06-04 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CON-001 | Golden path — create a contact | happy | P0 | authored |
| E2E-CON-002 | Edit a contact (full detail round-trip) | happy | P0 | authored |
| E2E-CON-003 | Search reloads the grid server-side | happy | P1 | authored |
| E2E-CON-004 | Country column shows the resolved bilingual name | happy | P1 | authored |
| E2E-CON-005 | Empty state renders SimfEmptyState | happy | P1 | authored |
| E2E-CON-006 | Auth gate (non-admin → /not-permitted) | auth | P0 | authored |
| E2E-CON-007 | Validation — blank Arabic name | error | P1 | authored |
| E2E-CON-008 | Validation — latitude without longitude | error | P1 | authored |
| E2E-CON-009 | Deactivate an unreferenced contact | happy | P1 | authored |
| E2E-CON-010 | Conflict — deactivate a referenced contact (409) | error | P0 | authored |
| E2E-CON-011 | Server 500 surfaces an error toast | resilience | P2 | authored |
| E2E-CON-012 | RTL render (Arabic UI) | i18n | P1 | authored |
| E2E-CON-013 | ContactPicker — link an existing contact on an admin form | happy | P0 | authored |
| E2E-CON-014 | ContactPicker — edit pre-loads the link; clear unlinks (no wipe) | happy | P0 | authored |

## Scenarios

### E2E-CON-001 — Golden path: create a contact

```gherkin
Feature: Contacts directory golden path
  As an administrator with Contacts.Edit
  I want to create a shared contact record
  So that Sponsors / Exhibitors / Speakers / Booths can link to one source of truth

Background:
  Given an Administrator is signed in
  And the administrator opens "/admin/contacts"

Scenario: Create a contact with the full card
  When the administrator clicks "Add" on the grid toolbar
  And fills "Name (Arabic)" with "علم البحرية"
  And fills "Name (English)" with "Naval Tech Co."
  And fills "Primary phone" with "+966500000000"
  And fills "Email" with "info@navaltech.test"
  And fills "Website" with "https://navaltech.test"
  And selects "Saudi Arabia (SA)" in "Country"
  And clicks "Save"
  Then a success toast "Contact saved." appears
  And the grid reloads with a row whose Name (Arabic) is "علم البحرية"
  And the Status pill shows "Active"
```

**Evidence captured:**
- Screenshot after: `docs/screenshots/contacts-create-after.png`
- Console errors: 0 expected · Network failures: 0 expected
- API: `POST /api/v1/admin/contacts` → 200 `ApiResult<AdminContactDetail>`; row persisted in `SIMF_App.Contacts`.

### E2E-CON-002 — Edit a contact (full detail round-trip)

```gherkin
Scenario: Editing loads the full detail (logo / social / map) then saves
  Given a contact "علم البحرية" exists
  When the administrator clicks the row edit (pencil) action
  Then the modal pre-fills Name (Arabic/English), phones, email, website,
       Facebook/X/LinkedIn/Instagram, latitude, longitude, Country and the Active toggle
       (fetched via GET /api/v1/admin/contacts/{id})
  When the administrator changes "Name (English)" to "Naval Technologies"
  And clicks "Save"
  Then a success toast "Contact saved." appears
  And re-opening the row shows "Naval Technologies"
```

### E2E-CON-003 — Search reloads the grid server-side

```gherkin
Scenario: The search box filters by name / phone / email
  Given contacts "علم البحرية" and "شركة الموانئ" exist
  When the administrator types "موانئ" in the search box and clicks "Search"
  Then the grid shows only "شركة الموانئ"
  And the request carried GridQuery.Search = "موانئ" with Skip reset to 0
```

### E2E-CON-004 — Country column shows the resolved bilingual name

```gherkin
Scenario: The grid country column needs no second fetch
  Given a contact linked to country "Saudi Arabia"
  When the administrator views the grid in the English UI
  Then the Country column shows "Saudi Arabia"
  And in the Arabic UI it shows "المملكة العربية السعودية"
  And a contact with no country shows "—"
```

### E2E-CON-005 — Empty state

```gherkin
Scenario: Empty directory renders SimfEmptyState
  Given the database has no active contacts matching the current query
  When the administrator opens the page
  Then the grid shows the SimfEmptyState "No contacts yet"
  And no error toast appears
```

### E2E-CON-006 — Auth gate

```gherkin
Scenario: A non-administrator (no Contacts.View) is denied
  Given a signed-in admin whose role lacks Contacts.View
  When they navigate to "/admin/contacts"
  Then they are redirected to "/not-permitted" with HTTP 200
  And the "Contacts" item is absent from the side navigation
```

### E2E-CON-007 — Validation: blank Arabic name

```gherkin
Scenario: Saving with a blank Arabic name is rejected
  When the administrator clicks "Add", leaves "Name (Arabic)" blank, and clicks "Save"
  Then an error toast "The Arabic name is required." appears and no row is created
  And if the client guard is bypassed, the API returns 400 CONTACT_INVALID
```

### E2E-CON-008 — Validation: latitude without longitude

```gherkin
Scenario: A half-filled map location is rejected
  When the administrator fills "Name (Arabic)" with "موقع"
  And fills "Latitude" with "24.7" but leaves "Longitude" blank
  And clicks "Save"
  Then an error toast "Set both latitude and longitude, or leave both blank." appears
  And the API (if reached) returns 400 CONTACT_INVALID
```

### E2E-CON-009 — Deactivate an unreferenced contact

```gherkin
Scenario: Soft-deleting a contact no entity links
  Given a contact "للحذف" linked by no active entity
  When the administrator clicks the row delete action and confirms
  Then a success toast "Contact deactivated." appears
  And the row's Status pill becomes "Inactive"
  And the contact no longer appears in the link picker (Contacts.View picker)
```

### E2E-CON-010 — Conflict: deactivate a referenced contact (409)

```gherkin
Scenario: A contact still linked by an active sponsor cannot be deactivated
  Given a contact "مرتبط" linked by an active Sponsor (ContactId set)
  When the administrator clicks the row delete action and confirms
  Then the API returns 409 CONTACT_IN_USE
  And an error toast shows the bilingual "contact in use" message
  And the contact stays Active
```

### E2E-CON-011 — Server 500

```gherkin
Scenario: A backend failure surfaces a graceful toast
  Given the contacts list endpoint returns a 500
  When the administrator opens the page
  Then an error toast "Something went wrong. Please try again." appears
  And no unhandled client exception is thrown
```

### E2E-CON-012 — RTL render

```gherkin
Scenario: The page renders right-to-left in Arabic
  Given the UI language is Arabic
  When the administrator opens "/admin/contacts"
  Then the banner reads "جهات الاتصال"
  And the grid, toolbar and modal mirror to RTL with Arabic labels
  And the Country dropdown shows Arabic country names
```

### E2E-CON-013 — ContactPicker: link an existing contact on an admin form

```gherkin
Feature: Link a shared Contact from an org-facing admin form (Slice C2b)
  The same ContactPicker is wired into the Sponsor / Exhibitor / MediaPartner /
  Speaker / Booth-officer admin forms. It is link-existing only (manage the
  directory at /admin/contacts).

Background:
  Given an Administrator is signed in
  And a contact "علم البحرية / Naval Tech Co." exists in the directory

Scenario: Link a contact while creating a sponsor
  Given the administrator opens "/admin/sponsors" and clicks "Add"
  And fills the sponsor's required name + tier
  When in the "Linked contact" picker they type "Naval" and click "Find"
  Then the result "علم البحرية · Naval Tech Co." is listed
  When they click that result
  Then the picker shows "Linked: علم البحرية · Naval Tech Co." with a "Clear" button
  When they click "Save"
  Then the sponsor is created with ContactId set
  And re-opening the sponsor shows the picker pre-loaded with that contact
  And the public sponsors list flattens the contact's name/logo/website (E2E-CON via API)
```

### E2E-CON-014 — ContactPicker: edit pre-loads the link; clear unlinks (no wipe)

```gherkin
Scenario: Editing a linked entity pre-loads the picker and never silently wipes the link
  Given a Sponsor already linked to a contact
  When the administrator opens the sponsor's edit modal
  Then the form fetches the full detail (GET /admin/sponsors/{id}) and the picker
       shows "Linked: <contact name>" — NOT empty
  When the administrator changes an unrelated field and saves
  Then the existing ContactId is preserved (not nulled)
  When instead the administrator clicks "Clear" in the picker and saves
  Then the link is removed (ContactId = null) and the public card falls back to the
       entity's own inline name/logo
```

**Evidence captured (CON-013/014):**
- The same flow applies to Exhibitor / MediaPartner / Speaker / Booth-officer forms
  (Booth's picker is the booth *officer*). Sponsor + MediaPartner edit fetch the
  detail via the C2b GET passthroughs; Exhibitor + Booth already fetched detail;
  Speaker uses its detail-backed form.

## Implementation notes

- API-layer coverage lives in `tests/SIMF.Api.Tests/ContactsTests.cs` (22 cases:
  CRUD, picker active/inactive, referenced-delete 409, blank-name 400,
  lat/long-pair 400, country-name projection, and the D-281 link/flatten cases).
- The page is gated by `Contacts.View`; `POST/PUT/DELETE /api/v1/admin/contacts`
  enforce `Contacts.Edit` server-side (the grid's Add/Edit/Delete actions follow
  the Organisation directory pattern — page View-gated, writes Edit-gated).
- The Contact **link picker** (Slice C2b, `ContactPicker.razor`) that wires
  `ContactId` onto the Sponsor / Exhibitor / MediaPartner / Speaker / Booth-officer
  forms is catalogued centrally here as **E2E-CON-013/014** (the component is
  identical across all five forms); each form's own catalogue cross-references it.

---

_Last reviewed:_ 2026-06-04 by SIMF Team.
