using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>The visual + audio tier of a notification.</summary>
public enum NotificationSeverity
{
    [Display(Description = nameof(ResNotificationSeverity.Info), ResourceType = typeof(ResNotificationSeverity))]
    Info = 0,

    [Display(Description = nameof(ResNotificationSeverity.Success), ResourceType = typeof(ResNotificationSeverity))]
    Success = 1,

    [Display(Description = nameof(ResNotificationSeverity.Warning), ResourceType = typeof(ResNotificationSeverity))]
    Warning = 2,

    [Display(Description = nameof(ResNotificationSeverity.Error), ResourceType = typeof(ResNotificationSeverity))]
    Error = 3,
}
