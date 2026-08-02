// Tests: SIMF.Api.Tests/ContactInquiryTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Support.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Support;
using SIMF.Domain.Support;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Support;

/// <summary>Contact-us inquiries (Figma 1388:7567): the public submit + the CP
/// inbox read/handled toggle. Built on <see cref="SimfAppDbContext"/>; timestamps
/// via the shared <see cref="TimeProvider"/>. Server-side length guards mirror
/// the FastEndpoints validator (defense in depth).</summary>
internal sealed class ContactInquiryService(
    SimfAppDbContext dbContext,
    TimeProvider timeProvider) : IContactInquiryService
{
    private const int NameMaxLength = 120;
    private const int EmailMaxLength = 256;
    private const int MessageMaxLength = 4000;

    public async Task<Guid> SubmitAsync(
        SubmitContactInquiryRequest request,
        Guid? submittedByUserId,
        CancellationToken cancellationToken = default)
    {
        var inquiry = new ContactInquiry
        {
            Id = Guid.NewGuid(),
            Name = RequireText(request.Name, NameMaxLength, "name", "الاسم"),
            Email = RequireText(request.Email, EmailMaxLength, "email", "البريد الإلكتروني"),
            Message = RequireText(request.Message, MessageMaxLength, "message", "الرسالة"),
            SubmittedByUserId = submittedByUserId,
            IsHandled = false,
            CreatedAt = timeProvider.SimfNow(),
        };
        dbContext.ContactInquiries.Add(inquiry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return inquiry.Id;
    }

    public async Task<GridPage<AdminContactInquiryRow>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.ContactInquiries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(c =>
                EF.Functions.Like(c.Name, $"%{term}%")
                || EF.Functions.Like(c.Email, $"%{term}%")
                || EF.Functions.Like(c.Message, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isHandled", out var handledFilter)
            && bool.TryParse(handledFilter, out var isHandled))
        {
            rows = rows.Where(c => c.IsHandled == isHandled);
        }

        // Default ordering: open inquiries first, then newest first.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("createdat", true) => rows.OrderByDescending(c => c.CreatedAt),
            ("createdat", false) => rows.OrderBy(c => c.CreatedAt),
            ("name", true) => rows.OrderByDescending(c => c.Name),
            ("name", false) => rows.OrderBy(c => c.Name),
            _ => rows.OrderBy(c => c.IsHandled).ThenByDescending(c => c.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(c => new AdminContactInquiryRow(
                c.Id, c.Name, c.Email, c.Message, c.IsHandled,
                c.SubmittedByUserId, c.CreatedAt, c.HandledAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminContactInquiryRow>.Of(page, total,
            skip, top);
    }

    public async Task MarkHandledAsync(
        Guid actorUserId, Guid id, bool handled,
        CancellationToken cancellationToken = default)
    {
        var inquiry = await dbContext.ContactInquiries
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "The contact inquiry was not found.",
                "لم يتم العثور على الرسالة.");
        if (inquiry.IsHandled == handled) { return; } // idempotent

        var now = timeProvider.SimfNow();
        inquiry.IsHandled = handled;
        inquiry.HandledAt = handled ? now : null;
        inquiry.HandledByUserId = handled ? actorUserId : null;
        inquiry.UpdatedAt = now;
        inquiry.UpdatedBy = actorUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string RequireText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length < 1)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"The {fieldEn} is required.", $"{fieldAr} مطلوب.");
        }
        if (value.Length > maxLength)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"The {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }
}
