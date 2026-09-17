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

function Require-Asset( [string] $AssetPath )
{
	$path = Join-Path $assetsRoot ($AssetPath.Replace( '/', '\' ))
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) )
	{
		$failures.Add( "Missing ejection asset: $AssetPath" )
	}
}

$source = Read-RepoText 'game\Code\Equipment\Weapon\ShootWeaponComponent.cs'
$projectileSource = Read-RepoText 'game\Code\Equipment\Weapon\ProjectileWeaponComponent.cs'
$aks = Read-RepoText 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\w_aks74u\w_aks74u.prefab'

$propertyMatches = [regex]::Matches(
	$source,
	'public GameObject\? EjectionPrefab \{ get; set; \}'
)
$clonePattern = 'EjectionPrefab\.Clone\(\s*new CloneConfig\s*\{(?<body>[\s\S]*?)\}\s*\);'
$sourceCloneMatches = [regex]::Matches( $source, $clonePattern )
$projectileCloneMatches = [regex]::Matches( $projectileSource, $clonePattern )

if ( $propertyMatches.Count -ne 1 )
{
	$failures.Add( "ShootWeaponComponent must define exactly one EjectionPrefab property; found $($propertyMatches.Count)." )
}
if ( $sourceCloneMatches.Count -ne 1 )
{
	$failures.Add( "ShootWeaponComponent must contain exactly one casing clone; found $($sourceCloneMatches.Count)." )
}
if ( $projectileCloneMatches.Count -ne 1 )
{
	$failures.Add( "ProjectileWeaponComponent reference contract is ambiguous; found $($projectileCloneMatches.Count) casing clones." )
}
if ( $sourceCloneMatches.Count -eq 1 -and $projectileCloneMatches.Count -eq 1 )
{
	$sourceClone = [regex]::Replace( $sourceCloneMatches[0].Value, '\s+', ' ' ).Trim()
	$projectileClone = [regex]::Replace( $projectileCloneMatches[0].Value, '\s+', ' ' ).Trim()
	if ( $sourceClone -ne $projectileClone )
	{
		$failures.Add( 'Hitscan casing clone no longer matches the established projectile clone contract.' )
	}
	if ( $sourceClone.Contains( 'NetworkSpawn' ) )
	{
		$failures.Add( 'Hitscan casing effects must remain per-client visuals and must not NetworkSpawn.' )
	}
}

Require-Match $source 'public GameObject\? EjectionPrefab \{ get; set; \}' `
	'ShootWeaponComponent does not expose the serialized EjectionPrefab contract.'
Require-Match $source 'EjectionPrefab\.IsValid\(\) && Effector\.EjectionPort\.IsValid\(\)' `
	'ShootWeaponComponent does not validate both the effect and active ejection port.'
Require-Match $source 'EjectionPrefab\.Clone\( new CloneConfig[\s\S]*?Parent = Effector\.EjectionPort' `
	'ShootWeaponComponent does not clone the casing effect at the active ejection port.'

Require-Match $aks '"EjectionPrefab"\s*:\s*null' `
	'AKS-74U must leave casing ejection null until a caliber-correct effect exists.'
if ( $aks.Contains( 'prefabs/weapon_effects/9mm_casing.prefab' ) )
{
	$failures.Add( 'AKS-74U still inherits the MP5 9mm casing effect.' )
}
$activeCasings = [ordered]@{
	'game\Assets\gameplay\equipment\weapons\m4a1\w_m4a1.prefab' = 'prefabs/weapon_effects/556_casing.prefab'
	'game\Assets\gameplay\equipment\weapons\m700\w_m700.prefab' = 'prefabs/weapon_effects/556_casing.prefab'
	'game\Assets\gameplay\equipment\weapons\mp5\w_mp5.prefab' = 'prefabs/weapon_effects/9mm_casing.prefab'
	'game\Assets\gameplay\equipment\weapons\spaghelli\w_spaghelli.prefab' = 'prefabs/weapon_effects/12g_casing.prefab'
	'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\w_ar15\w_ar15.prefab' = 'prefabs/weapon_effects/556_casing.prefab'
	'game\Assets\addons\lifepunch\lpweapons\m870\equipment\w_m870\w_m870.prefab' = 'prefabs/weapon_effects/12g_casing.prefab'
}

foreach ( $entry in $activeCasings.GetEnumerator() )
{
	$prefab = Read-RepoText $entry.Key
	$escapedAsset = [regex]::Escape( $entry.Value )
	$assetPattern = '"prefab"\s*:\s*"{0}"' -f $escapedAsset
	Require-Match $prefab $assetPattern `
		"$($entry.Key) no longer references $($entry.Value)."
	Require-Asset $entry.Value
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	propertyCount = $propertyMatches.Count
	cloneCount = $sourceCloneMatches.Count
	cloneMatchesProjectile = $sourceCloneMatches.Count -eq 1 -and
		$projectileCloneMatches.Count -eq 1 -and
		([regex]::Replace( $sourceCloneMatches[0].Value, '\s+', ' ' ).Trim() -eq
		[regex]::Replace( $projectileCloneMatches[0].Value, '\s+', ' ' ).Trim())
	aksEjectionNull = $aks -match '"EjectionPrefab"\s*:\s*null'
	aksWrong9mmAbsent = !$aks.Contains( 'prefabs/weapon_effects/9mm_casing.prefab' )
	activePrefabMappings = $activeCasings.Count
	activeEffectAssets = @($activeCasings.Values | Sort-Object -Unique).Count
	failures = @($failures)
}

$summary | ConvertTo-Json -Depth 4 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

Write-Output 'RESULT weapon_ejection=PASS property_count=1 clone_count=1 projectile_contract_match=1 active_prefab_mappings=6 active_effect_assets=3 aks_wrong_9mm=0 missing_effect_assets=0'
