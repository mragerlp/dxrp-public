<#
.SYNOPSIS
  Builds MANIFEST.txt rows and a layout dry-run for the private LFS asset repo (mragerlp/lifepunch-assets).

.DESCRIPTION
  Read-only on the source tree: it enumerates files, hashes them and writes ONLY under -OutDir.
  It never copies, moves or deletes, and it never runs git. Population of the real repo waits
  for the repo to exist and for the principal's "go ASSET-REPO-1".

  Sets:
    lpweapons      every file under <GameRoot>\Assets\addons\lifepunch\lpweapons
    lpbitcoin-excl lpbitcoin files the fork does not carry: any folder named _archive, audit,
                   source or provenance (or containing "extract"), or any file over -MaxForkBytes

  Never-list: names in -NeverListPath (one file name per line, kept OUTSIDE any repo) are
  withheld. A withheld file is not opened and not hashed, and its name is not written. It
  becomes a "WITHHELD" row carrying only its set and an ordinal. AGENTS.md: such files are never
  touched, opened, listed or compared.

  MANIFEST.txt row (tab-separated): repo_path  bytes  sha256  licence
  The licence comes from the nearest ATTRIBUTION*/LICENSE*/PROVENANCE* file walking up to the
  unit root. If that file has a table row naming this file, its Licence column is used.
  Otherwise the value is "BLANK" (the file says left blank), "SEE <rel>" (a file exists but
  has no row for this name) or "NONE".

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools\packaging\New-AssetRepoManifest.ps1 `
    -GameRoot D:\Cavelux\dxrp-workbench\game -OutDir C:\temp\asset-repo-dryrun -NeverListPath C:\temp\neverlist.txt
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $GameRoot,
    [Parameter(Mandatory)] [string] $OutDir,
    [string] $NeverListPath,
    [long] $MaxForkBytes = 50000000,
    [long] $LfsMinBytes = 1048576,
    [string[]] $LfsExtensions = @('.fbx', '.blend', '.zip', '.png', '.tga', '.psd')
)
$ErrorActionPreference = 'Stop'

$GameRoot = (Resolve-Path $GameRoot).Path.TrimEnd('\')
$outFull = [IO.Path]::GetFullPath($OutDir).TrimEnd('\')
if ($outFull.StartsWith($GameRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutDir is inside GameRoot; this tool never writes to the source tree."
}
New-Item -ItemType Directory -Force -Path $outFull | Out-Null

$never = @{}
if ($NeverListPath) {
    foreach ($n in Get-Content -LiteralPath $NeverListPath) {
        $t = $n.Trim()
        if ($t -and -not $t.StartsWith('#')) { $never[$t.ToLowerInvariant()] = $true }
    }
}

$addons = Join-Path $GameRoot 'Assets\addons\lifepunch'
$exclSeg = @('_archive', 'audit', 'source', 'provenance')

function Get-Licence([string] $file, [string] $unitRoot, [hashtable] $cache) {
    $name = [IO.Path]::GetFileName($file)
    $dir = [IO.Path]::GetDirectoryName($file)
    while ($dir.Length -ge $unitRoot.Length) {
        if (-not $cache.ContainsKey($dir)) {
            $cache[$dir] = @(Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^(ATTRIBUTION|LICEN[CS]E|PROVENANCE)' -and -not $never.ContainsKey($_.Name.ToLowerInvariant()) })
        }
        foreach ($a in $cache[$dir]) {
            $text = [IO.File]::ReadAllText($a.FullName)
            foreach ($line in $text -split "`n") {
                if ($line -match '^\s*\|' -and $line -match [regex]::Escape($name)) {
                    $cells = @($line.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
                    if ($cells.Count -ge 5 -and $cells[0] -eq $name) {
                        $lic = $cells[$cells.Count - 1]
                        if ($lic) { return $lic } else { return 'BLANK' }
                    }
                }
            }
            if ($text -match '(?i)left blank') { return 'BLANK' }
            return 'SEE ' + $a.FullName.Substring($addons.Length + 1).Replace('\', '/')
        }
        if ($dir -eq $unitRoot) { break }
        $dir = [IO.Path]::GetDirectoryName($dir)
    }
    return 'NONE'
}

$rows = New-Object System.Collections.Generic.List[object]
$withheld = @{}
$licCache = @{}

function Add-Set([string] $set, [string] $unitRoot, [scriptblock] $filter) {
    $i = 0
    foreach ($f in Get-ChildItem -LiteralPath $unitRoot -File -Recurse -Force) {
        if (-not (& $filter $f)) { continue }
        $rel = $f.FullName.Substring($addons.Length + 1).Replace('\', '/')
        if ($never.ContainsKey($f.Name.ToLowerInvariant())) {
            $i++
            $script:withheld[$set] = 1 + [int]$script:withheld[$set]
            $script:rows.Add([pscustomobject]@{ Set = $set; Path = "WITHHELD-$set-$i"; Bytes = ''; Sha256 = ''; Licence = 'WITHHELD (never-list)'; Ext = ''; Lfs = '' })
            continue
        }
        $ext = $f.Extension.ToLowerInvariant()
        $lfs = ($LfsExtensions -contains $ext) -and ($f.Length -gt $LfsMinBytes)
        $script:rows.Add([pscustomobject]@{
            Set = $set; Path = $rel; Bytes = $f.Length
            Sha256 = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
            Licence = (Get-Licence $f.FullName $unitRoot $script:licCache); Ext = $ext; Lfs = $lfs
        })
    }
}

Add-Set 'lpweapons' (Join-Path $addons 'lpweapons') { param($f) $true }
$btc = Join-Path $addons 'lpbitcoin'
Add-Set 'lpbitcoin-excl' $btc {
    param($f)
    $segs = $f.DirectoryName.Substring($btc.Length).Trim('\') -split '\\'
    $hit = $segs | Where-Object { $exclSeg -contains $_ -or $_ -like '*extract*' }
    [bool]$hit -or $f.Length -gt $MaxForkBytes
}

# MANIFEST.txt (LF, UTF-8 without BOM)
$utf8 = New-Object System.Text.UTF8Encoding($false)
$lines = @("# lifepunch-assets MANIFEST - generated $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')) from $GameRoot",
           "# repo_path`tbytes`tsha256`tlicence")
$lines += $rows | ForEach-Object { "$($_.Path)`t$($_.Bytes)`t$($_.Sha256)`t$($_.Licence)" }
[IO.File]::WriteAllText((Join-Path $outFull 'MANIFEST.txt'), ($lines -join "`n") + "`n", $utf8)

# .gitattributes proposal: one LFS pattern per extension that has any file over LfsMinBytes
$lfsExt = $rows | Where-Object { $_.Lfs -eq $true } | Select-Object -ExpandProperty Ext -Unique | Sort-Object
$ga = @('# LFS patterns proposed by New-AssetRepoManifest.ps1') + ($lfsExt | ForEach-Object { "*$_ filter=lfs diff=lfs merge=lfs -text" })
[IO.File]::WriteAllText((Join-Path $outFull 'gitattributes.proposed'), ($ga -join "`n") + "`n", $utf8)

# Dry-run summary
$real = $rows | Where-Object { $_.Bytes -ne '' }
$sum = New-Object System.Text.StringBuilder
[void]$sum.AppendLine("DRY-RUN lifepunch-assets layout (nothing copied, nothing pushed)")
foreach ($s in 'lpweapons', 'lpbitcoin-excl') {
    $r = @($real | Where-Object Set -eq $s)
    $b = ($r | Measure-Object Bytes -Sum).Sum
    $l = @($r | Where-Object { $_.Lfs -eq $true })
    $lb = ($l | Measure-Object Bytes -Sum).Sum
    [void]$sum.AppendLine(("{0}: {1} files, {2:N0} B ({3:N1} MB); LFS-eligible {4} files {5:N0} B; withheld {6}" -f $s, $r.Count, $b, ($b / 1e6), $l.Count, $lb, [int]$withheld[$s]))
}
[void]$sum.AppendLine("Licence values: " + (($real | Group-Object { ($_.Licence -split ' ')[0] } | Sort-Object Count -Descending | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', '))
[void]$sum.AppendLine("Over $MaxForkBytes B: " + @($real | Where-Object { [long]$_.Bytes -gt $MaxForkBytes }).Count)
[void]$sum.AppendLine("LFS extensions with a file over $LfsMinBytes B: " + ($lfsExt -join ' '))
foreach ($e in $LfsExtensions) {
    $r = @($real | Where-Object Ext -eq $e)
    if ($r.Count) { [void]$sum.AppendLine(("  {0}: {1} files, {2} over {3} B" -f $e, $r.Count, @($r | Where-Object { $_.Lfs -eq $true }).Count, $LfsMinBytes)) }
}
[void]$sum.AppendLine("Top-level layout:")
$real | Group-Object { ($_.Path -split '/')[0..1] -join '/' } | Sort-Object Name | ForEach-Object {
    [void]$sum.AppendLine(("  {0}/  {1} files  {2:N1} MB" -f $_.Name, $_.Count, (($_.Group | Measure-Object Bytes -Sum).Sum / 1e6)))
}
[IO.File]::WriteAllText((Join-Path $outFull 'DRYRUN_SUMMARY.txt'), $sum.ToString().Replace("`r`n", "`n"), $utf8)
Write-Output $sum.ToString()
