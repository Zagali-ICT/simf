namespace SIMF.Contracts.Exhibition;

/// <summary>D-199 — public booth list item (Mockup page 22). Only the
/// fields the visitor-facing exhibition page + 2D map need.</summary>
public sealed class PublicBoothSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? ExhibitorName { get; set; }
    public string? ExhibitorNameArabic { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }

    // D-432 — appended (append-only wire). The hall display name (the entity
    // already carries it; only HallId was sent before) + the booth-officer
    // contact resolved Contact-first, falling back to the inline columns.
    public string? HallName { get; set; }
    public string? HallNameArabic { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }

    // P6 — D-440 (append-only): the exhibitor's Contact id (Exhibitor.ContactId),
    // the owner of the CompanyLogo asset. The app renders the real booth logo via
    // GET /app/assets/CompanyLogo/{ExhibitorContactId}/image (D-357), falling back
    // to initials when the exhibitor has no linked Contact / logo.
    public Guid? ExhibitorContactId { get; set; }

    // D-456 (append-only): the exhibitor company's country (Exhibitor → Contact →
    // CountryId), ISO 3166-1 numeric, for the app's corner flag on the logo. Null
    // when the exhibitor has no linked Contact / country.
    public int? CountryId { get; set; }

    // #9 (append-only): the country NAME resolved from the Country lookup on
    // CountryId, so the app can show the country, not just the flag. Null when no
    // country / no matching lookup row.
    public string? CountryName { get; set; }
    public string? CountryNameArabic { get; set; }
}

/// <summary>D-199 — public booth detail (adds the description paragraph).</summary>
public sealed class PublicBoothDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? ExhibitorName { get; set; }
    public string? ExhibitorNameArabic { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }

    // D-432 — appended (append-only wire); see PublicBoothSummary.
    public string? HallName { get; set; }
    public string? HallNameArabic { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }

    // P6 — D-440 (append-only): exhibitor's Contact id (CompanyLogo owner); see
    // PublicBoothSummary.
    public Guid? ExhibitorContactId { get; set; }

    // D-456 (append-only): the exhibitor company's country (Exhibitor → Contact
    // → CountryId) for the app's corner flag; see PublicBoothSummary.
    public int? CountryId { get; set; }

    // #9 (append-only): the country NAME resolved from the Country lookup; see
    // PublicBoothSummary.
    public string? CountryName { get; set; }
    public string? CountryNameArabic { get; set; }

    // Wave 3 (Figma 1439:11881) — appended (append-only). The exhibitor-detail
    // screen's location line (City، Country), tier pill, about + website.
    // City is resolved Exhibitor → Contact.City; Website from Exhibitor.Website;
    // Tier from Exhibitor.Tier (TierName = the enum name, the app localizes it).
    // All null when the exhibitor / its Contact has no value.
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public int? Tier { get; set; }
    public string? TierName { get; set; }
    public string? Website { get; set; }

    // Appended (append-only): the linked exhibitor's own id (Exhibitor.Id), the
    // owner of the exhibitor's ExhibitorLogo asset. The app renders the exhibitor's
    // own logo via GET /app/assets/ExhibitorLogo/{ExhibitorId}/image, falling back
    // to the legacy CompanyLogo (via ExhibitorContactId) then initials. Null when
    // the booth has no linked exhibitor.
    public Guid? ExhibitorId { get; set; }
}

/// <summary>D-199 — admin grid row. B1 — D-222: the exhibitor is now the
/// <see cref="ExhibitorId"/> relation (the CP resolves the name client-side from
/// the loaded exhibitor list, mirroring <see cref="HallId"/>).</summary>
public sealed class AdminBoothSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? Sector { get; set; }
    public Guid? HallId { get; set; }
    public bool IsActive { get; set; }

    // D-357 — the booth's exhibitor company's Contact id + whether that contact has
    // an active CompanyLogo asset. Retained for the exhibitor-resolved detail;
    // the grid thumbnail now uses the booth's own logo (see HasBoothLogo).
    public Guid? ExhibitorContactId { get; set; }
    public bool HasLogo { get; set; }

    // A booth now owns its own BoothLogo (owner = the booth) — true when it has an
    // active BoothLogo asset, so the grid renders the booth's own logo thumbnail
    // (else an initials tile). The app renders this logo, not the exhibitor's.
    public bool HasBoothLogo { get; set; }
}

/// <summary>D-199 — admin full detail (every column incl. map position).
/// B1 — D-222: exhibitor = <see cref="ExhibitorId"/> relation + booth-officer
/// contact.</summary>
public sealed class AdminBoothDetail
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public bool IsActive { get; set; }

    // D-673 — read-only exhibitor-resolved fields so the CP booth detail matches
    // the app booth detail (mirrors PublicBoothDetail). All are owned by the
    // linked Exhibitor, NOT by the booth, so they are surfaced read-only on the
    // detail view and are NOT part of the create/update write surface. Resolved
    // on read (GetAsync) only — the create/update echo leaves them null. Website
    // + Tier come from the Exhibitor; City/CityArabic from the exhibitor's
    // Contact; ExhibitorContactId is the CompanyLogo owner (the booth logo the
    // app renders). All null when the booth has no linked exhibitor / Contact.
    public string? Website { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public int? Tier { get; set; }
    public string? TierName { get; set; }
    public Guid? ExhibitorContactId { get; set; }
}

/// <summary>D-199 — admin create payload. B1 — D-222: exhibitor =
/// <see cref="ExhibitorId"/> relation + booth-officer contact.</summary>
public sealed class AdminCreateBoothRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
}

/// <summary>D-199 — admin update payload. B1 — D-222: exhibitor =
/// <see cref="ExhibitorId"/> relation + booth-officer contact.</summary>
public sealed class AdminUpdateBoothRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public Guid? ExhibitorId { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerPhone { get; set; }
    public string? OfficerEmail { get; set; }
    public string? Sector { get; set; }
    public string? SectorArabic { get; set; }
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid? HallId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public bool IsActive { get; set; } = true;
}
