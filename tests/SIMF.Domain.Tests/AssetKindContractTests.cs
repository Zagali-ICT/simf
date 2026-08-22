// The Flutter client's AssetKind must stay a subset of the server's AssetCategory.
//
// Why this exists. The asset route resolves its category with
// `Enum.TryParse` + `Enum.IsDefined` and answers a miss with **404** — the same
// status it returns when the category is fine but nothing has been uploaded. So a
// client asking for a category the server does not have is indistinguishable, from
// the client's side, from an entity that simply has no logo. It shows the initials
// fallback and nothing anywhere reports a fault.
//
// That is not hypothetical. D-929 removed `AssetCategory.CompanyLogo` server-side —
// its own comment reads "Its Contact owner table was removed, so the category could
// never resolve; the integer stays empty so a persisted value never changes
// meaning" — and the Flutter enum kept `companyLogo('CompanyLogo')` with TWO live
// call sites still requesting it (the exhibitor-detail logo fallback and the
// partner-directory booth row). Both had been silently 404ing ever since, and no
// compiler, analyzer, golden or unit test could see it, because both sides were
// internally consistent and only the pair was wrong.
//
// Direction matters, and only one direction is a defect:
//   * a client kind the server lacks  -> can ONLY ever 404. Fails this test.
//   * a server category the client lacks -> fine. The server may offer categories
//     no app screen consumes yet (ArchiveCover, ArchiveGalleryImage,
//     ArchivePastSpeakerPhoto and OrganizationLogo are in exactly that position),
//     and asserting equality would force churn on the app every time the backend
//     grew one.
//
// Matching is on the WIRE NAME the client sends — the string inside
// `AssetKind.foo('Bar')` — not on the Dart identifier, because that string is what
// reaches TryParse.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

public sealed class AssetKindContractTests
{
    private const string ClientEnumPath =
        "src/Mobile/simf_app/lib/core/net/asset_urls.dart";

    private const string ServerEnumPath =
        "src/Shared/SIMF.Common/Enums/AssetCategory.cs";

    [Fact]
    public void Every_client_AssetKind_names_a_real_server_AssetCategory()
    {
        var clientKinds = ClientWireNames();
        var serverCategories = ServerCategoryNames();

        Assert.NotEmpty(clientKinds);
        Assert.NotEmpty(serverCategories);

        var unresolvable = clientKinds
            .Where(kind => !serverCategories.Contains(kind.WireName))
            .Select(kind => $"  AssetKind.{kind.Identifier} sends '{kind.WireName}'")
            .ToList();

        Assert.True(
            unresolvable.Count == 0,
            "The Flutter client can request asset categories the server does not "
            + "define:\n"
            + string.Join("\n", unresolvable)
            + "\n\nThe asset endpoint answers an unknown category with 404, which the "
            + "app cannot tell apart from 'no asset uploaded' — so these fail silently "
            + "and show the initials fallback forever. Either the category was removed "
            + "server-side and the client kind plus its call sites must go, or the name "
            + "is misspelled. Server categories currently defined: "
            + string.Join(", ", serverCategories.OrderBy(name => name, StringComparer.Ordinal)));
    }

    // `speakerPhoto('SpeakerPhoto'),` — the identifier for the message, the quoted
    // string for the comparison, since the quoted string is what goes on the wire.
    private static List<(string Identifier, string WireName)> ClientWireNames()
    {
        var source = File.ReadAllText(PathUnder(ClientEnumPath));
        return Regex.Matches(source, @"^\s+([a-z][A-Za-z0-9]*)\('([^']+)'\)", RegexOptions.Multiline)
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value))
            .ToList();
    }

    // `SpeakerPhoto = 0,` — a commented-out or reserved slot has no identifier and
    // is correctly skipped, which is what makes the removed CompanyLogo = 1 slot
    // read as absent rather than present.
    private static HashSet<string> ServerCategoryNames()
    {
        var source = File.ReadAllText(PathUnder(ServerEnumPath));
        return Regex.Matches(source, @"^\s+([A-Z][A-Za-z0-9]*)\s*=\s*\d+\s*,", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string PathUnder(string relative)
    {
        var full = Path.Combine(
            RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"Expected to find {relative} at {full}");
        return full;
    }

    // Anchored on SIMF.slnx, matching the sibling ratchets. `.git` is a FILE in a
    // git worktree, so a walk-up that tests for the directory lands on the wrong
    // tree and the guard passes vacuously — which has happened here before.
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
