using FluentValidation;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>
/// Bilingual-message helpers for FluentValidation rules. The English
/// message is attached the standard way with <c>WithMessage</c>; the Arabic
/// message is attached as the rule's <c>CustomState</c> so the response
/// builder in <c>Program.cs</c> can pair them on the way out.
/// </summary>
internal static class BilingualValidation
{
    /// <summary>
    /// Attaches an English message and an Arabic message to the immediately
    /// preceding validator.
    /// </summary>
    public static IRuleBuilderOptions<T, TProp> Bilingual<T, TProp>(
        this IRuleBuilderOptions<T, TProp> rule,
        string english,
        string arabic) =>
        rule.WithMessage(english).WithState(_ => arabic);
}
