# Product

## Register

product

## Users

The **SIMF Control Panel** operators — the back-office staff who run the Saudi
International Maritime Forum for the Royal Saudi Naval Forces (RSNF):

- **Forum organisers / administrators** — manage attendees, programme, exhibition,
  content, reference data, access control.
- **Registration-desk staff** — register walk-in visitors and approve pending
  ones, face-to-face, often on a tablet, under queue pressure.
- **Gate operators** — scan QR badges and admit attendees at the venue.
- **Scientific committee** — moderate sessions, question queues, summaries.
- **Public-relations staff** — invitations and VIPs.

Context: desk/office and on-site at the venue, frequently bilingual (English +
Arabic, RTL), high-stakes (a state-level event under NCA security compliance),
every action permission-gated and audited. The operator is usually mid-task and
wants to finish it fast and correctly, not to admire the interface.

## Product Purpose

The Control Panel is the operational console for running SIMF end to end:
visitor/attendee lifecycle (walk-in registration, pending approval, badges/QR,
gates and arrivals), programme (sessions, speakers, halls, seating), exhibition
(booths, sponsors, venue map), engagement (live sessions, moderation, ratings),
content (CMS, media, news), and reference + system data — all behind a
per-page/per-action permission system with a full audit trail.

Success = staff complete each operational task quickly and without error under
event-day pressure, the right people see the right actions (and only those), and
every change is traceable.

## Brand Personality

**Institutional, authoritative, trustworthy.** State-grade naval credibility:
formal, precise, calm under operational load. Bilingual by nature (English +
Arabic RTL as equals). The voice is direct and unembellished. It should read as
a serious government instrument, never as a casual consumer app or a hype-driven
product.

## Anti-references

This must NOT look like:

- **Consumer-playful / casual** — bright rounded everything, emoji, informal
  microcopy. Too light for a state maritime forum.
- **Generic AI-slop SaaS** — identical icon + heading + text card grids, the
  gradient hero-metric template, the interchangeable "modern dashboard".
- **Crypto-neon / flashy** — neon-on-black, glows, gradient-everywhere, hype.
- **Cluttered / dense legacy-ERP** — cramped raw tables wall-to-wall, no
  hierarchy, no breathing room.

## Design Principles

1. **Operational clarity first.** The operator's current task is unmistakable;
   each screen has one obvious primary action and a calm path to it. Speed and
   correctness beat decoration.
2. **Trust through restraint.** Institutional calm: tinted navy neutrals, one
   deliberate accent, generous structure. Nothing on screen that does not carry
   meaning.
3. **Bilingual by construction.** English and Arabic (RTL) are first-class, never
   bolted on — logical CSS properties, mirrored layouts, both scripts considered
   in every component.
4. **One source of truth for style.** All colour, type, spacing, elevation and
   radii come from `src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css`
   (CLAUDE.md §8). Zero hardcoded hex, zero hardcoded font-family in components.
5. **Permissioned and auditable by design.** The UI reflects the operator's
   permissions — no dead or ungated affordances — and consequential actions are
   confirmed and logged.

## Accessibility & Inclusion

- **Bilingual EN/AR with full RTL** is a baseline requirement, not an add-on.
- Target **WCAG 2.1 AA** (the project ships accessibility markup tests); colour is
  never the sole carrier of meaning.
- Keyboard and screen-reader support: focus-trapped modals, a skip-link, visible
  focus rings, correct roles/labels.
- Respect `prefers-reduced-motion`; motion is functional, never decorative.
- Operates under NCA security compliance — privacy of attendee PII (IDs, photos)
  is handled with care in every surface that displays it.
