// Tests: SIMF.Api.Tests/SeatTierEligibilityTests.cs
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SIMF.Api.Endpoints.Account;
using SIMF.Api.Endpoints.Admin; // AuthorizationPolicies
using SIMF.Api.RequestContext;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Sessions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Api.Endpoints.Staff;

/// <summary>The STAFF SEATING DESK API. Exhibition
/// staff open the tablet seating screen and help a guest find their seat two
/// ways: scan the guest's badge QR ("where do I sit?") or tap a seat on the hall
/// grid ("who sits here?").
/// <para>Gated on <c>Seating.Assist</c>, an OPERATIONAL permission the Staff and
/// Moderator app roles already carry via
/// <c>PermissionCatalog.OperationalPermissionsForAppRole</c> — a partner-side
/// staff attendee reaches it with no admin RBAC role, exactly like the gate
/// console and the walk-in desk. The routes live under <c>/app/staff/</c>
/// (not <c>/admin/</c>) so they are app-surface, not Control-Panel.</para></summary>
public sealed class StaffSeatByBadgeRoute : StaffSeatBadgeLookupRequest
{
    public Guid SessionId { get; set; }
}

/// <summary><c>POST /app/staff/sessions/{sessionId}/seating/by-badge</c>.
/// Resolves a scanned badge QR id to where that guest is seated in this session.
/// <c>Found = false</c> (with the guest's identity still filled in) means the
/// badge is valid but holds no seat here; an unknown badge is 404
/// <c>ATTENDEE_QR_UNKNOWN</c>.</summary>
public sealed class StaffSeatByBadgeEndpoint(ISeatReservationService service)
    : Endpoint<StaffSeatByBadgeRoute, ApiResult<StaffSeatOccupant>>
{
    public override void Configure()
    {
        Post("/app/staff/sessions/{sessionId:guid}/seating/by-badge");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Seating.Assist),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Options(rb => rb.RequireRateLimiting(RateLimitOptions.OperationalPolicy));
        Tags("Staff");
        Summary(s => s.Summary =
            "Staff seating desk: resolve a guest's badge QR to their seat in this session.");
    }

    public override async Task HandleAsync(StaffSeatByBadgeRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<StaffSeatOccupant>.Ok(
            await service.ResolveBadgeSeatAsync(req.SessionId, req.QrId, ct)), ct);
}

public sealed class StaffSeatOccupantRoute
{
    public Guid SessionId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
}

/// <summary><c>GET /app/staff/sessions/{sessionId}/seating/seat</c>.
/// Resolves one seat to its occupant: the reservation reference, the guest's
/// bilingual name, whether a photo can be streamed, and — for a VVIP protocol
/// seat, which has no registration — the administrator's manual guest hint.
/// <c>Found = false</c> means the seat is free (a valid answer, not an
/// error).</summary>
public sealed class StaffSeatOccupantEndpoint(ISeatReservationService service)
    : Endpoint<StaffSeatOccupantRoute, ApiResult<StaffSeatOccupant>>
{
    public override void Configure()
    {
        Get("/app/staff/sessions/{sessionId:guid}/seating/seat");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Seating.Assist),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
        Summary(s => s.Summary =
            "Staff seating desk: resolve one seat to its occupant (or its VVIP guest hint).");
    }

    public override async Task HandleAsync(StaffSeatOccupantRoute req, CancellationToken ct) =>
        await Send.OkAsync(ApiResult<StaffSeatOccupant>.Ok(
            await service.ResolveSeatOccupantAsync(
                req.SessionId, req.RowLabel, req.SeatNumber, ct)), ct);
}

/// <summary><c>GET
/// /app/staff/sessions/{sessionId}/seating/occupant/{userId}/photo</c>. Streams
/// the seated guest's avatar bytes so the desk can visually confirm the person in
/// the seat. Reuses the shared <c>AvatarBytes</c> reader (unified StoredFile
/// store, decrypted on read) rather than a second copy of that logic.
/// <para>Authorisation is the SAME <c>Seating.Assist</c> permission as the seat
/// lookups, and the photo is only served for a user who actually HOLDS an active
/// reservation in this session — so the endpoint can never be used to enumerate
/// arbitrary attendees' photos.</para>
/// <para>The route segment is an ACCOUNT id and stays one: it is the shipped
/// shape the staff tablet builds from <c>StaffSeatOccupant.UserId</c>, and an
/// avatar belongs to an account in any case. The seat it is checked against is
/// held by the attendee's PROFILE, so the account is translated first — an
/// attendee with no account has no avatar to serve and never reaches here.</para></summary>
public sealed class StaffSeatOccupantPhotoEndpoint(
    SimfAppDbContext appDb,
    IFileService files)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/app/staff/sessions/{sessionId:guid}/seating/occupant/{userId:guid}/photo");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Seating.Assist),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Staff");
        Summary(s => s.Summary =
            "Staff seating desk: stream the seated guest's photo (authenticated bytes).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        User.RequireActor();

        var sessionId = Route<Guid>("sessionId");
        var userId = Route<Guid>("userId");
        var profileId = await appDb.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(ct);
        var holdsSeat = profileId is not null
            && await appDb.SeatReservations.AsNoTracking()
                .AnyAsync(r => r.SessionId == sessionId
                    && r.ReservedForProfileId == profileId
                    && r.ReleasedAt == null, ct);
        if (!holdsSeat)
        {
            // Not seated in this session → nothing to show. 404 (not 403) so the
            // endpoint reveals no more than "no photo here".
            await Send.NotFoundAsync(ct);
            return;
        }

        var avatar = await AvatarBytes.ReadAsync(files, userId, ct);
        if (avatar is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "private, max-age=300";
        await Send.StreamAsync(
            new MemoryStream(avatar.Value.Content),
            contentType: avatar.Value.ContentType, cancellation: ct);
    }
}
