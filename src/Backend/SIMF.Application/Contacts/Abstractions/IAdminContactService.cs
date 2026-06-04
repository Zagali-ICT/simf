using SIMF.Common;
using SIMF.Contracts.Contacts;

namespace SIMF.Application.Contacts.Abstractions;

/// <summary>Admin CRUD over the shared <c>Contact</c> directory (SIMF-FDS-014,
/// D-261). One Contact may be reused across roles; a Contact still referenced by
/// an active entity cannot be deactivated. Mirrors IAdminOrganisationService.</summary>
public interface IAdminContactService
{
    /// <summary>One page of contact summaries for the admin grid.</summary>
    Task<GridPage<AdminContactSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default);

    /// <summary>One contact by id, or null when it is missing.</summary>
    Task<AdminContactDetail?> GetAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>Creates a new contact and returns its full detail.</summary>
    Task<AdminContactDetail> CreateAsync(
        Guid actorUserId, CreateContactRequest request,
        CancellationToken ct = default);

    /// <summary>Updates an existing contact and returns its full detail.</summary>
    Task<AdminContactDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateContactRequest request,
        CancellationToken ct = default);

    /// <summary>Soft-deletes (deactivates) a contact. Rejected (409) when the
    /// contact is still referenced by an active Company / Sponsor / MediaPartner /
    /// Speaker / Booth.</summary>
    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken ct = default);

    /// <summary>Active-contact typeahead for the CP "link existing contact"
    /// picker. Returns at most <paramref name="top"/> lightweight items.</summary>
    Task<IReadOnlyList<ContactPickerItem>> SearchPickerAsync(
        string? search, int top, CancellationToken ct = default);
}
