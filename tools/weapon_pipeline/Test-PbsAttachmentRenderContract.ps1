[CmdletBinding()]
param(
	[switch] $Ak47Only
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assetsRoot = Join-Path $repositoryRoot 'game\Assets'
$checks = [System.Collections.Generic.List[object]]::new()

$pbsModelAsset = 'addons/lifepunch/lpweapons/attachments/pbs04/pbs04_suppressor.vmdl'
$pbsMaterialAsset = 'addons/lifepunch/lpweapons/attachments/pbs04/pbs04_suppressor.vmat'
$ak47WorldAsset = 'addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab'
$ak47ViewAsset = 'addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab'
$originalWorldAsset = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab'
$originalViewAsset = 'addons/lifepunch/lpweapons/aks74ucovert/equipment/vm_aks74u_original/vm_aks74u_original.prefab'
$militaryWorldAsset = 'addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab'
$militaryViewAsset = 'addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab'
$provenancePath = Join-Path $assetsRoot 'addons\lifepunch\lpweapons\aks74ucovert\provenance\PROVENANCE.txt'

$stateType = 'LifePunch.DXRP.Addons.Weapons.Attachments.WeaponAttachmentState'
$controllerType = 'LifePunch.DXRP.Addons.Weapons.Attachments.WeaponAttachmentController'
$renderControllerType = 'LifePunch.DXRP.Addons.Weapons.Attachments.WeaponAttachmentRenderController'
$equipmentType = 'Dxura.RP.Game.Equipment'
$viewModelType = 'Dxura.RP.Game.ViewModel'
$modelRendererType = 'Sandbox.ModelRenderer'

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

function Get-AssetDiskPath {
	param([Parameter(Mandatory)] [string] $AssetPath)

	return Join-Path $assetsRoot ($AssetPath -replace '/', '\')
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

function Test-PbsPublicationEvidence {
	param([Parameter(Mandatory)] [string] $Text)

	$provenanceStatus = Get-ProvenanceFieldValue -Text $Text -Name 'provenance_status'
	$licenseStatus = Get-ProvenanceFieldValue -Text $Text -Name 'license_status'
	$licenseTier = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_license_tier'
	$tierEligibility = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_tier_eligibility'
	$licenseSnapshot = Get-ProvenanceFieldValue -Text $Text -Name 'acquisition_license_snapshot'
	$tierAndEligibilityAreCoherent =
		($licenseTier -ceq 'Personal' -and $tierEligibility -ceq 'VERIFIED') -or
		($licenseTier -ceq 'Professional' -and $tierEligibility -ceq 'NOT_APPLICABLE_BY_ACQUISITION_TERMS')

	return $provenanceStatus -ceq 'VERIFIED' -and
		$licenseStatus -ceq 'VERIFIED' -and
		$tierAndEligibilityAreCoherent -and
		$licenseSnapshot -ceq 'VERIFIED'
}

function Get-JsonProperty {
	param(
		[AllowNull()] $Object,
		[Parameter(Mandatory)] [string] $Name
	)

	if ($null -eq $Object) {
		return $null
	}

	$property = $Object.PSObject.Properties[$Name]
	if ($null -eq $property) {
		return $null
	}

	return $property.Value
}

function Get-PrefabDocument {
	param([Parameter(Mandatory)] [string] $AssetPath)

	$diskPath = Get-AssetDiskPath $AssetPath
	if (-not (Test-Path -LiteralPath $diskPath -PathType Leaf)) {
		return $null
	}

	try {
		return Get-Content -LiteralPath $diskPath -Raw | ConvertFrom-Json
	} catch {
		return $null
	}
}

function Get-AllNodes {
	param([AllowNull()] $Node)

	if ($null -eq $Node) {
		return
	}

	Write-Output $Node
	foreach ($child in @(Get-JsonProperty $Node 'Children')) {
		Get-AllNodes $child
	}
}

function Get-AllComponents {
	param([AllowNull()] $Root)

	foreach ($node in @(Get-AllNodes $Root)) {
		foreach ($component in @(Get-JsonProperty $node 'Components')) {
			Write-Output $component
		}
	}
}

function Get-ComponentsByType {
	param(
		[AllowNull()] $Node,
		[Parameter(Mandatory)] [string] $Type
	)

	return @(Get-JsonProperty $Node 'Components' | Where-Object {
		(Get-JsonProperty $_ '__type') -eq $Type
	})
}

function Get-NodeByGuid {
	param(
		[AllowNull()] $Root,
		[AllowNull()] [string] $Guid
	)

	if ([string]::IsNullOrWhiteSpace($Guid)) {
		return $null
	}

	return @(Get-AllNodes $Root | Where-Object {
		(Get-JsonProperty $_ '__guid') -eq $Guid
	}) | Select-Object -First 1
}

function Get-DirectChildByName {
	param(
		[AllowNull()] $Node,
		[Parameter(Mandatory)] [string] $Name
	)

	return @(Get-JsonProperty $Node 'Children' | Where-Object {
		(Get-JsonProperty $_ 'Name') -eq $Name
	}) | Select-Object -First 1
}

function Test-ComponentReference {
	param(
		[AllowNull()] $Reference,
		[AllowNull()] $Component,
		[AllowNull()] $OwnerNode
	)

	return $null -ne $Reference -and
		$null -ne $Component -and
		$null -ne $OwnerNode -and
		(Get-JsonProperty $Reference '_type') -eq 'component' -and
		(Get-JsonProperty $Reference 'component_id') -eq (Get-JsonProperty $Component '__guid') -and
		(Get-JsonProperty $Reference 'go') -eq (Get-JsonProperty $OwnerNode '__guid')
}

function Test-Perspective {
	param(
		[AllowNull()] $Actual,
		[Parameter(Mandatory)] [string] $ExpectedName,
		[Parameter(Mandatory)] [int] $ExpectedValue
	)

	return "$Actual" -eq $ExpectedName -or "$Actual" -eq "$ExpectedValue"
}

function Test-OtherRendererReferencesEmpty {
	param([AllowNull()] $RenderController)

	return $null -eq (Get-JsonProperty $RenderController 'PistolSuppressorRenderer') -and
		$null -eq (Get-JsonProperty $RenderController 'RedDotRenderer') -and
		$null -eq (Get-JsonProperty $RenderController 'LaserBodyRenderer') -and
		$null -eq (Get-JsonProperty $RenderController 'LaserBeamRenderer')
}

function Test-MuzzlePin {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [string] $AssetPath,
		[Parameter(Mandatory)] [string] $OwnerType,
		[Parameter(Mandatory)] [string] $ExpectedGuid,
		[Parameter(Mandatory)] [string] $ExpectedPosition,
		[Parameter(Mandatory)] [string] $ExpectedRotation,
		[Parameter(Mandatory)] [string] $ExpectedScale
	)

	$document = Get-PrefabDocument $AssetPath
	$root = Get-JsonProperty $document 'RootObject'
	$owner = @(Get-ComponentsByType $root $OwnerType) | Select-Object -First 1
	$muzzleReference = Get-JsonProperty $owner 'Muzzle'
	$muzzleGuid = Get-JsonProperty $muzzleReference 'go'
	$muzzle = Get-NodeByGuid $root $muzzleGuid
	$passed = $muzzleGuid -eq $ExpectedGuid -and
		(Get-JsonProperty $muzzle 'Position') -eq $ExpectedPosition -and
		(Get-JsonProperty $muzzle 'Rotation') -eq $ExpectedRotation -and
		(Get-JsonProperty $muzzle 'Scale') -eq $ExpectedScale
	Add-ContractCheck $Name $passed "asset=$AssetPath guid=$muzzleGuid position=$(Get-JsonProperty $muzzle 'Position') rotation=$(Get-JsonProperty $muzzle 'Rotation') scale=$(Get-JsonProperty $muzzle 'Scale')"
}

function Test-WorldAttachmentContract {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [string] $AssetPath,
		[Parameter(Mandatory)] [string] $WeaponId
	)

	$document = Get-PrefabDocument $AssetPath
	$root = Get-JsonProperty $document 'RootObject'
	$equipment = @(Get-ComponentsByType $root $equipmentType)
	$state = @(Get-ComponentsByType $root $stateType)
	$controller = @(Get-ComponentsByType $root $controllerType)
	$rootWiring = $equipment.Count -eq 1 -and $state.Count -eq 1 -and $controller.Count -eq 1
	Add-ContractCheck "${Name}_world_root_state_and_controller" $rootWiring "asset=$AssetPath equipment=$($equipment.Count) state=$($state.Count) controller=$($controller.Count)"

	$controllerValue = $controller | Select-Object -First 1
	$equipmentValue = $equipment | Select-Object -First 1
	$stateValue = $state | Select-Object -First 1
	$controllerWiring = $rootWiring -and
		(Get-JsonProperty $controllerValue 'WeaponId') -eq $WeaponId -and
		(Get-JsonProperty $controllerValue 'InventoryTransactionsEnabled') -eq $false -and
		(Test-ComponentReference (Get-JsonProperty $controllerValue 'Equipment') $equipmentValue $root) -and
		(Test-ComponentReference (Get-JsonProperty $controllerValue 'AttachmentState') $stateValue $root)
	Add-ContractCheck "${Name}_world_controller_is_fail_closed_and_self_bound" $controllerWiring "weaponId=$(Get-JsonProperty $controllerValue 'WeaponId') inventoryTransactionsEnabled=$(Get-JsonProperty $controllerValue 'InventoryTransactionsEnabled')"

	$legacyStateKeys = @(
		'MuzzleKind',
		'MuzzleItemId',
		'OpticKind',
		'OpticItemId',
		'RailKind',
		'RailItemId',
		'LaserEnabled'
	)
	$statePropertyNames = if ($null -eq $stateValue) { @() } else { @($stateValue.PSObject.Properties.Name) }
	$authoredLegacyStateKeys = @($legacyStateKeys | Where-Object { $statePropertyNames -contains $_ })
	$authoredAtomicLoadout = $statePropertyNames -contains 'ReplicatedLoadout'
	$stateUsesAtomicDefault = $state.Count -eq 1 -and
		$authoredLegacyStateKeys.Count -eq 0 -and
		-not $authoredAtomicLoadout
	Add-ContractCheck "${Name}_world_attachment_state_uses_implicit_atomic_empty_default" $stateUsesAtomicDefault "legacyKeys=$($authoredLegacyStateKeys -join ',') replicatedLoadoutAuthored=$authoredAtomicLoadout"

	$muzzleReference = Get-JsonProperty $equipmentValue 'Muzzle'
	$muzzleGuid = Get-JsonProperty $muzzleReference 'go'
	$muzzleNode = Get-NodeByGuid $root $muzzleGuid
	$socket = Get-DirectChildByName $muzzleNode 'attachment_socket_muzzle'
	$renderer = @(Get-ComponentsByType $socket $modelRendererType)
	$renderController = @(Get-ComponentsByType $socket $renderControllerType)
	$socketWiring = $null -ne $muzzleNode -and
		$null -ne $socket -and
		(Get-JsonProperty $socket 'Position') -eq '0,0,0' -and
		(Get-JsonProperty $socket 'Rotation') -eq '0,0,0,1' -and
		(Get-JsonProperty $socket 'Scale') -eq '1,1,1' -and
		(Get-JsonProperty $socket 'NetworkMode') -eq 2 -and
		$renderer.Count -eq 1 -and
		$renderController.Count -eq 1
	Add-ContractCheck "${Name}_world_has_one_identity_muzzle_child_renderer_and_binding" $socketWiring "asset=$AssetPath muzzleGuid=$muzzleGuid socketGuid=$(Get-JsonProperty $socket '__guid') position=$(Get-JsonProperty $socket 'Position') rotation=$(Get-JsonProperty $socket 'Rotation') scale=$(Get-JsonProperty $socket 'Scale') networkMode=$(Get-JsonProperty $socket 'NetworkMode') renderer=$($renderer.Count) renderController=$($renderController.Count)"

	$rendererValue = $renderer | Select-Object -First 1
	$renderControllerValue = $renderController | Select-Object -First 1
	$rendererWiring = $socketWiring -and
		(Get-JsonProperty $rendererValue 'Model') -eq $pbsModelAsset -and
		(Get-JsonProperty $rendererValue '__enabled') -eq $false -and
		(Test-Perspective (Get-JsonProperty $renderControllerValue 'Perspective') 'ThirdPerson' 1) -and
		(Test-ComponentReference (Get-JsonProperty $renderControllerValue 'PbsSuppressorRenderer') $rendererValue $socket) -and
		(Test-OtherRendererReferencesEmpty $renderControllerValue)
	Add-ContractCheck "${Name}_world_pbs_renderer_is_default_hidden_and_third_person" $rendererWiring "model=$(Get-JsonProperty $rendererValue 'Model') rendererEnabled=$(Get-JsonProperty $rendererValue '__enabled') perspective=$(Get-JsonProperty $renderControllerValue 'Perspective')"
}

function Test-ViewAttachmentContract {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [string] $AssetPath
	)

	$document = Get-PrefabDocument $AssetPath
	$root = Get-JsonProperty $document 'RootObject'
	$viewModels = @(Get-ComponentsByType $root $viewModelType)
	$allComponents = @(Get-AllComponents $root)
	$worldStateCount = @($allComponents | Where-Object {
		(Get-JsonProperty $_ '__type') -eq $stateType -or (Get-JsonProperty $_ '__type') -eq $controllerType
	}).Count
	$viewRootValid = $viewModels.Count -eq 1 -and $worldStateCount -eq 0
	Add-ContractCheck "${Name}_view_has_direct_viewmodel_and_no_local_world_state" $viewRootValid "asset=$AssetPath viewModels=$($viewModels.Count) worldStateOrController=$worldStateCount"

	$viewModel = $viewModels | Select-Object -First 1
	$additionalRootReference = Get-JsonProperty $viewModel 'AdditionalRendererRoot'
	$additionalRootGuid = Get-JsonProperty $additionalRootReference 'go'
	$additionalRoot = Get-NodeByGuid $root $additionalRootGuid
	$muzzleReference = Get-JsonProperty $viewModel 'Muzzle'
	$muzzleGuid = Get-JsonProperty $muzzleReference 'go'
	$muzzleNode = Get-NodeByGuid $root $muzzleGuid
	$muzzleIsUnderAdditionalRoot = $null -ne $additionalRoot -and @(
		Get-AllNodes $additionalRoot | Where-Object {
			(Get-JsonProperty $_ '__guid') -eq $muzzleGuid
		}
	).Count -eq 1
	$socket = Get-DirectChildByName $muzzleNode 'attachment_socket_muzzle'
	$renderer = @(Get-ComponentsByType $socket $modelRendererType)
	$renderController = @(Get-ComponentsByType $socket $renderControllerType)
	$socketWiring = $muzzleIsUnderAdditionalRoot -and
		$null -ne $socket -and
		(Get-JsonProperty $socket 'Position') -eq '0,0,0' -and
		(Get-JsonProperty $socket 'Rotation') -eq '0,0,0,1' -and
		(Get-JsonProperty $socket 'Scale') -eq '1,1,1' -and
		(Get-JsonProperty $socket 'NetworkMode') -eq 2 -and
		$renderer.Count -eq 1 -and
		$renderController.Count -eq 1
	Add-ContractCheck "${Name}_view_socket_is_identity_child_of_muzzle_under_additional_renderer_root" $socketWiring "asset=$AssetPath additionalRootGuid=$additionalRootGuid muzzleGuid=$muzzleGuid muzzleUnderRoot=$muzzleIsUnderAdditionalRoot socketGuid=$(Get-JsonProperty $socket '__guid') position=$(Get-JsonProperty $socket 'Position') rotation=$(Get-JsonProperty $socket 'Rotation') scale=$(Get-JsonProperty $socket 'Scale') networkMode=$(Get-JsonProperty $socket 'NetworkMode') renderer=$($renderer.Count) renderController=$($renderController.Count)"

	$rendererValue = $renderer | Select-Object -First 1
	$renderControllerValue = $renderController | Select-Object -First 1
	$rendererWiring = $socketWiring -and
		(Get-JsonProperty $rendererValue 'Model') -eq $pbsModelAsset -and
		(Get-JsonProperty $rendererValue '__enabled') -eq $false -and
		(Test-Perspective (Get-JsonProperty $renderControllerValue 'Perspective') 'FirstPerson' 0) -and
		(Test-ComponentReference (Get-JsonProperty $renderControllerValue 'PbsSuppressorRenderer') $rendererValue $socket) -and
		(Test-OtherRendererReferencesEmpty $renderControllerValue)
	Add-ContractCheck "${Name}_view_pbs_renderer_is_default_hidden_and_first_person" $rendererWiring "model=$(Get-JsonProperty $rendererValue 'Model') rendererEnabled=$(Get-JsonProperty $rendererValue '__enabled') perspective=$(Get-JsonProperty $renderControllerValue 'Perspective')"
}

function Test-MilitaryExclusion {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [string] $AssetPath
	)

	$document = Get-PrefabDocument $AssetPath
	$root = Get-JsonProperty $document 'RootObject'
	$nodes = @(Get-AllNodes $root)
	$components = @(Get-AllComponents $root)
	$attachmentComponents = @($components | Where-Object {
		$type = Get-JsonProperty $_ '__type'
		$type -eq $stateType -or $type -eq $controllerType -or $type -eq $renderControllerType
	})
	$pbsRenderers = @($components | Where-Object {
		(Get-JsonProperty $_ 'Model') -eq $pbsModelAsset
	})
	$namedSockets = @($nodes | Where-Object {
		(Get-JsonProperty $_ 'Name') -eq 'attachment_socket_muzzle'
	})
	$passed = $null -ne $root -and $attachmentComponents.Count -eq 0 -and $pbsRenderers.Count -eq 0 -and $namedSockets.Count -eq 0
	Add-ContractCheck "${Name}_remains_outside_pbs_attachment_contract" $passed "asset=$AssetPath attachmentComponents=$($attachmentComponents.Count) pbsRenderers=$($pbsRenderers.Count) namedSockets=$($namedSockets.Count)"
}

if (-not $Ak47Only) {
	$provenanceText = if (Test-Path -LiteralPath $provenancePath -PathType Leaf) {
		Get-Content -LiteralPath $provenancePath -Raw
	} else {
		''
	}
	$provenanceStatus = Get-ProvenanceFieldValue -Text $provenanceText -Name 'provenance_status'
	$licenseStatus = Get-ProvenanceFieldValue -Text $provenanceText -Name 'license_status'
	$licenseTier = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_license_tier'
	$tierEligibility = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_tier_eligibility'
	$licenseSnapshot = Get-ProvenanceFieldValue -Text $provenanceText -Name 'acquisition_license_snapshot'
	$provenanceCleared = Test-PbsPublicationEvidence -Text $provenanceText
	Add-ContractCheck 'pbs_source_license_and_provenance_are_verified' $provenanceCleared "provenance=$provenanceStatus license=$licenseStatus tier=$licenseTier eligibility=$tierEligibility snapshot=$licenseSnapshot; require coherent acquisition evidence before shared product assets exist"

	$statusOnlyPbsFixture = @'
license_status=VERIFIED
provenance_status=VERIFIED
acquisition_license_tier=UNVERIFIED
acquisition_tier_eligibility=UNVERIFIED
acquisition_license_snapshot=UNVERIFIED
'@
	$personalPbsFixture = @'
license_status=VERIFIED
provenance_status=VERIFIED
acquisition_license_tier=Personal
acquisition_tier_eligibility=VERIFIED
acquisition_license_snapshot=VERIFIED
'@
	$professionalPbsFixture = @'
license_status=VERIFIED
provenance_status=VERIFIED
acquisition_license_tier=Professional
acquisition_tier_eligibility=NOT_APPLICABLE_BY_ACQUISITION_TERMS
acquisition_license_snapshot=VERIFIED
'@
	Add-ContractCheck 'pbs_publication_gate_rejects_status_only_license_fixture' (-not (Test-PbsPublicationEvidence -Text $statusOnlyPbsFixture)) 'Verified status labels cannot replace coherent acquisition evidence'
	Add-ContractCheck 'pbs_publication_gate_accepts_coherent_personal_fixture' (Test-PbsPublicationEvidence -Text $personalPbsFixture) 'Personal requires verified transaction-time eligibility and license snapshot'
	Add-ContractCheck 'pbs_publication_gate_accepts_coherent_professional_fixture' (Test-PbsPublicationEvidence -Text $professionalPbsFixture) 'Professional requires explicit terms-based eligibility non-applicability and license snapshot'

	$pbsModelExists = Test-Path -LiteralPath (Get-AssetDiskPath $pbsModelAsset) -PathType Leaf
	$pbsMaterialExists = Test-Path -LiteralPath (Get-AssetDiskPath $pbsMaterialAsset) -PathType Leaf
	Add-ContractCheck 'one_shared_pbs_modeldoc_exists' $pbsModelExists $pbsModelAsset
	Add-ContractCheck 'one_shared_pbs_material_exists' $pbsMaterialExists $pbsMaterialAsset
}

Test-MuzzlePin 'ak47_world_muzzle_is_unchanged' $ak47WorldAsset $equipmentType `
	'f2f8b82c-7f96-4f1b-89b6-9e160c28f3b0' '20.0757103,0,7.21292114' '0,0,0,1' '1,1,1'
Test-MuzzlePin 'ak47_view_muzzle_is_unchanged' $ak47ViewAsset $viewModelType `
	'66487ae4-5d15-48d1-bc2e-7efb3df4efc0' '0,0,0' '0,0,0,1' '1,1,1'

Test-WorldAttachmentContract 'ak47' $ak47WorldAsset 'ak47'
Test-ViewAttachmentContract 'ak47' $ak47ViewAsset
if (-not $Ak47Only) {
	Test-WorldAttachmentContract 'aks74u_original' $originalWorldAsset 'aks74u_original'
	Test-ViewAttachmentContract 'aks74u_original' $originalViewAsset
	Test-MilitaryExclusion 'aks74u_military_world' $militaryWorldAsset
	Test-MilitaryExclusion 'aks74u_military_view' $militaryViewAsset
}

$attachmentPrefabAssets = if ($Ak47Only) {
	@($ak47WorldAsset, $ak47ViewAsset)
} else {
	@($ak47WorldAsset, $ak47ViewAsset, $originalWorldAsset, $originalViewAsset)
}
$expectedSharedPbsConsumerCount = if ($Ak47Only) { 2 } else { 4 }
$sharedPbsConsumerCount = 0
foreach ($assetPath in $attachmentPrefabAssets) {
	$document = Get-PrefabDocument $assetPath
	$root = Get-JsonProperty $document 'RootObject'
	$sharedPbsConsumerCount += @(
		Get-AllComponents $root | Where-Object {
			(Get-JsonProperty $_ 'Model') -eq $pbsModelAsset
		}
	).Count
}
Add-ContractCheck $(if ($Ak47Only) { 'ak47_has_exactly_two_shared_pbs_prefab_consumers' } else { 'shared_pbs_model_has_exactly_four_prefab_consumers' }) `
	($sharedPbsConsumerCount -eq $expectedSharedPbsConsumerCount) `
	"expected=$expectedSharedPbsConsumerCount actual=$sharedPbsConsumerCount assets=$($attachmentPrefabAssets -join ',')"

$failures = @($checks | Where-Object { -not $_.passed })
$result = [ordered]@{
	contract = if ($Ak47Only) { 'ak47-pbs-attachment-render' } else { 'pbs-attachment-render' }
	proofCeiling = if ($Ak47Only) { 'ak47-prefab-wiring-only' } else { 'shared-pbs-prefab-wiring-and-source-gate' }
	requiresFullContract = [bool] $Ak47Only
	status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
	exitCode = if ($failures.Count -eq 0) { 0 } else { 1 }
	passedAssertions = @($checks | Where-Object { $_.passed }).Count
	failedAssertions = $failures.Count
	checks = $checks
}

$result | ConvertTo-Json -Depth 8
exit $result.exitCode
