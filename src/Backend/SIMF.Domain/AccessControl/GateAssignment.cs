using SIMF.Domain.Common;

namespace SIMF.Domain.AccessControl;

/// <summary>An operator's assignment to a <see cref="Gate"/>. Revoking deactivates the row rather than deleting it, and the engine reads active rows only.</summary>
public class GateAssignment : BaseEntity
{
    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database, so there is no foreign key.</summary>
    public Guid UserId { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? RevokedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
}
