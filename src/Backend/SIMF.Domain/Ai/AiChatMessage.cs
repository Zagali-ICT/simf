namespace SIMF.Domain.Ai;

/// <summary>One turn in a visitor's conversation with the AI assistant. Per-user and
/// append-only: these rows are the assistant's memory.</summary>
public sealed class AiChatMessage
{
    /// <summary>Set by the service, not the audit interceptor, as is <see cref="CreatedAt"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database, so no FK crosses.</summary>
    public Guid UserId { get; set; }

    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
