// Tests: SIMF.Api.Tests/ContactsTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Contacts;

namespace SIMF.Api.Endpoints.Admin;

// SIMF-FDS-014 (D-261) — shared Contact directory admin CRUD + the CP
// "link existing contact" picker. Validation lives in the service (mirrors
// AdminOrganisationService); endpoints just gate + bind + delegate.
//
// CreateContactRequest / UpdateContactRequest carry no route id, so the {id}
// PUT reads the route GUID via Route<Guid>("id") (FAQ pattern). GET/DELETE bind
// the {id} route to this small DTO.
public sealed class ContactByIdRequest
{
    /// <summary>The contact's primary key, bound from the <c>{id}</c> route segment.</summary>
    public Guid Id { get; set; }
}

/// <summary><c>POST /admin/contacts/list</c> — paged admin grid over
/// <c>Contacts</c>.</summary>
public sealed class ListContactsEndpoint(IAdminContactService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminContactSummary>>>
{
    public override void Configure()
    {
        Post("/admin/contacts/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminContactSummary>>.Ok(
            await service.ListAsync(req, ct)), ct);
}

/// <summary><c>GET /admin/contacts/{id}</c> — a single contact's full detail.</summary>
public sealed class GetContactEndpoint(IAdminContactService service)
    : Endpoint<ContactByIdRequest, ApiResult<AdminContactDetail>>
{
    public override void Configure()
    {
        Get("/admin/contacts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ContactByIdRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.ContactNotFound, 404,
                "The contact was not found.",
                "لم يتم العثور على جهة الاتصال.");
        await Send.OkAsync(ApiResult<AdminContactDetail>.Ok(detail), ct);
    }
}

/// <summary><c>POST /admin/contacts</c> — create a new contact.</summary>
public sealed class CreateContactEndpoint(IAdminContactService service)
    : Endpoint<CreateContactRequest, ApiResult<AdminContactDetail>>
{
    public override void Configure()
    {
        Post("/admin/contacts");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(CreateContactRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminContactDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

/// <summary><c>PUT /admin/contacts/{id}</c> — update a contact. Reads the route
/// GUID via <c>Route&lt;Guid&gt;("id")</c> (FAQ pattern); the contract carries no id.</summary>
public sealed class UpdateContactEndpoint(IAdminContactService service)
    : Endpoint<UpdateContactRequest, ApiResult<AdminContactDetail>>
{
    public override void Configure()
    {
        Put("/admin/contacts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateContactRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminContactDetail>.Ok(
            await service.UpdateAsync(actorId, Route<Guid>("id"), req, ct)), ct);
    }
}

/// <summary><c>DELETE /admin/contacts/{id}</c> — soft-deactivate a contact.
/// Rejected (409) when the contact is still referenced by an active entity.</summary>
public sealed class DeactivateContactEndpoint(IAdminContactService service)
    : Endpoint<ContactByIdRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/contacts/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(ContactByIdRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await service.DeactivateAsync(actorId, req.Id, ct);
        await Send.OkAsync(ApiResult<bool>.Ok(true), ct);
    }
}

/// <summary><c>GET /admin/contacts/picker</c> — active-contact typeahead for the
/// CP "link existing contact" picker. Binds <c>search</c> + <c>top</c> (default 20)
/// query params.</summary>
public sealed class ContactPickerSearchRequest
{
    /// <summary>Free-text search over the contact name (Arabic / English); null returns the top rows.</summary>
    public string? Search { get; set; }

    /// <summary>Maximum number of picker items to return. Defaults to 20.</summary>
    public int Top { get; set; } = 20;
}

public sealed class ContactPickerEndpoint(IAdminContactService service)
    : Endpoint<ContactPickerSearchRequest, ApiResult<IReadOnlyList<ContactPickerItem>>>
{
    public override void Configure()
    {
        Get("/admin/contacts/picker");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Contacts.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(ContactPickerSearchRequest req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<IReadOnlyList<ContactPickerItem>>.Ok(
            await service.SearchPickerAsync(req.Search, req.Top, ct)), ct);
}
