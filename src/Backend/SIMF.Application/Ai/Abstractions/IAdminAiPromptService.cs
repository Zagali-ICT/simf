using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.Application.Ai.Abstractions;

/// <summary>D-176 (gap doc G12) — admin CRUD over <c>AiPrompt</c>.
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
}
