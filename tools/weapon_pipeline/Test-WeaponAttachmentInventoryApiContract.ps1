[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$sourcePath = Join-Path $repositoryRoot 'game\Code\Api\ServerApiClient.Inventory.cs'

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    [ordered]@{
        contract = 'weapon-attachment-inventory-api'
        status = 'HARNESS_ERROR'
        exitCode = 2
        reason = 'missing-production-source'
        source = [System.IO.Path]::GetRelativePath($repositoryRoot, $sourcePath)
    } | ConvertTo-Json -Compress
    exit 2
}

$source = [System.IO.File]::ReadAllText($sourcePath)
$methodStartMarker = 'public static async Task<bool> TakePlayerItem'
$methodEndMarker = 'public static async Task<List<InventoryItemDto>?> GetPlayerInventory'
$methodStart = $source.IndexOf($methodStartMarker, [System.StringComparison]::Ordinal)
$methodEnd = if ($methodStart -ge 0) {
    $source.IndexOf($methodEndMarker, $methodStart, [System.StringComparison]::Ordinal)
} else {
    -1
}

if ($methodStart -lt 0 -or $methodEnd -le $methodStart) {
    [ordered]@{
        contract = 'weapon-attachment-inventory-api'
        status = 'HARNESS_ERROR'
        exitCode = 2
        reason = 'take-player-item-method-boundary-not-found'
        source = [System.IO.Path]::GetRelativePath($repositoryRoot, $sourcePath)
    } | ConvertTo-Json -Compress
    exit 2
}

$methodSource = $source.Substring($methodStart, $methodEnd - $methodStart)
$regexOptions = [System.Text.RegularExpressions.RegexOptions]::Singleline -bor
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
$requestPattern = @'
\bvar\s+(?<response>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*await\s+ApiClientBase\.RequestAsync\s*\(\s*\$"\{Constants\.ApiBaseUrl\}/v1/server/inventory/\{playerId\}/take"\s*,\s*"POST"\s*,\s*Http\.CreateJsonContent\(\s*dto\s*\)\s*,\s*headers\s*\)\s*;
'@.Trim()
$returnTruePattern = '\breturn\s+true\s*;'

function Test-TakePlayerItemStatusContract {
    param([Parameter(Mandatory)][string]$MethodSource)

    $requestMatches = [regex]::Matches($MethodSource, $requestPattern, $regexOptions)
    $request = if ($requestMatches.Count -eq 1) { $requestMatches[0] } else { $null }
    $responseName = if ($null -ne $request) { $request.Groups['response'].Value } else { '' }
    $ensurePattern = if ($responseName.Length -gt 0) {
        '\b' + [regex]::Escape($responseName) + '\.EnsureSuccessStatusCode\s*\(\s*\)\s*;'
    } else {
        '(?!)'
    }
    $ensureMatches = [regex]::Matches($MethodSource, $ensurePattern, $regexOptions)
    $ensure = if ($ensureMatches.Count -eq 1) { $ensureMatches[0] } else { $null }
    $returnMatches = [regex]::Matches($MethodSource, $returnTruePattern, $regexOptions)
    $returnTrue = if ($returnMatches.Count -eq 1) { $returnMatches[0] } else { $null }
    $ordered = $null -ne $request -and
        $null -ne $ensure -and
        $null -ne $returnTrue -and
        $request.Index -lt $ensure.Index -and
        $ensure.Index -lt $returnTrue.Index

    [pscustomobject]@{
        Passed = $requestMatches.Count -eq 1 -and $ensureMatches.Count -eq 1 -and
            $returnMatches.Count -eq 1 -and $ordered
        RequestCount = $requestMatches.Count
        ResponseName = $responseName
        EnsureCount = $ensureMatches.Count
        ReturnTrueCount = $returnMatches.Count
        Ordered = $ordered
        Request = $request
        Ensure = $ensure
    }
}

$baseline = Test-TakePlayerItemStatusContract -MethodSource $methodSource
$withoutEnsureRejected = $false
$withoutCaptureRejected = $false

if ($baseline.Passed) {
    $ensureRegex = [regex]::new(
        '\b' + [regex]::Escape($baseline.ResponseName) + '\.EnsureSuccessStatusCode\s*\(\s*\)\s*;',
        $regexOptions)
    $withoutEnsure = $ensureRegex.Replace($methodSource, '', 1)
    $withoutEnsureRejected = $withoutEnsure -ne $methodSource -and
        -not (Test-TakePlayerItemStatusContract -MethodSource $withoutEnsure).Passed

    $captureRegex = [regex]::new(
        '\bvar\s+' + [regex]::Escape($baseline.ResponseName) + '\s*=\s*',
        $regexOptions)
    $withoutCapture = $captureRegex.Replace($methodSource, '', 1)
    $withoutCaptureRejected = $withoutCapture -ne $methodSource -and
        -not (Test-TakePlayerItemStatusContract -MethodSource $withoutCapture).Passed
}

$checks = [ordered]@{
    requestResponseCaptured = $baseline.RequestCount -eq 1
    ensureSuccessStatusCodeCalledOnce = $baseline.EnsureCount -eq 1
    returnTrueCalledOnce = $baseline.ReturnTrueCount -eq 1
    requestThenEnsureThenReturn = $baseline.Ordered
    missingEnsureMutationRejected = $withoutEnsureRejected
    missingResponseCaptureMutationRejected = $withoutCaptureRejected
}
$passed = @($checks.Values | Where-Object { -not $_ }).Count -eq 0
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash

[ordered]@{
    contract = 'weapon-attachment-inventory-api'
    status = if ($passed) { 'PASS' } else { 'RED' }
    exitCode = if ($passed) { 0 } else { 1 }
    source = [System.IO.Path]::GetRelativePath($repositoryRoot, $sourcePath)
    sourceSha256 = $sourceHash
    checks = $checks
} | ConvertTo-Json -Depth 4 -Compress

exit $(if ($passed) { 0 } else { 1 })
