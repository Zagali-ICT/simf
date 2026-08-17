// Tests: this IS the test for SIMF.Common/Grids.
//
// These run against an in-memory IQueryable rather than a database on purpose: what
// is under test is the CONTRACT (which keys are accepted, what happens to the ones
// that are not, and that an ordering always ends in the tiebreak), and that contract
// is decided entirely by expression composition. The SQL translation of the same
// trees is covered by the per-resource integration tests that already drive these
// services through the API.
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Domain.Programme;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class GridColumnsTests
{
    private static readonly GridColumns<Theme> Columns = new GridColumns<Theme>()
        .Add("code", theme => theme.Code, searchable: true)
        .Add("name", theme => theme.Name, searchable: true)
        .Add("displayOrder", theme => theme.DisplayOrder)
        .Add("isActive", theme => theme.IsActive)
        .DefaultOrder("displayOrder");

    // A fixed set, not a property that mints fresh ids per access: the tiebreak test
    // compares two independent compositions of the SAME rows, which only means
    // anything if the ids are stable between them.
    private static readonly Theme[] Rows =
    [
        NewTheme("B-CODE", "Maritime", order: 2, active: true),
        NewTheme("A-CODE", "Logistics", order: 1, active: false),
        NewTheme("C-CODE", "Maritime Safety", order: 1, active: true),
    ];

    private static IQueryable<Theme> Themes => Rows.AsQueryable();

    // --- the rule that was missing everywhere: an unknown key is loud -------------

    [Fact]
    public void UnknownSortKeyIsRejectedRatherThanFallingBackToNaturalOrder()
    {
        var query = new GridQuery { Sort = "notAColumn" };

        var error = Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList());

        Assert.Equal(ErrorCodes.GridSortKeyInvalid, error.Code);
        Assert.Equal(400, error.StatusCode);
        // The offending key is named, and so are the ones that would have worked.
        Assert.Contains("notAColumn", error.Message, StringComparison.Ordinal);
        Assert.Contains("displayOrder", error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(error.MessageArabic);
    }

    [Fact]
    public void UnknownFilterKeyIsRejectedRatherThanReturningEveryRow()
    {
        // The live defect this replaces: five CP filter boxes sent a key no service
        // read, so typing into them returned the UNFILTERED set with no error. On an
        // admin grid over people a silently widened set is a disclosure.
        var query = new GridQuery { Filters = { ["notAColumn"] = "x" } };

        var error = Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList());

        Assert.Equal(ErrorCodes.GridFilterKeyInvalid, error.Code);
        Assert.Equal(400, error.StatusCode);
        Assert.Contains("notAColumn", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnparseableFilterValueIsRejectedRatherThanSkipped()
    {
        // bool.TryParse failing used to drop the whole predicate, which widens the
        // result set exactly like a missing key does.
        var query = new GridQuery { Filters = { ["isActive"] = "yes-please" } };

        var error = Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList());

        Assert.Equal(ErrorCodes.GridFilterValueInvalid, error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void SearchOnAResourceWithNoSearchableColumnsIsRejected()
    {
        var noSearch = new GridColumns<Theme>().Add("code", theme => theme.Code);
        var query = new GridQuery { Search = "maritime" };

        var error = Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(query, noSearch, theme => theme.Id).ToList());

        Assert.Equal(ErrorCodes.GridSearchNotSupported, error.Code);
    }

    // --- the two policies that used to disagree on one request object -------------

    [Fact]
    public void FilterKeysMatchRegardlessOfCase()
    {
        // GridQuery.Filters is a case-SENSITIVE dictionary and the grid sends the
        // column key verbatim, while the services this replaces matched on the
        // lowercased form. Reconciling the two here is the whole point.
        var query = new GridQuery { Filters = { ["ISACTIVE"] = "true" } };

        var rows = Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, theme => Assert.True(theme.IsActive));
    }

    [Fact]
    public void TheSameColumnSentTwiceInDifferentCasesIsRejected()
    {
        // Both bind to one column and AND to the empty set, which reads as an empty
        // grid with no explanation.
        var query = new GridQuery
        {
            Filters = { ["isActive"] = "true", ["IsActive"] = "false" },
        };

        var error = Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList());

        Assert.Equal(ErrorCodes.GridFilterKeyInvalid, error.Code);
    }

    // --- the ordering rule EF Core warns about -----------------------------------

    [Fact]
    public void OrderingAlwaysEndsInTheTiebreakSoPagingCannotRepeatOrDropARow()
    {
        // Two themes share DisplayOrder 1. Without a tiebreak SQL Server may return
        // them in either order between two page requests, so one row can appear on
        // two pages while another appears on none.
        var query = new GridQuery { Sort = "displayOrder" };

        var first = Themes.ApplyGrid(query, Columns, theme => theme.Id)
            .Select(theme => theme.Id).ToList();
        var second = Themes.ApplyGrid(query, Columns, theme => theme.Id)
            .Select(theme => theme.Id).ToList();

        Assert.Equal(first, second);

        // The tied pair is ordered by Id, not left to the provider.
        var tied = Themes.ApplyGrid(query, Columns, theme => theme.Id)
            .Where(theme => theme.DisplayOrder == 1)
            .Select(theme => theme.Id)
            .ToList();
        Assert.Equal(tied.OrderBy(id => id).ToList(), tied);
    }

    [Fact]
    public void DescendingWorksOnEveryDeclaredColumnRatherThanOnlyTheOnesWithAnArm()
    {
        // The hand-written switches carried arms per (key, direction) pair, so a
        // missing `false` or `true` arm fell through the catch-all and quietly
        // returned a different order than the one the arrow claimed.
        var query = new GridQuery { Sort = "displayOrder", SortDescending = true };

        var orders = Themes.ApplyGrid(query, Columns, theme => theme.Id)
            .Select(theme => theme.DisplayOrder).ToList();

        Assert.Equal([2, 1, 1], orders);
    }

    // --- filtering and search ------------------------------------------------------

    [Fact]
    public void SearchMatchesAcrossEverySearchableColumn()
    {
        var byName = new GridQuery { Search = "Maritime" };
        Assert.Equal(2, Themes.ApplyGrid(byName, Columns, theme => theme.Id).Count());

        var byCode = new GridQuery { Search = "A-CODE" };
        Assert.Single(Themes.ApplyGrid(byCode, Columns, theme => theme.Id));
    }

    [Fact]
    public void ADeclaredStringColumnFiltersOnASubstring()
    {
        // ThemesList.razor marks `name` Filterable and the service it replaces had
        // no name filter at all, so this box returned every row.
        var query = new GridQuery { Filters = { ["name"] = "Logistics" } };

        var rows = Themes.ApplyGrid(query, Columns, theme => theme.Id).ToList();

        Assert.Single(rows);
        Assert.Equal("Logistics", rows[0].Name);
    }

    [Fact]
    public void AWildcardInAFilterIsMatchedLiterallyRatherThanAsAPattern()
    {
        // The services this replaces built EF.Functions.Like($"%{term}%") with no
        // escaping, so a `%` typed into a filter box matched every row.
        var query = new GridQuery { Filters = { ["name"] = "%" } };

        Assert.Empty(Themes.ApplyGrid(query, Columns, theme => theme.Id));
    }

    [Fact]
    public void ABlankFilterValueIsIgnoredButItsKeyIsStillChecked()
    {
        var known = new GridQuery { Filters = { ["isActive"] = "  " } };
        Assert.Equal(3, Themes.ApplyGrid(known, Columns, theme => theme.Id).Count());

        // A stale key is a client bug whether or not it carries a value today.
        var unknown = new GridQuery { Filters = { ["gone"] = string.Empty } };
        Assert.Throws<ApiException>(
            () => Themes.ApplyGrid(unknown, Columns, theme => theme.Id).ToList());
    }

    // --- misdeclarations fail at declaration, not on a user's request -------------

    [Fact]
    public void ANonStringColumnCannotBeDeclaredSearchable()
    {
        Assert.Throws<ArgumentException>(
            () => new GridColumns<Theme>().Add("displayOrder", t => t.DisplayOrder, searchable: true));
    }

    [Fact]
    public void AColumnCannotBeDeclaredTwiceEvenInADifferentCase()
    {
        Assert.Throws<ArgumentException>(
            () => new GridColumns<Theme>()
                .Add("name", theme => theme.Name)
                .Add("Name", theme => theme.NameArabic));
    }

    [Fact]
    public void ADefaultOrderMustNameASortableColumn()
    {
        Assert.Throws<ArgumentException>(
            () => new GridColumns<Theme>().Add("name", t => t.Name).DefaultOrder("nope"));
    }

    private static Theme NewTheme(string code, string name, int order, bool active) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            NameArabic = name,
            DisplayOrder = order,
            IsActive = active,
        };
}
