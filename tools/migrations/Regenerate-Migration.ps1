<#
.SYNOPSIS
    Regenerates a context's single InitialCreate migration and re-pins its id.

.DESCRIPTION
    The schema is described by exactly ONE migration per context (D-110, re-instated
    by D-895), so a schema change is not a new migration - it is a regeneration of
    the existing one. `dotnet ef migrations add` stamps a fresh UTC timestamp every
    time, which is what made the regeneration dangerous:

        branch A regenerates -> 20260816081717_InitialCreate.cs
        branch B regenerates -> 20260816145611_InitialCreate.cs

    Two different paths, so git merges both without a conflict, Azure reports
    mergeStatus "succeeded", and `main` ends up with two classes named
    InitialCreate in one namespace and no longer compiles. That happened twice on
    2026-08-16, three migrations deep each time.

    So the id is PINNED. Every regeneration lands on the same filename and the same
    [Migration("...")] attribute, which turns the silent double-add into an ordinary
    content conflict that git raises and a human resolves - by running this script
    once on the merged model, which is the only correct resolution anyway.

    An all-zero id is deliberately not a timestamp: nothing about it invites a
    reader to believe it records when anything happened. It sorts before any real
    timestamp, so a future delta migration still applies after it.

.PARAMETER Context
    App or Identity. Omit to regenerate both.

.EXAMPLE
    ./tools/migrations/Regenerate-Migration.ps1
    ./tools/migrations/Regenerate-Migration.ps1 -Context App
#>
[CmdletBinding()]
param(
    [ValidateSet('App', 'Identity')]
    [string] $Context
)

$ErrorActionPreference = 'Stop'

# Keep in step with SchemaFreezeTests.PinnedMigrationId, which fails the build
# when a migration on disk carries any other id.
$PinnedId = '00000000000000_InitialCreate'

# Two-argument Join-Path: Windows PowerShell 5.1 has no multi-segment overload.
$repoRoot = Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')
$project = Join-Path $repoRoot 'src/Backend/SIMF.Infrastructure'
$contexts = if ($Context) { @($Context) } else { @('App', 'Identity') }

foreach ($name in $contexts) {
    $dbContext = "Simf${name}DbContext"
    $folder = Join-Path $project "Persistence/Migrations/$name"

    Write-Host "Regenerating $dbContext" -ForegroundColor Cyan

    # Delete every migration file first. `dotnet ef migrations remove` refuses once
    # the migration has been applied to a local database, and a merge can leave
    # several migrations here at once - neither case is worth arguing with.
    if (Test-Path $folder) {
        Get-ChildItem $folder -Filter '*.cs' | Remove-Item -Force
    }

    # No --no-build. EF builds the model from the compiled assembly, so a stale
    # build silently generates the PREVIOUS schema.
    dotnet ef migrations add InitialCreate `
        --project $project `
        --startup-project $project `
        --context $dbContext `
        --output-dir "Persistence/Migrations/$name"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations add failed for $dbContext"
    }

    $generated = Get-ChildItem $folder -Filter '*_InitialCreate.cs' |
        Where-Object { $_.Name -notlike '*.Designer.cs' }
    if ($generated.Count -ne 1) {
        throw "Expected exactly one InitialCreate in $folder, found $($generated.Count)"
    }

    $generatedId = $generated.BaseName
    if ($generatedId -ne $PinnedId) {
        # The attribute is the id EF actually reads; the filenames only have to
        # agree with it by convention. Rewrite both.
        $designer = Join-Path $folder "${generatedId}.Designer.cs"
        (Get-Content $designer -Raw).Replace($generatedId, $PinnedId) |
            Set-Content $designer -NoNewline -Encoding utf8

        Move-Item $designer (Join-Path $folder "${PinnedId}.Designer.cs") -Force
        Move-Item $generated.FullName (Join-Path $folder "${PinnedId}.cs") -Force
    }

    Write-Host "  pinned to $PinnedId" -ForegroundColor Green
}

Write-Host ''
Write-Host 'Now verify the model and the migration agree:' -ForegroundColor Yellow
foreach ($name in $contexts) {
    Write-Host "  dotnet ef migrations has-pending-model-changes --project $project --startup-project $project --context Simf${name}DbContext"
}
