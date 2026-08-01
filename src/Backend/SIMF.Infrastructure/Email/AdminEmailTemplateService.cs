using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Email;
using SIMF.Domain.Auditing;
using SIMF.Domain.Email;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Email;

/// <summary>D-735 — CP admin service for the transactional email templates. The
/// catalogue owns the fixed set + defaults; the DB owns only admin overrides.
/// Save validates that the copy references no unknown token, upserts the override
/// row and bumps its version; reset deletes the override (reverting to the code
/// default).</summary>
internal sealed class AdminEmailTemplateService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminEmailTemplateService
{
    // Bounds the stored copy so an Edit admin cannot plant a multi-MB body that
    // is read + allocated on every OTP/reset render (D-735 security review).
    // Subject aligns with the EF nvarchar(256) column; bodies stay nvarchar(max)
    // in EF but are capped here + in the CP textarea maxlength.
    private const int MaxSubjectLength = 256;
    private const int MaxBodyLength = 8000;

    public async Task<GridPage<AdminEmailTemplateSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var overrides = await appDbContext.EmailTemplates
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Type, cancellationToken);

        var rows = EmailTemplateCatalog.All
            .Select(def =>
            {
                var row = overrides.GetValueOrDefault(def.Type);
                return new AdminEmailTemplateSummary(
                    def.Type,
                    def.Type.ToString(),
                    row?.Subject ?? def.Subject,
                    row is not null,
                    row?.Version ?? 0,
                    row?.UpdatedAt);
            })
            .ToList();

        return GridPage<AdminEmailTemplateSummary>.Of(rows, rows.Count, query);
    }

    public async Task<AdminEmailTemplateDetail> GetAsync(
        EmailTemplateType type, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.EmailTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);
        return ToDetail(type, row);
    }

    public async Task<AdminEmailTemplateDetail> UpdateAsync(
        Guid actorUserId,
        EmailTemplateType type,
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var subject = (request.Subject ?? string.Empty).Trim();
        var bodyEn = (request.BodyEn ?? string.Empty).Trim();
        var bodyAr = (request.BodyAr ?? string.Empty).Trim();

        if (subject.Length == 0 || bodyEn.Length == 0 || bodyAr.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                "Subject and both language bodies are required.",
                "العنوان ونص الرسالة بكلتا اللغتين مطلوبة.");
        }

        if (subject.Length > MaxSubjectLength
            || bodyEn.Length > MaxBodyLength
            || bodyAr.Length > MaxBodyLength)
        {
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                $"The subject must be at most {MaxSubjectLength} characters and each body at most {MaxBodyLength}.",
                $"يجب ألا يتجاوز العنوان {MaxSubjectLength} حرفًا وكل نص {MaxBodyLength} حرفًا.");
        }

        var unknown = UnknownTokens(type, subject, bodyEn, bodyAr);
        if (unknown.Count > 0)
        {
            var names = string.Join(", ", unknown.Select(t => "{" + t + "}"));
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                $"The template references unknown placeholders: {names}.",
                $"يشير القالب إلى عناصر نائبة غير معروفة: {names}.");
        }

        var now = timeProvider.SimfNow();
        var row = await appDbContext.EmailTemplates
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);

        if (row is null)
        {
            row = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                Type = type,
                Subject = subject,
                BodyEn = bodyEn,
                BodyAr = bodyAr,
                IsActive = request.IsActive,
                Version = 1,
                CreatedAt = now,
                UpdatedByUserId = actorUserId,
            };
            appDbContext.EmailTemplates.Add(row);
        }
        else
        {
            row.Subject = subject;
            row.BodyEn = bodyEn;
            row.BodyAr = bodyAr;
            row.IsActive = request.IsActive;
            row.Version++;
            row.UpdatedAt = now;
            row.UpdatedByUserId = actorUserId;
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.EmailTemplateUpdated,
                Outcome = AuditOutcome.Success,
                ActorUserId = actorUserId,
                Detail = JsonSerializer.Serialize(new
                {
                    type = type.ToString(),
                    version = row.Version,
                }),
            },
            cancellationToken);

        return ToDetail(type, row);
    }

    public async Task<AdminEmailTemplateDetail> ResetAsync(
        Guid actorUserId,
        EmailTemplateType type,
        CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.EmailTemplates
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);

        if (row is not null)
        {
            appDbContext.EmailTemplates.Remove(row);
            await appDbContext.SaveChangesAsync(cancellationToken);

            await auditLog.WriteAsync(
                new AuditEntry
                {
                    EventType = AuditEvents.EmailTemplateReset,
                    Outcome = AuditOutcome.Success,
                    ActorUserId = actorUserId,
                    Detail = JsonSerializer.Serialize(new { type = type.ToString() }),
                },
                cancellationToken);
        }

        return ToDetail(type, null);
    }

    public EmailTemplatePreviewResult Preview(
        EmailTemplateType type, PreviewEmailTemplateRequest request)
    {
        var subject = request.Subject ?? string.Empty;
        var bodyEn = request.BodyEn ?? string.Empty;
        var bodyAr = request.BodyAr ?? string.Empty;

        var samples = EmailTemplateCatalog.Default(type).Tokens
            .ToDictionary(t => t.Name, t => t.Sample, StringComparer.OrdinalIgnoreCase);

        var html = EmailTemplateRenderer.ComposeBody(
            EmailTemplateRenderer.Render(bodyEn, samples),
            EmailTemplateRenderer.Render(bodyAr, samples));

        return new EmailTemplatePreviewResult(
            EmailTemplateRenderer.Render(subject, samples),
            html,
            UnknownTokens(type, subject, bodyEn, bodyAr));
    }

    private static IReadOnlyList<string> UnknownTokens(
        EmailTemplateType type, string subject, string bodyEn, string bodyAr)
    {
        var known = EmailTemplateCatalog.KnownTokenNames(type);
        return EmailTemplateRenderer.FindUnknownTokens(subject, known)
            .Concat(EmailTemplateRenderer.FindUnknownTokens(bodyEn, known))
            .Concat(EmailTemplateRenderer.FindUnknownTokens(bodyAr, known))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdminEmailTemplateDetail ToDetail(EmailTemplateType type, EmailTemplate? row)
    {
        var def = EmailTemplateCatalog.Default(type);
        var tokens = def.Tokens
            .Select(t => new EmailTemplateTokenDto(t.Name, t.DisplayEn, t.DisplayAr, t.Sample))
            .ToList();

        return new AdminEmailTemplateDetail(
            type,
            type.ToString(),
            row?.Subject ?? def.Subject,
            row?.BodyEn ?? def.BodyEn,
            row?.BodyAr ?? def.BodyAr,
            row?.IsActive ?? true,
            row is not null,
            row?.Version ?? 0,
            def.Subject,
            def.BodyEn,
            def.BodyAr,
            tokens,
            row?.UpdatedAt);
    }
}
