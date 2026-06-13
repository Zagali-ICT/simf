namespace SIMF.Contracts.Authentication;

/// <summary>The result of <c>POST /api/v1/app/auth/sign-out</c>.</summary>
public sealed record SignOutResponse(bool SignedOut);
