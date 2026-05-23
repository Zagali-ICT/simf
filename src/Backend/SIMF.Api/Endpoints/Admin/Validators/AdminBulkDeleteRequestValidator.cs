using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates the admin bulk-delete request (decision D-045, H1 hardening of
/// D-044 b). The Ids list is capped at 500 — a single bulk action will not
/// hold the Identity DbContext for more than a few seconds, even at one
/// 4-DB-write transaction per subject.
/// </summary>
public sealed class AdminBulkDeleteRequestValidator : Validator<AdminBulkDeleteRequest>
{
    private const int MaxIds = 500;

    public AdminBulkDeleteRequestValidator()
    {
        RuleFor(request => request.Ids)
            .NotEmpty().Bilingual(
                "At least one user id is required.",
                "يجب تحديد مستخدم واحد على الأقل.")
            .Must(ids => ids.Count <= MaxIds).Bilingual(
                $"At most {MaxIds} ids per request.",
                $"الحد الأقصى {MaxIds} معرّفًا لكل طلب.");

        RuleFor(request => request.Reason)
            .NotEmpty().Bilingual(
                "A reason is required.",
                "السبب مطلوب.")
            .MinimumLength(10).Bilingual(
                "The reason must be at least 10 characters.",
                "يجب أن يتكوّن السبب من 10 أحرف على الأقل.")
            .MaximumLength(500).Bilingual(
                "The reason must be at most 500 characters.",
                "يجب ألا يتجاوز السبب 500 حرف.");
    }
}
