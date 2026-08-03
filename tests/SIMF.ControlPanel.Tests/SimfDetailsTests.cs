// Tests: src/Shared/SIMF.Components/Forms/SimfDetails.razor + SimfDetailRow.razor
//
// D-835 shipped these two components and SEVEN grids that use them, and nothing
// rendered either one. That is the same shape of hole that let D-830 ship a
// render-time crash: the ratchet reads source, the page-render smoke suite
// renders each page BARE, and every <SimfDetails> on those pages sits behind a
// null check (`Open="@(_entryDetails is not null)"`) that is false on first
// paint. So the markup compiled, passed 578 tests, and had still never been
// executed by a renderer.
//
// These render it.
using Bunit;
using Microsoft.AspNetCore.Components;
using SIMF.Components.Forms;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SimfDetailsTests : CpComponentTestBase
{
    public SimfDetailsTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedFragment RenderDetails(bool open, RenderFragment rows) =>
        Render(builder =>
        {
            builder.OpenComponent<SimfDetails>(0);
            builder.AddAttribute(1, nameof(SimfDetails.Open), open);
            builder.AddAttribute(2, nameof(SimfDetails.Title), "Majlis A");
            builder.AddAttribute(3, nameof(SimfDetails.CloseLabel), "Close");
            builder.AddAttribute(4, nameof(SimfDetails.ChildContent), rows);
            builder.CloseComponent();
        });

    private static RenderFragment Row(string label, string? value) => builder =>
    {
        builder.OpenComponent<SimfDetailRow>(0);
        builder.AddAttribute(1, nameof(SimfDetailRow.Label), label);
        builder.AddAttribute(2, nameof(SimfDetailRow.Value), value);
        builder.CloseComponent();
    };

    [Fact]
    public void Closed_renders_nothing_at_all()
    {
        // The state every one of the seven grids is in on first paint. If this
        // rendered a modal, every page using it would open with a dialog over it.
        var cut = RenderDetails(open: false, Row("Code", "T-01"));

        Assert.DoesNotContain("simf-dl", cut.Markup);
        Assert.DoesNotContain("T-01", cut.Markup);
    }

    [Fact]
    public void Open_renders_the_definition_list_with_each_row()
    {
        var cut = RenderDetails(open: true, builder =>
        {
            Row("Code", "T-01")(builder);
            Row("Capacity", "8")(builder);
        });

        // The dl/dt/dd shape is what .simf-dl styles; a div soup would render
        // unstyled and only a real render can tell the difference.
        var list = cut.Find("dl.simf-dl");
        Assert.Equal(2, list.QuerySelectorAll("dt").Length);
        Assert.Equal(2, list.QuerySelectorAll("dd").Length);
        Assert.Contains("Code", list.QuerySelectorAll("dt")[0].TextContent);
        Assert.Contains("T-01", list.QuerySelectorAll("dd")[0].TextContent);
        Assert.Contains("Capacity", list.QuerySelectorAll("dt")[1].TextContent);
        Assert.Contains("8", list.QuerySelectorAll("dd")[1].TextContent);
    }

    [Fact]
    public void A_blank_value_renders_the_not_set_glyph_rather_than_an_empty_cell()
    {
        // Every one of the seven views binds at least one nullable field
        // (RowLabel, ColumnNumber, UnitCount, RowColumnSpec, Notes, JobTitle,
        // CommentLabel...). A reader has to be able to tell "not set" apart from
        // a rendering fault, and the glyph must match the grid columns beside it.
        var cut = RenderDetails(open: true, builder =>
        {
            Row("Notes", null)(builder);
            Row("Rows / columns", "   ")(builder);
        });

        var cells = cut.Find("dl.simf-dl").QuerySelectorAll("dd");
        Assert.Equal("—", cells[0].TextContent.Trim());
        Assert.Equal("—", cells[1].TextContent.Trim());
    }

    [Fact]
    public void Markup_content_wins_over_the_plain_value()
    {
        // How the active/inactive pill is rendered on four of the seven views.
        var cut = RenderDetails(open: true, builder =>
        {
            builder.OpenComponent<SimfDetailRow>(0);
            builder.AddAttribute(1, nameof(SimfDetailRow.Label), "Status");
            builder.AddAttribute(2, nameof(SimfDetailRow.Value), "ignored");
            builder.AddAttribute(3, nameof(SimfDetailRow.ChildContent),
                (RenderFragment)(inner => inner.AddMarkupContent(0, "<span class=\"pill\">Active</span>")));
            builder.CloseComponent();
        });

        var cell = cut.Find("dl.simf-dl dd");
        Assert.Contains("Active", cell.TextContent);
        Assert.DoesNotContain("ignored", cut.Markup);
    }

    [Fact]
    public void It_renders_no_committing_control()
    {
        // The whole point: Details is a READ path, wired with no permission
        // because reading the row is what the page's View gate already bought.
        // If it ever grew a Save or Delete, that reasoning would silently become
        // a hole - an ungated mutating button on seven pages.
        var cut = RenderDetails(open: true, builder =>
        {
            Row("Code", "T-01")(builder);
        });

        foreach (var button in cut.FindAll("button"))
        {
            var label = (button.TextContent + " "
                + (button.GetAttribute("aria-label") ?? string.Empty)).ToLowerInvariant();
            Assert.True(
                !label.Contains("save") && !label.Contains("delete")
                    && !label.Contains("submit") && !label.Contains("confirm"),
                $"SimfDetails rendered a committing control ('{button.TextContent.Trim()}'). "
                + "It is a read-only view and carries no permission, so a mutating "
                + "button here would be reachable by anyone who can open the page.");
        }

        Assert.Empty(cut.FindAll("input"));
        Assert.Empty(cut.FindAll("textarea"));
    }

    [Fact]
    public void Reopening_after_a_close_renders_the_rows_again()
    {
        // The pages close by nulling the backing field, so the component goes
        // Open=true -> false -> true across a reader's session. Rendered behind
        // @if (Open), so this proves the instance is rebuilt cleanly rather than
        // coming back empty.
        var cut = RenderComponent<SimfDetails>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Majlis A")
            .Add(p => p.ChildContent, Row("Code", "T-01")));
        Assert.Contains("T-01", cut.Markup);

        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Open, false));
        Assert.DoesNotContain("T-01", cut.Markup);

        cut.SetParametersAndRender(parameters => parameters.Add(p => p.Open, true));
        Assert.Contains("T-01", cut.Markup);
    }
}
