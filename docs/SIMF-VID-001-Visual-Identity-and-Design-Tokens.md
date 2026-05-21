# Visual Identity and Design Tokens

| Field | Value |
|-------|-------|
| Document ID | SIMF-VID-001 |
| Title | Visual Identity and Design Tokens |
| Version | 1.2 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-CPD-001, SIMF-MAA-001, SIMF-SES-001, SIMF-CON-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. Captured from the client visual identity guide. |
| 1.1 | 2026-05-20 | Engineering & Architecture Team | Font approved by the owner; Regular weight file recorded; OI-1 narrowed to obtaining the remaining weights. |
| 1.2 | 2026-05-20 | Engineering & Architecture Team | Added §5.4 — the RSNF emblem registered as the owning-entity mark; OI-2 updated. |

---

## 1. Purpose

This document carries the SIMF brand into engineering. The client issued a
visual identity guide for SIMF 2026; this document restates its rules in a form
the team builds from, and turns them into named design tokens. The Control
Panel and the mobile app both take their colours, typography and visual
treatment from here.

The rule is simple: **the Control Panel design follows this document, and this
document follows the client's visual identity guide.** Where the two ever
disagree, the client's guide wins and this document is corrected.

## 2. Scope

This document covers the brand as it applies to the SIMF software surfaces —
the Control Panel, the website and the mobile app: the logo, the colour
palette, the typography, the supporting pattern, and the design tokens derived
from them.

It is not the full brand manual. Print, signage, merchandise and event
production are governed by the client's guide directly and are out of scope
here.

## 3. Source

The single source for this document is the client deliverable:

> **دليل الهوية البصرية — الملتقى البحري السعودي الدولي 2026**
> (Visual Identity Guide — Saudi International Maritime Forum 2026), 22 pages.
> File: `15-04-2024/دليل هويه البصريه د copy.pdf`.

Every value in this document is taken from that guide. Nothing here is invented.
Where the guide leaves something to be derived — for example the neutral and
state colours a user interface needs beyond the five brand colours — this
document says so and defers the derivation to the Control Panel design
(SIMF-CPD-001), rather than guessing a value.

## 4. Brand essence

The forum's identity is built on five values, carried from the guide:
partnerships (الشراكات), development (التطوير), innovation (الابتكار), maritime
security (الأمن البحري), and resilience (المرونة).

The forum statement: *the future of seabed security and supply chains in a
changing global environment* (مستقبل أمن قاع البحار وسلاسل الإمداد في بيئة
عالمية متغيرة).

The visual character the guide asks for is **minimal, elegant and calm**. The
interface should feel ordered and institutional, not busy. This is a direct
constraint on the Control Panel: restraint over decoration.

## 5. Logo

### 5.1 Composition

The logo's symbol is built from three elements, each with a meaning:

- **Compass (البوصلة)** — leadership and strategic direction in modern
  navigation.
- **Palm (النخلة)** — the Kingdom of Saudi Arabia.
- **Anchor (المرساة)** — stability and maritime security.

### 5.2 Rules

| Rule | Requirement |
|------|-------------|
| Clear space | Keep a clear margin of at least **40 pt** on every side of the logo. No text, image or graphic enters that space. The guide expresses the unit as `X = 40 pt`. |
| Minimum size | For digital use the logo width is **not less than 200 pt**. Below that, use the **symbol only**, without the wordmark. |
| Placement | The primary logo sits on the **right** in most applications. It may be **centred** in some uses. Keep breathing room around it. |
| Negative version | A **white (negative)** version is used on dark backgrounds. |
| Symbol alone | The symbol may be used on its own when space is tight or the context calls for it. |
| Partnership lockup | The forum logo goes on the **right**, the partner logo on the **left**, separated by a thin line, with the two sizes visually balanced. |

### 5.3 Misuse

The logo is never stretched or re-proportioned, never given effects (shadow,
glow, gradient overlay), and the wordmark is never moved relative to the symbol.
The logo is not used at all if it has lost its clarity.

For the software surfaces this means the logo is delivered as a clean vector
(SVG) asset in both the full and symbol-only forms, and in the standard and
negative versions. It is placed, never edited.

### 5.4 The RSNF emblem

SIMF is owned by the Royal Saudi Naval Forces. The official **RSNF emblem** is a
separate mark from the SIMF forum logo: a ship's wheel with an anchor inside a
green palm wreath, under the royal crown, with the Saudi national emblem at its
centre and the force's name — القوات البحرية الملكية السعودية — on a banner
below. Its colours are its own (royal blue, green, gold, brown, red) and are not
the SIMF palette in section 6.

The RSNF emblem is the **owning-entity mark**. It is used in official contexts
and as the partner mark in the lockup described in section 5.2. It is a fixed
official emblem: it is placed and scaled only, never recoloured, redrawn or
altered, and it keeps its own clear space.

The client has provided the coloured version as the file
`RSNF LOGO-COLORED (1).pdf`. A vector (SVG) version is requested for the
software surfaces. See OI-2.

## 6. Colour

### 6.1 Palette

The guide defines a five-colour palette.

| Swatch | Hex | Guide role |
|--------|-----|------------|
| Deep navy | `#244A77` | Primary |
| Mid blue | `#498FBD` | Secondary |
| Bright blue | `#007CD8` | Secondary / accent |
| Gold | `#E8C060` | Secondary / accent |
| Light neutral | `#F1F2F2` | Neutral |

### 6.2 Balance

The guide sets how much of each colour appears: navy dominates, the other blues
support, and gold is a small accent.

| Colour | Share |
|--------|-------|
| `#244A77` deep navy | 70% |
| `#498FBD` mid blue | 15% |
| `#007CD8` bright blue | 10% |
| `#E8C060` gold | 5% |

`#F1F2F2` is the light neutral and sits outside the ratio — it carries surfaces
and backgrounds.

The practical reading for an interface: navy is the structural colour
(headers, primary actions, key surfaces), the two blues handle secondary
elements and links, gold is reserved for small highlights and must stay rare,
and the light neutral is the background. Gold is an accent — using it widely
breaks the brand.

### 6.3 What still has to be derived

A working interface needs more than five colours: a near-black for body text,
greys for borders and disabled states, and success, warning and error colours.
The guide does not give these. They are **derived from this palette during the
Control Panel design** (SIMF-CPD-001) and added to the token set there. They are
not invented in this document.

## 7. Typography

### 7.1 Typeface

The brand typeface is **FS Albert Arabic**, used for **both Arabic and English**.
One family covers both scripts, which keeps the bilingual interface consistent.

Weights available: Thin, Light, Regular, Bold, Extra Bold.

### 7.2 Usage

| Use | Weight |
|-----|--------|
| Headings | **Bold** |
| Body text | **Regular** |

The guide also asks that colour help separate headings from body, and sets a
clear size order: a large, clear main title; medium sub-headings; small,
readable body text. The exact pixel sizes of the type scale are set in
SIMF-CPD-001 for the Control Panel and in SIMF-MAA-001 for the app, built on
this Bold-heading / Regular-body rule.

### 7.3 The font file

The owner has **approved** FS Albert Arabic for SIMF. The font is provided as
the file `alfont_com_AlFont_com_FSAlbertArabic-Regular`, which is the **Regular**
weight. The Regular weight covers body text.

The **Bold** weight — used for headings per section 7.2 — and any other weights
the design needs are obtained from the same source before the Control Panel
theme is finalised. Until the Bold file is in hand, headings fall back to the
heaviest available weight; the design does not substitute a different typeface.
See OI-1.

## 8. The pattern

The guide includes a supporting pattern: the logo's core element (the compass
with the anchor) repeated on a regular grid. It is a visual extension of the
logo.

Rules for using it in the software surfaces:

- Use only the identity colours — shades of blue.
- Do not change the shape or proportions of the repeated element.
- Keep it at **low opacity, 10%–30%**.
- Do not place it directly behind text unless its density is reduced enough to
  keep the text fully readable.
- Keep even spacing between the repeated elements.
- It supports the identity; it never competes with the logo or the content.

In the Control Panel this means the pattern is, at most, a quiet background
texture on a few surfaces — a sign-in screen, an empty state, a header band. It
does not sit behind working data tables or forms.

## 9. Design tokens

This is the bridge from the brand to the code. These named tokens are the
SIMF brand tokens. They are implemented in `theme.tokens.css` for the Control
Panel (per SIMF-SES-001 section 6.2) and in the Flutter theme for the app (per
SIMF-MAA-001 section 11). A surface uses a token; it never uses a raw hex value.

### 9.1 Colour tokens

| Token | Value | Use |
|-------|-------|-----|
| `--color-primary` | `#244A77` | Primary structural colour; headers, primary actions |
| `--color-secondary` | `#498FBD` | Secondary elements |
| `--color-accent-blue` | `#007CD8` | Links, secondary accents |
| `--color-accent-gold` | `#E8C060` | Small highlights only; used sparingly |
| `--color-neutral` | `#F1F2F2` | Background and light surfaces |

Functional tokens — `--color-text`, `--color-border`, `--color-surface`,
`--color-success`, `--color-warning`, `--color-error`, and the disabled and
hover states — are derived from this set in SIMF-CPD-001 and registered there.
They are listed as derived, not given a value here.

### 9.2 Typography tokens

| Token | Value |
|-------|-------|
| `--font-family-base` | `"FS Albert Arabic"` (Arabic and English) |
| `--font-weight-heading` | Bold |
| `--font-weight-body` | Regular |
| `--font-weight-light` | Light |
| `--font-weight-emphasis` | Extra Bold |

The type scale tokens (`--font-size-title`, `--font-size-subtitle`,
`--font-size-body`, and their line heights) are set in SIMF-CPD-001 and
SIMF-MAA-001, following the large-title / medium-subtitle / small-body order
from section 7.2.

### 9.3 Logo and pattern assets

| Asset | Form |
|-------|------|
| Logo — full, standard | SVG |
| Logo — full, negative (white) | SVG |
| Logo — symbol only, standard | SVG |
| Logo — symbol only, negative | SVG |
| Pattern tile | SVG, identity blues, used at 10%–30% opacity |
| RSNF emblem — coloured | Provided as `RSNF LOGO-COLORED (1).pdf`; SVG requested |

These are supplied as vector assets and placed unedited. Obtaining the
outstanding vector files from the client is OI-2.

## 10. How this applies to the surfaces

### 10.1 Control Panel

The Control Panel is designed in-house, so it follows this document directly.
SIMF-CPD-001 builds the Control Panel theme from these tokens: navy-led
structure, the 70/15/10/5 colour balance, gold kept rare, FS Albert Arabic with
Bold headings and Regular body, and the pattern only as a quiet background on a
few surfaces. The multi-theme support required by SIMF-CON-001 — including a
dark theme — keeps these brand tokens as its base; the dark theme uses the
negative logo and a navy-derived dark surface set, mapped in SIMF-CPD-001.

### 10.2 Mobile app

The app's visual design is produced by the external UI/UX designer. This
document is given to that designer as the brand constraint, and the design they
deliver is checked against it. The Flutter theme tokens in SIMF-MAA-001 section
11 are populated from section 9 here.

### 10.3 Website

The public website follows the same tokens, so the three surfaces stay
consistent.

## 11. Open items

| ID | Item | Needed for |
|----|------|-----------|
| OI-1 | Obtain the Bold and any other required weights of FS Albert Arabic. The font is approved and the Regular weight (`alfont_com_AlFont_com_FSAlbertArabic-Regular`) is provided. | Section 7 |
| OI-2 | Obtain the outstanding vector (SVG) assets: the SIMF forum logo (standard and negative, full and symbol-only) and the pattern tile, plus an SVG of the RSNF emblem. The RSNF emblem coloured version is provided as `RSNF LOGO-COLORED (1).pdf`. | Sections 5.4, 9.3 |
| OI-3 | Derive and register the functional and state colours, and the type scale, in SIMF-CPD-001 | Sections 6.3, 9.1, 9.2 |
| OI-4 | Confirm the dark-theme token mapping in SIMF-CPD-001 | Section 10.1 |

---

End of document.
