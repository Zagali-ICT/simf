using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>The kind of row-level change the audit row records.</summary>
public enum RowAuditOperation
{
    [Display(Description = nameof(ResRowAuditOperation.Insert), ResourceType = typeof(ResRowAuditOperation))]
    Insert = 0,

    [Display(Description = nameof(ResRowAuditOperation.Update), ResourceType = typeof(ResRowAuditOperation))]
    Update = 1,

    [Display(Description = nameof(ResRowAuditOperation.Delete), ResourceType = typeof(ResRowAuditOperation))]
    Delete = 2,
}
