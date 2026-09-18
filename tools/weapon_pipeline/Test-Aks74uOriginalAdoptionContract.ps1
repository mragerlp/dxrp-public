[CmdletBinding()]
param(
	[switch] $LicenseHoldOnly
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$assetRoot = Join-Path $projectRoot 'game\Assets\addons\lifepunch'
$codeRoot = Join-Path $projectRoot 'game\Code\Addons\lifepunch'
$sourceRelative = 'game/Assets/addons/lifepunch/lpweapons/aks74ucovert/source/aks-74u_extract/source/AKS74U Sketchfab.fbx'
$sourcePath = Join-Path $projectRoot ($sourceRelative -replace '/', '\')
$inspectPath = Join-Path $projectRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\provenance\inspect.json'
$provenancePath = Join-Path $projectRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\provenance\PROVENANCE.txt'
$expectedSourceHash = '2066A418F4C12C65C3D2D243A6981AFDB7A1B285C25082FBF72CFF145CC48B26'
$militaryWorld = 'addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab'
$militaryView = 'addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab'
$originalWorld = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab'
$originalView = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/vm_aks74u_original/vm_aks74u_original.prefab'

$checks = [System.Collections.Generic.List[object]]::new()

function Add-ContractCheck {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [bool] $Passed,
		[Parameter(Mandatory)] [string] $Evidence
	)

	$checks.Add([pscustomobject]@{
		name = $Name
		passed = $Passed
		evidence = $Evidence
	})
}

function Get-ProjectRelativePath {
	param([Parameter(Mandatory)] [string] $Path)

	return [System.IO.Path]::GetRelativePath($projectRoot, $Path).Replace('\', '/')
}

function Get-ProvenanceFieldValue {
	param(
		[Parameter(Mandatory)] [string] $Text,
		[Parameter(Mandatory)] [string] $Name
	)

	$fieldPattern = "(?m)^$([regex]::Escape($Name))=(?<value>[^\r\n]*)$"
	$fieldMatches = [regex]::Matches($Text, $fieldPattern)
	if ($fieldMatches.Count -ne 1) {
		return ''
	}

	return $fieldMatches[0].Groups['value'].Value.Trim()
}

function Test-AcquisitionLicenseEvidence {
	param([Parameter(Mandatory)] [string] $Text)

	$licenseStatus = Get-ProvenanceFieldValue -Text $Text -Name 'license_status'
	$licenseTier = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_license_tier'
	$tierEligibility = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_tier_eligibility'
	$licenseSnapshot = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_license_snapshot'
	$tierAndEligibilityAreCoherent =
		($licenseTier -ceq 'Personal' -and $tierEligibility -ceq 'VERIFIED') -or
		($licenseTier -ceq 'Professional' -and $tierEligibility -ceq 'NOT_APPLICABLE_BY_ACQUISITION_TERMS')

	return $licenseStatus -ceq 'VERIFIED' -and
		$tierAndEligibilityAreCoherent -and
		$licenseSnapshot -ceq 'VERIFIED'
}

function Get-CSharpMethodBody {
	param(
		[Parameter(Mandatory)] [string] $Text,
		[Parameter(Mandatory)] [string] $Signature
	)

	$signatureIndex = $Text.IndexOf($Signature, [System.StringComparison]::Ordinal)
	if ($signatureIndex -lt 0) { return '' }
	$openBrace = $Text.IndexOf('{', $signatureIndex)
	if ($openBrace -lt 0) { return '' }

	$depth = 0
	for ($index = $openBrace; $index -lt $Text.Length; $index++) {
		if ($Text[$index] -eq '{') { $depth++ }
		elseif ($Text[$index] -eq '}') {
			$depth--
			if ($depth -eq 0) {
				return $Text.Substring($openBrace + 1, $index - $openBrace - 1)
			}
		}
	}

	return ''
}

function Test-ForeignPathOwnershipGuardSource {
	param([Parameter(Mandatory)] [string] $Text)

	$body = Get-CSharpMethodBody -Text $Text -Signature 'private static bool HasConflictingContent()'
	$guardPattern = '(?s)GameModeAddonContents\.All\s*\.Any\s*\(\s*content\s*=>\s*content\.Id\s*!=\s*EditorContentId\s*&&\s*\(\s*string\.Equals\s*\(\s*content\.PrimaryReference\s*,\s*WorldPrefabPath\s*,\s*StringComparison\.OrdinalIgnoreCase\s*\)\s*\|\|\s*string\.Equals\s*\(\s*content\.SecondaryReference\s*,\s*ViewPrefabPath\s*,\s*StringComparison\.OrdinalIgnoreCase\s*\)\s*\)\s*\)'
	return $body -match $guardPattern
}

function Test-LicenseHoldGateSource {
	param([Parameter(Mandatory)] [string] $Text)

	if ($Text -notmatch '(?m)^\s*private const bool ProductSourceCleared = false;\s*$') {
		return $false
	}

	$body = Get-CSharpMethodBody -Text $Text -Signature 'public static GameModeEquipmentDto? Ensure()'
	if ([string]::IsNullOrWhiteSpace($body)) {
		return $false
	}

	$guardIndex = $body.IndexOf('if ( !ProductSourceCleared )', [System.StringComparison]::Ordinal)
	$worldPreflightIndex = $body.IndexOf('GameObject.GetPrefab( WorldPrefabPath )', [System.StringComparison]::Ordinal)
	$viewPreflightIndex = $body.IndexOf('GameObject.GetPrefab( ViewPrefabPath )', [System.StringComparison]::Ordinal)
	$contentMutationIndex = $body.IndexOf('GameModeAddonContents.EnsureEditorContent', [System.StringComparison]::Ordinal)
	$rowMutationIndex = $body.IndexOf('.Equipments.Add( row )', [System.StringComparison]::Ordinal)
	if ($guardIndex -lt 0 -or
		$worldPreflightIndex -le $guardIndex -or
		$viewPreflightIndex -le $guardIndex -or
		$contentMutationIndex -le $guardIndex -or
		$rowMutationIndex -le $guardIndex) {
		return $false
	}

	$guardSegment = $body.Substring($guardIndex, $worldPreflightIndex - $guardIndex)
	return $guardSegment.Contains('return null;', [System.StringComparison]::Ordinal) -and
		$guardSegment.Contains('Log.Warning', [System.StringComparison]::Ordinal) -and
		$guardSegment.Contains('license', [System.StringComparison]::OrdinalIgnoreCase)
}

$devRosterPath = Join-Path $codeRoot '_dev\Aks74uOriginalDevRoster.cs'
$devRosterText = if (Test-Path -LiteralPath $devRosterPath -PathType Leaf) { Get-Content -LiteralPath $devRosterPath -Raw } else { '' }
$licenseHoldFixture = @'
private const bool ProductSourceCleared = false;
public static GameModeEquipmentDto? Ensure()
{
	if ( !ProductSourceCleared )
	{
		Log.Warning( "license clearance is not pinned" );
		return null;
	}

	var worldPrefab = GameObject.GetPrefab( WorldPrefabPath );
	var viewPrefab = GameObject.GetPrefab( ViewPrefabPath );
	GameModeAddonContents.EnsureEditorContent();
	gameMode.Equipments.Add( row );
}
'@
$enabledLicenseFixture = $licenseHoldFixture.Replace(
	'private const bool ProductSourceCleared = false;',
	'private const bool ProductSourceCleared = true;')
$lateLicenseFixture = @'
private const bool ProductSourceCleared = false;
public static GameModeEquipmentDto? Ensure()
{
	var worldPrefab = GameObject.GetPrefab( WorldPrefabPath );
	if ( !ProductSourceCleared )
	{
		Log.Warning( "license clearance is not pinned" );
		return null;
	}

	var viewPrefab = GameObject.GetPrefab( ViewPrefabPath );
	GameModeAddonContents.EnsureEditorContent();
	gameMode.Equipments.Add( row );
}
'@
Add-ContractCheck 'positive_control_accepts_preflight_license_hold_gate' `
	(Test-LicenseHoldGateSource -Text $licenseHoldFixture) `
	'a false product-source gate returns before prefab access or editor roster mutation'
Add-ContractCheck 'negative_control_rejects_enabled_license_hold_gate' `
	(-not (Test-LicenseHoldGateSource -Text $enabledLicenseFixture)) `
	'the containment constant must remain false until acquisition evidence is pinned'
Add-ContractCheck 'negative_control_rejects_late_license_hold_gate' `
	(-not (Test-LicenseHoldGateSource -Text $lateLicenseFixture)) `
	'the license gate must run before any product-prefab preflight'
Add-ContractCheck 'original_editor_roster_requires_license_clearance_before_prefab_preflight_or_mutation' `
	(Test-LicenseHoldGateSource -Text $devRosterText) `
	'Aks74uOriginalDevRoster.Ensure must fail closed on an explicit false product-source gate before prefab access or in-memory roster mutation'

if ($LicenseHoldOnly) {
	$licenseHoldFailures = @($checks | Where-Object { -not $_.passed })
	[pscustomobject]@{
		contract = 'AKS-74U Original license hold'
		status = if ($licenseHoldFailures.Count -eq 0) { 'PASS' } else { 'FAIL' }
		checks = $checks
		failureCount = $licenseHoldFailures.Count
	} | ConvertTo-Json -Depth 6
	if ($licenseHoldFailures.Count -gt 0) { exit 1 }
	exit 0
}

$sourceExists = Test-Path -LiteralPath $sourcePath -PathType Leaf
Add-ContractCheck 'authoritative_source_exists' $sourceExists $sourceRelative

$sourceHash = if ($sourceExists) { (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash } else { '' }
Add-ContractCheck 'authoritative_source_hash' ($sourceHash -eq $expectedSourceHash) "expected=$expectedSourceHash actual=$sourceHash"

$inspect = if (Test-Path -LiteralPath $inspectPath -PathType Leaf) {
	Get-Content -LiteralPath $inspectPath -Raw | ConvertFrom-Json
} else {
	$null
}
$pbsSeparated = $null -ne $inspect -and
	$inspect.object_separation -eq $true -and
	@($inspect.mesh_names) -contains 'pbs04 suppressor'
Add-ContractCheck 'pbs_is_separate_source_mesh' $pbsSeparated 'inspect.json must report object_separation=true and mesh pbs04 suppressor'

$provenanceText = if (Test-Path -LiteralPath $provenancePath -PathType Leaf) {
	Get-Content -LiteralPath $provenancePath -Raw
} else {
	''
}
$licenseStatus = Get-ProvenanceFieldValue -Text $provenanceText -Name 'license_status'
$licenseTier = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_license_tier'
$tierEligibility = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_tier_eligibility'
$licenseSnapshot = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_license_snapshot'
$licenseVerified = Test-AcquisitionLicenseEvidence -Text $provenanceText
$provenanceVerified = $provenanceText -match '(?m)^provenance_status=VERIFIED\s*$'
Add-ContractCheck 'source_provenance_verified' $provenanceVerified 'PROVENANCE.txt must contain exact provenance_status=VERIFIED before product adoption'
Add-ContractCheck 'source_license_verified_before_product_adoption' $licenseVerified "license_status=$licenseStatus tier=$licenseTier eligibility=$tierEligibility snapshot=$licenseSnapshot; require coherent acquisition evidence before product adoption"

$statusOnlyFixture = @'
license_status=VERIFIED
acquisition_license_tier=UNVERIFIED
acquisition_tier_eligibility=UNVERIFIED
acquisition_license_snapshot=UNVERIFIED
'@
$personalFixture = @'
license_status=VERIFIED
acquisition_license_tier=Personal
acquisition_tier_eligibility=VERIFIED
acquisition_license_snapshot=VERIFIED
'@
$professionalFixture = @'
license_status=VERIFIED
acquisition_license_tier=Professional
acquisition_tier_eligibility=NOT_APPLICABLE_BY_ACQUISITION_TERMS
acquisition_license_snapshot=VERIFIED
'@
$referenceOnlyFixture = @'
license_status=VERIFIED
acquisition_license_tier=Reference-Only
acquisition_tier_eligibility=VERIFIED
acquisition_license_snapshot=VERIFIED
'@
$personalWithProfessionalEligibilityFixture = @'
license_status=VERIFIED
acquisition_license_tier=Personal
acquisition_tier_eligibility=NOT_APPLICABLE_BY_ACQUISITION_TERMS
acquisition_license_snapshot=VERIFIED
'@
Add-ContractCheck 'license_gate_rejects_status_only_fixture' (-not (Test-AcquisitionLicenseEvidence -Text $statusOnlyFixture)) 'A bare license_status flip must not clear missing acquisition evidence'
Add-ContractCheck 'license_gate_accepts_coherent_personal_fixture' (Test-AcquisitionLicenseEvidence -Text $personalFixture) 'Personal requires verified transaction-time eligibility and license snapshot'
Add-ContractCheck 'license_gate_accepts_coherent_professional_fixture' (Test-AcquisitionLicenseEvidence -Text $professionalFixture) 'Professional requires explicit terms-based eligibility non-applicability and license snapshot'
Add-ContractCheck 'license_gate_rejects_reference_only_fixture' (-not (Test-AcquisitionLicenseEvidence -Text $referenceOnlyFixture)) 'Reference-Only cannot adopt retained source FBX product bytes'
Add-ContractCheck 'license_gate_rejects_personal_with_professional_eligibility_fixture' (-not (Test-AcquisitionLicenseEvidence -Text $personalWithProfessionalEligibilityFixture)) 'Personal cannot reuse Professional eligibility non-applicability'

$worldCandidates = @(
	Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Filter 'w_aks74u_original.prefab' -ErrorAction SilentlyContinue
)
$viewCandidates = @(
	Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Filter 'vm_aks74u_original.prefab' -ErrorAction SilentlyContinue
)

$worldCandidatePaths = @($worldCandidates | ForEach-Object { (Get-ProjectRelativePath $_.FullName) -replace '^game/Assets/', '' })
$viewCandidatePaths = @($viewCandidates | ForEach-Object { (Get-ProjectRelativePath $_.FullName) -replace '^game/Assets/', '' })
Add-ContractCheck 'one_original_world_prefab_at_exact_path' `
	($worldCandidates.Count -eq 1 -and $worldCandidatePaths[0] -eq $originalWorld) `
	"expected=$originalWorld count=$($worldCandidates.Count) paths=$($worldCandidatePaths -join ',')"
Add-ContractCheck 'one_original_view_prefab_at_exact_path' `
	($viewCandidates.Count -eq 1 -and $viewCandidatePaths[0] -eq $originalView) `
	"expected=$originalView count=$($viewCandidates.Count) paths=$($viewCandidatePaths -join ',')"

$worldAssetPath = if ($worldCandidates.Count -eq 1 -and $worldCandidatePaths[0] -eq $originalWorld) {
	$originalWorld
} else { '' }
$viewAssetPath = if ($viewCandidates.Count -eq 1 -and $viewCandidatePaths[0] -eq $originalView) {
	$originalView
} else { '' }

$pathsAreDistinct = $worldAssetPath -ne '' -and
	$viewAssetPath -ne '' -and
	$worldAssetPath -ne $viewAssetPath -and
	$worldAssetPath -ne $militaryWorld -and
	$viewAssetPath -ne $militaryView
Add-ContractCheck 'original_paths_are_distinct_from_military' $pathsAreDistinct "world=$worldAssetPath view=$viewAssetPath"

$candidateAssetFiles = @(
	Get-ChildItem -LiteralPath $assetRoot -Recurse -File -ErrorAction SilentlyContinue |
		Where-Object {
			$relative = Get-ProjectRelativePath $_.FullName
			$relative -match '(?i)aks74u[_-]?original|aks74ucovert' -and
			$relative -notmatch '(?i)/(source|provenance|weaponanim_preview_cache)/' -and
			$_.Extension -in @('.prefab', '.vmdl', '.vmat')
		}
)
$candidateText = ($candidateAssetFiles | ForEach-Object {
	try { Get-Content -LiteralPath $_.FullName -Raw -ErrorAction Stop } catch { '' }
}) -join "`n"
$usesRejectedSecondary = $candidateText -match '(?i)aks74u_2\.fbx'
Add-ContractCheck 'original_product_rejects_military_secondary_model_nonvacuously' `
	($candidateAssetFiles.Count -gt 0 -and -not $usesRejectedSecondary) `
	"productFileCount=$($candidateAssetFiles.Count); product assets must exist and must not reference aks74u_2.fbx"

$devHelper = Join-Path $codeRoot '_dev\Aks74uOriginalDevGive.cs'
$devText = if (Test-Path -LiteralPath $devHelper -PathType Leaf) { Get-Content -LiteralPath $devHelper -Raw } else { '' }
$helperMatches = $devText -match 'lp_give_aks74u_original' -and
	$devText.Contains('private const string WorldPrefabPath = Aks74uOriginalDevRoster.WorldPrefabPath') -and
	$devText.Contains('private const string ViewPrefabPath = Aks74uOriginalDevRoster.ViewPrefabPath')
Add-ContractCheck 'distinct_original_dev_spawn_contract' $helperMatches 'Aks74uOriginalDevGive.cs must bind lp_give_aks74u_original to the isolated roster canonical world/view paths'

$helperFailsClosed = $helperMatches -and
	$devText.Contains('Aks74uOriginalDevRoster.Ensure()') -and
	$devText.Contains('Aks74uOriginalDevRoster.WorldPrefabPath') -and
	$devText.Contains('Aks74uOriginalDevRoster.ViewPrefabPath') -and
	$devText.Contains('GameObject.GetPrefab( WorldPrefabPath )') -and
	$devText.Contains('GameObject.GetPrefab( ViewPrefabPath )') -and
	$devText.Contains('viewPrefab.Components.Get<ViewModel>()') -and
	$devText.Contains('SecondaryPrefabPath()') -and
	$devText.Contains('resource.GameModeAddonContentId')
Add-ContractCheck 'original_dev_spawn_fails_closed_before_clone' $helperFailsClosed 'Original helper must resolve the exact isolated roster row and both prefabs, including a direct-root ViewModel, before cloning'

$ensureIndex = $devText.IndexOf('Aks74uOriginalDevRoster.Ensure()', [System.StringComparison]::Ordinal)
$worldPreflightIndex = $devText.IndexOf('GameObject.GetPrefab( WorldPrefabPath )', [System.StringComparison]::Ordinal)
$viewPreflightIndex = $devText.IndexOf('GameObject.GetPrefab( ViewPrefabPath )', [System.StringComparison]::Ordinal)
$cloneIndex = $devText.IndexOf('prefab.Clone', [System.StringComparison]::Ordinal)
$equipmentIndex = $devText.IndexOf('Components.Get<Equipment>', [System.StringComparison]::Ordinal)
$spawnIndex = $devText.IndexOf('go.NetworkSpawn', [System.StringComparison]::Ordinal)
$cleanupIndex = $devText.IndexOf('RemoveExisting', [System.StringComparison]::Ordinal)
$helperCleanupIsOriginalOnly = $ensureIndex -ge 0 -and
	$worldPreflightIndex -gt $ensureIndex -and
	$viewPreflightIndex -gt $worldPreflightIndex -and
	$cloneIndex -gt $viewPreflightIndex -and
	$equipmentIndex -gt $cloneIndex -and
	$spawnIndex -gt $equipmentIndex -and
	$cleanupIndex -gt $cloneIndex -and
	$cleanupIndex -gt $spawnIndex -and
	$devText.Contains('"w_aks74u_original"') -and
	-not $devText.Contains('GameObject.Name, "w_aks74u",')
Add-ContractCheck 'original_cleanup_cannot_remove_military_variant' $helperCleanupIsOriginalOnly 'Lifecycle must be ensure, both preflights, clone, validate, spawn, then Original-only cleanup; never w_aks74u'

$foreignPathGuardFixture = @'
private static bool HasConflictingContent()
{
	return GameModeAddonContents.All.Any( content =>
		content.Id != EditorContentId &&
		(string.Equals( content.PrimaryReference, WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) ||
		 string.Equals( content.SecondaryReference, ViewPrefabPath, StringComparison.OrdinalIgnoreCase )) );
}
'@
$sameIdOnlyGuardFixture = $foreignPathGuardFixture.Replace('content.Id != EditorContentId', 'content.Id == EditorContentId')
$fullPairOnlyGuardFixture = $foreignPathGuardFixture.Replace(') ||', ') &&')
Add-ContractCheck 'positive_control_accepts_foreign_path_ownership_guard' `
	(Test-ForeignPathOwnershipGuardSource -Text $foreignPathGuardFixture) `
	'foreign content ID owning either canonical path is rejected'
Add-ContractCheck 'negative_control_rejects_same_id_only_path_guard' `
	(-not (Test-ForeignPathOwnershipGuardSource -Text $sameIdOnlyGuardFixture)) `
	'checking only the synthetic ID does not protect path ownership'
Add-ContractCheck 'negative_control_rejects_full_pair_only_path_guard' `
	(-not (Test-ForeignPathOwnershipGuardSource -Text $fullPairOnlyGuardFixture)) `
	'either canonical path is identity-bearing and must conflict independently'
Add-ContractCheck 'original_editor_roster_rejects_foreign_content_path_ownership' `
	(Test-ForeignPathOwnershipGuardSource -Text $devRosterText) `
	'HasConflictingContent must reject a different content ID that already owns either canonical Original prefab path'
$isolatedRosterContract = $devRosterText.Contains('public static class Aks74uOriginalDevRoster') -and
	$devRosterText.Contains('public static readonly Guid EditorContentId') -and
	$devRosterText.Contains('public static readonly Guid EditorEquipmentRowId') -and
	$devRosterText.Contains('public const string WorldPrefabPath') -and
	$devRosterText.Contains('public const string ViewPrefabPath') -and
	$devRosterText.Contains($originalWorld) -and
	$devRosterText.Contains($originalView) -and
	$devRosterText.Contains('public static GameModeEquipmentDto? Ensure()') -and
	$devRosterText.Contains('Application.IsEditor') -and
	$devRosterText.Contains('Networking.IsHost') -and
	$devRosterText.Contains('GameObject.GetPrefab( WorldPrefabPath )') -and
	$devRosterText.Contains('GameObject.GetPrefab( ViewPrefabPath )') -and
	$devRosterText.Contains('viewPrefab.Components.Get<ViewModel>()') -and
	$devRosterText.Contains('GameModeAddonContents.EnsureEditorContent') -and
	$devRosterText.Contains('ResolveExactPathRow') -and
	$devRosterText.Contains('.Equipments.Add( row )')
Add-ContractCheck 'isolated_original_editor_roster_contract' $isolatedRosterContract 'Aks74uOriginalDevRoster.cs must own stable editor-only identities, exact paths, asset preflight, and fail-closed row resolution'

$guidMatches = [regex]::Matches($devRosterText, '(?i)new\(\s*"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})"\s*\)')
$stableGuidsAreDistinct = $guidMatches.Count -ge 2 -and
	$guidMatches[0].Groups[1].Value -ne '00000000-0000-0000-0000-000000000000' -and
	$guidMatches[1].Groups[1].Value -ne '00000000-0000-0000-0000-000000000000' -and
	$guidMatches[0].Groups[1].Value -ne $guidMatches[1].Groups[1].Value
Add-ContractCheck 'original_editor_ids_are_stable_distinct_and_nonempty' $stableGuidsAreDistinct 'Original editor content and equipment-row GUIDs must be explicit, nonempty, and distinct'

$rosterWorldPreflightIndex = $devRosterText.IndexOf('GameObject.GetPrefab( WorldPrefabPath )', [System.StringComparison]::Ordinal)
$rosterViewPreflightIndex = $devRosterText.IndexOf('GameObject.GetPrefab( ViewPrefabPath )', [System.StringComparison]::Ordinal)
$resolveIndex = $devRosterText.IndexOf('var exactRow = ResolveExactPathRow()', [System.StringComparison]::Ordinal)
$conflictIndex = $devRosterText.IndexOf('HasConflictingRows', [System.StringComparison]::Ordinal)
$contentIndex = $devRosterText.IndexOf('GameModeAddonContents.EnsureEditorContent', [System.StringComparison]::Ordinal)
$addIndex = $devRosterText.IndexOf('.Equipments.Add( row )', [System.StringComparison]::Ordinal)
$rosterMutationOrder = $rosterWorldPreflightIndex -ge 0 -and
	$rosterViewPreflightIndex -gt $rosterWorldPreflightIndex -and
	$resolveIndex -gt $rosterViewPreflightIndex -and
	$conflictIndex -gt $resolveIndex -and
	$contentIndex -gt $conflictIndex -and
	$addIndex -gt $contentIndex
Add-ContractCheck 'original_editor_roster_preflights_before_mutation' $rosterMutationOrder 'Both assets, direct-root ViewModel, exact live row, and conflicts must be checked before editor content or equipment mutation'

$rosterInjectIsNonDestructive = $isolatedRosterContract -and
	-not $devRosterText.Contains('.RemoveAll(') -and
	-not $devRosterText.Contains('.RemoveAt(') -and
	-not $devRosterText.Contains('.Clear(') -and
	-not $devRosterText.Contains('GameModeMarketItems') -and
	$devRosterText.Contains('HasConflictingRows') -and
	$devRosterText.Contains('return null;')
Add-ContractCheck 'original_editor_roster_rejects_conflicts_without_cleanup' $rosterInjectIsNonDestructive 'Original roster injection must reject conflicting row/content/path identities and must not delete inherited rows'

$rosterPath = Join-Path $codeRoot '_dev\WeaponRosterStatusDev.cs'
$rosterText = if (Test-Path -LiteralPath $rosterPath -PathType Leaf) { Get-Content -LiteralPath $rosterPath -Raw } else { '' }
$rosterMatches = $rosterText -match 'AKS-74U Original' -and
	$rosterText.Contains($originalWorld) -and
	$rosterText.Contains($originalView) -and
	$rosterText.Contains('Aks74uOriginalDevRoster.EditorContentId')
Add-ContractCheck 'original_has_distinct_roster_row' $rosterMatches 'WeaponRosterStatusDev.cs must resolve AKS-74U Original with its own world/view paths'

$rosterLabelsEditorIdentityWithoutGrantingMarket = $rosterMatches -and
	$rosterText.Contains('Guid? EditorContentId') -and
	$rosterText.Contains('editorInjected') -and
	$rosterText.Contains('readyForMarket = readyForEquip && !editorInjected && spawnableRows > 0') -and
	-not $rosterText.Contains('Aks74uOriginalDevRoster.Ensure()')
Add-ContractCheck 'original_roster_reports_editor_identity_without_market_readiness' $rosterLabelsEditorIdentityWithoutGrantingMarket 'Roster census must label the editor-only identity separately and still require a real spawnable market row'

$failures = @($checks | Where-Object { -not $_.passed })
$result = [pscustomobject]@{
	contract = 'AKS-74U Original adoption'
	status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
	checks = $checks
	failureCount = $failures.Count
}

$result | ConvertTo-Json -Depth 6
if ($failures.Count -gt 0) { exit 1 }
exit 0
