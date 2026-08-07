// Tests: the D-836 rule, for every future endpoint rather than the four it fixed.
//
// D-834 established that an action must be gated by the TIER IT ACTS ON. D-836
// found the other half of that: a correct permission on the route still lets a
// caller hand the handler an id from the other tier. The ID-document endpoints
// leaked national-ID images across the visitors/others desks for exactly that
// reason, and the only thing that found it was a hand sweep of 50 routes.
//
// This encodes the sweep. Every endpoint file that serves a per-{id} route under
// /admin/visitors/ or /admin/others/ must either carry a scope guard itself or
// be listed below with the service method that scopes on its behalf - so the
// next person to add one has to answer the question rather than inherit silence.
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SubjectScopeRatchetTests
{
    /// <summary>The markers that count as scoping IN THE ENDPOINT FILE. Each is a
    /// real mechanism in use, not a name to grep for its own sake.</summary>
    private static readonly string[] InlineGuards =
    [
        "IsSubjectInFamilyAsync",   // the endpoint calls the family guard directly
        "ExpectedIsVisitor",        // ...or declares it for a base that does
    ];

    /// <summary>Endpoint files whose scoping happens in the SERVICE they call,
    /// with where. Verified by reading each one during the D-836 sweep; an entry
    /// is a claim that a specific method narrows the subject to this route's tier,
    /// so it must name that method.</summary>
    private static readonly Dictionary<string, string> ScopedInService =
        new(StringComparer.Ordinal)
        {
            ["ApproveStaffEndpoint.cs"] =
                "ApproveOtherAsync -> LoadPendingSubjectAsync(ApprovalScope.PartnerOther), "
                + "which rejects a non-partner subject (AdminAccountService.Approval.cs).",
            ["RejectStaffEndpoint.cs"] =
                "RejectOtherAsync -> the same LoadPendingSubjectAsync scope check.",
            ["ApproveVisitorEndpoint.cs"] =
                "ApproveVisitorAsync -> LoadPendingSubjectAsync(ApprovalScope.AudienceVisitor).",
            ["RejectVisitorEndpoint.cs"] =
                "RejectVisitorAsync -> the same audience-side scope check.",
            ["GetVisitorProfileEndpoint.cs"] =
                "GetVisitorProfileAsync -> GetFullProfileAsync(expectAudienceScope: true).",
            ["GetOtherProfileEndpoint.cs"] =
                "GetOtherProfileAsync -> GetFullProfileAsync(expectAudienceScope: false).",
            ["GetPendingVisitorProfileEndpoint.cs"] =
                "GetPendingVisitorProfileAsync -> GetAsync(expectAudienceScope: true).",
            ["GetPendingOtherProfileEndpoint.cs"] =
                "GetPendingOtherProfileAsync -> GetAsync(expectAudienceScope: false).",
            ["UpdateUserEndpoints.cs"] =
                "UpdateVisitorAsync / UpdateOtherAsync pass expectedIsVisitor true/false "
                + "into the shared update path (AdminAccountService.Update.cs).",
        };

    [Fact]
    public void Every_per_subject_tier_route_scopes_the_subject_to_its_own_tier()
    {
        var offenders = new List<string>();

        foreach (var file in EndpointFilesServingAPerSubjectTierRoute())
        {
            var name = Path.GetFileName(file);
            var source = File.ReadAllText(file);

            if (InlineGuards.Any(marker => source.Contains(marker, StringComparison.Ordinal)))
            {
                continue;
            }
            if (ScopedInService.ContainsKey(name)) { continue; }

            offenders.Add(name);
        }

        Assert.True(
            offenders.Count == 0,
            "These endpoint files serve a per-{id} route under /admin/visitors/ or "
            + "/admin/others/ and nothing in them narrows the SUBJECT to that "
            + "route's tier. A permission gate gets the caller through the door; it "
            + "does not stop them passing an id from the other desk, and UserType "
            + "alone is not a tier check (D-186 made every non-admin account "
            + "UserType.Visitor). Call IsSubjectInFamilyAsync and answer 404, or add "
            + "the file to ScopedInService naming the method that scopes for it:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_scoped_in_service_list_has_no_stale_entries()
    {
        // An entry for a file that no longer serves such a route, or that has since
        // grown its own inline guard, is a false record of a review that happened.
        var live = EndpointFilesServingAPerSubjectTierRoute()
            .ToDictionary(file => Path.GetFileName(file)!, File.ReadAllText, StringComparer.Ordinal);

        var stale = new List<string>();
        foreach (var (name, reason) in ScopedInService)
        {
            if (!live.TryGetValue(name, out var source))
            {
                stale.Add($"{name} — no longer serves a per-{{id}} visitors/others route");
                continue;
            }
            if (InlineGuards.Any(marker => source.Contains(marker, StringComparison.Ordinal)))
            {
                stale.Add($"{name} — now carries its own guard, so the entry is redundant "
                    + $"(it claimed: {reason})");
            }
        }

        Assert.True(stale.Count == 0,
            "Entries no longer describe the file:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Every endpoint source file declaring a route of the shape
    /// <c>/admin/{visitors|others}/{id...}/…</c> — the routes that take a SUBJECT
    /// id and can therefore be handed one from the other tier. Collection routes
    /// (list, bulk-*, export, import, register-onsite) are out of scope: they take
    /// no single subject from the URL and are scoped by their own queries.</summary>
    private static IEnumerable<string> EndpointFilesServingAPerSubjectTierRoute()
    {
        var endpointsDir = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Api", "Endpoints");
        var files = Directory.EnumerateFiles(endpointsDir, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file) is var text
                && (text.Contains("\"/admin/visitors/{id", StringComparison.Ordinal)
                    || text.Contains("\"/admin/others/{id", StringComparison.Ordinal)))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();

        // A sweep that matches nothing passes silently. The surface was 12 files at
        // D-836; if a rename drops it to zero this fails instead of going green.
        Assert.True(files.Count >= 10,
            $"Expected the per-subject tier-route surface but matched {files.Count} files "
            + "— the enumeration is probably broken, not the endpoints.");
        return files;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
