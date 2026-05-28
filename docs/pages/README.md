# `docs/pages/` — per-page reference docs

This folder holds one Markdown reference per page in the SIMF system. Every page
in the Control Panel, Website, and (when it ships) Mobile App gets its own
`.md` file under `cp/`, `web/`, or `mobile/` respectively.

> **Authority:** D-133 (2026-05-28).
> **Coverage gate:** the master cross-reference is [`PAGE-INDEX.md`](PAGE-INDEX.md).
> If a row there says ✅ Real but the linked doc does not exist, the row is
> a **coverage gap**.

## How to navigate

- **By route:** open [`PAGE-INDEX.md`](PAGE-INDEX.md), find your route, click
  the **Doc** column.
- **By module / topic:** open the [Admin Manual](../manuals/Admin-Manual.md),
  [User Manual](../manuals/User-Manual.md), or [Developer Guide](../manuals/Developer-Guide.md);
  each chapter links the per-page doc(s) it covers.
- **By use case:** `SIMF-UCS-001` use-case entries cross-reference the page(s)
  that implement them.
- **By test:** every E2E scenario in [`docs/tests/e2e/`](../tests/e2e/) names the
  page reference it exercises.

## How to author a new doc

1. Add a row to [`PAGE-INDEX.md`](PAGE-INDEX.md) (route, status, audience,
   doc + test paths).
2. Copy [`_TEMPLATE.md`](_TEMPLATE.md) to `cp/{slug}.md` (or `web/`, `mobile/`).
3. Fill every section. Don't leave empty sections — write `N/A — {reason}`
   if a section genuinely doesn't apply.
4. Capture screenshots into `docs/screenshots/` with the page slug + state
   in the filename.
5. Add the matching chapter to the relevant manual; cross-link both directions.
6. Author an E2E test entry under `docs/tests/e2e/{slug}.md` covering the
   golden path + branches the §11 of the per-page doc names.
7. Add a use-case entry to `SIMF-UCS-001` if the page implements a new one.
8. Commit all the artefacts in one changeset.

## Current coverage

- **PAGE-INDEX.md** — full inventory (36 CP routes, 9 Website routes, mobile pending)
- **_TEMPLATE.md** — the per-page doc shape every entry follows
- **cp/admin-interests.md** — full sample, written against the D-132-migrated
  page; use it as the reference for the rest.

Coverage gaps (i.e. ✅ Real rows in `PAGE-INDEX.md` without a doc yet) are
tracked in `docs/decisions/DECISIONS_LOG.md` under D-133.

## Related folders

- [`../manuals/`](../manuals/) — user-facing manuals (User / Admin / Developer / Test)
- [`../tests/e2e/`](../tests/e2e/) — per-page Gherkin-style scenarios
- [`../dev/`](../dev/) — engineering patterns (CRUD grid, theming, …)
- [`../decisions/`](../decisions/) — decisions log
- [`../screenshots/`](../screenshots/) — every screenshot referenced from these docs
