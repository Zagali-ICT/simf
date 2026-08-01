using SIMF.Common.Enums;

namespace SIMF.Domain.Email;

/// <summary>D-735 — an admin-authored override for one transactional identity
/// email (<see cref="Type"/>). When an active row exists the resolver renders
/// its <see cref="Subject"/> / <see cref="BodyEn"/> / <see cref="BodyAr"/> (each
/// a <c>{Token}</c> template) and fills the tokens at send time; when no active
/// row exists it falls back to the code-owned default in
/// <c>EmailTemplateCatalog</c>. The table therefore starts EMPTY — a row is
/// written only when an admin customises a template — so a missing or broken
/// row can never stop a sign-in / reset email from going out.</summary>
public sealed class EmailTemplate
{
    public Guid Id { get; set; }

    /// <summary>Which transactional email this row overrides. Unique.</summary>
    public EmailTemplateType Type { get; set; }

    /// <summary>Email subject line. Supports <c>{Token}</c> placeholders.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>English HTML body block. <c>{Token}</c> template.</summary>
    public string BodyEn { get; set; } = string.Empty;

    /// <summary>Arabic HTML body block (composed right-to-left).
    /// <c>{Token}</c> template.</summary>
    public string BodyAr { get; set; } = string.Empty;

    /// <summary>When false the resolver ignores this row and uses the default.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Bumped by 1 on every successful save. Lets the CP show a
    /// version label and lets a test assert a new version was written.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
