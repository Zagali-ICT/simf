// Tests: SIMF.Domain.Tests/BaseAuditEntityTests.cs
using SIMF.Common;

namespace SIMF.Domain.Common;

/// <summary>Entities needing update and soft-delete columns derive from <see cref="BaseAuditEntity"/> instead.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>Saudi local time from the shared clock (fixed +03:00): deliberately neither <c>DateTime.Now</c>, which follows the host, nor <c>UtcNow</c>, which no surface displays.</summary>
    public DateTime CreatedAt { get; set; } = SimfClock.Now;
}

/// <summary>Full audit trail; the audit save-changes interceptor fills any stamp left unset.</summary>
public abstract class BaseAuditEntity
{
    public Guid Id { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Empty for seeder and system writes, which have no actor bound to them.</summary>
    public Guid CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>When this row was soft-deleted. Stamped by <see cref="Deactivate"/>,
    /// so a soft-deleted row always says WHEN, not merely that it is gone: the
    /// high-level and low-level designs both describe soft-delete as flipping
    /// <see cref="IsActive"/> and stamping this, and the offline-sync design reads
    /// it to build the deletion "tombstones" an app replays against its local
    /// cache. A restore clears it back to null.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Soft-deletes the row, recording the instant it happened.
    ///
    /// <para>Several services call this without first checking whether the row is
    /// already inactive, so the stamp is written only when it is absent: the
    /// instant worth keeping is when the row was deleted, not when a repeat call
    /// asked again. A service holding an injected <c>TimeProvider</c> may still
    /// assign <see cref="DeletedAt"/> itself to pin its own clock, and doing so
    /// before or after this call gives the same result.</para></summary>
    public void Deactivate()
    {
        IsActive = false;
        DeletedAt ??= SimfClock.Now;
    }
}
