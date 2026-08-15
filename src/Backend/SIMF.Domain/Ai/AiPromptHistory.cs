using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>An immutable snapshot of an <see cref="AiPrompt"/>, written on every update
/// and capturing the state BEFORE the change. Append-only: no update or delete path. The
/// template text is stored unredacted; redaction covers invocation inputs and outputs,
/// which can carry user data, and a template cannot.</summary>
public sealed class AiPromptHistory
{
    public Guid Id { get; set; }

    public Guid AiPromptId { get; set; }

    public int Version { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;

    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
    public bool IsActive { get; set; }

    /// <summary>HMAC over the system prompt and user template, in the shape the audit
    /// log uses, so a reviewer can match snapshot to audit row without the full text.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>When the live row stood at this version: the replaced version's update
    /// time, or the prompt's creation time for a version-1 baseline.</summary>
    public DateTime CapturedFromUpdatedAt { get; set; }

    /// <summary>Who authored the captured version. Null for the initial baseline.</summary>
    public Guid? UpdatedByUserId { get; set; }

    public DateTime CapturedAt { get; set; }
}
