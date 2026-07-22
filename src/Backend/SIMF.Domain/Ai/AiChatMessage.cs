namespace SIMF.Domain.Ai;

/// <summary>One turn in a visitor's AI-assistant conversation (Page 036 —
/// المساعد الذكي). Per-user, append-only; the assistant's persisted memory.
/// <see cref="UserId"/> is a bare logical FK to SimfUser.Id on the Identity DB
/// (D-157 — no cross-DB FK, no navigation, resolved on read). Convention-B POCO:
/// <see cref="Id"/> / <see cref="CreatedAt"/> are set by the service, not the
/// audit interceptor.</summary>
public sealed class AiChatMessage
{
    public Guid Id { get; set; }

    /// <summary>Logical FK to SimfUser.Id (Identity DB) — never a DB constraint
    /// and never a navigation (D-157).</summary>
    public Guid UserId { get; set; }

    /// <summary>Who authored the turn: "user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The message text (≤ the per-AI-input cap).</summary>
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
