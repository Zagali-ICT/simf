// WS4 — where the browser tests point, and whether there is anything there.
//
// The whole suite is SKIPPED, not failed, when no QA stack is reachable. That
// is deliberate: this project sits in SIMF.slnx and runs under the same
// `dotnet test` as every other suite, so if a missing stack meant a red build,
// nobody could run the unit tests on a laptop without first standing up an API,
// a Control Panel, a Website and a database. A skipped browser suite is honest;
// a suite everyone disables in their IDE is not.
using System.Net.Sockets;

namespace SIMF.E2E.Tests;

public static class QaStack
{
    // The QA ports from the programme's stack recipe, overridable so the same
    // suite can be pointed at a CI-provisioned stack without a code change.
    public static string ControlPanel =>
        Environment.GetEnvironmentVariable("SIMF_QA_CP_URL") ?? "http://localhost:5278";

    public static string Website =>
        Environment.GetEnvironmentVariable("SIMF_QA_WEB_URL") ?? "http://localhost:5280";

    public static string Api =>
        Environment.GetEnvironmentVariable("SIMF_QA_API_URL") ?? "http://localhost:5275";

    /// <summary>The admin the CP sweep signs in as. Never a literal password:
    /// the value comes from the environment, and the TOTP code from the
    /// <c>tools/totp</c> helper, exactly as the manual runs do.</summary>
    public static string? AdminEmail =>
        Environment.GetEnvironmentVariable("SIMF_QA_ADMIN_EMAIL");

    public static string? AdminPassword =>
        Environment.GetEnvironmentVariable("SIMF_QA_ADMIN_PASSWORD");

    public static string? AdminTotpSecret =>
        Environment.GetEnvironmentVariable("SIMF_QA_ADMIN_TOTP_SECRET");

    // Probed ONCE per process, not once per test. The CP sweep is a theory over
    // ~100 routes, and a 2-second TCP timeout per case turned "no stack, skip
    // everything" into a 6m45s step — long enough that someone would take the
    // project back out of the solution.
    private static readonly Dictionary<string, string?> Probed = [];
    private static readonly Lock ProbeGate = new();

    /// <summary>Null when the stack is up; otherwise the skip reason. Phrased so
    /// a developer seeing it in the output knows what to start, rather than
    /// assuming the suite is broken.</summary>
    public static string? SkipReasonFor(string baseUrl)
    {
        lock (ProbeGate)
        {
            if (Probed.TryGetValue(baseUrl, out var cached))
            {
                return cached;
            }
            var uri = new Uri(baseUrl);
            var reason = IsListening(uri.Host, uri.Port)
                ? null
                : $"No QA stack on {baseUrl}. Start it (API {Api}, CP "
                    + $"{ControlPanel}, Website {Website}) or set SIMF_QA_*_URL, "
                    + "then re-run. Browser tests skip rather than fail so the "
                    + "rest of the solution still runs without one.";
            Probed[baseUrl] = reason;
            return reason;
        }
    }

    /// <summary>Credentials are a second prerequisite: the CP sweep needs a
    /// signed-in admin, and a half-configured run that silently swept only the
    /// sign-in page would look like a pass.</summary>
    public static string? CredentialSkipReason() =>
        string.IsNullOrWhiteSpace(AdminEmail)
        || string.IsNullOrWhiteSpace(AdminPassword)
        || string.IsNullOrWhiteSpace(AdminTotpSecret)
            ? "Set SIMF_QA_ADMIN_EMAIL / _PASSWORD / _TOTP_SECRET to run the "
              + "signed-in Control Panel sweep."
            : null;

    private static bool IsListening(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2))
                && client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
