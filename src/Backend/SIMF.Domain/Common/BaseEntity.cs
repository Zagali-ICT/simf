using System;
using System.Collections.Generic;
using System.Text;

namespace SIMF.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>The user this key binds to. Real FK to
    /// <see cref="SimfUser.Id"/> (same Identity DbContext).</summary>/
    public Guid CreateBy { get; set; }

    /// <summary>When the token was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTime.Now;
}

public abstract class BaseAuditEntity
{
    public Guid Id { get; set; }

    //
    public DateTimeOffset CreatedAt { get; set; } = DateTime.Now;
    public Guid CreatedBy { get; set; }

    //
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // /// <summary>Soft-delete flag — false hides the group (and is treated as
    /// hiding its entries) without losing the rows.</summary>
    public bool IsActive { get; set; } = true;//isDeleted
    public DateTimeOffset? DeletedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
