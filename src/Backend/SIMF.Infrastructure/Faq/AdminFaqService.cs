// Tests: SIMF.Api.Tests/FaqTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Faq.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Faq;
using SIMF.Domain.Faq;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Faq;

/// <summary>Admin CRUD over the two-level FAQ. Mirrors
/// <see cref="SIMF.Infrastructure.PublicRelations.AdminNewsService"/>: built on
/// <see cref="SimfAppDbContext"/>, one audit row per mutation, timestamps via
/// <see cref="TimeProvider"/>, soft-delete through <c>IsActive</c>. The admin
/// lists return every row (incl. soft-deleted) so editors can manage them.</summary>
internal sealed class AdminFaqService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminFaqService> logger) : IAdminFaqService
{
    private const int NameMaxLength = 128;
    private const int QuestionMaxLength = 512;
    private const int AnswerMaxLength = 4000;

    // -- Groups ---------------------------------------------------------------

    /// <summary>
    /// The grid contract for the FAQ group list: one entry per key the groups grid
    /// on FaqManager.razor can send, as both its filter and its sort. A key not
    /// declared here is a 400, not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<FaqGroup> GroupColumns = new GridColumns<FaqGroup>()
        .Add("nameEn", group => group.Name, searchable: true)
        .Add("nameAr", group => group.NameArabic, searchable: true)
        .Add("displayOrder", group => group.DisplayOrder)
        .Add("isActive", group => group.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("nameEn")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<FaqGroup, AdminFaqGroupSummary>> ToGroupRow =
        group => new AdminFaqGroupSummary(
            group.Id,
            group.Name,
            group.NameArabic,
            group.DisplayOrder,
            group.IsActive,
            group.Entries.Count(entry => entry.IsActive),
            group.CreatedAt);

    public Task<GridPage<AdminFaqGroupSummary>> ListGroupsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.FaqGroups.ToGridPageAsync(
            query, GroupColumns, group => group.Id, ToGroupRow, cancellationToken);

    public async Task<AdminFaqGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.FaqGroups
            .AsNoTracking()
            .Where(group => group.Id == id)
            .Select(ToGroupRow)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminFaqGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateFaqGroupRequest request, CancellationToken cancellationToken = default)
    {
        var nameEn = RequireText(request.NameEn, NameMaxLength, "English name", "الاسم الإنجليزي");
        var nameAr = RequireText(request.NameAr, NameMaxLength, "Arabic name", "الاسم العربي");
        RequireNonNegative(request.DisplayOrder);

        var now = timeProvider.SimfNow();
        var group = new FaqGroup
        {
            Id = Guid.NewGuid(),
            Name = nameEn,
            NameArabic = nameAr,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.FaqGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqGroupCreated, actorUserId,
            $"id={group.Id}; nameEn={group.Name}", cancellationToken);
        logger.LogInformation("Admin {ActorId} created FAQ group {Name} ({Id})",
            actorUserId, group.Name, group.Id);

        return ToGroupSummary(group, entryCount: 0);
    }

    public async Task<AdminFaqGroupSummary> UpdateGroupAsync(
        Guid actorUserId, Guid id, UpdateFaqGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.FaqGroups
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw GroupNotFound();

        group.Name = RequireText(request.NameEn, NameMaxLength, "English name", "الاسم الإنجليزي");
        group.NameArabic = RequireText(request.NameAr, NameMaxLength, "Arabic name", "الاسم العربي");
        RequireNonNegative(request.DisplayOrder);
        group.DisplayOrder = request.DisplayOrder;
        group.IsActive = request.IsActive;
        group.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqGroupUpdated, actorUserId,
            $"id={group.Id}; nameEn={group.Name}; active={group.IsActive}", cancellationToken);

        var entryCount = await dbContext.FaqEntries
            .CountAsync(entry => entry.FaqGroupId == id && entry.IsActive, cancellationToken);
        return ToGroupSummary(group, entryCount);
    }

    public async Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.FaqGroups
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw GroupNotFound();
        if (!group.IsActive) { return; } // idempotent

        group.IsActive = false;
        group.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqGroupDeactivated, actorUserId,
            $"id={group.Id}; nameEn={group.Name}", cancellationToken);
    }

    // -- Entries --------------------------------------------------------------

    /// <summary>
    /// The grid contract for the FAQ entry list: one entry per key the entries grid
    /// on FaqManager.razor can send. The owning group is a scope predicate applied
    /// before the grid composes, not a declared filter.
    /// </summary>
    private static readonly GridColumns<FaqEntry> EntryColumns = new GridColumns<FaqEntry>()
        .Add("question", entry => entry.Question, searchable: true)
        .Add("questionAr", entry => entry.QuestionArabic, searchable: true)
        .Add("displayOrder", entry => entry.DisplayOrder)
        .Add("isActive", entry => entry.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("question")
        .PageSize(fallback: 50, max: 200);

    private static readonly Expression<Func<FaqEntry, AdminFaqEntrySummary>> ToEntryRow =
        entry => new AdminFaqEntrySummary(
            entry.Id, entry.FaqGroupId, entry.Question, entry.QuestionArabic,
            entry.Answer, entry.AnswerArabic,
            entry.DisplayOrder, entry.IsActive, entry.CreatedAt);

    public Task<GridPage<AdminFaqEntrySummary>> ListEntriesAsync(
        Guid groupId, GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.FaqEntries
            .Where(entry => entry.FaqGroupId == groupId)
            .ToGridPageAsync(
                query, EntryColumns, entry => entry.Id, ToEntryRow, cancellationToken);

    public async Task<AdminFaqEntrySummary?> GetEntryAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.FaqEntries
            .AsNoTracking()
            .Where(entry => entry.Id == id)
            .Select(ToEntryRow)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminFaqEntrySummary> CreateEntryAsync(
        Guid actorUserId, CreateFaqEntryRequest request, CancellationToken cancellationToken = default)
    {
        var groupExists = await dbContext.FaqGroups
            .AnyAsync(group => group.Id == request.FaqGroupId, cancellationToken);
        if (!groupExists) { throw GroupNotFound(); }

        var entry = new FaqEntry
        {
            Id = Guid.NewGuid(),
            FaqGroupId = request.FaqGroupId,
            Question = RequireText(request.Question, QuestionMaxLength, "English question", "السؤال الإنجليزي"),
            QuestionArabic = RequireText(request.QuestionArabic, QuestionMaxLength, "Arabic question", "السؤال العربي"),
            Answer = RequireText(request.Answer, AnswerMaxLength, "English answer", "الإجابة الإنجليزية"),
            AnswerArabic = RequireText(request.AnswerArabic, AnswerMaxLength, "Arabic answer", "الإجابة العربية"),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        RequireNonNegative(request.DisplayOrder);
        dbContext.FaqEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqEntryCreated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}", cancellationToken);

        return ToEntrySummary(entry);
    }

    public async Task<AdminFaqEntrySummary> UpdateEntryAsync(
        Guid actorUserId, Guid id, UpdateFaqEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.FaqEntries
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw EntryNotFound();

        entry.Question = RequireText(request.Question, QuestionMaxLength, "English question", "السؤال الإنجليزي");
        entry.QuestionArabic = RequireText(request.QuestionArabic, QuestionMaxLength, "Arabic question", "السؤال العربي");
        entry.Answer = RequireText(request.Answer, AnswerMaxLength, "English answer", "الإجابة الإنجليزية");
        entry.AnswerArabic = RequireText(request.AnswerArabic, AnswerMaxLength, "Arabic answer", "الإجابة العربية");
        RequireNonNegative(request.DisplayOrder);
        entry.DisplayOrder = request.DisplayOrder;
        entry.IsActive = request.IsActive;
        entry.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqEntryUpdated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}; active={entry.IsActive}", cancellationToken);

        return ToEntrySummary(entry);
    }

    public async Task DeactivateEntryAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.FaqEntries
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw EntryNotFound();
        if (!entry.IsActive) { return; } // idempotent

        entry.IsActive = false;
        entry.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.FaqEntryDeactivated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}", cancellationToken);
    }

    // -- internals ------------------------------------------------------------

    private static AdminFaqGroupSummary ToGroupSummary(FaqGroup group, int entryCount) =>
        new(group.Id, group.Name, group.NameArabic, group.DisplayOrder, group.IsActive,
            entryCount, group.CreatedAt);

    private static AdminFaqEntrySummary ToEntrySummary(FaqEntry entry) =>
        new(entry.Id, entry.FaqGroupId, entry.Question, entry.QuestionArabic,
            entry.Answer, entry.AnswerArabic,
            entry.DisplayOrder, entry.IsActive, entry.CreatedAt);

    private static ApiException GroupNotFound() => new(
        ErrorCodes.FaqGroupNotFound, 404,
        "The FAQ group was not found.", "لم يتم العثور على مجموعة الأسئلة.");

    private static ApiException EntryNotFound() => new(
        ErrorCodes.FaqEntryNotFound, 404,
        "The FAQ entry was not found.", "لم يتم العثور على السؤال.");

    private static void RequireNonNegative(int displayOrder)
    {
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.FaqInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
    }

    private static string RequireText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length < 1)
        {
            throw new ApiException(
                ErrorCodes.FaqInvalid, 400,
                $"FAQ {fieldEn} is required.",
                $"{fieldAr} مطلوب.");
        }
        if (value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.FaqInvalid, 400,
                $"FAQ {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }
}
