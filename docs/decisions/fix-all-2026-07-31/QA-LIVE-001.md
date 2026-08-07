# Pending global-doc merges — `QA-LIVE-001` (Track E, item E1)

Favicon: `GET /favicon.ico` returned 404 on every Control Panel page.

## DECISIONS_LOG

| D-NEXT | 2026-07-31 | **Control Panel shell head declares a tab icon (`QA-LIVE-001`).** `src/ControlPanel/SIMF.ControlPanel/Components/App.razor` carried five `<link rel="stylesheet">` tags and no `<link rel="icon">`, and `wwwroot/` held only `app.css` and `js/`. With no icon declared the browser falls back to requesting `/favicon.ico`, which nothing served — so **every** CP page load logged a 404 in the network panel and the browser tab carried the blank default glyph. **Fix:** `wwwroot/favicon.png` (the product's own 64×64 SIMF emblem, byte-copied from `src/Mobile/simf_app/web/favicon.png` so the Control Panel, the app and the Website all show one mark, rather than commissioning new artwork) plus `<link rel="icon" type="image/png" href="@Assets["favicon.png"]" />` in the head. PNG rather than `.ico` or `.svg`: `<link rel="icon" type="image/png">` is honoured by every browser the CP targets, an SVG icon is not (Safari before 16.4), and naming a PNG `.ico` would misdeclare its content type. `MapStaticAssets` serves both the fingerprinted and the plain path, so a direct `/favicon.png` also resolves. **Guard:** new `tests/SIMF.ControlPanel.Tests/CpHeadAssetsTests.cs` — the head must declare an icon whose file exists, **every** local `@Assets["…"]` in the head must resolve to a real file, and the linked scoped-CSS bundle must still be one the SDK emits. A render test cannot catch this class (bUnit does not fetch assets) and a live browser check only covers the page someone opened; resolving each href against the file that must serve it does. No schema, no permission, no new resx key. | The head is the one document every CP route is served inside, so a wrong href there is a defect on every page rather than on one. The same class had already shipped on the Website, whose head linked `SIMF.Web.styles.css` — a scoped-CSS bundle the SDK never emits for a project with no `.razor.css` — and 404'd all 17 public routes with a MIME-type refusal in the console. Pinning the whole head, not just the favicon, is what stops the third instance. |

## PAGE-INDEX

Row 37 — the Dashboard row now also carries the shell head. Replace:

```
| `/` | ✅ Real | Any signed-in CP user (figures need `Statistics.View`) | [cp/dashboard.md](cp/dashboard.md) | [e2e/cp-dashboard.md](../tests/e2e/cp-dashboard.md) |
```

with:

```
| `/` | ✅ Real | Any signed-in CP user (figures need `Statistics.View`) | [cp/dashboard.md](cp/dashboard.md) — also documents the shell head (§4.0, favicon) rendered on every CP route | [e2e/cp-dashboard.md](../tests/e2e/cp-dashboard.md) |
```

## E2E-README

Row 41 — the DSH range extends by one. Replace:

```
| `/` (Dashboard) - shell chrome + the Wave A programme dashboard (KPI grid + grouped bar chart + per-day cards, gated on `Statistics.View`) | [`cp-dashboard.md`](cp-dashboard.md) | E2E-DSH-001..025 |
```

with:

```
| `/` (Dashboard) - shell chrome + the shell head (favicon, `QA-LIVE-001`) + the Wave A programme dashboard (KPI grid + grouped bar chart + per-day cards, gated on `Statistics.View`) | [`cp-dashboard.md`](cp-dashboard.md) | E2E-DSH-001..026 |
```

**Roll-up:** this item adds **1** Coverage-matrix row (`E2E-DSH-026`). No new
catalogue file, so **Pages catalogued** is unchanged. `E2eCatalogueIntegrityTests.The_index_roll_up_matches_the_catalogue_it_describes`
parses `**Total scenarios:** N`, so N must be re-derived at merge — Track E
contributes **+3** in total (E2E-DSH-026, E2E-FRM-011, E2E-CNT-021), taking the
stated 2884 to 2887 if no other track has moved it.
