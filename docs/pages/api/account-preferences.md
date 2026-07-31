# Account accessibility preferences — `GET`/`PUT /app/account/preferences`

| | |
|--|--|
| **Surface** | App API — `SIMF.Api/Endpoints/Account/AccountPreferencesEndpoints.cs` |
| **Route** | `GET /api/v1/app/account/preferences` · `PUT /api/v1/app/account/preferences` |
| **Source** | `AccountPreferences` + `UpdateAccountPreferencesRequest` (Contracts) · `IAccountPreferencesService` (Application) · `AccountPreferencesService` (Infrastructure) · five additive `UserProfile` columns |
| **Authorisation** | `RequireApprovedAccount` + the caller's own `sub`. **No `PermissionCatalog` code** — this is an app-user surface with no CP page and no admin action. |
| **Consumers** | The Flutter accessibility screen (#38) — write-through on every toggle, hydrate once at sign-in |
| **Tests** | `tests/SIMF.Api.Tests/AccountPreferencesTests.cs` (8 facts) · E2E [`api-account-preferences.md`](../../tests/e2e/api-account-preferences.md) (E2E-ACP-001..013) · app half [`mobile-accessibility.md`](../../tests/e2e/mobile-accessibility.md) (E2E-MOB038-007..012) |
| **Last reviewed** | 2026-07-31 |

## Purpose

`accessibility-server-sync`. The five accessibility choices were **device prefs
only**, so they did not follow the user to a second device and did not survive a
reinstall. For an accessibility setting that is the failure that matters: the
person who needs `extraLarge` needs it on whichever phone is in their hand.

They are now account settings. The app writes through on every change and reads
the account copy back once at sign-in; the device prefs stay as the offline cache
and the only **read** path on first frame, so the right text scale still renders
before any network call.

## Contract

```
GET  /app/account/preferences  → 200 ApiResult<AccountPreferences>
PUT  /app/account/preferences  → 200 ApiResult<AccountPreferences>
```

| Field | Type | Default | Meaning |
|---|---|---|---|
| `textSize` | string | `"normal"` | `small` · `normal` · `large` · `extraLarge` — حجم الخط |
| `highContrast` | bool | `false` | تباين عالٍ |
| `reduceMotion` | bool | `false` | تقليل الحركة |
| `screenReaderAssist` | bool | `false` | قارئ الشاشة |
| `captions` | bool | **`true`** | الترجمة النصية — the one choice that defaults on |

Three rules the shape depends on:

1. **`textSize` is the app's stable enum NAME, never an index.** Reordering the
   Dart `AppTextSize` cases can then never re-interpret a stored row. The match
   is ordinal (case-sensitive), because the client compares byte for byte —
   `"ExtraLarge"` would decode as the fallback rather than as the user's pick, so
   it is rejected rather than coerced.
2. **The `PUT` is a full replace**, so it is idempotent; every field is optional
   and falls back to its shipped default, so a partial body from an older build
   stores a complete set instead of failing.
3. **The camelCase field names are a frozen wire contract.** The shipped app
   decodes them one by one and falls back on anything it does not recognise, so a
   rename would silently reset every user to the defaults rather than fail loudly.

## Behaviour

- **A never-saved account reads the defaults, not a 404.** "I have not chosen
  yet" is a value, not a missing resource, and the app's first read on a fresh
  device happens before any write.
- **A `PUT` with no profile row seeds a stub**, same contract as the ID-document
  upload (`UserProfileService.UploadIdImageAsync`). The stub is empty-named, so
  `IsProfileCompleteAsync` still reports "not registered" — picking a text size
  cannot flip a half-registered account to registered.
- **An unknown `textSize` is a bilingual 400 `VALIDATION_FAILED`** carrying one
  `details` entry on field `textSize`. The check runs **before** the profile
  lookup, so a rejected save cannot seed a stub row as a side effect.
- **A legacy or desk-created row with a blank text size degrades to `normal`**
  on read, so the app is always handed a name its enum can decode.
- **The `PUT` carries `RequireRateLimiting("auth")`** (20 / 60 s per IP by
  default) because the app writes through on every toggle. The `GET` is
  deliberately unlimited: it runs once per sign-in, and throttling it would
  degrade accessibility on shared-NAT venue Wi-Fi.

## Storage

Five **additive** columns on `UserProfile` (`SIMF_App`), configured in
`UserProfileConfiguration`:

| Column | Store default | Note |
|---|---|---|
| `AccessibilityTextSize` | `'normal'` | `nvarchar(16)`, required. `HasSentinel(UserProfile.DefaultAccessibilityTextSize)`. |
| `AccessibilityHighContrast` | `0` | |
| `AccessibilityReduceMotion` | `0` | |
| `AccessibilityScreenReaderAssist` | `0` | |
| `AccessibilityCaptions` | `1` | `HasSentinel(true)` |

> **Migration:** the five columns are declared on the entity and in
> `UserProfileConfiguration` but are **not yet in a migration** — the App
> `InitialCreate` snapshot on this branch predates them. They land in the
> consolidated migration run after this work merges. Until then the columns exist
> in the model only, and a database created from the current migration will not
> have them.

The two `HasSentinel` calls are load-bearing, not decoration. Without them the
first save that turns captions **off** would match EF's CLR default, be omitted
from the `INSERT`, and come back **on** from the column default — the user's one
explicit "no" silently reversed. Same reasoning for a first save that picks
`normal` explicitly.

`SIMF.Domain` does not reference `SIMF.Contracts`, so the column default
(`UserProfile.DefaultAccessibilityTextSize`) and the wire default
(`AccountPreferences.DefaultTextSize`) are two constants the compiler cannot keep
in step; `AccountPreferencesTests.The_stored_default_text_size_matches_the_contract_default`
pins them together instead.

## D-157

The preferences live on the profile row, which already carries the bare
`UserId`. The service touches `SimfAppDbContext` only — no navigation, no FK and
no join reaches `SIMF_Identity`, and there is no cross-database transaction. The
read is a five-column `AsNoTracking()` projection, so it never materialises the
profile's PII columns.

## Related

- App screen + sync behaviour: [`mobile/accessibility/`](../mobile/accessibility/README.md)
- Register item `accessibility-server-sync`, closed 2026-07-31 —
  [`docs/tests/SIMF-Defect-Register-2026-07-30.md`](../../tests/SIMF-Defect-Register-2026-07-30.md)
