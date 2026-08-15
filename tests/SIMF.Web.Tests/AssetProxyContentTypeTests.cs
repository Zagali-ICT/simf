// Tests: the asset-proxy same-origin content-injection fix.
//
// `/content/assets/{category}/{ownerId}/image` and `/content/media/{id}/image`
// re-stream bytes fetched from the API. An asset may be an EXTERNAL LINK, which
// the API answers with a 302 to a URL a Control Panel editor supplied. The
// Website's HttpClient followed that redirect, and the proxy then re-served the
// third party's bytes AND their Content-Type inline from web.simrsnf.com — a
// same-origin script-execution primitive on the public forum domain, reachable
// by any editor holding a per-category asset-write permission (Speakers.Edit is
// baselined to ScientificCommittee, News.Edit to PublicRelations) rather than by
// an administrator. Nothing enforced stopped it: the Website enforces only
// `frame-ancestors 'none'` and ships the real CSP Report-Only.
//
// The fix has two halves and BOTH are load-bearing:
//   1. the proxies serve only an allow-listed image type inline, and
//   2. the client no longer follows the redirect at all.
// Half 2 is what makes half 1 safe to include image/svg+xml, which is itself
// script-capable: SVG can only arrive from the upload pipeline now, never from
// an editor-chosen host.
//
// These are source ratchets rather than endpoint tests because SIMF.Web exposes
// no test harness and its internals are not visible to this assembly; the
// behavioural half is covered by SimfPublicClientTests. They fail on the pre-fix
// tree and hold afterwards, so a future edit that reinstates either half of the
// hole breaks the build.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class AssetProxyContentTypeTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string EndpointsPath =
        "src/Website/SIMF.Web/Endpoints/SiteContentEndpoints.cs";

    private const string ProgramPath = "src/Website/SIMF.Web/Program.cs";

    [Fact]
    public void The_image_proxies_never_echo_the_upstream_content_type_directly()
    {
        var source = Read(EndpointsPath);

        // The pre-fix shape at a CALL SITE — the fetched result handed straight
        // to Results.File with no allow-list. Deliberately matched on the
        // `image.` accessors so it does not catch ImageResult's own body, which
        // is the sanctioned place that call lives after the check has run.
        var echoes = Regex.Matches(
            source, @"Results\.File\(\s*image\.Bytes\s*,\s*image\.ContentType\s*\)");

        Assert.True(
            echoes.Count == 0,
            $"{echoes.Count} image proxy call(s) in {EndpointsPath} re-serve the upstream "
            + "Content-Type verbatim. Route them through ImageResult(), which serves only an "
            + "allow-listed image type inline and downgrades anything else to an attachment.");
    }

    [Fact]
    public void Both_image_proxies_route_through_the_allow_list()
    {
        var source = Read(EndpointsPath);

        // One per proxy: /content/media/{id}/image and
        // /content/assets/{category}/{ownerId}/image. If a third image proxy is
        // added later this fails, which is the intent — it has to opt in.
        var routed = Regex.Matches(
            source, @"ImageResult\(\s*image\.Bytes\s*,\s*image\.ContentType\s*\)");

        Assert.Equal(2, routed.Count);
    }

    [Fact]
    public void Both_image_proxies_hand_a_redirect_to_the_browser()
    {
        var source = Read(EndpointsPath);

        // The counterpart to AllowAutoRedirect=false: with the redirect no longer
        // followed, each proxy must pass it on or an external-link asset would
        // simply break.
        var forwarded = Regex.Matches(source, @"RedirectLocation is \{ \} \w+");

        Assert.Equal(2, forwarded.Count);
    }

    [Fact]
    public void The_inline_allow_list_admits_only_image_types()
    {
        var source = Read(EndpointsPath);

        var block = Regex.Match(
            source, @"InlineImageTypes\s*=\s*\[(?<types>[^\]]*)\]", RegexOptions.Singleline);
        Assert.True(block.Success, $"InlineImageTypes is not declared in {EndpointsPath}.");

        var types = Regex.Matches(block.Groups["types"].Value, "\"(?<t>[^\"]+)\"")
            .Select(m => m.Groups["t"].Value)
            .ToArray();

        Assert.NotEmpty(types);

        var nonImage = types.Where(t => !t.StartsWith("image/", StringComparison.Ordinal)).ToArray();
        Assert.True(
            nonImage.Length == 0,
            "Only image types may be served inline by the proxies. Found: "
            + string.Join(", ", nonImage));

        // The type that made this a vulnerability, named explicitly so no future
        // edit can reintroduce it without deleting this assertion on purpose.
        Assert.DoesNotContain("text/html", types, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Anything_outside_the_allow_list_is_served_as_an_attachment()
    {
        var source = Read(EndpointsPath);

        Assert.Contains("fileDownloadName", source);
        Assert.Contains("application/octet-stream", source);
    }

    [Fact]
    public void The_public_client_does_not_follow_redirects()
    {
        var source = Read(ProgramPath);

        // Written without whitespace assumptions: the point is the setting, not
        // its formatting.
        var configured = Regex.IsMatch(
            source, @"AllowAutoRedirect\s*=\s*false", RegexOptions.IgnoreCase);

        Assert.True(
            configured,
            $"{ProgramPath} must register SimfPublicClient's primary handler with "
            + "AllowAutoRedirect = false. Following the API's external-link 302 server-side "
            + "re-hosts an editor-chosen third party's bytes on this origin, and resolves a "
            + "hostname the Website tier should never resolve.");
    }

    private static string Read(string relativePath)
    {
        var path = Path.Combine(
            RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"Expected source file is missing: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
    }
}
