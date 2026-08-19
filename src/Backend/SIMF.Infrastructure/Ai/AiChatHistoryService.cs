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

    /// <summary>The ceiling on one saved-transcript read. Nothing prunes this
    /// append-only table, so an unbounded read would materialise every turn a
    /// visitor ever sent and serialise the lot into a single response on an
    /// endpoint the chat screen calls on every open. Past the cap the OLDEST
    /// turns drop, the same way the memory window does.</summary>
    private const int MaxHistoryTurns = 200;

    /// <summary>Cap the memory block a safe margin below the per-AI-input limit.
    /// The {history} value is independently capped at 4000 by AiService, so this
    /// only needs to keep the block itself under that limit.</summary>
    private const int MaxMemoryChars = AiInputLimits.MaxInputValueLength - 500;

    public async Task<IReadOnlyList<AiChatTurn>> GetHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // Newest-first + Take so the read is bounded in SQL, then flipped back to
        // chronological for the app. The wire shape is unchanged.
        var newestFirst = await appDbContext.AiChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryTurns)
            .Select(m => new AiChatTurn(m.Role, m.Content))
            .ToListAsync(cancellationToken);
        newestFirst.Reverse();
        return newestFirst;
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
        //
        // This is where redaction belongs, and the ONLY place the transcript is
        // redacted. The rows themselves are stored as written and encrypted at
        // rest; what must not carry a visitor's national id, mobile, IBAN or an
        // API key is the block about to be pasted into a prompt and sent to an
        // external model provider. Redacting at write time instead protected the
        // same values one step too late (they had already been sent, in the turn
        // that produced them) and mangled the visitor's own history for the
        // privilege. Redaction runs BEFORE the length accounting so the cap is
        // measured on the text that is actually sent.
        var kept = new List<string>();
        var length = 0;
        foreach (var turn in recent)
        {
            var line = (string.Equals(turn.Role, RoleUser, StringComparison.Ordinal)
                ? "Visitor: "
                : "Assistant: ") + AiAuditDetail.RedactValue(turn.Content);
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
    /// per-input cap), so an over-long turn never fails the insert.
    ///
    /// <para>Both turns are stored as the visitor and the model actually wrote
    /// them. They are NOT redacted here, and the earlier version of this method
    /// that did redact them was solving the right problem in the wrong place: it
    /// caught an Iqama or a mobile number because those are patterns, and missed a
    /// home address, an employer or a diagnosis because those are not -- while
    /// also handing the visitor back a transcript reading "[REDACTED_EMAIL]" where
    /// the assistant had quoted the event's own contact address. The column is now
    /// encrypted at rest (see SimfAppDbContext.OnModelCreating), which covers the
    /// whole turn rather than the parts a regular expression can name, and
    /// redaction moved to GetRecentContextAsync, on the boundary it was always for:
    /// what leaves this system for a model provider.</para></summary>
    private static string Cap(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length > AiInputLimits.MaxInputValueLength
            ? trimmed[..AiInputLimits.MaxInputValueLength]
            : trimmed;
    }
}
