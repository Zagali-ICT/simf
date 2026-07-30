# Control Panel page reference: Reports

| | |
|--|--|
| **Routes** | `/admin/reports` (hub), `/admin/reports/attendance`, `/admin/reports/registrations`, `/admin/reports/gates` |
| **Surface** | Control Panel |
| **Permissions** | `Reports.View` (hub), `Reports.Attendance`, `Reports.Registrations`, `Reports.Gates`, `Reports.Export` |
| **Backend** | `POST /api/v1/admin/reports/{slug}/list` and `/export` |
| **Writes** | None. Every report is read-only. |
| **Schema** | No new table, no new column, no migration. |
| **E2E** | [`cp-reports.md`](../../tests/e2e/cp-reports.md) |
| **Last reviewed** | 2026-07-30 |

## 1. What the module is

Three date-ranged views over records the other contexts own, each with an Excel
export. Reporting stores nothing of its own: it reads sessions, hall arrivals,
accounts and gate scans, and presents them.

| Report | Route | Answers |
|--------|-------|---------|
| Attendance | `/admin/reports/attendance` | Which sessions were attended, by how many distinct people, and how many are still inside |
| Registrations | `/admin/reports/registrations` | Who registered in the period, with profile type and approval state |
| Gate activity | `/admin/reports/gates` | Every recorded scan, allowed and denied, with the reason for each refusal |

## 2. Permissions

Each report has its own gate, so an operator can be given the gate log without
the attendee roster. `Reports.Export` is separate from viewing: taking a
spreadsheet of attendees off the premises is a bigger act than reading a page of
them on screen, so the export button is wrapped in
`<AuthorizedAction Permission="Reports.Export">` and the export endpoints gate on
that code rather than on the per-report one.

The hub only shows a card for a report the visitor can actually open, so it never
advertises a page that would bounce them to `/not-permitted`.

## 3. The date range

`From` and `To` are **Saudi calendar dates** and the range is **inclusive on both
ends**, which is what an operator picking "6 to 8 November" means.

Instants are stored as UTC. The service therefore resolves each end once, through
`SaudiTime.FromSaudiWallClock`:

```
start = Saudi 00:00 on From          -> the UTC instant
end   = Saudi 00:00 on (To + 1 day)  -> the UTC instant, EXCLUSIVE
```

The upper bound is the start of the day **after** `To`. Using `To` itself would
silently drop the whole final day of every report, which is the day people check
first. Range predicates compare against the stored column directly, so an index
on it is still usable.

Both ends are optional. An open end means "no bound in that direction".

## 4. What each figure means

**Attendance**

| Column | Definition |
|--------|------------|
| Attendees | **Distinct** `HallAttendance.UserId` for the session. Someone who steps out and returns counts once. |
| Inside now | Arrivals with no `Leave` recorded. |

**Gate activity**

The visitor name and profile type are the scan's own `ScannedDisplayName` and
`ScannedProfileTypeName` snapshots, not a live lookup. `GateScan` is an
append-only audit log and those columns exist so a historic scan still reads
correctly after the account is renamed or removed. Resolving them live would make
the report disagree with the audit trail it reports on.

**Registrations**

Attendee (non-admin) accounts only. This is the one report that genuinely spans
both databases: the account is in Identity, the profile type is in App. D-157
forbids a cross-database join, so the page of accounts is read from Identity and
the profile-type names for exactly those user ids are resolved with a second
query against App. A user with no profile row renders blank rather than a guess.

## 5. Totals

The figures above each grid describe the **whole filtered set**, not the visible
page. A header total that changed when you turned the page would be worse than
no total at all. The API returns them as resource **keys** plus a formatted
value, so the API stays language-neutral and the Control Panel localises them.

## 6. Export

The export returns the whole filtered set, not the visible page, capped at 20,000
rows. Past the cap it returns the first 20,000 in the report's sort order rather
than failing, and the operator is expected to narrow the range.

The download file name is stamped in **Saudi local time** (D-770): an operator
exporting at 1am Riyadh should not get a file dated the previous day.

Known carry-over: `ClosedXmlGridExcelExporter` writes a raw `DateTimeOffset` as
`UtcDateTime`. Every report row therefore carries its dates as **pre-formatted
Saudi strings** rather than live timestamps, which keeps UTC out of the workbook
without changing the shared exporter (which around 39 other exports depend on).

## 7. Wiring

The Control Panel is a BFF with **no catch-all proxy**. Each report needed three
pieces, not one:

1. the FastEndpoints endpoint (relative route; `RoutePrefix` supplies `api/v1`),
2. a `SimfAdminClient` method,
3. an explicit `group.MapPost(...)` passthrough in `AccountEndpoints.cs`.

Miss 2 or 3 and the page compiles, the API answers, and the browser gets a 404
with the grid silently empty.

## 8. Structure

`ReportPageBase<TRow>` holds everything the three pages share: the query, the
load, the grid callback, the range-applied reset and the export trigger. Each
page supplies only its slug, its columns and its labels. `ReportToolbar` is the
shared strip above every grid (range, totals, export, error). The service is one
partial class per report so no single file carries every query.

## 9. i18n, RTL and themes

Fifty `Admin.Reports.*` / `Nav.Reports` / `Module.Reports*` keys were added to
both `Strings.resx` and `Strings.ar.resx`. Dates render `dd-MM-yyyy hh:mm tt`
Saudi local; no UTC value reaches the UI. The date inputs carry ISO
`yyyy-MM-dd` values because that is a wire format, and only the browser's
rendering of it is localised. All colour comes from `theme.tokens.css`, so light,
dark and grey are covered by the token file alone.

## 10. Known limitations

- **No saved filters or scheduled delivery.** A report is a live query.
- **The grid's per-column filters are not applied server-side** for these
  reports; the free-text search and the date range are. Column filters therefore
  affect only sorting and paging state.
- **Five further reports are specified but not built**: sessions, ratings,
  partners, meetings and engagement.

## 11. Decisions

D-776 (reporting module: additive, read-only, per-report permissions with a
separate export gate), D-777 (inclusive Saudi date range resolved to a UTC
half-open window), D-778 (report rows carry pre-formatted Saudi date strings
because the shared XLSX exporter writes UTC).
