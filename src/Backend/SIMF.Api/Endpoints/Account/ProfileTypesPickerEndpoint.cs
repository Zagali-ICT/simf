// Tests: SIMF.Api.Tests/ProfileTypePickerTests.cs
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Account;

/// <summary>D-190 (D-186 follow-up, mobile sign-up unblock) —
/// <c>GET /api/v1/app/account/profile-types</c> returns the active
/// ProfileType list the mobile sign-up Screen 2 dropdown reads.
///
/// <para>Auth required (the bog-standard <c>auth</c> rate-limit
/// bucket); <b>not</b> admin-only and <b>not</b> approval-gated — the
/// caller is mid-registration and has not yet been approved, so a
/// <c>RequireApprovedAccount</c> policy would lock them out of the
/// picker. The data is not sensitive (display names + colours) so
/// authenticated-but-pending is the right floor.</para>
///
/// <para>Optional <c>?isVisitor=true</c> / <c>?isVisitor=false</c>
/// query lets the client filter audience-vs-partner server-side; null
/// returns every active row and the mobile filters client-side
/// (the row count is single-digit-to-low-double-digits at SIMF
/// scale, so either shape is fine).</para>
/// </summary>
public sealed class ProfileTypesPickerRequest
{
    /// <summary>Audience filter:
    /// <c>true</c> = audience profile types only (IsVisitor=true);
    /// <c>false</c> = partner profile types only (IsVisitor=false);
    /// <c>null</c> = no filter.</summary>
    public bool? IsVisitor { get; set; }
}

public sealed class ProfileTypesPickerEndpoint(SimfAppDbContext appDb)
    : Endpoint<ProfileTypesPickerRequest, ApiResult<ProfileTypePickerListResponse>>
{
    public override void Configure()
    {
        Get("/app/account/profile-types");
        Tags("Account");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Return active profile types for the sign-up picker (audience / partner / both).");
    }

    public override async Task HandleAsync(
        ProfileTypesPickerRequest req, CancellationToken ct)
    {
        // D-190: only Visitor-scope ProfileTypes are surfaced —
        // Admin-scope rows (if any are ever seeded) are never valid
        // for a self-registering user to pick. Combined with the
        // IsActive filter so soft-deleted rows never appear.
        var query = appDb.ProfileTypes
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (req.IsVisitor is { } flag)
        {
            query = query.Where(p => p.IsForVisitor == flag);
        }

        var rows = await query
            .OrderBy(p => p.Name)
            .Select(p => new ProfileTypePickerDto(
                p.Id, p.Name, p.NameArabic, p.PageColor, p.IsForVisitor))
            .ToArrayAsync(ct);

        await Send.OkAsync(
            ApiResult<ProfileTypePickerListResponse>.Ok(
                new ProfileTypePickerListResponse(rows)),
            ct);
    }
}
