// Tests: SIMF.Api.Tests/RatingConfigTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Feedback.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Feedback;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Common.Grids;
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

    /// <summary>The grid contract for the rating-types grid on RatingConfig.razor:
    /// one entry per key the page can send, as both its filter and its sort.</summary>
    private static readonly GridColumns<RatingType> TypeColumns = new GridColumns<RatingType>()
        .Add("code", type => type.Code, searchable: true)
        .Add("name", type => type.Name, searchable: true)
        .Add("nameArabic", type => type.NameArabic, searchable: true)
        .Add("displayOrder", type => type.DisplayOrder)
        .Add("isActive", type => type.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<RatingType, AdminRatingTypeSummary>> ToTypeRow =
        type => new AdminRatingTypeSummary(
            type.Id, type.Code, type.Name, type.NameArabic, type.Scope, type.HasOverallStars,
            type.AllowComment, type.CommentLabel, type.CommentLabelArabic, type.IsSystem,
            type.DisplayOrder, type.IsActive,
            type.Groups.Count(entry => entry.IsActive),
            type.Questions.Count(entry => entry.IsActive),
            // Filled in below. RatingResponse has no navigation back to RatingType,
            // so the response count cannot ride this projection.
            0,
            type.CreatedAt);

    public async Task<GridPage<AdminRatingTypeSummary>> ListTypesAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await dbContext.RatingTypes.ToGridPageAsync(
            query, TypeColumns, type => type.Id, ToTypeRow, cancellationToken);

        var typeIds = page.Items.Select(row => row.Id).ToList();
        var responseCounts = await dbContext.RatingResponses.AsNoTracking()
            .Where(response => response.IsActive && typeIds.Contains(response.RatingTypeId))
            .GroupBy(response => response.RatingTypeId)
            .Select(bucket => new { TypeId = bucket.Key, Count = bucket.Count() })
            .ToDictionaryAsync(entry => entry.TypeId, entry => entry.Count, cancellationToken);

        var items = page.Items
            .Select(row => row with { ResponseCount = responseCounts.GetValueOrDefault(row.Id) })
            .ToList();

        return GridPage<AdminRatingTypeSummary>.Of(items, page.Total, page.Skip, page.Top);
    }

    public async Task<AdminRatingTypeSummary?> GetTypeAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var type = await dbContext.RatingTypes.AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => new
            {
                row.Id, row.Code, row.Name, row.NameArabic, row.Scope, row.HasOverallStars,
                row.AllowComment, row.CommentLabel, row.CommentLabelArabic, row.IsSystem,
                row.DisplayOrder, row.IsActive,
                GroupCount = row.Groups.Count(group => group.IsActive),
                QuestionCount = row.Questions.Count(question => question.IsActive),
                row.CreatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (type is null) { return null; }

        var responseCount = await dbContext.RatingResponses
            .CountAsync(response => response.RatingTypeId == id && response.IsActive, cancellationToken);

        return new AdminRatingTypeSummary(
            type.Id, type.Code, type.Name, type.NameArabic, type.Scope, type.HasOverallStars,
            type.AllowComment, type.CommentLabel, type.CommentLabelArabic, type.IsSystem,
            type.DisplayOrder, type.IsActive, type.GroupCount, type.QuestionCount,
            responseCount, type.CreatedAt);
    }

    public async Task<AdminRatingTypeSummary> CreateTypeAsync(
        Guid actorUserId, CreateRatingTypeRequest request, CancellationToken cancellationToken = default)
    {
        var code = RequireText(request.Code, CodeMaxLength, "code", "الرمز");
        if (await dbContext.RatingTypes.AnyAsync(row => row.Code == code, cancellationToken))
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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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

    /// <summary>The grid contract for the question-groups grid on
    /// RatingConfig.razor. The owning type is a scope predicate, not a filter key,
    /// so it stays outside the grid.</summary>
    private static readonly GridColumns<RatingQuestionGroup> GroupColumns =
        new GridColumns<RatingQuestionGroup>()
            .Add("name", questionGroup => questionGroup.Name, searchable: true)
            .Add("nameArabic", questionGroup => questionGroup.NameArabic, searchable: true)
            .Add("displayOrder", questionGroup => questionGroup.DisplayOrder)
            .Add("isActive", questionGroup => questionGroup.IsActive)
            .DefaultOrder("displayOrder")
            .DefaultOrder("name")
            .PageSize(fallback: 50, max: 200);

    private static readonly Expression<Func<RatingQuestionGroup, AdminRatingQuestionGroupSummary>>
        ToGroupRow = questionGroup => new AdminRatingQuestionGroupSummary(
            questionGroup.Id, questionGroup.RatingTypeId, questionGroup.Name,
            questionGroup.NameArabic, questionGroup.DisplayOrder, questionGroup.IsActive,
            questionGroup.Questions.Count(question => question.IsActive), questionGroup.CreatedAt);

    public Task<GridPage<AdminRatingQuestionGroupSummary>> ListGroupsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.RatingQuestionGroups
            .Where(questionGroup => questionGroup.RatingTypeId == ratingTypeId)
            .ToGridPageAsync(
                query, GroupColumns, questionGroup => questionGroup.Id, ToGroupRow,
                cancellationToken);

    public async Task<AdminRatingQuestionGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.RatingQuestionGroups.AsNoTracking()
            .Where(questionGroup => questionGroup.Id == id)
            .Select(ToGroupRow)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminRatingQuestionGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateRatingQuestionGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RatingTypes.AnyAsync(
            type => type.Id == request.RatingTypeId, cancellationToken))
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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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
            .CountAsync(
                question => question.RatingQuestionGroupId == id && question.IsActive,
                cancellationToken);
        return ToGroupSummary(group, questionCount);
    }

    public async Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await dbContext.RatingQuestionGroups
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw GroupNotFound();
        if (!group.IsActive) { return; } // idempotent

        group.IsActive = false;
        group.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(AuditEvents.RatingGroupDeactivated, actorUserId,
            $"id={group.Id}; typeId={group.RatingTypeId}", cancellationToken);
    }

    // -- Questions ------------------------------------------------------------

    /// <summary>The grid contract for the questions grid on RatingConfig.razor.
    /// <c>groupId</c> is filter-only in practice (the grid renders the group name
    /// through a client-side lookup), but it is a real column here so an unparseable
    /// value is a 400 rather than a silently unfiltered grid.</summary>
    private static readonly GridColumns<RatingQuestion> QuestionColumns =
        new GridColumns<RatingQuestion>()
            .Add("text", question => question.Text, searchable: true)
            .Add("textArabic", question => question.TextArabic, searchable: true)
            .Add("groupId", question => question.RatingQuestionGroupId)
            .Add("displayOrder", question => question.DisplayOrder)
            .Add("isActive", question => question.IsActive)
            .DefaultOrder("displayOrder")
            .DefaultOrder("text")
            .PageSize(fallback: 50, max: 200);

    private static readonly Expression<Func<RatingQuestion, AdminRatingQuestionSummary>>
        ToQuestionRow = question => new AdminRatingQuestionSummary(
            question.Id, question.RatingTypeId, question.RatingQuestionGroupId,
            question.Text, question.TextArabic, question.IsRequired,
            question.DisplayOrder, question.IsActive, question.CreatedAt);

    public Task<GridPage<AdminRatingQuestionSummary>> ListQuestionsAsync(
        Guid ratingTypeId, GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.RatingQuestions
            .Where(question => question.RatingTypeId == ratingTypeId)
            .ToGridPageAsync(
                query, QuestionColumns, question => question.Id, ToQuestionRow,
                cancellationToken);

    public async Task<AdminRatingQuestionSummary?> GetQuestionAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.RatingQuestions.AsNoTracking()
            .Where(question => question.Id == id)
            .Select(ToQuestionRow)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminRatingQuestionSummary> CreateQuestionAsync(
        Guid actorUserId, CreateRatingQuestionRequest request, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.RatingTypes.AnyAsync(
            type => type.Id == request.RatingTypeId, cancellationToken))
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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
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
        if (groupId is not { } requiredGroupId) { return; }
        var belongs = await dbContext.RatingQuestionGroups
            .AnyAsync(
                group => group.Id == requiredGroupId && group.RatingTypeId == ratingTypeId,
                cancellationToken);
        if (!belongs) { throw GroupNotFound(); }
    }

    private static AdminRatingTypeSummary ToTypeSummary(
        RatingType type, int groupCount, int questionCount, int responseCount) =>
        new(type.Id, type.Code, type.Name, type.NameArabic, type.Scope, type.HasOverallStars,
            type.AllowComment, type.CommentLabel, type.CommentLabelArabic, type.IsSystem,
            type.DisplayOrder, type.IsActive,
            groupCount, questionCount, responseCount, type.CreatedAt);

    private static AdminRatingQuestionGroupSummary ToGroupSummary(
        RatingQuestionGroup group, int questionCount) =>
        new(group.Id, group.RatingTypeId, group.Name, group.NameArabic, group.DisplayOrder,
            group.IsActive, questionCount, group.CreatedAt);

    private static AdminRatingQuestionSummary ToQuestionSummary(RatingQuestion question) =>
        new(question.Id, question.RatingTypeId, question.RatingQuestionGroupId,
            question.Text, question.TextArabic,
            question.IsRequired, question.DisplayOrder, question.IsActive, question.CreatedAt);

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
