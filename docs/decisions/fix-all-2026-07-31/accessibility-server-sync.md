# accessibility-server-sync — the accessibility flags were device prefs only

Item ref: `accessibility-server-sync` (Track D-b, fix-all run 2026-07-30).
Files touched:
`src/Mobile/simf_app/lib/features/accessibility/data/accessibility_preferences_repository.dart` (new) ·
`src/Mobile/simf_app/lib/features/accessibility/data/accessibility_controller.dart` ·
`src/Mobile/simf_app/lib/features/account/post_auth_route.dart` ·
`src/Mobile/simf_app/test/features/accessibility/accessibility_server_sync_test.dart` (new) ·
`docs/tests/e2e/mobile-accessibility.md` · `docs/pages/mobile/accessibility/README.md`.

## DECISIONS_LOG

### D-NEXT — accessibility-server-sync: the five accessibility flags become account settings, with device prefs as the offline cache

Text size, high contrast, reduce motion, screen-reader assist and captions were
persisted **only** to `shared_preferences`. The screen's own doc comment said so
plainly, and there was no repository and no API call in the feature. A user who
had configured the app for their eyesight lost every setting on a reinstall or a
new device — the population most harmed by having to rediscover a settings
screen.

**Built:**

- `AccessibilityPreferencesRepository` — `GET` / `PUT
  /api/v1/app/account/preferences`. `textSize` travels as the **stable enum
  name** (`small` / `normal` / `large` / `extraLarge`), never an index, so
  `AppTextSize` can be reordered without silently re-mapping every user's
  choice. Decoding is tolerant: each field is optional and falls back to its
  shipped default (captions default **on**), so an older or partial payload
  never throws.
- **Write-through.** Every setter pushes the whole settings object after the
  local write.
- **Hydrate at sign-in.** `AccessibilitySync.hydrate()` is called from
  `routeAfterAuth` — the single post-auth seam every sign-in path already runs
  through (password, 2FA completion, badge password), so there is one call site
  rather than three.

**Device prefs stay the only READ path.** Nothing reads the server on the render
path; hydration writes *into* prefs. That keeps the first frame instant and
correct offline — a user with high contrast on must not get one frame of
low-contrast while a network call resolves.

**Both sync directions swallow their failures, by contract.** A preferences sync
must never disturb the choice the user just made, and must never fail or delay a
sign-in. This is the same contract `OrgProfileController.warm()` already
follows. A consequence worth stating: until the API half is deployed, the screen
degrades to **exactly** the pre-change behaviour rather than erroring.

**API contract required (Track C).** The app half is written against this
shape; if the API lands differently, the repository's two calls are the only
thing to change.

```csharp
// GET /api/v1/app/account/preferences  → ApiResult<AccountPreferencesResponse>
// PUT /api/v1/app/account/preferences  → ApiResult<AccountPreferencesResponse>
// Policies(nameof(AuthorizationPolicies.RequireApprovedAccount))
// Actor = the "sub" claim; a caller only ever reads/writes their OWN row.
public sealed class AccountPreferencesResponse
{
    public string TextSize { get; set; } = "normal"; // small|normal|large|extraLarge
    public bool HighContrast { get; set; }
    public bool ReduceMotion { get; set; }
    public bool ScreenReaderAssist { get; set; }
    public bool Captions { get; set; } = true;
}

public sealed class UpdateAccountPreferencesRequest
{
    public string TextSize { get; set; } = "normal";
    public bool HighContrast { get; set; }
    public bool ReduceMotion { get; set; }
    public bool ScreenReaderAssist { get; set; }
    public bool Captions { get; set; } = true;
}
```

A **dedicated** endpoint rather than the existing user-profile upsert:
`POST /app/account/user-profile` is a full-profile upsert whose validator
requires 1–10 interests and the whole registration form, so routing a
one-switch accessibility change through it would make toggling high contrast
re-submit (and re-validate) the user's identity data.

**Tests:** `accessibility_server_sync_test.dart` — write-through (all five
setters push the whole object), a failed push leaving state **and** prefs on the
user's choice, hydration replacing the local copy *and* writing prefs, an
unreachable server leaving the cache untouched, and two wire-decode cases (name
token; defaults on an empty/unknown payload). Every case fails on the pre-fix
tree, which had no repository, no write-through and no hydration.

## PAGE-INDEX

Replace the `#38 accessibility` row (line ~280) with:

| #38 `accessibility` (`GET`/`PUT /app/account/preferences` — signed-in sync; the screen itself works anonymously) | ✅ Real — Figma `1116:16630`; **clean-code frozen (D-640)** — 4 widgets → `widgets/`, twin white label → `SimfTokens.labelWhiteMedium`, golden. Persisted + applied app-wide (D-327); screen-reader/captions wired (D-465); **server-synced (2026-07-30)** — write-through on change + hydrate at sign-in, device prefs kept as the offline cache | Guest+ (sync: signed-in) | [mobile/accessibility/](mobile/accessibility/README.md) | [e2e/mobile-accessibility.md](../tests/e2e/mobile-accessibility.md) |

## E2E-README

Replace the `#38 accessibility` row (line ~285) with:

| #38 `accessibility` (`GET`/`PUT /app/account/preferences`) | [`mobile-accessibility.md`](mobile-accessibility.md) | E2E-MOB038-001..011 |

**Roll-up:** this item adds **+5** Coverage-matrix rows (`E2E-MOB038-007..011`).
`E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`
asserts `**Total scenarios:** N` equals the real row count, so bump it by 5 when
merging (Track D-b contributes **+10** in total).
