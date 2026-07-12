using SIMF.Common.Enums;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>D-148 — read-through cache for active gate configuration
/// (5-minute TTL per D-148 reasoning). Backed by <c>IMemoryCache</c>.
/// The CP master-detail invalidates entries on Update / Deactivate.</summary>
public interface IGateConfigCache
{
    Task<GateConfigSnapshot?> GetAsync(
        Guid gateId, CancellationToken cancellationToken = default);

    void Invalidate(Guid gateId);
}

/// <summary>D-148 — read-only snapshot of the gate config the engine consumes.
/// Allowed-profile-type ids are pre-filtered against active ProfileTypes per
/// L-15 safe-default rule.</summary>
public sealed record GateConfigSnapshot(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    DirectionMode DirectionMode,
    bool IsActive,
    /// <summary>The original allowed-profile-type id list, regardless of active state.</summary>
    IReadOnlyList<Guid> AllowedProfileTypeIdsRaw,
    /// <summary>The allow-list filtered to active ProfileTypes. Empty
    /// when the original list was empty (general gate). Filtered-empty
    /// when the raw list had entries but none reference active rows —
    /// in this case the engine denies all (L-15).</summary>
    IReadOnlyList<Guid> AllowedProfileTypeIdsFiltered,
    /// <summary>The active operator assignment user ids for this gate.</summary>
    IReadOnlyList<Guid> AssignedOperatorUserIds,
    /// <summary>X-1 — the hall this gate is a door for, or null for a
    /// perimeter/venue gate. Drives the hall-attendance chain on allowed scans.</summary>
    Guid? HallId = null);
