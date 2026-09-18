[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$checks = [System.Collections.Generic.List[object]]::new()
$allowedRelativeError = 0.02

# Pinned measurement chain: Blender 5.1.2 measured the unsuppressed and full
# semantic assemblies from the same FBX; the byte-pinned Weapon Animator record
# measured that full assembly in s&box units. Their ratio yields the base body.
$semanticBodyLongAxis = 4.3818522691726685
$semanticFullLongAxis = 5.99269700050354
$weaponAnimatorFullLongAxis = 599.269775
$sourceBodyLongAxis = $weaponAnimatorFullLongAxis * ($semanticBodyLongAxis / $semanticFullLongAxis)
$mp5WorldLongAxis = 22.586597
$mp5ViewLongAxis = 22.568323
$semanticSourceHash = '7082DACBE629B3C24C2F272BF8D96D426084337CB706E9470CD4FA1059FE4363'
$measurementRecordHash = '11AB89E463A67966EFE4039E8FC7BC37C4A226AD6D29BFCE23DE308C6642DBA2'

$semanticSourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\source\aks-74u_extract\source\aks74ucovert_semantic.fbx'
$measurementRecordPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\aks74ucovert.wepanim'
$modelDocPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\aks74u_original.vmdl'
$worldPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\equipment\w_aks74u_original\w_aks74u_original.prefab'
$viewPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\equipment\vm_aks74u_original\vm_aks74u_original.prefab'
$modelAsset = 'addons/lifepunch/lpweapons/aks74ucovert/aks74u_original.vmdl'
$semanticSourceAsset = 'addons/lifepunch/lpweapons/aks74ucovert/source/aks-74u_extract/source/aks74ucovert_semantic.fbx'
$rendererTypes = @('Sandbox.ModelRenderer', 'Sandbox.SkinnedModelRenderer')

function Add-Check {
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

function Read-Vector3 {
	param([Parameter(Mandatory)] [string] $Text)

	$parts = @($Text.Split(','))
	if ($parts.Count -ne 3) {
		throw "Expected Vector3 text, got '$Text'."
	}

	return @($parts | ForEach-Object {
		[double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture)
	})
}

function Get-ScaleNodes {
	param(
		[Parameter(Mandatory)] $Node,
		[double] $ParentScale = 1.0,
		[bool] $ParentUniformPositive = $true,
		[string] $ParentPath = ''
	)

	$local = Read-Vector3 ([string]$Node.Scale)
	$uniformPositive = $ParentUniformPositive -and
		$local[0] -gt 0.0 -and
		[Math]::Abs($local[0] - $local[1]) -le 0.0000001 -and
		[Math]::Abs($local[0] - $local[2]) -le 0.0000001
	$path = if ([string]::IsNullOrWhiteSpace($ParentPath)) { [string]$Node.Name } else { "$ParentPath/$($Node.Name)" }

	[pscustomobject]@{
		node = $Node
		path = $path
		cumulativeScale = $ParentScale * $local[0]
		uniformPositive = $uniformPositive
	}

	foreach ($child in @($Node.Children)) {
		Get-ScaleNodes $child ($ParentScale * $local[0]) $uniformPositive $path
	}
}

function Get-RendererScaleInfo {
	param(
		[Parameter(Mandatory)] [string] $PrefabPath,
		[Parameter(Mandatory)] [string] $ExpectedModel
	)

	$document = Get-Content -LiteralPath $PrefabPath -Raw | ConvertFrom-Json -Depth 100
	$matches = @(Get-ScaleNodes $document.RootObject | Where-Object {
		@($_.node.Components | Where-Object {
			Test-ExpectedRendererComponent $_ $ExpectedModel
		}).Count -eq 1
	})
	if ($matches.Count -ne 1) {
		throw "$PrefabPath must contain exactly one renderer for $ExpectedModel; found $($matches.Count)."
	}

	return $matches[0]
}

function Test-ExpectedRendererComponent {
	param(
		[AllowNull()] $Component,
		[Parameter(Mandatory)] [string] $ExpectedModel
	)

	return $null -ne $Component -and
		$Component.PSObject.Properties.Name -contains '__type' -and
		([string]$Component.__type -cin $rendererTypes) -and
		$Component.PSObject.Properties.Name -contains 'Model' -and
		[string]$Component.Model -ceq $ExpectedModel
}

function Test-ScaleProbe {
	param(
		[double] $ImportScale,
		[double] $CumulativeScale,
		[double] $Baseline,
		[bool] $UniformPositive
	)

	if (!$UniformPositive -or $ImportScale -le 0.0 -or $CumulativeScale -le 0.0) {
		return $false
	}

	$ratio = ($sourceBodyLongAxis * $ImportScale * $CumulativeScale) / $Baseline
	return [Math]::Abs($ratio - 1.0) -le $allowedRelativeError
}

function Test-CrossPerspectiveProbe {
	param([double] $WorldRatio, [double] $ViewRatio)
	return [Math]::Abs($WorldRatio - $ViewRatio) -le $allowedRelativeError
}

function Test-SuppressorExcludedBySingleFilter {
	param([Parameter(Mandatory)] [string] $Text)

	$filterMatches = [regex]::Matches($Text, '(?s)import_filter\s*=\s*\{(?<body>[^{}]*)\}')
	$filterBody = if ($filterMatches.Count -eq 1) { $filterMatches[0].Groups['body'].Value } else { '' }
	$excludeDefaultMatches = [regex]::Matches($filterBody, '(?m)^\s*exclude_by_default\s*=\s*(?<value>true|false)\s*$')
	$exceptionListMatches = [regex]::Matches($filterBody, '(?ms)^[ \t]*exception_list[ \t]*=[ \t]*\[(?<items>[^\[\]]*)\][ \t]*$')
	$exceptionItems = if ($exceptionListMatches.Count -eq 1) {
		@([regex]::Matches($exceptionListMatches[0].Groups['items'].Value, '"(?<value>[^"]+)"') | ForEach-Object { $_ })
	} else {
		@()
	}
	return $filterMatches.Count -eq 1 -and
		$excludeDefaultMatches.Count -eq 1 -and
		$excludeDefaultMatches[0].Groups['value'].Value -ceq 'false' -and
		$exceptionListMatches.Count -eq 1 -and
		@($exceptionItems).Count -eq 1 -and
		@($exceptionItems)[0].Groups['value'].Value -ceq 'suppressor'
}

$semanticExists = Test-Path -LiteralPath $semanticSourcePath -PathType Leaf
$semanticHash = if ($semanticExists) { (Get-FileHash -Algorithm SHA256 -LiteralPath $semanticSourcePath).Hash } else { '' }
Add-Check 'semantic_source_measurement_pin' ($semanticHash -ceq $semanticSourceHash) "expected=$semanticSourceHash actual=$semanticHash"

$recordExists = Test-Path -LiteralPath $measurementRecordPath -PathType Leaf
$recordHash = if ($recordExists) { (Get-FileHash -Algorithm SHA256 -LiteralPath $measurementRecordPath).Hash } else { '' }
Add-Check 'weapon_animator_measurement_pin' ($recordHash -ceq $measurementRecordHash) "expected=$measurementRecordHash actual=$recordHash"

$modelDocExists = Test-Path -LiteralPath $modelDocPath -PathType Leaf
Add-Check 'original_base_modeldoc_exists' $modelDocExists $modelDocPath

$importScale = 0.0
if ($modelDocExists) {
	$modelText = Get-Content -LiteralPath $modelDocPath -Raw
	$scaleMatches = [regex]::Matches($modelText, '(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$')
	if ($scaleMatches.Count -eq 1) {
		$importScale = [double]::Parse($scaleMatches[0].Groups['value'].Value, [Globalization.CultureInfo]::InvariantCulture)
	}
	$sourceMatches = [regex]::Matches($modelText, '(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$')
	$usesPinnedSource = $sourceMatches.Count -eq 1 -and $sourceMatches[0].Groups['value'].Value -ceq $semanticSourceAsset
	$excludesSuppressor = Test-SuppressorExcludedBySingleFilter $modelText
	Add-Check 'original_modeldoc_uses_pinned_semantic_source' $usesPinnedSource "expected=$semanticSourceAsset"
	Add-Check 'original_modeldoc_excludes_detachable_suppressor' $excludesSuppressor 'base weapon model must exclude mesh suppressor'
	Add-Check 'original_modeldoc_has_one_positive_import_scale' ($scaleMatches.Count -eq 1 -and $importScale -gt 0.0) "count=$($scaleMatches.Count) scale=$importScale"
}

$worldExists = Test-Path -LiteralPath $worldPrefabPath -PathType Leaf
$viewExists = Test-Path -LiteralPath $viewPrefabPath -PathType Leaf
Add-Check 'original_world_prefab_exists' $worldExists $worldPrefabPath
Add-Check 'original_view_prefab_exists' $viewExists $viewPrefabPath

$worldInfo = if ($modelDocExists -and $worldExists -and $importScale -gt 0.0) { Get-RendererScaleInfo $worldPrefabPath $modelAsset } else { $null }
$viewInfo = if ($modelDocExists -and $viewExists -and $importScale -gt 0.0) { Get-RendererScaleInfo $viewPrefabPath $modelAsset } else { $null }

$worldRatio = $null
$viewRatio = $null
if ($null -ne $worldInfo) {
	$worldRatio = ($sourceBodyLongAxis * $importScale * $worldInfo.cumulativeScale) / $mp5WorldLongAxis
	Add-Check 'original_world_scale_matches_mp5_class' (Test-ScaleProbe $importScale $worldInfo.cumulativeScale $mp5WorldLongAxis $worldInfo.uniformPositive) "ratio=$worldRatio renderer=$($worldInfo.path)"
}
if ($null -ne $viewInfo) {
	$viewRatio = ($sourceBodyLongAxis * $importScale * $viewInfo.cumulativeScale) / $mp5ViewLongAxis
	Add-Check 'original_view_scale_matches_mp5_class' (Test-ScaleProbe $importScale $viewInfo.cumulativeScale $mp5ViewLongAxis $viewInfo.uniformPositive) "ratio=$viewRatio renderer=$($viewInfo.path)"
}
if ($null -ne $worldRatio -and $null -ne $viewRatio) {
	Add-Check 'original_cross_perspective_scale_consistency' (Test-CrossPerspectiveProbe $worldRatio $viewRatio) "worldRatio=$worldRatio viewRatio=$viewRatio"
}

$fixtureWorldScale = $mp5WorldLongAxis / $sourceBodyLongAxis
$fixtureViewScale = $mp5ViewLongAxis / $sourceBodyLongAxis
Add-Check 'positive_control_accepts_exact_mp5_fit' (Test-ScaleProbe 1.0 $fixtureWorldScale $mp5WorldLongAxis $true) 'exact derived world fixture'
Add-Check 'negative_control_rejects_2_8x_ancestor' (-not (Test-ScaleProbe 1.0 ($fixtureWorldScale * 2.8) $mp5WorldLongAxis $true)) '2.8x cumulative ancestor mutation'
Add-Check 'negative_control_rejects_nonuniform_chain' (-not (Test-ScaleProbe 1.0 $fixtureWorldScale $mp5WorldLongAxis $false)) 'nonuniform ancestry fixture'
Add-Check 'negative_control_rejects_negative_scale' (-not (Test-ScaleProbe -1.0 $fixtureWorldScale $mp5WorldLongAxis $true)) 'negative import-scale fixture'
Add-Check 'positive_control_accepts_cross_perspective_match' (Test-CrossPerspectiveProbe 1.0 1.0) 'matching perspective ratios'
Add-Check 'negative_control_rejects_cross_perspective_drift' (-not (Test-CrossPerspectiveProbe 0.981 1.019)) 'both individual ratios fit 2%, but their 3.8% spread must fail'
$rendererFixture = [pscustomobject]@{ __type = 'Sandbox.ModelRenderer'; Model = $modelAsset }
$modelBearingDecoy = [pscustomobject]@{ __type = 'Sandbox.ModelCollider'; Model = $modelAsset }
Add-Check 'positive_control_accepts_typed_renderer' (Test-ExpectedRendererComponent $rendererFixture $modelAsset) 'typed renderer fixture'
Add-Check 'negative_control_rejects_model_bearing_nonrenderer' (-not (Test-ExpectedRendererComponent $modelBearingDecoy $modelAsset)) 'collider decoy with matching Model field'
$singleFilterFixture = @'
import_filter = {
  exclude_by_default = false
  exception_list = [ "suppressor" ]
}
'@
$crossBlockFilterFixture = @'
import_filter = {
  exclude_by_default = false
  exception_list = [ ]
}
import_filter = {
  exclude_by_default = true
  exception_list = [ "suppressor" ]
}
'@
$unrelatedSuppressorFixture = @'
import_filter = {
  exclude_by_default = false
  unrelated = "suppressor"
}
'@
Add-Check 'positive_control_accepts_single_suppressor_exclusion' (Test-SuppressorExcludedBySingleFilter $singleFilterFixture) 'one scoped exclusion filter'
Add-Check 'negative_control_rejects_cross_block_filter_decoy' (-not (Test-SuppressorExcludedBySingleFilter $crossBlockFilterFixture)) 'separate filter blocks cannot combine evidence'
Add-Check 'negative_control_rejects_unrelated_suppressor_field' (-not (Test-SuppressorExcludedBySingleFilter $unrelatedSuppressorFixture)) 'quoted suppressor outside an explicit exception_list is not exclusion evidence'

$failures = @($checks | Where-Object { -not $_.passed })
[pscustomobject]@{
	contract = 'AKS-74U Original class scale'
	status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
	sourceBodyLongAxis = $sourceBodyLongAxis
	mp5WorldLongAxis = $mp5WorldLongAxis
	mp5ViewLongAxis = $mp5ViewLongAxis
	worldRatio = $worldRatio
	viewRatio = $viewRatio
	checks = $checks
	failureCount = $failures.Count
} | ConvertTo-Json -Depth 7

if ($failures.Count -gt 0) { exit 1 }
exit 0
