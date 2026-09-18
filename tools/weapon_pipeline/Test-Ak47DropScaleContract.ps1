[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

# Independent sbox-native inspect_model_geometry measurements on engine
# 26.08.19. Held AK-47 FP/TP already matches the M4A1 class envelope; the
# defect covered here is the direct DTO world-model path bypassing that scale.
$expectedSourceHash = 'FBD90B3882F9C59A696008DD83A731F4CCCDEB9A01D2CFA35D61FFAC81808CA6'
$expectedModelDocHash = 'DC66AE0E3ECCD8B726841B9B0705259A8BC277924A1B41F8EA1BA2BA36FECA89'
$ak47UnitLongAxis = 36.938442
$m4a1ViewLongAxis = 31.093287
$m4a1WorldLongAxis = 31.099375
$allowedRelativeError = 0.02
$scaleTolerance = 0.0000001

$sourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ak47\models\lifepunch\ak47\w_ak47\source\ak47.fbx'
$modelDocPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ak47\models\lifepunch\ak47\w_ak47\w_ak47.vmdl'
$viewPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ak47\equipment\vm_ak47\vm_ak47.prefab'
$worldPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ak47\equipment\w_ak47\w_ak47.prefab'
$extensionsPath = Join-Path $repoRoot 'game\Code\Utilities\Resource\GameModeEquipmentDtoExtensions.cs'
$droppedEquipmentPath = Join-Path $repoRoot 'game\Code\Equipment\DroppedEquipment.cs'
$playerEquipmentPath = Join-Path $repoRoot 'game\Code\Player\Player.Equipment.cs'
$shipmentPath = Join-Path $repoRoot 'game\Code\Entity\Entities\ShipmentEntity.cs'

$expectedModelSource = 'addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/source/ak47.fbx'
$expectedModel = 'addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl'
$expectedViewRendererPath = 'vm_ak47/weapon_root/ak47_mesh'
$expectedWorldRendererPath = 'w_ak47/Model/ak47_mesh'

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Resolve-WorldModelScaleProbe( [object] $ConfiguredScale, [double] $PrefabScale )
{
	if ( $null -ne $ConfiguredScale )
	{
		$value = [double]$ConfiguredScale
		if ( ![double]::IsNaN( $value ) -and ![double]::IsInfinity( $value ) -and $value -gt 0.0 )
		{
			return $value
		}
	}

	return $PrefabScale
}

function Test-WorldModelScaleImplementation( [string] $Text )
{
	return (
		$Text.Contains( 'var configuredScale = dto.Content()?.WorldModelScale;' ) -and
		$Text.Contains( 'float.IsFinite( configuredScale.Value )' ) -and
		$Text.Contains( 'return configuredScale.Value;' ) -and
		$Text.Contains( 'return TryLoadWorldModelScaleFromPrefab( dto.PrefabPath() );' ) -and
		$Text.Contains( 'prefab.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants )' ) -and
		$Text.Contains( 'IsUsableWorldPreviewRenderer( equipment.ModelRenderer )' ) -and
		$Text.Contains( '!renderer.Enabled || !renderer.GameObject.Enabled' ) -and
		$Text.Contains( 'var scale = renderer.WorldScale;' ) -and
		$Text.Contains( 'return scale.x;' )
)
}

function Read-Vector3( [string] $Value )
{
	$values = @($Value.Split( ',' ))
	if ( $values.Count -ne 3 )
	{
		throw "Expected Vector3 text, got '$Value'."
	}

	return @($values | ForEach-Object {
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
		[Math]::Abs( $localScale[0] - $localScale[1] ) -le $scaleTolerance -and
		[Math]::Abs( $localScale[0] - $localScale[2] ) -le $scaleTolerance
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

function Get-Ak47ScaleInfo( [object] $Prefab, [string] $Label )
{
	$matches = @(Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '' | Where-Object {
		@($_.Node.Components | Where-Object {
			$_.PSObject.Properties.Name -contains 'Model' -and
			[string]$_.Model -ceq $expectedModel
		}).Count -eq 1
	})
	if ( $matches.Count -ne 1 )
	{
		throw "$Label must contain exactly one active AK-47 renderer for $expectedModel; found $($matches.Count)."
	}

	return $matches[0]
}

foreach ( $requiredPath in @(
	$sourcePath,
	$modelDocPath,
	$viewPrefabPath,
	$worldPrefabPath,
	$extensionsPath,
	$droppedEquipmentPath,
	$playerEquipmentPath,
	$shipmentPath
) )
{
	if ( !(Test-Path -LiteralPath $requiredPath -PathType Leaf) )
	{
		throw "Missing AK-47 drop-scale input: $requiredPath"
	}
}

$actualSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
if ( $actualSourceHash -cne $expectedSourceHash )
{
	Add-Failure 'AK-47 source FBX changed; remeasure compiled bounds before accepting drop scale.'
}

$actualModelDocHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $modelDocPath).Hash
if ( $actualModelDocHash -cne $expectedModelDocHash )
{
	Add-Failure 'AK-47 active ModelDoc changed; remeasure compiled bounds before accepting drop scale.'
}

$modelDocText = Get-Content -LiteralPath $modelDocPath -Raw
$sourceMatches = [regex]::Matches(
	$modelDocText,
	'(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$' )
$scaleMatches = [regex]::Matches(
	$modelDocText,
	'(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$' )
if ( $sourceMatches.Count -ne 1 -or
	$sourceMatches[0].Groups['value'].Value -cne $expectedModelSource )
{
	Add-Failure 'AK-47 ModelDoc must keep one exact active source filename.'
}
if ( $scaleMatches.Count -ne 1 )
{
	Add-Failure 'AK-47 ModelDoc must keep one explicit import_scale.'
}
else
{
	$importScale = [double]::Parse(
		$scaleMatches[0].Groups['value'].Value,
		[Globalization.CultureInfo]::InvariantCulture )
	if ( [Math]::Abs( $importScale - 0.03937008 ) -gt $scaleTolerance )
	{
		Add-Failure "AK-47 import scale changed from the measured 0.03937008 seam: $importScale."
	}
}

$viewPrefab = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$worldPrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$viewInfo = Get-Ak47ScaleInfo $viewPrefab $viewPrefabPath
$worldInfo = Get-Ak47ScaleInfo $worldPrefab $worldPrefabPath

if ( $viewInfo.Path -cne $expectedViewRendererPath )
{
	Add-Failure "AK-47 view renderer moved from $expectedViewRendererPath to $($viewInfo.Path)."
}
if ( $worldInfo.Path -cne $expectedWorldRendererPath )
{
	Add-Failure "AK-47 world renderer moved from $expectedWorldRendererPath to $($worldInfo.Path)."
}
foreach ( $surface in @(
	[pscustomobject]@{ Name = 'view'; Info = $viewInfo },
	[pscustomobject]@{ Name = 'world'; Info = $worldInfo }
) )
{
	if ( !$surface.Info.UniformChain )
	{
		Add-Failure "AK-47 $($surface.Name) renderer has non-uniform scale in its ancestry."
	}
	if ( $surface.Info.CumulativeUniformScale -le 0.0 )
	{
		Add-Failure "AK-47 $($surface.Name) renderer must keep a positive cumulative scale."
	}
}

$viewRatio = ($ak47UnitLongAxis * $viewInfo.CumulativeUniformScale) / $m4a1ViewLongAxis
$worldRatio = ($ak47UnitLongAxis * $worldInfo.CumulativeUniformScale) / $m4a1WorldLongAxis
if ( [Math]::Abs( $viewRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("AK-47 held view is {0:N4}x the M4A1 class baseline." -f $viewRatio)
}
if ( [Math]::Abs( $worldRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("AK-47 held world model is {0:N4}x the M4A1 class baseline." -f $worldRatio)
}

# The product already exposes nullable WorldModelScale on addon content. A
# positive finite configured value intentionally wins; only an absent/invalid
# value falls back to the chosen prefab renderer. The presentation path must
# then apply the resolved scale to the dropped root (renderer + collider),
# placement clearance, and shipment preview.
$extensionsText = Get-Content -LiteralPath $extensionsPath -Raw
$droppedText = Get-Content -LiteralPath $droppedEquipmentPath -Raw
$playerText = Get-Content -LiteralPath $playerEquipmentPath -Raw
$shipmentText = Get-Content -LiteralPath $shipmentPath -Raw

$scaleResolutionImplementationComplete = Test-WorldModelScaleImplementation $extensionsText
$hasConfiguredScalePriority =
	$extensionsText.Contains( 'return configuredScale.Value;' )
$hasPrefabScaleFallback =
	$extensionsText.Contains( 'return TryLoadWorldModelScaleFromPrefab( dto.PrefabPath() );' ) -and
	$extensionsText.Contains( 'return scale.x;' )
$prefersAuthoredEquipmentRenderer =
	$extensionsText.Contains( 'prefab.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants )' ) -and
	$extensionsText.Contains( 'IsUsableWorldPreviewRenderer( equipment.ModelRenderer )' )
$filtersDisabledRenderers =
	$extensionsText.Contains( '!renderer.Enabled || !renderer.GameObject.Enabled' )
$dropAppliesScale =
	[regex]::IsMatch(
		$droppedText,
		'(?s)var\s+worldModelScale\s*=\s*dto\.WorldModelScale\(\)\s*;.{0,1200}?go\.WorldScale\s*=\s*worldModelScale\s*;' )
$placementUsesScale =
	[regex]::Matches(
		$playerText,
		'worldModel\.Bounds\.Size\.Length\s*\*\s*resource\.WorldModelScale\(\)' ).Count -eq 2
$shipmentUsesScale =
	$shipmentText.Contains( 'previewRenderer.WorldScale = equipment.WorldModelScale() * 1.1f;' )

$surfaceConsumersComplete =
	$scaleResolutionImplementationComplete -and
	$dropAppliesScale -and
	$placementUsesScale -and
	$shipmentUsesScale

$prefabScale = [double]$worldInfo.CumulativeUniformScale
$scaleResolutionCases = @(
	[pscustomobject]@{ Name = 'absent'; Configured = $null; Expected = $prefabScale; Source = 'Prefab' },
	[pscustomobject]@{ Name = 'zero'; Configured = 0.0; Expected = $prefabScale; Source = 'Prefab' },
	[pscustomobject]@{ Name = 'negative'; Configured = -1.0; Expected = $prefabScale; Source = 'Prefab' },
	[pscustomobject]@{ Name = 'nan'; Configured = [double]::NaN; Expected = $prefabScale; Source = 'Prefab' },
	[pscustomobject]@{ Name = 'matching-configured'; Configured = $prefabScale; Expected = $prefabScale; Source = 'Configured' },
	[pscustomobject]@{ Name = 'explicit-one'; Configured = 1.0; Expected = 1.0; Source = 'Configured' }
)
foreach ( $case in $scaleResolutionCases )
{
	$case | Add-Member -NotePropertyName Actual -NotePropertyValue (Resolve-WorldModelScaleProbe $case.Configured $prefabScale)
	if ( [Math]::Abs( [double]$case.Actual - [double]$case.Expected ) -gt 0.000000001 )
	{
		Add-Failure "World-model scale resolution case '$($case.Name)' returned $($case.Actual), expected $($case.Expected)."
	}
}

$effectiveDropScale = Resolve-WorldModelScaleProbe $null $prefabScale
$heldWorldLongAxis = $ak47UnitLongAxis * $worldInfo.CumulativeUniformScale
$dropLongAxis = $ak47UnitLongAxis * $effectiveDropScale
$dropHeldRatio = $dropLongAxis / $heldWorldLongAxis
if ( !$surfaceConsumersComplete -or
	[Math]::Abs( $dropHeldRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("AK-47 direct world-model surfaces bypass the held scale (drop/held={0:N6}; prefabFallback={1}; drop={2}; placement={3}; shipment={4})." -f
		$dropHeldRatio,
		$hasPrefabScaleFallback,
		$dropAppliesScale,
		$placementUsesScale,
		$shipmentUsesScale)
}

$explicitOneScale = Resolve-WorldModelScaleProbe 1.0 $prefabScale
$explicitOneDropHeldRatio = ($ak47UnitLongAxis * $explicitOneScale) / $heldWorldLongAxis
$explicitOneReportsConfiguredOverride = [Math]::Abs( $explicitOneScale - 1.0 ) -le 0.000000001
if ( !$explicitOneReportsConfiguredOverride )
{
	Add-Failure 'World-model scale resolution hides an explicit configured scale of 1.0 behind prefab fallback.'
}

# In-memory arithmetic negatives prove the contract rejects the current direct
# scale-1 drop and a hidden 2.8x held ancestor without changing either prefab.
$unscaledDropRatio = $ak47UnitLongAxis / $heldWorldLongAxis
$unscaledDropRejected = [Math]::Abs( $unscaledDropRatio - 1.0 ) -gt $allowedRelativeError
if ( !$unscaledDropRejected )
{
	Add-Failure 'AK-47 drop-scale contract is blind to the current direct scale-1 world model.'
}

$ancestorProbe = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$ancestorProbe.RootObject.Scale = '2.8,2.8,2.8'
$ancestorInfo = Get-Ak47ScaleInfo $ancestorProbe 'AK-47 ancestor-scale negative control'
$ancestorRatio = ($ak47UnitLongAxis * $ancestorInfo.CumulativeUniformScale) / $m4a1WorldLongAxis
$ancestorRejected = [Math]::Abs( $ancestorRatio - 1.0 ) -gt $allowedRelativeError
if ( !$ancestorRejected )
{
	Add-Failure 'AK-47 drop-scale contract is blind to a 2.8x held ancestor scale.'
}

$fallbackMutationText = $extensionsText.Replace( 'return scale.x;', 'return 1f;' )
$fallbackMutationRejected = !(Test-WorldModelScaleImplementation $fallbackMutationText)
if ( !$fallbackMutationRejected )
{
	Add-Failure 'AK-47 drop-scale contract is blind to a scale-1 prefab fallback mutation.'
}

$configuredPriorityMutationText = $extensionsText.Replace( 'return configuredScale.Value;', 'return TryLoadWorldModelScaleFromPrefab( dto.PrefabPath() );' )
$configuredPriorityMutationRejected = !(Test-WorldModelScaleImplementation $configuredPriorityMutationText)
if ( !$configuredPriorityMutationRejected )
{
	Add-Failure 'AK-47 drop-scale contract is blind to loss of configured-scale priority.'
}

$exitCode = if ( $failures.Count -eq 0 ) { 0 } else { 1 }
$result = [ordered]@{
	contract = 'ak47-drop-scale'
	result = if ( $exitCode -eq 0 ) { 'PASS' } else { 'FAIL' }
	sourceSha256 = $actualSourceHash
	held = [ordered]@{
		viewScale = $viewInfo.CumulativeUniformScale
		worldScale = $worldInfo.CumulativeUniformScale
		viewM4a1Ratio = $viewRatio
		worldM4a1Ratio = $worldRatio
		worldLongAxis = $heldWorldLongAxis
	}
	directWorldModel = [ordered]@{
		configuredScalePriority = $hasConfiguredScalePriority
		prefabScaleFallback = $hasPrefabScaleFallback
		prefersAuthoredEquipmentRenderer = $prefersAuthoredEquipmentRenderer
		filtersDisabledRenderers = $filtersDisabledRenderers
		dropAppliesScale = $dropAppliesScale
		placementUsesScale = $placementUsesScale
		shipmentUsesScale = $shipmentUsesScale
		fallbackScale = $effectiveDropScale
		fallbackLongAxis = $dropLongAxis
		fallbackDropHeldRatio = $dropHeldRatio
		configuredScaleCases = @($scaleResolutionCases | ForEach-Object {
			[ordered]@{ name = $_.Name; source = $_.Source; expected = $_.Expected; actual = $_.Actual }
		})
		explicitOneDropHeldRatio = $explicitOneDropHeldRatio
		explicitOneReportsConfiguredOverride = $explicitOneReportsConfiguredOverride
		authoritativeAk47ConfiguredScale = 'UNVERIFIED_PORTAL_OR_LIVE_CONFIG'
	}
	negativeControls = [ordered]@{
		unscaledDropRatio = $unscaledDropRatio
		unscaledDropRejected = $unscaledDropRejected
		oversizeAncestorRatio = $ancestorRatio
		oversizeAncestorRejected = $ancestorRejected
		fallbackMutationRejected = $fallbackMutationRejected
		configuredPriorityMutationRejected = $configuredPriorityMutationRejected
	}
	allowedRelativeError = $allowedRelativeError
	failures = @($failures)
	exitCode = $exitCode
}

$result | ConvertTo-Json -Compress -Depth 7
exit $exitCode
