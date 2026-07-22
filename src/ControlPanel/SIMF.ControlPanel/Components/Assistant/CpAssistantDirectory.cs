// Tests: SIMF.ControlPanel.Tests/CpAssistantDirectoryTests.cs
using System.Text;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Ai;

namespace SIMF.ControlPanel.Components.Assistant;

/// <summary>Builds the grounding directory the Control Panel assistant is fed:
/// the CP pages the signed-in operator may open, grouped and localized, one line
/// per page with its exact route. Because the list is filtered to the caller's
/// permissions, the assistant can only ever cite a page the operator is allowed
/// to reach. Kept compact (name -> route only) so it fits the AI input value cap
/// (<c>AiService.MaxInputValueLength</c> = 4000).</summary>
public static class CpAssistantDirectory
{
    /// <summary>The maximum directory length, shared with the app assistant's
    /// grounding via <see cref="AiGroundingText.MaxLength"/>: one AI input value
    /// (<see cref="AiInputLimits.MaxInputValueLength"/>) kept a safe margin below
    /// that cap. Exposed so the tests assert against the real guard constant.</summary>
    public const int MaxLength = AiGroundingText.MaxLength;

    /// <summary>Renders the directory for the given permission snapshot.</summary>
    /// <param name="permissions">The operator's permission codes (from the JWT
    /// claims), used to hide pages they cannot open.</param>
    /// <param name="hasAllPermissions">True for the Administrator wildcard "*" —
    /// every page is listed.</param>
    /// <param name="localizer">The CP string localizer (culture-aware) so labels
    /// match the operator's interface language.</param>
    public static string Build(
        IReadOnlySet<string> permissions, bool hasAllPermissions,
        IStringLocalizer localizer)
    {
        var builder = new StringBuilder();
        foreach (var group in CpNavigation.Groups)
        {
            // A real page (not a "Soon" stub) the operator is permitted to open —
            // reusing the same visibility rule the side menu uses.
            var visible = group.Items
                .Where(item => !item.IsStub
                    && item.IsPermittedFor(permissions, hasAllPermissions))
                .ToList();
            if (visible.Count == 0)
            {
                continue;
            }

            // One line per permitted page ("- Label -> /route"), capped + truncated
            // on a whole-line boundary by the shared grounding primitive.
            AiGroundingText.AppendCappedSection(
                builder,
                "## " + localizer[group.LabelKey].Value,
                visible.Select(item =>
                    "- " + localizer[item.LabelKey].Value + " -> " + item.Href));
        }

        return builder.ToString().TrimEnd();
    }
}
