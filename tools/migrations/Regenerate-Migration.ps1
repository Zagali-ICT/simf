<#
.SYNOPSIS
    RETIRED. Regenerating a migration is no longer how SIMF changes the schema.

.DESCRIPTION
    This script used to delete every migration in a context's folder and re-add a
    single InitialCreate at a pinned id. Owner instruction on 2026-09-07 ended that
    workflow (D-959): the databases are PRODUCTION, nothing is dropped,
    InitialCreate is the baseline they are already AT, and every later schema change
    is its own `dotnet ef migrations add`.

    The script is kept rather than deleted because its reasoning is the record of
    why the old workflow existed, and because a reader who finds it referenced in an
    older document needs to land here rather than on a missing file. It refuses to
    run.

    WHY IT HAD TO GO, in one line: a regenerated migration never reaches a database
    that already has one. __EFMigrationsHistory records
    00000000000000_InitialCreate as applied, MigrateAsync finds nothing pending, and
    the regenerated file describes a schema the live database never receives. The
    old header said so itself and asked for a hand-run docs/migrations/2026/ delta
    alongside every regeneration. That instruction was followed sometimes. D-944
    shipped without it on 2026-08-28 and broke every visitor profile read in
    production; the local development pair was later found missing
    ProfileIdentityDocuments, BadgeBatchItems and UserProfile.OrganisationOther
    while EF believed the schema was current.

    It also deleted files. Under incremental migrations that is destructive: the
    migrations in those folders may already be applied to a live database, and a
    deleted migration is one EF will try to apply again.

    WHAT REPLACES IT

        dotnet ef migrations add <Name> `
            --project src/Backend/SIMF.Infrastructure `
            --startup-project src/Backend/SIMF.Infrastructure `
            --context Simf{App|Identity}DbContext `
            --output-dir Persistence/Migrations/{App|Identity}

    Never --no-build: EF builds the model from the compiled assembly, so a stale
    build silently generates the PREVIOUS schema.

    Then READ the generated Up(). EF's differ pairs a dropped column with an added
    column of similar type and infers a RenameColumn, with no knowledge of meaning.
    That inference is often wrong, and a wrong rename does not fail loudly - it
    moves data from one business field into an unrelated one. EF prints "review the
    migration for accuracy" for this reason. Correcting a scaffolded rename into
    DropColumn + AddColumn is part of the tool workflow, not hand-authoring.

    The design-time factories read SIMF_DESIGN_TIME_APP_CONNECTION and
    SIMF_DESIGN_TIME_IDENTITY_CONNECTION, so a migration can be rehearsed against a
    restored copy of a database without editing appsettings.

    The baseline id stays pinned at 00000000000000_InitialCreate. Existing
    databases - production included - record that exact id, and re-stamping it makes
    every one of them look un-migrated.
    tests/SIMF.Domain.Tests/SchemaFreezeTests.cs fails the build on any other
    baseline id, on a second InitialCreate, and on two migrations sharing a class
    name - which is the merge hazard the old pinning existed to prevent and the one
    thing here that still needs guarding.
#>
[CmdletBinding()]
param(
    [ValidateSet('App', 'Identity')]
    [string] $Context
)

Write-Host ''
Write-Host 'Regenerate-Migration.ps1 is RETIRED (D-959).' -ForegroundColor Red
Write-Host ''
Write-Host 'Regenerating the baseline does not reach a database that already has it,' -ForegroundColor Yellow
Write-Host 'and deleting migrations that are already applied breaks them permanently.' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Add a migration instead:' -ForegroundColor Cyan
Write-Host ''
Write-Host '  dotnet ef migrations add <Name> `'
Write-Host '      --project src/Backend/SIMF.Infrastructure `'
Write-Host '      --startup-project src/Backend/SIMF.Infrastructure `'
Write-Host '      --context Simf{App|Identity}DbContext `'
Write-Host '      --output-dir Persistence/Migrations/{App|Identity}'
Write-Host ''
Write-Host 'Then read the generated Up(): EF infers RenameColumn from type similarity'
Write-Host 'alone, and a wrong rename silently moves data between unrelated fields.'
Write-Host ''
Write-Host 'See the comment header of this file, and CLAUDE.md.' -ForegroundColor DarkGray
Write-Host ''

exit 1
