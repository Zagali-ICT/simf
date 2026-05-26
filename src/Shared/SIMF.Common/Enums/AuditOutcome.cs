using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>Whether an audited operation succeeded or failed.</summary>
public enum AuditOutcome
{
    [Display(Description = nameof(ResAuditOutcome.Success), ResourceType = typeof(ResAuditOutcome))]
    Success = 0,

    [Display(Description = nameof(ResAuditOutcome.Failure), ResourceType = typeof(ResAuditOutcome))]
    Failure = 1,
}
