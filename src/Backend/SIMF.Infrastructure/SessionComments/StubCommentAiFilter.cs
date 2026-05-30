// Tests: SIMF.Api.Tests/SessionCommentsTests.cs
using Microsoft.Extensions.Options;
using SIMF.Application.SessionComments.Abstractions;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.SessionComments;

/// <summary>
/// D-199 — the stub AI filter shipped in this increment. It does NOT
/// call any AI provider: it returns a deterministic verdict driven by
/// <see cref="CommentAiFilterOptions.AutoApprove"/>. This is the
/// explicit seam the increment calls for — the real moderation call
/// (routing through the central <c>IAiService</c> prompt catalogue,
/// D-176 / gap doc G12) replaces THIS class behind the unchanged
/// <see cref="ICommentAiFilter"/> interface, with no change to the
/// comment service or its tests.
///
/// <para><b>Wiring the real filter later:</b> implement
/// <see cref="ICommentAiFilter"/> with a class that injects
/// <c>IAiService</c>, calls
/// <c>InvokeAsync("comment-filter", { ["text"] = body }, caller, ct)</c>,
/// maps the model's verdict to <see cref="SessionCommentStatus.Approved"/>
/// vs <see cref="SessionCommentStatus.Pending"/>, and swap the DI
/// registration in <c>DependencyInjection.cs</c> from
/// <see cref="StubCommentAiFilter"/> to it. (The <c>comment-filter</c>
/// prompt row would be seeded into the AiPrompt catalogue at that
/// point — it is intentionally NOT seeded now, since the stub never
/// reads it.)</para>
/// </summary>
internal sealed class StubCommentAiFilter(IOptions<CommentAiFilterOptions> options)
    : ICommentAiFilter
{
    private readonly CommentAiFilterOptions _options = options.Value;

    public Task<CommentFilterVerdict> ScreenAsync(
        Guid sessionId,
        Guid userId,
        string body,
        CancellationToken cancellationToken = default)
    {
        // Deterministic, offline, no provider call. The verdict bucket
        // makes it clear in the audit trail that no real model ran.
        var verdict = _options.AutoApprove
            ? new CommentFilterVerdict(SessionCommentStatus.Approved, "stub-auto-approve")
            : new CommentFilterVerdict(SessionCommentStatus.Pending, "stub-manual-review");
        return Task.FromResult(verdict);
    }
}
