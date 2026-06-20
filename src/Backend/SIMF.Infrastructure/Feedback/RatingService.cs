// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
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

/// <summary>
/// D-199 (Mockup screen 40 "Rate the Forum") — one overall forum rating per
/// attendee. <see cref="RateAsync"/> upserts on <c>UserId</c> (the unique index
/// guarantees one row per user); <see cref="ListAllAsync"/> returns the admin
/// grid plus the average-stars aggregate over active ratings. Built on
/// <see cref="SimfAppDbContext"/>; mirrors <c>AdminSpeakerService</c> for the
/// grid/audit shape and <c>SeatReservationService</c> for the per-attendee
/// upsert + uniqueness-guard shape.
/// </summary>
internal sealed class RatingService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<RatingService> logger) : IRatingService
{
    private const int MinStars = 1;
    private const int MaxStars = 5;
    private const int CommentMaxLength = 2000;

    public async Task<RatingView> RateAsync(
        Guid userId, RateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Stars is < MinStars or > MaxStars)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "Stars must be between 1 and 5.",
                "يجب أن يكون التقييم بين 1 و 5.");
        }

        var comment = NullIfBlank(request.Comment);
        if (comment is not null && comment.Length > CommentMaxLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"Comment must be {CommentMaxLength} characters or less.",
                $"يجب ألا يتجاوز التعليق {CommentMaxLength} حرفاً.");
        }

        var now = timeProvider.GetUtcNow();

        // Upsert: one rating per user. Revising an existing row (even a
        // previously soft-deleted one) flips it back to active with the
        // new values.
        var existing = await dbContext.Ratings
            .SingleOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            existing.Stars = request.Stars;
            existing.OrganizationStars = request.OrganizationStars;
            existing.ContentStars = request.ContentStars;
            existing.AppStars = request.AppStars;
            existing.VenueStars = request.VenueStars;
            existing.Comment = comment;
            existing.IsActive = true;
            existing.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.RatingRevised,
                Outcome = AuditOutcome.Success,
                ActorUserId = userId,
                Detail = DetailFor(existing),
            }, cancellationToken);

            logger.LogInformation(
                "User {UserId} revised rating {RatingId} to {Stars} stars",
                userId, existing.Id, existing.Stars);

            return ToView(existing);
        }

        var rating = new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Stars = request.Stars,
            OrganizationStars = request.OrganizationStars,
            ContentStars = request.ContentStars,
            AppStars = request.AppStars,
            VenueStars = request.VenueStars,
            Comment = comment,
            CreatedAt = now,
            IsActive = true,
        };
        dbContext.Ratings.Add(rating);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RatingSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = DetailFor(rating),
        }, cancellationToken);

        logger.LogInformation(
            "User {UserId} submitted rating {RatingId} ({Stars} stars)",
            userId, rating.Id, rating.Stars);

        return ToView(rating);
    }

    public async Task<AdminRatingsPage> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.Ratings.AsNoTracking()
            .Where(rating => rating.IsActive);

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "comment":
                    rows = rows.Where(rating =>
                        rating.Comment != null && rating.Comment.Contains(v));
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(rating =>
                rating.Comment != null
                && EF.Functions.Like(rating.Comment, $"%{term}%"));
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("stars", true) => rows.OrderByDescending(rating => rating.Stars),
            ("stars", false) => rows.OrderBy(rating => rating.Stars),
            ("createdat", false) => rows.OrderBy(rating => rating.CreatedAt),
            _ => rows.OrderByDescending(rating => rating.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);

        // Average over the filtered active set; 0 when empty (AverageAsync over
        // an empty sequence throws, so guard on the count).
        var average = total == 0
            ? 0d
            : await rows.AverageAsync(rating => (double)rating.Stars, cancellationToken);

        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(rating => new AdminRatingSummary(
                rating.Id, rating.UserId, rating.Stars, rating.Comment,
                rating.IsActive, rating.CreatedAt, rating.UpdatedAt,
                rating.OrganizationStars, rating.ContentStars,
                rating.AppStars, rating.VenueStars))
            .ToListAsync(cancellationToken);

        var grid = GridPage<AdminRatingSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
        return new AdminRatingsPage(grid, average, total);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Self-contained audit detail: the overall + the four per-element scores
    // (D-463), so the audit trail records exactly what was submitted/revised.
    private static string DetailFor(Rating r) =>
        $"ratingId={r.Id}; stars={r.Stars}; org={r.OrganizationStars}; " +
        $"content={r.ContentStars}; app={r.AppStars}; venue={r.VenueStars}";

    private static RatingView ToView(Rating rating) =>
        new(rating.Id, rating.Stars, rating.Comment,
            rating.CreatedAt, rating.UpdatedAt,
            rating.OrganizationStars, rating.ContentStars,
            rating.AppStars, rating.VenueStars);
}
