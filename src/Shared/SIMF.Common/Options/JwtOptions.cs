namespace SIMF.Common.Options;

/// <summary>
/// JWT access-token settings, bound from the <c>Jwt</c> configuration section.
/// The signing key is supplied through the environment / <c>set-env</c> scripts
/// and is never committed.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SIMF";

    public string Audience { get; set; } = "SIMF";

    /// <summary>The HMAC-SHA256 signing key — at least 32 bytes.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>The access-token lifetime, in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 30;
}
