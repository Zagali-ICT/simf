namespace SIMF.Contracts.Ai;

/// <summary>One saved turn in a visitor's AI-assistant conversation (Page 036),
/// returned by <c>GET /app/ai/assistance/history</c>. <see cref="Role"/> is
/// "user" or "assistant".</summary>
public sealed record AiChatTurn(string Role, string Content);
