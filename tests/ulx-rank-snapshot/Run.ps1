$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$repoRoot = [IO.Path]::GetFullPath((Join-Path $testRoot '../..'))
$sourceRoot = Join-Path $repoRoot 'game/Code'
$generatedRoot = Join-Path $testRoot 'generated'
$utf8 = [Text.UTF8Encoding]::new($false)

function Read-Source([string]$relativePath) {
    return [IO.File]::ReadAllText((Join-Path $sourceRoot $relativePath)).Replace("`r`n", "`n")
}

function Slice-Source([string]$text, [string]$start, [string]$end) {
    $first = $text.IndexOf($start, [StringComparison]::Ordinal)
    if ($first -lt 0) { throw "Missing source start: $start" }
    $last = $text.IndexOf($end, $first + $start.Length, [StringComparison]::Ordinal)
    if ($last -lt 0) { throw "Missing source end: $end" }
    return $text.Substring($first, $last - $first)
}

$rankSystem = Read-Source 'System/Player/RankSystem.cs'
$snapshot = Slice-Source $rankSystem "`tpublic IReadOnlyList<RankDto> GetRanksSnapshot()" "`t/// <summary>`n`t/// Sets all rank assignments"
$projectionSource = Read-Source 'Addons/lifepunch/lifepunchulx/LpParityHost.cs'
$projection = Slice-Source $projectionSource "`t// --- Ranks view (all definitions" "#if !LIFEPUNCH_LOCAL`n`t/// <summary>`n`t/// Strip control"
$sanitizer = Slice-Source $projectionSource "`tprivate static string SanitizeRankName" "#endif`n}`n#endif"
$package = Slice-Source $projectionSource '#if LIFEPUNCH_PACKAGE' '#else'
$package = $package.Replace('#if LIFEPUNCH_PACKAGE', '').Replace('class LpParityHost', 'class PackageParityHost')
$generated = @"
using Dxura.RP.Shared;
using LifePunch.DXRP.Addons.StaffMenu;
public partial class RankSystem {
$snapshot
}
internal static partial class LpParityHost {
$projection
$sanitizer
}
$package
"@

New-Item -ItemType Directory -Path $generatedRoot -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $generatedRoot 'Generated.cs'), $utf8.GetBytes($generated))
foreach ($relative in @('Api/Dtos/RankDto.cs', 'Api/Enums/RankFlags.cs', 'Addons/lifepunch/lifepunchulx/LpParityModels.cs')) {
    $source = Join-Path $sourceRoot $relative
    [IO.File]::WriteAllBytes((Join-Path $generatedRoot ([IO.Path]::GetFileName($source))), [IO.File]::ReadAllBytes($source))
}

Push-Location $testRoot
try {
    dotnet run --project RankSnapshotChecks.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Rank snapshot regression checks failed.' }
}
finally { Pop-Location }