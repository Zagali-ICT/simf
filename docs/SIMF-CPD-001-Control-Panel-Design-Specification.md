# Control Panel Design Specification

| Field | Value |
|-------|-------|
| Document ID | SIMF-CPD-001 |
| Title | Control Panel Design Specification |
| Version | 1.0 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Project Owner (MoD / RSNF representative) |
| Date issued | 2026-05-20 |
| Related documents | SIMF-VID-001, SIMF-SES-001, SIMF-SAD-001, SIMF-CON-001, SIMF-RPM-001, SIMF-RDR-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. Information architecture, layout shell, theming, localisation and component standards. |

---

## 1. Purpose

This document defines how the SIMF Control Panel is designed: its structure,
the screens it holds, how a user moves through it, how it looks, and how it
behaves in two languages and more than one theme. It is the reference the
engineers build the Control Panel from and the reference the client reviews to
confirm the design before the build.

The Control Panel is designed in-house — there is no external mockup for it, as
there is for the mobile app. This document is that design.

## 2. Scope

This document covers the Control Panel as a Blazor application: its information
architecture, navigation, layout shell, theming, localisation, component
standards and the standard screen patterns.

It does not define the permission matrix — which role can do what — that is
SIMF-RPM-001. It refers to permissions; it does not set them. It does not
define the feature behaviour of each module in full detail; that is the job of
the per-feature design specifications (SIMF-FDS-NNN). It does not cover the
public website or the mobile app.

## 3. Design principles

1. **Follow the brand.** Every colour, font and visual treatment comes from
   SIMF-VID-001. The Control Panel does not introduce its own look.
2. **Minimal, elegant, calm.** The brand guide asks for restraint. The Control
   Panel is a working tool for organising teams; it favours clarity and density
   of useful information over decoration.
3. **Permission-driven.** A user sees only what their role allows. The
   navigation, the screens and the actions are all filtered by permission
   (SIMF-RPM-001). Nothing an admin cannot use is shown to them.
4. **One way to do a thing.** A list looks like every other list; a form
   behaves like every other form; an approval queue works the same everywhere.
   The standard patterns in section 13 are used; screens are not reinvented.
5. **Bilingual and bidirectional from the start.** Arabic and English, RTL and
   LTR, are designed in together — not retrofitted.
6. **Built on the standards.** The CSS, component and structure rules in
   SIMF-SES-001 are followed without exception.

## 4. The Control Panel at a glance

The Control Panel is the operations console for the SIMF organising teams. Its
users are the internal roles: Admins, the Security team, the PR team, the
Technical team, the Scientific team, Logistics, Marketing, and Moderators. Staff
do field work in the mobile app and do not use the Control Panel.

It is a Blazor application using the MudBlazor component library. Administrative
sign-in requires the email, password and a TOTP code, per SIMF-API-001.

## 5. Information architecture

### 5.1 Module map

The Control Panel is organised into nine navigation groups. A group, and each
item under it, is shown only to users whose role permits it.

| Group | Modules |
|-------|---------|
| Overview | Dashboard |
| People | Registration requests · Attendees · Roles & permissions |
| Programme | Themes & pillars · Sessions · Halls & seating · Speakers · Bookings |
| Exhibition | Exhibitors · Booths · Sponsors · Venue map |
| Engagement | Live sessions · Moderation queue |
| Knowledge & AI | FAQ groups & entries · AI settings |
| Content | Media Center · News · Previous editions |
| Communications | Notifications |
| System | Configuration · Operation log · Settings |

### 5.2 What each module is for

| Module | Purpose |
|--------|---------|
| Dashboard | Statistics and live attendance — the figures from SIMF-CON-001 §7.10, by day and overall |
| Registration requests | The approval queue: the Security team reviews registrations and approves or rejects, with bulk select |
| Attendees | The approved users, their type and profile |
| Roles & permissions | Manage roles and permissions; baseline roles plus new roles an Admin adds (decision D1) |
| Themes & pillars | The five forum pillars and their sub-topics |
| Sessions | Create and manage sessions; live or non-live; assign speakers and a hall |
| Halls & seating | Create halls, set seating capacity, manage the seat grid |
| Speakers | Speaker profiles — bio, photo, country flag, linked sessions |
| Bookings | Session seat bookings; every booking is approved here before it is confirmed (decision D4) |
| Exhibitors | The PR team reviews and approves exhibitors; the booth is assigned in the same step (decision D3) |
| Booths | The booth directory and booth detail |
| Sponsors | Sponsors by tier — Strategic, Premium, Gold |
| Venue map | The 3D venue map content — halls, zones, booth positions |
| Live sessions | Broadcast state for sessions that are streaming |
| Moderation queue | Session questions and comments; comments arrive AI-filtered and an admin approves or discards (decision D5) |
| FAQ groups & entries | The two-level FAQ knowledge — groups, then entries — that powers the assistant (decision D5) |
| AI settings | Configuration of the cognitive AI features |
| Media Center | Media coverage content — posts and social content |
| News | News items |
| Previous editions | The archive of past forum editions; visibility controlled here |
| Notifications | Compose and manage notifications across the four channels |
| Configuration | Dynamic content, categories, labels, colours; registration open/close; venue tracks and zones |
| Operation log | The record of changes and approvals made in the Control Panel |
| Settings | System-level settings |

Where a module's field-level content was left open in decision D6 — the media
and news fields, the exact statistics — this document and the per-feature
specifications propose it for the client to review.

## 6. Layout shell

Every Control Panel screen sits in the same shell: a top bar, a side
navigation, and a content region.

```
┌───────────────────────────────────────────────────────────┐
│  Top bar: logo · breadcrumb · language · theme · alerts · user │
├──────────┬────────────────────────────────────────────────┤
│          │  Page header: title · primary action           │
│   Side   ├────────────────────────────────────────────────┤
│   nav    │                                                 │
│ (groups) │   Content region                                │
│          │                                                 │
└──────────┴────────────────────────────────────────────────┘
```

### 6.1 Top bar

The top bar carries, from the leading edge to the trailing edge: the SIMF logo
(placed per SIMF-VID-001), the page breadcrumb, and then the controls — the
language switch, the theme switch, the notifications bell, and the user menu
(profile and sign-out).

### 6.2 Side navigation

The side navigation lists the nine groups from section 5, each group expanding
to its modules. It can be collapsed to icons to give the content more room. A
group with no modules the user may see is not rendered at all.

### 6.3 Content region

The content region has a page header — the page title and the primary action
for that page — and below it the page content, which is one of the standard
patterns in section 13.

### 6.4 Direction

In Arabic the shell is right-to-left: the side navigation is on the right, the
breadcrumb reads right to left, the top-bar controls sit on the left. In English
it mirrors. The shell is built with direction-aware layout so the mirror is
automatic, not a second hand-built layout.

## 7. Navigation behaviour

- Navigation is filtered by permission. The side navigation is built from the
  signed-in user's permissions; a user never sees a route they cannot open.
- A user who reaches a forbidden route by a direct link gets a clear "not
  permitted" page, not a broken screen.
- The breadcrumb shows where the user is and lets them step back up.
- The current location is marked in the side navigation.

## 8. Theming

### 8.1 Source

The Control Panel theme is built on the design tokens in SIMF-VID-001. The
brand colours, the typeface and the brand tokens are taken from there and are
not redefined here.

### 8.2 Token files

Per SIMF-SES-001 section 6.2, the styling lives in three files:

- `app.css` — global resets only.
- `theme.tokens.css` — every token: the brand tokens from SIMF-VID-001, the
  derived functional tokens (section 8.4), and the per-theme values.
- `theme.overrides.css` — visual overrides on MudBlazor where a component
  property cannot do the job.

There are zero hardcoded colours and zero hardcoded font names anywhere else.

### 8.3 Brand tokens

From SIMF-VID-001 section 9: `--color-primary` `#244A77`, `--color-secondary`
`#498FBD`, `--color-accent-blue` `#007CD8`, `--color-accent-gold` `#E8C060`,
`--color-neutral` `#F1F2F2`. Navy leads the interface; gold stays a rare accent.

### 8.4 Derived functional tokens — proposed for review

A working admin interface needs more than the five brand colours. The tokens
below are **derived from the brand palette and proposed for the client to
review** (SIMF-VID-001 open item OI-3). They are not final until reviewed.

| Token | Proposed value | Use |
|-------|----------------|-----|
| `--color-text` | `#1A2433` | Body text — a near-black derived from the navy |
| `--color-text-muted` | `#5A6573` | Secondary text, captions |
| `--color-surface` | `#FFFFFF` | Cards, tables, panels |
| `--color-background` | `#F1F2F2` | Page background — the brand neutral |
| `--color-border` | `#E2E5E9` | Lines, dividers, input borders |
| `--color-info` | `#007CD8` | Information — the brand bright blue |
| `--color-success` | `#2E7D5B` | Success states |
| `--color-warning` | `#C9912F` | Warning states |
| `--color-error` | `#B3261E` | Error and destructive states |

The brand gold `#E8C060` is not used for warnings; it stays a brand accent so
that a warning and a highlight are never confused.

### 8.5 Light and dark themes, and multi-theme

The Control Panel ships with a **light** theme and a **dark** theme, and the
token structure allows more themes to be added later.

- The active theme is set by a `data-theme` attribute on the root element.
  `theme.tokens.css` holds the default (light) token values and a
  `[data-theme="dark"]` block that overrides them.
- A theme override only changes token values. No component and no page is
  rewritten for a theme. This is what makes a third theme cheap to add.
- The **light** theme uses `#F1F2F2` for the background, white surfaces, navy
  structure, and the standard logo.
- The **dark** theme uses a dark surface set derived from the navy, light text,
  and the **negative (white) logo** from SIMF-VID-001. The dark token values
  are listed in this document once reviewed (open item OI-1).
- The theme switch is in the top bar; the chosen theme is remembered per user.

### 8.6 The pattern

The compass-and-anchor pattern from SIMF-VID-001 is used only as a quiet
background texture, at 10%–30% opacity, on a few low-density surfaces — the
sign-in screen, empty states, and the dashboard header band. It never sits
behind data tables, forms or dense content.

## 9. Typography and type scale

The typeface is FS Albert Arabic, for both Arabic and English (SIMF-VID-001
section 7). Headings are Bold; body text is Regular.

The type scale below is **proposed for review** (SIMF-VID-001 OI-3). Sizes are
in pixels at the default zoom.

| Token | Proposed size / weight | Use |
|-------|------------------------|-----|
| `--font-size-title` | 24 px Bold | Page title |
| `--font-size-subtitle` | 18 px Bold | Section headings |
| `--font-size-body` | 14 px Regular | Body text, table content, form fields |
| `--font-size-caption` | 12 px Regular | Captions, helper text, metadata |

Colour helps separate headings from body, as the brand guide asks: a heading
uses `--color-text` at a heading weight, supporting metadata uses
`--color-text-muted`.

## 10. Localisation

- The Control Panel is fully bilingual: Arabic and English. Arabic is the
  primary language.
- All interface text comes from resource files, one per language. No string is
  hardcoded in a component.
- The language switch is in the top bar. The choice is remembered per user and
  applied immediately.
- The layout direction follows the language — RTL for Arabic, LTR for English —
  using direction-aware layout so a screen is not built twice.
- Numbers and times follow the rule in SIMF-MAA-001 for consistency across the
  surfaces: dates display as `dd-MM-yyyy` and digits are Latin.
- Data entered in both languages — a session title, a speaker name — is
  captured in both, as the dynamic-content requirement needs.

## 11. Component standards

The Control Panel uses MudBlazor. The component rules from SIMF-SES-001 section
6 apply: MudBlazor properties first, then theme overrides, then CSS; no inline
styles; BEM class names; colours from tokens only.

| Element | Standard |
|---------|----------|
| Buttons | One primary action per page header, in `--color-primary`. Secondary actions are outlined or text. Destructive actions use `--color-error` and confirm before acting. |
| Tables | The list pattern (section 13.1): column headers, sortable where useful, paginated, row actions at the trailing edge. |
| Forms | Labels above fields; validation messages under the field; the field, validation and any limit aligned across layers per SIMF-SES-001 section 5.3. |
| Dialogs | Used for short confirmations and small focused edits. A long form is a page, not a dialog. |
| Status chips | A small coloured chip shows a status or a category. Category chips use the per-category colour from Configuration; status chips use the functional tokens. |
| Empty, loading, error states | Every data area has all three. A screen never shows a blank area on no data or on failure. |
| Notifications / toasts | Brief confirmation of an action; not used to carry errors that belong inline. |

## 12. The pattern motif

Covered in section 8.6 — the brand pattern is a restrained background texture
only, never behind working content.

## 13. Standard screen patterns

Every Control Panel screen is one of these patterns. A module does not invent a
new layout.

### 13.1 List page

A filterable, searchable, paginated table of records. It has a page header with
the page title and a primary action (for example, "New session"), a filter and
search row, the table, and pagination. Each row has actions at its trailing
edge. The list shows active records and offers an option to include inactive
ones where that is useful.

### 13.2 Detail page

A read view of one record, its fields grouped into clear sections, with the
actions available on it. A detail page links to the related records — a
session's detail links to its speakers and its hall.

### 13.3 Create and edit form

A form following the form standard in section 11. Create and edit use the same
layout. The form validates before it submits and shows field-level messages.

### 13.4 Approval queue

The pattern for Registration requests, Exhibitor approvals and Bookings. It is
a list page with two additions: a row carries an approve and a reject action,
and the toolbar carries a **select-all and bulk approve** control, as decision
D1 requires for registration. A rejection asks for a reason.

### 13.5 Moderation queue

For session questions and comments. Items arrive already AI-filtered; each item
shows its AI result and lets the admin approve or discard it (decision D5). The
queue updates live through SignalR (SIMF-SAD-001 section 6.4) so a moderator
sees new items without refreshing.

### 13.6 Dashboard

The Dashboard module: statistic cards and the live-attendance figures, grouped
and readable, refreshing live where the data is live. It is the one screen
where the brand pattern may sit behind the header band.

### 13.7 States

Loading, empty and error states are part of every pattern, not an afterthought.
A slow load shows a loading state; no data shows a helpful empty state; a
failure shows a clear error with a way to retry.

## 14. Accessibility

- Colour is never the only signal. A status carries an icon or text as well as
  a colour, so it reads without colour vision.
- Contrast meets a sensible standard for text on its background; the derived
  token values in section 8.4 are checked for contrast when reviewed.
- The Control Panel is operable by keyboard: every action is reachable and the
  focus order follows the reading order, including in RTL.
- Form fields have real labels, and validation messages are associated with
  their field.

## 15. Responsive behaviour

The Control Panel is built for a desktop browser, which is where the organising
teams work. It stays usable down to a small laptop: the side navigation
collapses to icons, tables scroll within their region rather than breaking the
layout, and the top bar keeps its controls. It is not designed for a phone; the
attendee experience on a phone is the mobile app.

## 16. Open items

| ID | Item | Needed for |
|----|------|-----------|
| OI-1 | Review and confirm the dark-theme token values | Section 8.5 |
| OI-2 | Client review of the derived functional tokens and the type scale | Sections 8.4, 9 |
| OI-3 | The per-type permission map that filters navigation and actions — from SIMF-RPM-001 | Sections 5, 7 |
| OI-4 | The field-level content of the media, news and statistics modules — proposed in the per-feature specifications for review | Section 5.2 |
| OI-5 | Confirm document classification with the owner | Control block |

---

End of document.
