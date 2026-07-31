using SIMF.Domain.Common;

namespace SIMF.Domain.Support;

/// <summary>
/// A "تواصل معنا / Contact us" message submitted from the app's contact form
/// (Figma 1388:7567). Stored so admins can triage it in the Control Panel
/// inbox. Additive table (App DB) — no cross-DB relation: the optional
/// submitter is a bare <see cref="SubmittedByUserId"/> Guid (logical FK to
/// <c>SimfUser.Id</c> in the Identity DB), resolved on read, never a DB FK
/// (D-157).
/// </summary>
public sealed class ContactInquiry : BaseAuditEntity
{
    /// <summary>Sender's display name (required).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sender's reply-to email (required).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The message body (required).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The signed-in user who submitted, when authenticated; null for
    /// an anonymous (guest) submission. Bare Guid — no DB FK (D-157).</summary>
    public Guid? SubmittedByUserId { get; set; }

    /// <summary>Set once an admin has actioned the inquiry.</summary>
    public bool IsHandled { get; set; }

    /// <summary>When it was marked handled (UTC), else null.</summary>
    public DateTime? HandledAt { get; set; }

    /// <summary>The admin who marked it handled (JWT <c>sub</c>), else null.</summary>
    public Guid? HandledByUserId { get; set; }
}
