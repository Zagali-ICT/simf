namespace SIMF.ControlPanel;

/// <summary>Builds the Control-Panel BFF URLs for the unified media-asset serve
/// endpoint, so every admin grid / form points at the same route format instead
/// of hand-building the string. The category matches an <c>AssetCategory</c>
/// name (e.g. "SpeakerPhoto", "SponsorLogo", "MediaPartnerLogo"); the endpoint
/// resolves the active asset for that (category, owner) or 404s when absent.</summary>
public static class CpAssetUrls
{
    /// <summary>The gated admin image-serve URL for (category, owner). Callers
    /// render this only when they know an asset exists (a `Has…` flag) so the
    /// grid never issues a request that 404s.</summary>
    public static string AdminImage(string category, Guid ownerId) =>
        $"/account/api/admin/assets/{category}/{ownerId}/image";
}
