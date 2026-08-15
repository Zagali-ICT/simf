using SIMF.Common;

namespace SIMF.Domain.Common;

/// <summary>
/// The lighter of the two bases: an id, a creating user and a creation stamp,
/// with no update or soft-delete columns. Entities that need those derive from
/// <see cref="BaseAuditEntity"/> instead.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>The user who created the row. Named to match
    /// <see cref="BaseAuditEntity.CreatedBy"/>, which records the identical fact
    /// on the heavier base.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Saudi local time, defaulted at construction from the shared
    /// clock, which is fixed at +03:00 with no daylight saving. Deliberately
    /// neither <c>DateTime.Now</c>, which depends on the host, nor
    /// <c>UtcNow</c>, which no surface here displays.</summary>
    public DateTime CreatedAt { get; set; } = SimfClock.Now;
}

/// <summary>
/// The base for entities that carry a full audit trail: who created and last
/// changed the row, when, and whether it is soft-deleted. The audit
/// save-changes interceptor fills the stamps that are left unset.
/// </summary>
public abstract class BaseAuditEntity
{
    public Guid Id { get; set; }

    /// <summary>Saudi local time, stamped by the audit interceptor when left
    /// unset.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The signed-in user who created the row, stamped by the audit
    /// interceptor when unset. Empty for seeder and system writes, which have no
    /// actor bound to them.</summary>
    public Guid CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Soft-delete flag: false hides the row without losing it.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
