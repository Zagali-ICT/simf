// FR-1203-markdown-render — ContentBlock's XML doc claimed "markdown allowed"
// while nothing on any surface rendered markdown: the Website hydrator writes the
// values with textContent (or HTML-escapes them through esc()), the CP prints
// them through Razor's auto-encoder, and the app decodes them into Text widgets.
//
// The ruling (2026-07-30) was to make the CONTRACT match the behaviour rather
// than build a renderer:
//
//   * every content-block key in use is a short plain-text field — an eyebrow, a
//     heading, a one-line paragraph, a counter ("stats.count1"), a button label
//     (see LandingSectionContentKeys / LandingHeroContentKeys). None is a
//     long-form document, so markdown buys nothing;
//   * rendering markdown would change what already-seeded production content
//     looks like — a '#' or '*' inside an Arabic paragraph would silently become
//     a heading;
//   * and it would require injecting HTML built from an ADMIN-EDITABLE field into
//     a public page that today has no such path at all. That is the one thing the
//     item's own rule forbids.
//
// These tests pin the behaviour the corrected contract describes: content-block
// text is displayed as TEXT on every surface this repo owns, and no new raw-markup
// path opens up behind us.
using System.Text.RegularExpressions;
using Bunit;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class ContentBlockPlainTextContractTests : CpComponentTestBase
{
    private const string ScriptPayload = "<script>alert('xss')</script>";

    [Fact]
    public void A_script_tag_in_a_content_block_is_shown_as_text_not_executed_markup()
    {
        // The CP is itself a consumer: one admin's content-block text is rendered
        // back to every other admin on the View/Delete pane. If a markdown/HTML
        // path were ever added, this is the first place it would become stored XSS
        // inside the admin tool.
        var block = new AdminContentBlockSummary(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "home.welcome.title",
            ScriptPayload,
            ScriptPayload,
            IsActive: true,
            DateTime.UnixEpoch,
            Guid.Empty);

        var cut = RenderComponent<ContentBlockViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, block));

        Assert.Empty(cut.FindAll("script"));
        Assert.DoesNotContain("<script>", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
        // The admin still sees exactly what was typed — escaped, not swallowed.
        Assert.Contains(ScriptPayload, cut.Find("dl.simf-dl").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void No_admin_editable_text_is_rendered_as_raw_markup()
    {
        // Razor encodes by default, so the ONLY way admin-editable text becomes
        // markup is an explicit (MarkupString) cast. The accepted uses all render
        // an SVG this server generated itself (the TOTP pairing QR, the badge QR)
        // or a compile-time icon body — never a value an admin typed. The rule is
        // stated on the expression rather than on a file list so that a new page
        // rendering a server-built QR passes, while `(MarkupString)block.Content`
        // fails wherever it is written.
        var offenders = new List<string>();

        foreach (var file in OwnedFrontEndSources())
        {
            if (Path.GetFileName(file) == "SimfIcon.razor")
            {
                continue;   // renders its own compile-time icon path data
            }

            foreach (Match cast in Regex.Matches(
                File.ReadAllText(file), @"\(MarkupString\)\s*([^\)\r\n]+)"))
            {
                var expression = cast.Groups[1].Value.Trim();
                if (!expression.Contains("Svg", StringComparison.Ordinal)
                    && !expression.Contains("svg", StringComparison.Ordinal))
                {
                    offenders.Add($"  {Relative(file)}: (MarkupString){expression}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "FR-1203: something is rendered as raw markup that is not a server-built "
            + "SVG. Admin-editable text (content blocks, banners, news, session copy) "
            + "must never be cast to MarkupString — there is no sanitizer behind it:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void The_public_landing_hydrator_escapes_every_value_from_the_content_feed()
    {
        // site-content.js builds card markup by string concatenation and assigns it
        // to innerHTML, so the feed's values (content blocks, session titles, news
        // excerpts — all admin-editable) pass within one character of being markup.
        var js = ReadSource("src/Website/SIMF.Web/wwwroot/js/site-content.js");

        Assert.Contains("""replace(/&/g, '&amp;')""", js, StringComparison.Ordinal);
        Assert.Contains("""replace(/</g, '&lt;')""", js, StringComparison.Ordinal);
        Assert.Contains("""replace(/>/g, '&gt;')""", js, StringComparison.Ordinal);
        Assert.Contains("""replace(/"/g, '&quot;')""", js, StringComparison.Ordinal);
        Assert.Contains("""replace(/'/g, '&#39;')""", js, StringComparison.Ordinal);

        // The single-value path — which is what every CMS field goes through —
        // never touches innerHTML at all.
        Assert.Contains("el.textContent = v;", js, StringComparison.Ordinal);

        // Four markup sinks exist (sessions, news, partners, speakers) and every
        // value each one interpolates is wrapped in esc(). Pinning the COUNT means
        // a fifth sink cannot be added without this failing and being read.
        Assert.Equal(4, Regex.Matches(js, @"\.innerHTML\s*=").Count);
    }

    // ----------------------------------------------------------------------

    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>Every Razor/C# front-end source this track owns — the Control
    /// Panel, the Website and the shared component library.</summary>
    private static IEnumerable<string> OwnedFrontEndSources()
    {
        foreach (var project in new[]
        {
            "src/ControlPanel/SIMF.ControlPanel",
            "src/Website/SIMF.Web",
            "src/Shared/SIMF.Components",
        })
        {
            var directory = Path.Combine(RepoRoot, project.Replace('/', Path.DirectorySeparatorChar));
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }
                if (file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }

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
