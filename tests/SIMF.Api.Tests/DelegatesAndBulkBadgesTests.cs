// Tests: D-473 (#10) — delegates (وفد) + bulk-generate placeholder badges.
//        D-751 (#10) — bulk-badge organiser email delivery (ZIP of QR PNGs).
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Organisations;
using SIMF.Domain.Badges;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-473 (#10) — a delegate is a normal visitor with <c>IsDelegate</c> set and an
/// invited country; plus the bulk-generate of placeholder badges by profile type.
/// </summary>
public sealed class DelegatesAndBulkBadgesTests : IClassFixture<BulkBadgeEmailApiFactory>
{
    private readonly BulkBadgeEmailApiFactory _factory;
    private readonly HttpClient _client;

    public DelegatesAndBulkBadgesTests(BulkBadgeEmailApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Walk_in_delegate_with_an_invited_country_succeeds_and_flags_the_profile()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("SA", invited: true);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "SA", isDelegate: true),
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == body.Data!.UserId);
        Assert.True(profile.IsDelegate);
    }

    [Fact]
    public async Task Walk_in_delegate_with_a_non_invited_country_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("US", invited: false);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "US", isDelegate: true),
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.Equal(ErrorCodes.DelegateCountryNotInvited, body.Error!.Code);
    }

    [Fact]
    public async Task A_non_delegate_walk_in_is_not_constrained_to_invited_countries()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("US", invited: false);

        // A plain (non-delegate) visitor from a non-invited country is fine.
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "US", isDelegate: false),
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_generate_creates_badges_per_type_with_qr_and_the_delegate_flag()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A dedicated profile type so the assertion sees only this batch's badges.
        var profileTypeId = await FreshVisitorProfileTypeAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                IsDelegate = true,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileTypeId, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(3, body.Data!.Created);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles
            .Where(p => p.ProfileTypeId == profileTypeId)
            .ToListAsync();
        Assert.Equal(3, badges.Count);
        Assert.All(badges, b => Assert.True(b.IsDelegate));
        // Every generated badge is Approved with a minted QR (ready to hand out).
        Assert.All(badges, b => Assert.False(string.IsNullOrEmpty(b.QrId)));
    }

    // -- Top-up ---------------------------------------------------------------
    // TopUpBadgeBatchAsync and its fold had NO coverage: the Control Panel test
    // stubs the HTTP layer, so it passed whether or not the two 409 guards below
    // existed. The fold used to parse the order's own " × "-delimited prose back
    // into counts; it merges child rows by profile-type id now, and either way it
    // is not code to leave unpinned.

    [Fact]
    public async Task Top_up_mints_more_badges_and_folds_a_repeated_tier_into_the_summary()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var normal = await NamedVisitorProfileTypeAsync("TopUpNormal");
        var vip = await NamedVisitorProfileTypeAsync("TopUpVip");

        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Ministry of Interior Team",
                NameArabic = "فريق وزارة الداخلية",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = normal.Id, Count = 4 },
                },
            },
            admin);
        var batchId = await BatchIdForTypeAsync(normal.Id);

        // A tier the order does not hold yet is APPENDED.
        var appended = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = batchId,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = vip.Id, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, appended.StatusCode);
        var appendedBody = (await appended.Content
            .ReadFromJsonAsync<ApiResult<AdminTopUpBadgeBatchResponse>>())!;
        Assert.Equal(3, appendedBody.Data!.Added);
        Assert.Equal(7, appendedBody.Data.TotalCount);

        // A tier it ALREADY holds is folded into that entry rather than appended
        // as a second one, or the breakdown grows a new term per top-up.
        var folded = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = batchId,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = normal.Id, Count = 2 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, folded.StatusCode);
        var foldedBody = (await folded.Content
            .ReadFromJsonAsync<ApiResult<AdminTopUpBadgeBatchResponse>>())!;
        Assert.Equal(2, foldedBody.Data!.Added);
        Assert.Equal(9, foldedBody.Data.TotalCount);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // The order holds ONE line per tier, folded in place rather than a second
        // line for a tier it already had, and in the order the lines were entered.
        var lines = await appDb.BadgeBatchItems
            .AsNoTracking()
            .Where(item => item.BadgeBatchId == batchId)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Equal((normal.Id, 6), (lines[0].ProfileTypeId, lines[0].Count));
        Assert.Equal((vip.Id, 3), (lines[1].ProfileTypeId, lines[1].Count));
        Assert.Equal(9, lines.Sum(item => item.Count));

        var members = await appDb.UserProfiles
            .Where(p => p.BadgeBatchId == batchId)
            .ToListAsync();
        Assert.Equal(9, members.Count);
        // Minted immediately, so the order's total always equals badges that exist.
        Assert.All(members, m => Assert.False(string.IsNullOrEmpty(m.QrId)));
    }

    [Fact]
    public async Task Top_up_of_a_revoked_order_is_refused()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileType = await NamedVisitorProfileTypeAsync("TopUpRevoked");

        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Revoked order",
                NameArabic = "طلب ملغى",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileType.Id, Count = 2 },
                },
            },
            admin);
        var batchId = await BatchIdForTypeAsync(profileType.Id);

        await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/revoke",
            new AdminRevokeBadgeBatchRequest { BatchId = batchId },
            admin);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = batchId,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileType.Id, Count = 1 },
                },
            },
            admin);

        // Revoking disabled every attendee in the order, so minting more into it
        // would hand out badges the door is already refusing.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(
            2,
            await appDb.UserProfiles.CountAsync(p => p.BadgeBatchId == batchId));
    }

    [Fact]
    public async Task Top_up_of_the_direct_registration_order_is_refused()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileType = await NamedVisitorProfileTypeAsync("TopUpDirect");
        var before = await DirectRegistrationMemberCountAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = BadgeBatch.DirectRegistrationId,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileType.Id, Count = 1 },
                },
            },
            admin);

        // Everyone who registered themselves is filed against this order. It is
        // not something badges are ordered against, and minting into it would
        // invent attendees nobody asked for.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await DirectRegistrationMemberCountAsync());
    }

    [Fact]
    public async Task The_list_carries_a_bilingual_breakdown_but_not_for_direct_registration()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var normal = await NamedVisitorProfileTypeAsync("ListNormal");

        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Breakdown order",
                NameArabic = "طلب التفصيل",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = normal.Id, Count = 2 },
                },
            },
            admin);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/list", new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>())!.Data!;

        // A real order carries the tiers, with BOTH names, so the reader renders in
        // the language it is reading rather than being handed English prose.
        var order = page.Items.Single(row => row.Name == "Breakdown order");
        var tier = Assert.Single(order.Tiers!);
        Assert.Equal(normal.Name, tier.Name);
        Assert.Equal("نوع اختباري", tier.NameArabic);
        Assert.Equal(2, tier.Count);

        // The direct-registration order does NOT. It is not a badge order - it is
        // where self-registrations are filed - so its summary stays prose instead of
        // growing one entry per profile type as the event runs.
        var direct = page.Items.SingleOrDefault(
            row => row.Id == BadgeBatch.DirectRegistrationId);
        if (direct is not null)
        {
            Assert.True(
                direct.Tiers is null || direct.Tiers.Count == 0,
                "the direct-registration order must keep its prose summary");
        }
    }

    private async Task<int> DirectRegistrationMemberCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.UserProfiles
            .CountAsync(p => p.BadgeBatchId == BadgeBatch.DirectRegistrationId);
    }

    /// <summary>A fresh visitor profile type with a KNOWN name, because the name
    /// is what CountsSummary renders and the fold assertion reads back.</summary>
    private async Task<(Guid Id, string Name)> NamedVisitorProfileTypeAsync(string prefix)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var name = $"{prefix}{Guid.NewGuid():N}"[..20];
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = "نوع اختباري",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return (fresh.Id, name);
    }

    [Fact]
    public async Task Bulk_generate_rejects_an_empty_request_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest { Name = "Test order", NameArabic = "طلب اختباري", Batches = new List<BulkBadgeBatch>() },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_generate_with_an_invalid_second_batch_writes_zero_accounts_and_400s()
    {
        // Fix #6 (data-integrity): every batch's profile type is validated BEFORE
        // any account is created, so an invalid later batch is a clean 400 with
        // nothing persisted — no partial write under a failure envelope.
        var admin = await CreateAdministratorAndSignInAsync();
        var validProfileTypeId = await FreshVisitorProfileTypeAsync();
        var missingProfileTypeId = Guid.NewGuid(); // no such profile type exists

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                IsDelegate = false,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = validProfileTypeId, Count = 3 },
                    new() { ProfileTypeId = missingProfileTypeId, Count = 1 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);

        // The valid first batch must NOT have been committed — zero badges written.
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles
            .Where(p => p.ProfileTypeId == validProfileTypeId)
            .ToListAsync();
        Assert.Empty(badges);
    }

    [Fact]
    public async Task Bulk_generate_with_a_recipient_email_enqueues_one_zip_of_all_badge_pngs()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // Two fresh types so the ZIP must carry the SUM of both counts (2 + 3 = 5).
        var typeA = await FreshVisitorProfileTypeAsync();
        var typeB = await FreshVisitorProfileTypeAsync();
        var recipient = $"organiser-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                IsDelegate = false,
                RecipientEmail = recipient,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeA, Count = 2 },
                    new() { ProfileTypeId = typeB, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(5, body.Data!.Created);
        Assert.True(body.Data!.EmailQueued);

        // Exactly one message to this (unique) recipient, carrying one ZIP whose
        // entries are one PNG per generated badge (sum of the batch counts).
        var messages = _factory.Emails.Messages.Where(m => m.To == recipient).ToList();
        var message = Assert.Single(messages);
        // D-759 (#10 Phase 3) — the pack now carries BOTH a ZIP of PNGs and a PDF sheet.
        Assert.Equal(2, message.Attachments!.Count);
        var attachment = Assert.Single(message.Attachments!, a => a.ContentType == "application/zip");
        Assert.StartsWith("badges-", attachment.FileName);
        Assert.EndsWith(".zip", attachment.FileName);
        var pdf = Assert.Single(message.Attachments!, a => a.ContentType == "application/pdf");
        Assert.EndsWith(".pdf", pdf.FileName);
        // The PDF is a real, non-empty PDF (magic bytes "%PDF").
        Assert.True(pdf.Content.Length > 4);
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, pdf.Content.Take(4).ToArray());

        using var zip = new ZipArchive(new MemoryStream(attachment.Content), ZipArchiveMode.Read);
        Assert.Equal(5, zip.Entries.Count);
        Assert.All(zip.Entries, entry => Assert.EndsWith(".png", entry.Name));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            // Each entry is a real, non-empty PNG (magic bytes 89 50 4E 47).
            Assert.True(bytes.Length > 8);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
        }
    }

    [Fact]
    public async Task Bulk_generate_with_an_invalid_recipient_email_is_400_and_writes_zero_accounts()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        var attachmentEmailsBefore =
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 });

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                IsDelegate = false,
                RecipientEmail = "not-an-email",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeId, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);

        // A 4xx must have no side effects — zero badges written, no email enqueued.
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles.Where(p => p.ProfileTypeId == typeId).ToListAsync();
        Assert.Empty(badges);
        Assert.Equal(attachmentEmailsBefore,
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 }));
    }

    [Fact]
    public async Task Bulk_generate_without_a_recipient_email_enqueues_nothing()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        var attachmentEmailsBefore =
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 });

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                IsDelegate = false,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeId, Count = 2 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(2, body.Data!.Created);
        Assert.False(body.Data!.EmailQueued);
        // Back-compat: no attachment-bearing email was enqueued by this path.
        Assert.Equal(attachmentEmailsBefore,
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 }));
    }

    // -- D-758 (#10 Phase 2) — persisted batch + list / re-email / revoke ------

    [Fact]
    public async Task Bulk_generate_persists_a_batch_and_stamps_each_profile()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = typeId, Count = 2 } },
            },
            admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles.Where(p => p.ProfileTypeId == typeId).ToListAsync();
        Assert.Equal(2, badges.Count);
        // Every badge back-references the same, persisted batch row.
        var batchId = Assert.Single(badges.Select(b => b.BadgeBatchId).Distinct());
        Assert.NotEqual(BadgeBatch.DirectRegistrationId, batchId);
        var batch = await appDb.BadgeBatches
            .Include(b => b.Items)
            .SingleAsync(b => b.Id == batchId);
        Assert.True(batch.IsActive);
        // What the order holds is a child row, not a rendered string, so the count
        // can be read back as a number instead of parsed out of prose.
        var line = Assert.Single(batch.Items);
        Assert.Equal(typeId, line.ProfileTypeId);
        Assert.Equal(2, line.Count);
    }

    [Fact]
    public async Task List_badge_batches_includes_a_generated_batch()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = typeId, Count = 1 } },
            },
            admin);

        var batchId = await BatchIdForTypeAsync(typeId);

        var listResponse = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/list", new GridQuery { Top = 200 }, admin);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = (await listResponse.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>())!;
        Assert.Contains(page.Data!.Items,
            b => b.Id == batchId && b.TotalCount == 1 && b.IsActive);
    }

    [Fact]
    public async Task Re_email_a_batch_enqueues_a_fresh_zip_to_the_recipient()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = typeId, Count = 3 } },
            },
            admin);
        var batchId = await BatchIdForTypeAsync(typeId);

        var recipient = $"reemail-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/re-email",
            new AdminReEmailBadgeBatchRequest { BatchId = batchId, RecipientEmail = recipient },
            admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminReEmailBadgeBatchResponse>>())!;
        Assert.Equal(3, body.Data!.BadgeCount);
        Assert.True(body.Data!.EmailQueued);

        var message = Assert.Single(_factory.Emails.Messages, m => m.To == recipient);
        // D-759 (#10 Phase 3) — re-email carries the ZIP + the PDF sheet, same as generate.
        Assert.Equal(2, message.Attachments!.Count);
        var attachment = Assert.Single(message.Attachments!, a => a.ContentType == "application/zip");
        Assert.Single(message.Attachments!, a => a.ContentType == "application/pdf");
        using var zip = new ZipArchive(new MemoryStream(attachment.Content), ZipArchiveMode.Read);
        Assert.Equal(3, zip.Entries.Count);
    }

    [Fact]
    public async Task Re_email_an_unknown_batch_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/re-email",
            new AdminReEmailBadgeBatchRequest
            {
                BatchId = Guid.NewGuid(),
                RecipientEmail = "organiser@simf.test",
            },
            admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_a_batch_disables_its_accounts_and_marks_it_inactive()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = typeId, Count = 2 } },
            },
            admin);

        Guid batchId;
        List<Guid> memberIds;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var members = await appDb.UserProfiles.Where(p => p.ProfileTypeId == typeId).ToListAsync();
            batchId = members[0].BadgeBatchId;
            // Bulk-badge members still get an account today; filtering keeps the
            // assertion honest if that changes rather than throwing on a null.
            memberIds = members.Where(m => m.UserId != null).Select(m => m.UserId!.Value).ToList();
        }

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/revoke",
            new AdminRevokeBadgeBatchRequest { BatchId = batchId }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRevokeBadgeBatchResponse>>())!;
        Assert.Equal(2, body.Data!.RevokedCount);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var batch = await appDb.BadgeBatches.SingleAsync(b => b.Id == batchId);
            Assert.False(batch.IsActive);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            foreach (var id in memberIds)
            {
                var user = await users.FindByIdAsync(id.ToString());
                Assert.Equal(AccountState.Disabled, user!.AccountState);
            }
        }
    }

    [Fact]
    public async Task Revoke_an_unknown_batch_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/revoke",
            new AdminRevokeBadgeBatchRequest { BatchId = Guid.NewGuid() }, admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_generated_badge_profile_is_incomplete_so_self_claim_prompts_the_profile_stage()
    {
        // The self-claim capture is delivered by the existing profile-completeness
        // flow with no new code: a bulk-minted badge carries no interests, no ID
        // image and a zero nationality, so it reads as incomplete. When the holder
        // activates the badge — which is what creates their account — and signs
        // in, /app/users/me returns profileComplete=false and the app forces the
        // add-profile stage.
        //
        // Asserted on the ATTENDEE rather than through an account, because the
        // mint no longer creates one: a badge sits in a box for months before
        // anybody claims it. These are the same fields completeness reads once an
        // account is linked.
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Test order",
                NameArabic = "طلب اختباري",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = typeId, Count = 1 } },
            },
            admin);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var minted = await appDb.UserProfiles
            .AsNoTracking()
            .Include(p => p.Interests)
            .Where(p => p.ProfileTypeId == typeId)
            .FirstAsync();

        Assert.Null(minted.UserId);
        Assert.Empty(minted.Interests);
        Assert.Equal(0, minted.NationalityId);
        Assert.Null(minted.IdImageFileId);
    }

    // -- Badge-order lines (BadgeBatchItem) -----------------------------------
    // The order's contents used to be a rendered string, "VIP × 3 + Normal × 2",
    // built at mint time. Nothing could be asked of it without parsing, it was
    // English whoever read it, and it froze the tier name — renaming a profile
    // type left every historical order labelled with a name that no longer
    // existed. The counts are child rows now, and the label is composed on read
    // against the LIVE profile type.

    [Fact]
    public async Task Bulk_generate_writes_one_order_line_per_profile_type_with_its_count()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var normal = await NamedVisitorProfileTypeAsync("LineNormal");
        var vip = await NamedVisitorProfileTypeAsync("LineVip");

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Two tier order",
                NameArabic = "طلب من فئتين",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = normal.Id, Count = 4 },
                    new() { ProfileTypeId = vip.Id, Count = 3 },
                },
            },
            admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var batchId = await BatchIdForTypeAsync(normal.Id);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var lines = await appDb.BadgeBatchItems
            .AsNoTracking()
            .Where(item => item.BadgeBatchId == batchId)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync();

        Assert.Equal(2, lines.Count);
        Assert.Equal(normal.Id, lines[0].ProfileTypeId);
        Assert.Equal(4, lines[0].Count);
        Assert.Equal(vip.Id, lines[1].ProfileTypeId);
        Assert.Equal(3, lines[1].Count);
        // Entry order is kept, so the composed breakdown reads back the way the
        // admin typed it rather than in whatever order the rows come off disk.
        Assert.Equal(new[] { 0, 1 }, lines.Select(item => item.DisplayOrder).ToArray());
    }

    [Fact]
    public async Task The_readable_summary_follows_a_profile_type_rename_rather_than_freezing_the_old_name()
    {
        // This is the test that justifies the whole change. The old column stored
        // the tier NAME at mint time, so renaming "TierBefore" to "TierAfter" left
        // every order that already existed still reading "TierBefore" - history
        // labelled with a name that is not in the system any more, and no way to
        // correct it short of rewriting stored prose.
        var admin = await CreateAdministratorAndSignInAsync();
        var tier = await NamedVisitorProfileTypeAsync("RenameTier");

        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Rename order",
                NameArabic = "طلب إعادة التسمية",
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = tier.Id, Count = 5 } },
            },
            admin);
        var batchId = await BatchIdForTypeAsync(tier.Id);

        var renamed = $"Renamed{Guid.NewGuid():N}"[..20];
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profileType = await appDb.ProfileTypes.SingleAsync(p => p.Id == tier.Id);
            profileType.Name = renamed;
            await appDb.SaveChangesAsync();
        }

        var listResponse = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/list", new GridQuery { Top = 200 }, admin);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = (await listResponse.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>())!;
        var row = Assert.Single(page.Data!.Items, b => b.Id == batchId);

        // Composed on read from the order's lines joined to the live profile type,
        // so the rename corrects history instead of breaking it.
        Assert.Equal($"{renamed} × 5", row.CountsSummary);
        Assert.DoesNotContain(tier.Name, row.CountsSummary, StringComparison.Ordinal);
        // The bilingual breakdown the Control Panel actually renders moves with it.
        var onlyTier = Assert.Single(row.Tiers!);
        Assert.Equal(renamed, onlyTier.Name);
        Assert.Equal(5, onlyTier.Count);
    }

    [Fact]
    public async Task An_orders_total_equals_the_sum_of_its_line_counts_including_after_a_top_up()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var normal = await NamedVisitorProfileTypeAsync("TotalNormal");
        var vip = await NamedVisitorProfileTypeAsync("TotalVip");

        await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                Name = "Total order",
                NameArabic = "طلب الإجمالي",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = normal.Id, Count = 4 },
                    new() { ProfileTypeId = vip.Id, Count = 3 },
                },
            },
            admin);
        var batchId = await BatchIdForTypeAsync(normal.Id);

        var topUp = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/top-up",
            new AdminTopUpBadgeBatchRequest
            {
                BatchId = batchId,
                Batches = new List<BulkBadgeBatch> { new() { ProfileTypeId = normal.Id, Count = 2 } },
            },
            admin);
        Assert.Equal(HttpStatusCode.OK, topUp.StatusCode);

        int lineTotal;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            lineTotal = await appDb.BadgeBatchItems
                .AsNoTracking()
                .Where(item => item.BadgeBatchId == batchId)
                .SumAsync(item => item.Count);
        }

        // The total is no longer a second copy of the same fact that could drift
        // from the lines; it IS the sum of them, on every surface that reads it.
        Assert.Equal(9, lineTotal);
        var topUpBody = (await topUp.Content
            .ReadFromJsonAsync<ApiResult<AdminTopUpBadgeBatchResponse>>())!;
        Assert.Equal(lineTotal, topUpBody.Data!.TotalCount);

        var listResponse = await PostAuthAsync(
            "/api/v1/admin/visitors/badge-batches/list", new GridQuery { Top = 200 }, admin);
        var page = (await listResponse.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>())!;
        var row = Assert.Single(page.Data!.Items, b => b.Id == batchId);
        Assert.Equal(lineTotal, row.TotalCount);
    }

    private async Task<Guid> BatchIdForTypeAsync(Guid profileTypeId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.UserProfiles
            .Where(p => p.ProfileTypeId == profileTypeId)
            .Select(p => p.BadgeBatchId)
            .FirstAsync();
    }

    // -- helpers --------------------------------------------------------------

    private static AdminWalkInRegistrationRequest BuildRequest(
        Guid profileTypeId, Guid organisationId, string nationalityCode, bool isDelegate)
    {
        var isSaudi = nationalityCode == "SA";
        return new AdminWalkInRegistrationRequest
        {
            DisplayName = "Delegate Subject",
            ArabicName = "عضو وفد",
            EnglishName = "Delegate Member",
            ProfileTypeId = profileTypeId,
            NationalityCode = nationalityCode,
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = isSaudi,
            NationalId = isSaudi ? "1101798278" : null,
            PassportNumber = isSaudi ? null : "P1234567",
            SaudiMobile = "+966500000002",
            OrganisationId = organisationId,
            IsDelegate = isDelegate,
        };
    }

    private async Task SetCountryInvitedAsync(string code, bool invited)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = code == "SA" ? 682 : 840,
                Code = code, Name = code, NameArabic = code,
                IsActive = true, CreatedAt = SimfClock.Now,
            };
            appDb.Countries.Add(country);
        }
        country.IsActive = true;
        country.IsInvited = invited;
        await appDb.SaveChangesAsync();
    }

    private async Task<Guid> OrganisationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.Organisations.FirstOrDefaultAsync(o => o.IsActive);
        if (existing is not null) return existing.Id;
        var fresh = new Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "جهة اختبار",
            Name = "Test Organisation",
            CommercialRegistration = $"CR{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Organisations.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> VisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — DelegateTestSeed",
            NameArabic = "زائر — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> FreshVisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Delegate Bulk {Guid.NewGuid():N}"[..24],
            NameArabic = "وفد — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"delegate-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Delegate Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
