// Tests: SIMF.Api.Tests/EmailSubjectRendererTests.cs (subject rendering),
//        SIMF.Api.Tests/EmailTemplateAdminTests.cs (override resolution)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Email;

/// <summary>Resolves a transactional email from the active DB override
/// when one exists, else the code-owned <see cref="EmailTemplateCatalog"/>
/// default. Never throws: any lookup failure logs a warning and uses the
/// default, so the email is always built. The template read is a plain,
/// standalone query on the App context (a "resolve on read") — it does
/// not join or share a transaction with the Identity write that produced the
/// code.</summary>
internal sealed class EmailTemplateResolver(
    SimfAppDbContext appDbContext,
    ILogger<EmailTemplateResolver> logger) : IEmailTemplateResolver
{
    public async Task<EmailMessage> RenderAsync(
        EmailTemplateType type,
        string to,
        IReadOnlyDictionary<string, string> tokens,
        CancellationToken cancellationToken = default)
    {
        var definition = EmailTemplateCatalog.DefaultOrFallback(type);
        var subject = definition.Subject;
        var bodyEn = definition.BodyEn;
        var bodyAr = definition.BodyAr;

        try
        {
            var row = await appDbContext.EmailTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Type == type && t.IsActive, cancellationToken);

            if (row is not null)
            {
                subject = row.Subject;
                bodyEn = row.BodyEn;
                bodyAr = row.BodyAr;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email template lookup failed for {TemplateType}; using the built-in default.",
                type);
        }

        var renderedBody = EmailTemplateRenderer.ComposeBody(
            EmailTemplateRenderer.Render(bodyEn, tokens),
            EmailTemplateRenderer.Render(bodyAr, tokens));

        // The bodies are HTML and get the encoding renderer; the subject is a MIME
        // header and gets the plain one, or a name with an apostrophe arrives as
        // "Sa&#39;ad".
        return new EmailMessage(
            to, EmailSubjectRenderer.Render(subject, tokens), renderedBody);
    }
}
