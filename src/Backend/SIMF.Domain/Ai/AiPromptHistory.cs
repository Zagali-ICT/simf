using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>
/// An immutable snapshot of an <see cref="AiPrompt"/>, written on every
/// successful update. It captures the state *before* the change, so a prior
/// version can be reconstructed after a drift detection.
///
/// <para>One row per prompt and version: the live row holds the current
/// revision, this table holds every earlier one. Append-only, with no update or
/// delete path, and a unique constraint on the pair rejects a duplicate snapshot
/// at the same version.</para>
///
/// <para>The text is stored as-is, neither hashed nor redacted. Redaction
/// applies to runtime invocation inputs and outputs, which can carry user data;
/// an admin-edited prompt template cannot. Encryption at rest is the host's
/// control.</para>
/// </summary>
public sealed class AiPromptHistory
{
    public Guid Id { get; set; }

    /// <summary>No navigation property: the snapshot outlives the live row's
    /// lifecycle, and deactivating the prompt must not touch its history.</summary>
    public Guid AiPromptId { get; set; }

    /// <summary>The version this row captures, being the live value before the
    /// bump that produced the snapshot.</summary>
    public int Version { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;

    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
    public bool IsActive { get; set; }

    /// <summary>An HMAC over the system prompt and the user template, in the
    /// shape the audit log already uses, so a reviewer can match this snapshot to
    /// its audit row by hash without loading the full text.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>When the live row stood at this version: the replaced version's
    /// update time, or the prompt's creation time for a version-1 baseline.</summary>
    public DateTime CapturedFromUpdatedAt { get; set; }

    /// <summary>Who authored the version being captured. Null for the initial
    /// baseline, which no one edited.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>When the snapshot was written, as its successor was about to
    /// land.</summary>
    public DateTime CapturedAt { get; set; }
}
