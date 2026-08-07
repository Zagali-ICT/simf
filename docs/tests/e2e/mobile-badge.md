# E2E test catalogue — `Entry badge` (`badge`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> dashboard read is already built (`GET /app/account/dashboard`,
> `RequireApprovedAccount`, D-300); the screen consumes **only** the identity
> (`qrId`, `fullNameEn`/`fullNameAr`) from it and reuses the My-Area data layer
> (`MyAreaRepository.getDashboard()` + `MyAreaDashboard` — no duplicate model).
> The **Flutter screen is built** and was **rebuilt to KSA Wave-2 frame
> 221:769 "QR"** (D-378 batch): the gold-bordered white card (QR 230 +
> "امسح للدخول" + the identity strip with avatar/name/tier and the
> **masked `ID · •••• tail`** — the strip is tinted by the profile type's colour,
> gold fallback, D-763), plus the bordered **امسح لإضافة شخص** action
> → the existing `/contacts/scan` (FDS-014). Widget-tested in
> `src/Mobile/simf_app/test/features/badge/badge_screen_test.dart` (issued QR
> card + strip + masked id, add-person → scanner, null-qrId pending state,
> error→retry, Arabic, the mask helper). The old mockup screen + test are
> parked in `_legacy_mockup/`.

| | |
|--|--|
| **Page** | [`Page_032`](../../App/Page_032/README.md) |
| **Route** | `GET /api/v1/app/account/dashboard` · app screen #32 `/badge` |
| **Surface** | Mobile (Flutter) + App API |
| **Auth setup** | **Signed-in (Approved)** — the route is auth-gated and the dashboard read is `RequireApprovedAccount`. (Auth via `Get-Totp`; never a literal secret.) |
| **Last reviewed** | 2026-06-06 |

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB032-001 | Approved visitor with a `qrId` sees the QR badge + their name + the hint | happy | P0 | authored ✓ (screen `an issued qrId renders the QR badge + the name`) |
| E2E-MOB032-002 | A pending account (null `qrId`) sees the "available after approval" state, no QR | edge | P1 | authored ✓ (screen `a null qrId shows the pending state, no QR`) |
| E2E-MOB032-003 | A dashboard read failure → error + Retry that re-fetches | resilience | P0 | authored ✓ (screen `a load failure shows the error + retry, which re-fetches`) |
| E2E-MOB032-004 | Arabic locale renders the Arabic name (RTL) alongside the QR | i18n | P2 | authored ✓ (screen `renders the Arabic name + hint in Arabic`) |
| E2E-MOB032-005 | **Header back chevron → Home (bug fix):** on the Badge tab of the bottom-nav shell the header back chevron returns to the **Home** tab. Previously it was a dead no-op — an in-shell tab never leaves the shell's `/` location, so `backOrHome`'s `goNamed(home)` navigated to `/` while already there | nav | P1 | authored ✓ (`simf_page_shell_test` — `backOrHome on an in-shell tab (nothing to pop) switches the shell to the Home tab`) |
| E2E-MOB032-006 | **Strip tinted by profile-type colour (D-763):** the identity strip fill is the account's `identity.pageColor` (`ProfileType.PageColor`, e.g. VIP `#0E7490`); a null/invalid value falls back to the token gold | happy | P1 | authored ✓ (screen `the identity strip is tinted by the profile-type pageColor` + `... falls back to token gold with no pageColor`) |
| E2E-MOB032-007 | **Badge actions gate on the app ROLE, not `isVisitor` (DEF-EXH-005):** Exhibitor → "Scan visitor badge" only; Visitor (including Media / Sponsor partner types, which resolve to `AppRole.Visitor`) → the two contact actions; Staff / Moderator / not-yet-approved → **no** action button | security | P0 | authored ✓ (screen `an exhibitor sees scan-visitor…`, `a partner-type visitor … never sees scan-visitor (DEF-EXH-005)`, `a Staff badge shows no action button (DEF-EXH-005)`, `a Moderator badge …`) |
| E2E-MOB032-009 | **True guest gets guest copy + a way in (BUG-013):** a visitor with NO account reaching the Badge tab sees "sign in or create an account to get your entry badge" and working Sign in / Create account actions — never the pending-account copy | auth | P1 | authored ✓ (screen `BUG-013 — a TRUE guest gets the guest copy and a working sign-in CTA, never the pending-account copy`) |
| E2E-MOB032-008 | **Back chevron has an accessible name (BUG-003):** the shared circled back control announces the localized "Back" tooltip instead of a bare "button" | a11y | P2 | authored ✓ (`simf_page_shell_test` — `BUG-003 — the circled back button carries an accessible name`) |
| E2E-MOB032-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB032-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB032-001 — The issued QR badge

```gherkin
Feature: Entry badge (QR)
  As an approved visitor
  I want my QR entry badge
  So that I can be scanned at the gate

Scenario: An approved visitor sees a scannable badge
  Given I am signed in with an Approved account that has a qrId
  When the badge screen calls GET /api/v1/app/account/dashboard
  Then a centred QR encoding my qrId is shown in a white card
  And my localized name is shown below it
  And a "show this at entry" hint is shown
```

**Evidence:** screen test `an issued qrId renders the QR badge + the name`.

### E2E-MOB032-002 — Pending account (no badge yet)

```gherkin
Scenario: A pending account has no badge yet
  Given my account has no qrId (pending approval)
  When the badge screen loads
  Then no QR is rendered
  And the "your badge is available after approval" state is shown
```

**Evidence:** screen test `a null qrId shows the pending state, no QR`.

### E2E-MOB032-003 — Error+retry / E2E-MOB032-004 — Arabic

```gherkin
Scenario: A failed read offers a retry
  Given the dashboard read fails
  Then an error + Retry are shown, and Retry re-runs the read

Scenario: Arabic locale renders RTL
  Given the app locale is Arabic and my account has a qrId
  Then the QR is shown and my Arabic name is rendered
```

**Evidence:** screen tests `a load failure shows the error + retry, which re-fetches`,
`renders the Arabic name + hint in Arabic`.

### Note — QR must be scannable in-app (D-743)

The badge QR is rendered as a **standard square** QR (not the old round D-423
style). The round style read on a native phone camera but was **undecodable by
the in-app `flutter_zxing` scanner** (badge sign-in / gate / exhibitor). When
verifying, confirm a badge shown on one device is decoded by the in-app scanner
on another — not only by a phone's native camera. (The shared scanner also now
decodes the **full frame**, so a QR filling the viewfinder still reads.)

### E2E-MOB032-006 — Strip tinted by the profile type's colour (D-763)

```gherkin
Scenario: A VIP visitor's badge strip uses the VIP colour
  Given I am signed in with an Approved account whose profile type is VIP
  And the dashboard identity carries pageColor "#0E7490"
  When the badge screen renders the identity strip
  Then the strip fill is #0E7490 (not the default gold)
  And the name/tier/ID ink stays legible against it

Scenario: A visitor with no profile-type colour falls back to gold
  Given the dashboard identity carries no pageColor
  When the badge screen renders the identity strip
  Then the strip fill is the token gold (SimfTokens.accent)
```

> The colour is a server value (`ProfileType.PageColor`, canonical `#RRGGBB`),
> parsed by `parseHexColor` (`core/utils/hex_color.dart`); an unset/invalid value
> falls back to the token gold. Ink flips to a dark tone only on a genuinely pale
> strip so the name never washes out. When verifying live, confirm each seeded
> tier (Media amber, Sponsor purple, Exhibitor cyan, VVIP red, VIP teal) renders a
> distinct strip on the badge.

**Evidence:** screen tests `the identity strip is tinted by the profile-type
pageColor`, `the identity strip falls back to token gold with no pageColor`.

### E2E-MOB032-005 — Header back chevron returns to Home

```gherkin
Scenario: The back chevron on the Badge tab returns to Home
  Given the user is on the Badge tab of the bottom-nav shell
  When they tap the back chevron in the header
  Then the shell switches to the Home tab
```

> The five bottom-nav tabs render inside `SimfAppShell`'s IndexedStack at the
> shell's `/` location, so `context.canPop()` is false and the old
> `goNamed(home)` was a no-op (navigating to `/` while already at `/`). The
> shared `backOrHome` now switches the shell tab to Home when it can't pop.

**Evidence:** `simf_page_shell_test.dart` — `backOrHome on an in-shell tab
(nothing to pop) switches the shell to the Home tab`.

### E2E-MOB032-007 — Badge actions gate on the app role (DEF-EXH-005)

```gherkin
Scenario: An exhibitor sees only the lead-capture action
  Given I am signed in with an Approved account whose profile type carries
    MobileAppRole.Exhibitor
  When the badge screen renders its actions
  Then only "مسح بطاقة زائر / Scan visitor badge" is shown
  And tapping it opens the exhibitor scan screen (/exhibitor/scan)

Scenario: A partner-type visitor keeps the contact actions
  Given I am signed in with an Approved Media or Sponsor account
    (ProfileType.IsForVisitor = false, MobileAppRole.None → app role "Visitor")
  When the badge screen renders its actions
  Then "امسح لإضافة شخص / Scan to add a contact" and
    "شارك جهة اتصالي / Share my contact" are shown
  And "مسح بطاقة زائر / Scan visitor badge" is NOT shown

Scenario: Staff and Moderator are offered no badge action
  Given I am signed in with an Approved Staff (or Moderator) account
  When the badge screen renders its actions
  Then no action button is shown at all
```

> Before the fix the branch keyed off the dashboard's `identity.isVisitor`, which
> is `false` for **every** partner profile type, so Staff, Moderator, Media and
> Sponsor were all shown the exhibitor-only scan button and the router
> (`_routeRoles[106] = {exhibitor}`) then bounced them home — a visible dead
> control. The button now keys off `CurrentUser.effectiveAppRole`, so a
> not-yet-approved account (which presents as `Guest`, D-666) also sees none.

**Evidence:** screen tests `an exhibitor sees scan-visitor, not the contact
buttons (D-426)`, `a partner-type visitor (isVisitor=false, role Visitor) keeps
the contact actions and never sees scan-visitor (DEF-EXH-005)`, `a Staff badge
shows no action button (DEF-EXH-005)`, `a Moderator badge shows no action button
(DEF-EXH-005)`.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — **security fix: the badge actions
gate on the signed-in app role instead of the dashboard `isVisitor` flag
(E2E-MOB032-007, DEF-EXH-005).** _Prior:_ `2026-07-24` — **feature: the identity strip is now
### E2E-MOB032-007 — A true guest is not shown pending-account copy

```gherkin
Scenario: A visitor with no account at all opens the Badge tab
  Given I have never signed in (no account)
  When I tap the QR tab in the bottom nav
  Then no QR is rendered
  And I do NOT see "your account is not approved yet" or "once your account is approved"
  And I see "Sign in or create an account to get your entry badge."
  And a "Sign in" button and a "Create account" link are offered
  When I tap "Sign in"
  Then the sign-in screen opens
```

> The five bottom-nav tabs switch **inside** `SimfAppShell`'s IndexedStack, so no
> go_router navigation happens and the router's auth redirect never runs — a
> signed-out visitor really does land on this screen. It previously rendered the
> PENDING-account copy, describing a registration the guest never submitted and
> offering no way out (BUG-013). The pending copy is unchanged for genuinely
> pending accounts (E2E-MOB032-002 / the not-approved state).

**Evidence:** screen test `BUG-013 — a TRUE guest gets the guest copy and a
working sign-in CTA, never the pending-account copy`.

---

_Last reviewed:_ `2026-07-26` by `SIMF Team` — **bug fix: the true-guest state on
the Badge tab (E2E-MOB032-007, BUG-013) + an accessible name on the shared back
control (E2E-MOB032-008, BUG-003).** _Prior:_ `2026-07-24` — **feature: the identity strip is now
tinted by the profile type's server colour (`ProfileType.PageColor` → `pageColor`),
gold fallback + luminance-based ink (E2E-MOB032-006, D-763).** _Prior:_ `2026-07-24`
— bug fix: header back chevron on the in-shell Badge tab (E2E-MOB032-005); `2026-07-11`
by `SIMF Team`.
