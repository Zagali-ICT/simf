# E2E test catalogue — `Terms & conditions` (`terms`)

> **Authority:** SIMF E2E catalogue (D-133 / D-245). Mobile screen #9 — the
> Terms & Conditions content + accept gate. Spec:
> [`Page_009`](../../App/Page_009/README.md). Runner-agnostic Gherkin. The screen
> glue is widget-tested in
> `src/Mobile/simf_app/test/features/content/terms_screen_test.dart`; the content
> model in `…/content/content_models_test.dart`. The backend read is covered by
> `tests/SIMF.Api.Tests/CmsTests.cs`.

| | |
|--|--|
| **Page** | [`Page_009`](../../App/Page_009/README.md) (App page docs) |
| **Route** | app screen #9 `terms` → `/terms` (standalone) · `/terms?consent=1` (in-flow gate). **Anonymous** (not auth-gated). |
| **APIs** | `GET /api/v1/app/content/terms` → `ApiResult<PublicContentBlock>` = `{ key, content, contentArabic, lastUpdatedAt }` (anonymous; `Last-Modified`/`If-Modified-Since` 304 handshake). **No accept write** — acceptance is client-side only (D8). |
| **Surface** | Mobile (Flutter) — Guest and above |
| **Auth setup** | None — the terms read is anonymous. |
| **Last reviewed** | 2026-06-11 |

> **KSA-Project redesign (D-367, Figma 505:1553):** the body now renders as
> gold-hairline **bullet cards** (one per non-empty body line) under the
> معلومات هامة لزوار الملتقى heading; in consent mode the interim
> checkbox + Decline are replaced by one always-enabled gold **موافق** button —
> the explicit tap IS the consent (client-side only, D8; the back chevron
> declines). Load/empty/404/error contract unchanged; the old screen is
> parked in `lib/features/_legacy_mockup/`. Consent-gate scenarios referring
> to the checkbox should be read against the new single-button gate.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOB009-001 | Standalone read → bullet-card body (active locale), no last-updated line, موافق present but non-binding (D-375) | happy | P0 | authored ✓ (widget test) |
| E2E-MOB009-002 | In-flow consent (`?consent=1`) → the always-enabled gold موافق button (D-367: the explicit tap is the consent — no checkbox) | happy | P0 | authored ✓ (widget test) |
| E2E-MOB009-003 | Accept (ticked) → client-side consent only (no server call) → control returns to the caller (`pop`) | happy | P0 | authored (screen — `pop(true)`) |
| E2E-MOB009-004 | Decline / back → no consent recorded; caller stays blocked | edge | P1 | authored (screen — `pop(false)`) |
| E2E-MOB009-005 | Missing/inactive key (404) → empty state ("No content") + retry | edge | P0 | authored ✓ (widget test) |
| E2E-MOB009-006 | Transport / 5xx → error message + single retry that reloads | resilience | P1 | authored ✓ (widget test) |
| E2E-MOB009-007 | Localized body — Arabic primary, English fallback when one side is blank | i18n | P1 | authored ✓ (model test) |
| E2E-MOB009-008 | RTL render (Arabic) — title, body, gate mirror | i18n | P1 | authored (screen) |
| E2E-MOB009-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-MOB009-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-MOB009-001 — Standalone read

```gherkin
Feature: Terms & conditions
Scenario: A guest reads the terms from a link
  Given a guest opens /terms (no consent flag)
  When GET /app/content/terms returns the terms block
  Then the localized body renders as gold-hairline bullet cards (frame 505:1553)
  And there is NO "Last updated" line (removed per the frame — D-375)
  And the gold موافق button shows here too (D-375 — per the frame it is
      always present; in standalone it simply leaves the page, no consent)
```

**Evidence:** `terms_screen_test` — "standalone mode renders the body, no
last-updated line, and the always-visible Agree button (D-375 — frame 505:1553)".

### E2E-MOB009-002 — In-flow consent gate

```gherkin
Scenario: A consent step shows the agree gate (D-367)
  Given the page is opened in-flow as /terms?consent=1
  Then the bullet-card terms render with a single gold "موافق / Agree" button
  And the button is always enabled — the explicit tap IS the consent
  And there is no checkbox row
```

**Evidence:** `terms_screen_test` — "consent mode shows the always-enabled Agree
button (D-367)".

### E2E-MOB009-003 — Accept is client-side only (D8)

```gherkin
Scenario: Accepting records consent locally and returns to the caller
  Given the in-flow gate is shown and the checkbox is ticked
  When the user taps "Accept & continue"
  Then NO request is sent (there is no accept endpoint — D8)
  And control returns to the calling flow (the screen pops with a positive result)
```

> HARD RULE (Page_009 L-3 / D8): there is no `POST …/terms/accept` in this version;
> acceptance is a local flag handed back to the caller. Re-entry shows the gate again
> (no server memory of consent).

### E2E-MOB009-004 — Decline

```gherkin
Scenario: Declining records nothing
  Given the in-flow gate is shown
  When the user taps the back chevron (or system back) instead of موافق
  Then no consent is recorded and the calling flow stays blocked (the screen pops with a negative result)
```

### E2E-MOB009-005 — Missing key → empty state

```gherkin
Scenario: No terms content is stored
  Given GET /app/content/terms returns 404 (key unstored / inactive)
  Then the screen shows "No content" with a Retry button (never a blank page)
```

**Evidence:** `terms_screen_test` — "an empty body shows the empty state with retry" + "a 404 is treated as the empty state".

### E2E-MOB009-006 — Transport error → retry

```gherkin
Scenario: The content fetch fails
  Given GET /app/content/terms fails (network / 5xx)
  Then the screen shows the failure message + a Retry button
  When the user taps Retry and the call succeeds
  Then the terms body renders
```

**Evidence:** `terms_screen_test` — "a transport error shows the message + retry, which reloads".

### E2E-MOB009-007 — Localized body + fallback

```gherkin
Scenario: The body follows the active locale with a fallback
  Given a block with both Arabic and English bodies
  Then the Arabic body shows under the Arabic locale and the English under English
  And when one side is blank the other is shown (Arabic primary, English secondary)
```

**Evidence:** `content_models_test` — "localizedBody picks the active locale and falls back".

### E2E-MOB009-008 — RTL render (Arabic)

```gherkin
Scenario: The page mirrors under Arabic
  Given the app language is Arabic
  Then the title, body, and (in-flow) the accept gate mirror right-to-left
```

> By construction: localized `AppL10n` strings + Material RTL; the body is rendered
> for the active locale.

---

_Last reviewed:_ `2026-06-11` by `SIMF Team`.
