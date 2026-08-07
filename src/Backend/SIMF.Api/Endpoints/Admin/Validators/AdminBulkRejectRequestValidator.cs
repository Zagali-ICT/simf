using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates the bulk-reject request. The Ids list is capped at 500
/// (same window as bulk-delete), and the shared reason follows the same
/// 10–500 char rule as the single reject (<see cref="RejectRouteRequestValidator"/>)
/// and the 2FA-reset validator.
/// </summary>
public sealed class AdminBulkRejectRequestValidator : Validator<AdminBulkRejectRequest>
{
    private const int MaxIds = 500;

    public AdminBulkRejectRequestValidator()
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
                "يجب ألا يتجاوز السبب 500 حرفًا.");
    }
}
