using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Domain.Organisations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Organisations;

/// <summary>
/// Development-only convenience seeder for the organisations lookup so the
/// registration organisation picker (Web profile, App sign-up, CP walk-in) has
/// data to show before the government Excel import has run. Idempotent — keyed
/// on the commercial-registration number, so re-running inserts only the rows
/// that are missing. NOT run in production: there the lookup is populated by the
/// real gov Excel import.
/// </summary>
public sealed class OrganisationSeeder(
    SimfAppDbContext appDbContext,
    ILogger<OrganisationSeeder> logger)
{
    // A small, realistic sample of Saudi maritime / defence / energy bodies so
    // the picker typeahead returns sensible matches during development & demos.
    private static readonly (string Cr, string NameAr, string NameEn, string Sector, string City)[] Sample =
    {
        ("1010000001", "القوات البحرية الملكية السعودية", "Royal Saudi Naval Forces", "Defense", "Riyadh"),
        ("1010000002", "الشركة السعودية للصناعات العسكرية", "Saudi Arabian Military Industries (SAMI)", "Defense Systems", "Riyadh"),
        ("2050000003", "أرامكو السعودية", "Saudi Aramco", "Energy", "Dhahran"),
        ("4030000004", "ميناء جدة الإسلامي", "Jeddah Islamic Port", "Maritime Logistics", "Jeddah"),
        ("2055000005", "الهيئة العامة للموانئ (موانئ)", "Saudi Ports Authority (Mawani)", "Maritime", "Riyadh"),
        ("4030000006", "البحري للنقل البحري", "Bahri", "Shipping", "Riyadh"),
        ("1010000007", "زامل للخدمات البحرية", "Zamil Offshore Services", "Offshore", "Dammam"),
        ("2052000008", "أكوا باور", "ACWA Power", "Energy", "Riyadh"),
        ("4030000009", "ميناء الملك عبدالله", "King Abdullah Port", "Maritime Logistics", "Rabigh"),
        ("1010000010", "هيئة المساحة الجيولوجية السعودية", "Saudi Geological Survey", "Research", "Jeddah"),
        ("1010000011", "جامعة الملك فهد للبترول والمعادن", "King Fahd University of Petroleum and Minerals", "Education", "Dhahran"),
        ("1010000012", "وزارة الدفاع", "Ministry of Defense", "Government", "Riyadh"),
    };

    public async Task SeedFakeAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await appDbContext.Organisations
                .Where(o => o.CommercialRegistration != null)
                .Select(o => o.CommercialRegistration!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = Sample
            .Where(row => !existing.Contains(row.Cr))
            .Select(row => new Organisation
            {
                Id = Guid.NewGuid(),
                NameArabic = row.NameAr,
                Name = row.NameEn,
                CommercialRegistration = row.Cr,
                Sector = row.Sector,
                City = row.City,
                IsActive = true,
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation(
                "Organisation dev-seed skipped — all {Count} sample rows already present.",
                Sample.Length);
            return;
        }

        appDbContext.Organisations.AddRange(toAdd);
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Organisation dev-seed inserted {Count} sample organisations.", toAdd.Count);
    }
}
