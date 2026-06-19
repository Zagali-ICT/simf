using SIMF.Common.Enums;

namespace SIMF.Contracts.UserProfile;

/// <summary>The body returned by <c>GET /api/v1/app/account/user-profile</c>
/// (decisions D-046 b, P8 — D-049). When the user has not filled the
/// form yet, every field is empty / null except <see cref="QrId"/> (which
/// is present whenever the account state is Approved) and
/// <see cref="ProfileTypeId"/> (which is present when the admin has
/// already assigned a subtype).</summary>
public sealed class UserProfileResponse
{
    /// <summary>The <see cref="ProfileType"/> id, when one is assigned.
    /// P8 moved this off <c>SimfUser</c>.</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>The picked interest ids (P9 — D-050; الاهتمامات). Empty
    /// when the user has not filled the form yet; the validator requires
    /// 1-10 ids on every save.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    /// <summary>D-163 (PDF §2.6) — optional job title.</summary>
    public string? JobTitle { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }

    /// <summary>C6 — D-371/D-459 (رقم اللوحة): the optional Saudi vehicle plate,
    /// stored as the canonical Latin "code" (Latin letters + Western digits,
    /// no separators). See <see cref="PlateNumberAr"/> / <see cref="PlateNumberEn"/>
    /// for the per-script renderings.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>C6 — D-459: the plate rendered in Arabic (Arabic letters +
    /// Arabic-Indic digits), derived from <see cref="PlateNumber"/>.</summary>
    public string? PlateNumberAr { get; set; }

    /// <summary>C6 — D-459: the plate rendered in English/Latin (the canonical
    /// code); same value as <see cref="PlateNumber"/>.</summary>
    public string? PlateNumberEn { get; set; }

    /// <summary>D-373 — the registration reference (<c>SIMF-2026-00000001</c>),
    /// issued once at profile creation. Customer-facing lookup key; NOT the
    /// QR id.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>B3 — D-221 (الجهة): the picked <see cref="Organisation"/> id,
    /// or null when the user has not picked one.</summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>B3 — D-221 (الجنس): the user's gender;
    /// <see cref="Gender.Unspecified"/> until picked.</summary>
    public Gender Gender { get; set; }

    /// <summary>True when an ID-image has been uploaded. The image bytes
    /// themselves are not in the response — fetch them at
    /// <c>GET /api/v1/app/account/user-profile/id-image</c>.</summary>
    public bool HasIdImage { get; set; }

    /// <summary>True when a face photo (avatar) has been uploaded. The face
    /// photo is the visible profile photo; mandatory for male registrants,
    /// optional for women. The bytes are streamed at
    /// <c>GET /api/v1/app/account/avatar/{userId}</c>. (Append-only field —
    /// added with the two-photo split; D-431-follow-up.)</summary>
    public bool HasAvatar { get; set; }

    /// <summary>The 12-character Crockford QR id; null when the account is
    /// not yet Approved.</summary>
    public string? QrId { get; set; }
}

/// <summary>The body posted to <c>POST /api/v1/app/account/user-profile</c>
/// (D-046 b, P8). An upsert — first call creates the row, every later
/// call updates it. The validator enforces the field-shape rules.
/// <para>D-190 (D-186 follow-up): <see cref="ProfileTypeId"/> is the
/// user's self-pick from the public
/// <c>GET /api/v1/app/account/profile-types</c> endpoint. Optional — if
/// the user submits without picking, the admin assigns one later via
/// the admin endpoints. The validator rejects unknown ids, inactive
/// rows, and Admin-scope rows. The service preserves any admin-
/// pre-assigned ProfileTypeId (admin pre-pick wins over user
/// self-pick).</para></summary>
public sealed class UpsertUserProfileRequest
{
    /// <summary>D-190 — the user's self-picked
    /// <see cref="UserProfileResponse.ProfileTypeId"/>. Optional;
    /// admin pre-pick wins on conflict (see
    /// <c>UserProfileService.UpsertMineAsync</c>).</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>The picked interest ids (P9 — D-050). Required: 1-10
    /// active <see cref="Interest"/> ids; the validator rejects empties /
    /// duplicates / unknown ids / deactivated ids.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    /// <summary>D-163 (PDF §2.6) — optional job title.</summary>
    public string? JobTitle { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }

    /// <summary>C6 — D-371 (رقم اللوحة): optional Saudi vehicle plate. When
    /// present the validator requires 3 letters (Arabic or Latin) + 1–4
    /// digits, ≤ 7 chars after separators are stripped; the service stores
    /// the normalized value.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>B3 — D-221 (الجهة): the user's self-picked
    /// <see cref="Organisation"/> id (from <c>GET /api/v1/app/organisations</c>).
    /// Optional; the service rejects an unknown / inactive id.</summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>B3 — D-221 (الجنس): the user's gender. Optional —
    /// <see cref="Gender.Unspecified"/> when not picked.</summary>
    public Gender Gender { get; set; }
}

/// <summary>One country entry surfaced to the client picker.</summary>
public sealed record CountryDto(string Code, string Name, string NameArabic);

/// <summary>The body of <c>GET /api/v1/app/account/user-profile/countries</c>.</summary>
public sealed record CountryListResponse(IReadOnlyList<CountryDto> Countries);
