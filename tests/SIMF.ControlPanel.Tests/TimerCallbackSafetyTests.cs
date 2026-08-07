// A static guard against ONE specific way to take the Control Panel down.
//
// System.Timers.Timer.Elapsed is an `ElapsedEventHandler` — it returns void. An
// `async` lambda attached to it is therefore `async void`: nothing awaits it, and
// any exception that escapes is raised on a thread-pool thread with no handler
// above it. In .NET that terminates the PROCESS. Not the circuit, not the request
// — the whole Control Panel, for every signed-in admin.
//
// LogsViewer shipped exactly that. Its 5-second log-tail poll called JS interop
// inside an unguarded `async (_, _) =>`; navigating away from /admin/logs while a
// poll was in flight cancelled the interop call, TaskCanceledException escaped,
// and the server died. It was found only because the WS4 browser sweep lost the
// Control Panel mid-run and the crash landed in the console log.
//
// The check is deliberately structural rather than behavioural. Reproducing the
// crash in a test means crashing the test host, and a bUnit render cannot prove
// the absence of an escape path on a timer thread. What CAN be checked cheaply
// and reliably is the shape: an async Elapsed handler must open a try before it
// awaits anything. PeriodicTimer loops (ServicesMonitor, SessionLiveHall) are not
// covered here because they await inside a Task, where an escape is an unobserved
// task exception rather than a process kill — a different, survivable bug.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class TimerCallbackSafetyTests
{
    /// <summary>`X.Elapsed += async` — the dangerous shape, wherever it appears.</summary>
    private static readonly Regex AsyncElapsedHandler = new(
        @"\.Elapsed\s*\+=\s*async\b", RegexOptions.Compiled);

    [Fact]
    public void Every_async_timer_Elapsed_handler_guards_its_body_with_try()
    {
        var offenders = new List<string>();

        foreach (var file in EnumerateControlPanelSources())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in AsyncElapsedHandler.Matches(text))
            {
                if (!OpensTryBeforeFirstAwait(text, match.Index))
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add(
                        $"{Path.GetFileName(file)}:{line} — async Elapsed handler "
                        + "awaits before entering a try block.");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "An async System.Timers.Timer.Elapsed handler is `async void`: an "
            + "exception escaping it is unhandled on a thread-pool thread and KILLS "
            + "THE PROCESS — the whole Control Panel, not just one circuit. Wrap the "
            + "body in try/catch (see LogsViewer.ResetTimer).\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>True when a <c>try</c> appears between the handler's <c>=&gt;</c>
    /// and its first <c>await</c>. Crude on purpose: it only has to distinguish
    /// "guarded" from "not guarded", and being crude keeps it from silently
    /// accepting a shape it does not really understand.</summary>
    private static bool OpensTryBeforeFirstAwait(string text, int handlerIndex)
    {
        var arrow = text.IndexOf("=>", handlerIndex, StringComparison.Ordinal);
        if (arrow < 0) { return false; }

        var firstAwait = text.IndexOf("await", arrow, StringComparison.Ordinal);
        if (firstAwait < 0) { return true; } // nothing awaited: nothing to escape

        var firstTry = text.IndexOf("try", arrow, StringComparison.Ordinal);
        return firstTry >= 0 && firstTry < firstAwait;
    }

    private static IEnumerable<string> EnumerateControlPanelSources()
    {
        var root = Path.Combine(RepoRoot(), "src", "ControlPanel");
        Assert.True(Directory.Exists(root), $"Control Panel source not found at {root}");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static string RepoRoot()
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
