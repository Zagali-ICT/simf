# E2E test catalogue — `Contact Inquiries` (CP `/admin/contact-inquiries`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). D-464 — the CP inbox for the
> public "تواصل معنا / Contact us" messages submitted anonymously from the app +
> website (`POST /app/contact-inquiry`). An admin reviews each message and marks it
> handled / reopens it.
>
> **D-649 (2026-07-07):** this catalogue was authored when the page's CP BFF
> passthroughs (`/account/api/admin/contact-inquiries/*`) were wired — before that
> the page loaded but its grid `POST` fell through to the GET-only Blazor fallback
> and 400'd ("incorrect Content-type"), so the inbox showed an error banner + empty
> grid. The API endpoints (`ContactInquiryEndpoints`, gated `ContactInquiries.View`/`.Manage`)
> and the page always existed; only the CP forwarding layer was missing.

| | |
|--|--|
| **Page** | CP `/admin/contact-inquiries` (`ContactInquiriesList.razor`) |
| **Route** | `/admin/contact-inquiries` (nav item gated `ContactInquiries.View`) |
| **APIs** | `POST /api/v1/admin/contact-inquiries/list` — `GridQuery` → `GridPage<AdminContactInquiryRow>` (`ContactInquiries.View`); `POST /api/v1/admin/contact-inquiries/{id}/handled` — `{ handled: bool }` → `bool` (`ContactInquiries.Manage`). Public submit: `POST /api/v1/app/contact-inquiry` (anonymous, rate-limited). |
| **Surface** | Control Panel (Blazor) — Administrator |
| **Auth setup** | A signed-in admin with `ContactInquiries.View` (+ `.Manage` for the toggle). Use `Get-Totp` for 2FA — never a literal secret. |
| **Columns** | Name · Email · Message (truncated to 80 chars) · Status · Received (وردت) · Actions |
| **Last reviewed** | 2026-07-07 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CINQ-001 | Page loads; the grid lists submitted inquiries, open first / newest first, paged | happy | P0 | authored ✓ (`ContactInquiryTests` list) |
| E2E-CINQ-002 | An anonymous `POST /app/contact-inquiry` appears in the CP inbox on next load | happy | P0 | authored ✓ (`ContactInquiryTests` submit→list) |
| E2E-CINQ-003 | Mark an open inquiry **handled** → success toast; the row's status flips and the list reloads | happy | P0 | authored ✓ (`ContactInquiryTests` mark-handled) |
| E2E-CINQ-004 | **Reopen** a handled inquiry → success toast; status flips back | happy | P1 | authored ✓ (`ContactInquiryTests` reopen) |
| E2E-CINQ-005 | Empty inbox → the "لا توجد رسائل بعد" empty state (no error banner) | empty | P0 | spec |
| E2E-CINQ-006 | Auth gate — an admin without `ContactInquiries.View` cannot load the list (403) and the nav item is hidden; the toggle needs `ContactInquiries.Manage` | auth | P0 | authored ✓ (`PermissionEnforcementTests` + `CpNavigationPermissionTests`) |
| E2E-CINQ-007 | Load wire failure (5xx / the BFF route missing — the D-649 regression) → error toast, empty grid, no crash | resilience | P0 | authored ✓ (D-649 live-verified: 400 → error banner) |
| E2E-CINQ-008 | RTL render (Arabic) — header, columns, status + action icons mirror; email/date stay LTR | i18n | P1 | spec |
| E2E-CINQ-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-CINQ-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-CINQ-002 — A submitted message reaches the inbox

```gherkin
Feature: CP Contact Inquiries inbox
Scenario: An anonymous contact message shows up for an admin
  Given a visitor submits POST /app/contact-inquiry with name "Sara", email "sara@example.com", message "Question about registration"
  When an admin with ContactInquiries.View opens /admin/contact-inquiries
  Then POST /admin/contact-inquiries/list returns the message as an open row (newest first)
  And the Message cell shows the text truncated to 80 characters
```

### E2E-CINQ-003 — Mark handled / reopen

```gherkin
Scenario: An admin resolves then reopens an inquiry
  Given an open inquiry row is shown
  When the admin taps "Mark handled"
  Then POST /admin/contact-inquiries/{id}/handled with { handled: true } succeeds
  And a success toast "تم" is shown and the list reloads with the row now handled
  When the admin taps "Reopen" on that row
  Then POST /admin/contact-inquiries/{id}/handled with { handled: false } flips it back to open
```

### E2E-CINQ-006 — Auth gate

```gherkin
Scenario: Only admins with the ContactInquiries permission use the inbox
  Given a signed-in admin without ContactInquiries.View
  When they request POST /api/v1/admin/contact-inquiries/list
  Then the API returns 403 Forbidden
  And the CP nav hides the Contact Inquiries item
  And the "Mark handled/Reopen" action requires ContactInquiries.Manage (403 without it)
```

### E2E-CINQ-007 — The D-649 regression must not recur

```gherkin
Scenario: A missing/broken BFF forward degrades gracefully
  Given the /account/api/admin/contact-inquiries/list forward returns a non-2xx
  When the admin opens /admin/contact-inquiries
  Then the page shows the error toast "أعاد الخادم استجابة غير متوقعة" and an empty grid
  And the page does not crash (no Blazor error UI)
  # Regression guard for D-649 — the route MUST be mapped in AccountEndpoints.cs
```

---

_Last reviewed:_ `2026-07-07` by `SIMF Team` — D-649 (BFF wiring restored; catalogue authored).
