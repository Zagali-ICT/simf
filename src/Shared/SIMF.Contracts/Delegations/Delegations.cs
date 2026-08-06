namespace SIMF.Contracts.Delegations;

/// <summary>D-499 (Figma 1426:10771 الوفود) — the public delegations view: the
/// invited countries' delegations with the screen's two aggregate stats. Public /
/// anonymous (only the designated head + a member count are exposed, no member PII).</summary>
public sealed record AppDelegations(
    /// <summary>"دولة مشاركة" — the number of invited countries on display.</summary>
    int CountryCount,
    /// <summary>"إجمالي المشاركين" — the total delegate members across them.</summary>
    int TotalParticipants,
    IReadOnlyList<AppDelegationItem> Items);

/// <summary>One delegation card — an invited country with its head of delegation,
/// date range and member count.</summary>
public sealed record AppDelegationItem(
    int CountryId,
    /// <summary>ISO 3166-1 alpha-2 code ("SA") — the app derives the flag from it.</summary>
    string CountryCode,
    string CountryName,
    string CountryNameArabic,
    /// <summary>The head of delegation's name (رئيس الوفد); null when none is set.</summary>
    string? HeadName,
    string? HeadNameArabic,
    /// <summary>The head's job title / honorific shown under the name; null when none.</summary>
    string? HeadTitle,
    DateOnly? ArrivalDate,
    DateOnly? DepartureDate,
    int MemberCount,
    /// <summary>2026-07-19 (owner) — the Arabic twin of <see cref="HeadTitle"/>
    /// (from the head profile's JobTitleArabic ?? HonorificArabic), so the app
    /// renders the head title in the active locale. Null when unset. Append-only
    /// Older clients ignore it and keep <see cref="HeadTitle"/>.</summary>
    string? HeadTitleArabic = null);
