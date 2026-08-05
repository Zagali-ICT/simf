using SIMF.Domain.Common;

namespace SIMF.Domain.Support;

/// <summary>
/// A "contact us" message from the app's contact form, kept so admins can triage
/// it in the Control Panel inbox.
/// </summary>
public sealed class ContactInquiry : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Where a reply goes.</summary>
    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>The signed-in user who submitted it, null for a guest. A bare
    /// Guid resolved on read: the user lives in the Identity database.</summary>
    public Guid? SubmittedByUserId { get; set; }

    /// <summary>Set once an admin has actioned the inquiry.</summary>
    public bool IsHandled { get; set; }

    /// <summary>Saudi local time, null until it is handled.</summary>
    public DateTime? HandledAt { get; set; }

    public Guid? HandledByUserId { get; set; }
}
