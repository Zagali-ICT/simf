using SIMF.Domain.Common;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// D-148 — an operator-to-gate assignment (SIMF-FDS-003 §5.6 / design notes
/// §2.1). Many-to-many between <see cref="Gate"/> and <c>SimfUser</c> with
/// activation lifecycle. An operator can be assigned to multiple gates; an
/// admin can rotate them between gates and the history survives via the
/// <see cref="RevokedAt"/> / <see cref="IsActive"/> pair. All UserId references
/// are logical FKs (cross-context — DAT-001 §5.3.1).
/// </summary>
public class GateAssignment : BaseEntity
{

    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }


    /// <summary>The assigned operator. Logical FK to <c>SimfUser</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>True while this assignment is in effect. The constraint
    /// engine reads only active rows.</summary>
    public bool IsActive { get; set; } = true;



    /// <summary>The admin who revoked this assignment, when applicable.
    /// Logical FK to <c>SimfUser</c>.</summary>
    public Guid? RevokedByUserId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    
}
