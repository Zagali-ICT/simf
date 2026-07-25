using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>Shape validation for the admin notification-broadcast request
/// (Announcements desk). Cross-field checks (session required for a session
/// broadcast, audience required + valid for an audience broadcast, session
/// existence) live in <c>NotificationBroadcastService</c> with bilingual
/// messages. Lengths mirror the EF max-lengths on <c>NotificationBroadcast</c>.</summary>
public sealed class AdminCreateBroadcastValidator
    : Validator<AdminCreateBroadcastRequest>
{
    public AdminCreateBroadcastValidator()
    {
        RuleFor(request => request.TargetMode)
            .NotEmpty().Bilingual(
                "A target is required.",
                "الهدف مطلوب.")
            .Must(mode => mode is "Session" or "Audience").Bilingual(
                "Target must be a session or an audience.",
                "يجب أن يكون الهدف جلسة أو جمهوراً.");

        RuleFor(request => request.Title)
            .NotEmpty().Bilingual(
                "The English title is required.",
                "العنوان بالإنجليزية مطلوب.")
            .MaximumLength(200).Bilingual(
                "The English title must be at most 200 characters.",
                "يجب ألا يتجاوز العنوان بالإنجليزية 200 حرف.");

        RuleFor(request => request.TitleArabic)
            .NotEmpty().Bilingual(
                "The Arabic title is required.",
                "العنوان بالعربية مطلوب.")
            .MaximumLength(200).Bilingual(
                "The Arabic title must be at most 200 characters.",
                "يجب ألا يتجاوز العنوان بالعربية 200 حرف.");

        RuleFor(request => request.Body)
            .NotEmpty().Bilingual(
                "The English message is required.",
                "الرسالة بالإنجليزية مطلوبة.")
            .MaximumLength(2000).Bilingual(
                "The English message must be at most 2000 characters.",
                "يجب ألا تتجاوز الرسالة بالإنجليزية 2000 حرف.");

        RuleFor(request => request.BodyArabic)
            .NotEmpty().Bilingual(
                "The Arabic message is required.",
                "الرسالة بالعربية مطلوبة.")
            .MaximumLength(2000).Bilingual(
                "The Arabic message must be at most 2000 characters.",
                "يجب ألا تتجاوز الرسالة بالعربية 2000 حرف.");

        RuleFor(request => request.Severity)
            .Must(severity => severity is "Info" or "Success" or "Warning" or "Error")
            .Bilingual(
                "Importance must be Info, Success, Warning or Error.",
                "يجب أن تكون الأهمية: معلومة أو نجاح أو تحذير أو حرِج.");
    }
}
