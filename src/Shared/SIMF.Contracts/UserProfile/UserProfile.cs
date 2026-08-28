using SIMF.Common.Enums;

namespace SIMF.Contracts.UserProfile;

/// <summary>The body returned by <c>GET /api/v1/app/account/user-profile</c>.
/// When the user has not filled the
/// form yet, every field is empty / null except <see cref="QrId"/> (which
/// is present whenever the account state is Approved) and
/// <see cref="ProfileTypeId"/> (which is present when the admin has
/// already assigned a subtype).</summary>
public sealed class UserProfileResponse
{
    /// <summary>The <c>ProfileType</c> id, when one is assigned. It lives on
    /// the profile row, not on <c>SimfUser</c>.</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>The picked interest ids (الاهتمامات). Empty
    /// when the user has not filled the form yet; the validator requires
    /// 1-10 ids on every save.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    /// <summary>Optional job title.</summary>
    public string? JobTitle { get; set; }
    /// <summary>2026-07-20: Arabic twin of JobTitle so a visitor can set a
    /// bilingual title the app localizes (contacts / exhibitor cards / vCard).</summary>
    public string? JobTitleArabic { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }

    /// <summary>The optional Saudi vehicle plate (رقم اللوحة),
    /// stored as the canonical Latin "code" (Latin letters + Western digits,
    /// no separators). See <see cref="PlateNumberAr"/> / <see cref="PlateNumberEn"/>
    /// for the per-script renderings.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>The plate rendered in Arabic (Arabic letters +
    /// Arabic-Indic digits), derived from <see cref="PlateNumber"/>.</summary>
    public string? PlateNumberAr { get; set; }

    /// <summary>The plate rendered in English/Latin (the canonical
    /// code); same value as <see cref="PlateNumber"/>.</summary>
    public string? PlateNumberEn { get; set; }

    /// <summary>The registration reference (<c>SIMF-2026-00000001</c>),
    /// issued once at profile creation. Customer-facing lookup key; NOT the
    /// QR id.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>The picked <c>Organisation</c> id (الجهة),
    /// or null when the user has not picked one.</summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>The employer the user typed when theirs is not in the curated
    /// lookup, alongside <see cref="OrganisationId"/> pointing at the seeded
    /// "Other" row. Null for every ordinary pick.</summary>
    public string? OrganisationOther { get; set; }

    /// <summary>The picked region id (المنطقة), from
    /// <c>GET /api/v1/app/regions</c>, or null when the user has not picked
    /// one. Append-only field — the region pick is now persisted (it used to
    /// be discarded). The app already ships a region picker
    /// (<c>region_repository.dart</c>); this closes the write path.</summary>
    public Guid? RegionId { get; set; }

    /// <summary>The user's gender (الجنس);
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
    /// added with the two-photo split.)</summary>
    public bool HasAvatar { get; set; }

    /// <summary>The 12-character Crockford QR id; null when the account is
    /// not yet Approved.</summary>
    public string? QrId { get; set; }

    /// <summary>True when the account's assigned
    /// <c>ProfileType</c> is a VIP tier
    /// (<c>IsVipTier</c>, i.e. VVIP / VIP). It used to show the
    /// "request a speaker meeting" affordance; that gate is now
    /// <see cref="AllowsSpeakerMeeting"/> below, so do NOT read this one for
    /// meetings. What the tier still decides server-side is VIP-tier seat
    /// self-reservation. Append-only field.</summary>
    public bool IsVip { get; set; }

    /// <summary>Admin-assigned per-user eligibility to request a
    /// <b>speaker meeting</b>. Replaces <see cref="IsVip"/> as the meetings gate: the
    /// app shows the "request a speaker meeting" affordance when this is true (and the
    /// speaker has opted in), regardless of tier; the endpoint enforces the same rule
    /// server-side. Append-only field; defaults false so an older payload is not
    /// eligible.</summary>
    public bool AllowsSpeakerMeeting { get; set; }

    /// <summary>Admin-assigned per-user eligibility to request a
    /// <b>delegation (country) meeting</b>. Replaces the old delegate-only gate: the
    /// app shows the "request a delegation meeting" affordance when this is true,
    /// regardless of tier (the target country must still be an invited delegation).
    /// Append-only field; defaults false.</summary>
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>Whether this profile appears in "Meet People Like You"
    /// recommendations. Defaults to true; the user can opt out via the sign-up
    /// form or profile settings.</summary>
    public bool ShowInMeetLikeYou { get; set; }

    /// <summary>True when the assigned <c>ProfileType.IsForVisitor</c>
    /// is true (audience tiers: VVIP / VIP / Gold / Normal); false for the
    /// "Other" (partner / staff) tiers. The app uses it to show the
    /// "show me in Meet People Like You" opt-in only to "Other"-type users.
    /// Append-only field; defaults to true so an older payload treats the
    /// account as audience (opt-in hidden).</summary>
    public bool IsForVisitor { get; set; } = true;
}

/// <summary>The body posted to <c>POST /api/v1/app/account/user-profile</c>.
/// An upsert — first call creates the row, every later
/// call updates it. The validator enforces the field-shape rules.
/// <para><see cref="ProfileTypeId"/> is the
/// user's self-pick from the public
/// <c>GET /api/v1/app/account/profile-types</c> endpoint. Optional — if
/// the user submits without picking, the admin assigns one later via
/// the admin endpoints. The validator rejects unknown ids, inactive
/// rows, and Admin-scope rows. The service preserves any admin-
/// pre-assigned ProfileTypeId (admin pre-pick wins over user
/// self-pick).</para></summary>
public sealed class UpsertUserProfileRequest
{
    /// <summary>The user's self-picked
    /// <see cref="UserProfileResponse.ProfileTypeId"/>. Optional;
    /// admin pre-pick wins on conflict (see
    /// <c>UserProfileService.UpsertMineAsync</c>).</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>The picked interest ids. Required: 1-10
    /// active <c>UserInterest</c> ids; the validator rejects empties /
    /// duplicates / unknown ids / deactivated ids.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    /// <summary>Optional job title.</summary>
    public string? JobTitle { get; set; }
    /// <summary>2026-07-20: Arabic twin of JobTitle so a visitor can set a
    /// bilingual title the app localizes (contacts / exhibitor cards / vCard).</summary>
    public string? JobTitleArabic { get; set; }
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }

    /// <summary>Optional Saudi vehicle plate (رقم اللوحة). When
    /// present the validator requires 3 letters (Arabic or Latin) + 1–4
    /// digits, ≤ 7 chars after separators are stripped; the service stores
    /// the normalized value.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>The user's self-picked <c>Organisation</c> id (الجهة),
    /// from <c>GET /api/v1/app/organisations</c>.
    /// Optional; the service rejects an unknown / inactive id.</summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>The employer the user typed when theirs is not in the lookup.
    /// Only meaningful with <see cref="OrganisationId"/> set to the seeded
    /// "Other" row, which the picker flags as <c>isOther</c>; ignored
    /// otherwise. Max 150, matching the lookup's own name column so a value
    /// promoted into the list later cannot be truncated on the way.</summary>
    public string? OrganisationOther { get; set; }

    /// <summary>The user's self-picked region id (المنطقة), from
    /// <c>GET /api/v1/app/regions</c>. Optional; the service rejects an unknown /
    /// inactive id, exactly like <see cref="OrganisationId"/>.</summary>
    public Guid? RegionId { get; set; }

    /// <summary>The user's gender (الجنس). Optional —
    /// <see cref="Gender.Unspecified"/> when not picked.</summary>
    public Gender Gender { get; set; }

    /// <summary>Whether this profile appears in "Meet People Like You"
    /// recommendations. Null means "no change" (preserves current value on the
    /// server). The default server-side is <c>true</c>.</summary>
    public bool? ShowInMeetLikeYou { get; set; }
}

/// <summary>One country entry surfaced to the client picker.</summary>
public sealed record CountryDto(string Code, string Name, string NameArabic);

/// <summary>The body of <c>GET /api/v1/app/account/user-profile/countries</c>.</summary>
public sealed record CountryListResponse(IReadOnlyList<CountryDto> Countries);
