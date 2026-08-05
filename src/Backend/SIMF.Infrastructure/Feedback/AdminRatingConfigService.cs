// Tests: SIMF.Api.Tests/RatingConfigTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Feedback;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Feedback;

/// <summary>Admin CRUD over the rating configuration (types → groups → questions).
/// Mirrors <see cref="SIMF.Infrastructure.Faq.AdminFaqService"/>: built on
/// <see cref="SimfAppDbContext"/>, one audit row per mutation, timestamps via
/// <see cref="TimeProvider"/>, soft-delete through <c>IsActive</c>. System types
/// (App / Session) can't be deleted and their <c>Code</c>/<c>Scope</c> are locked.</summary>
internal sealed class AdminRatingConfigService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminRatingConfigService> logger) : IAdminRatingConfigService
{
    private const int CodeMaxLength = 64;
    private const int NameMaxLength = 128;
    private const int LabelMaxLength = 128;
    private const int TextMaxLength = 512;

    // -- Types ----------------------------------------------------------------

    public async Task<GridPage<AdminRatingTypeSummary>> ListTypesAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.RatingTypes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(t =>
                EF.Functions.Like(t.Name, $"%{term}%")
                || EF.Functions.Like(t.NameArabic, $"%{term}%")
                || EF.Functions.Like(t.Code, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(t => t.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(t => t.Name),
            ("name", false) => rows.OrderBy(t => t.Name),
            ("code", true) => rows.OrderByDescending(t => t.Code),
            ("code", false) => rows.OrderBy(t => t.Code),
            _ => rows.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(t => new
            {
                t.Id, t.Code, t.Name, t.NameArabic, t.Scope, t.HasOverallStars,
                t.AllowComment, t.CommentLabel, t.CommentLabelArabic, t.IsSystem,
                t.DisplayOrder, t.IsActive,
                GroupCount = t.Groups.Count(g => g.IsActive),
                QuestionCount = t.Questions.Count(q => q.IsActive),
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // Response counts per type (separate grouped query — RatingResponse has no
        // navigation on RatingType so the counts can't ride the projection above).
        var typeIds = page.Select(t => t.Id).ToList();
        var responseCounts = await dbContext.RatingResponses.AsNoTracking()
            .Where(r => r.IsActive && typeIds.Contains(r.RatingTypeId))
            .GroupBy(r => r.RatingTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TypeId, g => g.Count, cancellationToken);

        var items = page.Select(t => new AdminRatingTypeSummary(
            t.Id, t.Code, t.Name, t.NameArabic, t.Scope, t.HasOverallStars,
            t.AllowComment, t.CommentLabel, t.CommentLabelArabic, t.IsSystem,
            t.DisplayOrder, t.IsActive, t.GroupCount, t.QuestionCount,
            responseCounts.GetValueOrDefault(t.Id), t.CreatedAt)).ToList();

        return GridPage<AdminRatingTypeSummary>.Of(items, total,
            skip, top);
    }

    public async Task<AdminRatingTypeSummary?> GetTypeAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var t = await dbContext.RatingTypes.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id, t.Code, t.Name, t.NameArabic, t.Scope, t.HasOverallStars,
                t.AllowComment, t.CommentLabel, t.CommentLabelArabic, t.IsSystem,
                t.DisplayOrder, t.IsActive,
                GroupCount = t.Groups.Count(g => g.IsActive),
                QuestionCount = t.Questions.Count(q => q.IsActive),
                t.CreatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (t is null) { return null; }

        var responseCount = await dbContext.RatingResponses
            .CountAsync(r => r.RatingTypeId == id && r.IsActive, cancellationToken);

        return new AdminRatingTypeSummary(
            t.Id, t.Code, t.Name, t.NameArabic, t.Scope, t.HasOverallStars,
            t.AllowComment, t.CommentLabel, t.CommentLabelArabic, t.IsSystem,
            t.DisplayOrder, t.IsActive, t.GroupCount, t.QuestionCount,
            responseCount, t.CreatedAt);
    }

    public async Task<AdminRatingTypeSummary> CreateTypeAsync(
        Guid actorUserId, CreateRatingTypeRequest request, CancellationToken cancellationToken = default)
    {
        var code = RequireText(request.Code, CodeMaxLength, "code", "الرمز");
        if (await dbContext.RatingTypes.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new ApiException(ErrorCodes.RatingTypeCodeTaken, 409,
                "A rating type with this code already exists.",
                "يوجد نوع تقييم بهذا الرمز بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var type = new RatingType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = RequireText(request.Name, NameMaxLength, "English name", "الاسم الإنجليزي"),
            NameArabic = RequireText(request.NameArabic, NameMaxLength, "Arabic name", "الاسم العربي"),
            Scope = request.Scope,
            HasOverallStars = request.HasOverallStars,
            AllowComment = request.AllowComment,
            CommentLabel = OptionalText(request.CommentLabel, LabelMaxLength, "comment label", "عنوان التعليق"),
            CommentLabelArabic = OptionalText(request.CommentLabelArabic, LabelMaxLength, "Arabic comment label", "عنوان التعليق العربي"),
            IsSystem = false,
            DisplayOrder = RequireNonNegative(request.DisplayOrder),
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.RatingTypes.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingTypeCreated, actorUserId,
            $"id={type.Id}; code={type.Code}; scope={type.Scope}", cancellationToken);
        logger.LogInformation("Admin {ActorId} created rating type {Code} ({Id})",
            actorUserId, type.Code, type.Id);

        return ToTypeSummary(type, groupCount: 0, questionCount: 0, responseCount: 0);
    }

    public async Task<AdminRatingTypeSummary> UpdateTypeAsync(
        Guid actorUserId, Guid id, UpdateRatingTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = await dbContext.RatingTypes
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw TypeNotFound();

        // Code + Scope are immutable after create; only the editable surface moves.
        type.Name = RequireText(request.Name, NameMaxLength, "English name", "الاسم الإنجليزي");
        type.NameArabic = RequireText(request.NameArabic, NameMaxLength, "Arabic name", "الاسم العربي");
        type.HasOverallStars = request.HasOverallStars;
        type.AllowComment = request.AllowComment;
        type.CommentLabel = OptionalText(request.CommentLabel, LabelMaxLength, "comment label", "عنوان التعليق");
        type.CommentLabelArabic = OptionalText(request.CommentLabelArabic, LabelMaxLength, "Arabic comment label", "عنوان التعليق العربي");
        type.DisplayOrder = RequireNonNegative(request.DisplayOrder);
        // A system type can't be deactivated via the edit form either.
        type.IsActive = type.IsSystem || request.IsActive;
        type.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingTypeUpdated, actorUserId,
            $"id={type.Id}; code={type.Code}; active={type.IsActive}", cancellationToken);

        return (await GetTypeAsync(id, cancellationToken))!;
    }

    public async Task DeactivateTypeAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var type = await dbContext.RatingTypes
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw TypeNotFound();
        if (type.IsSystem)
        {
            throw new ApiException(ErrorCodes.RatingTypeIsSystem, 400,
                "Built-in rating types can't be deleted.",
                "لا يمكن حذف أنواع التقييم المدمجة.");
        }
        if (!type.IsActive) { return; } // idempotent

        type.IsActive = false;
        type.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingTypeDeactivated, actorUserId,
            $"id={type.Id}; code={type.Code}", cancellationToken);
    }

    // -- Question groups ------------------------------------------------------

    public async Task<GridPage<AdminRatingQuestionGroupSummary>> ListGroupsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 200);

        var rows = dbContext.RatingQuestionGroups.AsNoTracking()
            .Where(g => g.RatingTypeId == ratingTypeId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(g =>
                EF.Functions.Like(g.Name, $"%{term}%")
                || EF.Functions.Like(g.NameArabic, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(g => g.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(g => g.Name),
            ("name", false) => rows.OrderBy(g => g.Name),
            _ => rows.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(g => new AdminRatingQuestionGroupSummary(
                g.Id, g.RatingTypeId, g.Name, g.NameArabic, g.DisplayOrder, g.IsActive,
                g.Questions.Count(q => q.IsActive), g.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminRatingQuestionGroupSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminRatingQuestionGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.RatingQuestionGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new AdminRatingQuestionGroupSummary(
                g.Id, g.RatingTypeId, g.Name, g.NameArabic, g.DisplayOrder, g.IsActive,
                g.Questions.Count(q => q.IsActive), g.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminRatingQuestionGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateRatingQuestionGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RatingTypes.AnyAsync(t => t.Id == request.RatingTypeId, cancellationToken))
        {
            throw TypeNotFound();
        }

        var group = new RatingQuestionGroup
        {
            Id = Guid.NewGuid(),
            RatingTypeId = request.RatingTypeId,
            Name = RequireText(request.Name, NameMaxLength, "English name", "الاسم الإنجليزي"),
            NameArabic = RequireText(request.NameArabic, NameMaxLength, "Arabic name", "الاسم العربي"),
            DisplayOrder = RequireNonNegative(request.DisplayOrder),
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        dbContext.RatingQuestionGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingGroupCreated, actorUserId,
            $"id={group.Id}; typeId={group.RatingTypeId}", cancellationToken);

        return ToGroupSummary(group, questionCount: 0);
    }

    public async Task<AdminRatingQuestionGroupSummary> UpdateGroupAsync(
        Guid actorUserId, Guid id, UpdateRatingQuestionGroupRequest request, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.RatingQuestionGroups
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw GroupNotFound();

        group.Name = RequireText(request.Name, NameMaxLength, "English name", "الاسم الإنجليزي");
        group.NameArabic = RequireText(request.NameArabic, NameMaxLength, "Arabic name", "الاسم العربي");
        group.DisplayOrder = RequireNonNegative(request.DisplayOrder);
        group.IsActive = request.IsActive;
        group.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingGroupUpdated, actorUserId,
            $"id={group.Id}; typeId={group.RatingTypeId}; active={group.IsActive}", cancellationToken);

        var questionCount = await dbContext.RatingQuestions
            .CountAsync(q => q.RatingQuestionGroupId == id && q.IsActive, cancellationToken);
        return ToGroupSummary(group, questionCount);
    }

    public async Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.RatingQuestionGroups
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw GroupNotFound();
        if (!group.IsActive) { return; } // idempotent

        group.IsActive = false;
        group.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingGroupDeactivated, actorUserId,
            $"id={group.Id}; typeId={group.RatingTypeId}", cancellationToken);
    }

    // -- Questions ------------------------------------------------------------

    public async Task<GridPage<AdminRatingQuestionSummary>> ListQuestionsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 200);

        var rows = dbContext.RatingQuestions.AsNoTracking()
            .Where(q => q.RatingTypeId == ratingTypeId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(q =>
                EF.Functions.Like(q.Text, $"%{term}%")
                || EF.Functions.Like(q.TextArabic, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(q => q.IsActive == isActive);
        }
        if (query.Filters.TryGetValue("groupId", out var groupFilter)
            && Guid.TryParse(groupFilter, out var groupId))
        {
            rows = rows.Where(q => q.RatingQuestionGroupId == groupId);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("text", true) => rows.OrderByDescending(q => q.Text),
            ("text", false) => rows.OrderBy(q => q.Text),
            _ => rows.OrderBy(q => q.DisplayOrder).ThenBy(q => q.Text),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(ToQuestionSummaryExpr)
            .ToListAsync(cancellationToken);

        return GridPage<AdminRatingQuestionSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminRatingQuestionSummary?> GetQuestionAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.RatingQuestions.AsNoTracking()
            .Where(q => q.Id == id)
            .Select(ToQuestionSummaryExpr)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminRatingQuestionSummary> CreateQuestionAsync(
        Guid actorUserId, CreateRatingQuestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RatingTypes.AnyAsync(t => t.Id == request.RatingTypeId, cancellationToken))
        {
            throw TypeNotFound();
        }
        await EnsureGroupBelongsAsync(request.RatingQuestionGroupId, request.RatingTypeId, cancellationToken);

        var question = new RatingQuestion
        {
            Id = Guid.NewGuid(),
            RatingTypeId = request.RatingTypeId,
            RatingQuestionGroupId = request.RatingQuestionGroupId,
            Text = RequireText(request.Text, TextMaxLength, "English text", "النص الإنجليزي"),
            TextArabic = RequireText(request.TextArabic, TextMaxLength, "Arabic text", "النص العربي"),
            IsRequired = request.IsRequired,
            DisplayOrder = RequireNonNegative(request.DisplayOrder),
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        dbContext.RatingQuestions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingQuestionCreated, actorUserId,
            $"id={question.Id}; typeId={question.RatingTypeId}", cancellationToken);

        return ToQuestionSummary(question);
    }

    public async Task<AdminRatingQuestionSummary> UpdateQuestionAsync(
        Guid actorUserId, Guid id, UpdateRatingQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = await dbContext.RatingQuestions
            .SingleOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw QuestionNotFound();
        await EnsureGroupBelongsAsync(request.RatingQuestionGroupId, question.RatingTypeId, cancellationToken);

        question.RatingQuestionGroupId = request.RatingQuestionGroupId;
        question.Text = RequireText(request.Text, TextMaxLength, "English text", "النص الإنجليزي");
        question.TextArabic = RequireText(request.TextArabic, TextMaxLength, "Arabic text", "النص العربي");
        question.IsRequired = request.IsRequired;
        question.DisplayOrder = RequireNonNegative(request.DisplayOrder);
        question.IsActive = request.IsActive;
        question.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingQuestionUpdated, actorUserId,
            $"id={question.Id}; typeId={question.RatingTypeId}; active={question.IsActive}", cancellationToken);

        return ToQuestionSummary(question);
    }

    public async Task DeactivateQuestionAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var question = await dbContext.RatingQuestions
            .SingleOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw QuestionNotFound();
        if (!question.IsActive) { return; } // idempotent

        question.IsActive = false;
        question.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingQuestionDeactivated, actorUserId,
            $"id={question.Id}; typeId={question.RatingTypeId}", cancellationToken);
    }

    // -- internals ------------------------------------------------------------

    private async Task EnsureGroupBelongsAsync(
        Guid? groupId, Guid ratingTypeId, CancellationToken cancellationToken)
    {
        if (groupId is not { } gid) { return; }
        var ok = await dbContext.RatingQuestionGroups
            .AnyAsync(g => g.Id == gid && g.RatingTypeId == ratingTypeId, cancellationToken);
        if (!ok) { throw GroupNotFound(); }
    }

    private static AdminRatingTypeSummary ToTypeSummary(
        RatingType t, int groupCount, int questionCount, int responseCount) =>
        new(t.Id, t.Code, t.Name, t.NameArabic, t.Scope, t.HasOverallStars, t.AllowComment,
            t.CommentLabel, t.CommentLabelArabic, t.IsSystem, t.DisplayOrder, t.IsActive,
            groupCount, questionCount, responseCount, t.CreatedAt);

    private static AdminRatingQuestionGroupSummary ToGroupSummary(RatingQuestionGroup g, int questionCount) =>
        new(g.Id, g.RatingTypeId, g.Name, g.NameArabic, g.DisplayOrder, g.IsActive, questionCount, g.CreatedAt);

    private static AdminRatingQuestionSummary ToQuestionSummary(RatingQuestion q) =>
        new(q.Id, q.RatingTypeId, q.RatingQuestionGroupId, q.Text, q.TextArabic,
            q.IsRequired, q.DisplayOrder, q.IsActive, q.CreatedAt);

    private static System.Linq.Expressions.Expression<Func<RatingQuestion, AdminRatingQuestionSummary>> ToQuestionSummaryExpr =>
        q => new AdminRatingQuestionSummary(
            q.Id, q.RatingTypeId, q.RatingQuestionGroupId, q.Text, q.TextArabic,
            q.IsRequired, q.DisplayOrder, q.IsActive, q.CreatedAt);

    private static ApiException TypeNotFound() => new(
        ErrorCodes.RatingTypeNotFound, 404,
        "The rating type was not found.", "لم يتم العثور على نوع التقييم.");

    private static ApiException GroupNotFound() => new(
        ErrorCodes.RatingGroupNotFound, 404,
        "The rating question group was not found.", "لم يتم العثور على مجموعة الأسئلة.");

    private static ApiException QuestionNotFound() => new(
        ErrorCodes.RatingQuestionNotFound, 404,
        "The rating question was not found.", "لم يتم العثور على السؤال.");

    private static int RequireNonNegative(int displayOrder)
    {
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.RatingInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        return displayOrder;
    }

    private static string RequireText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length < 1)
        {
            throw new ApiException(
                ErrorCodes.RatingInvalid, 400,
                $"The {fieldEn} is required.", $"{fieldAr} مطلوب.");
        }
        if (value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.RatingInvalid, 400,
                $"The {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length < 1) { return null; }
        if (value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.RatingInvalid, 400,
                $"The {fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }
}
