using SIMF.Domain.Common;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// An operator's assignment to a <see cref="Gate"/>, many-to-many and with its
/// own activation lifecycle. One operator can hold several gates, and an admin
/// can rotate them between gates without losing the history, which survives in
/// the revoked and active columns.
/// </summary>
public class GateAssignment : BaseEntity
{
    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>The assigned operator. A bare Guid: the user lives in the
    /// Identity database, so there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>The constraint engine reads active rows only.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The admin who revoked the assignment, where one did.</summary>
    public Guid? RevokedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
}
