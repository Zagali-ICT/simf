// Covers CpAssistantDirectory — the grounding page catalogue fed to the CP
// assistant. It must list only the pages an operator can open (permission
// filtered), skip "Soon" stubs, localise labels, and stay within the AI input
// value cap so the assistant can only ever cite a real, reachable route.
using Microsoft.Extensions.Localization;
using SIMF.Common;
using SIMF.ControlPanel.Components.Assistant;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpAssistantDirectoryTests
{
    private static readonly IStringLocalizer Localizer = new KeyEchoLocalizer();

    [Fact]
    public void Administrator_wildcard_lists_every_real_page_and_no_stub()
    {
        var text = CpAssistantDirectory.Build(
            new HashSet<string>(StringComparer.Ordinal),
            hasAllPermissions: true, Localizer);

        Assert.Contains("/admin/sessions", text);
        Assert.Contains("/admin/roles", text);
        Assert.Contains("Module.Dashboard", text);   // the ungated dashboard
        // "/m/live-sessions" is an IsStub "Soon" entry — it is not a real page,
        // so it must never be offered as a destination.
        Assert.DoesNotContain("/m/live-sessions", text);
        Assert.DoesNotContain("Module.LiveSessions", text);
    }

    [Fact]
    public void Filters_to_the_pages_the_operator_can_open()
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            PermissionCatalog.Sessions.View,
        };

        var text = CpAssistantDirectory.Build(
            permissions, hasAllPermissions: false, Localizer);

        Assert.Contains("/admin/sessions", text);      // granted
        Assert.Contains("Module.Dashboard", text);     // ungated, always shown
        Assert.DoesNotContain("/admin/roles", text);   // Roles.View not granted
    }

    [Fact]
    public void Localises_group_and_item_labels_through_the_localizer()
    {
        var text = CpAssistantDirectory.Build(
            new HashSet<string>(StringComparer.Ordinal),
            hasAllPermissions: true, Localizer);

        Assert.Contains("## Nav.Programme", text);      // group heading, localised
        Assert.Contains("Module.Sessions", text);       // item label, localised
    }

    [Fact]
    public void Stays_within_the_cap_with_no_partial_route()
    {
        var text = CpAssistantDirectory.Build(
            new HashSet<string>(StringComparer.Ordinal),
            hasAllPermissions: true, Localizer);

        Assert.NotEmpty(text);
        // Assert against the actual guard constant (not a loose literal) so a
        // future catalogue that would cross the cap fails here, not at runtime.
        Assert.True(text.Length <= CpAssistantDirectory.MaxLength,
            $"Directory was {text.Length} chars.");
        AssertNoPartialRoute(text);
    }

    [Fact]
    public void Truncates_on_a_line_boundary_when_the_catalogue_overflows()
    {
        // Pad every label so the real catalogue overflows the cap, forcing the
        // truncation path. The result must stay within MaxLength and never cut a
        // route in half (which the assistant could then cite as a garbled path).
        var text = CpAssistantDirectory.Build(
            new HashSet<string>(StringComparer.Ordinal),
            hasAllPermissions: true, new PaddingLocalizer(200));

        Assert.True(text.Length <= CpAssistantDirectory.MaxLength,
            $"Truncated directory was {text.Length} chars.");
        AssertNoPartialRoute(text);
    }

    // Every item line ("- Label -> /route") must be whole.
    private static void AssertNoPartialRoute(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                Assert.Contains(" -> ", line);
            }
        }
    }

    // Minimal IStringLocalizer that returns each key verbatim, so the assertions
    // match the resource keys rather than coupling to the English strings.
    private sealed class KeyEchoLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }

    // Returns each key padded with filler so the built directory overflows the
    // cap — used to exercise the truncation path.
    private sealed class PaddingLocalizer : IStringLocalizer
    {
        private readonly string _pad;

        public PaddingLocalizer(int padLength) => _pad = new string('x', padLength);

        public LocalizedString this[string name] =>
            new(name, name + _pad, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments) + _pad, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }
}
