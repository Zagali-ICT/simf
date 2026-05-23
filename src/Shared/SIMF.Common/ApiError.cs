namespace SIMF.Common;

/// <summary>
/// The error detail carried by a failed <see cref="ApiResult{T}"/>
/// (SIMF-API-001 section 7).
/// </summary>
/// <remarks>
/// Every SIMF error carries the message in both English and Arabic — the
/// client picks the one its current culture asks for. Decision D-030
/// (2026-05-23): the API returns both languages on every error, mandated
/// by the customer (myComment item #14), reversing the earlier
/// one-language-per-Accept-Language stance of SIMF-API-001 section 7.
/// </remarks>
public sealed class ApiError
{
    /// <summary>
    /// A stable, machine-readable code from <see cref="ErrorCodes"/>. Clients
    /// branch on this, not on the message.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The human-readable English message. Safe to show a user.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The human-readable Arabic message. Safe to show a user.
    /// </summary>
    public required string MessageArabic { get; init; }

    /// <summary>Field-level errors; empty when there are none.</summary>
    public IReadOnlyList<ApiErrorDetail> Details { get; init; } = [];
}

/// <summary>One field-level error entry (SIMF-API-001 section 7.1).</summary>
public sealed class ApiErrorDetail
{
    /// <summary>The request body field the error applies to.</summary>
    public required string Field { get; init; }

    /// <summary>The English reason the field is invalid.</summary>
    public required string Message { get; init; }

    /// <summary>The Arabic reason the field is invalid.</summary>
    public required string MessageArabic { get; init; }
}
