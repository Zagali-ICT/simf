using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// D-109: end-to-end verification of the row-audit SaveChanges
/// interceptor. We drive the Identity DbContext directly so we can
/// observe the audit row land in the SAME transaction as the data
/// change — no HTTP layer in the path.
/// </summary>
public sealed class RowAuditTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public RowAuditTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Insert_writes_a_RowAudit_row_with_post_image_and_no_old_values()
    {
        var interestId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        db.Interests.Add(new Interest
        {
            Id = interestId,
            Name = $"Audit Insert {Guid.NewGuid():N}",
            NameArabic = "تدقيق إدخال",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = await db.RowAudits
            .AsNoTracking()
            .Where(row => row.EntityType == nameof(Interest) && row.PrimaryKey == interestId.ToString())
            .SingleAsync();

        Assert.Equal(RowAuditOperation.Insert, audit.Operation);
        Assert.NotNull(audit.NewValuesJson);
        Assert.Contains("\"Name\"", audit.NewValuesJson);
        Assert.Null(audit.OldValuesJson);
        Assert.Null(audit.AffectedColumns);
    }

    [Fact]
    public async Task Update_writes_a_RowAudit_row_with_changed_columns_and_both_images()
    {
        var interestId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        var interest = new Interest
        {
            Id = interestId,
            Name = $"Original {Guid.NewGuid():N}",
            NameArabic = "أصلي",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Interests.Add(interest);
        await db.SaveChangesAsync();

        interest.IsActive = false;
        await db.SaveChangesAsync();

        var audit = await db.RowAudits
            .AsNoTracking()
            .Where(row => row.EntityType == nameof(Interest)
                && row.PrimaryKey == interestId.ToString()
                && row.Operation == RowAuditOperation.Update)
            .SingleAsync();

        Assert.NotNull(audit.AffectedColumns);
        Assert.Contains("IsActive", audit.AffectedColumns);
        Assert.NotNull(audit.OldValuesJson);
        Assert.NotNull(audit.NewValuesJson);
    }

    [Fact]
    public async Task RowAudits_table_itself_does_not_appear_in_RowAudits()
    {
        var interestId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        db.Interests.Add(new Interest
        {
            Id = interestId,
            Name = $"Recursion Guard {Guid.NewGuid():N}",
            NameArabic = "حماية من التكرار",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var selfReferentialRows = await db.RowAudits
            .AsNoTracking()
            .CountAsync(row => row.EntityType == nameof(RowAudit));

        Assert.Equal(0, selfReferentialRows);
    }
}
