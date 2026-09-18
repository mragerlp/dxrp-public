[CmdletBinding()]
param(
	[string[]] $WeaponNames = @(
		'ak47',
		'aks74u',
		'ar15',
		'deserteagle',
		'm870',
		'm1911',
		'sr25'
	),
	[switch] $ListAssets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$assetsRoot = Join-Path $repoRoot 'game\Assets'
$failures = [System.Collections.Generic.List[string]]::new()
$globalGuids = @{}
$assetClosure = [System.Collections.Generic.HashSet[string]]::new(
	[System.StringComparer]::OrdinalIgnoreCase
)
$prefabCount = 0
$gameObjectCount = 0
$componentCount = 0
$worldRendererContractCount = 0
$worldRendererOwnershipCheckCount = 0
$hiddenDonorRendererCheckCount = 0
$viewModelArmsContractCount = 0

$worldRendererContracts = @{
	'game/Assets/addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl'
		VisibleMaterialOverride = 'addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/materials/ak47_body.vmat'
		DonorModel = 'models/weapons/sbox_assault_m4a1/w_m4a1.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/invisible.vmat'
	}
	'game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/aks74u/aks74u.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = 'models/weapons/sbox_smg_mp5/w_mp5.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/invisible.vmat'
	}
	'game/Assets/addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_world.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = 'models/weapons/sbox_assault_m4a1/w_m4a1.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/invisible.vmat'
	}
	'game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/deserteagle/Desert Eagle.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = 'models/weapons/sbox_pistol_usp/w_usp.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/invisible.vmat'
	}
	'game/Assets/addons/lifepunch/lpweapons/m1911/equipment/w_m1911/w_m1911.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/m1911/models/m1911_world.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = $null
		DonorMaterialOverride = $null
	}
	'game/Assets/addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/m870/models/m870_world.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = 'models/weapons/sbox_shotgun_spaghellim4/w_spaghellim4.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/m870/equipment/vm_m870/invisible.vmat'
	}
	'game/Assets/addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25.prefab' = [ordered]@{
		VisibleModel = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_world.vmdl'
		VisibleMaterialOverride = $null
		DonorModel = 'models/weapons/sbox_assault_m4a1/w_m4a1.vmdl'
		DonorMaterialOverride = 'addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/invisible.vmat'
	}
}

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function ConvertTo-AssetDiskPath( [string] $AssetPath )
{
	return Join-Path $assetsRoot ($AssetPath -replace '/', [IO.Path]::DirectorySeparatorChar)
}

function Add-LocalAssetReference( [string] $AssetPath )
{
	if ( [string]::IsNullOrWhiteSpace( $AssetPath ) )
	{
		return
	}

	$normalized = $AssetPath.Replace( '\', '/' )
	if ( $normalized -notmatch '^addons/lifepunch/' )
	{
		return
	}
	if ( $normalized -match '(?i)\.vsnd$' )
	{
		$sourceCandidates = @(
			@(
				'.wav',
				'.mp3',
				'.ogg'
			) | ForEach-Object {
				$sourcePath = $normalized -replace '(?i)\.vsnd$', $_
				if ( Test-Path -LiteralPath (ConvertTo-AssetDiskPath $sourcePath) -PathType Leaf )
				{
					$sourcePath
				}
			}
		)

		if ( $sourceCandidates.Count -eq 1 )
		{
			$normalized = $sourceCandidates[0]
		}
		elseif ( $sourceCandidates.Count -eq 0 )
		{
			Add-Failure "No source audio resolves virtual sound $normalized"
			return
		}
		else
		{
			Add-Failure "Multiple source audio files resolve virtual sound $normalized"
			return
		}
	}

	if ( $assetClosure.Add( $normalized ) )
	{
		return $normalized
	}
	return $null
}

function Visit-ReferenceValue(
	[object] $Value,
	[hashtable] $GameObjects,
	[hashtable] $Components,
	[string] $PrefabPath
)
{
	if ( $null -eq $Value )
	{
		return
	}

	if ( $Value -is [System.Collections.IDictionary] )
	{
		if ( $Value.Contains( 'Type' ) -and $Value.Contains( 'IdValue' ) )
		{
			$kind = [string] $Value['Type']
			$id = [string] $Value['IdValue']
			if ( $id -and $kind -eq 'GameObject' -and !$GameObjects.ContainsKey( $id ) )
			{
				Add-Failure "$PrefabPath has dangling prefab-patch GameObject reference $id"
			}
			elseif ( $id -and $kind -eq 'Component' -and !$Components.ContainsKey( $id ) )
			{
				Add-Failure "$PrefabPath has dangling prefab-patch component reference $id"
			}
		}

		if ( $Value.Contains( '_type' ) )
		{
			$kind = [string] $Value['_type']
			if ( $kind -eq 'component' )
			{
				$componentId = [string] $Value['component_id']
				$gameObjectId = [string] $Value['go']
				if ( !$Components.ContainsKey( $componentId ) )
				{
					Add-Failure "$PrefabPath has dangling component reference $componentId"
				}
				elseif ( $gameObjectId -and $Components[$componentId].Owner -ne $gameObjectId )
				{
					Add-Failure "$PrefabPath component $componentId has owner mismatch $gameObjectId"
				}

				if ( $Components.ContainsKey( $componentId ) -and $Value.Contains( 'component_type' ) )
				{
					$expectedType = [string] $Value['component_type']
					$actualType = [string] $Components[$componentId].Component['__type']
					$actualTypeName = @($actualType -split '\.')[-1]
					$typeCompatible = $expectedType -eq $actualTypeName -or
						($expectedType -eq 'ModelRenderer' -and $actualTypeName -eq 'SkinnedModelRenderer')
					if ( !$typeCompatible )
					{
						Add-Failure "$PrefabPath component reference $componentId expects $expectedType but resolves $actualType"
					}
				}

				if ( $gameObjectId -and !$GameObjects.ContainsKey( $gameObjectId ) )
				{
					Add-Failure "$PrefabPath has dangling component-owner GameObject $gameObjectId"
				}
			}
			elseif ( $kind -eq 'gameobject' -and $Value.Contains( 'go' ) )
			{
				$gameObjectId = [string] $Value['go']
				if ( $gameObjectId -and !$GameObjects.ContainsKey( $gameObjectId ) )
				{
					Add-Failure "$PrefabPath has dangling GameObject reference $gameObjectId"
				}
			}
		}

		foreach ( $child in $Value.Values )
		{
			Visit-ReferenceValue $child $GameObjects $Components $PrefabPath
		}
		return
	}

	if ( $Value -is [System.Collections.IEnumerable] -and $Value -isnot [string] )
	{
		foreach ( $child in $Value )
		{
			Visit-ReferenceValue $child $GameObjects $Components $PrefabPath
		}
		return
	}

	if ( $Value -is [string] )
	{
		[void] (Add-LocalAssetReference $Value)
	}
}

function Test-WorldRendererState(
	[object] $RendererEntry,
	[hashtable] $GameObjects,
	[string] $PrefabPath,
	[string] $Role
)
{
	$component = $RendererEntry['Component']
	$ownerId = [string] $RendererEntry['Owner']
	if ( !$GameObjects.ContainsKey( $ownerId ) )
	{
		Add-Failure "$PrefabPath $Role renderer has missing owner GameObject $ownerId"
		return
	}

	$owner = $GameObjects[$ownerId]
	if ( !$owner.ContainsKey( 'Enabled' ) -or $owner['Enabled'] -ne $true )
	{
		Add-Failure "$PrefabPath $Role renderer owner GameObject is disabled."
	}
	if ( !$component.ContainsKey( '__enabled' ) -or $component['__enabled'] -ne $true )
	{
		Add-Failure "$PrefabPath $Role renderer component is disabled."
	}
	if ( !$component.ContainsKey( 'RenderType' ) -or $component['RenderType'] -cne 'On' )
	{
		Add-Failure "$PrefabPath $Role renderer RenderType is not On."
	}

	if ( !$component.ContainsKey( 'RenderOptions' ) -or $null -eq $component['RenderOptions'] )
	{
		Add-Failure "$PrefabPath $Role renderer has no RenderOptions."
		return
	}

	$options = $component['RenderOptions']
	$expectedLayers = [ordered]@{
		GameLayer = $true
		OverlayLayer = $false
		BloomLayer = $false
		AfterUILayer = $false
	}
	foreach ( $layer in $expectedLayers.Keys )
	{
		if ( !$options.ContainsKey( $layer ) -or $options[$layer] -ne $expectedLayers[$layer] )
		{
			Add-Failure "$PrefabPath $Role renderer has incorrect $layer state."
		}
	}
}

function Test-WorldRendererContract(
	[string] $PrefabPath,
	[hashtable] $GameObjects,
	[hashtable] $Components
)
{
	if ( !$worldRendererContracts.ContainsKey( $PrefabPath ) )
	{
		return
	}

	$script:worldRendererContractCount++
	$contract = $worldRendererContracts[$PrefabPath]
	$renderers = @($Components.Values | Where-Object {
		$_['Component']['__type'] -eq 'Sandbox.SkinnedModelRenderer'
	})
	$expectedRendererCount = if ( $null -eq $contract['DonorModel'] ) { 1 } else { 2 }
	if ( $renderers.Count -ne $expectedRendererCount )
	{
		Add-Failure "$PrefabPath has $($renderers.Count) world renderers instead of $expectedRendererCount."
	}

	$equipmentEntries = @($Components.Values | Where-Object {
		$_['Component']['__type'] -eq 'Dxura.RP.Game.Equipment'
	})
	if ( $equipmentEntries.Count -ne 1 )
	{
		Add-Failure "$PrefabPath has $($equipmentEntries.Count) Equipment components instead of exactly one."
		return
	}

	$visibleRenderers = @($renderers | Where-Object {
		$_['Component']['Model'] -ceq $contract['VisibleModel']
	})
	if ( $visibleRenderers.Count -ne 1 )
	{
		Add-Failure "$PrefabPath has $($visibleRenderers.Count) visible renderers for $($contract['VisibleModel']) instead of exactly one."
		return
	}

	$script:worldRendererOwnershipCheckCount++
	$visibleEntry = $visibleRenderers[0]
	$visible = $visibleEntry['Component']
	Test-WorldRendererState $visibleEntry $GameObjects $PrefabPath 'visible'
	$visibleMaterial = if ( $visible.ContainsKey( 'MaterialOverride' ) ) {
		$visible['MaterialOverride']
	} else {
		$null
	}
	if ( $visibleMaterial -cne $contract['VisibleMaterialOverride'] )
	{
		Add-Failure "$PrefabPath visible renderer material override changed from '$($contract['VisibleMaterialOverride'])' to '$visibleMaterial'."
	}

	$equipment = $equipmentEntries[0]['Component']
	if ( !$equipment.ContainsKey( 'ModelRenderer' ) -or
		$equipment['ModelRenderer'] -isnot [System.Collections.IDictionary] )
	{
		Add-Failure "$PrefabPath Equipment.ModelRenderer is not a component reference."
	}
	else
	{
		$rendererReference = $equipment['ModelRenderer']
		if ( $rendererReference['_type'] -cne 'component' -or
			$rendererReference['component_type'] -cne 'SkinnedModelRenderer' -or
			$rendererReference['component_id'] -cne $visible['__guid'] -or
			$rendererReference['go'] -cne $visibleEntry['Owner'] )
		{
			Add-Failure "$PrefabPath Equipment.ModelRenderer does not own the visible custom renderer."
		}
	}

	if ( $null -eq $contract['DonorModel'] )
	{
		return
	}

	$script:hiddenDonorRendererCheckCount++
	$donorRenderers = @($renderers | Where-Object {
		$_['Component']['Model'] -ceq $contract['DonorModel']
	})
	if ( $donorRenderers.Count -ne 1 )
	{
		Add-Failure "$PrefabPath has $($donorRenderers.Count) donor renderers for $($contract['DonorModel']) instead of exactly one."
		return
	}

	$donorEntry = $donorRenderers[0]
	$donor = $donorEntry['Component']
	Test-WorldRendererState $donorEntry $GameObjects $PrefabPath 'donor'
	if ( $donor['MaterialOverride'] -cne $contract['DonorMaterialOverride'] )
	{
		Add-Failure "$PrefabPath donor renderer no longer uses $($contract['DonorMaterialOverride'])."
	}
	if ( $donor['__guid'] -ceq $visible['__guid'] )
	{
		Add-Failure "$PrefabPath donor renderer is also the visible renderer target."
	}

	$invisiblePath = ConvertTo-AssetDiskPath $contract['DonorMaterialOverride']
	if ( !(Test-Path -LiteralPath $invisiblePath -PathType Leaf) )
	{
		Add-Failure "$PrefabPath donor invisible material is missing: $($contract['DonorMaterialOverride'])"
		return
	}

	$invisibleText = Get-Content -LiteralPath $invisiblePath -Raw
	if ( $invisibleText -notmatch '(?m)^\s*F_TRANSLUCENT\s+1\s*$' -or
		$invisibleText -notmatch '(?m)^\s*g_flOpacityScale\s+"0\.000"\s*$' -or
		$invisibleText -notmatch '(?m)^\s*g_vColorTint\s+"\[1\.000000 1\.000000 1\.000000 0\.000000\]"\s*$' )
	{
		Add-Failure "$PrefabPath donor invisible material no longer pins translucent zero-opacity output."
	}
}

function Test-ViewModelArmsContract(
	[string] $PrefabPath,
	[hashtable] $Components
)
{
	$viewModels = @($Components.Values | Where-Object {
		$_['Component']['__type'] -eq 'Dxura.RP.Game.ViewModel'
	})
	if ( $viewModels.Count -eq 0 )
	{
		return
	}

	$script:viewModelArmsContractCount++
	if ( $viewModels.Count -ne 1 )
	{
		Add-Failure "$PrefabPath has $($viewModels.Count) ViewModel components instead of exactly one."
		return
	}

	$armsRenderers = @($Components.Values | Where-Object {
		$_['Component']['__type'] -eq 'Sandbox.SkinnedModelRenderer' -and
		$_['Component']['Model'] -ceq 'models/first_person/v_first_person_arms_human.vmdl'
	})
	if ( $armsRenderers.Count -ne 1 )
	{
		Add-Failure "$PrefabPath has $($armsRenderers.Count) human-arms renderers instead of exactly one."
		return
	}

	$viewModel = $viewModels[0]['Component']
	$armsEntry = $armsRenderers[0]
	if ( !$viewModel.ContainsKey( 'Arms' ) -or
		$viewModel['Arms'] -isnot [System.Collections.IDictionary] )
	{
		Add-Failure "$PrefabPath ViewModel.Arms is not a component reference."
		return
	}

	$armsReference = $viewModel['Arms']
	if ( $armsReference['_type'] -cne 'component' -or
		$armsReference['component_type'] -cne 'SkinnedModelRenderer' -or
		$armsReference['component_id'] -cne $armsEntry['Component']['__guid'] -or
		$armsReference['go'] -cne $armsEntry['Owner'] )
	{
		Add-Failure "$PrefabPath ViewModel.Arms does not reference its human-arms renderer."
	}
}

function Test-Prefab( [System.IO.FileInfo] $Prefab )
{
	$relativePath = $Prefab.FullName.Substring( $repoRoot.Length + 1 ).Replace( '\', '/' )
	try
	{
		$document = Get-Content -LiteralPath $Prefab.FullName -Raw | ConvertFrom-Json -AsHashtable
	}
	catch
	{
		Add-Failure "$relativePath is not valid JSON: $($_.Exception.Message)"
		return
	}

	$gameObjects = @{}
	$components = @{}
	$prefabGuids = @{}

	function Register-ReferenceGameObject( [hashtable] $GameObject, [string] $Location )
	{
		if ( $null -eq $GameObject )
		{
			return
		}

		$gameObjectId = [string] $GameObject['__guid']
		if ( [string]::IsNullOrWhiteSpace( $gameObjectId ) )
		{
			Add-Failure "$relativePath inherits a GameObject without a GUID at $Location"
		}
		elseif ( !$gameObjects.ContainsKey( $gameObjectId ) )
		{
			$gameObjects[$gameObjectId] = $GameObject
		}
		elseif ( $prefabGuids.ContainsKey( $gameObjectId ) )
		{
			Add-Failure "$relativePath local GUID $gameObjectId collides with inherited $Location"
		}
		else
		{
			Add-Failure "$relativePath inherited hierarchy repeats GameObject GUID $gameObjectId at $Location"
		}

		if ( $GameObject.ContainsKey( 'Components' ) -and $null -ne $GameObject['Components'] )
		{
			foreach ( $component in @($GameObject['Components']) )
			{
				if ( $null -eq $component )
				{
					continue
				}
				$componentId = [string] $component['__guid']
				if ( [string]::IsNullOrWhiteSpace( $componentId ) )
				{
					Add-Failure "$relativePath inherits a component without a GUID at $Location"
				}
				elseif ( !$components.ContainsKey( $componentId ) )
				{
					$components[$componentId] = @{
						Owner = $gameObjectId
						Component = $component
					}
				}
				elseif ( $prefabGuids.ContainsKey( $componentId ) )
				{
					Add-Failure "$relativePath local GUID $componentId collides with inherited component at $Location"
				}
				else
				{
					Add-Failure "$relativePath inherited hierarchy repeats component GUID $componentId at $Location"
				}
			}
		}

		if ( $GameObject.ContainsKey( 'Children' ) -and $null -ne $GameObject['Children'] )
		{
			$childIndex = 0
			foreach ( $child in @($GameObject['Children']) )
			{
				if ( $null -ne $child )
				{
					Register-ReferenceGameObject $child "$Location/$($child['Name'])[$childIndex]"
				}
				$childIndex++
			}
		}
	}

	function Import-ReferencePrefabIds( [string] $AssetPath, [string] $Location )
	{
		$baseDiskPath = ConvertTo-AssetDiskPath $AssetPath
		if ( !(Test-Path -LiteralPath $baseDiskPath -PathType Leaf) )
		{
			Add-Failure "$relativePath inherits missing prefab $AssetPath"
			return
		}

		try
		{
			$baseDocument = Get-Content -LiteralPath $baseDiskPath -Raw | ConvertFrom-Json -AsHashtable
		}
		catch
		{
			Add-Failure "$relativePath inherits invalid prefab $AssetPath`: $($_.Exception.Message)"
			return
		}

		$baseRoot = $baseDocument['RootObject']
		if ( $baseRoot.ContainsKey( '__Prefab' ) -and $baseRoot['__Prefab'] )
		{
			Import-ReferencePrefabIds ([string] $baseRoot['__Prefab']) "$Location/base"
		}
		Register-ReferenceGameObject $baseRoot "$Location/RootObject"

		if ( $baseRoot.ContainsKey( '__PrefabInstancePatch' ) )
		{
			$basePatch = $baseRoot['__PrefabInstancePatch']
			if ( $basePatch.ContainsKey( 'AddedObjects' ) -and $null -ne $basePatch['AddedObjects'] )
			{
				$addedIndex = 0
				foreach ( $addedObject in @($basePatch['AddedObjects']) )
				{
					if ( $null -ne $addedObject -and $null -ne $addedObject['Data'] )
					{
						Register-ReferenceGameObject $addedObject['Data'] "$Location/AddedObjects[$addedIndex]"
					}
					$addedIndex++
				}
			}
		}
	}

	function Visit-GameObject( [hashtable] $GameObject, [string] $Location )
	{
		if ( $null -eq $GameObject )
		{
			return
		}

		$script:gameObjectCount++
		$gameObjectId = [string] $GameObject['__guid']
		if ( [string]::IsNullOrWhiteSpace( $gameObjectId ) )
		{
			Add-Failure "$relativePath has a GameObject without a GUID at $Location"
		}
		elseif ( $prefabGuids.ContainsKey( $gameObjectId ) )
		{
			Add-Failure "$relativePath repeats GUID $gameObjectId"
		}
		else
		{
			$prefabGuids[$gameObjectId] = $Location
			$gameObjects[$gameObjectId] = $GameObject
		}

		$componentIndex = 0
		$gameObjectComponents = if ( $GameObject.ContainsKey( 'Components' ) -and $null -ne $GameObject['Components'] )
		{
			@($GameObject['Components'])
		}
		else
		{
			@()
		}
		foreach ( $component in $gameObjectComponents )
		{
			if ( $null -eq $component )
			{
				continue
			}
			$script:componentCount++
			if ( $component.ContainsKey( 'BodyGroups' ) -and $null -ne $component['BodyGroups'] )
			{
				try
				{
					$bodyGroups = [System.Numerics.BigInteger]::Parse(
						[string] $component['BodyGroups'],
						[Globalization.NumberStyles]::Integer,
						[Globalization.CultureInfo]::InvariantCulture
					)
					$maxBodyGroups = [System.Numerics.BigInteger]::Parse(
						[UInt64]::MaxValue.ToString( [Globalization.CultureInfo]::InvariantCulture )
					)
					if ( $bodyGroups -lt [System.Numerics.BigInteger]::Zero -or $bodyGroups -gt $maxBodyGroups )
					{
						Add-Failure "$relativePath has BodyGroups outside UInt64 range at $Location/component[$componentIndex]: $bodyGroups"
					}
				}
				catch
				{
					Add-Failure "$relativePath has invalid BodyGroups at $Location/component[$componentIndex]: $($component['BodyGroups'])"
				}
			}
			$componentId = [string] $component['__guid']
			if ( [string]::IsNullOrWhiteSpace( $componentId ) )
			{
				Add-Failure "$relativePath has a component without a GUID at $Location"
			}
			elseif ( $prefabGuids.ContainsKey( $componentId ) )
			{
				Add-Failure "$relativePath repeats GUID $componentId"
			}
			else
			{
				$prefabGuids[$componentId] = "$Location/component[$componentIndex]"
				$components[$componentId] = @{
					Owner = $gameObjectId
					Component = $component
				}
			}
			$componentIndex++
		}

		$childIndex = 0
		$gameObjectChildren = if ( $GameObject.ContainsKey( 'Children' ) -and $null -ne $GameObject['Children'] )
		{
			@($GameObject['Children'])
		}
		else
		{
			@()
		}
		foreach ( $child in $gameObjectChildren )
		{
			if ( $null -ne $child )
			{
				Visit-GameObject $child "$Location/$($child['Name'])[$childIndex]"
			}
			$childIndex++
		}
	}

	$rootObject = $document['RootObject']
	Visit-GameObject $rootObject 'RootObject'
	if ( $rootObject.ContainsKey( '__PrefabInstancePatch' ) )
	{
		$patch = $rootObject['__PrefabInstancePatch']
		if ( $patch.ContainsKey( 'AddedObjects' ) -and $null -ne $patch['AddedObjects'] )
		{
			$addedIndex = 0
			foreach ( $addedObject in @($patch['AddedObjects']) )
			{
				if ( $null -ne $addedObject -and $null -ne $addedObject['Data'] )
				{
					Visit-GameObject $addedObject['Data'] "RootObject/AddedObjects[$addedIndex]"
				}
				$addedIndex++
			}
		}
	}
	if ( $rootObject.ContainsKey( '__Prefab' ) -and $rootObject['__Prefab'] )
	{
		Import-ReferencePrefabIds ([string] $rootObject['__Prefab']) 'InheritedPrefab'
	}
	Visit-ReferenceValue $document $gameObjects $components $relativePath
	Test-WorldRendererContract $relativePath $gameObjects $components
	Test-ViewModelArmsContract $relativePath $components

	foreach ( $guid in $prefabGuids.Keys )
	{
		if ( $globalGuids.ContainsKey( $guid ) )
		{
			Add-Failure "$relativePath collides on GUID $guid with $($globalGuids[$guid])"
		}
		else
		{
			$globalGuids[$guid] = $relativePath
		}
	}
}

if ( $null -eq $WeaponNames -or $WeaponNames.Count -eq 0 )
{
	Add-Failure 'At least one weapon name is required; an empty validation target is not a pass.'
}

foreach ( $weaponName in $WeaponNames )
{
	$weaponRoot = Join-Path $assetsRoot "addons\lifepunch\lpweapons\$weaponName"
	if ( !(Test-Path -LiteralPath $weaponRoot -PathType Container) )
	{
		Add-Failure "Weapon root does not exist: addons/lifepunch/lpweapons/$weaponName"
		continue
	}

	$equipmentRoot = Join-Path $weaponRoot 'equipment'
	$prefabs = if ( Test-Path -LiteralPath $equipmentRoot -PathType Container )
	{
		@(
			Get-ChildItem -LiteralPath $equipmentRoot -Recurse -Filter '*.prefab' -File |
				Where-Object { $_.BaseName -eq $_.Directory.Name }
		)
	}
	else
	{
		@()
	}

	if ( $prefabs.Count -lt 2 )
	{
		Add-Failure "addons/lifepunch/lpweapons/$weaponName has $($prefabs.Count) equipment prefabs; expected at least 2"
	}

	foreach ( $prefab in $prefabs )
	{
		$prefabCount++
		$assetPath = $prefab.FullName.Substring( $assetsRoot.Length + 1 ).Replace( '\', '/' )
		[void] (Add-LocalAssetReference $assetPath)
		Test-Prefab $prefab
	}
}

$assetPattern = [regex]::new(
	'(?<path>(?:addons|models|materials|textures|sounds|prefabs)/[^"''\s<>]+\.(?:vmdl|vmat|sound|vsnd|wav|mp3|ogg|prefab|fbx|png|jpg|jpeg|tga))',
	[System.Text.RegularExpressions.RegexOptions]::IgnoreCase
)
$queue = [System.Collections.Generic.Queue[string]]::new()
foreach ( $asset in $assetClosure )
{
	$queue.Enqueue( $asset )
}
$visited = [System.Collections.Generic.HashSet[string]]::new(
	[System.StringComparer]::OrdinalIgnoreCase
)

while ( $queue.Count -gt 0 )
{
	$assetPath = $queue.Dequeue()
	if ( !$visited.Add( $assetPath ) )
	{
		continue
	}

	$diskPath = ConvertTo-AssetDiskPath $assetPath
	if ( !(Test-Path -LiteralPath $diskPath -PathType Leaf) )
	{
		Add-Failure "Missing local asset in active closure: $assetPath"
		continue
	}

	if ( $assetPath -match '(?i)(?:^|/)(?:autorig|autorig_dl)(?:/|$)' -or
		$assetPath -match '(?i)models/autorig' )
	{
		Add-Failure "Active closure reaches provenance-pending Auto Rigger asset: $assetPath"
	}

	if ( [IO.Path]::GetExtension( $diskPath ) -notin @('.prefab', '.vmdl', '.vmat', '.sound') )
	{
		continue
	}

	$text = Get-Content -LiteralPath $diskPath -Raw
	foreach ( $match in $assetPattern.Matches( $text ) )
	{
		$reference = $match.Groups['path'].Value.Replace( '\', '/' )
		$resolvedReference = Add-LocalAssetReference $reference
		if ( $resolvedReference )
		{
			$queue.Enqueue( $resolvedReference )
		}
	}
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapons = @($WeaponNames)
	prefabs = $prefabCount
	gameObjects = $gameObjectCount
	components = $componentCount
	uniqueGuids = $globalGuids.Count
	activeLocalAssets = $visited.Count
	worldRendererContracts = $worldRendererContractCount
	worldRendererOwnershipChecks = $worldRendererOwnershipCheckCount
	hiddenDonorRendererChecks = $hiddenDonorRendererCheckCount
	viewModelArmsContracts = $viewModelArmsContractCount
	failures = @($failures)
}
if ( $ListAssets )
{
	$summary['activeAssetPaths'] = @($visited | Sort-Object)
}

$summary | ConvertTo-Json -Depth 5 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

Write-Output ((
	'RESULT weapon_assets=PASS weapons={0} prefabs={1} gameobjects={2} ' +
	'components={3} unique_guids={4} active_local_assets={5} ' +
	'duplicate_or_colliding_guids=0 dangling_refs=0 missing_local_assets=0 ' +
	'active_autorigger_refs=0 world_renderer_contracts={6} ' +
	'visible_renderer_ownership={7} hidden_donor_renderers={8} ' +
	'viewmodel_arms_contracts={9}'
) -f $WeaponNames.Count, $prefabCount, $gameObjectCount, $componentCount,
	$globalGuids.Count, $visited.Count, $worldRendererContractCount,
	$worldRendererOwnershipCheckCount, $hiddenDonorRendererCheckCount,
	$viewModelArmsContractCount)
