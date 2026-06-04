// Tests: SIMF.Api.Tests/DelegationsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Contracts.Delegations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Delegations;

/// <summary>D-174 — anonymous public list of active delegations.</summary>
internal sealed class PublicDelegationService(SimfAppDbContext appDbContext)
    : IPublicDelegationService
{
    public async Task<PublicDelegations> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.Delegations.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.IsPriority)
            .ThenBy(d => d.DisplayOrder)
            .ThenBy(d => d.NameArabic)
            .Select(d => new PublicDelegation(
                d.Id, d.Name, d.NameArabic,
                d.Country != null ? d.Country.Name : null,
                d.Country != null ? d.Country.NameArabic : null,
                d.MemberCount, d.IsPriority, d.IsInternational,
                d.DisplayOrder))
            .ToListAsync(cancellationToken);
        return new PublicDelegations(rows);
    }
}
