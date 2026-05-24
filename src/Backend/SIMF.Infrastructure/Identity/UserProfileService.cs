// Tests: SIMF.Api.Tests/UserProfileTests.cs (upsert round-trip, ID image
//        round-trip, get-empty-when-not-saved-yet, nationality-unknown)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// User self-service profile + encrypted ID-document storage (decisions
/// D-046 b, P8 — D-049; renamed from <c>VisitorProfileService</c>). The
/// actor identity is taken from the access token (the endpoint resolves
/// <c>sub</c>); every call operates on the actor's own row, so the
/// service does not need an admin-vs-self check.
/// </summary>
internal sealed class UserProfileService(
    UserManager<SimfUser> userManager,
    SimfIdentityDbContext dbContext,
    IUserIdDocumentStorage idStorage,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<UserProfileService> logger) : IUserProfileService
{
    public async Task<UserProfileResponse> GetMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(actorUserId.ToString())
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);

        if (profile is null)
        {
            // Empty response — the user has not filled the form yet.
            // The QR id is surfaced anyway (it lives on SimfUser, minted
            // on Approved per D-046 a) so the page can show it next to
            // the empty form.
            return new UserProfileResponse { QrId = user.QrId };
        }

        return ToResponse(profile, user.QrId);
    }

    public async Task<UserProfileResponse> UpsertMineAsync(
        Guid actorUserId,
        UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the nationality against the curated list — an unmatched
        // code is rejected here even though the validator already checks.
        // (Defence in depth — a future caller that bypasses the validator
        // still cannot persist garbage.)
        if (!Countries.IsKnown(request.NationalityCode))
        {
            throw new DataValidationException(
                $"Nationality code '{request.NationalityCode}' is not supported.",
                $"الجنسية '{request.NationalityCode}' غير مدعومة.");
        }

        var user = await userManager.FindByIdAsync(actorUserId.ToString())
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        var now = timeProvider.GetUtcNow();
        var profile = await dbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);

        var isNew = profile is null;
        // P8 — the admin may have created a stub row with a ProfileTypeId
        // already set (e.g. via /admin/others). Preserve it; the user
        // cannot self-pick a profile type on the upsert.
        profile ??= new UserProfile { UserId = actorUserId, CreatedAt = now };

        profile.ArabicName = request.ArabicName;
        profile.EnglishName = request.EnglishName;
        profile.NationalityCode = request.NationalityCode.ToUpperInvariant();
        profile.DateOfBirth = request.DateOfBirth;
        profile.PlaceOfBirth = request.PlaceOfBirth;
        profile.IsSaudi = request.IsSaudi;
        profile.NationalId = request.IsSaudi ? request.NationalId : null;
        profile.IqamaNumber = request.IsSaudi ? null : request.IqamaNumber;
        profile.PassportNumber = request.IsSaudi ? null : request.PassportNumber;
        profile.SaudiMobile = NormaliseOptional(request.SaudiMobile);
        profile.InternationalMobile = NormaliseOptional(request.InternationalMobile);
        if (!isNew)
        {
            profile.UpdatedAt = now;
        }

        if (isNew)
        {
            dbContext.UserProfiles.Add(profile);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileSaved,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = isNew ? "created" : "updated",
        }, cancellationToken);

        logger.LogInformation(
            "User profile {Operation} for {UserId}",
            isNew ? "created" : "updated", actorUserId);

        return ToResponse(profile, user.QrId);
    }

    public async Task UploadIdImageAsync(
        Guid actorUserId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(actorUserId.ToString())
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على الحساب.");

        // ID image follows the avatar contract (D-039): magic-byte and
        // size already checked at the endpoint, the storage layer
        // encrypts and writes.
        var profile = await dbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);
        if (profile is null)
        {
            // ID image only makes sense alongside a profile row — create
            // a stub so the relative path has somewhere to live.
            profile = new UserProfile
            {
                UserId = actorUserId,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            dbContext.UserProfiles.Add(profile);
        }

        var relativePath = await idStorage.SaveAsync(
            actorUserId, content, contentType, cancellationToken);
        profile.IdImageRelativePath = relativePath;
        profile.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserProfileIdImageUploaded,
            Outcome = AuditOutcome.Success,
            SubjectUserId = actorUserId,
            SubjectEmail = user.Email,
            ActorUserId = actorUserId,
            Detail = $"{content.Length} bytes, {contentType}",
        }, cancellationToken);
    }

    public async Task<UserIdDocumentImage?> ReadIdImageAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == actorUserId, cancellationToken);
        if (profile is null || string.IsNullOrEmpty(profile.IdImageRelativePath))
        {
            return null;
        }
        var read = await idStorage.OpenReadAsync(profile.IdImageRelativePath, cancellationToken);
        return read is null ? null : new UserIdDocumentImage(read.Content, read.ContentType);
    }

    private static UserProfileResponse ToResponse(UserProfile profile, string? qrId) =>
        new()
        {
            ProfileTypeId = profile.ProfileTypeId,
            ArabicName = profile.ArabicName,
            EnglishName = profile.EnglishName,
            NationalityCode = profile.NationalityCode,
            DateOfBirth = profile.DateOfBirth,
            PlaceOfBirth = profile.PlaceOfBirth,
            IsSaudi = profile.IsSaudi,
            NationalId = profile.NationalId,
            IqamaNumber = profile.IqamaNumber,
            PassportNumber = profile.PassportNumber,
            SaudiMobile = profile.SaudiMobile,
            InternationalMobile = profile.InternationalMobile,
            HasIdImage = !string.IsNullOrEmpty(profile.IdImageRelativePath),
            QrId = qrId,
        };

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
