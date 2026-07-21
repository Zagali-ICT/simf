# E2E test catalogue — `My interests` (edit) (`myInterests`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Item **#14** — the user can
> edit their interests **after** sign-up, not only during it. The SAME interests
> page as `signUpInterests` rendered in **edit mode** (opened from My-Area). Spec:
> [`mobile/my-interests`](../../pages/mobile/my-interests/README.md). Runner-agnostic Gherkin.

| | |
|--|--|
| **Page** | [`my-interests`](../../pages/mobile/my-interests/README.md) — shared screen `SignUpInterestsScreen(editMode: true)` |
| **Route** | app screen #702 `myInterests` → `/my-area/interests` (**auth-gated**) |
| **APIs** | `GET /api/v1/app/account/user-profile` (current profile, for pre-select + lossless re-save); `GET /api/v1/app/account/interests` (lookup); **`POST /api/v1/app/account/user-profile`** (the full-profile upsert carrying the new `interestIds`). Signed-in, no role/permission (D7). |
| **Surface** | Mobile (Flutter) — any signed-in account, opened from My-Area → "اهتماماتي / My interests" |
| **Auth setup** | A signed-in token (own `sub`). Obtain via the standard app sign-in; never a literal secret. |
| **Last reviewed** | 2026-07-21 (created under #14) |

> **What this is (#14).** `myInterests` reuses the sign-up interests screen in
> **edit mode**: it self-loads the current profile, pre-selects the saved
> interests, applies the same **1-10** client rule, and on Save re-POSTs the
> **full** profile with only `interestIds` changed — then pops back to My-Area.
> The full upsert is the only write path, so the round-trip carries every field
> (notably `regionId` + `jobTitleArabic`, which the server sets unconditionally);
> without that, an interests-only save would wipe them. Grounding:
> `lib/features/account/sign_up_interests_screen.dart` (`editMode`) +
> `UserProfileResponse.toUpsertRequest()`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MYINT-001 | Golden — open from My-Area → saved interests pre-selected → change → Save → one full-profile `POST` → toast → pop back | happy | P0 | authored ✓ (widget) |
| E2E-MYINT-002 | Round-trip fidelity — the edit re-POSTs `regionId` + `jobTitleArabic` unchanged; NO field is wiped | happy | P0 | authored ✓ (unit + widget) |
| E2E-MYINT-003 | The 1-10 rule — Save disabled < 1; cap at 10 (toast); `n/10` counter | validation | P0 | authored ✓ (widget) |
| E2E-MYINT-004 | Self-load — GET user-profile + interests lookup; saved interests pre-selected | happy | P0 | authored ✓ (widget) |
| E2E-MYINT-005 | Load failure → error state + retry; no upsert fired | resilience | P1 | authored ✓ (widget) |
| E2E-MYINT-006 | Auth gate — anonymous open of `/my-area/interests` redirects to sign-in | auth | P0 | authored ✓ (router-gate matrix) |
| E2E-MYINT-007 | RTL render (Arabic) — chip grid + counter mirror; title اهتماماتي | i18n | P1 | spec |

## Scenarios

### E2E-MYINT-001 — Golden path: edit interests from My-Area

```gherkin
Feature: Edit interests after sign-up
Scenario: A signed-in user changes their interests from My-Area
  Given a signed-in user on My-Area
  When they tap the "اهتماماتي / My interests" row
  Then the interests screen opens in edit mode and self-loads their profile
  And the interests they saved at sign-up are pre-selected (the n/10 counter reflects them)
  When they select one more interest and tap "Save"
  Then the app POSTs ONE UpsertUserProfileRequest to /app/account/user-profile
  And the body carries the FULL profile with the updated interestIds
  And on ApiResult.Ok a "Your interests were updated" toast shows and the screen pops back to My-Area
```

### E2E-MYINT-002 — Round-trip fidelity (the #14 bug fix)

```gherkin
Scenario: An interests-only edit does not wipe any other profile field
  Given the loaded profile has regionId "region-7" and jobTitleArabic "مهندس"
  When the user changes only their interests and saves
  Then the single full-profile POST still carries regionId "region-7" and jobTitleArabic "مهندس"
  And organisationId, names, nationality, mobile and every other field are unchanged
  # Before #14 the Flutter DTO omitted regionId + jobTitleArabic, so the service
  # (which sets both unconditionally) nulled them on any interests-only save.
```

**Evidence:** `profile_models_test` — `toUpsertRequest` mirrors every field +
`copyWith` swaps only `interestIds`; `sign_up_interests_screen_test` edit-mode —
the captured upsert preserves `regionId`/`jobTitleArabic`/`organisationId`/name.

### E2E-MYINT-003 — The 1-10 rule (shared with create)

```gherkin
Scenario: The edit picker enforces 1..10
  Given the interest chips are shown with a counter
  Then "Save" is disabled when nothing is selected
  When one interest is selected "Save" enables
  And attempting an 11th shows the max-reached toast and is ignored
```

### E2E-MYINT-004 — Self-load + pre-select

```gherkin
Scenario: Edit mode loads the current profile and lookup, and pre-selects
  Given a signed-in user opens /my-area/interests
  Then the app calls GET /app/account/user-profile and GET /app/account/interests
  And the chips for the user's saved interests are shown selected
  And no create-mode "recover to profile-data" state is shown (edit mode self-loads)
```

### E2E-MYINT-005 — Load failure

```gherkin
Scenario: A profile-load failure shows the error, no write
  Given GET /app/account/user-profile fails
  When the screen loads in edit mode
  Then the error message + a Retry button are shown
  And no upsert is fired (nothing was saved)
```

### E2E-MYINT-006 — Auth gate

```gherkin
Scenario: An anonymous open is impossible
  Given no session
  When /my-area/interests is requested
  Then the router redirects to /sign-in (route #702 is in the auth gate)
```

### E2E-MYINT-007 — RTL render (Arabic)

```gherkin
Scenario: The edit interests grid mirrors under Arabic
  Given the app language is Arabic
  Then the header reads اهتماماتي, the chip grid wraps right-to-left, and the "n / 10" counter mirrors
  And each interest row's Arabic label (nameArabic) is shown
```

---

_Last reviewed:_ `2026-07-21` by `SIMF Team` — created under #14 (shared interests page, edit mode + the round-trip fidelity fix).
