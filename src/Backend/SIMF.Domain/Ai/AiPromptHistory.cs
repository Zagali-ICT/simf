using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>D-188 (D-181 architectural carry-over) — immutable
/// snapshot of an <see cref="AiPrompt"/> at every successful
/// <c>UpdateAsync</c>. Captures the PRE-mutation state so a SOC
/// analyst (or the CP "History" tab) can reconstruct any prior
/// version after a drift detection from the
/// <c>AiPrompt.Updated</c> audit row.
///
/// <para>One row per (AiPromptId, Version). The current
/// <see cref="AiPrompt.Version"/> is the live revision; this table
/// holds versions <c>1 .. Current-1</c>. Append-only — the table
/// has no Update or Delete code paths. The configuration enforces
/// the unique (AiPromptId, Version) constraint so a duplicate
/// snapshot at the same version is rejected at the DB level.</para>
///
/// <para>Stored as-is (no HMAC, no redaction). The audit-trail
/// integrity assumption is that admin-edited prompt text is not a
/// PII surface — the audit-time redaction in <c>AiAuditDetail</c>
/// applies only to runtime invocation inputs/outputs, not to the
/// prompt templates themselves. Encryption-at-rest is the host-side
/// control.</para></summary>
public sealed class AiPromptHistory
{
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="AiPrompt.Id"/>. No navigation property
    /// — the snapshot is independent of the live row's lifecycle (the
    /// live row can be deactivated, the snapshot remains).</summary>
    public Guid AiPromptId { get; set; }

    /// <summary>The version number this row captures. Matches the
    /// <see cref="AiPrompt.Version"/> value BEFORE the bump that
    /// produced this snapshot.</summary>
    public int Version { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;

    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
    public bool IsActive { get; set; }

    /// <summary>HMAC of <see cref="SystemPrompt"/> + 0x1F +
    /// <see cref="UserPromptTemplate"/>. The same shape the audit
    /// log already uses (D-181 <c>v1:</c> prefix). Lets a SOC
    /// analyst correlate this snapshot with the
    /// <c>AiPrompt.Updated</c> audit row by hash without loading the
    /// full text.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Snapshot of when the live row was at this version
    /// (i.e. the <see cref="AiPrompt.UpdatedAt"/> from the version
    /// being replaced, or <see cref="AiPrompt.CreatedAt"/> for
    /// version 1 baseline snapshots).</summary>
    public DateTimeOffset CapturedFromUpdatedAt { get; set; }

    /// <summary>The admin user id that authored the version this row
    /// captures. Null when capturing the initial v1 baseline.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>When the snapshot row was written (i.e. when the
    /// successor version was about to land).</summary>
    public DateTimeOffset CapturedAt { get; set; }
}
