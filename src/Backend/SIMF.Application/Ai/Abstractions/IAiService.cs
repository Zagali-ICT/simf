using SIMF.Contracts.Ai;

namespace SIMF.Application.Ai.Abstractions;

/// <summary>D-176 (gap doc G12) — the one entry point every AI
/// feature uses. Looks up the prompt by key, substitutes inputs,
/// routes to the configured provider, persists an
/// <c>AiInvocation</c> row, and returns the response. On error the
/// invocation row is still written with an <see cref="AiCallerContext"/>
/// + non-null <c>ErrorCode</c>.</summary>
public interface IAiService
{
    Task<AiCallResult> InvokeAsync(
        string promptKey,
        IReadOnlyDictionary<string, string> inputs,
        AiCallerContext caller,
        CancellationToken cancellationToken = default);
}

/// <summary>D-176 — who called. <see cref="CallerKind"/> goes on the
/// invocation row as a free-text bucket: Anonymous, Visitor, Staff,
/// Admin, Moderator.</summary>
public sealed record AiCallerContext(Guid? UserId, string CallerKind);
