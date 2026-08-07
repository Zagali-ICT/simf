namespace SIMF.Domain.Ai;

/// <summary>
/// One turn in a visitor's conversation with the AI assistant. Per-user and
/// append-only: these rows are the assistant's memory.
/// </summary>
public sealed class AiChatMessage
{
    /// <summary>Set by the service rather than by the audit interceptor, as is
    /// <see cref="CreatedAt"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>A bare Guid: the user lives in the Identity database, so there is
    /// no navigation and no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>Who authored the turn: "user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Capped at the configured AI input limit.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
