// Tests: SIMF.Api.Tests/AdminSpeakersTests.cs
using System.Security.Claims;
using FastEndpoints;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>D-153 — admin CRUD over <c>Speakers</c> (SIMF-DAT-001 §5.4).
/// Mirrors ThemeEndpoints / HallEndpoints / CountryEndpoints shape.</summary>
public sealed class ListSpeakersEndpoint(IAdminSpeakerService service)
    : Endpoint<GridQuery, ApiResult<GridPage<AdminSpeakerSummary>>>
{
    public override void Configure()
    {
        Post("/admin/speakers/list");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GridQuery req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<GridPage<AdminSpeakerSummary>>.Ok(
            await service.ListAllAsync(req, ct)), ct);
}

public sealed class GetSpeakerRequest { public Guid Id { get; set; } }

public sealed class GetSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<GetSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Get("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(GetSpeakerRequest req, CancellationToken ct)
    {
        var detail = await service.GetAsync(req.Id, ct)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(detail), ct);
    }
}

public sealed class CreateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<AdminCreateSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Post("/admin/speakers");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Create),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(AdminCreateSpeakerRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(
            await service.CreateAsync(actorId, req, ct)), ct);
    }
}

public sealed class UpdateSpeakerRequest
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string? RankArabic { get; set; }
    public int? CountryId { get; set; }
    public Guid? UserProfileId { get; set; }
    public string? Bio { get; set; }
    public string? BioArabic { get; set; }
    public string? Qualifications { get; set; }
    public string? QualificationsArabic { get; set; }
    public string? TrainingExperience { get; set; }
    public string? TrainingExperienceArabic { get; set; }
    public string? Awards { get; set; }
    public string? AwardsArabic { get; set; }
    public bool AllowsMeetingRequests { get; set; }
    public bool AllowsDataSharing { get; set; }
    public string? FacebookUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Contact identity-card fields inlined from the removed Contact directory (D-766).
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? InstagramUrl { get; set; }
    public string? City { get; set; }
    public string? CityArabic { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed class UpdateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<UpdateSpeakerRequest, ApiResult<AdminSpeakerDetail>>
{
    public override void Configure()
    {
        Put("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Edit),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(UpdateSpeakerRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        await Send.OkAsync(ApiResult<AdminSpeakerDetail>.Ok(
            await service.UpdateAsync(actorId, req.Id,
                new AdminUpdateSpeakerRequest
                {
                    Code = req.Code,
                    Name = req.Name,
                    NameArabic = req.NameArabic,
                    Rank = req.Rank,
                    RankArabic = req.RankArabic,
                    CountryId = req.CountryId,
                    UserProfileId = req.UserProfileId,
                    Bio = req.Bio,
                    BioArabic = req.BioArabic,
                    Qualifications = req.Qualifications,
                    QualificationsArabic = req.QualificationsArabic,
                    TrainingExperience = req.TrainingExperience,
                    TrainingExperienceArabic = req.TrainingExperienceArabic,
                    Awards = req.Awards,
                    AwardsArabic = req.AwardsArabic,
                    AllowsMeetingRequests = req.AllowsMeetingRequests,
                    AllowsDataSharing = req.AllowsDataSharing,
                    FacebookUrl = req.FacebookUrl,
                    LinkedInUrl = req.LinkedInUrl,
                    XUrl = req.XUrl,
                    WebsiteUrl = req.WebsiteUrl,
                    DisplayOrder = req.DisplayOrder,
                    IsActive = req.IsActive,
                    Email = req.Email,
                    PhonePrimary = req.PhonePrimary,
                    PhoneSecondary = req.PhoneSecondary,
                    InstagramUrl = req.InstagramUrl,
                    City = req.City,
                    CityArabic = req.CityArabic,
                    Latitude = req.Latitude,
                    Longitude = req.Longitude,
                }, ct)), ct);
    }
}

public sealed class DeactivateSpeakerRequest { public Guid Id { get; set; } }

public sealed class DeactivateSpeakerEndpoint(IAdminSpeakerService service)
    : Endpoint<DeactivateSpeakerRequest, ApiResult<bool>>
{
    public override void Configure()
    {
        Delete("/admin/speakers/{id:guid}");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Speakers.Delete),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting("auth"));
        Tags("Admin");
    }

    public override async Task HandleAsync(DeactivateSpeakerRequest req, CancellationToken ct)
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
