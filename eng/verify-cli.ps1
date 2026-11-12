#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Executable,
    [string[]] $Prefix = @(),
    [Parameter(Mandatory)][string] $WorkDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$schema = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/cli.schema.json'
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
Push-Location $WorkDirectory
try {
    # Independent SFNT/name wire fixture: one UTF-16 MIT record, no OS/2 table.
    [byte[]] $font = @(0,1,0,0,0,1,0,16,0,0,0,0,110,97,109,101,0,0,0,0,0,0,0,28,0,0,0,24,
        0,0,0,1,0,18,0,3,0,1,4,9,0,13,0,6,0,0,0,77,0,73,0,84)
    [IO.File]::WriteAllBytes((Join-Path $PWD 'valid.ttf'), $font)
    # Two records sharing a string; the second references missing storage.
    [byte[]] $partial = @($font[0..27]) + @(0,0,0,2,0,30,
        0,3,0,1,4,9,0,13,0,6,0,0, 0,3,0,1,4,9,0,14,0,6,1,0, 0,77,0,73,0,84)
    $partial[27] = 36
    [IO.File]::WriteAllBytes((Join-Path $PWD 'partial.ttf'), $partial)
    $plain = & $Executable @Prefix inspect valid.ttf --analyze-licenses 2> errors.txt
    if($LASTEXITCODE -ne 0 -or ($plain -join "`n") -notmatch 'FLA001' -or (Get-Item errors.txt).Length -ne 0) {
        throw 'Installed/portable CLI plain inspection failed.'
    }
    foreach($flags in @(@(), @('--include-name-records'), @('--analyze-licenses'), @('--include-name-records', '--analyze-licenses'))) {
        $json = & $Executable @Prefix inspect valid.ttf partial.ttf --format json @flags 2> errors.txt
        if($LASTEXITCODE -ne 1 -or (Get-Item errors.txt).Length -ne 0) { throw 'CLI partial inspection exit/channel check failed.' }
        $text = $json -join "`n"
        if(-not (Test-Json -Json $text -SchemaFile $schema)) { throw 'CLI output violates its JSON Schema.' }
        $report = $text | ConvertFrom-Json
        if($report.results.Count -ne 2 -or $report.results[0].status -cne 'complete' -or $report.results[1].status -cne 'partial') {
            throw 'CLI inspection lost complete/partial results.'
        }
        if($report.options.analyzeLicenses) {
            if($report.results[0].metadata.faces[0].licenseAnalysis.hints[0].sourceRecord.value -cne 'MIT') {
                throw 'CLI license evidence was lost.'
            }
        }
        [IO.File]::WriteAllText((Join-Path $PWD ("report-" + $flags.Count + '-' + $report.options.analyzeLicenses + '.json')), $text)
    }
    $json = & $Executable @Prefix inspect missing.ttf valid.ttf --format json 2> errors.txt
    if($LASTEXITCODE -ne 3 -or (Get-Item errors.txt).Length -ne 0) { throw 'CLI per-source I/O check failed.' }
    if(-not (Test-Json -Json ($json -join "`n") -SchemaFile $schema)) { throw 'CLI I/O JSON is invalid.' }
    $examples = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/examples'
    foreach($example in Get-ChildItem -LiteralPath $examples -Filter '*.json') {
        if(-not (Test-Json -LiteralPath $example.FullName -SchemaFile $schema)) { throw 'Documented JSON example is invalid.' }
    }
    # Negative examples ensure validation enforces types and option-dependent presence.
    $invalid = $report | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $invalid.results[0].metadata.faces[0].embeddingPermissions = '0'
    if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
        throw 'Schema accepted string-valued raw flags.'
    }
    $invalid = $report | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $invalid.options.includeNameRecords = $false
    if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
        throw 'Schema accepted disabled optional name records.'
    }
    $invalid = $report | ConvertTo-Json -Depth 100 | ConvertFrom-Json -AsHashtable
    $invalid.results = @()
    if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
        throw 'Schema accepted an empty inspection batch.'
    }
    $invalid = $json -join "`n" | ConvertFrom-Json -AsHashtable
    $invalid.results[0].metadata = $report.results[0].metadata
    if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
        throw 'Schema accepted metadata on an I/O failure.'
    }
    $invalid = $json -join "`n" | ConvertFrom-Json -AsHashtable
    $invalid.results[0].diagnostics = @()
    if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
        throw 'Schema accepted an I/O failure without its diagnostic.'
    }
    Write-Host 'CLI plain/JSON, schema, license evidence, partial and I/O checks passed.'
}
finally {
    Pop-Location
}

# Expected CLI failures must not leak their native exit code into the Actions shell.
# Keep this after validation and cleanup so exceptions still fail the caller.
exit 0
