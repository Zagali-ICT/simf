namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/app/auth/sign-up</c> (SIMF-API-001 section 12.4).</summary>
public sealed class SignUpRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The data returned by a successful sign-up.</summary>
public sealed record SignUpResponse(string Email, int CodeExpiresInSeconds);
