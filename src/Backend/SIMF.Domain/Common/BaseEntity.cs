using System;
using System.Collections.Generic;
using System.Text;
using SIMF.Common;

namespace SIMF.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>The user this key binds to. Real FK to
    /// <see cref="SimfUser.Id"/> (same Identity DbContext).</summary>/
    public Guid CreateBy { get; set; }

    /// <summary>When the row was created (Saudi local). Defaulted at construction
    /// from <see cref="SimfClock"/>, which is fixed +03:00 with no DST — never
    /// <c>DateTime.Now</c> (host-dependent) and never <c>UtcNow</c> (D-813).</summary>
    public DateTime CreatedAt { get; set; } = SimfClock.Now;
}

public abstract class BaseAuditEntity
{
    public Guid Id { get; set; }

    /// <summary>When the row was created (Saudi local) — stamped by the audit
    /// SaveChanges interceptor from the shared TimeProvider when left unset.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The signed-in user who created the row (JWT <c>sub</c>); stamped
    /// by the audit interceptor when unset. <c>Guid.Empty</c> for seeder / system
    /// writes with no bound actor.</summary>
    public Guid CreatedBy { get; set; }

    //
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // /// <summary>Soft-delete flag — false hides the group (and is treated as
    /// hiding its entries) without losing the rows.</summary>
    public bool IsActive { get; set; } = true;//isDeleted
    public DateTime? DeletedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
