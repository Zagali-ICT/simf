using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Archive;
using SIMF.Contracts.Archive;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>D-199 — validators for admin archive-edition CRUD. Lengths +
/// ranges mirror the EF configuration (<c>ArchiveEditionConfiguration</c>) and
/// the service-side <c>AdminArchiveService.Validate</c> exactly.
///
/// FastEndpoints binds a validator to the endpoint's request type. The create
/// endpoint's request type is <see cref="CreateArchiveEditionRequest"/>; the
/// update endpoint's request type is the derived route DTO
/// <see cref="UpdateArchiveEditionRoute"/> (id from the path + the update
/// body), so the update validator targets that derived type — a validator on
/// the base <c>UpdateArchiveEditionRequest</c> would not be auto-discovered for
/// the route type. Year uniqueness is a cross-row check and stays in the
/// service (it cannot be expressed as a stateless field rule).</summary>
public sealed class CreateArchiveEditionRequestValidator
    : Validator<CreateArchiveEditionRequest>
{
    public CreateArchiveEditionRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SummaryEn).MaximumLength(1024);
        RuleFor(x => x.SummaryAr).MaximumLength(1024);
        RuleFor(x => x.Attendees).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sessions).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Speakers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CoverImageRelativePath).MaximumLength(512);
    }
}

public sealed class UpdateArchiveEditionRequestValidator
    : Validator<UpdateArchiveEditionRoute>
{
    public UpdateArchiveEditionRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SummaryEn).MaximumLength(1024);
        RuleFor(x => x.SummaryAr).MaximumLength(1024);
        RuleFor(x => x.Attendees).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sessions).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Speakers).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CoverImageRelativePath).MaximumLength(512);
    }
}
