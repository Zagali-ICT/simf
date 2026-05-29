using SIMF.Common.Enums;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// D-148 — a configured venue access point (Gate Module, SIMF-FDS-003 §5.6).
/// Each gate has a direction mode (In / Out / Both), an optional allow-list of
/// <see cref="UserProfileId"/> profile types (empty = general gate), and a set
/// of assigned operators via <see cref="GateAssignment"/>. The constraint engine
/// in <c>GateOperatorService</c> applies these to every scan.
/// </summary>
public class Gate
{
    public Guid Id { get; set; }

    /// <summary>Short stable code (2–16 chars; case-insensitive unique;
    /// uppercase-normalised by the service).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English display name (1–128 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Optional English description (≤ 1024 chars).</summary>
    public string? Description { get; set; }

    /// <summary>Optional Arabic description (≤ 1024 chars).</summary>
    public string? DescriptionArabic { get; set; }

    /// <summary>Direction policy. In / Out → fixed direction button on the
    /// operator console. Both → server-side direction inference based on
    /// the visitor's last allowed scan at this gate (constraint engine
    /// step 10).</summary>
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;

    /// <summary>Soft-delete flag. <c>false</c> rows still hold their
    /// assignments + scan history but the operator console hides the
    /// gate and POST /scans returns 503 GATE_INACTIVE.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>The allowed-profile-type rows. Empty = all allowed (general
    /// gate). Non-empty = filtered through active ProfileTypes per the
    /// safe-default rule L-15 (deactivating every entry denies all scans
    /// rather than silently flipping to "everyone").</summary>
    public ICollection<GateProfileTypeAllow> AllowedProfileTypes { get; set; }
        = new List<GateProfileTypeAllow>();

    /// <summary>The operator assignments. Active rows are the only ones
    /// the engine checks; inactive rows are kept for audit.</summary>
    public ICollection<GateAssignment> Assignments { get; set; }
        = new List<GateAssignment>();
}
