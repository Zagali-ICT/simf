# E2E test catalogue — `Sign up — interests` (`signUpInterests`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #7‑01 — the
> interests pick (mockup 5‑01) **+ the single profile save**. **New under D-332**
> (split out of Page 007, reversing D12). Spec:
> [`Page_007-01`](../../App/Page_007-01/README.md). Runner-agnostic Gherkin.
>
> **Status note:** target spec; the Flutter build (D-332 step 2) authors the widget
> tests and re-points `Evidence`.
>
> **Shared screen (#14, 2026-07-21):** this same screen now also serves the
> post-sign-up **edit** surface (`myInterests` → `/my-area/interests`,
> `SignUpInterestsScreen(editMode: true)`). The create render + 505:1083 golden
> are unchanged; the edit-mode scenarios live in
> [`mobile-my-interests.md`](mobile-my-interests.md).

| | |
|--|--|
| **Page** | [`Page_007-01`](../../App/Page_007-01/README.md) (App page docs) |
| **Route** | app screen #7‑01 `signUpInterests` → `/sign-up/interests` (**auth-gated**) |
| **APIs** | `GET /api/v1/app/account/interests` (lookup); **`POST /api/v1/app/account/user-profile`** (the single upsert: Page-007 data **+** `interestIds`); `POST …/user-profile/id-image` (multipart, optional, after the row exists). Signed-in, no role/permission (D7). |
| **Surface** | Mobile (Flutter) — Visitor (signed-in, profile-incomplete), arriving from Page 007 |
| **Auth setup** | A signed-in Visitor token (own `sub`). Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-06-30 (clean-code freeze D-550; behaviour unchanged) |

> **KSA-Project redesign (D-365, Figma 505:1083):** the screen now renders the
> two-column pill grid (gold selected / `navyDeep`+border unselected), the
> اختر اهتماماتك heading + long helper, the centred n/10 counter, and a
> **متابعة** (Continue) primary button — the Save label is gone. The
> draft / 1–10 / single-upsert / ID-image contract is unchanged; the old
> screen is parked in `lib/features/_legacy_mockup/`. Live browser check is
> N/A for this screen (auth-gated + requires the in-memory Page-007 draft);
> the widget tests cover the render + contract.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB7A-001 | Golden — receive Page-007 data + pick 1–10 → Save → one `POST` (data+interestIds) → "please wait" → Confirmation | happy | P0 | spec (D-332) |
| E2E-MOB7A-002 | The 1–10 rule — Save disabled < 1; cap at 10 (toast); `n/10` counter | validation | P0 | spec (D-332) |
| E2E-MOB7A-003 | The single `POST` carries BOTH the carried Page-007 fields AND `interestIds` (no separate interests write) | happy | P0 | spec (D-332) |
| E2E-MOB7A-004 | Optional ID image (picked on Page 007) uploaded after the row exists; failure is non-blocking | happy | P1 | authored ✓ (MIME unit + server) |
| E2E-MOB7A-005 | Server validation / 500 on save → message; selection + carried state preserved; Back → Page 007 keeps data | resilience | P1 | spec (D-332) |
| E2E-MOB7A-006 | Empty interests lookup → empty state (seed-data dependency, not a screen error) | edge | P1 | spec (D-332) |
| E2E-MOB7A-007 | Auth gate — anonymous open redirects to sign-in | auth | P0 | authored ✓ (router-gate test) |
| E2E-MOB7A-008 | RTL render (Arabic) — chip grid + counter mirror | i18n | P1 | spec (D-332) |
| E2E-MOB7A-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB7A-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB7A-001 — Golden path: pick interests → single save → Confirmation

```gherkin
Feature: Interests pick + the single profile save
Scenario: A visitor finishes registration by picking interests and saving once
  Given a signed-in visitor arrived from Page 007 with a valid profile-data form state
  And the interests lookup (GET /app/account/interests) has loaded
  When they select between 1 and 10 interest chips
  And they tap "Save"
  Then the app POSTs ONE UpsertUserProfileRequest to /app/account/user-profile
  And the body carries the Page-007 fields (names, nationality, DOB, profileTypeId, …) AND interestIds
  And on ApiResult.Ok the app shows "please wait" and routes to Confirmation (Page_010 → Page_011)
```

### E2E-MOB7A-002 — The 1–10 rule

```gherkin
Scenario: The interests picker enforces 1..10
  Given the interest chips are shown with a "0 / 10 selected" counter
  Then "Save" is disabled
  When one interest is selected the counter reads "1 / 10" and "Save" enables
  And attempting an 11th shows "You can pick at most 10 interests" and is ignored
```

### E2E-MOB7A-003 — One write carries everything

```gherkin
Scenario: There is no separate interests write
  When the visitor saves
  Then exactly one request is sent — POST /app/account/user-profile
  And it contains both the carried Page-007 data and the interestIds
  And no other endpoint is called to persist interests
```

### E2E-MOB7A-004 — ID image upload (multipart, after save)

```gherkin
Scenario: The optional ID image (chosen on Page 007) uploads after the row exists
  Given an ID image was attached on Page 007
  When the visitor saves on the interests screen
  Then the profile is upserted first, then the image is POSTed multipart to
       /app/account/user-profile/id-image with the correct Content-Type
  And if only the image upload fails, "Profile saved, but the image upload failed." is shown
       (the profile save still succeeded — non-blocking)
```

**Evidence:** `profile_repository_mime_test` (jpg/jpeg/png/webp → MIME); server
`UserIdDocumentUploadEndpoint` magic-byte gate (covered by `UserProfileTests`).

### E2E-MOB7A-005 — Save failure preserves state

```gherkin
Scenario: A save fails and the user can fix and retry
  When the upsert returns a validation error / 500
  Then the message is shown and the interests selection is preserved (no navigation)
  And if the bad field belongs to Page 007, the user can tap Back to Page 007 with the data intact, fix it, and return
```

### E2E-MOB7A-006 — Empty interests lookup

```gherkin
Scenario: A lookup that returns no rows shows an empty state
  Given GET /app/account/interests returns []
  Then the screen shows "No interests yet" (a seed-data dependency, not a blocking error)
  And Save stays disabled (1–10 cannot be satisfied)
```

### E2E-MOB7A-007 — Auth gate

```gherkin
Scenario: An anonymous open is impossible
  Given no session
  When /sign-up/interests is requested
  Then the router redirects to /sign-in (the interests route is in the auth gate)
```

### E2E-MOB7A-008 — RTL render (Arabic)

```gherkin
Scenario: The interests grid mirrors under Arabic
  Given the app language is Arabic
  Then the chip grid wraps right-to-left and the "n / 10" counter mirrors
  And each interest row's Arabic label (nameArabic) is shown
```

---

_Last reviewed:_ `2026-06-11` by `SIMF Team` — created under D-332; D-365 redesign noted.
