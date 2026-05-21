namespace SIMF.Common;

/// <summary>
/// The error detail carried by a failed <see cref="ApiResult{T}"/>
/// (SIMF-API-001 section 7).
/// </summary>
public sealed class ApiError
{
    /// <summary>
    /// A stable, machine-readable code from <see cref="ErrorCodes"/>. Clients
    /// branch on this, not on the message.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// A human-readable message in the request's language. Safe to show a user.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>Field-level errors; empty when there are none.</summary>
    public IReadOnlyList<ApiErrorDetail> Details { get; init; } = [];
}

/// <summary>One field-level error entry (SIMF-API-001 section 7.1).</summary>
public sealed class ApiErrorDetail
{
    /// <summary>The request body field the error applies to.</summary>
    public required string Field { get; init; }

    /// <summary>The reason the field is invalid.</summary>
    public required string Message { get; init; }
}
