using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// One media partner in the app's partner grid. The public list is ordered by
/// <see cref="DisplayOrder"/>, tie-broken by <see cref="NameArabic"/>.
/// </summary>
public sealed class MediaPartner : BaseAuditEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The primary surface on the mobile app.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The partner's logo; a card without one falls back to the name, as its row in the one file store. A real foreign key:
    /// both sides live in the App database.
    ///
    /// <para>This was <c>LogoRelativePath</c>, admin-typed free text. An uploaded image and
    /// a linked one are now the same thing, a <c>StoredFile</c>, so the value is
    /// validated and stored once instead of living untyped on this row.</para>
    /// </summary>
    public Guid? LogoFileId { get; set; }

    /// <summary>An outbound link to the partner's site, null when they have
    /// none. Doubles as the website slot of the contact card below.</summary>
    public string? Url { get; set; }

    /// <summary>Ascending, tie-broken by <see cref="NameArabic"/>.</summary>
    public int DisplayOrder { get; set; }

    // Contact-card fields, inlined when the shared contact directory was removed.
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? FacebookUrl { get; set; }
    public string? XUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>A real foreign key, since the country lives in the same
    /// database.</summary>
    public int? CountryId { get; set; }

    public Country? Country { get; set; }
}
