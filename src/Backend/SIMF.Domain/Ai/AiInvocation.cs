using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>
/// One telemetry row per AI call, written on success and on failure alike. The
/// Control Panel's invocations grid pages over this table.
/// </summary>
public sealed class AiInvocation
{
    public Guid Id { get; set; }

    /// <summary>The <see cref="AiPrompt.Key"/> the caller hit.</summary>
    public string PromptKey { get; set; } = string.Empty;

    public AiFeature Feature { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;

    /// <summary>The substituted inputs, as JSON. The service redacts personal
    /// data and secrets before this is persisted.</summary>
    public string InputJson { get; set; } = string.Empty;

    /// <summary>The provider's response; null when the call failed.</summary>
    public string? OutputText { get; set; }

    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }
    public int LatencyMs { get; set; }

    /// <summary>One of the stable API error codes; null on success.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Null for an anonymous caller. A bare Guid: the user lives in the
    /// Identity database.</summary>
    public Guid? CallerUserId { get; set; }

    /// <summary>Which bucket the caller fell into — Anonymous, Visitor, Staff,
    /// Admin or Moderator.</summary>
    public string CallerKind { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
