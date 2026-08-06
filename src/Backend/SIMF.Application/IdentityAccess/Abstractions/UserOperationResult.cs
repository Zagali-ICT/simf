namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// SIMF-owned outcome shape for <see cref="IUserAccountRepository"/>
/// methods that previously returned <c>IdentityResult</c>. Application
/// code never sees Identity types now (H21 — D-082, closes the leftover
/// of Architecture SEV-1.1).
///
/// <para>The implementation in Infrastructure translates
/// <c>IdentityResult</c> → <c>UserOperationResult</c> at the boundary;
/// the <c>Code</c> string on each error stays Identity-compatible
/// (e.g. <c>"PasswordMismatch"</c>, <c>"DuplicateEmail"</c>) so the
/// existing translation table in <c>IdentityErrorTranslator</c> keeps
/// working with the new shape.</para>
/// </summary>
public sealed record UserOperationResult
{
    private static readonly IReadOnlyList<UserOperationError> NoErrors =
        Array.Empty<UserOperationError>();

    /// <summary>True when the operation completed without errors.</summary>
    public bool Succeeded { get; }

    /// <summary>Zero-or-more errors. Empty when <see cref="Succeeded"/> is true.</summary>
    public IReadOnlyList<UserOperationError> Errors { get; }

    private UserOperationResult(bool succeeded, IReadOnlyList<UserOperationError> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    /// <summary>Cached singleton for the common no-errors path.</summary>
    public static UserOperationResult Success { get; } = new(true, NoErrors);

    /// <summary>Builds a failed result from the given errors.</summary>
    public static UserOperationResult Failed(IEnumerable<UserOperationError> errors) =>
        new(false, errors.ToList());

    /// <summary>Builds a failed result from one error.</summary>
    public static UserOperationResult Failed(string code, string description) =>
        new(false, new[] { new UserOperationError(code, description) });
}

/// <summary>One operation error — stable identifier + human-readable description.</summary>
public sealed record UserOperationError(string Code, string Description);

/// <summary>
/// Helpers for <see cref="UserOperationResult"/>. Application
/// callers that don't have a sensible fallback for a failed operation
/// (most write-side paths after R3) call <c>.EnsureSuccessAsync()</c>
/// directly on the repository task — a failure throws and propagates
/// to the error middleware as a 500. Pre-H20 18 call sites discarded
/// the <see cref="UserOperationResult"/> entirely; this extension is the
/// minimum-invasive way to close that gap without rewriting each caller.
/// </summary>
public static class UserOperationResultExtensions
{
    /// <summary>
    /// Awaits the task and throws <see cref="InvalidOperationException"/>
    /// when the operation did not succeed. The exception message carries
    /// the joined error codes + descriptions so an unhandled failure is
    /// at least diagnosable from the log.
    /// </summary>
    public static async Task EnsureSuccessAsync(
        this Task<UserOperationResult> task)
    {
        var result = await task.ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "User-account operation failed: "
                + string.Join("; ",
                    result.Errors.Select(error => $"{error.Code}: {error.Description}")));
        }
    }
}
