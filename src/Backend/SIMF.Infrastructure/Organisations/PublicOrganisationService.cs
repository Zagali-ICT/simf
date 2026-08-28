// Tests: SIMF.Api.Tests/OrganisationTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Organisations.Abstractions;
using SIMF.Contracts.Organisations;
using SIMF.Domain.Organisations;
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
        // The catch-all is excluded from the search and re-attached below. It
        // has to be reachable at the moment the search finds NOTHING, which is
        // exactly when a name filter would drop it: someone typing their real
        // employer is not typing "Other", so matching it on name would hide it
        // from the only person who needs it.
        var rows = db.Organisations.AsNoTracking()
            .Where(o => o.IsActive && o.Id != Organisation.OtherId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(o =>
                EF.Functions.Like(o.NameArabic, $"%{term}%")
                || EF.Functions.Like(o.Name, $"%{term}%")
                || EF.Functions.Like(o.City, $"%{term}%"));
        }

        // take - 1 keeps the response within the caller's requested size once
        // the catch-all is appended, so a client sizing its list to `top` does
        // not silently lose a real match to it.
        var matches = await rows
            .OrderBy(o => o.NameArabic)
            .Take(Math.Max(1, take - 1))
            .Select(o => new OrganisationPickerItem(
                o.Id,
                o.NameArabic,
                o.Name,
                o.City,
                false))
            .ToListAsync(ct);

        var other = await db.Organisations.AsNoTracking()
            .Where(o => o.Id == Organisation.OtherId && o.IsActive)
            .Select(o => new OrganisationPickerItem(
                o.Id,
                o.NameArabic,
                o.Name,
                o.City,
                true))
            .FirstOrDefaultAsync(ct);

        // Last, not first: it is the fallback, and a picker that offers "Other"
        // above the visitor's actual employer invites the wrong pick. Null only
        // if an administrator deactivated the seeded row, in which case the list
        // degrades to what it was before rather than failing.
        return other is null ? matches : [.. matches, other];
    }
}
