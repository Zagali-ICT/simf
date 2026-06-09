namespace SIMF.Common.Options;

/// <summary>
/// D-355 — controls exposure of the OpenAPI (Swagger) UI, bound from the
/// <c>Swagger</c> configuration section. Outside production the UI is always
/// served; in production it is off unless <see cref="AllowSwagger"/> is set,
/// and then only behind the Basic-auth gate (<see cref="Username"/> /
/// <see cref="Password"/>) — the API is public via the reverse proxy
/// (SAD-001 §10.1), so the App + CP contract must not be anonymously
/// enumerable. The credentials are supplied through the environment /
/// <c>set-env</c> scripts and are never committed.
/// </summary>
public sealed class SwaggerOptions
{
    public const string SectionName = "Swagger";

    /// <summary>
    /// When <c>true</c>, the OpenAPI UI is also served in Production (default
    /// <c>false</c>). Production exposure additionally requires
    /// <see cref="Username"/> and <see cref="Password"/>; the API refuses to
    /// start if this is enabled without them.
    /// </summary>
    public bool AllowSwagger { get; set; }

    /// <summary>Basic-auth username gating the production OpenAPI UI.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Basic-auth password gating the production OpenAPI UI.</summary>
    public string Password { get; set; } = string.Empty;
}
