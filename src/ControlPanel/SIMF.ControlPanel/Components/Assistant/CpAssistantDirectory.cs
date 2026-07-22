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
    /// <summary>The maximum directory length. The directory is one AI input value
    /// (<see cref="AiInputLimits.MaxInputValueLength"/>), so it is kept a safe
    /// margin below that shared cap — deriving from it so a future cap change
    /// carries through. The current catalogue is well under it; the guard only
    /// bites if the catalogue ever balloons, and it then truncates on a
    /// whole-line boundary so a route is never half-emitted.</summary>
    public const int MaxLength = AiInputLimits.MaxInputValueLength - 200;

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

            var headerWritten = false;
            foreach (var item in visible)
            {
                var line = "- " + localizer[item.LabelKey].Value + " -> " + item.Href;
                if (!headerWritten)
                {
                    // Start the group only if the header AND its first item both
                    // fit, so we never leave a dangling group heading. (+1 each for
                    // the newline AppendLine adds.)
                    var header = "## " + localizer[group.LabelKey].Value;
                    if (builder.Length + header.Length + 1 + line.Length + 1 > MaxLength)
                    {
                        return builder.ToString().TrimEnd();
                    }
                    builder.AppendLine(header);
                    headerWritten = true;
                }
                else if (builder.Length + line.Length + 1 > MaxLength)
                {
                    // Stop on a whole-line boundary — a route is never half-emitted.
                    return builder.ToString().TrimEnd();
                }
                builder.AppendLine(line);
            }
        }

        return builder.ToString().TrimEnd();
    }
}
