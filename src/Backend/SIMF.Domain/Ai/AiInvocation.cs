using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>D-176 (gap doc G12) — one telemetry/audit row per AI
/// call. Persisted on both success and failure. The CP admin
/// invocations grid pages over this table.</summary>
public sealed class AiInvocation
{
    public Guid Id { get; set; }

    /// <summary>The <see cref="AiPrompt.Key"/> the caller hit.</summary>
    public string PromptKey { get; set; } = string.Empty;

    public AiFeature Feature { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;

    /// <summary>JSON of the substituted input dictionary. Redact at
    /// the service layer before persisting (PII / API keys / etc.).</summary>
    public string InputJson { get; set; } = string.Empty;

    /// <summary>The provider's text response (null on error).</summary>
    public string? OutputText { get; set; }

    public int? TokensInput { get; set; }
    public int? TokensOutput { get; set; }
    public int LatencyMs { get; set; }

    /// <summary>Stable error code (<see cref="SIMF.Common.ErrorCodes"/>)
    /// — null on success.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Logical FK to SimfUser.Id (Identity DB). Null for
    /// anonymous callers.</summary>
    public Guid? CallerUserId { get; set; }

    /// <summary>Free-text caller bucket: Anonymous, Visitor, Staff,
    /// Admin, Moderator.</summary>
    public string CallerKind { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
