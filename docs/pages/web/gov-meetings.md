# Website "Government business meetings" — `/programme/gov-meetings`

| | |
|--|--|
| **Route** | `/programme/gov-meetings` — Blazor SSR Razor page (static render) |
| **Surface** | Website (public marketing site — `ln-` Bootstrap SSR) |
| **Audience** | Anyone (public) |
| **Auth** | None — anonymous |
| **Status** | ✅ Real minimal — bilingual, responsive; static. The Figma frame is a pure stub, so this shows real minimal content (see §7) |
| **Source** | [`GovMeetings.razor`](../../../src/Website/SIMF.Web/Components/Pages/GovMeetings.razor) · [`LandingPageHero.razor`](../../../src/Website/SIMF.Web/Components/Layout/LandingPageHero.razor) · [`landing.css`](../../../src/Website/SIMF.Web/wwwroot/css/landing.css) (reused `ln-pghero` / `ln-fsection` / `ln-venue` / `ln-btn`) |
| **Strings** | [`Strings.resx`](../../../src/Website/SIMF.Web/Resources/Strings.resx) / [`Strings.ar.resx`](../../../src/Website/SIMF.Web/Resources/Strings.ar.resx) (`GovMeetings.*`) |
| **Data** | None — static |
| **Figma** | KSA Maritime Forum — Government Business Meetings (Desktop AR), node `5867-23988` — **a pure stub (un-customised Organizer clone)** |
| **E2E** | [`e2e/web-gov-meetings.md`](../../tests/e2e/web-gov-meetings.md) (`E2E-WGBM-*`) |

## 1. Purpose

The forum's **government business meetings** (B2G) — the fourth (final) page of the
Programme cluster. A **Blazor SSR** page on the shared `ln-` chrome: the interior
photo-hero (no breadcrumb), then a brief description of the meetings + a
"register your interest" CTA.

## 2. Architecture

- **Rendering** — static SSR (no API). Shared chrome via `LandingShell`; the hero is
  the reusable `LandingPageHero` (no breadcrumb) and carries the page's single `<h1>`.
- **Intro card** — the section reuses `ln-fsection` (header) + the `ln-venue` centred
  info card (a briefcase icon, a heading, a description and a primary CTA). The CTA is a
  `mailto:info@simforum.mod.gov.sa` "register your interest" link. No new CSS, no
  code-behind, no new assets.
- **Content** — all copy is `GovMeetings.*` resx keys (this page has its own hero
  subtitle key). The hero backdrop reuses the shared cluster photo.

## 3. Sections

| # | Section | Class | Content |
|---|---------|-------|---------|
| 1 | Interior hero (no breadcrumb) | `ln-pghero` (via `LandingPageHero`) | `<h1>` (`GovMeetings.Hero.Title`) + subtitle (`GovMeetings.Hero.Subtitle`) + venue + date pills |
| 2 | Intro + CTA | `ln-fsection` → `ln-venue` | Title (`GovMeetings.Section.Title`) + sub + a card: briefcase icon + heading (`GovMeetings.Card.Title`) + body (`GovMeetings.Card.Body`) + a `mailto` CTA (`GovMeetings.Cta`) |

## 4. Bilingual model (AR RTL / EN LTR)

- **All copy** → resx (`GovMeetings.*`), following the `/culture` switch.
- **Direction** — the reused `ln-fsection` + `ln-venue` are direction-agnostic.

## 5. Responsive

The reused `ln-venue` card is a single centred column (max-width 720px); the hero block
goes full-width below 720px. No horizontal overflow at 1440 / 1024 / 768 / 390
(`scrollWidth == clientWidth` verified in both languages).

## 6. Verification (2026-07-18)

- **Build** — `dotnet build -c Release` 0 warnings / 0 errors.
- **Component tests** — `tests/SIMF.Web.Tests/GovMeetingsPageTests.cs` (2, green):
  single-`<h1>` with no breadcrumb; the intro `ln-venue` card + the `mailto`
  "register your interest" CTA.
- **Live render** — visually verified at **AR@1440** and **EN@1440** (correct RTL→LTR
  mirror): the hero + the centred intro card (briefcase icon, heading, body, CTA).
  Console clean; no horizontal overflow.

## 7. Follow-ups — content flagged (this was a stub Figma frame)

The Figma frame `5867-23988` is a **pure stub** — an un-customised clone of the
Organizer placeholder (same "الجهة المنظمة" title + MOD cards, no gov-meetings design).
Rather than replicate the placeholder, this page shows a **real minimal description +
CTA**. **When a real design exists, extend this shell** — likely a meetings agenda /
booking flow — and confirm the copy (and whether the CTA should be a form rather than a
`mailto`) with the client.

Minor (shared DRY): `ln-venue` is a "venue"-named centred info card reused here for a
non-venue card; a rename to a generic `ln-infocard` belongs in the deferred DRY pass
([`about.md`](about.md) §7).

_Last reviewed:_ 2026-07-18 by Claude (Government business meetings page — `ln-` Bootstrap SSR, Figma 5867-23988 [stub]).
