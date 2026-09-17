[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$weaponRoot = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons'
$failures = [System.Collections.Generic.List[string]]::new()
$modelDocs = @(Get-ChildItem -LiteralPath $weaponRoot -Recurse -Filter '*.vmdl' -File)
$absoluteReferenceCount = 0
$invalidSourcePhysicsCount = 0
$aksBodySplitSource = 'game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_body_minus_bolt.vmdl'

foreach ( $modelDoc in $modelDocs )
{
	$relativePath = $modelDoc.FullName.Substring( $repoRoot.Length + 1 ).Replace( '\', '/' )
	$matches = @(Select-String -LiteralPath $modelDoc.FullName -Pattern '^\s*(?:filename|to)\s*=\s*"[A-Za-z]:[\\/]')
	foreach ( $match in $matches )
	{
		$absoluteReferenceCount++
		$failures.Add( "${relativePath}:$($match.LineNumber) contains an absolute ModelDoc resource path." )
	}
}

$aksBodySplitSourcePath = Join-Path $repoRoot ($aksBodySplitSource -replace '/', [IO.Path]::DirectorySeparatorChar)
if ( Test-Path -LiteralPath $aksBodySplitSourcePath -PathType Leaf )
{
	$aksBodySplitText = Get-Content -LiteralPath $aksBodySplitSourcePath -Raw
	if ( $aksBodySplitText -match '\bPhysicsHullFromRender\b' )
	{
		$invalidSourcePhysicsCount++
		$failures.Add( "$aksBodySplitSource contains the invalid intermediate collision hull generator." )
	}
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	modelDocs = $modelDocs.Count
	absoluteReferences = $absoluteReferenceCount
	invalidSourcePhysics = $invalidSourcePhysicsCount
	failures = @($failures)
}
$summary | ConvertTo-Json -Depth 4 -Compress

if ( $failures.Count -gt 0 )
{
	exit 1
}

Write-Output "RESULT weapon_intake_modeldocs=PASS modeldocs=$($modelDocs.Count) absolute_references=0 invalid_source_physics=0"
