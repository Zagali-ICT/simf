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

    public DateTime? DeletedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
