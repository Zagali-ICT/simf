// Tests: the full-route sweep (2026-08-14) found /meeting/confirm shipping with no
// <title> element at all, so its browser tab showed a bare URL. It was the only
// Website page carrying a no-prerender render mode, and App.razor renders
// <HeadOutlet /> statically: a <PageTitle> that exists only inside the interactive
// circuit has no outlet to reach on the first response, so the title is simply lost.
//
// Nothing failed. It compiled, the page worked, every test passed, and no test
// asserted a title — the sweep only noticed because every other page in the same
// pass had one. This is a ratchet so the next page that opts out of prerendering
// breaks the build instead of quietly losing its title too.
using Xunit;

namespace SIMF.Web.Tests;

public sealed class PageTitleReachesTheHeadTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string WebRoot =>
        Path.Combine(RepoRoot, "src", "Website", "SIMF.Web");

    /// <summary>True when App.razor's HeadOutlet carries no render mode, i.e. the
    /// head is rendered statically and only server-rendered PageTitles reach it.</summary>
    private static bool HeadOutletIsStatic(out string appRazor)
    {
        appRazor = File.ReadAllText(
            Path.Combine(WebRoot, "Components", "App.razor"));
        var at = appRazor.IndexOf("<HeadOutlet", StringComparison.Ordinal);
        Assert.True(at >= 0, "App.razor no longer renders a <HeadOutlet />.");
        var end = appRazor.IndexOf('>', at);
        var tag = appRazor[at..end];
        return !tag.Contains("@rendermode", StringComparison.Ordinal);
    }

    [Fact]
    public void No_routable_page_disables_prerendering_while_the_head_is_static()
    {
        if (!HeadOutletIsStatic(out _))
        {
            // The head became interactive; a page may then opt out safely and this
            // guard no longer applies. Deliberately not asserted the other way -
            // that is a design choice, not a defect.
            return;
        }

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(WebRoot, "Components"), "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("@page ", StringComparison.Ordinal)) { continue; }

            foreach (var line in source.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("@rendermode", StringComparison.Ordinal)) { continue; }
                if (trimmed.Contains("NoPrerender", StringComparison.Ordinal)
                    || trimmed.Contains("prerender: false", StringComparison.Ordinal)
                    || trimmed.Contains("prerender:false", StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file) + " -> " + trimmed.Trim());
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "App.razor renders <HeadOutlet /> statically, so a page that disables "
            + "prerendering ships with NO <title> element and its tab shows a bare "
            + "URL. Either prerender the page, or give the HeadOutlet a matching "
            + "render mode - but note that changes head rendering for every static "
            + "SSR page on the site. Offending page(s):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_meeting_confirm_page_declares_a_title()
    {
        // The page the sweep caught. Kept as a named case beside the general rule,
        // because the general rule would still pass if someone deleted the
        // PageTitle rather than the render mode.
        var source = File.ReadAllText(Path.Combine(
            WebRoot, "Components", "Pages", "MeetingConfirm.razor"));

        Assert.Contains("<PageTitle>", source, StringComparison.Ordinal);
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
