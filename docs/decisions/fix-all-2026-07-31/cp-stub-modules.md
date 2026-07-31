# Pending global-doc merges — `cp-stub-modules` (Track E, item E2)

The last `IsStub` CP module was visible to, and openable by, every signed-in
admin regardless of role.

## DECISIONS_LOG

| D-NEXT | 2026-07-31 | **A CP stub module is gated on a permission, not merely labelled "Soon" (`cp-stub-modules`, owner decision Q4).** The register's headline ("8 of 22 D-134 stubs remain") was stale — seven graduated and `CpNavigation.cs` carried **exactly one** `IsStub` entry, `Module.LiveSessions` → `/m/live-sessions`. The live defect was not the count but the gate: an `IsStub` item carried `RequiredPermission: null`, and `CpShellLayout` filters the menu on that value, so `null` meant "show to everyone"; the page it links to (`ModulePlaceholder.razor`, `@page "/m/{Module}"`) was gated on `[Authorize]` alone, so **any** signed-in admin could open the URL. The 2026-07-28 §6.16 run recorded `/m/live-sessions` in a `GateOperator`'s menu (a role holding only `Gates.Operate` + `Gates.ViewOwnReports`) and logged it as a pass. **Fix, both halves on the SAME code:** the nav item now carries `RequiredPermission: PermissionCatalog.Sessions.View` and the placeholder carries `@attribute [RequirePermission(PermissionCatalog.Sessions.View)]` — the programme-read permission the console it stands in for would need. Gating only the menu would have moved the hole rather than closed it: the item disappears and the URL still opens for anyone who types it. **Per Q4 no Live Sessions console was built** — the entry stays `IsStub`, so `CpAssistantDirectory` still excludes it. **Guards:** two new facts in `CpNavigationPermissionTests` — `Every_stub_nav_item_is_permission_gated` (no `IsStub` item may carry a null permission) and `Every_stub_nav_gate_matches_the_gate_on_the_placeholder_that_serves_it`, which resolves a stub href against a **parameterised** `@page` template, the case the existing literal-route lookup could not see and therefore skipped. E2E: `cp-framework-pages.md` gains `E2E-FRM-011`; `E2E-FRM-004` and `-007` change expectation and must be re-run. No schema, no new permission code, no new resx key. | The "Soon" pill is a label, not an authorization decision, and this was invisible to every test and every manual pass: the seeded super-admin's `*` wildcard satisfies both gates, so the hole only exists for exactly the restricted roles nobody signs in as. Gating the catch-all is correct **while** the sole declared stub is a programme stub; the page comment records that a future stub needing a different permission must graduate to its own route rather than widen this gate, so the next person cannot quietly make it wrong. |

## PAGE-INDEX

Row 141 — the stub row must state its gate, otherwise the index still reads as
though the placeholder is open to any administrator. Replace:

```
| `/m/{module}` | 🚧 Stub | Administrator | — | — |
```

with:

```
| `/m/{module}` | 🚧 Stub | `Sessions.View` (nav + page, `cp-stub-modules`) | — | [e2e/cp-framework-pages.md](../tests/e2e/cp-framework-pages.md) |
```

## E2E-README

Row 181 — the FRM range extends by one. Replace:

```
| `/not-permitted` + `/not-found` + `/Error` | [`cp-framework-pages.md`](cp-framework-pages.md) | E2E-FRM-001..010 |
```

with:

```
| `/not-permitted` + `/not-found` + `/Error` + the `/m/{module}` stub gate | [`cp-framework-pages.md`](cp-framework-pages.md) | E2E-FRM-001..011 |
```

**Roll-up:** adds **1** Coverage-matrix row (`E2E-FRM-011`); no new catalogue
file. See the note in `QA-LIVE-001.md` — Track E contributes **+3** in total.
