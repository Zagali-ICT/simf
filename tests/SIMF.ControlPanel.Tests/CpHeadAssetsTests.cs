// QA-LIVE-001 — the CP shell head is rendered on EVERY Control Panel page, so a
// single wrong href there is a 404 on every page load. Two were found this way:
//
//   * No <link rel="icon"> at all, so the browser fell back to requesting
//     /favicon.ico — which nothing served. Every CP page logged a 404 and the
//     browser tab carried the blank default glyph.
//   * The same class of defect had already shipped on the Website, where the head
//     linked SIMF.Web.styles.css — a scoped-CSS bundle the SDK never emits for a
//     project with no .razor.css — and the SPA fallback answered every request
//     with HTML, so all 17 public routes logged a MIME-type refusal.
//
// A render test cannot catch either one: bUnit does not fetch the assets, and a
// live browser check only covers the page someone happened to open. Resolving
// every href in the head against the file that must serve it does.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpHeadAssetsTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpProjectDir = "src/ControlPanel/SIMF.ControlPanel";

    private const string SharedComponentsProjectDir = "src/Shared/SIMF.Components";

    /// <summary>Every <c>@Assets["…"]</c> lookup in the shell document, whether it
    /// is a stylesheet href, an icon href or a script src.</summary>
    private static readonly Regex AssetLookup =
        new("""@Assets\[\s*"([^"]+)"\s*\]""", RegexOptions.Compiled);

    [Fact]
    public void The_shell_head_declares_a_favicon()
    {
        var app = ReadSource($"{CpProjectDir}/Components/App.razor");

        var icon = Regex.Match(
            app, """<link\s+rel="icon"[^>]*href="@Assets\[\s*"([^"]+)"\s*\][^>]*>""");

        Assert.True(icon.Success,
            "QA-LIVE-001: the CP shell head declares no <link rel=\"icon\">, so every "
            + "page load falls back to requesting /favicon.ico and gets a 404. Add the "
            + "link next to the stylesheet links in Components/App.razor.");

        Assert.True(File.Exists(WwwrootPath(CpProjectDir, icon.Groups[1].Value)),
            "QA-LIVE-001: the head declares an icon at '" + icon.Groups[1].Value
            + "' but no such file exists under " + CpProjectDir + "/wwwroot, so the "
            + "declared icon 404s exactly like the fallback it replaced.");
    }

    [Fact]
    public void Every_local_asset_the_shell_head_references_exists_on_disk()
    {
        var missing = new List<string>();

        foreach (Match lookup in AssetLookup.Matches(ReadSource($"{CpProjectDir}/Components/App.razor")))
        {
            var asset = lookup.Groups[1].Value;

            // _framework/* is emitted by the Blazor build, and _content/Cropper.Blazor/*
            // comes out of a NuGet package's static web assets — neither has a source
            // file to resolve against. SIMF.ControlPanel.styles.css is the SDK's
            // scoped-CSS bundle, checked separately below because its failure mode is
            // "the project stopped having scoped CSS", not "the file moved".
            if (asset.StartsWith("_framework/", StringComparison.Ordinal)
                || asset.StartsWith("_content/Cropper.Blazor/", StringComparison.Ordinal)
                || asset == "SIMF.ControlPanel.styles.css")
            {
                continue;
            }

            var path = asset.StartsWith("_content/SIMF.Components/", StringComparison.Ordinal)
                ? WwwrootPath(SharedComponentsProjectDir, asset["_content/SIMF.Components/".Length..])
                : WwwrootPath(CpProjectDir, asset);

            if (!File.Exists(path))
            {
                missing.Add($"  {asset} — expected at {Relative(path)}");
            }
        }

        Assert.True(missing.Count == 0,
            "QA-LIVE-001: the CP shell head references assets that do not exist, so "
            + "every Control Panel page load 404s on each of them:\n"
            + string.Join('\n', missing));
    }

    [Fact]
    public void The_scoped_css_bundle_the_head_links_is_actually_emitted()
    {
        // The Razor SDK emits <Assembly>.styles.css only when the project HAS at
        // least one .razor.css. The Website shipped that link with no scoped CSS
        // and 404'd site-wide; the CP link is only correct while this holds.
        var app = ReadSource($"{CpProjectDir}/Components/App.razor");
        if (!app.Contains("SIMF.ControlPanel.styles.css", StringComparison.Ordinal))
        {
            return;
        }

        var scoped = Directory.EnumerateFiles(
                Path.Combine(RepoRoot, CpProjectDir.Replace('/', Path.DirectorySeparatorChar)),
                "*.razor.css", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Take(1)
            .ToList();

        Assert.True(scoped.Count > 0,
            "The CP shell head links SIMF.ControlPanel.styles.css, but the project no "
            + "longer contains any .razor.css, so the Razor SDK does not emit that "
            + "bundle and the link 404s on every page. Either restore the scoped CSS "
            + "or drop the link (the Website hit exactly this).");
    }

    // ----------------------------------------------------------------------

    private static string WwwrootPath(string projectDir, string relativeAsset) =>
        Path.Combine(
            RepoRoot,
            projectDir.Replace('/', Path.DirectorySeparatorChar),
            "wwwroot",
            relativeAsset.Replace('/', Path.DirectorySeparatorChar));

    private static string Relative(string absolute) =>
        Path.GetRelativePath(RepoRoot, absolute).Replace(Path.DirectorySeparatorChar, '/');

    private static string ReadSource(string relativePath)
    {
        var absolute = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolute), $"Expected source file not found: {absolute}");
        return File.ReadAllText(absolute);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
