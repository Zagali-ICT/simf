using System.Security.Claims;
using SIMF.Application.Abstractions;

namespace SIMF.Api.RequestContext;

/// <summary>
/// Supplies <see cref="IRequestContext"/> from the current ASP.NET Core
/// <c>HttpContext</c>.
/// </summary>
internal sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? SourceIp =>
        accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public string? CorrelationId =>
        accessor.HttpContext?.TraceIdentifier;

    public Guid? ActorUserId => accessor.HttpContext?.User.ActorIdOrNull();

    public string? ActorDisplayName =>
        accessor.HttpContext?.User.FindFirstValue("display_name");
}
