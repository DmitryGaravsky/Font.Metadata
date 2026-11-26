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
    $inventoryJson = & $Executable @Prefix inspect valid.ttf partial.ttf --include-table-inventory --format json
    if($LASTEXITCODE -ne 1) { throw 'Inventory inspection lost the partial result.' }
    $inventoryText = $inventoryJson -join [Environment]::NewLine
    if(-not (Test-Json -Json $inventoryText -SchemaFile $schema)) { throw 'Invalid table inventory report.' }
    foreach($field in @('glyphCount', 'head', 'headTableStatus', 'maxpTableStatus', 'tableInventory', 'storedLength', 'limitReason', 'includeTableInventory')) {
        $invalid = $inventoryText | ConvertFrom-Json -AsHashtable
        $face = $invalid.results[0].metadata.faces[0]
        switch($field) {
            'storedLength' { $face.tableInventory.entries[0].storedLength = -1 }
            'limitReason' { $invalid.results[1].diagnostics[0]['limitReason'] = 123 }
            'includeTableInventory' { $invalid.options.Remove('includeTableInventory') }
            default { $face[$field] = $false }
        }
        if(Test-Json -Json ($invalid | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
            throw ("Schema accepted invalid {0}." -f $field)
        }
    }
    # A single fvar entry declares one wght axis; valid.ttf has only a name table.
    $variable = [Convert]::FromHexString(
        '00010000000100100000000066766172000000000000001C00000024' +
        '000100000010000200010014000000087767687400008000000100000001800000010100')
    [IO.File]::WriteAllBytes((Join-Path $PWD 'variable.ttf'), $variable)
    foreach($audit in @($false, $true)) {
        $axisFlags = @()
        if($audit) { $axisFlags = @('--include-variation-axes') }
        $catalogJson = & $Executable @Prefix inspect variable.ttf valid.ttf --format json @axisFlags 2> errors.txt
        if($LASTEXITCODE -ne 0 -or (Get-Item errors.txt).Length -ne 0) { throw 'Design-axis inspection failed.' }
        $catalogText = $catalogJson -join "`n"
        if(-not (Test-Json -Json $catalogText -SchemaFile $schema)) { throw 'Invalid design-axis JSON report.' }
        $catalog = $catalogText | ConvertFrom-Json -AsHashtable
        $variableFace = $catalog.results[0].metadata.faces[0]
        $staticFace = $catalog.results[1].metadata.faces[0]
        if($catalog.options.Contains('includeVariationAxes') -ne $audit -or
            $variableFace.variationAxes.status -cne 'complete' -or $variableFace.variationAxes.axes[0].tag -cne 'wght' -or
            $variableFace.Contains('unicodeCoverage') -or $staticFace.Contains('unicodeCoverage') -or
            $staticFace.Contains('variationAxes') -ne $audit -or ($audit -and $staticFace.variationAxes.status -cne 'missing')) {
            throw 'Installed/portable CLI lost automatic or audited axis reporting.'
        }
        if($audit) { $staticFace.Remove('variationAxes') }
        else { $staticFace['variationAxes'] = @{ status = 'missing'; axes = @(); diagnostics = @() } }
        if(Test-Json -Json ($catalog | ConvertTo-Json -Depth 100) -SchemaFile $schema -ErrorAction SilentlyContinue) {
            throw 'Schema accepted incorrect static-face axis reporting.'
        }
    }
    Copy-Item -LiteralPath (Join-Path $examples 'Recovery.eot') -Destination 'Recovery.eot'
    $eotJson = & $Executable @Prefix inspect Recovery.eot --include-name-records --analyze-licenses --format json
    if($LASTEXITCODE -ne 1) { throw 'The documented EOT input must give a partial inspection.' }
    $eotText = $eotJson -join "`n"
    if(-not (Test-Json -Json $eotText -SchemaFile $schema)) { throw 'Invalid EOT example report.' }
    $documentedEot = (Get-Content -LiteralPath (Join-Path $examples 'cli-eot-recovery.json')) -join "`n"
    if($eotText -cne $documentedEot) { throw 'The documented 1.1.0 EOT report differs from the CLI output.' }
    $eotReport = $eotText | ConvertFrom-Json
    if($eotReport.results[0].status -cne 'partial') { throw 'Expected partial EOT metadata.' }
    $readFlags = Join-Path $examples 'read-embedding-flags.ps1'
    $rows = @(& $readFlags -Report $eotReport)
    if($rows.Count -ne 3 -or
        $rows[0].Source -cne 'EOT header' -or $null -ne $rows[0].FaceIndex -or $rows[0].SectionStatus -cne 'complete' -or
        $rows[0].RawFsType -cne '0x0002' -or $true -ne $rows[0].Bit0002Set -or
        $rows[1].Source -cne 'eotMain' -or $rows[1].FaceIndex -ne 0 -or $rows[1].SectionStatus -cne 'notRead' -or
        $null -ne $rows[1].RawFsType -or $null -ne $rows[1].Bit0002Set -or
        $rows[2].Source -cne 'eotEudc' -or $rows[2].FaceIndex -ne 1 -or $rows[2].SectionStatus -cne 'complete' -or
        $rows[2].RawFsType -cne '0x0000' -or $false -ne $rows[2].Bit0002Set) {
        throw 'The documented fsType example lost a source, a raw value or its availability.'
    }
    $extended = $eotText | ConvertFrom-Json -AsHashtable
    $extended.results[0].metadata.faces[0]['futureSection'] = @{ status = 'example' }
    $extendedText = $extended | ConvertTo-Json -Depth 100
    if(-not (Test-Json -Json $extendedText -SchemaFile $schema)) { throw 'JSON v1 rejected an additional optional field.' }
    $extendedRows = @(& $readFlags -Report ($extendedText | ConvertFrom-Json))
    if((ConvertTo-Json $rows) -cne (ConvertTo-Json $extendedRows)) {
        throw 'An additional optional field changed the v1 consumer output.'
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
    foreach($case in @(
        @{ name='unsupported'; bytes=[byte[]]@(116,116,99,102,0,3,0,0,0,0,0,1); status='unsupported' },
        @{ name='failed'; bytes=[byte[]]@(0,1,0,0); status='failed' },
        @{ name='unknown'; bytes=[byte[]]@(1,2,3); status='notRecognized' }
    )) {
        $path = 'spdx-' + $case.name + '.bin'
        [IO.File]::WriteAllBytes((Join-Path $PWD $path), $case.bytes)
        $emptyJson = & $Executable @Prefix inspect $path --format json --analyze-spdx 2> errors.txt
        if($LASTEXITCODE -ne 1 -or (Get-Item errors.txt).Length -ne 0) { throw 'Empty/unsupported input changed exit semantics.' }
        $emptyText = $emptyJson -join "`n"
        if(-not (Test-Json -Json $emptyText -SchemaFile $schema)) { throw 'Empty/unsupported SPDX report violates schema.' }
        $emptyReport = $emptyText | ConvertFrom-Json -AsHashtable
        if($null -eq $emptyReport.spdxContext -or $emptyReport.results[0].status -cne $case.status) { throw 'Empty/unsupported result lost context or status.' }
        if($case.name -eq 'unknown') {
            if($null -ne $emptyReport.results[0].metadata) { throw 'Unknown input has fabricated metadata.' }
        } elseif($case.name -eq 'unsupported') {
            if($emptyReport.results[0].metadata.faces.Count -ne 0) { throw 'Unsupported collection has fabricated faces.' }
        } else {
            $faces = $emptyReport.results[0].metadata.faces
            if($faces.Count -ne 1 -or $faces[0].nameTableStatus -cne 'notRead' -or
                $faces[0].spdxAnalysis.isSourceComplete -or $faces[0].spdxAnalysis.declarations.Count -ne 0) {
                throw 'Failed SFNT lost its unread face placeholder.'
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
    $documentSchema = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/cli-license.schema.json'
    $license = Join-Path (Split-Path $PSScriptRoot -Parent) 'LICENSE'
    $notice = "🎵 NOTICE`r`nSPDX-License-Identifier: MIT OR Apache-2.0`r`n"
    [IO.File]::WriteAllText((Join-Path $PWD 'NOTICE'), $notice)
    [IO.File]::WriteAllText((Join-Path $PWD 'third-party.NOTICE'), 'The supplied archive also refers to the MIT License.')
    $documentJson = & $Executable @Prefix inspect-license missing-license.txt NOTICE $license third-party.NOTICE --format json 2> errors.txt
    if($LASTEXITCODE -ne 3 -or (Get-Item errors.txt).Length -ne 0) { throw 'License document I/O exit/channel check failed.' }
    $documentText = $documentJson -join "`n"
    if(-not (Test-Json -Json $documentText -SchemaFile $documentSchema)) { throw 'License document report violates its schema.' }
    $documents = ($documentText | ConvertFrom-Json -AsHashtable).results
    $noticeHash = (Get-FileHash -LiteralPath 'NOTICE' -Algorithm SHA256).Hash.ToLowerInvariant()
    $expression = 'MIT OR Apache-2.0'
    $line = $documents[1].evidence[0]
    if($documents.Count -ne 4 -or $documents[0].status -cne 'ioError' -or $null -ne $documents[0].sha256 -or
        $documents[1].source.path -cne 'NOTICE' -or $documents[1].sha256 -cne $noticeHash -or
        $documents[1].status -cne 'complete' -or $documents[1].evidence.Count -ne 1 -or
        $line.origin -cne 'spdxDeclaration' -or $line.expressionText -cne $expression -or
        $line.expressionStart -ne $notice.IndexOf($expression, [StringComparison]::Ordinal) -or
        $line.sourceText -cne $notice.Substring($line.sourceStart, $line.sourceLength) -or
        $line.validation.hasIdentifierErrors -or
        @($documents[2].evidence | Where-Object { $_.origin -ceq 'fullText' -and $_.identifier -ceq 'MIT' }).Count -ne 1 -or
        $documents[3].status -cne 'complete' -or $documents[3].evidence.Count -ne 0) {
        throw 'License document provenance, SPDX or full-text contract failed.'
    }
    $invalidDocument = $documentText | ConvertFrom-Json -AsHashtable
    $invalidDocument.results[1].sha256 = 'invalid'
    if(Test-Json -Json ($invalidDocument | ConvertTo-Json -Depth 100) -SchemaFile $documentSchema -ErrorAction SilentlyContinue) {
        throw 'License document schema accepted an invalid digest.'
    }
    Write-Host 'CLI plain/JSON, schema, license evidence, partial and I/O checks passed.'
}
finally {
    Pop-Location
}

# Expected CLI failures must not leak their native exit code into the Actions shell.
# Keep this after validation and cleanup so exceptions still fail the caller.
exit 0
