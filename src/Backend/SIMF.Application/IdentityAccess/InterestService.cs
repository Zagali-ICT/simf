// Tests: SIMF.Api.Tests/InterestTests.cs
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Auditing;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Interests CRUD (P9 — D-050; الاهتمامات). The admin grid is paged +
/// filtered through <see cref="GridQuery"/>; mutations audit one row
/// each. The visitor picker (<see cref="ListActiveAsync"/>) hits the
/// composite filter index <c>(IsActive, DisplayOrder)</c>.
///
/// <para>R4 — D-209: moved from <c>SIMF.Infrastructure.Identity</c>;
/// persistence is delegated to <see cref="IInterestRepository"/> so this
/// service holds only the orchestration (validation, audit, logging).</para>
/// </summary>
internal sealed class InterestService(
    IInterestRepository interests,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<InterestService> logger) : IInterestService
{
    public Task<IReadOnlyList<InterestDto>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        interests.ListActiveAsync(cancellationToken);

    public async Task<GridPage<AdminInterestSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var (items, total) = await interests.ListPageAsync(query, skip, top, cancellationToken);

        return GridPage<AdminInterestSummary>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public Task<AdminInterestSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        interests.GetSummaryAsync(id, cancellationToken);

    public async Task<AdminInterestSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateInterestRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await interests.NameExistsAsync(request.Name, null, cancellationToken);
        if (existing)
        {
            throw new ApiException(
                ErrorCodes.InterestNameDuplicate, 409,
                $"An interest named '{request.Name}' already exists.",
                $"يوجد اهتمام بالاسم '{request.Name}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NameArabic = request.NameArabic.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        await interests.AddAsync(interest, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created interest {Name} ({Id})",
            actorUserId, interest.Name, interest.Id);

        return new AdminInterestSummary(
            interest.Id, interest.Name, interest.NameArabic,
            interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public async Task<AdminInterestSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateInterestRequest request,
        CancellationToken cancellationToken = default)
    {
        var interest = await interests.FindAsync(id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InterestNotFound, 404,
                "The interest was not found.",
                "لم يتم العثور على الاهتمام.");

        // Reject a rename that collides with another row.
        if (!string.Equals(interest.Name, request.Name, StringComparison.Ordinal))
        {
            var clash = await interests.NameExistsAsync(request.Name, id, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.InterestNameDuplicate, 409,
                    $"An interest named '{request.Name}' already exists.",
                    $"يوجد اهتمام بالاسم '{request.Name}' بالفعل.");
            }
        }

        interest.Name = request.Name.Trim();
        interest.NameArabic = request.NameArabic.Trim();
        interest.DisplayOrder = request.DisplayOrder;
        interest.IsActive = request.IsActive;
        interest.UpdatedAt = timeProvider.GetUtcNow();
        await interests.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}; active={interest.IsActive}",
        }, cancellationToken);

        return new AdminInterestSummary(
            interest.Id, interest.Name, interest.NameArabic,
            interest.DisplayOrder, interest.IsActive, interest.CreatedAt);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var interest = await interests.FindAsync(id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InterestNotFound, 404,
                "The interest was not found.",
                "لم يتم العثور على الاهتمام.");

        if (!interest.IsActive)
        {
            // Idempotent — already deactivated.
            return;
        }

        interest.IsActive = false;
        interest.UpdatedAt = timeProvider.GetUtcNow();
        await interests.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InterestDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={interest.Id}; name={interest.Name}",
        }, cancellationToken);
    }
}
