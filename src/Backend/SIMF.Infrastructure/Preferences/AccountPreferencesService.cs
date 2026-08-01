// Tests: SIMF.Api.Tests/AccountPreferencesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Preferences;
using SIMF.Common;
using SIMF.Contracts.Account;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Preferences;

/// <summary>
/// `accessibility-server-sync` — the five accessibility choices as account
/// preferences on the user's <c>UserProfile</c> row. Read is a five-column
/// projection (no PII columns touched, nothing tracked); write is a full
/// replace, so a repeated PUT stores the same row. D-157: the profile carries
/// the bare <c>UserId</c>, so nothing here crosses into the Identity DB.
/// </summary>
internal sealed class AccountPreferencesService(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider) : IAccountPreferencesService
{
    public async Task<AccountPreferences> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stored = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new AccountPreferences(
                profile.AccessibilityTextSize,
                profile.AccessibilityHighContrast,
                profile.AccessibilityReduceMotion,
                profile.AccessibilityScreenReaderAssist,
                profile.AccessibilityCaptions,
                profile.AccessibilityConfiguredAt != null))
            .SingleOrDefaultAsync(cancellationToken);

        // No profile row yet (the account signed up but has not filled the
        // registration form) — the choices simply have not been made, which is
        // the defaults, not a 404.
        if (stored is null)
        {
            return AccountPreferences.Defaults;
        }

        // A row written before this feature — or by the walk-in desk / the
        // ID-document stub path — can hold an empty text size. Hand the app a
        // name its enum can decode instead of one it would silently drop.
        return AccountPreferences.IsAllowedTextSize(stored.TextSize)
            ? stored
            : stored with { TextSize = AccountPreferences.DefaultTextSize };
    }

    public async Task<AccountPreferences> SaveMineAsync(
        Guid userId,
        UpdateAccountPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var textSize = request.TextSize ?? AccountPreferences.DefaultTextSize;
        if (!AccountPreferences.IsAllowedTextSize(textSize))
        {
            throw new DataValidationException(
                "One or more fields are invalid.",
                "يوجد حقل أو أكثر غير صالح.",
                [
                    new ApiErrorDetail
                    {
                        // The wire field name, as the app spells it — same
                        // camelCase convention as PasswordService's "newPassword".
                        Field = "textSize",
                        Message =
                            "Text size must be one of: "
                            + string.Join(", ", AccountPreferences.AllowedTextSizes) + ".",
                        MessageArabic =
                            "يجب أن يكون حجم الخط أحد القيم التالية: "
                            + string.Join("، ", AccountPreferences.AllowedTextSizes) + ".",
                    },
                ]);
        }

        var now = timeProvider.SimfNow();
        var profile = await appDbContext.UserProfiles
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken);

        if (profile is null)
        {
            // Same stub contract as the ID-document upload
            // (UserProfileService.UploadIdImageAsync): the preferences need a row
            // to live on. An empty-named stub still reads as incomplete to
            // IsProfileCompleteAsync, so seeding one here cannot flip a
            // half-registered account to "registered".
            profile = new UserProfile { UserId = userId, CreatedAt = now };
            appDbContext.UserProfiles.Add(profile);
        }
        else
        {
            profile.UpdatedAt = now;
        }

        profile.AccessibilityTextSize = textSize;
        profile.AccessibilityHighContrast = request.HighContrast;
        profile.AccessibilityReduceMotion = request.ReduceMotion;
        profile.AccessibilityScreenReaderAssist = request.ScreenReaderAssist;
        profile.AccessibilityCaptions = request.Captions;
        // Stamps the row as CHOSEN. Every later GET then answers configured:true, so
        // the app knows these values are the user's pick and may safely replace its
        // local cache with them.
        profile.AccessibilityConfiguredAt = now;

        await appDbContext.SaveChangesAsync(cancellationToken);

        return new AccountPreferences(
            textSize,
            request.HighContrast,
            request.ReduceMotion,
            request.ScreenReaderAssist,
            request.Captions,
            Configured: true);
    }
}
