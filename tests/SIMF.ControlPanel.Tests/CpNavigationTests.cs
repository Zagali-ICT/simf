namespace SIMF.ControlPanel.Tests;

/// <summary>Tests for the Control Panel navigation map.</summary>
public sealed class CpNavigationTests
{
    [Fact]
    public void There_are_nine_navigation_groups()
    {
        Assert.Equal(9, CpNavigation.Groups.Count);
    }

    [Fact]
    public void Every_module_route_is_unique()
    {
        var routes = CpNavigation.Groups.SelectMany(group => group.Items)
            .Select(item => item.Href)
            .ToList();

        Assert.Equal(routes.Count, routes.Distinct().Count());
    }

    [Theory]
    [InlineData("/", "Module.Dashboard")]
    [InlineData("/m/sessions", "Module.Sessions")]
    [InlineData("/M/SESSIONS", "Module.Sessions")]
    public void LabelKeyForHref_resolves_a_known_route(string href, string expectedKey)
    {
        Assert.Equal(expectedKey, CpNavigation.LabelKeyForHref(href));
    }

    [Fact]
    public void LabelKeyForHref_returns_null_for_an_unknown_route()
    {
        Assert.Null(CpNavigation.LabelKeyForHref("/m/does-not-exist"));
    }
}
