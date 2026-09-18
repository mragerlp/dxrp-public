[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

# Independent measurements from sbox-native inspect_model_geometry on engine
# 26.08.19. The seven filtered AR-15 parts assemble to the source-model span
# below; v_m4a1 is the Facepunch assault-rifle first-person baseline.
$expectedSourceHash = '69BB9AFDC7CF19766A2127811498AA41AD02694E9B54A780CD3744D92D87808A'
$ar15AssembledLongAxis = 38.923767
$m4a1ViewLongAxis = 31.093287
$allowedRelativeError = 0.02
$targetRendererScale = $m4a1ViewLongAxis / $ar15AssembledLongAxis

$sourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\source\generated\ar15_native_clean.fbx'
$viewPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'
$expectedModelSource = 'addons/lifepunch/lpweapons/ar15/source/generated/ar15_native_clean.fbx'

$parts = @(
	[pscustomobject]@{
		Name = 'body'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_body.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/ar15_body_renderer_wrapper'
		ModelDocHash = 'FCC0C3BC80E208EC6A2167F813A57B7802E09C1A056585222A808F257E794D27'
	},
	[pscustomobject]@{
		Name = 'stock'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_stock.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/stock/ar15_stock_renderer_wrapper'
		ModelDocHash = '04B9421D94402496A313A65C396EADD9B14851C926933DDEE314538B51BF7FE1'
	},
	[pscustomobject]@{
		Name = 'trigger'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_trigger.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/trigger/ar15_trigger_renderer_wrapper'
		ModelDocHash = 'BD7FE1F867FB43366EF61E703B08735E29A01143E45E2242C01322281D7E2D2B'
	},
	[pscustomobject]@{
		Name = 'magazine'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_magazine.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/magazine/ar15_magazine_renderer_wrapper'
		ModelDocHash = '0EC5B1FAA4FFE90413220164268BF261214AA18E15F3509B846C5F8D7CDB2CC1'
	},
	[pscustomobject]@{
		Name = 'bolt_flap'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_bolt_flap.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/bolt_flap/ar15_bolt_flap_renderer_wrapper'
		ModelDocHash = '1616AD2379E3288CD56A211BD516B9EDDF08A372B4B696FE7B2E258BB8886397'
	},
	[pscustomobject]@{
		Name = 'bolt'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_bolt.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/bolt/ar15_bolt_renderer_wrapper'
		ModelDocHash = 'F287F1457BC1FFFB6B302CBE59841A77F68F8FC196E1CF2AF6A382BCAFF82181'
	},
	[pscustomobject]@{
		Name = 'charging_handle'
		Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_charging_handle.vmdl'
		Path = 'vm_ar15/weapon_root/weapon_root_children/charging_handle/ar15_charging_handle_renderer_wrapper'
		ModelDocHash = '09E56635FDBAE607C07A65E31B8ED8D6E03232E82126913B4629877A3579C760'
	}
)

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
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
		[Math]::Abs( $localScale[0] - $localScale[1] ) -le 0.0000001 -and
		[Math]::Abs( $localScale[0] - $localScale[2] ) -le 0.0000001
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

function Get-PartRowsFromDocument( [object] $Prefab, [string] $Label )
{
	$scaleNodes = @(Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '')
	$rows = [System.Collections.Generic.List[object]]::new()
	foreach ( $part in $parts )
	{
		$matches = @($scaleNodes | Where-Object {
			@($_.Node.Components | Where-Object {
				$_.PSObject.Properties.Name -contains 'Model' -and
				[string]$_.Model -ceq $part.Model
			}).Count -eq 1
		})
		if ( $matches.Count -ne 1 )
		{
			throw "$Label must contain exactly one $($part.Name) renderer owner for $($part.Model); found $($matches.Count)."
		}

		$rows.Add( [pscustomobject]@{
			Part = $part
			Node = $matches[0].Node
			Path = $matches[0].Path
			LocalScale = $matches[0].LocalScale
			CumulativeUniformScale = $matches[0].CumulativeUniformScale
			UniformChain = $matches[0].UniformChain
		} )
	}

	return @($rows)
}

foreach ( $requiredPath in @($sourcePath, $viewPrefabPath) )
{
	if ( !(Test-Path -LiteralPath $requiredPath -PathType Leaf) )
	{
		throw "Missing AR-15 view-scale input: $requiredPath"
	}
}

$actualSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
if ( $actualSourceHash -cne $expectedSourceHash )
{
	Add-Failure 'AR-15 source FBX changed; remeasure the assembled view bounds before accepting scale.'
}

foreach ( $part in $parts )
{
	$modelDocPath = Join-Path $repoRoot ('game\Assets\' + $part.Model.Replace( '/', '\' ))
	if ( !(Test-Path -LiteralPath $modelDocPath -PathType Leaf) )
	{
		throw "Missing AR-15 $($part.Name) ModelDoc: $modelDocPath"
	}

	$actualModelDocHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $modelDocPath).Hash
	if ( $actualModelDocHash -cne $part.ModelDocHash )
	{
		Add-Failure "AR-15 $($part.Name) ModelDoc changed; remeasure the assembled view bounds."
	}

	$text = Get-Content -LiteralPath $modelDocPath -Raw
	$sourceMatches = [regex]::Matches(
		$text,
		'(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$' )
	$scaleMatches = [regex]::Matches(
		$text,
		'(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$' )
	if ( $sourceMatches.Count -ne 1 -or
		$sourceMatches[0].Groups['value'].Value -cne $expectedModelSource )
	{
		Add-Failure "AR-15 $($part.Name) ModelDoc must keep one exact generated-source filename."
	}
	if ( $scaleMatches.Count -ne 1 )
	{
		Add-Failure "AR-15 $($part.Name) ModelDoc must keep one explicit import_scale."
	}
	else
	{
		$importScale = [double]::Parse(
			$scaleMatches[0].Groups['value'].Value,
			[Globalization.CultureInfo]::InvariantCulture )
		if ( [Math]::Abs( $importScale - 39.37008 ) -gt 0.0000001 )
		{
			Add-Failure "AR-15 $($part.Name) import scale changed from the measured 39.37008 seam: $importScale."
		}
	}
}

$prefab = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$partRows = @(Get-PartRowsFromDocument $prefab $viewPrefabPath)
foreach ( $row in $partRows )
{
	if ( $row.Path -cne $row.Part.Path )
	{
		Add-Failure "AR-15 $($row.Part.Name) renderer moved from $($row.Part.Path) to $($row.Path)."
	}
	if ( !$row.UniformChain )
	{
		Add-Failure "AR-15 $($row.Part.Name) renderer has non-uniform scale in its ancestry: $($row.Path)."
	}
	if ( [Math]::Abs( $row.LocalScale[0] - $row.LocalScale[1] ) -gt 0.0000001 -or
		[Math]::Abs( $row.LocalScale[0] - $row.LocalScale[2] ) -gt 0.0000001 )
	{
		Add-Failure "AR-15 $($row.Part.Name) renderer scale must remain uniform: $($row.LocalScale -join ',')."
	}
	if ( [Math]::Abs( $row.CumulativeUniformScale - $row.LocalScale[0] ) -gt 0.0000001 )
	{
		Add-Failure "AR-15 $($row.Part.Name) scale must stay on its renderer wrapper; an ancestor also scales it."
	}
}

$referenceScale = [double]$partRows[0].CumulativeUniformScale
foreach ( $row in $partRows )
{
	if ( [Math]::Abs( $row.CumulativeUniformScale - $referenceScale ) -gt 0.0000001 )
	{
		Add-Failure "AR-15 assembled view parts diverge in scale: $($row.Part.Name)=$($row.CumulativeUniformScale), reference=$referenceScale."
	}
}

$effectiveLongAxis = $ar15AssembledLongAxis * [Math]::Abs( $referenceScale )
$viewRatio = $effectiveLongAxis / $m4a1ViewLongAxis
if ( [Math]::Abs( $viewRatio - 1.0 ) -gt $allowedRelativeError )
{
	Add-Failure ("AR-15 view long axis is {0:N4}x the M4A1 baseline; expected 1.00x +/- {1:P0}." -f $viewRatio, $allowedRelativeError)
}

# Negative controls mutate only in-memory documents. They prove the contract
# rejects the exact pre-fix 0.90330522 wrapper scale and a hidden 2.8x root.
$originalScaleProbe = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
foreach ( $row in @(Get-PartRowsFromDocument $originalScaleProbe 'AR-15 pre-fix scale negative control') )
{
	$row.Node.Scale = '0.90330522,0.90330522,0.90330522'
}
$originalScaleRows = @(Get-PartRowsFromDocument $originalScaleProbe 'AR-15 pre-fix scale negative control')
$originalScaleRatio = (
	$ar15AssembledLongAxis * [Math]::Abs( $originalScaleRows[0].CumulativeUniformScale )
) / $m4a1ViewLongAxis
$originalScaleRejected = [Math]::Abs( $originalScaleRatio - 1.0 ) -gt $allowedRelativeError
if ( !$originalScaleRejected )
{
	Add-Failure 'AR-15 view scale contract is blind to the pre-fix 0.90330522 defect.'
}

$ancestorProbe = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$ancestorProbe.RootObject.Scale = '2.8,2.8,2.8'
$ancestorRows = @(Get-PartRowsFromDocument $ancestorProbe 'AR-15 ancestor-scale negative control')
$ancestorRatio = (
	$ar15AssembledLongAxis * [Math]::Abs( $ancestorRows[0].CumulativeUniformScale )
) / $m4a1ViewLongAxis
$ancestorRejected = [Math]::Abs( $ancestorRatio - 1.0 ) -gt $allowedRelativeError
if ( !$ancestorRejected )
{
	Add-Failure 'AR-15 view scale contract is blind to a 2.8x ancestor/root scale mutation.'
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapon = 'AR-15'
	surface = 'view'
	sourceSha256 = $actualSourceHash
	parts = @($partRows | ForEach-Object {
		[ordered]@{
			name = $_.Part.Name
			path = $_.Path
			localScale = $_.LocalScale
			cumulativeScale = $_.CumulativeUniformScale
		}
	})
	targetRendererScale = $targetRendererScale
	m4a1ViewLongAxis = $m4a1ViewLongAxis
	effectiveViewLongAxis = $effectiveLongAxis
	viewRatio = $viewRatio
	preFixMutationRatio = $originalScaleRatio
	preFixMutationRejected = $originalScaleRejected
	oversizeAncestorMutationRatio = $ancestorRatio
	oversizeAncestorMutationRejected = $ancestorRejected
	allowedRelativeError = $allowedRelativeError
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 7
if ( $failures.Count -gt 0 )
{
	exit 1
}
