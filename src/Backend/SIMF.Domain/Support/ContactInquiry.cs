using SIMF.Domain.Common;

namespace SIMF.Domain.Support;

/// <summary>A "contact us" message from the app's contact form, triaged in the Control Panel inbox.</summary>
public sealed class ContactInquiry : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Null for a guest. A bare Guid: the user lives in the Identity database.</summary>
    public Guid? SubmittedByUserId { get; set; }

    public bool IsHandled { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime? HandledAt { get; set; }

    public Guid? HandledByUserId { get; set; }
}
