using System.Security.Claims;
using SIMF.Common;

namespace SIMF.Api.RequestContext;

/// <summary>
/// Reads the signed-in caller's id from the bearer token's <c>sub</c> claim.
/// Every endpoint and <see cref="HttpRequestContext"/> go through here, so the
/// claim name and the refusal shape are defined once for the request surface.
///
/// <para>The bearer pipeline itself still reads the raw claim before a principal
/// exists — <c>JwtBearerSetup</c> during token validation, and the rate-limiter
/// partitioners in <c>Program.cs</c>, which run ahead of authentication. Those
/// sit outside the request surface and are deliberately not routed through
/// here.</para>
/// </summary>
public static class ActorClaims
{
    private const string SubjectClaim = "sub";

    /// <summary>The caller's id, or null when the request is anonymous.</summary>
    public static Guid? ActorIdOrNull(this ClaimsPrincipal? user) =>
        Guid.TryParse(user?.FindFirstValue(SubjectClaim), out var id) ? id : null;

    /// <summary>
    /// The caller's id, refusing an anonymous request with 401.
    ///
    /// <para>The refusal is an <see cref="ApiException"/> rather than a bare
    /// status so the error middleware writes the standard failure envelope.
    /// A body-less 401 cannot be decoded by <c>ApiEnvelope</c>, which reports
    /// it to the operator as a transport failure the API never had.</para>
    /// </summary>
    public static Guid ActorId(this ClaimsPrincipal? user) =>
        user.ActorIdOrNull() ?? throw NotAuthenticated();

    /// <summary>
    /// Refuses an anonymous request without yielding the id, for the endpoints
    /// that gate on being signed in but do not care who the caller is.
    /// </summary>
    public static void RequireActor(this ClaimsPrincipal? user) => _ = user.ActorId();

    private static ApiException NotAuthenticated() =>
        new(
            ErrorCodes.AuthNotAuthenticated, 401,
            "You must be signed in to perform this action.",
            "يجب تسجيل الدخول لتنفيذ هذا الإجراء.");
}
