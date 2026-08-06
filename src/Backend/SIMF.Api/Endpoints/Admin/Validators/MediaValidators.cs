using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Media;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>FluentValidation for media create/update. Lengths MUST
/// match the EF <c>HasMaxLength</c> in <c>MediaItemConfiguration</c> and the
/// service-side guards in <c>AdminMediaService</c>.
/// All title/album fields are optional, so they are length-only.</summary>
public sealed class CreateMediaRequestValidator : Validator<AdminCreateMediaRequest>
{
    public CreateMediaRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200)
            .WithMessage("English title must be 200 characters or less.");
        RuleFor(x => x.TitleArabic).MaximumLength(200)
            .WithMessage("Arabic title must be 200 characters or less.");
        RuleFor(x => x.Album).MaximumLength(200)
            .WithMessage("English album must be 200 characters or less.");
        RuleFor(x => x.AlbumArabic).MaximumLength(200)
            .WithMessage("Arabic album must be 200 characters or less.");
        RuleFor(x => x.Url).MaximumLength(2048)
            .WithMessage("URL must be 2048 characters or less.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0)
            .WithMessage("Display order must be zero or positive.");
    }
}

public sealed class UpdateMediaRequestValidator : Validator<UpdateMediaRoute>
{
    public UpdateMediaRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200)
            .WithMessage("English title must be 200 characters or less.");
        RuleFor(x => x.TitleArabic).MaximumLength(200)
            .WithMessage("Arabic title must be 200 characters or less.");
        RuleFor(x => x.Album).MaximumLength(200)
            .WithMessage("English album must be 200 characters or less.");
        RuleFor(x => x.AlbumArabic).MaximumLength(200)
            .WithMessage("Arabic album must be 200 characters or less.");
        RuleFor(x => x.Url).MaximumLength(2048)
            .WithMessage("URL must be 2048 characters or less.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0)
            .WithMessage("Display order must be zero or positive.");
    }
}
