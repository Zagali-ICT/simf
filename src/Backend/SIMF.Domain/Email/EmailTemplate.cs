using SIMF.Common.Enums;

namespace SIMF.Domain.Email;

/// <summary>
/// An admin-authored override for one transactional identity email; the resolver renders
/// this row's subject and bodies, filling <c>{Token}</c> placeholders at send time, and
/// falls back to the code-owned default where no active row exists. The table therefore
/// ships empty, so a missing or malformed row can never stop a sign-in or password-reset
/// email going out.
/// </summary>
public sealed class EmailTemplate
{
    public Guid Id { get; set; }

    public EmailTemplateType Type { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyEn { get; set; } = string.Empty;

    public string BodyAr { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Incremented on every successful save.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
