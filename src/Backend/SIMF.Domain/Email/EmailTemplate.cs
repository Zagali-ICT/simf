using SIMF.Common.Enums;

namespace SIMF.Domain.Email;

/// <summary>
/// An admin-authored override for one transactional identity email. Where an
/// active row exists the resolver renders its subject and bodies, filling the
/// <c>{Token}</c> placeholders at send time; where none does, it falls back to
/// the code-owned default.
///
/// <para>The table therefore starts empty, and a row appears only once an admin
/// customises a template. That is deliberate: a missing or malformed row can
/// never stop a sign-in or password-reset email from going out.</para>
/// </summary>
public sealed class EmailTemplate
{
    public Guid Id { get; set; }

    /// <summary>Which transactional email this row overrides. Unique.</summary>
    public EmailTemplateType Type { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyEn { get; set; } = string.Empty;

    /// <summary>Composed right-to-left.</summary>
    public string BodyAr { get; set; } = string.Empty;

    /// <summary>False makes the resolver ignore this row and use the
    /// default.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Incremented on every successful save, so the Control Panel can
    /// show a version label and a test can assert a new one was written.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
