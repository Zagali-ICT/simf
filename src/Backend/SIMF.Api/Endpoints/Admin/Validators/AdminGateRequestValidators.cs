// Tests: SIMF.Api.Tests/AdminGatesTests.cs (BUG-018 — blank name / over-long
//        code are rejected before the service runs)
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// BUG-018 (18-5) — the gate create request had no FluentValidation validator at
/// all; every shape rule lived only in the service. The limits here mirror the EF
/// column maxes on <c>Gate</c> (<c>GateConfiguration</c>) and the CP form's
/// <c>MaxLength</c> attributes (<c>GatesAddEdit.razor</c>) — the validation
/// triple-lock. Uniqueness and the logical-FK rules stay in the service: they need
/// a DB read and carry more specific error codes.
/// </summary>
public sealed class AdminCreateGateRequestValidator
    : Validator<AdminCreateGateRequest>
{
    public AdminCreateGateRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().Bilingual("Gate code is required.", "رمز البوابة مطلوب.")
            // The 2-character MINIMUM stays in the service (it answers with the
            // more specific GATE_INVALID code the CP + tests key off); this
            // validator owns the "required + max length" half of the triple-lock.
            .MaximumLength(16).Bilingual(
                "Gate code must be at most 16 characters.",
                "يجب ألا يتجاوز رمز البوابة 16 حرفاً.");

        RuleFor(request => request.Name)
            .NotEmpty().Bilingual("Name is required.", "الاسم مطلوب.")
            .MaximumLength(128).Bilingual(
                "Name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم 128 حرفاً.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual("Arabic name is required.", "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفاً.");

        RuleFor(request => request.Description)
            .MaximumLength(1024).Bilingual(
                "Description must be at most 1024 characters.",
                "يجب ألا يتجاوز الوصف 1024 حرفاً.");

        RuleFor(request => request.DescriptionArabic)
            .MaximumLength(1024).Bilingual(
                "Arabic description must be at most 1024 characters.",
                "يجب ألا يتجاوز الوصف بالعربية 1024 حرفاً.");

        RuleFor(request => request.AllowedProfileTypeIds)
            .NotNull().Bilingual(
                "The allowed profile type list must be present (it may be empty).",
                "قائمة أنواع الملفات المسموح بها مطلوبة (ويمكن أن تكون فارغة).");

        RuleFor(request => request.AssignedOperatorUserIds)
            .NotNull().Bilingual(
                "The assigned operator list must be present (it may be empty).",
                "قائمة المشغّلين المعيّنين مطلوبة (ويمكن أن تكون فارغة).");
    }
}

/// <summary>BUG-018 (18-5) — same shape rules for the update endpoint's
/// route-bound request (<see cref="UpdateGateRequest"/>). Written out rather than
/// shared with the create validator, matching the create/update validator pairs
/// already in this folder (e.g. the ProfileType ones).</summary>
public sealed class UpdateGateRequestValidator
    : Validator<UpdateGateRequest>
{
    public UpdateGateRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().Bilingual("Gate code is required.", "رمز البوابة مطلوب.")
            // The 2-character MINIMUM stays in the service (it answers with the
            // more specific GATE_INVALID code the CP + tests key off); this
            // validator owns the "required + max length" half of the triple-lock.
            .MaximumLength(16).Bilingual(
                "Gate code must be at most 16 characters.",
                "يجب ألا يتجاوز رمز البوابة 16 حرفاً.");

        RuleFor(request => request.Name)
            .NotEmpty().Bilingual("Name is required.", "الاسم مطلوب.")
            .MaximumLength(128).Bilingual(
                "Name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم 128 حرفاً.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual("Arabic name is required.", "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفاً.");

        RuleFor(request => request.Description)
            .MaximumLength(1024).Bilingual(
                "Description must be at most 1024 characters.",
                "يجب ألا يتجاوز الوصف 1024 حرفاً.");

        RuleFor(request => request.DescriptionArabic)
            .MaximumLength(1024).Bilingual(
                "Arabic description must be at most 1024 characters.",
                "يجب ألا يتجاوز الوصف بالعربية 1024 حرفاً.");

        RuleFor(request => request.AllowedProfileTypeIds)
            .NotNull().Bilingual(
                "The allowed profile type list must be present (it may be empty).",
                "قائمة أنواع الملفات المسموح بها مطلوبة (ويمكن أن تكون فارغة).");

        RuleFor(request => request.AssignedOperatorUserIds)
            .NotNull().Bilingual(
                "The assigned operator list must be present (it may be empty).",
                "قائمة المشغّلين المعيّنين مطلوبة (ويمكن أن تكون فارغة).");
    }
}
