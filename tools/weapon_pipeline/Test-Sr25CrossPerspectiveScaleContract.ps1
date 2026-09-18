[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

# The SR-25 is a semi-auto marksman rifle. DXRP's weapon-fit doctrine assigns
# M4A1 as its physical/animation donor and M700 only for scope/ADS behavior.
# These spans were measured independently with sbox-native
# inspect_model_geometry on engine 26.08.19. The SR-25 source span is shared by
# its eleven filtered view models and one combined world model.
$expectedSourceHash = '0ECC693CD4F35879ABC0E6AC6643BF2C70783032DA3AAF3AEFA73A7EF7BB0B1C'
$sr25UnitLongAxis = 43.08609
$sr25UnitMaxX = 21.928625
$m4a1ViewLongAxis = 31.093287
$m4a1WorldLongAxis = 31.099375
$m700ViewLongAxis = 39.330677
$m700WorldLongAxis = 39.32762
$allowedSurfaceRatioDelta = 0.02
$scaleTolerance = 0.0000001
$vectorTolerance = 0.000001
$viewVectorTolerance = 0.00001

$sourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\sr25\source\generated\sr25_native_clean.fbx'
$viewPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'
$worldPrefabPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\w_sr25\w_sr25.prefab'
$expectedModelSource = 'addons/lifepunch/lpweapons/sr25/source/generated/sr25_native_clean.fbx'
$worldModel = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_world.vmdl'
$worldRendererPath = 'w_sr25/Model/sr25_world'

$parts = @(
	[pscustomobject]@{ Name = 'body'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_body.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/sr25_body_renderer_wrapper'; ModelDocHash = 'D2965CEC829AE8BFB51813CA99C36DB5270983F0128DDD1AF37A60EBA24F0E5E' },
	[pscustomobject]@{ Name = 'stock'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_stock.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/stock/sr25_stock_renderer_wrapper'; ModelDocHash = 'AA042D34B67F959403E24C444622C57C7B35531E971D7A61C53D19B654C168DA' },
	[pscustomobject]@{ Name = 'trigger'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_trigger.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/trigger/sr25_trigger_renderer_wrapper'; ModelDocHash = 'B77D812A20BB7AC6670FFDB84C52D8A2DCFA4ACCB0D8AD7CE7AB5AA49E03CC1F' },
	[pscustomobject]@{ Name = 'magazine'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_magazine.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/magazine/sr25_magazine_renderer_wrapper'; ModelDocHash = '0191861D93A45E73A1E8AFE2856C098E473F605CD62692C22E79DC4F472E98B2' },
	[pscustomobject]@{ Name = 'mode_selector'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_mode_selector.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/mode_selector/sr25_mode_selector_renderer_wrapper'; ModelDocHash = '3A3DDE27FE2E085AC1C15197580A7C7154BC91FBE5F93626A768BF18FAA11FA9' },
	[pscustomobject]@{ Name = 'bolt_flap'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_bolt_flap.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/bolt_flap/sr25_bolt_flap_renderer_wrapper'; ModelDocHash = '02FA3A0BBFD0AD7ACBA9599EA37E683F9E1A02BE62E4C9AB6D3FC7E9701245EB' },
	[pscustomobject]@{ Name = 'bolt'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_bolt.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/bolt/sr25_bolt_renderer_wrapper'; ModelDocHash = '2B796E7D067E38B7E2517CB61B14E61DB6BFD74C0F3A09C9EC0CE98378C233FE' },
	[pscustomobject]@{ Name = 'charging_handle'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_charging_handle.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/charging_handle/sr25_charging_handle_renderer_wrapper'; ModelDocHash = 'C9D143F06983AC548707D6366E0BA367ACDE6A45B37D264BB461BFB36A57949A' },
	[pscustomobject]@{ Name = 'scope'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_scope.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/sr25_scope/sr25_scope_renderer_wrapper'; ModelDocHash = 'DDBC774723ED48887E38C286091B784616A087CD666D4B3ED02ADE7CFC3BCB48' },
	[pscustomobject]@{ Name = 'scope_mount'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_scope_mount.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/sr25_scope_mount/sr25_scope_mount_renderer_wrapper'; ModelDocHash = 'E30F31E3E1352F0602F9B1C00428AD40DAA27910E5822DF6FD6A7CDE920B1B18' },
	[pscustomobject]@{ Name = 'suppressor'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_suppressor.vmdl'; Path = 'vm_sr25/weapon_root/weapon_root_children/sr25_suppressor/sr25_suppressor_renderer_wrapper'; ModelDocHash = '508ADB18F92D48568EB2269A0398CF28D244580A9F80DB176B0BF14EA7091218' }
)

$worldModelDoc = [pscustomobject]@{
	Name = 'world'
	Model = $worldModel
	ModelDocHash = '8D4DA15AC65F47D19899AFC6D6CE812A34561EC65DE2FB91D71EC0DC5BAAEE28'
}

$anchorContracts = @(
	[pscustomobject]@{
		Name = 'Muzzle'
		Path = 'w_sr25/Model/Muzzle'
		Guid = '57250000-5a25-4000-8000-00000000006e'
		ReferenceProperty = 'Muzzle'
		UnitPosition = '21.928625,-0.096,0.905'
	},
	[pscustomobject]@{
		Name = 'EjectionPort'
		Path = 'w_sr25/Model/EjectionPort'
		Guid = '57250000-5a25-4000-8000-00000000006d'
		ReferenceProperty = 'EjectionPort'
		UnitPosition = '-5.898,-0.953,0.263'
	}
)

$viewAnchorContracts = @(
	[pscustomobject]@{
		Name = 'Muzzle'
		Path = 'vm_sr25/weapon_root/weapon_root_children/sr25_muzzle'
		Guid = '57250000-5a25-4000-8000-000000000065'
		ReferenceProperty = 'Muzzle'
		WrapperModel = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_body.vmdl'
		UnitPosition = '21.928625,-0.096,0.905'
	},
	[pscustomobject]@{
		Name = 'EjectionPort'
		Path = 'vm_sr25/weapon_root/weapon_root_children/bolt_flap/sr25_ejection_port'
		Guid = '57250000-5a25-4000-8000-000000000066'
		ReferenceProperty = 'EjectionPort'
		WrapperModel = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_bolt_flap.vmdl'
		UnitPosition = '-5.898,-0.953,0.263'
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

function Read-Quaternion( [string] $Value )
{
	$values = @($Value.Split( ',' ))
	if ( $values.Count -ne 4 )
	{
		throw "Expected Quaternion text, got '$Value'."
	}

	$parsed = @($values | ForEach-Object {
		[float]::Parse( $_, [Globalization.CultureInfo]::InvariantCulture )
	})
	return [System.Numerics.Quaternion]::new(
		$parsed[0], $parsed[1], $parsed[2], $parsed[3] )
}

function Transform-Point(
	[double[]] $UnitPosition,
	[string] $PositionText,
	[string] $RotationText,
	[double] $UniformScale
)
{
	$position = Read-Vector3 $PositionText
	$point = [System.Numerics.Vector3]::new(
		[float]($UnitPosition[0] * $UniformScale),
		[float]($UnitPosition[1] * $UniformScale),
		[float]($UnitPosition[2] * $UniformScale) )
	$rotated = [System.Numerics.Vector3]::Transform(
		$point,
		(Read-Quaternion $RotationText) )
	return @(
		([double]$rotated.X + [double]$position[0]),
		([double]$rotated.Y + [double]$position[1]),
		([double]$rotated.Z + [double]$position[2])
	)
}

function Test-VectorClose(
	[double[]] $Actual,
	[double[]] $Expected,
	[double] $Tolerance
)
{
	return [bool](
		[Math]::Abs( $Actual[0] - $Expected[0] ) -le $Tolerance -and
		[Math]::Abs( $Actual[1] - $Expected[1] ) -le $Tolerance -and
		[Math]::Abs( $Actual[2] - $Expected[2] ) -le $Tolerance
	)
}

function Test-InMarksmanDonorEnvelope(
	[double] $EffectiveLongAxis,
	[double] $M4A1LongAxis,
	[double] $M700LongAxis
)
{
	return $EffectiveLongAxis -ge $M4A1LongAxis -and
		$EffectiveLongAxis -le $M700LongAxis
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
		[Math]::Abs( $localScale[0] - $localScale[1] ) -le $scaleTolerance -and
		[Math]::Abs( $localScale[0] - $localScale[2] ) -le $scaleTolerance
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

function Get-ViewPartRows( [object] $Prefab, [string] $Label )
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

function Get-WorldRendererInfo( [object] $Prefab, [string] $Label )
{
	$matches = @(Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '' | Where-Object {
		@($_.Node.Components | Where-Object {
			$_.PSObject.Properties.Name -contains 'Model' -and
			[string]$_.Model -ceq $worldModel
		}).Count -eq 1
	})
	if ( $matches.Count -ne 1 )
	{
		throw "$Label must contain exactly one renderer owner for $worldModel; found $($matches.Count)."
	}

	return $matches[0]
}

function Get-WorldAnchorCoherence( [object] $Prefab, [object] $RendererInfo )
{
	$scaleNodes = @(Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '')
	$equipment = @($Prefab.RootObject.Components | Where-Object {
		$_.__type -ceq 'Dxura.RP.Game.Equipment'
	})
	$rows = [System.Collections.Generic.List[object]]::new()
	$coherent = $equipment.Count -eq 1

	foreach ( $contract in $anchorContracts )
	{
		$matches = @($scaleNodes | Where-Object { $_.Path -ceq $contract.Path })
		if ( $matches.Count -ne 1 )
		{
			$coherent = $false
			$rows.Add( [pscustomobject]@{
				name = $contract.Name
				path = $contract.Path
				actualPosition = $null
				expectedPosition = $null
				coherent = $false
			} )
			continue
		}

		$nodeInfo = $matches[0]
		$unitPosition = Read-Vector3 $contract.UnitPosition
		$expectedPosition = @($unitPosition | ForEach-Object {
			$_ * [double]$RendererInfo.LocalScale[0]
		})
		$actualPosition = Read-Vector3 ([string]$nodeInfo.Node.Position)
		$referenceMatches = if ( $equipment.Count -eq 1 ) {
			$reference = $equipment[0].($contract.ReferenceProperty)
			$null -ne $reference -and [string]$reference.go -ceq $contract.Guid
		} else {
			$false
		}
		$rowCoherent =
			[string]$nodeInfo.Node.__guid -ceq $contract.Guid -and
			[string]$nodeInfo.Node.Rotation -ceq '0,0,0,1' -and
			[string]$nodeInfo.Node.Scale -ceq '1,1,1' -and
			$referenceMatches -and
			(Test-VectorClose $actualPosition $expectedPosition $viewVectorTolerance)
		$coherent = $coherent -and $rowCoherent
		$rows.Add( [pscustomobject]@{
			name = $contract.Name
			path = $contract.Path
			guid = [string]$nodeInfo.Node.__guid
			actualPosition = $actualPosition
			expectedPosition = $expectedPosition
			coherent = $rowCoherent
		} )
	}

	return [pscustomobject]@{
		Coherent = $coherent
		Rows = @($rows)
	}
}

function Get-ViewAnchorCoherence( [object] $Prefab )
{
	$scaleNodes = @(Get-PrefabScaleNodes $Prefab.RootObject 1.0 $true '')
	$viewModels = @($Prefab.RootObject.Components | Where-Object {
		$_.__type -ceq 'Dxura.RP.Game.ViewModel'
	})
	$rows = [System.Collections.Generic.List[object]]::new()
	$coherent = $viewModels.Count -eq 1

	foreach ( $contract in $viewAnchorContracts )
	{
		$anchorMatches = @($scaleNodes | Where-Object { $_.Path -ceq $contract.Path })
		$wrapperMatches = @($scaleNodes | Where-Object {
			@($_.Node.Components | Where-Object {
				$_.PSObject.Properties.Name -contains 'Model' -and
				[string]$_.Model -ceq $contract.WrapperModel
			}).Count -eq 1
		})
		if ( $anchorMatches.Count -ne 1 -or $wrapperMatches.Count -ne 1 )
		{
			$coherent = $false
			$rows.Add( [pscustomobject]@{
				name = $contract.Name
				path = $contract.Path
				wrapperModel = $contract.WrapperModel
				actualPosition = $null
				expectedPosition = $null
				coherent = $false
			} )
			continue
		}

		$anchorInfo = $anchorMatches[0]
		$wrapperInfo = $wrapperMatches[0]
		$unitPosition = Read-Vector3 $contract.UnitPosition
		$expectedPosition = Transform-Point `
			$unitPosition `
			([string]$wrapperInfo.Node.Position) `
			([string]$wrapperInfo.Node.Rotation) `
			([double]$wrapperInfo.LocalScale[0])
		$actualPosition = Read-Vector3 ([string]$anchorInfo.Node.Position)
		$anchorParentPath = $anchorInfo.Path.Substring(
			0, $anchorInfo.Path.LastIndexOf( '/' ) )
		$wrapperParentPath = $wrapperInfo.Path.Substring(
			0, $wrapperInfo.Path.LastIndexOf( '/' ) )
		$referenceMatches = if ( $viewModels.Count -eq 1 ) {
			$reference = $viewModels[0].($contract.ReferenceProperty)
			$null -ne $reference -and [string]$reference.go -ceq $contract.Guid
		} else {
			$false
		}
		$rowCoherent =
			[string]$anchorInfo.Node.__guid -ceq $contract.Guid -and
			$anchorParentPath -ceq $wrapperParentPath -and
			[string]$anchorInfo.Node.Rotation -ceq [string]$wrapperInfo.Node.Rotation -and
			[string]$anchorInfo.Node.Scale -ceq '1,1,1' -and
			$referenceMatches -and
			(Test-VectorClose $actualPosition $expectedPosition $vectorTolerance)
		$coherent = $coherent -and $rowCoherent
		$rows.Add( [pscustomobject]@{
			name = $contract.Name
			path = $contract.Path
			guid = [string]$anchorInfo.Node.__guid
			wrapperPath = $wrapperInfo.Path
			actualPosition = $actualPosition
			expectedPosition = $expectedPosition
			coherent = $rowCoherent
		} )
	}

	return [pscustomobject]@{
		Coherent = $coherent
		Rows = @($rows)
	}
}

function Test-ModelDoc( [object] $Contract )
{
	$modelDocPath = Join-Path $repoRoot ('game\Assets\' + $Contract.Model.Replace( '/', '\' ))
	if ( !(Test-Path -LiteralPath $modelDocPath -PathType Leaf) )
	{
		throw "Missing SR-25 $($Contract.Name) ModelDoc: $modelDocPath"
	}

	$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $modelDocPath).Hash
	if ( $actualHash -cne $Contract.ModelDocHash )
	{
		Add-Failure "SR-25 $($Contract.Name) ModelDoc changed; remeasure the compiled geometry before accepting scale."
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
		Add-Failure "SR-25 $($Contract.Name) ModelDoc must keep one exact generated-source filename."
	}
	if ( $scaleMatches.Count -ne 1 )
	{
		Add-Failure "SR-25 $($Contract.Name) ModelDoc must keep one explicit import_scale."
	}
	else
	{
		$importScale = [double]::Parse(
			$scaleMatches[0].Groups['value'].Value,
			[Globalization.CultureInfo]::InvariantCulture )
		if ( [Math]::Abs( $importScale - 39.37008 ) -gt $scaleTolerance )
		{
			Add-Failure "SR-25 $($Contract.Name) import scale changed from the measured 39.37008 seam: $importScale."
		}
	}
}

foreach ( $requiredPath in @($sourcePath, $viewPrefabPath, $worldPrefabPath) )
{
	if ( !(Test-Path -LiteralPath $requiredPath -PathType Leaf) )
	{
		throw "Missing SR-25 cross-perspective scale input: $requiredPath"
	}
}

$actualSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
if ( $actualSourceHash -cne $expectedSourceHash )
{
	Add-Failure 'SR-25 source FBX changed; remeasure both compiled perspectives before accepting scale.'
}

foreach ( $part in $parts )
{
	Test-ModelDoc $part
}
Test-ModelDoc $worldModelDoc

$viewPrefab = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$viewRows = @(Get-ViewPartRows $viewPrefab $viewPrefabPath)
foreach ( $row in $viewRows )
{
	if ( $row.Path -cne $row.Part.Path )
	{
		Add-Failure "SR-25 $($row.Part.Name) renderer moved from $($row.Part.Path) to $($row.Path)."
	}
	if ( !$row.UniformChain )
	{
		Add-Failure "SR-25 $($row.Part.Name) view renderer has non-uniform scale in its ancestry."
	}
	if ( $row.CumulativeUniformScale -le 0.0 )
	{
		Add-Failure "SR-25 $($row.Part.Name) view renderer must keep a positive cumulative scale."
	}
	if ( [Math]::Abs( $row.CumulativeUniformScale - $row.LocalScale[0] ) -gt $scaleTolerance )
	{
		Add-Failure "SR-25 $($row.Part.Name) view scale must remain on its renderer wrapper; an ancestor also scales it."
	}
}

$viewReferenceScale = [double]$viewRows[0].CumulativeUniformScale
foreach ( $row in $viewRows )
{
	if ( [Math]::Abs( $row.CumulativeUniformScale - $viewReferenceScale ) -gt $scaleTolerance )
	{
		Add-Failure "SR-25 assembled view parts diverge in scale: $($row.Part.Name)=$($row.CumulativeUniformScale), reference=$viewReferenceScale."
	}
}

$viewAnchorCoherence = Get-ViewAnchorCoherence $viewPrefab
if ( !$viewAnchorCoherence.Coherent )
{
	Add-Failure 'SR-25 view Muzzle/EjectionPort must be derived from the pinned unit-space seeds through their visible renderer wrappers.'
}

$worldPrefab = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$worldInfo = Get-WorldRendererInfo $worldPrefab $worldPrefabPath
if ( $worldInfo.Path -cne $worldRendererPath )
{
	Add-Failure "SR-25 world renderer moved from $worldRendererPath to $($worldInfo.Path)."
}
if ( !$worldInfo.UniformChain )
{
	Add-Failure 'SR-25 world renderer has non-uniform scale in its ancestry.'
}
if ( $worldInfo.CumulativeUniformScale -le 0.0 )
{
	Add-Failure 'SR-25 world renderer must keep a positive cumulative scale.'
}
if ( [Math]::Abs( $worldInfo.CumulativeUniformScale - $worldInfo.LocalScale[0] ) -gt $scaleTolerance )
{
	Add-Failure 'SR-25 world scale must remain on the custom renderer; an ancestor also scales it.'
}

$viewEffectiveLongAxis = $sr25UnitLongAxis * [Math]::Abs( $viewReferenceScale )
$worldEffectiveLongAxis = $sr25UnitLongAxis * [Math]::Abs( $worldInfo.CumulativeUniformScale )
$viewClassRatio = $viewEffectiveLongAxis / $m4a1ViewLongAxis
$worldClassRatio = $worldEffectiveLongAxis / $m4a1WorldLongAxis
$viewM700Ratio = $viewEffectiveLongAxis / $m700ViewLongAxis
$worldM700Ratio = $worldEffectiveLongAxis / $m700WorldLongAxis
$surfaceRatioDelta = [Math]::Abs( ($worldClassRatio / $viewClassRatio) - 1.0 )
$targetWorldScale = $viewReferenceScale * ($m4a1WorldLongAxis / $m4a1ViewLongAxis)
if ( !(Test-InMarksmanDonorEnvelope $viewEffectiveLongAxis $m4a1ViewLongAxis $m700ViewLongAxis) )
{
	Add-Failure "SR-25 first-person long axis must stay between the M4A1 animation donor and M700 marksman ceiling: actual=$viewEffectiveLongAxis envelope=$m4a1ViewLongAxis..$m700ViewLongAxis."
}
if ( !(Test-InMarksmanDonorEnvelope $worldEffectiveLongAxis $m4a1WorldLongAxis $m700WorldLongAxis) )
{
	Add-Failure "SR-25 third-person long axis must stay between the M4A1 animation donor and M700 marksman ceiling: actual=$worldEffectiveLongAxis envelope=$m4a1WorldLongAxis..$m700WorldLongAxis."
}
if ( $surfaceRatioDelta -gt $allowedSurfaceRatioDelta )
{
	Add-Failure ("SR-25 world-to-view class ratio differs by {0:P2}; expected parity within {1:P0}." -f $surfaceRatioDelta, $allowedSurfaceRatioDelta)
}

$anchorCoherence = Get-WorldAnchorCoherence $worldPrefab $worldInfo
if ( !$anchorCoherence.Coherent )
{
	Add-Failure 'SR-25 world Muzzle/EjectionPort must stay on their pinned unit-space seeds scaled by the visible world renderer.'
}

$muzzleSeed = Read-Vector3 $anchorContracts[0].UnitPosition
if ( [Math]::Abs( $muzzleSeed[0] - $sr25UnitMaxX ) -gt $vectorTolerance )
{
	Add-Failure 'SR-25 muzzle seed no longer matches the measured unit-scale model tip.'
}

# In-memory negative controls prove the assertion sees the three defect classes
# it is intended to prevent. No asset file is changed by these probes.
$viewDriftProbe = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$viewDriftRows = @(Get-ViewPartRows $viewDriftProbe 'SR-25 view-part drift negative control')
$viewDriftScale = $viewReferenceScale * 1.1
$viewDriftScaleText = $viewDriftScale.ToString( '0.########', [Globalization.CultureInfo]::InvariantCulture )
$viewDriftRows[0].Node.Scale = "$viewDriftScaleText,$viewDriftScaleText,$viewDriftScaleText"
$viewDriftRows = @(Get-ViewPartRows $viewDriftProbe 'SR-25 view-part drift negative control')
$viewDriftRejected = @($viewDriftRows | Where-Object {
	[Math]::Abs( $_.CumulativeUniformScale - $viewDriftRows[1].CumulativeUniformScale ) -gt $scaleTolerance
}).Count -gt 0
if ( !$viewDriftRejected )
{
	Add-Failure 'SR-25 scale contract is blind to one diverging first-person renderer.'
}

$ancestorProbe = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$ancestorProbe.RootObject.Scale = '2.8,2.8,2.8'
$ancestorInfo = Get-WorldRendererInfo $ancestorProbe 'SR-25 ancestor-scale negative control'
$ancestorWorldRatio = ($sr25UnitLongAxis * [Math]::Abs( $ancestorInfo.CumulativeUniformScale )) / $m4a1WorldLongAxis
$ancestorDelta = [Math]::Abs( ($ancestorWorldRatio / $viewClassRatio) - 1.0 )
$ancestorRejected = $ancestorDelta -gt $allowedSurfaceRatioDelta
if ( !$ancestorRejected )
{
	Add-Failure 'SR-25 scale contract is blind to a 2.8x ancestor/root scale mutation.'
}

$negativeScaleProbe = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$negativeScaleInfo = Get-WorldRendererInfo $negativeScaleProbe 'SR-25 negative-scale control'
$negativeScaleText = (-1.0 * $targetWorldScale).ToString( '0.########', [Globalization.CultureInfo]::InvariantCulture )
$negativeScaleInfo.Node.Scale = "$negativeScaleText,$negativeScaleText,$negativeScaleText"
$negativeScaleInfo = Get-WorldRendererInfo $negativeScaleProbe 'SR-25 negative-scale control'
$negativeScaleRejected = $negativeScaleInfo.CumulativeUniformScale -le 0.0
if ( !$negativeScaleRejected )
{
	Add-Failure 'SR-25 scale contract is blind to a mirrored negative world scale.'
}

$rendererOnlyProbe = Get-Content -LiteralPath $worldPrefabPath -Raw | ConvertFrom-Json -Depth 100
$rendererOnlyInfo = Get-WorldRendererInfo $rendererOnlyProbe 'SR-25 renderer-only negative control'
$targetWorldScaleText = $targetWorldScale.ToString( '0.########', [Globalization.CultureInfo]::InvariantCulture )
$rendererOnlyInfo.Node.Scale = "$targetWorldScaleText,$targetWorldScaleText,$targetWorldScaleText"
$rendererOnlyNodes = @(Get-PrefabScaleNodes $rendererOnlyProbe.RootObject 1.0 $true '')
foreach ( $contract in $anchorContracts )
{
	$anchorMatch = @($rendererOnlyNodes | Where-Object { $_.Path -ceq $contract.Path })
	if ( $anchorMatch.Count -ne 1 )
	{
		throw "SR-25 renderer-only negative control cannot resolve $($contract.Path)."
	}
	$anchorMatch[0].Node.Position = $contract.UnitPosition
}
$rendererOnlyInfo = Get-WorldRendererInfo $rendererOnlyProbe 'SR-25 renderer-only negative control'
$rendererOnlyCoherence = Get-WorldAnchorCoherence $rendererOnlyProbe $rendererOnlyInfo
$rendererOnlyRejected = !$rendererOnlyCoherence.Coherent
if ( !$rendererOnlyRejected )
{
	Add-Failure 'SR-25 scale contract is blind to a renderer-only correction that strands functional anchors.'
}

$viewAnchorProbe = Get-Content -LiteralPath $viewPrefabPath -Raw | ConvertFrom-Json -Depth 100
$viewAnchorProbeNodes = @(Get-PrefabScaleNodes $viewAnchorProbe.RootObject 1.0 $true '')
foreach ( $contract in $viewAnchorContracts )
{
	$anchorMatch = @($viewAnchorProbeNodes | Where-Object { $_.Path -ceq $contract.Path })
	if ( $anchorMatch.Count -ne 1 )
	{
		throw "SR-25 view-anchor negative control cannot resolve $($contract.Path)."
	}
	$anchorMatch[0].Node.Position = $contract.UnitPosition
}
$viewAnchorProbeCoherence = Get-ViewAnchorCoherence $viewAnchorProbe
$viewAnchorStrandingRejected = !$viewAnchorProbeCoherence.Coherent
if ( !$viewAnchorStrandingRejected )
{
	Add-Failure 'SR-25 scale contract is blind to first-person anchors stranded at unit-space seeds.'
}

$undersizeEnvelopeProbeRejected = !(Test-InMarksmanDonorEnvelope `
	($m4a1ViewLongAxis * 0.9) `
	$m4a1ViewLongAxis `
	$m700ViewLongAxis)
if ( !$undersizeEnvelopeProbeRejected )
{
	Add-Failure 'SR-25 scale contract is blind to a rifle smaller than its M4A1 animation donor.'
}

$oversizeEnvelopeProbeRejected = !(Test-InMarksmanDonorEnvelope `
	($m700WorldLongAxis * 1.1) `
	$m4a1WorldLongAxis `
	$m700WorldLongAxis)
if ( !$oversizeEnvelopeProbeRejected )
{
	Add-Failure 'SR-25 scale contract is blind to a rifle larger than its M700 marksman ceiling.'
}

$exitCode = if ( $failures.Count -eq 0 ) { 0 } else { 1 }
$result = [ordered]@{
	contract = 'sr25-cross-perspective-scale'
	result = if ( $exitCode -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapon = 'SR-25'
	sourceSha256 = $actualSourceHash
	unitLongAxis = $sr25UnitLongAxis
	view = [ordered]@{
		parts = $viewRows.Count
		cumulativeScale = $viewReferenceScale
		effectiveLongAxis = $viewEffectiveLongAxis
		m4a1LongAxis = $m4a1ViewLongAxis
		m700LongAxis = $m700ViewLongAxis
		classRatio = $viewClassRatio
		m700Ratio = $viewM700Ratio
		withinMarksmanDonorEnvelope = Test-InMarksmanDonorEnvelope $viewEffectiveLongAxis $m4a1ViewLongAxis $m700ViewLongAxis
		anchorCoherence = $viewAnchorCoherence.Rows
	}
	world = [ordered]@{
		rendererPath = $worldInfo.Path
		localScale = $worldInfo.LocalScale
		cumulativeScale = $worldInfo.CumulativeUniformScale
		effectiveLongAxis = $worldEffectiveLongAxis
		targetScaleForViewParity = $targetWorldScale
		m4a1LongAxis = $m4a1WorldLongAxis
		m700LongAxis = $m700WorldLongAxis
		classRatio = $worldClassRatio
		m700Ratio = $worldM700Ratio
		withinMarksmanDonorEnvelope = Test-InMarksmanDonorEnvelope $worldEffectiveLongAxis $m4a1WorldLongAxis $m700WorldLongAxis
		anchorCoherence = $anchorCoherence.Rows
	}
	surfaceRatioDelta = $surfaceRatioDelta
	allowedSurfaceRatioDelta = $allowedSurfaceRatioDelta
	negativeControls = [ordered]@{
		divergingViewRendererRejected = $viewDriftRejected
		oversizeAncestorRejected = $ancestorRejected
		negativeWorldScaleRejected = $negativeScaleRejected
		rendererOnlyAnchorStrandingRejected = $rendererOnlyRejected
		viewAnchorStrandingRejected = $viewAnchorStrandingRejected
		undersizeMarksmanEnvelopeRejected = $undersizeEnvelopeProbeRejected
		oversizeMarksmanEnvelopeRejected = $oversizeEnvelopeProbeRejected
	}
	failures = @($failures)
	exitCode = $exitCode
}

$result | ConvertTo-Json -Compress -Depth 8
exit $exitCode
