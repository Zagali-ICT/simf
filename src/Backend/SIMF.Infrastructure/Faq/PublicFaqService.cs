// Tests: SIMF.Api.Tests/FaqTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Faq.Abstractions;
using SIMF.Contracts.Faq;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Faq;

/// <summary>Public read of the two-level FAQ for the app accordion.
/// Two cheap queries (groups, then their entries) stitched in
/// memory — mirrors <see cref="SIMF.Infrastructure.Configuration.OrganizationProfileReadService"/>'s
/// multi-query style. Active-only and ordered server-side; groups with no active
/// entry are dropped so the accordion never shows an empty section.</summary>
internal sealed class PublicFaqService(SimfAppDbContext dbContext) : IPublicFaqService
{
    public async Task<IReadOnlyList<PublicFaqGroup>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await dbContext.FaqGroups.AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.NameArabic })
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return [];
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var entries = await dbContext.FaqEntries.AsNoTracking()
            .Where(e => e.IsActive && groupIds.Contains(e.FaqGroupId))
            .OrderBy(e => e.DisplayOrder).ThenBy(e => e.Question)
            .Select(e => new
            {
                e.Id,
                e.FaqGroupId,
                e.Question,
                e.QuestionArabic,
                e.Answer,
                e.AnswerArabic,
            })
            .ToListAsync(cancellationToken);

        var byGroup = entries.ToLookup(e => e.FaqGroupId);

        return groups
            .Select(g => new PublicFaqGroup(
                g.Id,
                g.Name,
                g.NameArabic,
                byGroup[g.Id]
                    .Select(e => new PublicFaqEntry(
                        e.Id, e.Question, e.QuestionArabic, e.Answer, e.AnswerArabic))
                    .ToList()))
            .Where(g => g.Entries.Count > 0)
            .ToList();
    }
}
