# E2E test catalogue — `Entry badge` (`badge`)

> **Authority:** SIMF E2E test catalogue template (D-133). Mobile catalogue — the
> dashboard read is already built (`GET /app/account/dashboard`,
> `RequireApprovedAccount`, D-300); the screen consumes **only** the identity
> (`qrId`, `fullNameEn`/`fullNameAr`) from it and reuses the My-Area data layer
> (`MyAreaRepository.getDashboard()` + `MyAreaDashboard` — no duplicate model).
> The **Flutter screen is built** and was **rebuilt to KSA Wave-2 frame
> 221:769 "QR"** (D-378 batch): the gold-bordered white card (QR 230 +
> "امسح للدخول" + the gold identity strip with avatar/name/tier and the
> **masked `ID · •••• tail`**), plus the bordered **امسح لإضافة شخص** action
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

---

_Last reviewed:_ `2026-07-24` by `SIMF Team` — **bug fix: the header back chevron
on the in-shell Badge tab was a dead no-op; the shared `backOrHome` now switches the
shell to the Home tab when there is nothing to pop (E2E-MOB032-005).** _Prior:_
`2026-07-11` by `SIMF Team`.
