using Microsoft.AspNetCore.Components;

namespace SIMF.ControlPanel.Components;

/// <summary>D-727 (owner item 5) — a labelled subject-photo block for the CP
/// admin view / pending-review pages. Extracted so the profile photo is
/// rendered from ONE place across the Others / Visitors view and pending-review
/// pages instead of a per-page copy.</summary>
public partial class AdminProfilePhotoBlock
{
    /// <summary>The block heading (e.g. the localised "Profile photo").</summary>
    [Parameter, EditorRequired] public string Heading { get; set; } = string.Empty;

    /// <summary>The resolved image URL (the admin avatar-fetch route + a cache
    /// buster). The caller gates rendering on the photo's existence.</summary>
    [Parameter, EditorRequired] public string Src { get; set; } = string.Empty;
}
