using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Email;

namespace SIMF.Application.Email;

/// <summary>D-735 — the CP admin surface over the transactional email templates:
/// list the fixed set, read one for editing, save an override (validating tokens,
/// bumping the version), reset to the code default, and render a live preview.
/// The catalogue owns the defaults; the DB owns only overrides.</summary>
public interface IAdminEmailTemplateService
{
    Task<GridPage<AdminEmailTemplateSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDetail> GetAsync(
        EmailTemplateType type, CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDetail> UpdateAsync(
        Guid actorUserId,
        EmailTemplateType type,
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDetail> ResetAsync(
        Guid actorUserId,
        EmailTemplateType type,
        CancellationToken cancellationToken = default);

    EmailTemplatePreviewResult Preview(
        EmailTemplateType type, PreviewEmailTemplateRequest request);
}
