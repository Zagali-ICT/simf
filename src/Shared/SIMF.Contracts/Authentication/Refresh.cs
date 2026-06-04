namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/auth/refresh</c> (SIMF-API-001 section 12.4).</summary>
public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
