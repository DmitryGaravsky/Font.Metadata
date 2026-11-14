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
    # SPDX is independent of legacy hints and raw-record output. Exercise actual installed/portable binaries.
    foreach($flags in @(@(), @('--include-name-records'), @('--analyze-licenses'), @('--include-name-records', '--analyze-licenses'))) {
        $json = & $Executable @Prefix inspect valid.ttf partial.ttf missing.ttf --format json --analyze-spdx @flags 2> errors.txt
        if($LASTEXITCODE -ne 3 -or (Get-Item errors.txt).Length -ne 0) { throw 'SPDX inspection changed I/O exit semantics.' }
        $text = $json -join "`n"
        if(-not (Test-Json -Json $text -SchemaFile $schema)) { throw 'SPDX JSON violates its schema.' }
        $spdxReport = $text | ConvertFrom-Json -AsHashtable
        $complete = $spdxReport.results[0].metadata.faces[0].spdxAnalysis
        $partialAnalysis = $spdxReport.results[1].metadata.faces[0].spdxAnalysis
        if($complete.declarations[0].expressionText -cne 'MIT' -or -not $complete.isSourceComplete -or
            $partialAnalysis.isSourceComplete -or -not $partialAnalysis.isAnalysisComplete -or $null -ne $spdxReport.results[2].metadata) {
            throw 'SPDX source/outcome contract failed.'
        }
    }
    # Independent one-record wire fixtures exercise all projected result shapes, not just MIT success.
    $cases = @{
        syntax = 'SPDX-License-Identifier: MIT OR'
        empty = 'SPDX-License-Identifier: '
        explicitLimit = 'SPDX-License-Identifier: ' + ('x' * 257)
        probeLimit = ('x' * 257)
        outcomes = 'SPDX-License-Identifier: GPL-2.0+ OR Unknown-1.0 OR Classpath-exception-2.0 OR DocumentRef-Other:LicenseRef-Custom'
        truncation = 'SPDX-License-Identifier: MIT OR ' + ((@('Unknown-1.0') * 130) -join ' OR ') + ' OR LicenseRef-Custom'
    }
    foreach($case in $cases.GetEnumerator()) {
        [byte[]] $value = [Text.Encoding]::BigEndianUnicode.GetBytes($case.Value)
        [byte[]] $fixture = @($font[0..45]) + $value
        $tableLength = 18 + $value.Length
        $fixture[26] = [byte]($tableLength -shr 8)
        $fixture[27] = [byte]($tableLength -band 255)
        $fixture[42] = [byte]($value.Length -shr 8)
        $fixture[43] = [byte]($value.Length -band 255)
        $path = 'spdx-' + $case.Key + '.ttf'
        [IO.File]::WriteAllBytes((Join-Path $PWD $path), $fixture)
        $caseJson = & $Executable @Prefix inspect $path --format json --analyze-spdx 2> errors.txt
        if($LASTEXITCODE -ne 0 -or (Get-Item errors.txt).Length -ne 0) { throw "SPDX case changed reader exit/channel: $($case.Key)" }
        $caseText = $caseJson -join "`n"
        if(-not (Test-Json -Json $caseText -SchemaFile $schema)) { throw "SPDX result shape violates schema: $($case.Key)" }
        [IO.File]::WriteAllText((Join-Path $PWD ('spdx-' + $case.Key + '.json')), $caseText)
        $analysis = ($caseText | ConvertFrom-Json -AsHashtable).results[0].metadata.faces[0].spdxAnalysis
        switch($case.Key) {
            { $_ -in 'syntax', 'empty' } {
                if($analysis.declarations[0].parseStatus -cne 'invalidSyntax' -or -not $analysis.isAnalysisComplete) { throw 'Invalid explicit syntax was not a completed attempt.' }
            }
            'explicitLimit' {
                if($analysis.declarations[0].parseStatus -cne 'limitExceeded' -or $analysis.isAnalysisComplete) { throw 'Explicit parser limit was lost.' }
            }
            'probeLimit' {
                if($analysis.declarations.Count -ne 0 -or $analysis.isAnalysisComplete -or $analysis.diagnostics[0].code -cne 'FSI_PROBE_PARSE_LIMIT') { throw 'Bare parser limit was lost.' }
            }
            'outcomes' {
                $validation = $analysis.declarations[0].validation
                if(($validation.identifierOccurrences.lookupStatus -join ',') -cne 'known,unknown,wrongKind' -or
                    -not $validation.identifierOccurrences[0].isDeprecated -or -not $validation.hasIdentifierErrors -or
                    -not $validation.hasUnresolvedReferences -or $validation.references[0].documentId -cne 'Other') { throw 'Validation outcomes changed.' }
            }
            'truncation' {
                $validation = $analysis.declarations[0].validation
                if(-not $validation.diagnosticsTruncated -or $validation.diagnostics.Count -ne 128 -or
                    $validation.identifierOccurrences.Count -ne 131 -or -not $validation.hasUnresolvedReferences -or
                    -not $analysis.isAnalysisComplete) { throw 'Truncation hid validation evidence.' }
            }
        }
    }
    $plainSpdx = & $Executable @Prefix inspect valid.ttf --analyze-spdx
    if($LASTEXITCODE -ne 0 -or ($plainSpdx -join "`n") -notmatch 'FSI002') { throw 'SPDX plain report failed.' }
    foreach($mutation in @('context', 'flag', 'face', 'nullValidation', 'bareFailure', 'parseDiagnostic', 'hash', 'budget', 'sourceId')) {
        $invalid = $text | ConvertFrom-Json -AsHashtable
        $declaration = $invalid.results[0].metadata.faces[0].spdxAnalysis.declarations[0]
        switch($mutation) {
            'context' { $invalid.Remove('spdxContext') }
            'flag' { $invalid.options.analyzeSpdx = $false }
            'face' { $invalid.results[0].metadata.faces[0].Remove('spdxAnalysis') }
            'nullValidation' { $declaration.validation = $null }
            'bareFailure' {
                $declaration.parseStatus = 'invalidSyntax'
                $declaration.validation = $null
                $declaration.parseDiagnostics = @(@{code='x';severity='error';span=@{start=0;length=1};message='x'})
            }
            'parseDiagnostic' { $declaration.parseDiagnostics = @(@{code='x';severity='error';span=@{start=0;length=1};message='x'}) }
            'hash' { $invalid.spdxContext.registry.licensesSha256 = 'invalid' }
            'budget' { $invalid.spdxContext.options.maxDeclarations = 0 }
            'sourceId' { $declaration.sourceRecord.nameId = 14 }
        }
        if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
            throw "Schema accepted invalid SPDX case: $mutation"
        }
    }
    Write-Host 'CLI plain/JSON, schema, license evidence, partial and I/O checks passed.'
}
finally {
    Pop-Location
}

# Expected CLI failures must not leak their native exit code into the Actions shell.
# Keep this after validation and cleanup so exceptions still fail the caller.
exit 0
