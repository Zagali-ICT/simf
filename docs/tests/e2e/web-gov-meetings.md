# E2E test catalogue — Website "Government business meetings" (`/programme/gov-meetings`)

| | |
|--|--|
| **Page** | [`web/gov-meetings.md`](../../pages/web/gov-meetings.md) |
| **Route** | `/programme/gov-meetings` |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Test runner** | Chrome DevTools MCP (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | **None — anonymous and static.** No API call, no bearer token, no seeding. |
| **Figma** | KSA Maritime Forum — Government Business Meetings (Desktop AR), node `5867-23988` — **a pure stub** |
| **Last reviewed** | 2026-07-18 |

> **What this page is.** `/programme/gov-meetings` (`GovMeetings.razor`) is the
> Website's public, anonymous **government-business-meetings (B2G)** page (Figma
> `5867-23988`) — the fourth Programme-cluster page. Static SSR on the shared
> `LandingShell` chrome, no CRUD. Two sections:
> 1. **Interior hero** (`ln-pghero`, via `LandingPageHero`) — photo + gradient, the
>    single `<h1>`, a subtitle and the venue + date pills. **No breadcrumb.**
> 2. **Intro + CTA** (`ln-fsection` → `ln-venue`) — a title + subtitle, then a centred
>    card (briefcase icon + heading + description) with a "register your interest"
>    `mailto:` CTA.
>
> **Content note.** The Figma frame is a pure stub; this page shows real minimal
> content instead (see `web/gov-meetings.md` §7).
>
> **Auth model (Website, anonymous).** No `RequirePermission`, no `/not-permitted`,
> no `/login` redirect.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WGBM-001 | Golden path — hero + intro card render + shared chrome, one `<h1>` | happy | P0 | _to author_ |
| E2E-WGBM-002 | Hero has NO breadcrumb (Programme cluster); the venue + date pills render | happy | P1 | _to author_ |
| E2E-WGBM-003 | Intro card — a `ln-venue` card (icon + heading + body) with a `mailto:info@simforum.mod.gov.sa` "register your interest" CTA | happy | P1 | _to author_ |
| E2E-WGBM-004 | Static/anonymous — no `/api/...` request, no Authorization header, no `/login` or `/not-permitted` redirect | auth | P0 | _to author_ |
| E2E-WGBM-005 | RTL / Arabic ⇄ LTR / English — hero + content mirror; Arabic content in AR, English in EN | i18n | P1 | _to author_ |
| E2E-WGBM-006 | Responsive — the centred card holds; no horizontal overflow at 1440/1024/768/390 both languages | responsive | P1 | _to author_ |
| E2E-WGBM-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-WGBM-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | 2026-07-29 PASS (LTR+RTL) |

## Scenarios

### E2E-WGBM-001 — Golden path

```gherkin
Feature: Website Government-business-meetings page describes the B2G programme
  As any visitor (anonymous or signed in)
  I want to know about the government business meetings
  So that I can register my interest to take part

Background:
  Given the Website is reachable
  And the browser is a fresh anonymous session (no auth cookie, no bearer token)

Scenario: The page renders the hero + the intro card
  When the browser opens /programme/gov-meetings
  Then NO request to /api/... is made (the page is static)
  And the shared header + footer render (LandingShell chrome)
  And an interior hero (section.ln-pghero) renders with exactly one <h1>
  And a section (section.ln-fsection) renders one .ln-venue intro card
  And the page title is "Government business meetings — Saudi International Maritime Forum"
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-gov-meetings-ar-1440.png` (AR) + `web-gov-meetings-en-1440.png` (EN)
- Console errors: 0 expected
- Network: no `/api/v1/...` request; the hero photo returns 200
- Audit row: none

### E2E-WGBM-002 — Hero without breadcrumb

```gherkin
Scenario: The Programme-cluster hero omits the breadcrumb
  When the browser opens /programme/gov-meetings
  Then NO .ln-pghero__crumbs breadcrumb element is present
  And the hero renders its <h1> title, subtitle and two .ln-pghero__pill pills
```

### E2E-WGBM-003 — Intro card + CTA

```gherkin
Scenario: The intro card shows the description and a register-interest CTA
  When the browser opens /programme/gov-meetings
  Then section.ln-fsection renders its title (GovMeetings.Section.Title) + subtitle
  And one .ln-venue card renders with a .ln-venue__icon (briefcase), a heading and body text
  And the card's a.ln-btn "register your interest" CTA href starts with "mailto:info@simforum.mod.gov.sa"
```

### E2E-WGBM-004 — Static / anonymous

```gherkin
Scenario: The page loads anonymously and fires no API request
  Given a fresh browser with no auth cookie and no bearer token
  When the user opens /programme/gov-meetings directly
  Then the page renders WITHOUT redirecting to /login or /not-permitted
  And NO request to /api/... is made and no Authorization header is sent

Scenario: A signed-in session changes nothing
  Given an Approved Visitor is signed in on the Website
  When they open /programme/gov-meetings
  Then the rendered page is identical to the anonymous view
```

### E2E-WGBM-005 — RTL / LTR

```gherkin
Scenario: The page mirrors between Arabic and English
  When the browser opens /programme/gov-meetings under the Arabic UI culture (<html dir="rtl" lang="ar">)
  Then the page mirrors right-to-left, the hero photo sits on the LEFT, content renders Arabic

  When the browser opens /programme/gov-meetings under the English UI culture (<html dir="ltr" lang="en">)
  Then the hero photo flips to the RIGHT and the content renders English
```

### E2E-WGBM-006 — Responsive

```gherkin
Scenario: The card holds with no horizontal overflow
  When the browser opens /programme/gov-meetings and the viewport width is set to each of 1440, 1024, 768, 390
  Then the centred .ln-venue card renders (padding tightens below 560px)
  And the hero block goes full-width below 720px
  And at every width in {1440, 1024, 768, 390} document.scrollWidth == document.clientWidth (no horizontal overflow)
  And this holds in BOTH the EN (LTR) and AR (RTL) cultures
```

---

## Implementation notes

- **Read-only, anonymous, static, no CRUD.** The only interaction is the `mailto` CTA.
  The matrix above is exhaustive.
- **Stub Figma frame → real minimal content.** The Figma is an un-customised Organizer
  clone; this page shows a real description + CTA. A full design (agenda / booking flow)
  awaits a real Figma frame (`web/gov-meetings.md` §7).
- **Lower-layer coverage:** component (bUnit, no browser)
  `tests/SIMF.Web.Tests/GovMeetingsPageTests.cs` pins the render + the CTA.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a `.feature`
  under `tests/SIMF.E2E.Tests/`.

---

_Last reviewed:_ 2026-07-18 by Claude (Government business meetings page — `ln-` Bootstrap SSR, Figma 5867-23988 [stub]).
