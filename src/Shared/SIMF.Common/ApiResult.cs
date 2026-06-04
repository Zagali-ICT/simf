namespace SIMF.Common;

/// <summary>
/// The standard SIMF API response envelope (SIMF-API-001 section 6). Every API
/// response — success or failure — is returned as an <see cref="ApiResult{T}"/>,
/// so a client parses success and failure the same way every time.
/// </summary>
/// <typeparam name="T">The type of the success payload.</typeparam>
public sealed class ApiResult<T>
{
    /// <summary>True when the request succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The result payload on success; <c>null</c> on failure.</summary>
    public T? Data { get; init; }

    /// <summary>The error detail on failure; <c>null</c> on success.</summary>
    public ApiError? Error { get; init; }

    /// <summary>Optional extra information, for example pagination.</summary>
    public object? Meta { get; init; }

    /// <summary>Builds a success result.</summary>
    public static ApiResult<T> Ok(T data, object? meta = null) =>
        new() { Success = true, Data = data, Meta = meta };

    /// <summary>Builds a failure result.</summary>
    public static ApiResult<T> Fail(ApiError error) =>
        new() { Success = false, Error = error };
}
