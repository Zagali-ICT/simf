// Tests: SIMF.Api.Tests/RowAuditTests.cs (insert / update / no self-recursion,
//        and an identity document's number redacted out of its audit row)
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Domain.Auditing;

using SIMF.Common.Enums;
using SIMF.Common;

namespace SIMF.Infrastructure.Auditing;

/// <summary>
/// EF SaveChanges interceptor that writes a <see cref="RowAudit"/> row
/// for every INSERT / UPDATE / DELETE performed through the DbContext.
///
/// <para>The audit row is added to the same DbContext as the originating
/// change, so the data row and the audit row commit in the same transaction
/// — either both land or both don't.</para>
///
/// <para>Skipped entity types:</para>
/// <list type="bullet">
///   <item><see cref="RowAudit"/> itself (infinite recursion).</item>
///   <item><see cref="OperationLogEntry"/> — already an event-level audit.</item>
///   <item>High-volume token/code tables (refresh tokens, account codes,
///         second-factor tokens, TOTP recovery codes) where every sign-in
///         would otherwise add 1–3 audit rows.</item>
/// </list>
///
/// <para>Sensitive column values are redacted before being written to JSON —
/// columns named in <see cref="RedactedColumnNames"/>, or anything ending in
/// <c>Hash</c>, <c>Secret</c>, <c>Token</c> or <c>Code</c>, are replaced with
/// <c>"***"</c>. The audit row records the FACT of a change, not the secret
/// itself.</para>
///
/// <para>The <c>Code</c> suffix used to sit in <see cref="RedactedSuffixes"/> and
/// has been removed, because it protected nothing and cost a great deal. Both
/// secret-bearing <c>Code</c> columns live on entities excluded outright above
/// (<c>AccountCode</c>, <c>TotpRecoveryCode</c>), so the suffix never reached one;
/// what it did blank was every human-readable business identifier in the model -
/// <c>Gate</c>, <c>Hall</c>, <c>Session</c>, <c>Theme</c>, <c>Booth</c>,
/// <c>Speaker</c>, <c>MeetingTable</c>, <c>Country</c>, <c>Region</c>,
/// <c>RatingType</c> and <c>Permission.Code</c>. A permission-catalogue or gate
/// rename recorded only <c>***</c> -&gt; <c>***</c>, which is precisely the change
/// a security review most wants to read. Should a genuinely secret <c>Code</c>
/// column ever be added on a non-excluded entity, name it in
/// <see cref="RedactedColumnNames"/> rather than restoring the blanket
/// suffix.</para>
/// </summary>
internal sealed class RowAuditingSaveChangesInterceptor(
    IRequestContext requestContext,
    TimeProvider timeProvider,
    ILogger<RowAuditingSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    private static readonly HashSet<string> ExcludedEntityTypes = new(StringComparer.Ordinal)
    {
        "RowAudit",
        "OperationLogEntry",
        "RefreshToken",
        "AccountCode",
        "SecondFactorToken",
        "TotpRecoveryCode",
        // GateScan is itself an append-only audit log (it carries
        // ScannedByUserId + CorrelationId + IpAddress + UserAgent already).
        // Auditing the audit log doubles the write volume for zero gain.
        "GateScan",
        // ScanIdempotency is a short-lived replay store, not domain
        // data. Auditing it would add a RowAudit row per scan.
        "ScanIdempotency",
        // AiChatMessages is append-only, per-user and high-volume (two
        // rows per assistant turn), and its Content is raw conversation text (PII)
        // that the AiInvocation path already redacts. Auditing it would double the
        // write volume and store that text unredacted.
        "AiChatMessage",
    };

    private static readonly HashSet<string> RedactedColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        // A2-10 — the PII identifiers are encrypted at rest; the row-audit reads
        // the model (plaintext) value, so redact them here too so the audit trail
        // records *that* they changed without leaking the PII.
        //
        // "Number" is ProfileIdentityDocument.Number, the single column that took
        // over from UserProfile's NationalId / IqamaNumber / PassportNumber. It is
        // named here rather than covered by a suffix rule because the identity
        // document is the only entity in either model with a property called
        // exactly "Number" — the others (PlateNumber, ReferenceNumber) are
        // distinct names carrying non-secret values.
        "Number",
        "SaudiMobile",
        "InternationalMobile",
    };

    // Each suffix has to earn its place, because a suffix rule is blind: it
    // blanks every property whose name happens to end that way, secret or not.
    //
    // "Code" used to be here and is deliberately gone. Every entity in either
    // model with a secret-bearing Code column - AccountCode and TotpRecoveryCode -
    // is excluded WHOLESALE above, so the suffix never protected a secret. What it
    // did protect was Booth.Code, Country.Code, Gate.Code, Hall.Code,
    // MeetingTable.Code, Permission.Code, RatingType.Code, Region.Code,
    // Session.Code, Speaker.Code and Theme.Code - every one of them a business
    // identifier an operator needs in order to read the audit trail at all. A row
    // audit recording that somebody changed a hall, with the hall's code shown as
    // "***", answers none of the questions an audit exists to answer.
    private static readonly string[] RedactedSuffixes =
    {
        "Hash",
        "Secret",
        "Token",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null)
        {
            CaptureChanges(context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context is not null)
        {
            CaptureChanges(context);
        }
        return base.SavingChanges(eventData, result);
    }

    private void CaptureChanges(DbContext context)
    {
        var now = timeProvider.SimfNow();
        var actorUserId = requestContext.ActorUserId;
        // Snapshot the actor's display name from the JWT claim
        // so each RowAudit row stands alone without a cross-DB JOIN.
        var actorDisplayName = requestContext.ActorDisplayName;
        var correlationId = requestContext.CorrelationId;

        // Snapshot the entries — we'll be adding new RowAudit entries below,
        // which would change the collection mid-iteration without this.
        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => !ExcludedEntityTypes.Contains(entry.Entity.GetType().Name))
            .ToList();

        if (entries.Count == 0) { return; }

        var auditRows = new List<RowAudit>(entries.Count);
        foreach (var entry in entries)
        {
            try
            {
                auditRows.Add(BuildAuditRow(entry, now, actorUserId, actorDisplayName, correlationId));
            }
            catch (Exception ex)
            {
                // A failed serialise must not break the original SaveChanges
                // call — the operation itself was not the audit log.
                logger.LogError(
                    ex,
                    "RowAudit capture failed for {Entity} (state {State}); change committed without an audit row.",
                    entry.Entity.GetType().Name,
                    entry.State);
            }
        }

        // Hand the audit rows to the DbContext so they ship in the same
        // SaveChanges batch (same transaction).
        context.AddRange(auditRows.Cast<object>());
    }

    private static RowAudit BuildAuditRow(
        EntityEntry entry, DateTime now, Guid? actorUserId,
        string? actorDisplayName, string? correlationId)
    {
        var operation = entry.State switch
        {
            EntityState.Added => RowAuditOperation.Insert,
            EntityState.Modified => RowAuditOperation.Update,
            EntityState.Deleted => RowAuditOperation.Delete,
            _ => throw new InvalidOperationException(
                $"Unexpected entity state {entry.State} for {entry.Entity.GetType().Name}."),
        };

        var tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
        var entityType = entry.Entity.GetType().Name;
        var primaryKey = BuildPrimaryKey(entry);

        string? oldValuesJson = null;
        string? newValuesJson = null;
        string? affectedColumns = null;

        switch (operation)
        {
            case RowAuditOperation.Insert:
                newValuesJson = SerialiseValues(entry, useOriginalValues: false, changedOnly: false);
                break;
            case RowAuditOperation.Delete:
                oldValuesJson = SerialiseValues(entry, useOriginalValues: true, changedOnly: false);
                break;
            case RowAuditOperation.Update:
                oldValuesJson = SerialiseValues(entry, useOriginalValues: true, changedOnly: true);
                newValuesJson = SerialiseValues(entry, useOriginalValues: false, changedOnly: true);
                affectedColumns = string.Join(",",
                    entry.Properties
                        .Where(property => property.IsModified)
                        .Select(property => property.Metadata.Name));
                break;
        }

        return new RowAudit
        {
            OccurredAt = now,
            TableName = tableName,
            EntityType = entityType,
            Operation = operation,
            PrimaryKey = primaryKey,
            ActorUserId = actorUserId,
            // Clip to the column cap (128) — display names from a
            // claim are well-bounded but defence in depth.
            ActorDisplayName = actorDisplayName is null || actorDisplayName.Length <= 128
                ? actorDisplayName
                : actorDisplayName[..128],
            CorrelationId = correlationId is null || correlationId.Length <= 64
                ? correlationId
                : correlationId[..64],
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            AffectedColumns = affectedColumns is null || affectedColumns.Length <= 2000
                ? affectedColumns
                : affectedColumns[..2000],
        };
    }

    private static string BuildPrimaryKey(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties ?? [];
        if (keyProperties.Count == 0)
        {
            return string.Empty;
        }
        if (keyProperties.Count == 1)
        {
            var keyValue = entry.Property(keyProperties[0].Name).CurrentValue;
            return keyValue?.ToString() ?? string.Empty;
        }
        var composite = new Dictionary<string, object?>(keyProperties.Count);
        foreach (var keyProperty in keyProperties)
        {
            composite[keyProperty.Name] = entry.Property(keyProperty.Name).CurrentValue;
        }
        return JsonSerializer.Serialize(composite, JsonOptions);
    }

    private static string SerialiseValues(EntityEntry entry, bool useOriginalValues, bool changedOnly)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            if (changedOnly && !property.IsModified) { continue; }
            var name = property.Metadata.Name;
            values[name] = IsRedactedColumn(name)
                ? "***"
                : useOriginalValues ? property.OriginalValue : property.CurrentValue;
        }
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static bool IsRedactedColumn(string columnName)
    {
        if (RedactedColumnNames.Contains(columnName)) { return true; }
        foreach (var suffix in RedactedSuffixes)
        {
            if (columnName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
