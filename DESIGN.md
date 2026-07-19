---
name: SIMF Control Panel
description: The operational console for the Saudi International Maritime Forum — institutional, navy-led, flat, multi-theme.
colors:
  primary-navy: "#244A77"
  secondary-blue: "#498FBD"
  accent-blue: "#007CD8"
  accent-gold: "#E8C060"
  neutral-bg: "#F1F2F2"
  surface: "#FFFFFF"
  surface-sunken: "#F7F8F8"
  text: "#1A2433"
  text-muted: "#5A6573"
  border: "#E2E5E9"
  link: "#007CD8"
  success: "#2E7D5B"
  warning: "#C9912F"
  error: "#B3261E"
typography:
  title:
    fontFamily: "FS Albert Arabic, Segoe UI, Tahoma, Arial, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 700
    lineHeight: "2rem"
  subtitle:
    fontFamily: "FS Albert Arabic, Segoe UI, Tahoma, Arial, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 700
    lineHeight: "1.625rem"
  body:
    fontFamily: "FS Albert Arabic, Segoe UI, Tahoma, Arial, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: "1.375rem"
  caption:
    fontFamily: "FS Albert Arabic, Segoe UI, Tahoma, Arial, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 400
    lineHeight: "1.125rem"
rounded:
  field: "6px"
  card: "10px"
  sm: "4px"
  md: "8px"
  lg: "12px"
spacing:
  s2: "0.5rem"
  s4: "1rem"
  s6: "1.5rem"
  s7: "2rem"
components:
  button-primary:
    backgroundColor: "{colors.primary-navy}"
    textColor: "{colors.surface}"
    rounded: "{rounded.field}"
    padding: "0.5rem 1.25rem"
  button-primary-hover:
    backgroundColor: "#1D3D63"
    textColor: "{colors.surface}"
  button-ghost:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.primary-navy}"
    rounded: "{rounded.field}"
  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.field}"
    padding: "0.5rem 0.75rem"
  card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.card}"
    padding: "1.5rem"
---

# Design System: SIMF Control Panel

## 1. Overview

**Creative North Star: "The Ship's Bridge"**

A calm operational bridge: every instrument legible at a glance, nothing blinking
for attention's sake, the whole console trustworthy under load. The SIMF Control
Panel is a state instrument for running the Royal Saudi Naval Forces' maritime
forum, so it carries naval-institutional gravity, not product-marketing shine.
Deep navy is the structural voice; the surface is quiet and tinted; colour speaks
only when it means something (a primary action, a link, a status).

The system is **restrained and flat by default**. One accent family (navy →
bright blue) carries structure and interaction; gold is a rare highlight, never a
theme. Surfaces sit flat at rest and lift only in response to state. Density is
comfortable, not cramped: generous structure so an operator at a registration
desk, mid-task and under a queue, finds the one primary action without hunting.
It is bilingual by construction — English and Arabic (RTL) are equals, so layout
is built on logical properties and mirrors cleanly.

It explicitly rejects the consumer-playful (bright rounded everything, emoji),
the generic AI-slop SaaS (identical icon-card grids, gradient hero-metric
templates), the crypto-neon/flashy (glows, gradients everywhere), and the
cluttered legacy-ERP (wall-to-wall raw tables with no hierarchy).

**Key Characteristics:**
- Navy-led, restrained palette; accent ≤10% of any screen.
- Flat by default; a single soft card shadow, used sparingly.
- Three first-class themes: light (default), dark, grey.
- Bilingual EN/AR + RTL as a baseline, never an add-on.
- Comfortable density with clear one-primary-action hierarchy.

## 2. Colors

A deep-navy structural palette on tinted near-white neutrals, with one bright-blue
interaction accent and a rare gold highlight. Status colours are muted, never the
brand gold.

### Primary
- **Deep Navy** (#244A77): the structural voice — primary actions, the sign-in
  brand panel, headings/wordmark, active nav. The colour the system is "made of".
- **Action Navy, hover/active** (#1D3D63 / #172F4D): the primary action darkened
  ~10% / ~20% in lightness for hover and pressed states.

### Secondary
- **Mid Blue** (#498FBD): secondary structural elements; the dark theme's primary
  action (readable on a dark surface); selected seats.

### Tertiary
- **Bright Blue** (#007CD8): links and the focus ring only. The interactive
  signal.
- **Brand Gold** (#E8C060): a rare highlight, used sparingly. Never a status
  colour, never a fill.

### Neutral
- **Brand Neutral** (#F1F2F2): the page background in the light theme.
- **Surface** (#FFFFFF) / **Sunken** (#F7F8F8): cards and fields / insets.
- **Ink** (#1A2433): body text and headings — a near-black navy, never `#000`.
- **Muted Ink** (#5A6573): secondary text, captions, helper text.
- **Border** (#E2E5E9): field and card borders, dividers.

### Status (muted, with paired tint surfaces)
- **Success** (#2E7D5B), **Warning** (#C9912F), **Error** (#B3261E), each with a
  low-chroma surface tint (e.g. error surface #F7E5E4) for banners.

### Named Rules
**The One Accent Rule.** Bright blue (#007CD8) is links and focus only. Navy
carries structure and primary action. Gold is a highlight measured in pixels, not
regions. If a screen reads as "blue and gold", the gold is wrong.

**The Tinted-Neutral Rule.** Never `#000` or `#fff` for text or page chrome —
neutrals are tinted toward navy (ink #1A2433, neutral #F1F2F2). Pure black/white
reads as unfinished here.

## 3. Typography

**Brand Font:** FS Albert Arabic (covers Arabic + English; files pending per
SIMF-VID-001 OI-1, so the stack falls back to Segoe UI, Tahoma, Arial — the
typeface is fallen-back, never substituted).
**Mono Font:** ui-monospace, Consolas, Liberation Mono (log viewer, code cells,
TOTP secrets).

**Character:** A single humanist sans across both scripts — plain, legible,
institutional. Hierarchy comes from weight (400 body, 700 heading, 800 emphasis)
and a measured scale, not from decorative display faces. This is a working
console, not a magazine.

### Hierarchy
- **Title** (700, 1.5rem/2rem): the page / screen title — one per screen.
- **Subtitle** (700, 1.125rem/1.625rem): section and brand-panel headings.
- **Body** (400, 0.875rem/1.375rem): body text, labels, fields, buttons. Cap
  prose at 65–75ch.
- **Caption** (400, 0.75rem/1.125rem): helper text, captions, error messages.
- **Label** (700, 0.75rem, slight tracking): table column headers and small
  eyebrow labels.

### Named Rules
**The Weight-Not-Size Rule.** This is an admin tool: there is no giant display
type. Separate hierarchy levels by weight and a ≥1.25 step, not by inflating font
sizes into a marketing hero.

## 4. Elevation

**Flat by default.** The system conveys depth through tonal layering (background →
sunken → surface), not shadow. There is essentially one shadow token, soft and
low, reserved for genuinely floating surfaces (the auth card, modals); everything
else is delineated by a 1px tinted border and a tonal step.

### Shadow Vocabulary
- **Card** (`box-shadow: 0 1px 2px rgba(26,36,51,.04), 0 4px 12px rgba(26,36,51,.06)`):
  the only ambient elevation — the sign-in card, modals. (Dark theme deepens it.)
- **Small** (`box-shadow: 0 1px 3px rgba(26,36,51,.12)`): rare, for popovers /
  menus that must read as detached.

### Named Rules
**The Flat-At-Rest Rule.** Surfaces are flat at rest. Lift (shadow / tonal shift)
is a response to state — hover, focus, a floating layer — never decoration.

## 5. Components

### Buttons
- **Shape:** gently rounded (6px, `--radius-field`).
- **Primary:** navy fill (#244A77), white text, padding ~0.5rem 1.25rem; hover
  darkens to #1D3D63, active #172F4D via a fast 120ms ease.
- **Ghost / Secondary:** surface background, navy text, 1px border; used for
  Cancel and secondary actions beside a single primary.
- **Danger:** error fill (#B3261E) for destructive confirmation only.
- **Focus:** a 3px bright-blue focus ring (`box-shadow`, `--focus-ring`) on top of
  the border — never an outline removal.

### Inputs / Fields
- **Style:** white (or themed) fill, 1px tinted border, 6px radius, ~0.5rem
  0.75rem padding.
- **Focus:** border shifts to bright blue + the 3px focus-ring glow.
- **Error:** error border + a red-tinted focus ring (`--focus-ring-error`) and a
  caption-size message below.

### Cards / Containers
- **Corner:** 10px (`--radius-card`).
- **Background:** surface (#FFFFFF) on the tinted page neutral; sunken insets for
  nested quiet regions.
- **Shadow:** none at rest (see Elevation) — delineated by the 1px border.
- **Padding:** generous (~1.5rem). Never nest a card inside a card.

### Chips / Tags
- **Style:** pill (radius 999px), surface background, 1px border; selected state
  fills navy with white text. Used for interests and filters.

### Navigation
- **Style:** a left sidebar of grouped links (Overview / People / Programme / …),
  body-size, muted at rest, navy + emphasis when active; permission-filtered (no
  link the operator can't use). Top bar carries language switch, theme toggle,
  notifications, profile, sign-out.

### Data grid (signature component)
The CP standard for every list page (`SimfDataGrid`): column-filter row,
select-all + per-row checkbox, quiet per-row **icon** actions (Details / Edit /
Copy / Delete), pagination. Never a raw `<table>`. This is the workhorse surface —
it must stay calm and scannable, not dense.

## 6. Do's and Don'ts

### Do:
- **Do** take every colour, font, space, radius, shadow from
  `theme.tokens.css` (`--color-*`, `--space-*`, `--radius-*`). Zero hardcoded hex,
  zero hardcoded font-family in components (CLAUDE.md §8).
- **Do** keep the page navy-structural and quiet: one primary action per screen,
  accent ≤10%.
- **Do** build on **logical properties** (`inline-size`, `margin-inline`,
  `padding-block`) so Arabic RTL mirrors for free.
- **Do** convey depth by tonal layering (background → sunken → surface) and 1px
  tinted borders; reserve the card shadow for truly floating surfaces.
- **Do** use the `SimfDataGrid` for every list; use a 3px bright-blue focus ring
  on every focusable control.

### Don't:
- **Don't** look consumer-playful or casual: no bright rounded-everything, no
  emoji, no informal microcopy. This is a state maritime forum.
- **Don't** ship generic AI-slop SaaS: no identical icon + heading + text card
  grids, no gradient hero-metric template, no interchangeable "modern dashboard".
- **Don't** go crypto-neon / flashy: no neon-on-black, no glows, no
  gradients-everywhere, no hype.
- **Don't** fall into cluttered legacy-ERP density: no wall-to-wall raw `<table>`,
  no hierarchy-free cramming. Give it breathing room.
- **Don't** use a colored `border-left`/`border-right` > 1px as an accent stripe;
  **don't** use gradient `background-clip: text`; **don't** use glassmorphism by
  default. Use full borders, tonal tints, leading icons, or nothing.
- **Don't** use `#000` / `#fff`; **don't** make gold a status or a fill;
  **don't** animate layout properties or add bounce/elastic motion.
