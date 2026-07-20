using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "Key themes" page (/about/themes — Figma
// 5865-35289). The third About-cluster page: an interactive theme explorer.
// The five themes themselves (title + description) are reused from the landing
// (Landing.Themes) so the theme wording has a single source of truth; this page
// only adds the short ordinal tab labels ("Theme 1" … "Theme 5") that the narrow
// vertical tab list shows.
public partial class Themes
{
    // Short tab labels for the five-theme explorer (the panel shows each theme's
    // full title + description from Landing.Themes). Index-aligned with Landing.Themes.
    public static readonly IReadOnlyList<Bilingual> TabLabels =
    [
        new("المحور الأول", "Theme 1"),
        new("المحور الثاني", "Theme 2"),
        new("المحور الثالث", "Theme 3"),
        new("المحور الرابع", "Theme 4"),
        new("المحور الخامس", "Theme 5"),
    ];
}
