using SIMF.Contracts.Ai;

namespace SIMF.Application.Ai.Abstractions;

/// <summary>Per-user AI-assistant chat history (Page 036). Persists the
/// visitor's conversation so it survives navigation / app-restart and gives the
/// assistant short-term memory of earlier turns. Stored in SIMF_App keyed by a
/// bare Guid user id (D-157 — no cross-DB FK to Identity).</summary>
public interface IAiChatHistoryService
{
    /// <summary>The visitor's full saved transcript, oldest-first.</summary>
    Task<IReadOnlyList<AiChatTurn>> GetHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The recent turns as a compact context block for the model's
    /// short-term memory (capped under the per-AI-input limit). Empty when the
    /// visitor has no prior turns.</summary>
    Task<string> GetRecentContextAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Appends one exchange — the visitor message and the assistant
    /// reply — in a single unit of work.</summary>
    Task AppendTurnAsync(
        Guid userId, string userMessage, string assistantReply,
        CancellationToken cancellationToken = default);
}
