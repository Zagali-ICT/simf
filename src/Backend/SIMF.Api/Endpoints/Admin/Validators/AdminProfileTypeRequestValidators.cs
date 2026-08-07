using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates <see cref="AdminCreateProfileTypeRequest"/>. The
/// UserType value is checked at the service layer (more precise error
/// code there); this validator only enforces field-shape rules whose
/// limits mirror the EF column maxes on <c>ProfileType</c>.
/// </summary>
public sealed class AdminCreateProfileTypeRequestValidator
    : Validator<AdminCreateProfileTypeRequest>
{
    public AdminCreateProfileTypeRequestValidator()
    {
        RuleFor(request => request.UserType)
            .NotEmpty().Bilingual(
                "User type is required.",
                "نوع المستخدم مطلوب.")
            .MaximumLength(16);

        RuleFor(request => request.Name)
            .NotEmpty().Bilingual("Name is required.", "الاسم مطلوب.")
            .MaximumLength(128).Bilingual(
                "Name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم 128 حرفًا.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual("Arabic name is required.", "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفًا.");

        RuleFor(request => request.PageColor)
            .NotEmpty().Bilingual("Colour is required.", "اللون مطلوب.")
            .MaximumLength(32).Bilingual(
                "Colour must be at most 32 characters.",
                "يجب ألا يتجاوز اللون 32 حرفًا.");
    }
}

/// <summary>Validates <see cref="AdminUpdateProfileTypeRequest"/>.
/// Same length rules as the create validator minus UserType.</summary>
public sealed class AdminUpdateProfileTypeRequestValidator
    : Validator<AdminUpdateProfileTypeRequest>
{
    public AdminUpdateProfileTypeRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().Bilingual("Name is required.", "الاسم مطلوب.")
            .MaximumLength(128).Bilingual(
                "Name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم 128 حرفًا.");

        RuleFor(request => request.NameArabic)
            .NotEmpty().Bilingual("Arabic name is required.", "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفًا.");

        RuleFor(request => request.PageColor)
            .NotEmpty().Bilingual("Colour is required.", "اللون مطلوب.")
            .MaximumLength(32).Bilingual(
                "Colour must be at most 32 characters.",
                "يجب ألا يتجاوز اللون 32 حرفًا.");
    }
}
