namespace SIMF.Infrastructure.Configuration;

/// <summary>Options for the CP-uploaded home/landing hero background video
/// that SIMF serves from its own API (so the Flutter home hero can play it on
/// Android, where a clipped YouTube WebView cannot render into the hero
/// band).</summary>
public sealed class OrganizationHeroVideoOptions
{
    public const string SectionName = "OrganizationHeroVideo";

    /// <summary>The public origin + route prefix of THIS API as reached from the
    /// public internet (e.g. <c>https://api.example.com/api/v1</c>), used to compose
    /// the absolute <c>.mp4</c> URL persisted into
    /// <c>OrganizationProfile.BackgroundVideoUrl</c>. It must be absolute + https so
    /// the app/website hero accept-gate (<c>LiveStreamUrlPolicy</c> — an absolute
    /// https URL whose path ends <c>.mp4</c>) passes. When blank, the upload endpoint
    /// falls back to the incoming request scheme/host — correct for a direct dev
    /// call, but a reverse-proxied deployment SHOULD set this, since the proxy does
    /// not forward the original Host header (only X-Forwarded-For/Proto).</summary>
    public string PublicApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Max accepted upload size, in bytes (default 200 MiB). A looping,
    /// muted hero clip should be short + web-optimised; this bounds the anonymously
    /// served payload. Raised on the upload endpoint's single request only, so the
    /// global DoS posture on every other endpoint is unchanged.</summary>
    public long MaxUploadBytes { get; set; } = 209_715_200;
}
