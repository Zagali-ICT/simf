# SIMF Grid Lists Dev Guide (adding a server-paged list)

| | |
|--|--|
| **Decision** | D-928 |
| **Status** | Seam shipped and the conversion is still running. The list methods that remain hand-written are enumerated in the ratchet test's allow-list, which is the live record. |
| **Audience** | Developers adding a Control Panel list page, an admin list endpoint, or a grid export. |
| **Reference service** | [`AdminThemeService.cs`](../../src/Backend/SIMF.Infrastructure/Programme/AdminThemeService.cs) |
| **Seam** | [`SIMF.Common/Grids`](../../src/Shared/SIMF.Common/Grids) plus [`GridQueryExtensions.cs`](../../src/Backend/SIMF.Infrastructure/Common/Grids/GridQueryExtensions.cs) |

---

## 1. What the seam does

A list resource declares, once, which keys the Control Panel may sort, filter and
search on. One call then runs the whole pipeline:

```
validate -> filter -> search -> order + MANDATORY tiebreak -> CountAsync -> Skip/Take -> project
```

`AsNoTracking` is applied inside `ToGridPageAsync`, so no caller has to remember
it. The total is counted on the server, over the filtered set, before paging.

Two types matter:

| Type | Project | Role |
|------|---------|------|
| `GridColumns<TEntity>` | `SIMF.Common.Grids` | The declaration: keys, default order, page-size policy. |
| `GridQueryComposition.ApplyGrid` | `SIMF.Common.Grids` | Validate, filter, search, order, tiebreak. No paging. |
| `GridQueryExtensions.ToGridPageAsync` | `SIMF.Infrastructure.Common.Grids` | `ApplyGrid` plus count, page and project. Needs EF Core, which is why it is not beside the others. |

Usings you will need: `System.Linq.Expressions`, `SIMF.Common.Grids`,
`SIMF.Infrastructure.Common.Grids`.

---

## 2. The recipe

Copy `AdminThemeService`. It is three pieces: a static declaration, a static
projection, and a one-line list method.

```csharp
/// <summary>
/// The grid contract for /admin/themes: one entry per key ThemesList.razor can
/// send, as both its filter and its sort. A key not declared here is a 400, not
/// a silently ignored request.
/// </summary>
private static readonly GridColumns<Theme> Columns = new GridColumns<Theme>()
    .Add("code", theme => theme.Code, searchable: true)
    .Add("name", theme => theme.Name, searchable: true)
    .Add("nameArabic", theme => theme.NameArabic, searchable: true)
    .Add("displayOrder", theme => theme.DisplayOrder)
    .Add("isActive", theme => theme.IsActive)
    .DefaultOrder("displayOrder")
    .DefaultOrder("name")
    .PageSize(fallback: 25, max: 200);

private static readonly Expression<Func<Theme, AdminThemeSummary>> ToSummary =
    theme => new AdminThemeSummary(
        theme.Id, theme.Code, theme.Name, theme.NameArabic,
        theme.DisplayOrder, theme.PageColor, theme.IsActive,
        theme.CreatedAt, theme.Description, theme.DescriptionArabic);

public Task<GridPage<AdminThemeSummary>> ListAllAsync(
    GridQuery query, CancellationToken cancellationToken = default) =>
    dbContext.Themes.ToGridPageAsync(
        query, Columns, theme => theme.Id, ToSummary, cancellationToken);
```

Rules that come with it:

- **Declare the field `static readonly`.** The set is sealed on the first query
  and read concurrently afterwards; mutating it later throws rather than racing
  the readers. A misdeclaration (a non-string column marked `searchable`, a
  duplicate key, a `DefaultOrder` naming a key that is not sortable) throws at
  declaration time, which surfaces as a `TypeInitializationException` on the
  resource's first request and on the first test that touches it.
- **Keep `TProp` inferred.** `Add<TProp>` closes over the property's real CLR
  type, so there is no `Convert` node in the ordering tree. An
  `Expression<Func<T, object>>` column table would box, and EF then orders by
  the boxed value, which sorts a `DisplayOrder` of 10 before 2.
- **Keys match case-insensitively.** `GridQuery.Filters` is a plain
  case-sensitive dictionary and `SimfDataGrid` sends the key verbatim in
  camelCase, so the declaration is the one place the two are reconciled. Sending
  the same key in two cases is a 400, not an empty grid.
- **The projection is passed in**, so the SELECT pulls only the row's columns and
  may compute values that are not sortable or filterable at all. Any `Include`
  on the source is inert under a projection: delete it, do not carry it over.

Where a summary needs a value from the instance (an options value, a second
context), build the projection per call as a local `Expression<Func<T, TRow>>`;
`AdminSessionService.ListAllAsync` does exactly that.

---

## 3. Declare EVERY key the client can send

An undeclared key that a client sends is now a live 400. Before you finish a
declaration, read all three sources:

1. **The page**: every `<SimfDataGridColumn TItem="..." Key="..." Sortable Filterable>`
   in the `.razor`.
2. **The code-behind**: keys the page pins itself, for example
   `next.Filters["isVisitor"] = "true"` in `VisitorProfileTypesList.razor.cs`, or
   `_query.Filters["speakerId"]` in `SessionsList.razor.cs`.
3. **The endpoint**: keys the API injects. `HallEndpoints` builds the hall
   schedule panel by setting `query.Filters["hallId"]` and
   `query.Filters["isActive"]` before calling the same `ListAllAsync`, so both
   keys are declared on `AdminSessionService.Columns` even though the sessions
   page never sends `hallId`.

**Never drop a key the old service accepted, even if it ignored it.** Turning a
request that has always worked into a 400 is a live regression on a page nobody
changed. Two real cases:

- `AdminOperationLogService` keeps `actorUserId`, which is not a column on the
  viewer at all: the endpoint accepted it before this list moved onto the seam
  and an API caller may still send it.
- `AdminProfileTypeCommandService` keeps `userType` through `AddFilter`, because
  the old service ignored the key and still looked right (every non-admin profile
  type lives under `Visitor`). Honouring it makes the answer correct rather than
  accidental. Removing it broke five tests once already.

---

## 4. The escape hatch: `AddFilter`

`Add` infers the operator from the property type. Use `AddFilter` when the
predicate is not a comparison on one property. The key still joins the closed
allow-list, so an unknown key still 400s; only the predicate is bespoke, and the
key is filter-only, never sortable.

The builder receives the trimmed raw value and MUST return a predicate or throw.
There is no null return, because a null predicate is the silently skipped filter
this design exists to make unexpressible. Use `GridFilters.ValueInvalid` for the
standard bilingual 400.

**A date range over one column** (`AdminOperationLogService`), two half-ranges
that no per-column operator can express:

```csharp
.AddFilter("from", raw =>
{
    var start = GridFilters.ParseDay("from", raw);
    return row => row.Timestamp >= start;
})
.AddFilter("to", raw =>
{
    var end = GridFilters.ParseDay("to", raw).AddDays(1);
    return row => row.Timestamp < end;
})
```

The upper bound is half-open deliberately. It used to be `Timestamp <= to`, and
the picker hands over a bare date, so asking for "up to the 16th" dropped every
entry after midnight on the 16th, which is usually the day the incident being
traced happened.

**A navigation subquery** (`AdminSessionService`), for the Speakers grid's
"sessions for this speaker" deep link:

```csharp
.AddFilter("speakerId", raw =>
{
    if (!Guid.TryParse(raw, out var speakerId))
    {
        throw GridFilters.ValueInvalid("speakerId", raw, "a speaker id (GUID)");
    }
    return session => session.Speakers.Any(link => link.SpeakerId == speakerId);
})
```

`GridFilters.ParseDay` reads a value as a Saudi wall-clock calendar day, which is
what the columns store (see `SimfClock`). Do not convert to UTC in a builder: it
shifts the day and filters the previous one.

---

## 5. Default order, and the tiebreak that is not optional

`DefaultOrder` is called once per level, in order, and names the resource's
natural order for a request that sorts on nothing. It is validated at declaration
time, not on a user's request.

The `tiebreak` argument is required, and must be unique per row (normally the
primary key). A non-unique ORDER BY lets SQL Server return tied rows in any order
between two page requests, so the same row can appear on two pages while another
appears on none. Making it a required parameter is the only way to make that
mistake unexpressible; roughly 60 hand-written orderings had no tiebreak at all.

Two orderings follow the STORED form and surprise people:

- An enum mapped `HasConversion<string>()` sorts alphabetically, not in
  declaration order. Mapped `HasConversion<int>()` it sorts by ordinal.
- SQL Server orders NULLs first ascending and last descending, and that is not
  configurable per query, so a grid sorted ascending on a nullable column leads
  with blank rows.

---

## 6. What a bad request gets

| Situation | Code | Status |
|-----------|------|--------|
| Sort key not declared, or declared filter-only | `GRID_SORT_KEY_INVALID` | 400 |
| Filter key not declared, or sent twice in different cases | `GRID_FILTER_KEY_INVALID` | 400 |
| Filter value that will not parse | `GRID_FILTER_VALUE_INVALID` | 400 |
| Search term on a resource with no searchable column | `GRID_SEARCH_NOT_SUPPORTED` | 400 |
| More than 20 filter keys, or a search term over 128 characters | `VALIDATION_FAILED` | 400 |

Every message is bilingual and names the offending key plus the keys that would
have worked. A blank value is ignored, but its key is still checked: a stale key
is a client bug whether or not it carries a value today.

Nothing is ever silently skipped. A skipped filter widens the result set, and on
an admin grid over people a silently widened set is a disclosure, not an
inconvenience.

Values are parsed by CLR type: string is a literal substring (no wildcard
language, so a typed `%` matches nothing rather than everything), bool, Guid and
the numeric types parse invariantly, `DateTime` and `DateOnly` take a calendar
day, and enums parse by NAME only, case-insensitively. `"2"` does not quietly
become the second enum member.

---

## 7. Traps

**Encrypted columns.** A column declared over an AES-GCM value-converted property
compiles, translates, runs and matches nothing, forever: the term is encrypted
with a fresh nonce and compared against differently nonced ciphertext. On a people
grid "no rows" reads as "not registered", and no translation smoke test catches it
because translation succeeds. Check `SimfAppDbContext.OnModelCreating` before
declaring. The known set is `UserProfile.MobileNumber`, `UserProfile.SaudiMobile`,
`UserProfile.InternationalMobile` and `ProfileIdentityDocument.Number`. Filter on
a companion column instead: the document number has `NumberHash`, the deterministic
digest kept deliberately outside the converter for exactly that reason. Note that
the guard test below names only the three `UserProfile` columns, so the document
number is on you to remember.

**Scope predicates stay outside.** Compose them onto the source before the seam
runs, so they cannot be widened by a request:

```csharp
var responses = await dbContext.RatingResponses
    .Where(response => response.IsActive)
    .ToGridPageAsync(query, Columns, response => response.Id, ToSummary, cancellationToken);
```

Do not pre-page the source. A `Take` already on it makes EF push the ordering into
a subquery instead of clearing it for the count.

**Exports use `ApplyGrid`, not a second hand-written query.** The same filters,
the same search and the same order over the whole result set, with no `Skip`/`Take`,
so the workbook cannot drift out of parity with the grid it came from:

```csharp
var rows = await dbContext.OperationLog
    .AsNoTracking()
    .ApplyGrid(query, Columns, row => row.Id)
    .Take(ExportRowCap)
    .Select(ToSummary)
    .ToListAsync(cancellationToken);
```

`AdminRatingResponseService` uses the same split for a headline average over the
filtered set rather than over the page.

**When the seam does not fit.** The live record of which files stay hand-written is
the allow-list in `GridContractTests`, not this page, and it only ever shrinks. It
currently holds one entry, `InterestRepository`, which is there because it backs
the app's interest picker rather than a Control Panel grid: it has no `GridQuery`
to honour and nothing to declare.

A list that spans both databases is **not** automatically exempt. Where the
cross-database work produces a scope predicate, that predicate composes onto the
source ahead of the grid's own filters and the paged query still goes through the
seam; the admin account, attendee and invitation lists all do exactly that. Only
two shapes genuinely resist it: a sort whose column lives on the other database, so
there is no single `IQueryable` to order; and a free-text search that ORs a branch
resolved on the other database, which is why `ListAccountsAsync` composes its
filters and ordering through `ApplyGrid` but keeps its own search. If your list has
one of those shapes, use `ApplyGrid` for the part that fits and say in the code
which part does not. Anything else declares columns.

---

## 8. The tests that fail if you get it wrong

[`tests/SIMF.Api.Tests/GridContractTests.cs`](../../tests/SIMF.Api.Tests/GridContractTests.cs)
holds three facts, and it reads the tree rather than a fixture, so a new list is
covered the moment it is written:

1. `Every_sortable_or_filterable_control_panel_column_is_declared_by_its_service`
   walks the razor files and the services, joins them on the row DTO, and fails
   when a page marks a column `Sortable` or `Filterable` that no service declares.
   That is the wrong-sort bug class: three shipped and were each found by eye
   weeks later.
2. `The_number_of_hand_written_sort_switches_only_goes_down` is a ratchet over the
   files it names. A file matching `query.Sort?.ToLowerInvariant()` that is not on
   that list fails the build, so hand-rolling a filter or sort block is a
   regression by definition.
3. `No_grid_column_is_declared_over_an_encrypted_column` scans the `.Add("key", x => x.Property`
   declarations for the encrypted property names.

[`tests/SIMF.Api.Tests/GridColumnsTests.cs`](../../tests/SIMF.Api.Tests/GridColumnsTests.cs)
pins the seam's own contract against an in-memory `IQueryable`: unknown sort key,
unknown filter key, unparseable value, search with nothing searchable,
case-insensitive key matching, the duplicate-case rejection, the tiebreak, and
descending on every declared column.

Per-resource behaviour is still covered by the integration tests that drive the
service through the API, for example `AdminGridV2Tests`, `AdminGridOthersTests`
and `AdminGridVisitorsTests`.

---

## 9. Definition of done for a new list

Per the project rules (D-246), the same changeset carries:

- the column declaration, with every key the page, its code-behind and its
  endpoint can send;
- the permission gate on the endpoint and on the page (see the
  [Auth and Permissions guide](SIMF-Auth-Permissions-Dev-Guide.md));
- unit plus integration tests, and the per-page E2E catalogue file under
  `docs/tests/e2e/`;
- the row in `docs/pages/PAGE-INDEX.md` and the per-page reference doc.
