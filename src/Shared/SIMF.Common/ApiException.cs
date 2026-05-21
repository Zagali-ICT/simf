namespace SIMF.Common;

/// <summary>
/// An exception that carries a SIMF API error code and an HTTP status. The
/// error-handling middleware turns it directly into an <see cref="ApiResult{T}"/>
/// failure response. Use it for expected business failures — a duplicate email,
/// a missing account, an invalid code.
/// </summary>
public class ApiException : Exception
{
    public ApiException(
        string code,
        int statusCode,
        string message,
        IReadOnlyList<ApiErrorDetail>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details ?? [];
    }

    /// <summary>The machine-readable error code (see <see cref="ErrorCodes"/>).</summary>
    public string Code { get; }

    /// <summary>The HTTP status code to return.</summary>
    public int StatusCode { get; }

    /// <summary>Field-level error details; empty when there are none.</summary>
    public IReadOnlyList<ApiErrorDetail> Details { get; }
}
