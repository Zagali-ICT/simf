using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Application.Ai.Abstractions;

/// <summary>Admin CRUD over <c>AiPrompt</c>.
/// All writes audit + bump <c>Version</c>.</summary>
public interface IAdminAiPromptService
{
    Task<GridPage<AdminAiPromptSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminAiPromptDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminAiPromptDetail> CreateAsync(
        Guid actorUserId, CreateAiPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminAiPromptDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateAiPromptRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<AiCallResult> TestAsync(
        Guid actorUserId, Guid id, TestAiPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminAiInvocationRow>> ListInvocationsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>Full payload (InputJson + OutputText) for SOC
    /// drill-down. The grid row deliberately omits these for the
    /// admin grid; this method is the audit-trail read.</summary>
    Task<AdminAiInvocationDetail?> GetInvocationAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Append-only snapshot history for the given
    /// AiPrompt id. Newest-first. Empty list when the prompt has
    /// never been updated past v1. Returns an empty list for an
    /// unknown id (no 404 — the caller already does the existence
    /// check via <see cref="GetAsync"/> when needed).</summary>
    Task<IReadOnlyList<AdminAiPromptHistoryEntry>> GetHistoryAsync(
        Guid promptId, CancellationToken cancellationToken = default);

    /// <summary>CP Phase-1 — the AI dashboard: rolled-up invocation health over
    /// the last <paramref name="windowHours"/> hours (calls / errors / latency /
    /// tokens, overall + per service) plus the configured-service counts.</summary>
    Task<AdminAiDashboard> GetDashboardAsync(
        int windowHours = 24, CancellationToken cancellationToken = default);
}
