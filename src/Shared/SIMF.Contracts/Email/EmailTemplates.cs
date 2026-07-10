using SIMF.Common.Enums;

namespace SIMF.Contracts.Email;

/// <summary>D-735 — one row in the CP email-templates grid. <see cref="IsOverride"/>
/// is true when an admin has customised the template (a DB row exists); otherwise
/// the subject shown is the built-in default and <see cref="Version"/> is 0.</summary>
public sealed record AdminEmailTemplateSummary(
    EmailTemplateType Type,
    string TypeName,
    string Subject,
    bool IsOverride,
    int Version,
    DateTimeOffset? UpdatedAt);

/// <summary>D-735 — one editable placeholder a template exposes (used as
/// <c>{Name}</c>), with bilingual labels for the CP token chips and a sample
/// value for the live preview.</summary>
public sealed record EmailTemplateTokenDto(
    string Name, string DisplayEn, string DisplayAr, string Sample);

/// <summary>D-735 — full detail for the editor: the CURRENT subject/body (the
/// admin override if present, else the code default), the code DEFAULTS (so the
/// editor can preview + reset), the tokens the body may reference, and whether an
/// override row exists.</summary>
public sealed record AdminEmailTemplateDetail(
    EmailTemplateType Type,
    string TypeName,
    string Subject,
    string BodyEn,
    string BodyAr,
    bool IsActive,
    bool IsOverride,
    int Version,
    string DefaultSubject,
    string DefaultBodyEn,
    string DefaultBodyAr,
    IReadOnlyList<EmailTemplateTokenDto> Tokens,
    DateTimeOffset? UpdatedAt);

/// <summary>D-735 — save the admin's edited copy for a template type. When
/// <see cref="IsActive"/> is false the override is kept but the resolver falls
/// back to the code default (a "draft" state).</summary>
public class UpdateEmailTemplateRequest
{
    public string Subject { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>D-735 — render arbitrary (unsaved) template copy with the catalogue's
/// sample values for the CP live preview.</summary>
public class PreviewEmailTemplateRequest
{
    public string Subject { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
}

/// <summary>D-735 — the rendered preview: the subject + composed bilingual HTML
/// body (sample values substituted), plus any tokens the copy references that
/// this template type does not define (a non-empty list ⇒ the CP blocks save).</summary>
public sealed record EmailTemplatePreviewResult(
    string Subject,
    string HtmlBody,
    IReadOnlyList<string> UnknownTokens);
