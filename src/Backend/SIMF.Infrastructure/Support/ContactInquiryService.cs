// Tests: SIMF.Api.Tests/ContactInquiryTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Support.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Support;
using SIMF.Domain.Support;
using SIMF.Infrastructure.Common.Grids;
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

    /// <summary>The grid contract for /admin/contact-inquiries: one entry per key
    /// ContactInquiriesList.razor can send, as both its filter and its sort.</summary>
    private static readonly GridColumns<ContactInquiry> Columns = new GridColumns<ContactInquiry>()
        .Add("name", inquiry => inquiry.Name, searchable: true)
        .Add("email", inquiry => inquiry.Email, searchable: true)
        .Add("message", inquiry => inquiry.Message, searchable: true)
        .Add("isHandled", inquiry => inquiry.IsHandled)
        .Add("createdAt", inquiry => inquiry.CreatedAt)
        // Open inquiries first — false sorts before true — then newest first.
        .DefaultOrder("isHandled")
        .DefaultOrder("createdAt", descending: true)
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<ContactInquiry, AdminContactInquiryRow>> ToRow =
        inquiry => new AdminContactInquiryRow(
            inquiry.Id, inquiry.Name, inquiry.Email, inquiry.Message, inquiry.IsHandled,
            inquiry.SubmittedByUserId, inquiry.CreatedAt, inquiry.HandledAt);

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

    public Task<GridPage<AdminContactInquiryRow>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.ContactInquiries.ToGridPageAsync(
            query, Columns, inquiry => inquiry.Id, ToRow, cancellationToken);

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
