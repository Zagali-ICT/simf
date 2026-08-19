// Tests: SIMF.Api.Tests/EmailTemplateAdminTests.cs
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Email;
using SIMF.Domain.Auditing;
using SIMF.Domain.Email;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Email;

/// <summary>CP admin service for the transactional email templates. The
/// catalogue owns the fixed set + defaults; the DB owns only admin overrides.
/// Save validates that the copy references no unknown token, upserts the override
/// row and bumps its version; reset deletes the override (reverting to the code
/// default).</summary>
internal sealed class AdminEmailTemplateService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminEmailTemplateService
{
    // Bounds the stored copy so an Edit admin cannot plant a multi-MB body that
    // is read + allocated on every OTP/reset render.
    // Subject aligns with the EF nvarchar(256) column; bodies stay nvarchar(max)
    // in EF but are capped here + in the CP textarea maxlength.
    private const int MaxSubjectLength = 256;
    private const int MaxBodyLength = 8000;

    // The keys this list accepts as a sort column and as a filter column, held
    // lower-cased because keys match case-insensitively: the grid sends its column
    // key verbatim in camelCase, exactly as it does for the queryable grid seam.
    private const string TypeKey = "type";
    private const string SubjectKey = "subject";
    private const string CustomisedKey = "customised";
    private const string VersionKey = "version";
    private const string UpdatedAtKey = "updatedat";

    private static readonly string[] GridKeys =
        [TypeKey, SubjectKey, CustomisedKey, VersionKey, UpdatedAtKey];

    /// <summary>The accepted keys in the camelCase form the grid sends, for the
    /// bilingual error naming them. Matches the columns EmailTemplatesList.razor
    /// renders.</summary>
    private const string GridKeyList = "type, subject, customised, version, updatedAt";

    /// <summary>
    /// One page of the template list, with the request's filters, search, sort and
    /// paging actually applied.
    ///
    /// <para>
    /// The query is composed here by hand rather than through <c>ApplyGrid</c> /
    /// <c>ToGridPageAsync</c>, and that is a deliberate choice rather than an
    /// oversight. This resource is not an <c>IQueryable</c>: the rows are the fixed
    /// code-side <see cref="EmailTemplateCatalog"/> left-joined to whatever
    /// override rows exist, so there is no entity set to compose onto. Running the
    /// seam over <c>AsQueryable()</c> would compile, and would be wrong: the seam
    /// matches with <c>string.Contains</c> and gets its case-insensitivity from the
    /// SQL Server column collation, so evaluated in memory instead of translated to
    /// SQL that same expression tree becomes an ORDINAL, case-SENSITIVE match. That
    /// would quietly make this the one grid in SIMF where filtering "otp" fails to
    /// find "SignInOtp".
    /// </para>
    ///
    /// <para>
    /// So the seam's SEMANTICS are reproduced instead of its code: an unknown sort
    /// or filter key is a bilingual 400 rather than a silently ignored clause, a
    /// value that will not parse is a 400 rather than a dropped predicate, matching
    /// is case-insensitive, the order always ends in a per-row-unique tiebreak, and
    /// <c>Total</c> is the count AFTER filtering. It previously returned
    /// <c>rows.Count</c> for a page it never actually paged, so the grid footer
    /// read "1-10 of 10" whatever was asked for.
    /// </para>
    /// </summary>
    public async Task<GridPage<AdminEmailTemplateSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var overrides = await appDbContext.EmailTemplates
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Type, cancellationToken);

        var matched = EmailTemplateCatalog.All
            .Select(def =>
            {
                var row = overrides.GetValueOrDefault(def.Type);
                return new AdminEmailTemplateSummary(
                    def.Type,
                    def.Type.ToString(),
                    row?.Subject ?? def.Subject,
                    row is not null,
                    row?.Version ?? 0,
                    row?.UpdatedAt);
            })
            .Where(RowTest(query))
            .ToList();

        var (skip, top) = query.ClampPage();

        return GridPage<AdminEmailTemplateSummary>.Of(
            Sort(matched, query).Skip(skip).Take(top).ToList(),
            matched.Count,
            skip,
            top);
    }

    /// <summary>Builds the row test: every requested filter ANDed, then the
    /// free-text search ORed across the two text columns.</summary>
    private static Func<AdminEmailTemplateSummary, bool> RowTest(GridQuery query)
    {
        var tests = new List<Func<AdminEmailTemplateSummary, bool>>();

        foreach (var (key, raw) in query.Filters)
        {
            var test = FilterFor(key, raw);
            if (test is not null)
            {
                tests.Add(test);
            }
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            tests.Add(row => Contains(row.TypeName, search) || Contains(row.Subject, search));
        }

        return row => tests.TrueForAll(test => test(row));
    }

    /// <summary>Resolves one filter key to its row test. A blank value is no
    /// filter, but the KEY is validated either way: a stale key is a client bug
    /// whether or not it carries a value on this particular request.</summary>
    private static Func<AdminEmailTemplateSummary, bool>? FilterFor(string key, string? raw)
    {
        var normalised = RequireKey(key, ErrorCodes.GridFilterKeyInvalid, "filterable");

        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return normalised switch
        {
            TypeKey => row => Contains(row.TypeName, value),
            SubjectKey => row => Contains(row.Subject, value),
            CustomisedKey => BoolTest(key, value),
            VersionKey => VersionTest(key, value),
            // RequireKey has already rejected anything else, so this is updatedAt.
            _ => DayTest(key, value),
        };
    }

    private static IEnumerable<AdminEmailTemplateSummary> Sort(
        List<AdminEmailTemplateSummary> rows, GridQuery query)
    {
        // No sort key: the catalogue's own declaration order, which the enum value
        // preserves. It is unique per row, so it is its own tiebreak.
        if (string.IsNullOrWhiteSpace(query.Sort))
        {
            return rows.OrderBy(row => row.Type);
        }

        var descending = query.SortDescending;

        return RequireKey(query.Sort, ErrorCodes.GridSortKeyInvalid, "sortable") switch
        {
            TypeKey => Order(rows, row => row.TypeName, StringComparer.OrdinalIgnoreCase, descending),
            SubjectKey => Order(rows, row => row.Subject, StringComparer.OrdinalIgnoreCase, descending),
            CustomisedKey => Order(rows, row => row.IsOverride, Comparer<bool>.Default, descending),
            VersionKey => Order(rows, row => row.Version, Comparer<int>.Default, descending),
            _ => Order(rows, row => row.UpdatedAt, Comparer<DateTime?>.Default, descending),
        };
    }

    /// <summary>Orders by one column and always appends the template type as a
    /// tiebreak. The tiebreak is unique per row, so rows that tie on the sorted
    /// column cannot swap places between two requests — which is what lets a row
    /// appear on two pages while another appears on none.</summary>
    private static IEnumerable<AdminEmailTemplateSummary> Order<TKey>(
        List<AdminEmailTemplateSummary> rows,
        Func<AdminEmailTemplateSummary, TKey> selector,
        IComparer<TKey> comparer,
        bool descending) =>
        (descending
            ? rows.OrderByDescending(selector, comparer)
            : rows.OrderBy(selector, comparer))
        .ThenBy(row => row.Type);

    /// <summary>Normalises a requested key and rejects one this list does not
    /// declare, naming the accepted set. A silently ignored sort flips the header
    /// arrow without moving a row; a silently ignored filter WIDENS the result set,
    /// which reads as data rather than as a fault.</summary>
    private static string RequireKey(string key, string errorCode, string capability)
    {
        var normalised = key.Trim().ToLowerInvariant();
        if (Array.IndexOf(GridKeys, normalised) >= 0)
        {
            return normalised;
        }

        var english =
            $"'{key}' is not a {capability} column on this list. Columns: {GridKeyList}.";
        var arabic =
            $"العمود '{key}' غير متاح في هذه القائمة. الأعمدة المتاحة: {GridKeyList}.";

        throw new ApiException(
            errorCode, 400, english, arabic,
            [new ApiErrorDetail { Field = key, Message = english, MessageArabic = arabic }]);
    }

    private static bool Contains(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static Func<AdminEmailTemplateSummary, bool> BoolTest(string key, string raw) =>
        bool.TryParse(raw, out var wanted)
            ? row => row.IsOverride == wanted
            : throw GridFilters.ValueInvalid(key, raw, "true or false");

    private static Func<AdminEmailTemplateSummary, bool> VersionTest(string key, string raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wanted)
            ? row => row.Version == wanted
            : throw GridFilters.ValueInvalid(key, raw, "a whole number");

    /// <summary>A date filter names a calendar DAY, half-open [d, d+1), so a row
    /// stamped 14:30 on that day matches. Saudi wall-clock, per GridFilters.</summary>
    private static Func<AdminEmailTemplateSummary, bool> DayTest(string key, string raw)
    {
        var day = GridFilters.ParseDay(key, raw);
        return row => row.UpdatedAt >= day && row.UpdatedAt < day.AddDays(1);
    }

    public async Task<AdminEmailTemplateDetail> GetAsync(
        EmailTemplateType type, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.EmailTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);
        return ToDetail(type, row);
    }

    public async Task<AdminEmailTemplateDetail> UpdateAsync(
        Guid actorUserId,
        EmailTemplateType type,
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var subject = (request.Subject ?? string.Empty).Trim();
        var bodyEn = (request.BodyEn ?? string.Empty).Trim();
        var bodyAr = (request.BodyAr ?? string.Empty).Trim();

        if (subject.Length == 0 || bodyEn.Length == 0 || bodyAr.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                "Subject and both language bodies are required.",
                "العنوان ونص الرسالة بكلتا اللغتين مطلوبة.");
        }

        if (subject.Length > MaxSubjectLength
            || bodyEn.Length > MaxBodyLength
            || bodyAr.Length > MaxBodyLength)
        {
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                $"The subject must be at most {MaxSubjectLength} characters and each body at most {MaxBodyLength}.",
                $"يجب ألا يتجاوز العنوان {MaxSubjectLength} حرفًا وكل نص {MaxBodyLength} حرفًا.");
        }

        var unknown = UnknownTokens(type, subject, bodyEn, bodyAr);
        if (unknown.Count > 0)
        {
            var names = string.Join(", ", unknown.Select(t => "{" + t + "}"));
            throw new ApiException(
                ErrorCodes.EmailTemplateInvalid, 400,
                $"The template references unknown placeholders: {names}.",
                $"يشير القالب إلى عناصر نائبة غير معروفة: {names}.");
        }

        var now = timeProvider.SimfNow();
        var row = await appDbContext.EmailTemplates
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);

        if (row is null)
        {
            row = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                Type = type,
                Subject = subject,
                BodyEn = bodyEn,
                BodyAr = bodyAr,
                IsActive = request.IsActive,
                Version = 1,
                CreatedAt = now,
                UpdatedByUserId = actorUserId,
            };
            appDbContext.EmailTemplates.Add(row);
        }
        else
        {
            row.Subject = subject;
            row.BodyEn = bodyEn;
            row.BodyAr = bodyAr;
            row.IsActive = request.IsActive;
            row.Version++;
            row.UpdatedAt = now;
            row.UpdatedByUserId = actorUserId;
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.EmailTemplateUpdated,
            actorUserId,
            JsonSerializer.Serialize(new
            {
                type = type.ToString(),
                version = row.Version,
            }),
            cancellationToken);

        return ToDetail(type, row);
    }

    public async Task<AdminEmailTemplateDetail> ResetAsync(
        Guid actorUserId,
        EmailTemplateType type,
        CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.EmailTemplates
            .SingleOrDefaultAsync(t => t.Type == type, cancellationToken);

        if (row is not null)
        {
            appDbContext.EmailTemplates.Remove(row);
            await appDbContext.SaveChangesAsync(cancellationToken);

            await auditLog.WriteSuccessAsync(
                AuditEvents.EmailTemplateReset,
                actorUserId,
                JsonSerializer.Serialize(new { type = type.ToString() }),
                cancellationToken);
        }

        return ToDetail(type, null);
    }

    public EmailTemplatePreviewResult Preview(
        EmailTemplateType type, PreviewEmailTemplateRequest request)
    {
        var subject = request.Subject ?? string.Empty;
        var bodyEn = request.BodyEn ?? string.Empty;
        var bodyAr = request.BodyAr ?? string.Empty;

        var samples = EmailTemplateCatalog.Default(type).Tokens
            .ToDictionary(t => t.Name, t => t.Sample, StringComparer.OrdinalIgnoreCase);

        var html = EmailTemplateRenderer.ComposeBody(
            EmailTemplateRenderer.Render(bodyEn, samples),
            EmailTemplateRenderer.Render(bodyAr, samples));

        return new EmailTemplatePreviewResult(
            // Plain, not HTML-encoded - the preview must show the subject the
            // recipient's mail client will actually display.
            EmailSubjectRenderer.Render(subject, samples),
            html,
            UnknownTokens(type, subject, bodyEn, bodyAr));
    }

    private static IReadOnlyList<string> UnknownTokens(
        EmailTemplateType type, string subject, string bodyEn, string bodyAr)
    {
        var known = EmailTemplateCatalog.KnownTokenNames(type);
        return EmailTemplateRenderer.FindUnknownTokens(subject, known)
            .Concat(EmailTemplateRenderer.FindUnknownTokens(bodyEn, known))
            .Concat(EmailTemplateRenderer.FindUnknownTokens(bodyAr, known))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdminEmailTemplateDetail ToDetail(EmailTemplateType type, EmailTemplate? row)
    {
        var def = EmailTemplateCatalog.Default(type);
        var tokens = def.Tokens
            .Select(t => new EmailTemplateTokenDto(t.Name, t.DisplayEn, t.DisplayAr, t.Sample))
            .ToList();

        return new AdminEmailTemplateDetail(
            type,
            type.ToString(),
            row?.Subject ?? def.Subject,
            row?.BodyEn ?? def.BodyEn,
            row?.BodyAr ?? def.BodyAr,
            row?.IsActive ?? true,
            row is not null,
            row?.Version ?? 0,
            def.Subject,
            def.BodyEn,
            def.BodyAr,
            tokens,
            row?.UpdatedAt);
    }
}
