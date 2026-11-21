#Requires -Version 7.0
param([Parameter(Mandatory)] $Report)

if($Report.schemaVersion -ne 1) {
    throw 'Expected a Font.Metadata CLI report with schemaVersion 1.'
}

function Show-Flags($path, $source, $faceIndex, $sectionStatus, $value) {
    $hex = $null
    $bitSet = $null
    if($null -ne $value) {
        $hex = '0x{0:X4}' -f $value
        $bitSet = ($value -band 0x0002) -ne 0
    }
    [pscustomobject]@{
        Path = $path
        Source = $source
        FaceIndex = $faceIndex
        SectionStatus = $sectionStatus
        RawFsType = $hex
        Bit0002Set = $bitSet
    }
}

foreach($result in $Report.results) {
    $metadata = $result.metadata
    if($null -eq $metadata) {
        continue
    }
    if($metadata.format -eq 'eot') {
        $headerFlags = if($null -ne $metadata.eot) { $metadata.eot.embeddingPermissions }
        Show-Flags $result.source.path 'EOT header' $null $metadata.eotHeaderStatus $headerFlags
    }
    foreach($face in $metadata.faces) {
        Show-Flags $result.source.path $face.source $face.index $face.os2TableStatus $face.embeddingPermissions
    }
}
