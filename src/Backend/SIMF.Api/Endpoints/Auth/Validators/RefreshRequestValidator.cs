using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the refresh request.</summary>
public sealed class RefreshRequestValidator : Validator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().Bilingual(
                "The refresh token is required.",
                "رمز التحديث مطلوب.");
    }
}
