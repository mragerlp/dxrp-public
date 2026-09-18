$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True( [bool] $Condition, [string] $Message )
{
	if ( -not $Condition )
	{
		$script:failures.Add( $Message )
	}
}

function Get-OrdinalManifest( [string] $Directory, [string] $Filter )
{
	$filesByName = @{}
	$names = [string[]]@( Get-ChildItem -LiteralPath $Directory -File -Filter $Filter |
		ForEach-Object {
			$filesByName[$_.Name] = $_
			$_.Name
		} )
	[Array]::Sort( $names, [StringComparer]::Ordinal )
	$files = @( $names | ForEach-Object { $filesByName[$_] } )
	$rows = foreach ( $file in $files )
	{
		"$($file.Name)`t$($file.Length)`t$((Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash)`n"
	}

	$text = [string]::Concat( $rows )
	$bytes = [System.Text.Encoding]::UTF8.GetBytes( $text )
	$sha256 = [Convert]::ToHexString( [System.Security.Cryptography.SHA256]::HashData( $bytes ) )

	return [pscustomobject]@{
		Count = $files.Count
		Bytes = ($files | Measure-Object Length -Sum).Sum
		SHA256 = $sha256
	}
}

function Read-Text( [string] $Path )
{
	return Get-Content -Raw -LiteralPath $Path
}

function Get-GuidDefinitions( [string] $Raw )
{
	return @( [regex]::Matches( $Raw, '"__guid"\s*:\s*"([0-9a-fA-F-]{36})"' ) |
		ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() } )
}

function Get-GuidReferences( [string] $Raw )
{
	return @( [regex]::Matches( $Raw, '"(?:go|component_id|IdValue)"\s*:\s*"([0-9a-fA-F-]{36})"' ) |
		ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() } )
}

$repoRoot = [IO.Path]::GetFullPath( (Join-Path $PSScriptRoot '..\..') )
$root = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\sr25'
$viewModelPath = "$root/equipment/vm_sr25/vm_sr25.prefab"
$worldModelPath = "$root/equipment/w_sr25/w_sr25.prefab"
$devCommandPath = Join-Path $repoRoot 'game\Code\Addons\lifepunch\_dev\Sr25DevGive.cs'
$viewModelRaw = Read-Text $viewModelPath
$worldModelRaw = Read-Text $worldModelPath
$devCommandRaw = Read-Text $devCommandPath

$pinnedFiles = @{
	"$root/sr25_blender4.blend" = @('75815175', '9936B46A24EAB5018EB9AF9F77D3279AA6E6C68ECA4AD04ECE8285F615F5BD42')
	"$root/sr25-og.fbx" = @('69168000', '6D923A14CA2B22C754D1E005D7B1159BA865A78A3BB4251EB5AD4D32FF632C4F')
	"$root/source/generated/sr25_native_clean.fbx" = @('1858012', '0ECC693CD4F35879ABC0E6AC6643BF2C70783032DA3AAF3AEFA73A7EF7BB0B1C')
	$viewModelPath = @('85430', 'FFC3EB52516107EE9292821F1ECDE575E46EE85B04F1FC3B695EC21B40F6C8D4')
	$worldModelPath = @('18919', '73E003C36495CA0B66AD3B2F0D327369FF2D6D586B0261AD3EEE14227DA09BE3')
}

foreach ( $entry in $pinnedFiles.GetEnumerator() )
{
	Assert-True (Test-Path -LiteralPath $entry.Key -PathType Leaf) "Missing pinned file: $($entry.Key)"
	if ( Test-Path -LiteralPath $entry.Key -PathType Leaf )
	{
		$item = Get-Item -LiteralPath $entry.Key
		$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $entry.Key).Hash
		Assert-True ($item.Length -eq [int64]$entry.Value[0]) "Byte mismatch: $($entry.Key)"
		Assert-True ($hash -eq $entry.Value[1]) "Hash mismatch: $($entry.Key)"
	}
}

$manifests = [ordered]@{
	SourceTextures = Get-OrdinalManifest "$root/textures" '*.png'
	DerivedTextures = Get-OrdinalManifest "$root/textures/native_candidate" '*.png'
	Materials = Get-OrdinalManifest "$root/materials/native_candidate" '*.vmat'
	ModelDocs = Get-OrdinalManifest "$root/models/native_candidate" '*.vmdl'
}

Assert-True ($manifests.SourceTextures.Count -eq 42 -and $manifests.SourceTextures.Bytes -eq 68653083 -and $manifests.SourceTextures.SHA256 -eq '0B4E72C886AC0BDC61CBF00E94787AA4367F08BC5E4A1E54E746AEFA4AA7E3B1') 'Source texture manifest mismatch.'
Assert-True ($manifests.DerivedTextures.Count -eq 42 -and $manifests.DerivedTextures.Bytes -eq 36486262 -and $manifests.DerivedTextures.SHA256 -eq 'A5EEF7F687EE7FCBAE58D5950E0F0627EA6FF00919E119A9A18FC5CFF2425457') 'Derived texture manifest mismatch.'
Assert-True ($manifests.Materials.Count -eq 14 -and $manifests.Materials.Bytes -eq 11175 -and $manifests.Materials.SHA256 -eq 'F3BE0F55F8D126BEB1C5B5B7D5251D3A8F15DCF56553A0EA2341C0611114F818') 'Material manifest mismatch.'
Assert-True ($manifests.ModelDocs.Count -eq 12 -and $manifests.ModelDocs.Bytes -eq 36754 -and $manifests.ModelDocs.SHA256 -eq '693F06C9C7BC4F93BCD48401D99AD4AC5125EDE75FE3FCE8251C12E62517015B') 'ModelDoc manifest mismatch.'

$materialFamilies = @(
	'ammo_762x51_m62', 'barrel_ar10', 'body', 'charging_handle',
	'handguard_ar10', 'mag_ar10', 'muzzle_ar10', 'pistolgrip_ar15',
	'reciever_ar10', 'scope_30mm_24x', 'scope_mount', 'silencer_ar10',
	'stock_ar15', 'stock_tube_ar15'
)

$sourceTextureNames = [ordered]@{
	'ammo_762x51_m62' = @('ammo_762x51_m62_diff.png', 'ammo_762x51_nrm.png')
	'barrel_ar10' = @('barrel_diff.png', 'barrel_nrm.png')
	'body' = @('body_diff.png', 'body_nrm.png')
	'charging_handle' = @('charge_diff.png', 'charge_nrm.png')
	'handguard_ar10' = @('handguard_diff.png', 'handguard_nrm.png')
	'mag_ar10' = @('mag_ar10_diff.png', 'mag_ar10_nrm.png')
	'muzzle_ar10' = @('muzzle_diff.png', 'muzzle_nrm.png')
	'pistolgrip_ar15' = @('pistolgrip_diff.png', 'pistolgrip_nrm.png')
	'reciever_ar10' = @('reciever_ar10_diff.png', 'reciever_ar10_nrm.png')
	'scope_30mm_24x' = @('scope_30mm_diff.png', 'scope_30mm_nrm.png')
	'scope_mount' = @('scope_mount_diff.png', 'scope_mount_nrm.png')
	'silencer_ar10' = @('silencer_diff.png', 'silencer_nrm.png')
	'stock_ar15' = @('stock_diff.png', 'stock_nrm.png')
	'stock_tube_ar15' = @('stock_tube_diff.png', 'stock_tube_nrm.png')
}

foreach ( $family in $materialFamilies )
{
	foreach ( $suffix in @('AO', 'Roughness', 'Metalness') )
	{
		Assert-True (Test-Path -LiteralPath "$root/textures/native_candidate/${family}_${suffix}.png") "Missing derived texture ${family}_${suffix}.png"
	}

	$baseTextureName = $sourceTextureNames[$family][0]
	$normalTextureName = $sourceTextureNames[$family][1]
	Assert-True (Test-Path -LiteralPath "$root/textures/$baseTextureName") "Missing source base texture $baseTextureName"
	Assert-True (Test-Path -LiteralPath "$root/textures/$normalTextureName") "Missing source normal texture $normalTextureName"

	$materialPath = "$root/materials/native_candidate/${family}.vmat"
	Assert-True (Test-Path -LiteralPath $materialPath) "Missing VMAT $family"
	if ( Test-Path -LiteralPath $materialPath )
	{
		$materialRaw = Read-Text $materialPath
		Assert-True ($materialRaw.Contains( "textures/$baseTextureName" )) "VMAT missing base texture: $family"
		Assert-True ($materialRaw.Contains( "textures/$normalTextureName" )) "VMAT missing normal texture: $family"
		foreach ( $suffix in @('AO', 'Roughness', 'Metalness') )
		{
			Assert-True ($materialRaw.Contains( "textures/native_candidate/${family}_${suffix}.png" )) "VMAT missing derived ${suffix}: $family"
		}
	}
}

$modelDocs = @( Get-ChildItem -LiteralPath "$root/models/native_candidate" -File -Filter '*.vmdl' | Sort-Object Name )
Assert-True ($modelDocs.Count -eq 12) 'Expected 12 native candidate ModelDocs.'
foreach ( $modelDoc in $modelDocs )
{
	$raw = Read-Text $modelDoc.FullName
	Assert-True ($raw.Contains( 'filename = "addons/lifepunch/lpweapons/sr25/source/generated/sr25_native_clean.fbx"' )) "Wrong FBX reference: $($modelDoc.Name)"
	Assert-True ($raw.Contains( 'import_rotation = [ 0.0, -90.0, 0.0 ]' )) "Wrong rotation: $($modelDoc.Name)"
	Assert-True ($raw.Contains( 'import_scale = 39.37008' )) "Wrong scale: $($modelDoc.Name)"
	$remapCount = [regex]::Matches( $raw, 'to = "addons/lifepunch/lpweapons/sr25/materials/native_candidate/[^"]+\.vmat"' ).Count
	Assert-True ($remapCount -eq 14) "Material remap count $remapCount in $($modelDoc.Name)"

	$stem = [IO.Path]::GetFileNameWithoutExtension( $modelDoc.Name )
	if ( $stem -eq 'sr25_world' )
	{
		Assert-True ($raw.Contains( 'exclude_by_default = false' )) 'World ModelDoc does not include the full assembly.'
	}
	else
	{
		Assert-True ($raw.Contains( "`"$stem`"" )) "Part ModelDoc filter mismatch: $stem"
		Assert-True ($raw.Contains( 'exclude_by_default = true' )) "Part ModelDoc is not exclusive: $stem"
	}
}

$viewModelDefinitions = Get-GuidDefinitions $viewModelRaw
$worldModelDefinitions = Get-GuidDefinitions $worldModelRaw
Assert-True ($viewModelDefinitions.Count -eq 102 -and @( $viewModelDefinitions | Sort-Object -Unique ).Count -eq 102) 'View-model GUID definitions are not 102 unique values.'
Assert-True ($worldModelDefinitions.Count -eq 17 -and @( $worldModelDefinitions | Sort-Object -Unique ).Count -eq 17) 'World-model GUID definitions are not 17 unique values.'
$allDefinitions = @( $viewModelDefinitions + $worldModelDefinitions )
Assert-True (@( $allDefinitions | Sort-Object -Unique ).Count -eq 119) 'Cross-prefab GUID collision.'
Assert-True (@( $allDefinitions | Where-Object { $_ -notmatch '^57250000-5a25-4000-8000-' } ).Count -eq 0) 'Unexpected SR-25 GUID namespace.'

$danglingInternalReferences = [System.Collections.Generic.List[string]]::new()
foreach ( $prefab in @(@($viewModelRaw, $viewModelDefinitions, 'View-model'), @($worldModelRaw, $worldModelDefinitions, 'World-model')) )
{
	$references = Get-GuidReferences $prefab[0]
	$missing = @( $references | Where-Object { $_ -notin $prefab[1] } | Sort-Object -Unique )
	foreach ( $missingReference in $missing )
	{
		$danglingInternalReferences.Add( "$($prefab[2]):$missingReference" )
	}
	Assert-True ($missing.Count -eq 0) "$($prefab[2]) dangling internal GUID refs: $($missing -join ',')"
}

$donorPaths = @(
	(Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'),
	(Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\w_ar15\w_ar15.prefab'),
	(Join-Path $repoRoot 'game\Assets\gameplay\equipment\weapons\m4a1\vm_m4a1.prefab'),
	(Join-Path $repoRoot 'game\Assets\gameplay\equipment\weapons\m4a1\w_m4a1.prefab')
)
$donorGuids = @()
foreach ( $donorPath in $donorPaths )
{
	$donorGuids += Get-GuidDefinitions (Read-Text $donorPath)
}
$donorOverlap = @( $allDefinitions | Where-Object { $_ -in $donorGuids } | Sort-Object -Unique )
Assert-True ($donorOverlap.Count -eq 0) "SR-25 GUIDs overlap donors: $($donorOverlap -join ',')"

$expectedParts = @(
	'sr25_body', 'sr25_stock', 'sr25_trigger', 'sr25_magazine',
	'sr25_mode_selector', 'sr25_bolt_flap', 'sr25_bolt',
	'sr25_charging_handle', 'sr25_scope', 'sr25_scope_mount',
	'sr25_suppressor'
)
$viewModelParts = @( [regex]::Matches( $viewModelRaw, '"Model"\s*:\s*"addons/lifepunch/lpweapons/sr25/models/native_candidate/(sr25_[^"]+)\.vmdl"' ) |
	ForEach-Object { $_.Groups[1].Value } )
Assert-True ($viewModelParts.Count -eq 11 -and @( $viewModelParts | Sort-Object -Unique ).Count -eq 11) 'View-model does not reference 11 unique semantic models.'
Assert-True (@( Compare-Object ($expectedParts | Sort-Object) ($viewModelParts | Sort-Object) ).Count -eq 0) 'View-model semantic model set mismatch.'
Assert-True ($viewModelRaw.Contains( '"CanADS": false' )) 'View-model CanADS is not false.'
Assert-True ($viewModelRaw.Contains( 'models/weapons/sbox_assault_m4a1/v_m4a1.vmdl' )) 'View-model M4 driver missing.'
Assert-True ($viewModelRaw.Contains( 'models/first_person/v_first_person_arms_human.vmdl' )) 'View-model arms driver missing.'
Assert-True ([regex]::IsMatch( $viewModelRaw, '"AdditionalRendererRoot"\s*:\s*\{[^}]*"go"\s*:\s*"57250000-5a25-4000-8000-000000000034"', 'Singleline' )) 'View-model custom-renderer visibility root is missing.'

Assert-True ($worldModelRaw.Contains( '"HoldType": "Rifle"' )) 'World-model HoldType is not Rifle.'
Assert-True ($worldModelRaw.Contains( 'models/weapons/sbox_assault_m4a1/w_m4a1.vmdl' )) 'World-model M4 driver missing.'
Assert-True ([regex]::Matches( $worldModelRaw, 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_world\.vmdl' ).Count -eq 1) 'World assembled-model reference mismatch.'
Assert-True ([regex]::IsMatch( $worldModelRaw, '"ModelRenderer"\s*:\s*\{[^}]*"component_id"\s*:\s*"57250000-5a25-4000-8000-000000000076"[^}]*"component_type"\s*:\s*"SkinnedModelRenderer"', 'Singleline' )) 'Equipment world-renderer reference is not SkinnedModelRenderer-compatible.'
Assert-True ([regex]::IsMatch( $worldModelRaw, '"__type"\s*:\s*"Sandbox\.SkinnedModelRenderer"\s*,\s*"__guid"\s*:\s*"57250000-5a25-4000-8000-000000000076"', 'Singleline' )) 'Visible world renderer is not a SkinnedModelRenderer.'
Assert-True ([regex]::Matches( $viewModelRaw, '"BodyGroups"\s*:\s*18446744073709551615' ).Count -eq 11) 'View-model custom renderers do not all use canonical UInt64.MaxValue BodyGroups.'
Assert-True ([regex]::Matches( $worldModelRaw, '"BodyGroups"\s*:\s*18446744073709551615' ).Count -eq 1) 'World-model custom renderer does not use canonical UInt64.MaxValue BodyGroups.'
$fovOffsetCount = [regex]::Matches( $worldModelRaw, '"__type": "Dxura\.RP\.Game\.FovOffset"' ).Count
$scopeWeaponComponentCount = [regex]::Matches( $worldModelRaw, '"__type": "Dxura\.RP\.Game\.ScopeWeaponComponent"' ).Count
$boltPusherCount = [regex]::Matches( $worldModelRaw, 'BoltPusher' ).Count
$manualOffsetCount = [regex]::Matches( $worldModelRaw, 'ManualOffset' ).Count
Assert-True ($fovOffsetCount -eq 1) 'FovOffset count is not one.'
Assert-True ($scopeWeaponComponentCount -eq 1) 'ScopeWeaponComponent count is not one.'
Assert-True ($boltPusherCount -eq 0) 'BoltPusher must not be present.'
Assert-True ($manualOffsetCount -eq 0) 'ManualOffset must not be present.'
Assert-True ($worldModelRaw.Contains( '"Length": 0.2' ) -and $worldModelRaw.Contains( '"Size": 5' )) 'M700 FOV seed missing.'
Assert-True ($worldModelRaw.Contains( '"HoldToScope": true' ) -and $worldModelRaw.Contains( '"ScopeOverlay": "materials/scope.vmat"' ) -and $worldModelRaw.Contains( '"UnzoomOnShot": true' )) 'M700 scope behavior seed missing.'
Assert-True ($worldModelRaw.Contains( '"ZoomLevels": [' ) -and $worldModelRaw.Contains( '              60' )) 'Scope zoom seed missing.'
Assert-True ($worldModelRaw.Contains( '"ShootSound": null' )) 'SR-25 fire sound must remain unwired.'
Assert-True ($worldModelRaw.Contains( '"EjectionPrefab": null' )) 'SR-25 must not present the donor 5.56 casing as a correct 7.62 casing.'
Assert-True ($worldModelRaw.Contains( 'prefabs/weapon_effects/suppressed_muzzleflash.prefab' )) 'Mounted SR-25 suppressor must use the suppressed muzzle-flash presentation.'
Assert-True (-not $worldModelRaw.Contains( 'prefabs/weapon_effects/556_casing.prefab' )) 'SR-25 still references the incorrect donor 5.56 casing.'
Assert-True (-not $worldModelRaw.Contains( 'prefabs/weapon_effects/rifle_muzzleflash.prefab' )) 'SR-25 still references the unsuppressed rifle muzzle flash.'
Assert-True ($worldModelRaw.Contains( 'addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab' )) 'World-model exact view-model reference missing.'

$devCommandChecks = [ordered]@{
	Name = $devCommandRaw.Contains( 'private const string Command = "lp_give_sr25";' )
	ExactPrefabConstant = $devCommandRaw.Contains( 'private const string WorldPrefabPath = "addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25.prefab";' )
	RosterLookup = $devCommandRaw.Contains( 'var resource = ResolveResource();' ) -and
		$devCommandRaw.Contains( 'if ( pathRows.Length != 1 ) return null;' ) -and
		$devCommandRaw.Contains( 'contentRows.Length == 1 && contentRows[0].Id == resource.Id' )
	PrefabLoad = $devCommandRaw.Contains( 'GameObject.GetPrefab( WorldPrefabPath )' )
	SingleLiteralPath = ([regex]::Matches( $devCommandRaw, 'addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25\.prefab' ).Count -eq 1)
}
Assert-True $devCommandChecks.Name 'Dev command name mismatch.'
Assert-True $devCommandChecks.ExactPrefabConstant 'Dev exact-prefab constant missing.'
Assert-True $devCommandChecks.RosterLookup 'Dev roster lookup missing.'
Assert-True $devCommandChecks.PrefabLoad 'Dev exact-prefab load missing.'
Assert-True $devCommandChecks.SingleLiteralPath 'Dev command contains more than one literal prefab path.'

$productFiles = @(
	Get-ChildItem -LiteralPath "$root/equipment" -File -Recurse | Where-Object { $_.Extension -in '.prefab', '.vmat' }
	Get-ChildItem -LiteralPath "$root/materials/native_candidate" -File -Filter '*.vmat'
	Get-ChildItem -LiteralPath "$root/models/native_candidate" -File -Filter '*.vmdl'
	Get-Item -LiteralPath "$root/sr25.vmdl"
	Get-Item -LiteralPath "$root/sr25_mat.vmat"
	Get-Item -LiteralPath $devCommandPath
)
$productRaw = [string]::Join( "`n", @( $productFiles | ForEach-Object { Read-Text $_.FullName } ) )
$forbiddenAutoRiggerReferences = [regex]::Matches( $productRaw, '(?i)(?:addons/lifepunch/autorig|models/autorig)' ).Count
Assert-True ($forbiddenAutoRiggerReferences -eq 0) 'Candidate references an Auto Rigger path.'
Assert-True (-not $productRaw.Contains( 'addons/lifepunch/lpweapons/ar15' )) 'Candidate references the AR-15 addon path.'
Assert-True (-not [regex]::IsMatch( $productRaw, '(?<!native_clean)sr25\.fbx' )) 'Candidate references excluded flattened sr25.fbx.'

$localReferences = @( [regex]::Matches( $productRaw, 'addons/lifepunch/lpweapons/sr25/[A-Za-z0-9_./-]+\.(?:vmat|vmdl|prefab|fbx|png)' ) |
	ForEach-Object { $_.Value } | Sort-Object -Unique )
$missingLocalReferences = @( $localReferences | Where-Object {
	-not (Test-Path -LiteralPath (Join-Path "$repoRoot\game\Assets" ($_ -replace '/', '\')) -PathType Leaf)
} )
Assert-True ($missingLocalReferences.Count -eq 0) "Missing local asset refs: $($missingLocalReferences -join ',')"

$result = [ordered]@{
	Ok = ($failures.Count -eq 0)
	Failures = @($failures)
	PinnedFileHashes = $pinnedFiles.Count
	Manifests = $manifests
	MaterialFamilies = $materialFamilies.Count
	ModelDocs = $modelDocs.Count
	ViewModelSemanticModels = $viewModelParts.Count
	GuidDefinitions = [ordered]@{
		ViewModel = $viewModelDefinitions.Count
		WorldModel = $worldModelDefinitions.Count
		CombinedUnique = @( $allDefinitions | Sort-Object -Unique ).Count
		DonorOverlap = $donorOverlap.Count
	}
	DanglingInternalReferences = $danglingInternalReferences.Count
	LocalAssetReferences = $localReferences.Count
	MissingLocalReferences = $missingLocalReferences.Count
	ForbiddenAutoRiggerReferences = $forbiddenAutoRiggerReferences
	WorldScopeComponents = [ordered]@{
		FovOffset = $fovOffsetCount
		ScopeWeaponComponent = $scopeWeaponComponentCount
		BoltPusher = $boltPusherCount
		ManualOffset = $manualOffsetCount
	}
	DevCommand = $devCommandChecks
}

$result | ConvertTo-Json -Depth 8 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

exit 0
