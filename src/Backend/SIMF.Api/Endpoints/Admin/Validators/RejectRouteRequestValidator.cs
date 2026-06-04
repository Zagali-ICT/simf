using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.Endpoints.Auth.Validators;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates the reject request shape (P4) — applies to both
/// <c>POST /admin/staff/{id}/reject</c> and <c>POST /admin/visitors/{id}/reject</c>
/// since they share <see cref="RejectRouteRequest"/>. Same 10–500 char
/// reason rule as the 2FA-reset validator (D-041).
/// </summary>
public sealed class RejectRouteRequestValidator : Validator<RejectRouteRequest>
{
    public RejectRouteRequestValidator()
    {
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
