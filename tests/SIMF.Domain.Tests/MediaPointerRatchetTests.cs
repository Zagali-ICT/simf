using System.Reflection;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// Guards the one-store rule: a domain entity refers to a file by a typed
/// <c>Guid? XFileId</c> keyed to <c>StoredFiles</c>, never by a string a person
/// typed. A string pointer carries none of what the file table carries — media
/// type, service policy, sensitivity tier, retention, owner — and nothing can
/// join on it.
///
/// <para>This is reflection over the domain assembly rather than a grep, because
/// the naming convention is exactly what a bypass would not follow: the columns
/// that started this programme were found by grepping <c>*RelativePath</c>, and
/// the three worst offenders were called <c>ImageUrl</c>, <c>Url</c> and
/// <c>BackgroundVideoUrl</c>.</para>
///
/// <para>Social and navigation links are NOT media and are deliberately absent
/// from the rules below — a sponsor's website, a banner's click-through, a
/// notification's deep link and the seven social columns are addresses of pages,
/// not of files. <c>StoredFile.ExternalUrl</c> is the store itself: it is where a
/// URL is SUPPOSED to live, and is the reason an externally hosted image needs no
/// exception anywhere else.</para>
/// </summary>
public sealed class MediaPointerRatchetTests
{
    /// <summary>A property is a file pointer if its name ends this way, whatever
    /// the entity calls itself.</summary>
    private static readonly string[] PointerSuffixes = ["RelativePath", "StoredFileName"];

    /// <summary>Media URL columns, named individually. A blanket "ends with Url"
    /// rule would sweep up every social and navigation link, so the media ones are
    /// listed as <c>Entity.Property</c> and everything else is left alone.</summary>
    private static readonly string[] MediaUrlProperties =
    [
        "ArchiveMediaItem.Url",
        "MediaItem.Url",
        "Session.LiveStreamUrl",
        "Session.LiveSignLanguageUrl",
        "SessionSummary.SummaryVideoUrl",
        "OrganizationProfile.LiveStreamUrl",
        "OrganizationProfile.BackgroundVideoUrl",
        "Banner.ImageUrl",
    ];

    /// <summary>Empty, and that is the point: every column the programme set out
    /// to convert is converted. It stays here as a hook rather than being deleted,
    /// so a future conversion that has to land in stages can record its remainder
    /// and still keep the guard live in the meantime.</summary>
    private static readonly HashSet<string> KnownRemaining = new(StringComparer.Ordinal);

    [Fact]
    public void No_new_media_pointer_string_appears_on_a_domain_entity()
    {
        var added = Offenders().Where(o => !KnownRemaining.Contains(o)).ToList();

        Assert.True(
            added.Count == 0,
            "These domain properties hold a media reference as a string. Store the "
            + "file in dbo.StoredFiles and point at it with a typed Guid? XFileId "
            + "instead — an external URL is not an exception, it becomes a "
            + "StoredFile of source type ExternalLink. Offenders: "
            + string.Join(", ", added));
    }

    [Fact]
    public void A_recorded_remainder_that_is_gone_is_deleted()
    {
        // Without this the list would quietly become a licence rather than a
        // countdown: a converted column could stay listed for ever and its slot
        // would silently re-admit a new string pointer of the same name.
        var live = Offenders().ToHashSet(StringComparer.Ordinal);
        var resolved = KnownRemaining.Where(entry => !live.Contains(entry)).ToList();

        Assert.True(
            resolved.Count == 0,
            "These entries are converted and must be removed from KnownRemaining: "
            + string.Join(", ", resolved));
    }

    private static IEnumerable<string> Offenders()
    {
        var media = MediaUrlProperties.ToHashSet(StringComparer.Ordinal);

        foreach (var type in typeof(SIMF.Domain.Files.StoredFile).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType != typeof(string))
                {
                    continue;
                }

                var qualified = $"{type.Name}.{property.Name}";
                var isPointer = PointerSuffixes.Any(suffix =>
                    property.Name.EndsWith(suffix, StringComparison.Ordinal));

                if (isPointer || media.Contains(qualified))
                {
                    yield return qualified;
                }
            }
        }
    }
}
