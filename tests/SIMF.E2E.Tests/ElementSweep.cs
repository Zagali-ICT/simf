// WS4 — the Playwright side of the element sweep.
//
// This runs `tools/qa/element-sweep.js` — the SAME file the interactive Chrome
// DevTools runs execute — inside the page. There is no second implementation to
// keep in step: the sweep is plain DOM with no imports precisely so one source
// can serve both the exploratory pass and CI.
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace SIMF.E2E.Tests;

/// <summary>The sweep's report for one page. Mirrors the JS return shape.</summary>
public sealed record SweepReport
{
    [JsonPropertyName("route")] public string Route { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("dir")] public string Dir { get; init; } = "ltr";
    [JsonPropertyName("overflowX")] public bool OverflowX { get; init; }
    [JsonPropertyName("counts")] public SweepCounts Counts { get; init; } = new();
    [JsonPropertyName("disabled")] public SweepDisabled Disabled { get; init; } = new();
    [JsonPropertyName("inventory")] public SweepInventory Inventory { get; init; } = new();
    [JsonPropertyName("problems")] public SweepProblems Problems { get; init; } = new();
    [JsonPropertyName("capped")] public SweepCap? Capped { get; init; }
    [JsonPropertyName("pass")] public bool Pass { get; init; }

    /// <summary>A failure message naming what actually went wrong, so a red CI
    /// run is actionable without opening a browser.</summary>
    public string Describe()
    {
        var lines = new List<string> { $"element sweep failed on {Route}" };
        foreach (var control in Problems.Unnamed)
        {
            lines.Add($"  unnamed {control.Kind}: {control.Element}");
        }
        foreach (var image in Problems.BrokenImages)
        {
            lines.Add($"  broken image: {image.Src}");
        }
        foreach (var link in Problems.BadLinks)
        {
            lines.Add($"  {link.Status}: {link.Path}"
                + (link.From is null ? "" : $"  (from \"{link.From}\")"));
        }
        if (OverflowX)
        {
            lines.Add("  horizontal overflow (scrollWidth > clientWidth)");
        }
        if (Capped is { } cap)
        {
            lines.Add($"  link check truncated: {cap.Checked} of {cap.Found} — "
                + "raise maxUrlChecks; a partial check must not read as clean");
        }
        return string.Join('\n', lines);
    }
}

public sealed record SweepCounts
{
    [JsonPropertyName("buttons")] public int Buttons { get; init; }
    [JsonPropertyName("links")] public int Links { get; init; }
    [JsonPropertyName("inputs")] public int Inputs { get; init; }
    [JsonPropertyName("selects")] public int Selects { get; init; }
    [JsonPropertyName("textareas")] public int Textareas { get; init; }
    [JsonPropertyName("images")] public int Images { get; init; }

    /// <summary>SimfDataGrids actually rendered — lets the caller tell "this page's
    /// grid sits behind a precondition" from "this page's grid regressed away".</summary>
    [JsonPropertyName("grids")] public int Grids { get; init; }
}

public sealed record SweepDisabled
{
    [JsonPropertyName("buttons")] public int Buttons { get; init; }
    [JsonPropertyName("inputs")] public int Inputs { get; init; }
}

public sealed record SweepInventory
{
    [JsonPropertyName("buttons")] public List<SweepButton> Buttons { get; init; } = [];
    [JsonPropertyName("links")] public List<SweepLink> Links { get; init; } = [];
}

public sealed record SweepButton
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("disabled")] public bool Disabled { get; init; }
}

public sealed record SweepLink
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("href")] public string? Href { get; init; }
}

public sealed record SweepProblems
{
    [JsonPropertyName("unnamed")] public List<SweepUnnamed> Unnamed { get; init; } = [];
    [JsonPropertyName("brokenImages")] public List<SweepImage> BrokenImages { get; init; } = [];
    [JsonPropertyName("badLinks")] public List<SweepBadLink> BadLinks { get; init; } = [];
}

public sealed record SweepUnnamed
{
    [JsonPropertyName("kind")] public string Kind { get; init; } = "";
    [JsonPropertyName("el")] public string Element { get; init; } = "";
}

public sealed record SweepImage
{
    [JsonPropertyName("src")] public string? Src { get; init; }
}

public sealed record SweepBadLink
{
    [JsonPropertyName("path")] public string Path { get; init; } = "";
    [JsonPropertyName("status")] public JsonElement Status { get; init; }

    /// <summary>The raw href/src that resolved to this path — without it a
    /// "/admin/ 404" reported on 61 pages names no anchor to go and fix.</summary>
    [JsonPropertyName("from")] public string? From { get; init; }
}

public sealed record SweepCap
{
    [JsonPropertyName("found")] public int Found { get; init; }
    [JsonPropertyName("checked")] public int Checked { get; init; }
}

public static class ElementSweep
{
    private static readonly Lazy<string> Source = new(() =>
    {
        // Copied next to the assembly by the .csproj, so the runner never
        // depends on the repo layout at test time.
        var path = Path.Combine(AppContext.BaseDirectory, "qa", "element-sweep.js");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "tools/qa/element-sweep.js was not copied to the output. The "
                + "sweep source is shared with the interactive runs — do not "
                + "inline a second copy here.", path);
        }
        return File.ReadAllText(path);
    });

    /// <summary>Runs the shared sweep against the page as it currently stands.</summary>
    public static async Task<SweepReport> RunAsync(IPage page)
    {
        var raw = await page.EvaluateAsync<JsonElement>(Source.Value);
        return raw.Deserialize<SweepReport>()
            ?? throw new InvalidOperationException(
                "the element sweep returned nothing for " + page.Url);
    }
}
