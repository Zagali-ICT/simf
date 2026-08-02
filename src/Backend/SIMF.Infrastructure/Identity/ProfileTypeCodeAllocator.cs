// Tests: SIMF.Api.Tests/OfflineBadgeUploadTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-819 — allocates <see cref="UserProfileType.Code"/>, the small number the
/// printed badge carries in place of the type's Guid.
///
/// <para>Shared by the two places a profile type is born (the CP create endpoint
/// and the first-run seeder) because getting it wrong is silent: a type with
/// code 0 is invisible to the offline desk, and a REUSED code would make a
/// retired badge read as a different type on a scanner that is offline and
/// cannot check the database.</para>
///
/// <para>Allocation is max-over-every-row-plus-one, deliberately including
/// inactive types, so a deactivated type's code is never handed out again even
/// though the unique index would allow it.</para>
/// </summary>
internal static class ProfileTypeCodeAllocator
{
    public static async Task<short> NextAsync(
        SimfAppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var highest = await dbContext.ProfileTypes
            .AsNoTracking()
            .MaxAsync(type => (short?)type.Code, cancellationToken) ?? 0;
        if (highest >= short.MaxValue)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeInvalidUserType, 409,
                "No badge code is available for a new profile type.",
                "لا يوجد رمز بطاقة متاح لنوع ملف شخصي جديد.");
        }
        return (short)(highest + 1);
    }
}
