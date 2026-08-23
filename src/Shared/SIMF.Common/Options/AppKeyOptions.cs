namespace SIMF.Common.Options;

/// <summary>
/// Settings for the <c>X-App-Key</c> gate on the mobile surface
/// (<c>/api/v1/app/*</c>), bound from the <c>AppKey</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is abuse control, not authentication.</b> The value ships inside the
/// Flutter binary and inside the website's server configuration, and anyone can
/// read it out of a published APK in about a minute. It raises the cost of
/// casual scripted traffic against the mobile surface and lets that traffic be
/// told apart from the website's; it protects nothing on its own. Every endpoint
/// behind it keeps its own authentication and permission gate, and none of them
/// may be relaxed because this exists.
/// </para>
/// <para>
/// <b>It fails OPEN, deliberately.</b> With no <c>Keys</c> configured the gate is
/// inert and every request passes, which is exactly the behaviour the API had
/// before it existed. That is what makes it safe to deploy ahead of the clients:
/// turning it on is a separate, ordered decision, and the order matters. Ship a
/// mobile build carrying the key and deploy the website with it FIRST, then
/// populate <c>Keys</c>. Populating it first locks out every installed app.
/// </para>
/// <para>
/// <c>Keys</c> is a list because more than one caller sends the header - the
/// Flutter app and the public website - and because rotating a key needs both
/// the old and the new value accepted while builds roll out.
/// </para>
/// </remarks>
public sealed class AppKeyOptions
{
    public const string SectionName = "AppKey";

    /// <summary>
    /// The accepted <c>X-App-Key</c> values. EMPTY BY DEFAULT, which disables
    /// the gate entirely.
    /// </summary>
    public IReadOnlyList<string> Keys { get; set; } = [];

    /// <summary>
    /// Whether the gate does anything. False whenever no key is configured, so
    /// the default configuration cannot reject a request.
    /// </summary>
    public bool IsEnabled => Keys.Count > 0;

    /// <summary>
    /// Whether <paramref name="candidate"/> is one of the accepted keys.
    /// Ordinal, because these are opaque ASCII tokens rather than text: a
    /// culture-aware comparison could treat two different byte sequences as
    /// equal.
    /// </summary>
    public bool Accepts(string? candidate) =>
        !string.IsNullOrEmpty(candidate)
        && Keys.Any(key => string.Equals(key, candidate, StringComparison.Ordinal));
}
