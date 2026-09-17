[CmdletBinding()]
param(
	[int] $ExpectedWiredPrefabs = 0,
	[string] $ExpectedAk47ExistingIkSha256 =
		'8F52CE80F41E51F8B4CF86C2575E350425C78E6D700A5224063BAA6EBFAC5B58'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $repoRoot 'game\Code\Equipment\Weapon\WorldWeaponLeftHandIk.cs'
$assetsRoot = Join-Path $repoRoot 'game\Assets'
$failures = [System.Collections.Generic.List[string]]::new()

if ( !(Test-Path -LiteralPath $sourcePath -PathType Leaf) )
{
	$failures.Add( 'Missing shared WorldWeaponLeftHandIk source.' )
	$source = ''
}
else
{
	$source = Get-Content -LiteralPath $sourcePath -Raw
}

function Require-Count( [string] $Pattern, [int] $Expected, [string] $Failure )
{
	$count = [regex]::Matches( $source, $Pattern ).Count
	if ( $count -ne $Expected )
	{
		$failures.Add( "$Failure Found $count; expected $Expected." )
	}
}

Require-Count 'public required Equipment Equipment \{ get; set; \}' 1 `
	'The shared IK component must require exactly one same-object Equipment.'
Require-Count '\[RequireComponent\]' 1 `
	'The shared IK component Equipment property must carry RequireComponent.'
Require-Count 'public GameObject\? LeftGrip \{ get; set; \}' 1 `
	'The shared IK component must expose exactly one per-weapon LeftGrip.'
Require-Count 'helper\.IkLeftHand\s*=\s*LeftGrip\s*;' 1 `
	'The shared IK component must assign only its measured left target.'
Require-Count 'helper\.SetIk\(\s*"hand_left"\s*,\s*LeftGrip\.Transform\.World\s*\)' 1 `
	'The shared IK component must refresh the settled world transform.'
Require-Count 'owner\.CurrentEquipment\s*!=\s*Equipment\s*\|\|\s*owner\.IsTyping\s*\|\|' 1 `
	'The shared IK component must release its grip so typing can claim the slot.'
Require-Count 'helper\.IkLeftHand\.IsValid\(\)\s*&&\s*helper\.IkLeftHand\.Active\s*&&\s*helper\.IkLeftHand\s*!=\s*LeftGrip' 1 `
	'The shared IK component must not yield forever to an inactive stale target.'
Require-Count 'IkRightHand' 0 `
	'The shared IK component must never read, assign, or clear the right-hand slot.'
Require-Count 'hand_right' 0 `
	'The shared IK component must never drive the right hand through SetIk.'
Require-Count 'private GameObject\? _assignedGrip;' 1 `
	'The shared IK component must cache the exact grip target it owns.'
Require-Count '_assignedGrip\s*=\s*LeftGrip\s*;' 1 `
	'The shared IK component must cache the grip after assigning it.'
Require-Count '_assignedHelper\.IkLeftHand\s*==\s*_assignedGrip' 1 `
	'The shared IK component must release by the cached target, not a rebound property.'
Require-Count '_assignedGrip\s*=\s*null\s*;' 1 `
	'The shared IK component must clear cached target ownership.'
Require-Count 'protected override void OnDisabled\(\)' 1 `
	'The shared IK component must release its target when disabled.'
Require-Count 'protected override void OnDestroy\(\)' 1 `
	'The shared IK component must release its target when destroyed.'

$wiredPrefabs = @(
	if ( Test-Path -LiteralPath $assetsRoot -PathType Container )
	{
		Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.prefab' |
			Where-Object {
				Select-String -LiteralPath $_.FullName -SimpleMatch `
					'"__type": "Dxura.RP.Game.WorldWeaponLeftHandIk"' -Quiet
			} |
			ForEach-Object {
				$_.FullName.Substring( $repoRoot.Length + 1 ).Replace( '\', '/' )
			}
	}
)
if ( $wiredPrefabs.Count -ne $ExpectedWiredPrefabs )
{
	$failures.Add( "Shared IK is wired into $($wiredPrefabs.Count) prefabs; expected $ExpectedWiredPrefabs." )
}

$akPath = Join-Path $repoRoot 'game\Code\Addons\lifepunch\ak47\Ak47HoldIk.cs'
$akHash = if ( Test-Path -LiteralPath $akPath -PathType Leaf )
{
	(Get-FileHash -LiteralPath $akPath -Algorithm SHA256).Hash
}
else
{
	$failures.Add( 'Existing AK-47 IK source is missing.' )
	$null
}
if ( $null -ne $akHash -and $akHash -ne $ExpectedAk47ExistingIkSha256 )
{
	$failures.Add( "Existing AK-47 IK source changed from the pinned baseline: $akHash." )
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	sharedSourceSha256 = if ( Test-Path -LiteralPath $sourcePath -PathType Leaf ) {
		(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
	} else { $null }
	wiredPrefabs = $wiredPrefabs
	ak47ExistingIkSha256 = $akHash
	failures = @($failures)
}

$summary | ConvertTo-Json -Depth 5 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

Write-Output ((
	'RESULT weapon_ik=PASS shared_component=1 left_assignments=1 ' +
	'right_assignments=0 cached_target_ownership=1 wired_prefabs={0} ' +
	'ak47_existing_ik_preserved=1'
) -f $wiredPrefabs.Count)
