[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$assetsRoot = Join-Path $repoRoot 'game\Assets'
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoText( [string] $RelativePath )
{
	$path = Join-Path $repoRoot $RelativePath
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) )
	{
		$failures.Add( "Missing required file: $RelativePath" )
		return ''
	}

	return Get-Content -LiteralPath $path -Raw
}

function Require-Match( [string] $Text, [string] $Pattern, [string] $Failure )
{
	if ( $Text -notmatch $Pattern )
	{
		$failures.Add( $Failure )
	}
}

function Test-PrefabRootComponent(
	[string] $AssetPath,
	[string] $ComponentType )
{
	$path = Join-Path $assetsRoot ($AssetPath.Replace( '/', '\' ))
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) )
	{
		return $false
	}
	$prefab = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
	$componentsProperty = $prefab.RootObject.PSObject.Properties['Components']
	$components = if ( $null -eq $componentsProperty ) { @() } else { @($componentsProperty.Value) }
	return @($components | Where-Object { $_.__type -eq $ComponentType }).Count -gt 0
}

$weapons = @(
	[ordered]@{
		Name = 'AK-47'
		Source = 'game\Code\Addons\lifepunch\_dev\Ak47DevGive.cs'
		Command = 'lp_give_ak'
		GrokCommand = 'lp_give_ak_grok'
		GrokDelegate = 'Ak47DevGive.GiveAkTo'
		World = 'addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab'
		View = 'addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab'
		ObjectName = 'w_ak47'
		PinnedId = 'c029bcc7-bcec-4197-a7a4-8558cc3d90e7'
	}
	[ordered]@{
		Name = 'AKS-74U'
		Source = 'game\Code\Addons\lifepunch\_dev\Aks74uDevGive.cs'
		Command = 'lp_give_aks74u'
		GrokCommand = 'lp_give_aks74u_grok'
		GrokDelegate = 'Aks74uDevGive.GiveAks74uTo'
		World = 'addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab'
		View = 'addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab'
		ObjectName = 'w_aks74u'
		PinnedId = $null
	}
	[ordered]@{
		Name = 'AR-15'
		Source = 'game\Code\Addons\lifepunch\_dev\Ar15DevGive.cs'
		Command = 'lp_give_ar15'
		GrokCommand = 'lp_give_ar15_grok'
		GrokDelegate = 'Ar15DevGive.GiveAr15To'
		World = 'addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab'
		View = 'addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab'
		ObjectName = 'w_ar15'
		PinnedId = $null
	}
	[ordered]@{
		Name = 'Desert Eagle'
		Source = 'game\Code\Addons\lifepunch\_dev\DesertEagleDevGive.cs'
		Command = 'lp_give_deagle'
		GrokCommand = 'lp_give_deagle_grok'
		GrokDelegate = 'DesertEagleDevGive.GiveDesertEagleTo'
		World = 'addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab'
		View = 'addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab'
		ObjectName = 'w_desert_eagle'
		PinnedId = $null
	}
	[ordered]@{
		Name = 'M1911'
		Source = 'game\Code\Addons\lifepunch\_dev\M1911DevGive.cs'
		Command = 'lp_give_m1911'
		GrokCommand = 'lp_give_m1911_grok'
		GrokDelegate = 'M1911DevGive.GiveM1911To'
		World = 'addons/lifepunch/lpweapons/m1911/equipment/w_m1911/w_m1911.prefab'
		View = 'addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab'
		ObjectName = 'w_m1911'
		PinnedId = $null
	}
	[ordered]@{
		Name = 'M870'
		Source = 'game\Code\Addons\lifepunch\_dev\M870DevGive.cs'
		Command = 'lp_give_m870'
		GrokCommand = 'lp_give_m870_grok'
		GrokDelegate = 'M870DevGive.GiveM870To'
		World = 'addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab'
		View = 'addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab'
		ObjectName = 'w_m870'
		PinnedId = $null
	}
	[ordered]@{
		Name = 'SR-25'
		Source = 'game\Code\Addons\lifepunch\_dev\Sr25DevGive.cs'
		Command = 'lp_give_sr25'
		GrokCommand = 'lp_give_sr25_grok'
		GrokDelegate = 'Sr25DevGive.GiveSr25To'
		World = 'addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25.prefab'
		View = 'addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab'
		ObjectName = 'w_sr25'
		PinnedId = $null
	}
)

$commands = @($weapons | ForEach-Object { $_.Command })
$grokCommands = @($weapons | ForEach-Object { $_.GrokCommand })
$viewAssetPreflightCount = 0
$activeMeasurementCount = 0
if ( @($commands | Sort-Object -Unique).Count -ne $weapons.Count )
{
	$failures.Add( 'Developer weapon commands must be unique.' )
}
if ( @($grokCommands | Sort-Object -Unique).Count -ne $weapons.Count )
{
	$failures.Add( 'GROK third-person weapon commands must be unique.' )
}

foreach ( $weapon in $weapons )
{
	$source = Read-RepoText $weapon.Source
	$escapedCommand = [regex]::Escape( $weapon.Command )
	$escapedWorld = [regex]::Escape( $weapon.World )
	$escapedView = [regex]::Escape( $weapon.View )
	$escapedObjectName = [regex]::Escape( $weapon.ObjectName )

	if ( $weapon.PinnedId )
	{
		Require-Match $source ('\[ConCmd\(\s*"{0}"\s*\)\]' -f $escapedCommand) `
			"$($weapon.Name) does not expose the exact literal command $($weapon.Command)."
	}
	else
	{
		Require-Match $source ('private const string Command\s*=\s*"{0}";' -f $escapedCommand) `
			"$($weapon.Name) command constant does not equal $($weapon.Command)."
		Require-Match $source '\[ConCmd\(\s*Command\s*\)\]' `
			"$($weapon.Name) console command is no longer bound to its pinned constant."
	}
	$helperMatch = [regex]::Match(
		$source,
		'public static bool \w+To\([\s\S]*?(?=\r?\n\tprivate static )' )
	if ( !$helperMatch.Success )
	{
		$failures.Add( "$($weapon.Name) callable give helper could not be isolated." )
		$helperSource = ''
	}
	else
	{
		$helperSource = $helperMatch.Value
	}
	Require-Match $source 'public static void \w+\(\)[\s\S]*?Application\.IsEditor[\s\S]*?Networking\.IsHost[\s\S]*?\w+To\( Player\.Local' `
		"$($weapon.Name) console wrapper no longer enforces editor-host context before delegation."
	Require-Match $helperSource '!Application\.IsEditor\s*\|\|\s*!Networking\.IsHost' `
		"$($weapon.Name) callable helper no longer enforces editor-host context."
	Require-Match $helperSource '!player\.IsValid\(\)\s*\|\|\s*!player\.WeaponGameObject\.IsValid\(\)' `
		"$($weapon.Name) does not fail closed for an invalid pawn or weapon holder."
	$worldPrefabCall = if ( $weapon.PinnedId ) {
		'var prefab = GameObject.GetPrefab( prefabPath );'
	} else {
		'var prefab = GameObject.GetPrefab( WorldPrefabPath );'
	}
	$viewPrefabCall = if ( $weapon.PinnedId ) {
		'var viewPrefab = GameObject.GetPrefab( viewPrefabPath );'
	} else {
		'var viewPrefab = GameObject.GetPrefab( ViewPrefabPath );'
	}
	Require-Match $helperSource ([regex]::Escape( $worldPrefabCall )) `
		"$($weapon.Name) does not preflight its exact world prefab."
	Require-Match $helperSource ([regex]::Escape( $viewPrefabCall )) `
		"$($weapon.Name) does not preflight its exact viewmodel prefab."
	if ( $helperSource.Contains( $worldPrefabCall ) -and $helperSource.Contains( $viewPrefabCall ) )
	{
		$viewAssetPreflightCount++
	}
	Require-Match $helperSource 'viewPrefab\.Components\.Get<ViewModel>\(\)\.IsValid\(\)' `
		"$($weapon.Name) does not prove its effective view prefab has a root ViewModel component."
	Require-Match $helperSource 'Components\.Get<Equipment>\(\s*FindMode\.EverythingInSelfAndDescendants\s*\)' `
		"$($weapon.Name) does not validate the cloned Equipment component."
	Require-Match $helperSource 'if \( !go\.IsValid\(\) \)' `
		"$($weapon.Name) does not fail closed when its world-prefab clone is invalid."
	Require-Match $helperSource 'equipment\.ViewModelPrefab = viewPrefab;' `
		"$($weapon.Name) does not bind the validated view prefab onto the runtime clone."
	Require-Match $helperSource 'if \( !player\.CantSwitch \)[\s\S]*?player\.SetCurrentEquipment\( equipment \);' `
		"$($weapon.Name) no longer guards activation with CantSwitch."
	Require-Match $helperSource 'var active = player\.CurrentEquipment == equipment;' `
		"$($weapon.Name) does not measure whether the created weapon became active."
	Require-Match $helperSource 'created [^\r\n]*active=\{active\}' `
		"$($weapon.Name) success log does not distinguish creation from activation."
	if ( $helperSource -match 'var active = player\.CurrentEquipment == equipment;' )
	{
		$activeMeasurementCount++
	}
	$worldPrefabIndex = $helperSource.IndexOf( $worldPrefabCall, [StringComparison]::Ordinal )
	$worldPrefabValidationIndex = $helperSource.IndexOf( 'if ( !prefab.IsValid() )', [StringComparison]::Ordinal )
	$viewPrefabIndex = $helperSource.IndexOf( $viewPrefabCall, [StringComparison]::Ordinal )
	$viewPrefabValidationIndex = $helperSource.IndexOf( 'if ( !viewPrefab.IsValid() )', [StringComparison]::Ordinal )
	$viewComponentIndex = $helperSource.IndexOf( 'viewPrefab.Components.Get<ViewModel>()', [StringComparison]::Ordinal )
	$cloneIndex = $helperSource.IndexOf( 'var go = prefab.Clone', [StringComparison]::Ordinal )
	$cloneValidationIndex = $helperSource.IndexOf( 'if ( !go.IsValid() )', [StringComparison]::Ordinal )
	$equipmentValidationIndex = $helperSource.IndexOf( 'var equipment = go.Components.Get<Equipment>', [StringComparison]::Ordinal )
	$equipmentIsValidIndex = $helperSource.IndexOf( 'if ( !equipment.IsValid() )', [StringComparison]::Ordinal )
	$equipmentIdAssignment = if ( $weapon.PinnedId ) {
		'equipment.EquipmentId = AkEquipmentId;'
	} else {
		'equipment.EquipmentId = resource.GameModeAddonContentId;'
	}
	$equipmentIdIndex = $helperSource.IndexOf( $equipmentIdAssignment, [StringComparison]::Ordinal )
	$viewBindingIndex = $helperSource.IndexOf( 'equipment.ViewModelPrefab = viewPrefab;', [StringComparison]::Ordinal )
	$spawnIndex = $helperSource.LastIndexOf( 'go.NetworkSpawn', [StringComparison]::Ordinal )
	$removeIndex = $helperSource.IndexOf( 'RemoveExisting(', [StringComparison]::Ordinal )
	$orderedIndexes = @(
		$worldPrefabIndex,
		$worldPrefabValidationIndex,
		$viewPrefabIndex,
		$viewPrefabValidationIndex,
		$viewComponentIndex,
		$cloneIndex,
		$cloneValidationIndex,
		$equipmentValidationIndex,
		$equipmentIsValidIndex,
		$equipmentIdIndex,
		$viewBindingIndex,
		$spawnIndex,
		$removeIndex
	)
	if ( @($orderedIndexes | Where-Object { $_ -lt 0 }).Count -gt 0 )
	{
		$failures.Add( "$($weapon.Name) replacement lifecycle is missing a required stage." )
	}
	else
	{
		for ( $i = 1; $i -lt $orderedIndexes.Count; $i++ )
		{
			if ( $orderedIndexes[$i - 1] -ge $orderedIndexes[$i] )
			{
				$failures.Add( "$($weapon.Name) replacement lifecycle order regressed before stage $i." )
				break
			}
		}
	}
	Require-Match $source 'ReferenceEquals\( weapon, except \)' `
		"$($weapon.Name) replacement cleanup does not protect the newly spawned equipment."
	Require-Match $source ('string\.Equals\( weapon\.GameObject\.Name, "{0}", StringComparison\.OrdinalIgnoreCase \)' -f $escapedObjectName) `
		"$($weapon.Name) replacement cleanup is no longer limited to its exact object name."

	if ( $weapon.PinnedId )
	{
		Require-Match $source ([regex]::Escape( $weapon.PinnedId )) `
			"$($weapon.Name) lost its pinned content ID."
		Require-Match $source 'contentRows\.Length == 1' `
			"$($weapon.Name) no longer requires one unique content-ID row."
		Require-Match $source 'pathRows\.Length == 1' `
			"$($weapon.Name) no longer requires one unique exact-path row."
		Require-Match $source 'pathResource!?\.GameModeAddonContentId != AkEquipmentId' `
			"$($weapon.Name) no longer requires ID/path agreement."
		Require-Match $source 'pathResource\.Id != resource!?\.Id' `
			"$($weapon.Name) no longer requires the pinned and exact-path rows to be the same DTO row."
		Require-Match $source 'Ak47Weapon\.ViewModelPrefabPath' `
			"$($weapon.Name) no longer uses its exact viewmodel contract."
		Require-Match $source 'resource\.SecondaryPrefabPath\(\), viewPrefabPath, StringComparison\.OrdinalIgnoreCase' `
			"$($weapon.Name) no longer requires the exact Portal viewmodel reference."
		Require-Match $source 'GameObject\.GetPrefab\( viewPrefabPath \)' `
			"$($weapon.Name) does not preflight its exact viewmodel asset."
		Require-Match $source 'equipment\.EquipmentId = AkEquipmentId;' `
			"$($weapon.Name) no longer stamps the pinned content ID."
		Require-Match $helperSource 'RemoveExisting\( player, equipment \);' `
			"$($weapon.Name) does not pass the replacement as the cleanup exclusion."
		Require-Match $source 'weapon\.EquipmentId == AkEquipmentId' `
			"$($weapon.Name) cleanup is no longer limited to the pinned equipment ID."
		$akContract = Read-RepoText 'game\Code\Addons\lifepunch\ak47\AK47.cs'
		Require-Match $akContract ('WorldPrefabPath\s*=\s*"{0}"' -f $escapedWorld) `
			"$($weapon.Name) world-prefab constant no longer matches the dev contract."
		Require-Match $akContract ('ViewModelPrefabPath\s*=\s*"{0}"' -f $escapedView) `
			"$($weapon.Name) viewmodel-prefab constant no longer matches the dev contract."
	}
	else
	{
		Require-Match $source ('WorldPrefabPath\s*=\s*"{0}"' -f $escapedWorld) `
			"$($weapon.Name) world-prefab constant is not exact."
		Require-Match $source ('ViewPrefabPath\s*=\s*"{0}"' -f $escapedView) `
			"$($weapon.Name) view-prefab constant is not exact."
		Require-Match $source 'var resource = ResolveResource\(\);' `
			"$($weapon.Name) no longer uses its unique exact-path resolver."
		Require-Match $source 'pathRows\.Length != 1' `
			"$($weapon.Name) no longer fails closed on missing or duplicate exact-path rows."
		Require-Match $source 'contentRows\.Length == 1 && contentRows\[0\]\.Id == resource\.Id' `
			"$($weapon.Name) no longer proves content-ID resolution returns the selected DTO row."
		Require-Match $source 'resource!\.SecondaryPrefabPath\(\), ViewPrefabPath, StringComparison\.OrdinalIgnoreCase' `
			"$($weapon.Name) no longer requires the exact Portal viewmodel reference."
		Require-Match $source 'GameObject\.GetPrefab\( ViewPrefabPath \)' `
			"$($weapon.Name) does not preflight its exact viewmodel asset."
		Require-Match $source 'equipment\.EquipmentId = resource\.GameModeAddonContentId;' `
			"$($weapon.Name) no longer stamps the resolved content ID."
		Require-Match $helperSource 'RemoveExisting\( player, resource\.GameModeAddonContentId, equipment \);' `
			"$($weapon.Name) does not pass the exact ID and replacement exclusion to cleanup."
		Require-Match $source 'weapon\.EquipmentId == equipmentId' `
			"$($weapon.Name) cleanup is no longer limited to the selected equipment ID."
	}

	$worldPath = Join-Path $assetsRoot ($weapon.World.Replace( '/', '\' ))
	$viewPath = Join-Path $assetsRoot ($weapon.View.Replace( '/', '\' ))
	if ( !(Test-Path -LiteralPath $worldPath -PathType Leaf) )
	{
		$failures.Add( "$($weapon.Name) world prefab is missing: $($weapon.World)" )
		continue
	}
	if ( !(Test-Path -LiteralPath $viewPath -PathType Leaf) )
	{
		$failures.Add( "$($weapon.Name) viewmodel prefab is missing: $($weapon.View)" )
	}
	elseif ( !(Test-PrefabRootComponent $weapon.View 'Dxura.RP.Game.ViewModel') )
	{
		$failures.Add( "$($weapon.Name) viewmodel prefab does not materialize a direct root Dxura.RP.Game.ViewModel component." )
	}

	try
	{
		$world = Get-Content -LiteralPath $worldPath -Raw | ConvertFrom-Json
		$equipment = @($world.RootObject.Components | Where-Object { $_.__type -eq 'Dxura.RP.Game.Equipment' })
		if ( $equipment.Count -ne 1 )
		{
			$failures.Add( "$($weapon.Name) world prefab must contain exactly one root Equipment; found $($equipment.Count)." )
		}
		elseif ( $equipment[0].ViewModelPrefab.prefab -ne $weapon.View )
		{
			$failures.Add( "$($weapon.Name) world prefab points at '$($equipment[0].ViewModelPrefab.prefab)' instead of '$($weapon.View)'." )
		}
	}
	catch
	{
		$failures.Add( "$($weapon.Name) world prefab JSON failed to parse: $($_.Exception.Message)" )
	}
}

$intellibotSource = Read-RepoText 'game\Code\Addons\lifepunch\_dev\IntellibotSession.cs'
$testBotSource = Read-RepoText 'game\Code\Addons\lifepunch\_dev\StaffMenuTestBots.cs'
$grokMappingCount = 0
foreach ( $weapon in $weapons )
{
	$escapedGrokCommand = [regex]::Escape( $weapon.GrokCommand )
	$escapedGrokDelegate = [regex]::Escape( $weapon.GrokDelegate )
	$mappingPattern = '\[ConCmd\(\s*"{0}"\s*\)\][\s\S]*?GiveWeaponToGrok\(\s*"{0}"\s*,\s*{1}\s*\);' -f
		$escapedGrokCommand,
		$escapedGrokDelegate
	if ( $intellibotSource -match $mappingPattern )
	{
		$grokMappingCount++
	}
	else
	{
		$failures.Add( "$($weapon.Name) lacks its exact GROK third-person command/delegate mapping." )
	}
}

$originalGrokCommand = 'lp_give_aks74u_original_grok'
$originalGrokDelegate = 'Aks74uOriginalDevGive.GiveAks74uOriginalTo'
$originalGrokMappingPattern = '\[ConCmd\(\s*"{0}"\s*\)\][\s\S]*?GiveWeaponToGrok\(\s*"{0}"\s*,\s*{1}\s*\);' -f
	[regex]::Escape( $originalGrokCommand ),
	[regex]::Escape( $originalGrokDelegate )
$originalGrokMapping = $intellibotSource -match $originalGrokMappingPattern
if ( !$originalGrokMapping )
{
	$failures.Add( 'AKS-74U Original lacks its exact GUID-owned GROK third-person command/delegate mapping.' )
}

Require-Match $intellibotSource 'GiveWeaponToGrok\([\s\S]*?GuardHost\( cmd \)[\s\S]*?Config\.Current\.IsReady[\s\S]*?TryRequireSpawnedGrok\( cmd, out var grok \)[\s\S]*?give\( grok, cmd \);' `
	'GROK weapon delegation must remain editor-host gated, config-ready, and exact-spawn-owned.'
Require-Match $intellibotSource 'TryRequireSpawnedGrok\([\s\S]*?TryFindGrok\(\)[\s\S]*?Player\.Local[\s\S]*?grok\.GameObject\.Id[\s\S]*?grok\.WeaponGameObject\.IsValid\(\)' `
	'GROK target resolution must refuse Player.Local and require a valid weapon holder.'
Require-Match $intellibotSource 'TryFindGrok\(\)[\s\S]*?TryGetSpawnedNamedBot\( GrokSteamId, GrokName, out var grok \)' `
	'GROK lookup must remain limited to the exact bot recorded by the spawn lane.'
Require-Match $intellibotSource '\[ConCmd\( "lp_grok_here" \)\][\s\S]*?MoveSeatHere\( "lp_grok_here", GrokSteamId, GrokName, 0 \);' `
	'GROK third-person observation positioning command is missing.'
Require-Match $intellibotSource 'MoveSeatHere\( string cmd, long steamId, string name, int seat \)[\s\S]*?TryGetSpawnedNamedBot\( steamId, name, out var pawn \)' `
	'GROK positioning must resolve the exact GUID-owned named pawn before teleporting it.'
Require-Match $intellibotSource '\[ConCmd\( "lp_clear_grok" \)\][\s\S]*?ClearSeat\( "lp_clear_grok", GrokSteamId, GrokName \);' `
	'GROK cleanup command is missing.'
Require-Match $intellibotSource 'ClearSeat\( string cmd, long steamId, string name \)[\s\S]*?RemoveSpawnedNamedBot\( steamId, name, cmd \);' `
	'GROK cleanup must delegate to exact GUID-owned named-bot removal.'
Require-Match $testBotSource 'if \( !EnsureBotSlot\( steamId, name \) \)[\s\S]*?return null;' `
	'Named bot spawning must fail closed when its fixed synthetic slot is not safely replaceable.'
Require-Match $testBotSource 'EnsureBotSlot\( long steamId, string exactName \)[\s\S]*?!TryGetSpawnedNamedBot\( steamId, exactName, out var owned \)[\s\S]*?!ReferenceEquals\( player, owned \)[\s\S]*?refusing to replace' `
	'Fixed synthetic slots must refuse an occupied pawn unless exact spawn-lane ownership is proven.'
Require-Match $testBotSource 'Dictionary<long, Guid> _spawnedGameObjectIds = new\(\);' `
	'Test-bot ownership must retain the originally spawned GameObject GUID for every synthetic SteamId.'
Require-Match $testBotSource '_spawnedGameObjectIds\[steamId\] = go\.Id;' `
	'Test-bot spawning must pin the newly created GameObject GUID.'
Require-Match $testBotSource 'TryGetSpawnedBot\( long steamId, out Player player \)[\s\S]*?_spawnedGameObjectIds\.TryGetValue\( steamId, out var gameObjectId \)[\s\S]*?candidate\.GameObject\.Id != gameObjectId' `
	'Test-bot resolution must compare the current pawn with its originally recorded GameObject GUID.'
Require-Match $testBotSource 'TryGetSpawnedNamedBot\( long steamId, string exactName, out Player player \)[\s\S]*?TryGetSpawnedBot\( steamId, out player \)' `
	'Named bot resolution must inherit the GameObject-GUID ownership proof.'
Require-Match $testBotSource 'RemoveSpawnedNamedBot\( long steamId, string exactName, string logPrefix \)[\s\S]*?!TryGetSpawnedNamedBot\( steamId, exactName, out var player \)[\s\S]*?player\.GameObject\.Destroy\(\)' `
	'Named bot destruction must require exact GUID-owned named-bot resolution.'
Require-Match $testBotSource 'ClearTestBots\(\)[\s\S]*?foreach \( var id in _spawned\.ToArray\(\) \)[\s\S]*?!TryGetSpawnedBot\( id, out var player \)[\s\S]*?refusing pawn and rank mutation' `
	'Global bot cleanup must fail closed when GameObject-GUID ownership is not proved.'
Require-Match $testBotSource 'TryAssignBotRank\([\s\S]*?!TryGetSpawnedBot\( steamId, out _ \)[\s\S]*?refusing assignment' `
	'Test-bot rank writes must require GameObject-GUID ownership.'
Require-Match $testBotSource 'BotSay\( string args = "" \)[\s\S]*?ResolveBotSteamId\( botToken \)[\s\S]*?!TryGetSpawnedBot\( steamId, out var player \)' `
	'Test-bot chat must re-prove GameObject-GUID ownership immediately before acting.'

$ensureSlotMatch = [regex]::Match(
	$testBotSource,
	'private static bool EnsureBotSlot\( long steamId, string exactName \)(?<body>[\s\S]*?)(?=\r?\n\t\[ConCmd\()' )
if ( !$ensureSlotMatch.Success )
{
	$failures.Add( 'Fixed synthetic-slot helper could not be isolated for rank-mutation containment.' )
}
else
{
	$ensureSlotBody = $ensureSlotMatch.Groups['body'].Value
	$ownedBranchIndex = $ensureSlotBody.IndexOf( 'if ( ownedBotRemoved )', [StringComparison]::Ordinal )
	$ownedOpenIndex = if ( $ownedBranchIndex -ge 0 ) {
		$ensureSlotBody.IndexOf( '{', $ownedBranchIndex )
	} else {
		-1
	}
	$ownedCloseIndex = -1
	if ( $ownedOpenIndex -ge 0 )
	{
		$depth = 0
		for ( $i = $ownedOpenIndex; $i -lt $ensureSlotBody.Length; $i++ )
		{
			if ( $ensureSlotBody[$i] -eq '{' )
			{
				$depth++
			}
			elseif ( $ensureSlotBody[$i] -eq '}' )
			{
				$depth--
				if ( $depth -eq 0 )
				{
					$ownedCloseIndex = $i
					break
				}
			}
		}
	}

	if ( $ownedOpenIndex -lt 0 -or $ownedCloseIndex -le $ownedOpenIndex )
	{
		$failures.Add( 'Fixed synthetic-slot GUID-owned removal branch could not be brace-isolated.' )
	}
	else
	{
		$ownedBranchBody = $ensureSlotBody.Substring(
			$ownedOpenIndex + 1,
			$ownedCloseIndex - $ownedOpenIndex - 1 )
		$outsideOwnedBranch = $ensureSlotBody.Remove(
			$ownedOpenIndex,
			$ownedCloseIndex - $ownedOpenIndex + 1 )
		if ( ([regex]::Matches( $ownedBranchBody, 'ranks\.SetPlayerRanks\(' )).Count -ne 1 )
		{
			$failures.Add( 'GUID-owned removal branch must contain exactly one rank-clear call.' )
		}
		if ( $outsideOwnedBranch -match 'ranks\.SetPlayerRanks\(' )
		{
			$failures.Add( 'Fixed synthetic-slot helper contains a rank mutation outside the GUID-owned removal branch.' )
		}
	}
}
if ( $testBotSource -match 'EnsureBotSlot\(\s*steamId\s*\);' )
{
	$failures.Add( 'Named bot spawning still uses the unsafe SteamId-only replacement call.' )
}

$equipmentSource = Read-RepoText 'game\Code\Player\Player.Equipment.cs'
Require-Match $equipmentSource `
	'FindEquipment\( GameModeEquipmentDto resource \)[\s\S]*?weapon\.EquipmentId == resource\.GameModeAddonContentId' `
	'Player.FindEquipment must compare EquipmentId with GameModeAddonContentId.'
if ( $equipmentSource -match 'FindEquipment\( GameModeEquipmentDto resource \)[\s\S]*?weapon\.EquipmentId == resource\.Id' )
{
	$failures.Add( 'Player.FindEquipment still compares the content ID with the equipment-row ID.' )
}

$statusFailureBaseline = $failures.Count
$statusSource = Read-RepoText 'game\Code\Addons\lifepunch\_dev\WeaponRosterStatusDev.cs'
Require-Match $statusSource '\[ConCmd\( Command \)\]' `
	'Weapon roster diagnostic does not expose its command constant.'
Require-Match $statusSource 'Command\s*=\s*"lp_weapon_roster_status"' `
	'Weapon roster diagnostic command name changed.'
Require-Match $statusSource 'Application\.IsEditor' `
	'Weapon roster diagnostic must remain editor-only.'
Require-Match $statusSource 'Networking\.IsHost' `
	'Weapon roster diagnostic must report the host roster only.'
Require-Match $statusSource 'GameModeEquipments\.All[\s\S]*?row\.PrefabPath\(\)[\s\S]*?candidate\.WorldPrefabPath' `
	'Weapon roster diagnostic no longer enumerates exact world-prefab matches.'
Require-Match $statusSource 'matchingEquipmentRows\.Length == 1' `
	'Weapon roster diagnostic no longer fails closed on missing or duplicate exact-path rows.'
Require-Match $statusSource 'var matchingContentRows = rosterRowReady[\s\S]*?GameModeEquipments\.All[\s\S]*?row\.GameModeAddonContentId ==[\s\S]*?equipment!\.GameModeAddonContentId[\s\S]*?\.ToArray\(\)' `
	'Weapon roster diagnostic no longer enumerates every row sharing the selected content ID.'
Require-Match $statusSource 'matchingContentRows\.Length == 1[\s\S]*?matchingContentRows\[0\]\.Id == equipment!\.Id' `
	'Weapon roster diagnostic no longer fails closed when a different row shares the selected content ID.'
Require-Match $statusSource 'var contentIdReady = contentIdUnique && expectedContentIdReady;' `
	'Weapon roster diagnostic no longer combines content-ID uniqueness with the optional pinned ID.'
Require-Match $statusSource 'viewPrefab\.IsValid\(\)[\s\S]*?viewPrefab\.Components\.Get<ViewModel>\(\)\.IsValid\(\)' `
	'Weapon roster diagnostic no longer requires a deployable root ViewModel component.'
Require-Match $statusSource 'matchingEquipmentRowIds\.Contains\( item\.ReferenceId\.Value \)' `
	'Weapon roster diagnostic must report market rows across every duplicate exact-path DTO.'
Require-Match $statusSource 'item\.Type == GameModeMarketItemType\.Equipment[\s\S]*?matchingEquipmentRowIds\.Contains\( item\.ReferenceId\.Value \)' `
	'Weapon roster diagnostic must exclude non-equipment market rows that reuse an equipment-row GUID.'
Require-Match $statusSource 'GameModeMarketItems\.IsSpawnable' `
	'Weapon roster diagnostic no longer measures actual market spawnability.'
Require-Match $statusSource 'string\.Equals\([\s\S]*?equipment!\.SecondaryPrefabPath\(\)[\s\S]*?candidate\.ViewPrefabPath[\s\S]*?StringComparison\.OrdinalIgnoreCase' `
	'Weapon roster diagnostic no longer compares the exact secondary view-prefab path.'
Require-Match $statusSource 'matchingRosterRows=\{matchingEquipmentRows\.Length\}' `
	'Weapon roster diagnostic no longer reports exact-path row cardinality.'
Require-Match $statusSource 'matchingContentRows=\{matchingContentRows\.Length\}' `
	'Weapon roster diagnostic no longer reports content-ID row cardinality.'
Require-Match $statusSource 'contentIdUnique=\{contentIdUnique\}' `
	'Weapon roster diagnostic no longer reports content-ID uniqueness.'
Require-Match $statusSource 'expectedContentIdMatch=\{expectedContentIdReady\}' `
	'Weapon roster diagnostic no longer reports the optional pinned-content-ID result.'
Require-Match $statusSource 'equipmentRowId=\{equipmentRowId\}' `
	'Weapon roster diagnostic no longer reports the equipment DTO row ID.'
Require-Match $statusSource 'marketItemIds=\{marketItemIds\}' `
	'Weapon roster diagnostic no longer reports matching market-item IDs.'
Require-Match $statusSource 'secondaryMatch=\{secondaryReady\}' `
	'Weapon roster diagnostic no longer reports secondary view-path agreement.'
Require-Match $statusSource 'spawnableMarketRows=\{spawnableRows\}' `
	'Weapon roster diagnostic no longer reports spawnable market-row count.'
Require-Match $statusSource 'editorInjected=\{editorInjected\}' `
	'Weapon roster diagnostic no longer labels editor-only synthetic rows.'
Require-Match $statusSource 'readyForMarket\s*=\s*readyForEquip\s*&&\s*!editorInjected\s*&&\s*spawnableRows\s*>\s*0' `
	'Weapon roster diagnostic may not report an editor-only synthetic row as market-ready.'
Require-Match $statusSource 'LP_WEAPON_ROSTER_SUMMARY' `
	'Weapon roster diagnostic lost its machine-readable summary marker.'

$statusCandidates = @(
	@{ Name = 'AK-47'; World = 'addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab'; View = 'addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab'; ExpectedId = 'Ak47DevGive.AkEquipmentId' }
	@{ Name = 'AKS-74U'; World = 'addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab'; View = 'addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab'; ExpectedId = $null }
	@{ Name = 'AKS-74U Original'; World = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab'; View = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/vm_aks74u_original/vm_aks74u_original.prefab'; ExpectedId = $null; EditorId = 'Aks74uOriginalDevRoster.EditorContentId' }
	@{ Name = 'AR-15'; World = 'addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab'; View = 'addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab'; ExpectedId = $null }
	@{ Name = 'Desert Eagle'; World = 'addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab'; View = 'addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab'; ExpectedId = $null }
	@{ Name = 'SR-25'; World = 'addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25.prefab'; View = 'addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab'; ExpectedId = $null }
	@{ Name = 'M1911'; World = 'addons/lifepunch/lpweapons/m1911/equipment/w_m1911/w_m1911.prefab'; View = 'addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab'; ExpectedId = $null }
	@{ Name = 'Glock'; World = 'addons/lifepunch/lpweapons/glock/equipment/w_glock/w_glock.prefab'; View = 'addons/lifepunch/lpweapons/glock/equipment/vm_glock/vm_glock.prefab'; ExpectedId = $null }
	@{ Name = 'M870'; World = 'addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab'; View = 'addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab'; ExpectedId = $null }
)
$candidateBlockMatch = [regex]::Match(
	$statusSource,
	'private static readonly Candidate\[\] Candidates\s*=\s*\[(?<body>[\s\S]*?)\];' )
if ( !$candidateBlockMatch.Success )
{
	$failures.Add( 'Weapon roster diagnostic candidate block could not be isolated.' )
	$candidateBlock = ''
}
else
{
	$candidateBlock = $candidateBlockMatch.Groups['body'].Value
}
$implementationCandidateCount = ([regex]::Matches( $candidateBlock, 'new\(\s*"' )).Count
if ( $implementationCandidateCount -ne $statusCandidates.Count )
{
	$failures.Add( "Weapon roster diagnostic must contain exactly $($statusCandidates.Count) candidates; found $implementationCandidateCount." )
}
foreach ( $candidate in $statusCandidates )
{
	$expectedIdPattern = if ( $candidate.ExpectedId ) {
		',\s*' + [regex]::Escape( $candidate.ExpectedId )
	} else {
		''
	}
	$editorIdPattern = if ( $candidate.ContainsKey( 'EditorId' ) ) {
		',\s*EditorContentId:\s*' + [regex]::Escape( $candidate.EditorId )
	} else {
		''
	}
	$tuplePattern = 'new\(\s*"{0}",\s*"{1}",\s*"{2}"{3}{4}\s*\)' -f
		[regex]::Escape( $candidate.Name ),
		[regex]::Escape( $candidate.World ),
		[regex]::Escape( $candidate.View ),
		$expectedIdPattern,
		$editorIdPattern
	$count = ([regex]::Matches( $candidateBlock, $tuplePattern )).Count
	if ( $count -ne 1 )
	{
		$failures.Add( "Weapon roster diagnostic must contain the exact $($candidate.Name) name/world/view/ID tuple once; found $count." )
	}
}

$duplicateContentId = [Guid]::Parse( '10000000-0000-0000-0000-000000000001' )
$duplicateContentRows = @(
	[pscustomobject]@{
		Id = [Guid]::Parse( '20000000-0000-0000-0000-000000000001' )
		PrefabPath = 'addons/lifepunch/example/equipment/w_example/w_example.prefab'
		GameModeAddonContentId = $duplicateContentId
	}
	[pscustomobject]@{
		Id = [Guid]::Parse( '20000000-0000-0000-0000-000000000002' )
		PrefabPath = 'addons/lifepunch/other/equipment/w_other/w_other.prefab'
		GameModeAddonContentId = $duplicateContentId
	}
)
$negativePathRows = @($duplicateContentRows | Where-Object {
	$_.PrefabPath -eq 'addons/lifepunch/example/equipment/w_example/w_example.prefab'
})
$negativeEquipment = if ( $negativePathRows.Count -eq 1 ) {
	$negativePathRows[0]
} else {
	$null
}
$negativeContentRows = if ( $null -ne $negativeEquipment ) {
	@($duplicateContentRows | Where-Object {
		$_.GameModeAddonContentId -eq $negativeEquipment.GameModeAddonContentId
	})
} else {
	@()
}
$duplicateContentNegativeControlPassed =
	$null -ne $negativeEquipment -and
	$negativeContentRows.Count -eq 2 -and
	-not ($negativeContentRows.Count -eq 1 -and
		$negativeContentRows[0].Id -eq $negativeEquipment.Id)
if ( !$duplicateContentNegativeControlPassed )
{
	$failures.Add( 'Duplicate-content-ID negative control did not reproduce a rejected status row.' )
}

$statusMutationPatterns = @(
	'GetOrCreatePlaceholder',
	'SetCurrentEquipment',
	'\.Clone\(',
	'NetworkSpawn\(',
	'\.Destroy\(',
	'\.(?:Add|AddRange|Clear|Insert|Remove|RemoveAll|RemoveAt|UnionWith|ExceptWith)\(',
	'Config\.Current\.GameMode\.(?:Equipments|MarketItems)\s*=',
	'Config\.Current\.GameMode\.(?:Equipments|MarketItems)\s*\[[^\]]+\]\s*=',
	'GameMode(?:Equipments|MarketItems)\.All\s*\[[^\]]+\]\s*=',
	'(?:equipment|item|row)\.[A-Za-z_]\w*\s*=(?!=)'
)
$statusMutationSeams = 0
foreach ( $mutationPattern in $statusMutationPatterns )
{
	if ( $statusSource -match $mutationPattern )
	{
		$statusMutationSeams++
		$failures.Add( "Weapon roster diagnostic contains forbidden mutation seam: $mutationPattern" )
	}
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapons = $weapons.Count
	commands = @($commands | Sort-Object -Unique).Count
	grokCommands = @($grokCommands | Sort-Object -Unique).Count
	grokMappings = $grokMappingCount
	originalGrokMapping = $originalGrokMapping
	grokSlotOwnershipGuard =
		$testBotSource -match 'EnsureBotSlot\( long steamId, string exactName \)' -and
		$testBotSource -match '_spawnedGameObjectIds\[steamId\] = go\.Id;' -and
		$testBotSource -match 'candidate\.GameObject\.Id != gameObjectId' -and
		$testBotSource -match 'ClearTestBots\(\)[\s\S]*?!TryGetSpawnedBot\( id, out var player \)'
	worldPrefabs = $weapons.Count
	viewPrefabs = $weapons.Count
	pinnedContentIds = @($weapons | Where-Object { $_.PinnedId }).Count
	dynamicExactPathResolvers = @($weapons | Where-Object { !$_.PinnedId }).Count
	viewAssetPreflights = $viewAssetPreflightCount
	activationMeasurements = $activeMeasurementCount
	statusCandidates = $implementationCandidateCount
	statusMutationSeams = $statusMutationSeams
	statusStaticContainment = $failures.Count -eq $statusFailureBaseline
	statusContentIdUniqueness =
		$duplicateContentNegativeControlPassed -and
		$statusSource -match 'matchingContentRows\.Length == 1[\s\S]*?matchingContentRows\[0\]\.Id == equipment!\.Id'
	findEquipmentUsesContentId = $equipmentSource -match 'FindEquipment\( GameModeEquipmentDto resource \)[\s\S]*?weapon\.EquipmentId == resource\.GameModeAddonContentId'
	failures = @($failures)
}

$summary | ConvertTo-Json -Depth 4 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

$resultLine =
	'RESULT weapon_dev_roster=PASS weapons={0} commands={1} exact_world_paths={2} ' +
	'exact_view_paths={3} view_asset_preflights={4} activation_measurements={5} ' +
	'pinned_content_ids={6} dynamic_path_resolvers={7} status_candidates={8} ' +
	'status_duplicate_detection=1 status_content_id_uniqueness={9} status_mutation_seams={10} ' +
	'find_equipment_content_id={11} grok_commands={12} grok_mappings={13} grok_slot_ownership_guard={14}'
Write-Output ($resultLine -f
	$summary.weapons,
	$summary.commands,
	$summary.worldPrefabs,
	$summary.viewPrefabs,
	$summary.viewAssetPreflights,
	$summary.activationMeasurements,
	$summary.pinnedContentIds,
	$summary.dynamicExactPathResolvers,
	$summary.statusCandidates,
	([int]$summary.statusContentIdUniqueness),
	$summary.statusMutationSeams,
	([int]$summary.findEquipmentUsesContentId),
	$summary.grokCommands,
	$summary.grokMappings,
	([int]$summary.grokSlotOwnershipGuard) )
