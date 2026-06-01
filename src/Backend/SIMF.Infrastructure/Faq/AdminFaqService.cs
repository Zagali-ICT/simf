// Tests: SIMF.Api.Tests/FaqTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Faq.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Faq;
using SIMF.Domain.Faq;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Faq;

/// <summary>P2.1 (D-211) — admin CRUD over the two-level FAQ. Mirrors
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

    public async Task<GridPage<AdminFaqGroupSummary>> ListGroupsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.FaqGroups.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(g =>
                EF.Functions.Like(g.NameEn, $"%{term}%")
                || EF.Functions.Like(g.NameAr, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(g => g.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nameen", true) => rows.OrderByDescending(g => g.NameEn),
            ("nameen", false) => rows.OrderBy(g => g.NameEn),
            ("displayorder", true) => rows.OrderByDescending(g => g.DisplayOrder),
            _ => rows.OrderBy(g => g.DisplayOrder).ThenBy(g => g.NameEn),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(g => new AdminFaqGroupSummary(
                g.Id,
                g.NameEn,
                g.NameAr,
                g.DisplayOrder,
                g.IsActive,
                g.Entries.Count(e => e.IsActive),
                g.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminFaqGroupSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminFaqGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.FaqGroups
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new AdminFaqGroupSummary(
                g.Id, g.NameEn, g.NameAr, g.DisplayOrder, g.IsActive,
                g.Entries.Count(e => e.IsActive), g.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminFaqGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateFaqGroupRequest request, CancellationToken cancellationToken = default)
    {
        var nameEn = RequireText(request.NameEn, NameMaxLength, "English name", "الاسم الإنجليزي");
        var nameAr = RequireText(request.NameAr, NameMaxLength, "Arabic name", "الاسم العربي");
        RequireNonNegative(request.DisplayOrder);

        var now = timeProvider.GetUtcNow();
        var group = new FaqGroup
        {
            Id = Guid.NewGuid(),
            NameEn = nameEn,
            NameAr = nameAr,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.FaqGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqGroupCreated, actorUserId,
            $"id={group.Id}; nameEn={group.NameEn}", cancellationToken);
        logger.LogInformation("Admin {ActorId} created FAQ group {Name} ({Id})",
            actorUserId, group.NameEn, group.Id);

        return ToGroupSummary(group, entryCount: 0);
    }

    public async Task<AdminFaqGroupSummary> UpdateGroupAsync(
        Guid actorUserId, Guid id, UpdateFaqGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.FaqGroups
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw GroupNotFound();

        group.NameEn = RequireText(request.NameEn, NameMaxLength, "English name", "الاسم الإنجليزي");
        group.NameAr = RequireText(request.NameAr, NameMaxLength, "Arabic name", "الاسم العربي");
        RequireNonNegative(request.DisplayOrder);
        group.DisplayOrder = request.DisplayOrder;
        group.IsActive = request.IsActive;
        group.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqGroupUpdated, actorUserId,
            $"id={group.Id}; nameEn={group.NameEn}; active={group.IsActive}", cancellationToken);

        var entryCount = await dbContext.FaqEntries
            .CountAsync(e => e.FaqGroupId == id && e.IsActive, cancellationToken);
        return ToGroupSummary(group, entryCount);
    }

    public async Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.FaqGroups
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw GroupNotFound();
        if (!group.IsActive) { return; } // idempotent

        group.IsActive = false;
        group.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqGroupDeactivated, actorUserId,
            $"id={group.Id}; nameEn={group.NameEn}", cancellationToken);
    }

    // -- Entries --------------------------------------------------------------

    public async Task<GridPage<AdminFaqEntrySummary>> ListEntriesAsync(
        Guid groupId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 200);

        var rows = dbContext.FaqEntries.AsNoTracking()
            .Where(e => e.FaqGroupId == groupId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(e =>
                EF.Functions.Like(e.QuestionEn, $"%{term}%")
                || EF.Functions.Like(e.QuestionAr, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(e => e.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("questionen", true) => rows.OrderByDescending(e => e.QuestionEn),
            ("questionen", false) => rows.OrderBy(e => e.QuestionEn),
            ("displayorder", true) => rows.OrderByDescending(e => e.DisplayOrder),
            _ => rows.OrderBy(e => e.DisplayOrder).ThenBy(e => e.QuestionEn),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(ToEntrySummaryExpr)
            .ToListAsync(cancellationToken);

        return GridPage<AdminFaqEntrySummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminFaqEntrySummary?> GetEntryAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.FaqEntries
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(ToEntrySummaryExpr)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminFaqEntrySummary> CreateEntryAsync(
        Guid actorUserId, CreateFaqEntryRequest request, CancellationToken cancellationToken = default)
    {
        var groupExists = await dbContext.FaqGroups
            .AnyAsync(g => g.Id == request.FaqGroupId, cancellationToken);
        if (!groupExists) { throw GroupNotFound(); }

        var entry = new FaqEntry
        {
            Id = Guid.NewGuid(),
            FaqGroupId = request.FaqGroupId,
            QuestionEn = RequireText(request.QuestionEn, QuestionMaxLength, "English question", "السؤال الإنجليزي"),
            QuestionAr = RequireText(request.QuestionAr, QuestionMaxLength, "Arabic question", "السؤال العربي"),
            AnswerEn = RequireText(request.AnswerEn, AnswerMaxLength, "English answer", "الإجابة الإنجليزية"),
            AnswerAr = RequireText(request.AnswerAr, AnswerMaxLength, "Arabic answer", "الإجابة العربية"),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        RequireNonNegative(request.DisplayOrder);
        dbContext.FaqEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqEntryCreated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}", cancellationToken);

        return ToEntrySummary(entry);
    }

    public async Task<AdminFaqEntrySummary> UpdateEntryAsync(
        Guid actorUserId, Guid id, UpdateFaqEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.FaqEntries
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw EntryNotFound();

        entry.QuestionEn = RequireText(request.QuestionEn, QuestionMaxLength, "English question", "السؤال الإنجليزي");
        entry.QuestionAr = RequireText(request.QuestionAr, QuestionMaxLength, "Arabic question", "السؤال العربي");
        entry.AnswerEn = RequireText(request.AnswerEn, AnswerMaxLength, "English answer", "الإجابة الإنجليزية");
        entry.AnswerAr = RequireText(request.AnswerAr, AnswerMaxLength, "Arabic answer", "الإجابة العربية");
        RequireNonNegative(request.DisplayOrder);
        entry.DisplayOrder = request.DisplayOrder;
        entry.IsActive = request.IsActive;
        entry.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqEntryUpdated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}; active={entry.IsActive}", cancellationToken);

        return ToEntrySummary(entry);
    }

    public async Task DeactivateEntryAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.FaqEntries
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw EntryNotFound();
        if (!entry.IsActive) { return; } // idempotent

        entry.IsActive = false;
        entry.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(AuditEvents.FaqEntryDeactivated, actorUserId,
            $"id={entry.Id}; groupId={entry.FaqGroupId}", cancellationToken);
    }

    // -- internals ------------------------------------------------------------

    private Task WriteAuditAsync(
        string eventType, Guid actorUserId, string detail, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = detail,
        }, cancellationToken);

    private static AdminFaqGroupSummary ToGroupSummary(FaqGroup g, int entryCount) =>
        new(g.Id, g.NameEn, g.NameAr, g.DisplayOrder, g.IsActive, entryCount, g.CreatedAt);

    private static AdminFaqEntrySummary ToEntrySummary(FaqEntry e) =>
        new(e.Id, e.FaqGroupId, e.QuestionEn, e.QuestionAr, e.AnswerEn, e.AnswerAr,
            e.DisplayOrder, e.IsActive, e.CreatedAt);

    private static System.Linq.Expressions.Expression<Func<FaqEntry, AdminFaqEntrySummary>> ToEntrySummaryExpr =>
        e => new AdminFaqEntrySummary(
            e.Id, e.FaqGroupId, e.QuestionEn, e.QuestionAr, e.AnswerEn, e.AnswerAr,
            e.DisplayOrder, e.IsActive, e.CreatedAt);

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
