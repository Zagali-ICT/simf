namespace SIMF.Contracts.Admin;

/// <summary>D-495 — the CP "Organization Profile" editor payload. A single
/// full-document upsert: the scalar branding fields plus the complete desired
/// about-items + details lists. The admin service updates the scalars and
/// reconciles each child list by id (update existing, insert new, soft-delete
/// the rows no longer present). A blank URL clears the setting.</summary>
public sealed class AdminUpdateOrganizationProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public string? SloganArabic { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }

    public string? Version { get; set; }
    public DateTimeOffset? VersionDate { get; set; }
    public string? SysVersion { get; set; }
    public DateTimeOffset? ReleaseDate { get; set; }

    public DateTimeOffset? EventStartDate { get; set; }
    public DateTimeOffset? EventEndDate { get; set; }
    public int CurrentYear { get; set; }

    /// <summary>The forum status — the <c>ForumStatus</c> enum name
    /// (<c>Soon</c> / <c>Open</c> / <c>Archived</c>).</summary>
    public string Status { get; set; } = "Open";

    public string? LocationText { get; set; }
    public string? LocationTextArabic { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactWebsite { get; set; }
    public string? LiveStreamUrl { get; set; }

    public string? Facebook { get; set; }
    public string? X { get; set; }
    public string? Instagram { get; set; }
    public string? LinkedIn { get; set; }
    public string? YouTube { get; set; }
    public string? TikTok { get; set; }
    public string? Snapchat { get; set; }

    public List<AdminOrganizationAboutItem> AboutItems { get; set; } = new();
    public List<AdminOrganizationDetail> Details { get; set; } = new();
}

/// <summary>One about-item in the upsert. <c>Id</c> null/empty = a new row.</summary>
public sealed class AdminOrganizationAboutItem
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string TextArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>One detail row in the upsert. <c>Id</c> null/empty = a new row.</summary>
public sealed class AdminOrganizationDetail
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
