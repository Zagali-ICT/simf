using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>One telemetry row per AI call, written on failure as well as success.</summary>
public sealed class AiInvocation
{
    public Guid Id { get; set; }

    /// <summary>The <see cref="AiPrompt.Key"/> the caller hit.</summary>
    public string PromptKey { get; set; } = string.Empty;

    public AiFeature Feature { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;

    /// <summary>The substituted inputs as JSON. The service redacts personal data
    /// and secrets before this is persisted.</summary>
    public string InputJson { get; set; } = string.Empty;

    public string? OutputText { get; set; }

    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }
    public int LatencyMs { get; set; }

    /// <summary>One of the stable API error codes; null on success.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database. Null when anonymous.</summary>
    public Guid? CallerUserId { get; set; }

    /// <summary>Anonymous, Visitor, Staff, Admin or Moderator.</summary>
    public string CallerKind { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
