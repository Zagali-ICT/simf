// Tests: SIMF.Api.Tests/AiModuleTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Ai.Abstractions;
using SIMF.Contracts.Ai;
using SIMF.Domain.Ai;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Ai;

/// <summary>Persists + reads the visitor's AI-assistant conversation.
/// One append-only row per turn in SIMF_App, keyed by the bare Guid
/// user id — there is no cross-DB FK to Identity. Gives the assistant
/// short-term memory via
/// <see cref="GetRecentContextAsync"/> and the app its saved transcript via
/// <see cref="GetHistoryAsync"/>.</summary>
internal sealed class AiChatHistoryService(
    SimfAppDbContext appDbContext, TimeProvider timeProvider) : IAiChatHistoryService
{
    private const string RoleUser = "user";
    private const string RoleAssistant = "assistant";

    /// <summary>How many recent turns feed the model's short-term memory.</summary>
    private const int MaxMemoryTurns = 12;

    /// <summary>Cap the memory block a safe margin below the per-AI-input limit.
    /// The {history} value is independently capped at 4000 by AiService, so this
    /// only needs to keep the block itself under that limit.</summary>
    private const int MaxMemoryChars = AiInputLimits.MaxInputValueLength - 500;

    public async Task<IReadOnlyList<AiChatTurn>> GetHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await appDbContext.AiChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiChatTurn(m.Role, m.Content))
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GetRecentContextAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // Newest-first + Take so the window is bounded server-side.
        var recent = await appDbContext.AiChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxMemoryTurns)
            .Select(m => new AiChatTurn(m.Role, m.Content))
            .ToListAsync(cancellationToken);

        // Accumulate from the NEWEST end so that under the char cap the OLDEST
        // turns are dropped, not the most recent (short-term memory wants the
        // latest exchange most). Then flip to chronological for the model.
        var kept = new List<string>();
        var length = 0;
        foreach (var turn in recent)
        {
            var line = (string.Equals(turn.Role, RoleUser, StringComparison.Ordinal)
                ? "Visitor: "
                : "Assistant: ") + turn.Content;
            if (length + line.Length + 1 > MaxMemoryChars)
            {
                break;
            }
            kept.Add(line);
            length += line.Length + 1;
        }
        kept.Reverse();
        return string.Join('\n', kept);
    }

    public async Task AppendTurnAsync(
        Guid userId, string userMessage, string assistantReply,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        appDbContext.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = RoleUser,
            Content = Cap(userMessage),
            CreatedAt = now,
        });
        appDbContext.AiChatMessages.Add(new AiChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = RoleAssistant,
            Content = Cap(assistantReply),
            // +1 tick so the assistant reply always orders after its question.
            CreatedAt = now.AddTicks(1),
        });
        await appDbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Trim + clamp to the persisted column length (mirrors the AI
    /// per-input cap), so an over-long turn never fails the insert.</summary>
    private static string Cap(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length > AiInputLimits.MaxInputValueLength
            ? trimmed[..AiInputLimits.MaxInputValueLength]
            : trimmed;
    }
}
