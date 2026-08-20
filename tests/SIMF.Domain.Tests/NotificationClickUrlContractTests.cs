// Every clickUrl the server can stamp on a notification must be a path the
// Flutter app is willing to open.
//
// Why this exists. NotificationKindCatalog.ClickUrlFor decides where a tile
// navigates, and the app re-checks that value against a hardcoded allow-list
// before pushing it. The re-check is deliberate and correct - the router has no
// error page, so pushing an unknown route would be worse. But the two sets are
// maintained by hand, in two languages, and when they drift the tile does not
// error: _maybeDeepLink simply returns, the tile is marked read, and NOTHING
// happens. No log, no toast, no crash. The feature is silently unreachable.
//
// That has now happened twice. QA A27 caught it on the four meeting-lifecycle
// kinds, whose "/meetings" url was rejected until someone noticed the tiles did
// nothing. The comment recording that fix was still sitting in the app file on
// 2026-08-20, directly above an allow-list missing "/meet" - added to the
// catalogue afterwards for MatchRecommended (FR-803) and never mirrored here.
// The recommendation feature's only notification entry point had been dead
// since it shipped.
//
// Direction matters, and only one direction is a defect:
//   * a server clickUrl the app does not allow -> the tile is inert. Fails here.
//   * an app-allowed path the server never emits -> harmless. The app may allow
//     a path for a kind the backend has not started sending yet, and asserting
//     equality would red the build on a half-landed feature.
//
// Matching is on the PATH only, query string stripped, because that is what the
// app compares (D-678: "Only the path is matched; the query string is
// ignored"). So /rate?code=Event and /rate?code=App both reduce to /rate, which
// is the single entry the app carries.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

public sealed class NotificationClickUrlContractTests
{
    private const string AppAllowListPath =
        "src/Mobile/simf_app/lib/features/notifications/notifications_screen.dart";

    private const string ServerCatalogPath =
        "src/Backend/SIMF.Application/Notifications/NotificationKindCatalog.cs";

    [Fact]
    public void Every_click_url_the_server_can_emit_is_a_path_the_app_will_open()
    {
        var allowed = AppAllowedPaths();
        var emitted = ServerClickUrlPaths();

        Assert.NotEmpty(allowed);
        Assert.NotEmpty(emitted);

        var unreachable = emitted
            .Where(path => !allowed.Contains(path))
            .Select(path => "  " + path)
            .ToList();

        Assert.True(
            unreachable.Count == 0,
            "The server stamps clickUrl values the mobile app refuses to open:\n"
            + string.Join("\n", unreachable)
            + "\n\nThe app checks the path against _allowedClickPaths in "
            + AppAllowListPath
            + " and silently does nothing when it misses - no error, no log, the tile "
            + "just never navigates. Either add the path to that set (with a comment "
            + "naming the kind that needs it), or make ClickUrlFor return null for the "
            + "kind so the tile is honestly informational. Paths the app allows today: "
            + string.Join(", ", allowed.OrderBy(path => path, StringComparer.Ordinal)));
    }

    // Reads the catalogue as TEXT rather than calling it, because this test
    // project references SIMF.Domain only and NotificationKindCatalog lives in
    // SIMF.Application. Widening the test project's dependency graph to reach
    // one static method is a worse trade than a regex, and the sibling
    // AssetKindContractTests already establishes text-matching as the pattern
    // for a cross-boundary pin.
    //
    // Every arm that navigates yields a literal beginning with "/", plain or
    // interpolated, so one pattern covers both. Arms returning null yield
    // nothing and are correctly ignored - a kind with no deep link is not a
    // defect (SessionCancelled is deliberately one).
    private static HashSet<string> ServerClickUrlPaths()
    {
        var source = File.ReadAllText(PathUnder(ServerCatalogPath));

        var start = source.IndexOf("ClickUrlFor", StringComparison.Ordinal);
        Assert.True(
            start >= 0,
            "Could not find ClickUrlFor in " + ServerCatalogPath
            + ". If it was renamed or moved, update this test - do not delete it.");

        // Any quoted literal opening with `/`. An interpolated arm carries its
        // `$` OUTSIDE the quote ($"/rate?..."), so the quote is still the first
        // character matched and one pattern covers both forms.
        return Regex.Matches(source[start..], @"""(?<url>/[^""]*)""")
            .Select(match => PathOf(match.Groups["url"].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    // The quoted entries of the const set literal. Comment lines inside it start
    // with // and carry no quoted path, so they do not match.
    private static HashSet<string> AppAllowedPaths()
    {
        var source = File.ReadAllText(PathUnder(AppAllowListPath));

        var setBody = Regex.Match(
            source,
            @"_allowedClickPaths\s*=\s*<String>\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.True(
            setBody.Success,
            "Could not find the _allowedClickPaths set literal in " + AppAllowListPath
            + ". If it was renamed or moved, update this test - do not delete it.");

        return Regex.Matches(setBody.Groups["body"].Value, "'(?<path>/[^']*)'")
            .Select(match => match.Groups["path"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string PathOf(string clickUrl)
    {
        var queryStart = clickUrl.IndexOf('?');
        return queryStart < 0 ? clickUrl : clickUrl[..queryStart];
    }

    private static string PathUnder(string relative)
    {
        var full = Path.Combine(
            RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), "Expected to find " + relative + " at " + full);
        return full;
    }

    // Anchored on SIMF.slnx, matching the sibling ratchets. .git is a FILE in a
    // git worktree, so a walk-up that tests for the directory lands on the wrong
    // tree and the guard passes vacuously - which has happened here before.
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
