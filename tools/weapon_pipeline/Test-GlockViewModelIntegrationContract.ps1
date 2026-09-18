[CmdletBinding()]
param(
	[string] $LivePrefabOverride = '',
	[string] $GeneratedPrefabOverride = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$livePrefabPath = if ( [string]::IsNullOrWhiteSpace( $LivePrefabOverride ) )
{
	Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\glock\equipment\vm_glock\vm_glock.prefab'
}
elseif ( [System.IO.Path]::IsPathRooted( $LivePrefabOverride ) )
{
	$LivePrefabOverride
}
else
{
	Join-Path $repoRoot $LivePrefabOverride
}

$generatedPrefabPath = if ( [string]::IsNullOrWhiteSpace( $GeneratedPrefabOverride ) )
{
	Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\glock\generated\custom_pistol_9mm\v_custom_pistol_9mm.prefab'
}
elseif ( [System.IO.Path]::IsPathRooted( $GeneratedPrefabOverride ) )
{
	$GeneratedPrefabOverride
}
else
{
	Join-Path $repoRoot $GeneratedPrefabOverride
}

$generatedModel = 'addons/lifepunch/lpweapons/glock/generated/custom_pistol_9mm/custom_pistol_9mm_vm.vmdl'
$armsModel = 'models/first_person/v_first_person_arms_human.vmdl'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Read-Prefab( [string] $Path, [string] $Label )
{
	if ( !(Test-Path -LiteralPath $Path -PathType Leaf) )
	{
		throw "Missing $Label prefab: $Path"
	}

	try
	{
		return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
	}
	catch
	{
		throw "Invalid JSON in $Label prefab: $($_.Exception.Message)"
	}
}

function Get-ObjectProperty( [object] $Object, [string] $Name )
{
	if ( $null -eq $Object )
	{
		return $null
	}

	$property = $Object.PSObject.Properties[$Name]
	if ( $null -eq $property )
	{
		return $null
	}

	return $property.Value
}

function Find-NodesByName( [object] $Node, [string] $Name )
{
	if ( $null -eq $Node )
	{
		return
	}

	if ( (Get-ObjectProperty $Node 'Name') -ceq $Name )
	{
		$Node
	}

	foreach ( $child in @((Get-ObjectProperty $Node 'Children')) )
	{
		Find-NodesByName $child $Name
	}
}

function Find-ComponentRecordsByType( [object] $Node, [string] $TypeName )
{
	if ( $null -eq $Node )
	{
		return
	}

	foreach ( $component in @((Get-ObjectProperty $Node 'Components')) )
	{
		if ( (Get-ObjectProperty $component '__type') -ceq $TypeName )
		{
			[pscustomobject]@{
				Component = $component
				GameObject = $Node
			}
		}
	}

	foreach ( $child in @((Get-ObjectProperty $Node 'Children')) )
	{
		Find-ComponentRecordsByType $child $TypeName
	}
}

function Test-ComponentReference( [object] $Reference, [object] $Component, [object] $GameObject, [string] $Label )
{
	if ( $null -eq $Reference )
	{
		Add-Failure "$Label reference is null."
		return
	}
	if ( [string](Get-ObjectProperty $Reference '_type') -cne 'component' -or
		[string](Get-ObjectProperty $Reference 'component_id') -cne [string](Get-ObjectProperty $Component '__guid') -or
		[string](Get-ObjectProperty $Reference 'go') -cne [string](Get-ObjectProperty $GameObject '__guid') )
	{
		Add-Failure "$Label does not resolve to the expected component."
	}
}

function Test-GameObjectReference( [object] $Reference, [object] $GameObject, [string] $Label )
{
	if ( $null -eq $Reference -or
		[string](Get-ObjectProperty $Reference '_type') -cne 'gameobject' -or
		[string](Get-ObjectProperty $Reference 'go') -cne [string](Get-ObjectProperty $GameObject '__guid') )
	{
		Add-Failure "$Label does not resolve to the expected GameObject."
	}
}

$live = Read-Prefab $livePrefabPath 'live Glock'
$generated = Read-Prefab $generatedPrefabPath 'generated Glock'

if ( [string]$live.RootObject.Name -cne 'vm_glock' )
{
	Add-Failure "Live root is '$($live.RootObject.Name)'; expected vm_glock."
}
if ( [string]$live.RootObject.Tags -cne 'player,viewmodel' )
{
	Add-Failure "Live root tags are '$($live.RootObject.Tags)'; expected player,viewmodel."
}
if ( [int]$live.RootObject.NetworkMode -ne 0 )
{
	Add-Failure "Live root NetworkMode is $($live.RootObject.NetworkMode); expected 0."
}

$viewModels = @(Find-ComponentRecordsByType $live.RootObject 'Dxura.RP.Game.ViewModel')
if ( $viewModels.Count -ne 1 -or [string]$viewModels[0].GameObject.__guid -cne [string]$live.RootObject.__guid )
{
	Add-Failure 'Live prefab must have exactly one ViewModel component on its root.'
}

$liveRenderers = @(Find-ComponentRecordsByType $live.RootObject 'Sandbox.SkinnedModelRenderer')
$generatedRendererRecords = @($liveRenderers | Where-Object { [string]$_.Component.Model -ceq $generatedModel })
$armsRendererRecords = @($liveRenderers | Where-Object { [string]$_.Component.Model -ceq $armsModel })
if ( $liveRenderers.Count -ne 2 )
{
	Add-Failure "Live prefab has $($liveRenderers.Count) skinned renderers; expected only the generated Glock and its arms."
}
if ( $generatedRendererRecords.Count -ne 1 )
{
	Add-Failure "Live prefab has $($generatedRendererRecords.Count) generated Glock renderers; expected exactly one."
}
if ( $armsRendererRecords.Count -ne 1 )
{
	Add-Failure "Live prefab has $($armsRendererRecords.Count) first-person arms renderers; expected exactly one."
}

$generatedRendererRecord = $generatedRendererRecords | Select-Object -First 1
$armsRendererRecord = $armsRendererRecords | Select-Object -First 1
if ( $null -ne $generatedRendererRecord )
{
	$renderer = $generatedRendererRecord.Component
	if ( ![bool]$renderer.UseAnimGraph -or [string]$renderer.RenderType -cne 'On' )
	{
		Add-Failure 'Generated Glock renderer must be visible and AnimGraph-enabled.'
	}
	if ( [bool]$renderer.RenderOptions.GameLayer -or ![bool]$renderer.RenderOptions.OverlayLayer )
	{
		Add-Failure 'Generated Glock renderer must use the established first-person overlay layer.'
	}
}
if ( $null -ne $armsRendererRecord )
{
	$renderer = $armsRendererRecord.Component
	if ( ![bool]$renderer.UseAnimGraph -or [string]$renderer.RenderType -cne 'On' )
	{
		Add-Failure 'Generated Glock arms must be visible and AnimGraph-enabled.'
	}
	if ( [bool]$renderer.RenderOptions.GameLayer -or ![bool]$renderer.RenderOptions.OverlayLayer )
	{
		Add-Failure 'Generated Glock arms must use the established first-person overlay layer.'
	}
	if ( $null -ne $generatedRendererRecord )
	{
		Test-ComponentReference $renderer.BoneMergeTarget $generatedRendererRecord.Component $generatedRendererRecord.GameObject 'Arms BoneMergeTarget'
	}
}

$generatedMuzzle = @(Find-NodesByName $generated.RootObject 'muzzle')
$generatedEject = @(Find-NodesByName $generated.RootObject 'eject')
$liveMuzzle = @(Find-NodesByName $live.RootObject 'muzzle')
$liveEject = @(Find-NodesByName $live.RootObject 'eject')
$liveCamera = @(Find-NodesByName $live.RootObject 'camera')
foreach ( $entry in @(
	@('camera', $liveCamera.Count),
	@('muzzle', $liveMuzzle.Count),
	@('eject', $liveEject.Count)
) )
{
	if ( [int]$entry[1] -ne 1 )
	{
		Add-Failure "Live prefab must contain exactly one $($entry[0]) object; found $($entry[1])."
	}
}
if ( $generatedMuzzle.Count -eq 1 -and $liveMuzzle.Count -eq 1 )
{
	if ( [string]$liveMuzzle[0].Position -cne [string]$generatedMuzzle[0].Position -or
		[string]$liveMuzzle[0].Rotation -cne [string]$generatedMuzzle[0].Rotation -or
		[string]$liveMuzzle[0].Scale -cne [string]$generatedMuzzle[0].Scale )
	{
		Add-Failure 'Live muzzle transform drifted from the generated candidate.'
	}
}
if ( $generatedEject.Count -eq 1 -and $liveEject.Count -eq 1 )
{
	if ( [string]$liveEject[0].Position -cne [string]$generatedEject[0].Position -or
		[string]$liveEject[0].Rotation -cne [string]$generatedEject[0].Rotation -or
		[string]$liveEject[0].Scale -cne [string]$generatedEject[0].Scale )
	{
		Add-Failure 'Live eject transform drifted from the generated candidate.'
	}
}

if ( $viewModels.Count -eq 1 )
{
	$viewModel = $viewModels[0].Component
	if ( $null -ne $generatedRendererRecord )
	{
		Test-ComponentReference $viewModel.ModelRenderer $generatedRendererRecord.Component $generatedRendererRecord.GameObject 'ViewModel.ModelRenderer'
	}
	if ( $null -ne $armsRendererRecord )
	{
		Test-ComponentReference $viewModel.Arms $armsRendererRecord.Component $armsRendererRecord.GameObject 'ViewModel.Arms'
	}
	if ( $liveMuzzle.Count -eq 1 )
	{
		Test-GameObjectReference $viewModel.Muzzle $liveMuzzle[0] 'ViewModel.Muzzle'
	}
	if ( $liveEject.Count -eq 1 )
	{
		Test-GameObjectReference $viewModel.EjectionPort $liveEject[0] 'ViewModel.EjectionPort'
	}
	if ( $null -ne (Get-ObjectProperty $viewModel 'AdditionalRendererRoot') )
	{
		Add-Failure 'ViewModel.AdditionalRendererRoot must be null after generated integration.'
	}
	if ( ![bool]$viewModel.CanADS -or ![bool]$viewModel.UseMovementInertia -or [double]$viewModel.IronsightsFireScale -ne 0.2 )
	{
		Add-Failure 'ViewModel ADS, inertia, or fire-scale configuration drifted.'
	}
}

$liveText = Get-Content -LiteralPath $livePrefabPath -Raw
foreach ( $legacyText in @(
	'models/weapons/sbox_pistol_usp/v_usp.vmdl',
	'addons/lifepunch/lpweapons/glock/models/glock_world.vmdl',
	'addons/lifepunch/lpweapons/glock/equipment/vm_glock/invisible.vmat',
	'"Name": "glock_mesh"'
) )
{
	if ( $liveText.Contains( $legacyText, [StringComparison]::Ordinal ) )
	{
		Add-Failure "Live prefab still contains legacy first-person content: $legacyText"
	}
}

$expectedReferences = @($generated.__references | Sort-Object)
$actualReferences = @($live.__references | Sort-Object)
if ( ($expectedReferences -join "`n") -cne ($actualReferences -join "`n") )
{
	Add-Failure 'Live prefab package references do not match the generated candidate.'
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	livePrefab = $livePrefabPath
	generatedPrefab = $generatedPrefabPath
	viewModels = $viewModels.Count
	skinnedRenderers = $liveRenderers.Count
	generatedRenderers = $generatedRendererRecords.Count
	armsRenderers = $armsRendererRecords.Count
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 6
if ( $failures.Count -gt 0 )
{
	exit 1
}
