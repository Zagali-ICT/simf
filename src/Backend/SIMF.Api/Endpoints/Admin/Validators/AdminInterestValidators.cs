using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>Validates the admin-create-interest request.</summary>
public sealed class AdminCreateInterestRequestValidator
    : Validator<AdminCreateInterestRequest>
{
    public AdminCreateInterestRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().Bilingual(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(128).Bilingual(
                "The English name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالإنجليزية 128 حرفًا.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "The Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفًا.");

        RuleFor(request => request.DisplayOrder)
            .GreaterThanOrEqualTo(0).Bilingual(
                "Display order must be zero or greater.",
                "يجب أن يكون ترتيب العرض صفرًا أو أكثر.");
    }
}

/// <summary>Validates the admin-update-interest request.
/// Same shape as the create request plus the soft-delete flag.</summary>
public sealed class AdminUpdateInterestRequestValidator
    : Validator<AdminUpdateInterestRequest>
{
    public AdminUpdateInterestRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().Bilingual(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(128).Bilingual(
                "The English name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالإنجليزية 128 حرفًا.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "The Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفًا.");

        RuleFor(request => request.DisplayOrder)
            .GreaterThanOrEqualTo(0).Bilingual(
                "Display order must be zero or greater.",
                "يجب أن يكون ترتيب العرض صفرًا أو أكثر.");
    }
}
