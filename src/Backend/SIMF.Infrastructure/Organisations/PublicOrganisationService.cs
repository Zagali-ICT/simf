// Tests: SIMF.Api.Tests/OrganisationTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Contracts.Organisations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Organisations;

/// <summary>
/// Organisation lookup — public, anonymous picker / typeahead search over the
/// active organisations directory. Mirrors <c>PublicBoothService</c>:
/// AsNoTracking, IsActive filter, projection to the public contract. A blank
/// search returns the first <c>top</c> organisations by Arabic name; otherwise
/// it matches the term across Arabic / English name and city.
/// </summary>
internal sealed class PublicOrganisationService(SimfAppDbContext db) : IPublicOrganisationService
{
    public async Task<IReadOnlyList<OrganisationPickerItem>> SearchAsync(
        string? search, int top, CancellationToken ct = default)
    {
        var take = Math.Clamp(top, 1, 50);
        var rows = db.Organisations.AsNoTracking().Where(o => o.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(o =>
                EF.Functions.Like(o.NameArabic, $"%{term}%")
                || EF.Functions.Like(o.Name, $"%{term}%")
                || EF.Functions.Like(o.City, $"%{term}%"));
        }

        return await rows
            .OrderBy(o => o.NameArabic)
            .Take(take)
            .Select(o => new OrganisationPickerItem(
                o.Id,
                o.NameArabic,
                o.Name,
                o.City))
            .ToListAsync(ct);
    }
}
