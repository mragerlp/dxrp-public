[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

# Independent measurements from sbox-native inspect_model_geometry on engine
# 26.08.19. The source hash pins the AR-15 measurement to the FBX that produced
# it; the M4A1 span is the Facepunch assault-rifle world-model baseline.
$expectedSourceHash = '69BB9AFDC7CF19766A2127811498AA41AD02694E9B54A780CD3744D92D87808A'
$ar15UnitScaleLongAxis = 38.923767
$m4a1WorldLongAxis = 31.099375
$allowedRelativeError = 0.02
$targetRendererScale = $m4a1WorldLongAxis / $ar15UnitScaleLongAxis

$sourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\source\generated\ar15_native_clean.fbx'
$modelDocPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\models\native_candidate\ar15_world.vmdl'
$worldPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\w_ar15\w_ar15.prefab'
$expectedModel = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_world.vmdl'
$expectedRendererPath = 'w_ar15/Model/ar15_world'

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Read-Vector3( [string] $Value )
{
	$parts = @($Value.Split( ',' ))
	if ( $parts.Count -ne 3 )
	{
		throw "Expected Vector3 text, got '$Value'."
	}

	return @($parts | ForEach-Object {
		[double]::Parse( $_, [Globalization.CultureInfo]::InvariantCulture )
	})
}

function Get-PrefabScaleNodes(
	[object] $Node,
	[double] $ParentUniformScale,
	[bool] $ParentChainIsUniform,
	[string] $ParentPath
)
{
	$localScale = Read-Vector3 ([string]$Node.Scale)
	$localIsUniform =
		[Math]::Abs( $localScale[0] - $localScale[1] ) -le 0.0000001 -and
		[Math]::Abs( $localScale[0] - $localScale[2] ) -le 0.0000001
	$chainIsUniform = $ParentChainIsUniform -and $localIsUniform
	$cumulativeUniformScale = $ParentUniformScale * [double]$localScale[0]
	$path = if ( [string]::IsNullOrWhiteSpace( $ParentPath ) ) {
		[string]$Node.Name
	} else {
		"$ParentPath/$($Node.Name)"
	}

	[pscustomobject]@{
		Node = $Node
		Path = $path
		LocalScale = $localScale
		CumulativeUniformScale = $cumulativeUniformScale
		UniformChain = $chainIsUniform
	}

	foreach ( $child in @($Node.Children) )
	{
		Get-PrefabScaleNodes $child $cumulativeUniformScale $chainIsUniform $path
	}
}

function Get-RendererScaleInfoFromDocument(
	[object] $Prefab,
	[string] $Label
)
{
	$matches = @(
		Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '' | Where-Object {
			@($_.Node.Components | Where-Object {
				$_.PSObject.Properties.Name -contains 'Model' -and
				[string]$_.Model -ceq $expectedModel
			}).Count -eq 1
		}
	)
	if ( $matches.Count -ne 1 )
	{
		throw "$Label must contain exactly one renderer owner for $expectedModel; found $($matches.Count)."
	}

	return $matches[0]
}

foreach ( $requiredPath in @($sourcePath, $modelDocPath, $worldPrefabPath) )
{
	if ( !(Test-Path -LiteralPath $requiredPath -PathType Leaf) )
	{
		throw "Missing AR-15 scale-contract input: $requiredPath"
	}
}

$actualSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
if ( $actualSourceHash -cne $expectedSourceHash )
{
	Add-Failure 'AR-15 source FBX changed; remeasure compiled bounds before accepting scale.'
}

$modelDocText = Get-Content -LiteralPath $modelDocPath -Raw
$sourceMatches = [regex]::Matches(
	$modelDocText,
	'(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$' )
$scaleMatches = [regex]::Matches(
	$modelDocText,
	'(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$' )
if ( $sourceMatches.Count -ne 1 -or
	$sourceMatches[0].Groups['value'].Value -cne 'addons/lifepunch/lpweapons/ar15/source/generated/ar15_native_clean.fbx' )
{
	Add-Failure 'AR-15 world ModelDoc must keep one exact generated-source filename.'
}
if ( $scaleMatches.Count -ne 1 )
{
	Add-Failure "AR-15 world ModelDoc must contain one explicit import_scale; found $($scaleMatches.Count)."
	$modelImportScale = $null
}
else
{
	$modelImportScale = [double]::Parse(
		$scaleMatches[0].Groups['value'].Value,
		[Globalization.CultureInfo]::InvariantCulture )
	if ( [Math]::Abs( $modelImportScale - 39.37008 ) -gt 0.0000001 )
	{
		Add-Failure "AR-15 world import scale changed from the measured 39.37008 seam: $modelImportScale."
	}
}

$prefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$scaleInfo = Get-RendererScaleInfoFromDocument $prefab $worldPrefabPath
if ( $scaleInfo.Path -cne $expectedRendererPath )
{
	Add-Failure "AR-15 world renderer moved from the scale seam $expectedRendererPath to $($scaleInfo.Path)."
}
if ( !$scaleInfo.UniformChain )
{
	Add-Failure "AR-15 world renderer has non-uniform scale in its ancestry: $($scaleInfo.Path)."
}

$localScale = $scaleInfo.LocalScale
if ( [Math]::Abs( $localScale[0] - $localScale[1] ) -gt 0.0000001 -or
	[Math]::Abs( $localScale[0] - $localScale[2] ) -gt 0.0000001 )
{
	Add-Failure "AR-15 visible world renderer scale must remain uniform; got $($localScale -join ',')."
}

$effectiveLongAxis = $ar15UnitScaleLongAxis * [Math]::Abs( $scaleInfo.CumulativeUniformScale )
$worldRatio = $effectiveLongAxis / $m4a1WorldLongAxis
if ( [Math]::Abs( $worldRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("AR-15 world long axis is {0:N4}x the M4A1 baseline; expected 1.00x +/- {1:P0}." -f $worldRatio, $allowedRelativeError)
}

# Negative controls mutate only in-memory prefab documents. They prove the
# assertion rejects both the original identity-scale defect and hidden ancestor
# scaling, instead of checking only the visible child's local value.
$identityProbePrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$identityProbeInfo = Get-RendererScaleInfoFromDocument $identityProbePrefab 'AR-15 identity-scale negative control'
$identityProbeInfo.Node.Scale = '1,1,1'
$identityProbeInfo = Get-RendererScaleInfoFromDocument $identityProbePrefab 'AR-15 identity-scale negative control'
$identityProbeRatio = (
	$ar15UnitScaleLongAxis * [Math]::Abs( $identityProbeInfo.CumulativeUniformScale )
) / $m4a1WorldLongAxis
$identityProbeRejected = [Math]::Abs( $identityProbeRatio - 1.0 ) -gt $allowedRelativeError
if ( !$identityProbeRejected )
{
	Add-Failure 'AR-15 world scale contract is blind to the original identity-scale defect.'
}

$ancestorProbePrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$ancestorProbePrefab.RootObject.Scale = '2.8,2.8,2.8'
$ancestorProbeInfo = Get-RendererScaleInfoFromDocument $ancestorProbePrefab 'AR-15 ancestor-scale negative control'
$ancestorProbeRatio = (
	$ar15UnitScaleLongAxis * [Math]::Abs( $ancestorProbeInfo.CumulativeUniformScale )
) / $m4a1WorldLongAxis
$ancestorProbeRejected = [Math]::Abs( $ancestorProbeRatio - 1.0 ) -gt $allowedRelativeError
if ( !$ancestorProbeRejected )
{
	Add-Failure 'AR-15 world scale contract is blind to a 2.8x ancestor/root scale mutation.'
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapon = 'AR-15'
	surface = 'world'
	sourceSha256 = $actualSourceHash
	modelImportScale = $modelImportScale
	rendererPath = $scaleInfo.Path
	rendererScale = $localScale
	cumulativeScale = $scaleInfo.CumulativeUniformScale
	targetRendererScale = $targetRendererScale
	m4a1WorldLongAxis = $m4a1WorldLongAxis
	effectiveWorldLongAxis = $effectiveLongAxis
	worldRatio = $worldRatio
	identityMutationRatio = $identityProbeRatio
	identityMutationRejected = $identityProbeRejected
	oversizeAncestorMutationRatio = $ancestorProbeRatio
	oversizeAncestorMutationRejected = $ancestorProbeRejected
	allowedRelativeError = $allowedRelativeError
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 6
if ( $failures.Count -gt 0 )
{
	exit 1
}
