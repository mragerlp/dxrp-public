[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

# Independent measurements from sbox-native inspect_model_geometry on engine
# 26.08.19. The source hash pins the unit-scale AKS measurement to the FBX that
# produced it; the MP5 spans are Facepunch world/view class baselines.
$expectedSourceHash = '6EDAB0636B1D43744A0344FF9168766229D3D0DD68E845E7C21EDB9971B34780'
$aksUnitScaleLongAxis = 63.9965
$mp5WorldLongAxis = 22.586597
$mp5ViewLongAxis = 22.568323
$allowedRelativeError = 0.02

$sourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\source\fab_original\aks74u.fbx'
$worldPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\w_aks74u\w_aks74u.prefab'
$viewPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\vm_aks74u\vm_aks74u.prefab'

$modelDocs = [ordered]@{
	combined = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\aks74u.vmdl'
	body = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\aks74u_body.vmdl'
	bodyMinusBolt = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\aks74u_body_minus_bolt.vmdl'
	bolt = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\aks74u_bolt.vmdl'
	magazine = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74u\aks74u_mag.vmdl'
}

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

function Get-ModelDocImportScale( [string] $Path )
{
	if ( !(Test-Path -LiteralPath $Path -PathType Leaf) )
	{
		throw "Missing AKS-74U ModelDoc: $Path"
	}

	$text = Get-Content -LiteralPath $Path -Raw
	$matches = [regex]::Matches(
		$text,
		'(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$' )
	if ( $matches.Count -ne 1 )
	{
		throw "Expected exactly one explicit import_scale in $Path; found $($matches.Count)."
	}

	return [double]::Parse(
		$matches[0].Groups['value'].Value,
		[Globalization.CultureInfo]::InvariantCulture )
}

function Get-PrefabNodes( [object] $Node )
{
	$Node
	foreach ( $child in @($Node.Children) )
	{
		Get-PrefabNodes $child
	}
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
	$localIsPositive =
		$localScale[0] -gt 0.0 -and
		$localScale[1] -gt 0.0 -and
		$localScale[2] -gt 0.0
	$chainIsUniform = $ParentChainIsUniform -and $localIsUniform -and $localIsPositive
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
	[string] $ExpectedModel,
	[string] $Label
)
{
	$matches = @(
		Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '' | Where-Object {
			@($_.Node.Components | Where-Object {
				$_.PSObject.Properties.Name -contains 'Model' -and
				[string]$_.Model -ceq $ExpectedModel
			}).Count -eq 1
		}
	)
	if ( $matches.Count -ne 1 )
	{
		throw "$Label must contain exactly one renderer owner for $ExpectedModel; found $($matches.Count)."
	}

	return $matches[0]
}

function Get-RendererScaleInfo(
	[string] $PrefabPath,
	[string] $ExpectedModel
)
{
	if ( !(Test-Path -LiteralPath $PrefabPath -PathType Leaf) )
	{
		throw "Missing AKS-74U prefab: $PrefabPath"
	}

	$prefab = Get-Content -LiteralPath $PrefabPath -Raw | ConvertFrom-Json -Depth 100
	return Get-RendererScaleInfoFromDocument $prefab $ExpectedModel $PrefabPath
}

if ( !(Test-Path -LiteralPath $sourcePath -PathType Leaf) )
{
	throw "Missing AKS-74U source FBX: $sourcePath"
}

$actualSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
if ( $actualSourceHash -cne $expectedSourceHash )
{
	Add-Failure "Military source FBX changed; remeasure its unit-scale bounds before accepting scale."
}

$importScales = [ordered]@{}
foreach ( $entry in $modelDocs.GetEnumerator() )
{
	$importScales[$entry.Key] = Get-ModelDocImportScale $entry.Value
}

$referenceScale = [double]$importScales.combined
foreach ( $entry in $importScales.GetEnumerator() )
{
	if ( [Math]::Abs( [double]$entry.Value - $referenceScale ) -gt 0.0000001 )
	{
		Add-Failure "Military $($entry.Key) import scale $($entry.Value) diverges from combined scale $referenceScale."
	}
}

$worldScaleInfo = Get-RendererScaleInfo $worldPrefabPath 'addons/lifepunch/lpweapons/aks74u/aks74u.vmdl'
$viewScaleInfo = Get-RendererScaleInfo $viewPrefabPath 'addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl'
$worldScale = $worldScaleInfo.LocalScale
$viewScale = $viewScaleInfo.LocalScale

foreach ( $spec in @(
	[pscustomobject]@{ Name = 'world'; Scale = $worldScale },
	[pscustomobject]@{ Name = 'view'; Scale = $viewScale }
) )
{
	if ( [Math]::Abs( $spec.Scale[0] - $spec.Scale[1] ) -gt 0.0000001 -or
		[Math]::Abs( $spec.Scale[0] - $spec.Scale[2] ) -gt 0.0000001 -or
		$spec.Scale[0] -le 0.0 -or
		$spec.Scale[1] -le 0.0 -or
		$spec.Scale[2] -le 0.0 )
	{
		Add-Failure "Military $($spec.Name) visible renderer scale must remain positive and uniform; got $($spec.Scale -join ',')."
	}
}

if ( !$worldScaleInfo.UniformChain )
{
	Add-Failure "Military world renderer has a non-positive or non-uniform scale somewhere in its prefab ancestry: $($worldScaleInfo.Path)."
}
if ( !$viewScaleInfo.UniformChain )
{
	Add-Failure "Military view renderer has a non-positive or non-uniform scale somewhere in its prefab ancestry: $($viewScaleInfo.Path)."
}

$worldEffectiveLongAxis = $aksUnitScaleLongAxis * $referenceScale * [Math]::Abs( $worldScaleInfo.CumulativeUniformScale )
$viewEffectiveLongAxis = $aksUnitScaleLongAxis * [double]$importScales.bodyMinusBolt * [Math]::Abs( $viewScaleInfo.CumulativeUniformScale )
$worldRatio = $worldEffectiveLongAxis / $mp5WorldLongAxis
$viewRatio = $viewEffectiveLongAxis / $mp5ViewLongAxis

if ( [Math]::Abs( $worldRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("Military world long axis is {0:N4}x the MP5 baseline; expected 1.00x +/- {1:P0}." -f $worldRatio, $allowedRelativeError)
}
if ( [Math]::Abs( $viewRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("Military view long axis is {0:N4}x the MP5 baseline; expected 1.00x +/- {1:P0}." -f $viewRatio, $allowedRelativeError)
}

# Negative control: the principal previously observed an approximately 2.8x
# oversized Military instance. Mutate only an in-memory prefab document and
# prove the cumulative-scale assertion would reject that exact defect family.
$oversizeProbePrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$oversizeProbePrefab.RootObject.Scale = '2.8,2.8,2.8'
$oversizeProbeInfo = Get-RendererScaleInfoFromDocument `
	$oversizeProbePrefab `
	'addons/lifepunch/lpweapons/aks74u/aks74u.vmdl' `
	'AKS-74U Military 2.8x ancestor-scale negative control'
$oversizeProbeRatio = (
	$aksUnitScaleLongAxis *
	$referenceScale *
	[Math]::Abs( $oversizeProbeInfo.CumulativeUniformScale )
) / $mp5WorldLongAxis
$oversizeProbeRejected = [Math]::Abs( $oversizeProbeRatio - 1.0 ) -gt $allowedRelativeError
if ( !$oversizeProbeRejected )
{
	Add-Failure 'Military scale contract is blind to a 2.8x ancestor/root scale mutation.'
}

# Negative control: absolute-value size ratios cannot reveal a mirrored weapon.
# Mutate only an in-memory prefab document and require the ancestry contract to
# reject a negative uniform scale even though its apparent long-axis size stays
# inside the normal MP5 tolerance.
$mirroredProbePrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$mirroredProbePrefab.RootObject.Scale = '-1,-1,-1'
$mirroredProbeInfo = Get-RendererScaleInfoFromDocument `
	$mirroredProbePrefab `
	'addons/lifepunch/lpweapons/aks74u/aks74u.vmdl' `
	'AKS-74U Military mirrored ancestor-scale negative control'
$mirroredProbeRatio = (
	$aksUnitScaleLongAxis *
	$referenceScale *
	[Math]::Abs( $mirroredProbeInfo.CumulativeUniformScale )
) / $mp5WorldLongAxis
$mirroredProbeSizeWouldPass =
	[Math]::Abs( $mirroredProbeRatio - 1.0 ) -le $allowedRelativeError
$mirroredProbeRejected = !$mirroredProbeInfo.UniformChain
if ( !$mirroredProbeSizeWouldPass )
{
	Add-Failure 'Military mirrored-scale negative-control fixture drifted outside the MP5 size tolerance.'
}
if ( !$mirroredProbeRejected )
{
	Add-Failure 'Military scale contract is blind to a negative uniform ancestor/root scale mutation.'
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapon = 'AKS-74U Military'
	sourceSha256 = $actualSourceHash
	modelImportScales = $importScales
	worldRendererScale = $worldScale
	viewRendererScale = $viewScale
	worldCumulativeScale = $worldScaleInfo.CumulativeUniformScale
	viewCumulativeScale = $viewScaleInfo.CumulativeUniformScale
	worldRendererPath = $worldScaleInfo.Path
	viewRendererPath = $viewScaleInfo.Path
	mp5WorldLongAxis = $mp5WorldLongAxis
	mp5ViewLongAxis = $mp5ViewLongAxis
	effectiveWorldLongAxis = $worldEffectiveLongAxis
	effectiveViewLongAxis = $viewEffectiveLongAxis
	worldRatio = $worldRatio
	viewRatio = $viewRatio
	oversizeAncestorMutationRatio = $oversizeProbeRatio
	oversizeAncestorMutationRejected = $oversizeProbeRejected
	mirroredAncestorMutationRatio = $mirroredProbeRatio
	mirroredAncestorSizeWouldPass = $mirroredProbeSizeWouldPass
	mirroredAncestorMutationRejected = $mirroredProbeRejected
	allowedRelativeError = $allowedRelativeError
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 6
if ( $failures.Count -gt 0 )
{
	exit 1
}
