# Control Panel — per-page documentation (config pages behind the app)

Last updated: 2026-06-13 — CP config-page documentation (D-380)

This folder holds the **per-page 5-file documentation sets** for the Control
Panel pages that **configure the data the mobile app consumes** — authored in the
same format as the Flutter app's pages under [`../App/`](../App/) (a `README.md`
index plus four aspect files: **Function**, **Logic**, **API**, **Design**).

Each CP page-group lives in `docs/CP/<slug>/`, keyed on the same slug as its
existing single-file reference doc (`docs/pages/cp/<slug>.md`) and its E2E
catalogue (`docs/tests/e2e/cp-<slug>.md`), so the three doc layers cross-link
cleanly:

| Layer | Location | Role |
|-------|----------|------|
| Per-page set (this folder) | `docs/CP/<slug>/{README,_Function,_Logic,_API,_Design}.md` | App-parity 5-file documentation |
| Reference doc | `docs/pages/cp/<slug>.md` | Canonical single-file narrative (13-section) |
| E2E catalogue | `docs/tests/e2e/cp-<slug>.md` | Gherkin regression scenarios |

The reference docs remain the canonical narrative; these per-page sets
**supplement and cross-link** them (they do not replace them).

## Documented page-groups (the config pages the documented app pages consume)

Ordered to mirror the app sign-up → core-screen sequence. "Feeds" names the
app page(s) (under [`../App/`](../App/)) that read the same data.

| # | CP page-group | Route | Page permission | Feeds app page(s) |
|---|---------------|-------|-----------------|-------------------|
| 1 | [Organisations](admin-organisations/README.md) | `/admin/organisations` | `Organisations.View` | Page 007 (الجهة), 005 register |
| 2 | [Countries / nationalities](admin-countries/README.md) | `/admin/countries` | `Countries.View` | Page 007 (nationality) |
| 3 | [Profile types — Visitor](admin-profile-types-visitor/README.md) | `/admin/profile-types/visitor` | `ProfileTypes.View` | Page 007 (Visitor tab) |
| 4 | [Profile types — Other](admin-profile-types-other/README.md) | `/admin/profile-types/other` | `ProfileTypes.View` | Page 007 (Other tab) |
| 5 | [Interests](admin-interests/README.md) | `/admin/interests` | `Interests.View` | Page 007‑01 (interests) |
| 6 | [Content blocks (CMS)](admin-content-blocks/README.md) | `/admin/content-blocks` | `ContentBlocks.View` | Page 009 (terms), 013 (about) |
| 7 | [Programme sessions](admin-sessions/README.md) | `/admin/sessions` | `Sessions.View` | Page 016 (agenda), 013 |
| 8 | [Session categories](admin-session-categories/README.md) | `/admin/session-categories` | `SessionCategories.View` | Page 016 (category filter) |
| 9 | [Speakers](admin-speakers/README.md) | `/admin/speakers` | `Speakers.View` | Page 016 (session detail) |
| 10 | [Halls](admin-halls/README.md) | `/admin/halls` | `Halls.View` | Page 016 (hall), 015 (venue) |
| 11 | [Venue-map nodes](admin-venue-map/README.md) | `/admin/venue-map` | `VenueMap.View` | Page 015 (venue map) |
| 12 | [Booths (Exhibition)](admin-booths/README.md) | `/admin/booths` | `Booths.View` | Page 015 (booth nodes/detail) |
| 13 | [Exhibitors](admin-exhibitors/README.md) | `/admin/exhibitors` | `Exhibitors.View` | Page 015 (booth company) |

Permission codes are nested classes of
[`PermissionCatalog`](../../src/Shared/SIMF.Common/PermissionCatalog.cs); routes
are the `@page` directive of each page's `List.razor`. Every set states its exact
`/app/*` consumption endpoint in its `_API.md`.

## Fast-follow (not in this wave)

These CP pages configure app screens that were **not** in the documented app-page
batch (the news / sponsors / media / FAQ / archive app screens), so they are a
named follow-up rather than part of this set:

`/admin/banners`, `/admin/news`, `/admin/sponsors`, `/admin/media`,
`/admin/media-partners`, `/admin/faq`, `/admin/archive`.

They already have the single-file reference doc + E2E catalogue; only the 5-file
per-page set is outstanding.

## Out of scope (no app-facing config data)

CP-internal operational pages — gate consoles, attendance/arrivals dashboards,
logs/operation-log viewers, role/permission editors, AI prompts, account/2FA,
statistics, the People-domain admin pages (`/admin/visitors`, `/admin/others`,
`/admin/admins`) — are not config pages for the documented app screens. The
visitor/badge (app Page 014 / 032) data is governed by the People-domain pages,
not a reference-data lookup; its displayed lookups (interest / organisation /
country / profile-type) are the sets documented above.
