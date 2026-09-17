[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()
$allowedMeasuredRatioDrift = 0.01
$allowedPerspectiveDelta = 0.02
$pistolClassMinimum = 0.80
$pistolClassMaximum = 1.20

# Default-pose render bounds measured read-only with sbox-native
# inspect_model_geometry on engine 26.08.19. Source and ModelDoc hashes below
# invalidate these measurements whenever an authored input changes.
$spaghelliViewLongAxis = 40.457546
$spaghelliWorldLongAxis = 40.456245
$uspViewLongAxis = 9.068356
$uspWorldLongAxis = 9.084944

$sources = @(
	[pscustomobject]@{ Name = 'M870'; Path = 'game\Assets\addons\lifepunch\lpweapons\m870\source\generated\m870_clean.fbx'; Hash = '7740C3C423EAF3F54081866833651EDC0B1FFBD9254A86937A1B4A40B3C661FD' },
	[pscustomobject]@{ Name = 'Desert Eagle world'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\source\desert_eagle_world.fbx'; Hash = 'AC3A70239AB4B81B8ECCE6FDB70FCAA7A52FEDD4BBDC9A0A937954712959A39F' },
	[pscustomobject]@{ Name = 'Desert Eagle body'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\source\desert_eagle_body.fbx'; Hash = 'E9061EEEA5D623A8BCEE9D68F8E22E22C82B9E4C26B4396AB8EFA036BAA1A2AE' },
	[pscustomobject]@{ Name = 'Desert Eagle slide'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\source\desert_eagle_slide.fbx'; Hash = '53EBB3124644686F91F9FB3DF3406DEAA1ADD66DB2B2DB9D19F08D5D6DB5D595' },
	[pscustomobject]@{ Name = 'Desert Eagle magazine'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\source\desert_eagle_magazine.fbx'; Hash = '88C3124612D2E88E88F7A7B8F91EB4E919C36F7B36FAD7EFF7B00AE94EC377F5' },
	[pscustomobject]@{ Name = 'M1911 world'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\source\generated\m1911_world.fbx'; Hash = 'D97562BF2A5031A80276D097F023C58121205128BF8AE135388A50B1DC4FED19' },
	[pscustomobject]@{ Name = 'M1911 body'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\source\generated\m1911_body.fbx'; Hash = '0CF961714E4524D3E7EA8DDCE34F0EDB9BBEDDBCCFDFD810782A9944D5BC593F' },
	[pscustomobject]@{ Name = 'M1911 slide'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\source\generated\m1911_slide.fbx'; Hash = 'F632CE9F54FC711788E64C9F198B87DDA51973EFB632179E122DA8E8691A3D86' },
	[pscustomobject]@{ Name = 'M1911 magazine'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\source\generated\m1911_magazine.fbx'; Hash = '18557FBDD38286B02D4EF87AAB513208A38B62385BE078000581261BA41791EF' }
)

$modelDocs = @(
	[pscustomobject]@{ Name = 'M870 world'; Path = 'game\Assets\addons\lifepunch\lpweapons\m870\models\m870_world.vmdl'; Hash = '45647A948B23CC0819C39244322DA7738773BC196EAE127F41F071738EEE0BAF'; Source = 'addons/lifepunch/lpweapons/m870/source/generated/m870_clean.fbx'; ImportScale = 0.3937008 },
	[pscustomobject]@{ Name = 'M870 body'; Path = 'game\Assets\addons\lifepunch\lpweapons\m870\models\m870_body.vmdl'; Hash = '2106FD9E01FFDC8B21357FC09824D850EFAF5A4BF82D3588D6F9192E339A0E87'; Source = 'addons/lifepunch/lpweapons/m870/source/generated/m870_clean.fbx'; ImportScale = 0.3937008 },
	[pscustomobject]@{ Name = 'M870 pump'; Path = 'game\Assets\addons\lifepunch\lpweapons\m870\models\m870_pump.vmdl'; Hash = '98FA09FD7C236702DF213E7137F666516135E932736962FA34F2C7B95868A27B'; Source = 'addons/lifepunch/lpweapons/m870/source/generated/m870_clean.fbx'; ImportScale = 0.3937008 },
	[pscustomobject]@{ Name = 'Desert Eagle world'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\Desert Eagle.vmdl'; Hash = '2E32CD809A3FB86A53D282C47821D14B3D01AEC3089CD33619BABB4FAA30B9C5'; Source = 'addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_world.fbx'; ImportScale = 0.03937008 },
	[pscustomobject]@{ Name = 'Desert Eagle body'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\desert_eagle_body.vmdl'; Hash = '4BDF2FF3A9F851F535CF9026745528570B41F32A0F6F911F1C0066C33EB6B167'; Source = 'addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_body.fbx'; ImportScale = 0.03937008 },
	[pscustomobject]@{ Name = 'Desert Eagle slide'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\desert_eagle_slide.vmdl'; Hash = '0CD7B832625A3E75E1B6C9803753AAAFAB542423D22E00DCCBE2ABE156A616D8'; Source = 'addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_slide.fbx'; ImportScale = 0.03937008 },
	[pscustomobject]@{ Name = 'Desert Eagle magazine'; Path = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\desert_eagle_magazine.vmdl'; Hash = 'B1C349C1BCB35F57E9221B66BD60AAF856D24C1638FB5D56A3C25C57034A1B79'; Source = 'addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_magazine.fbx'; ImportScale = 0.03937008 },
	[pscustomobject]@{ Name = 'M1911 world'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\models\m1911_world.vmdl'; Hash = '71EDF01DC311FDD35E169613FCD5DC0E2BB9583A8715826A934012AD5D9B1BCB'; Source = 'addons/lifepunch/lpweapons/m1911/source/generated/m1911_world.fbx'; ImportScale = 1.0 },
	[pscustomobject]@{ Name = 'M1911 body'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\models\m1911_body.vmdl'; Hash = '6290422E22E170B7D2B2A0C550F2F42349B67C1679A98DAE0C3354D33A8EE63B'; Source = 'addons/lifepunch/lpweapons/m1911/source/generated/m1911_body.fbx'; ImportScale = 1.0 },
	[pscustomobject]@{ Name = 'M1911 slide'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\models\m1911_slide.vmdl'; Hash = '72D0D8525BA146FB0AE67485BD33E524CC2339E6729DEDA5FD8627B301D37C10'; Source = 'addons/lifepunch/lpweapons/m1911/source/generated/m1911_slide.fbx'; ImportScale = 1.0 },
	[pscustomobject]@{ Name = 'M1911 magazine'; Path = 'game\Assets\addons\lifepunch\lpweapons\m1911\models\m1911_magazine.vmdl'; Hash = '9ED95532DE6AD603B4CA3EF8BF6C385A94D677214AC0B1166D2F95FBF181CBA6'; Source = 'addons/lifepunch/lpweapons/m1911/source/generated/m1911_magazine.fbx'; ImportScale = 1.0 }
)

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Assert-FileHash( [object] $Asset )
{
	$path = Join-Path $repoRoot $Asset.Path
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) )
	{
		throw "Missing measured input: $path"
	}

	$actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
	if ( $actual -cne $Asset.Hash )
	{
		Add-Failure "$($Asset.Name) changed; remeasure its compiled default-pose bounds."
	}
}

function Assert-ModelDoc( [object] $Asset )
{
	Assert-FileHash $Asset
	$path = Join-Path $repoRoot $Asset.Path
	$text = Get-Content -LiteralPath $path -Raw
	$sourceMatches = [regex]::Matches( $text, '(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$' )
	$scaleMatches = [regex]::Matches( $text, '(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$' )
	if ( $sourceMatches.Count -ne 1 -or $sourceMatches[0].Groups['value'].Value -cne $Asset.Source )
	{
		Add-Failure "$($Asset.Name) must keep one exact measured source filename."
	}
	if ( $scaleMatches.Count -ne 1 )
	{
		Add-Failure "$($Asset.Name) must keep one explicit import_scale."
	}
	else
	{
		$actualScale = [double]::Parse( $scaleMatches[0].Groups['value'].Value, [Globalization.CultureInfo]::InvariantCulture )
		if ( [Math]::Abs( $actualScale - [double]$Asset.ImportScale ) -gt 0.000000001 )
		{
			Add-Failure "$($Asset.Name) import_scale changed from $($Asset.ImportScale) to $actualScale."
		}
	}
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

function Get-ScaleNodes(
	[object] $Node,
	[double] $ParentUniformScale,
	[bool] $ParentChainIsUniform,
	[string] $ParentPath
)
{
	$localScale = Read-Vector3 ([string]$Node.Scale)
	$localIsUniform =
		[Math]::Abs( $localScale[0] - $localScale[1] ) -le 0.000000001 -and
		[Math]::Abs( $localScale[0] - $localScale[2] ) -le 0.000000001
	$chainIsUniform = $ParentChainIsUniform -and $localIsUniform
	$cumulativeScale = $ParentUniformScale * [double]$localScale[0]
	$path = if ( [string]::IsNullOrWhiteSpace( $ParentPath ) ) { [string]$Node.Name } else { "$ParentPath/$($Node.Name)" }

	[pscustomobject]@{
		Node = $Node
		Path = $path
		LocalScale = $localScale
		CumulativeScale = $cumulativeScale
		UniformChain = $chainIsUniform
	}

	foreach ( $child in @($Node.Children) )
	{
		Get-ScaleNodes $child $cumulativeScale $chainIsUniform $path
	}
}

function Get-RendererRows( [object] $Prefab, [object[]] $Parts, [string] $Label )
{
	$nodes = @(Get-ScaleNodes $Prefab.RootObject 1.0 $true '')
	$rows = [System.Collections.Generic.List[object]]::new()
	foreach ( $part in $Parts )
	{
		$matches = @($nodes | Where-Object {
			@($_.Node.Components | Where-Object {
				$_.PSObject.Properties.Name -contains 'Model' -and [string]$_.Model -ceq $part.Model
			}).Count -eq 1
		})
		if ( $matches.Count -ne 1 )
		{
			throw "$Label must contain exactly one renderer for $($part.Model); found $($matches.Count)."
		}
		$match = $matches[0]
		if ( $match.Path -cne $part.Path )
		{
			Add-Failure "$Label renderer moved from $($part.Path) to $($match.Path)."
		}
		if ( !$match.UniformChain )
		{
			Add-Failure "$Label renderer has a non-uniform scale chain: $($match.Path)."
		}
		$rows.Add( [pscustomobject]@{
			Name = $part.Name
			Model = $part.Model
			Path = $match.Path
			Node = $match.Node
			LocalScale = $match.LocalScale
			CumulativeScale = $match.CumulativeScale
		} )
	}
	return @($rows)
}

foreach ( $source in $sources ) { Assert-FileHash $source }
foreach ( $modelDoc in $modelDocs ) { Assert-ModelDoc $modelDoc }

$m870ViewPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\m870\equipment\vm_m870\vm_m870.prefab'
$m870WorldPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\m870\equipment\w_m870\w_m870.prefab'
$deViewPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
$deWorldPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\w_desert_eagle\w_desert_eagle.prefab'
$m1911ViewPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\m1911\equipment\vm_m1911\vm_m1911.prefab'
$m1911WorldPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\m1911\equipment\w_m1911\w_m1911.prefab'

foreach ( $path in @($m870ViewPath, $m870WorldPath, $deViewPath, $deWorldPath, $m1911ViewPath, $m1911WorldPath) )
{
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) ) { throw "Missing weapon prefab: $path" }
}

$m870ViewParts = @(
	[pscustomobject]@{ Name = 'body'; Model = 'addons/lifepunch/lpweapons/m870/models/m870_body.vmdl'; Path = 'vm_m870/weapon_root/weapon_root_children/m870_body' },
	[pscustomobject]@{ Name = 'pump'; Model = 'addons/lifepunch/lpweapons/m870/models/m870_pump.vmdl'; Path = 'vm_m870/weapon_root/weapon_root_children/m870_pump' }
)
$m870WorldParts = @(
	[pscustomobject]@{ Name = 'world'; Model = 'addons/lifepunch/lpweapons/m870/models/m870_world.vmdl'; Path = 'w_m870/Model/m870_world' }
)
$deViewParts = @(
	[pscustomobject]@{ Name = 'body'; Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl'; Path = 'vm_desert_eagle/weapon_root/weapon_root_children/desert_eagle_body' },
	[pscustomobject]@{ Name = 'slide'; Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl'; Path = 'vm_desert_eagle/weapon_root/weapon_root_children/slide/desert_eagle_slide' },
	[pscustomobject]@{ Name = 'magazine'; Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl'; Path = 'vm_desert_eagle/weapon_root/weapon_root_children/magazine/desert_eagle_magazine_bind_wrapper/desert_eagle_magazine' }
)
$deWorldParts = @(
	[pscustomobject]@{ Name = 'world'; Model = 'addons/lifepunch/lpweapons/deserteagle/Desert Eagle.vmdl'; Path = 'w_desert_eagle/Model/desert_eagle_mesh' }
)
$m1911ViewParts = @(
	[pscustomobject]@{ Name = 'body'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl'; Path = 'vm_m1911/weapon_root/weapon_root_children/m1911_body' },
	[pscustomobject]@{ Name = 'slide'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl'; Path = 'vm_m1911/weapon_root/weapon_root_children/slide/m1911_slide' },
	[pscustomobject]@{ Name = 'magazine'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl'; Path = 'vm_m1911/weapon_root/weapon_root_children/magazine/m1911_magazine' }
)
$m1911WorldParts = @(
	[pscustomobject]@{ Name = 'world'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_world.vmdl'; Path = 'w_m1911/Model' }
)

$m870View = Get-Content -LiteralPath $m870ViewPath -Raw | ConvertFrom-Json -Depth 100
$m870World = Get-Content -LiteralPath $m870WorldPath -Raw | ConvertFrom-Json -Depth 100
$deView = Get-Content -LiteralPath $deViewPath -Raw | ConvertFrom-Json -Depth 100
$deWorld = Get-Content -LiteralPath $deWorldPath -Raw | ConvertFrom-Json -Depth 100
$m1911View = Get-Content -LiteralPath $m1911ViewPath -Raw | ConvertFrom-Json -Depth 100
$m1911World = Get-Content -LiteralPath $m1911WorldPath -Raw | ConvertFrom-Json -Depth 100

$m870ViewRows = @(Get-RendererRows $m870View $m870ViewParts 'M870 view')
$m870WorldRows = @(Get-RendererRows $m870World $m870WorldParts 'M870 world')
$deViewRows = @(Get-RendererRows $deView $deViewParts 'Desert Eagle view')
$deWorldRows = @(Get-RendererRows $deWorld $deWorldParts 'Desert Eagle world')
$m1911ViewRows = @(Get-RendererRows $m1911View $m1911ViewParts 'M1911 view')
$m1911WorldRows = @(Get-RendererRows $m1911World $m1911WorldParts 'M1911 world')

foreach ( $group in @($m870ViewRows, $m870WorldRows, $deViewRows, $deWorldRows, $m1911ViewRows, $m1911WorldRows) )
{
	$referenceScale = [double]$group[0].CumulativeScale
	foreach ( $row in $group )
	{
		if ( [double]$row.CumulativeScale -le 0.0 )
		{
			Add-Failure "$($row.Path) must keep a positive cumulative scale."
		}
		if ( [Math]::Abs( [double]$row.CumulativeScale - $referenceScale ) -gt 0.000001 )
		{
			Add-Failure "$($row.Path) diverges from the other assembled view renderers."
		}
	}
}

$m870UnitLongAxis = 39.71254
$deUnitLongAxis = 10.278969
$m1911UnitLongAxis = 9.16087

$m870ViewRatio = ($m870UnitLongAxis * [Math]::Abs( [double]$m870ViewRows[0].CumulativeScale )) / $spaghelliViewLongAxis
$m870WorldRatio = ($m870UnitLongAxis * [Math]::Abs( [double]$m870WorldRows[0].CumulativeScale )) / $spaghelliWorldLongAxis
$deViewRatio = ($deUnitLongAxis * [Math]::Abs( [double]$deViewRows[0].CumulativeScale )) / $uspViewLongAxis
$deWorldRatio = ($deUnitLongAxis * [Math]::Abs( [double]$deWorldRows[0].CumulativeScale )) / $uspWorldLongAxis
$m1911ViewRatio = ($m1911UnitLongAxis * [Math]::Abs( [double]$m1911ViewRows[0].CumulativeScale )) / $uspViewLongAxis
$m1911WorldRatio = ($m1911UnitLongAxis * [Math]::Abs( [double]$m1911WorldRows[0].CumulativeScale )) / $uspWorldLongAxis

$measuredSurfaces = @(
	[pscustomobject]@{ Name = 'M870 view'; Ratio = $m870ViewRatio; Expected = 1.0 },
	[pscustomobject]@{ Name = 'M870 world'; Ratio = $m870WorldRatio; Expected = 1.0 },
	[pscustomobject]@{ Name = 'Desert Eagle view'; Ratio = $deViewRatio; Expected = 1.1334986187132485 },
	[pscustomobject]@{ Name = 'Desert Eagle world'; Ratio = $deWorldRatio; Expected = 1.1314289884450581 },
	[pscustomobject]@{ Name = 'M1911 view'; Ratio = $m1911ViewRatio; Expected = 1.0102018491554587 },
	[pscustomobject]@{ Name = 'M1911 world'; Ratio = $m1911WorldRatio; Expected = 1.008357343754678 }
)
foreach ( $surface in $measuredSurfaces )
{
	$drift = [Math]::Abs( ([double]$surface.Ratio / [double]$surface.Expected) - 1.0 )
	if ( $drift -gt $allowedMeasuredRatioDrift )
	{
		Add-Failure "$($surface.Name) ratio drifted by $drift from its measured class-relative baseline."
	}
}

foreach ( $surface in @(
	[pscustomobject]@{ Name = 'Desert Eagle view'; Ratio = $deViewRatio },
	[pscustomobject]@{ Name = 'Desert Eagle world'; Ratio = $deWorldRatio },
	[pscustomobject]@{ Name = 'M1911 view'; Ratio = $m1911ViewRatio },
	[pscustomobject]@{ Name = 'M1911 world'; Ratio = $m1911WorldRatio }
) )
{
	if ( [double]$surface.Ratio -lt $pistolClassMinimum -or [double]$surface.Ratio -gt $pistolClassMaximum )
	{
		Add-Failure "$($surface.Name) is $($surface.Ratio)x the USP baseline; expected the ruled 0.80x-1.20x pistol envelope."
	}
}

$dePerspectiveDelta = [Math]::Abs( $deViewRatio - $deWorldRatio ) / [Math]::Max( $deViewRatio, $deWorldRatio )
$m1911PerspectiveDelta = [Math]::Abs( $m1911ViewRatio - $m1911WorldRatio ) / [Math]::Max( $m1911ViewRatio, $m1911WorldRatio )
if ( $dePerspectiveDelta -gt $allowedPerspectiveDelta ) { Add-Failure "Desert Eagle view/world class ratios diverge by $dePerspectiveDelta." }
if ( $m1911PerspectiveDelta -gt $allowedPerspectiveDelta ) { Add-Failure "M1911 view/world class ratios diverge by $m1911PerspectiveDelta." }

# In-memory negative controls prove the contract rejects a hidden 2.8x root and
# a one-perspective-only pistol scale detour without touching the prefabs.
$m870OversizeProbe = Get-Content -LiteralPath $m870WorldPath -Raw | ConvertFrom-Json -Depth 100
$m870OversizeProbe.RootObject.Scale = '2.8,2.8,2.8'
$m870OversizeRows = @(Get-RendererRows $m870OversizeProbe $m870WorldParts 'M870 oversize negative control')
$m870OversizeRatio = ($m870UnitLongAxis * [Math]::Abs( [double]$m870OversizeRows[0].CumulativeScale )) / $spaghelliWorldLongAxis
$m870OversizeRejected = [Math]::Abs( $m870OversizeRatio - 1.0 ) -gt $allowedMeasuredRatioDrift
if ( !$m870OversizeRejected ) { Add-Failure 'M870 scale contract is blind to a 2.8x ancestor mutation.' }

$m870IdentityProbe = Get-Content -LiteralPath $m870WorldPath -Raw | ConvertFrom-Json -Depth 100
$m870IdentityRows = @(Get-RendererRows $m870IdentityProbe $m870WorldParts 'M870 identity-scale negative control')
$m870IdentityRows[0].Node.Scale = '1,1,1'
$m870IdentityRows = @(Get-RendererRows $m870IdentityProbe $m870WorldParts 'M870 identity-scale negative control')
$m870IdentityRatio = ($m870UnitLongAxis * [Math]::Abs( [double]$m870IdentityRows[0].CumulativeScale )) / $spaghelliWorldLongAxis
$m870IdentityRejected = [Math]::Abs( $m870IdentityRatio - 1.0 ) -gt $allowedMeasuredRatioDrift
if ( !$m870IdentityRejected ) { Add-Failure 'M870 scale contract is blind to an identity world-renderer regression.' }

$splitPartProbe = Get-Content -LiteralPath $deViewPath -Raw | ConvertFrom-Json -Depth 100
$splitPartRows = @(Get-RendererRows $splitPartProbe $deViewParts 'Desert Eagle split-part negative control')
$splitPartRows[0].Node.Scale = '1.1,1.1,1.1'
$splitPartRows = @(Get-RendererRows $splitPartProbe $deViewParts 'Desert Eagle split-part negative control')
$splitPartMutationRejected = [Math]::Abs( [double]$splitPartRows[0].CumulativeScale - [double]$splitPartRows[1].CumulativeScale ) -gt 0.000001
if ( !$splitPartMutationRejected ) { Add-Failure 'Split-pistol scale contract is blind to a 10% part-only mutation.' }

$negativeScaleProbe = Get-Content -LiteralPath $m1911WorldPath -Raw | ConvertFrom-Json -Depth 100
$negativeScaleRows = @(Get-RendererRows $negativeScaleProbe $m1911WorldParts 'M1911 negative-scale control')
$negativeScaleRows[0].Node.Scale = '-1,-1,-1'
$negativeScaleRows = @(Get-RendererRows $negativeScaleProbe $m1911WorldParts 'M1911 negative-scale control')
$negativeWorldScaleRejected = [double]$negativeScaleRows[0].CumulativeScale -le 0.0
if ( !$negativeWorldScaleRejected ) { Add-Failure 'Pistol scale contract is blind to a negative world-renderer scale.' }

$deDivergenceProbe = Get-Content -LiteralPath $deViewPath -Raw | ConvertFrom-Json -Depth 100
$deDivergenceProbe.RootObject.Scale = '1.1,1.1,1.1'
$deDivergenceRows = @(Get-RendererRows $deDivergenceProbe $deViewParts 'Desert Eagle divergence negative control')
$deDivergenceRatio = ($deUnitLongAxis * [Math]::Abs( [double]$deDivergenceRows[0].CumulativeScale )) / $uspViewLongAxis
$deDivergenceDelta = [Math]::Abs( $deDivergenceRatio - $deWorldRatio ) / [Math]::Max( $deDivergenceRatio, $deWorldRatio )
$deDivergenceRejected = $deDivergenceDelta -gt $allowedPerspectiveDelta
if ( !$deDivergenceRejected ) { Add-Failure 'Pistol scale contract is blind to a one-perspective-only 1.10x mutation.' }

$result = [ordered]@{
	contract = 'remaining-weapon-class-scale'
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	measurements = [ordered]@{
		m870 = [ordered]@{ viewRatio = $m870ViewRatio; worldRatio = $m870WorldRatio; viewScale = $m870ViewRows[0].CumulativeScale; worldScale = $m870WorldRows[0].CumulativeScale }
		desertEagle = [ordered]@{ viewRatio = $deViewRatio; worldRatio = $deWorldRatio; perspectiveDelta = $dePerspectiveDelta; viewScale = $deViewRows[0].CumulativeScale; worldScale = $deWorldRows[0].CumulativeScale }
		m1911 = [ordered]@{ viewRatio = $m1911ViewRatio; worldRatio = $m1911WorldRatio; perspectiveDelta = $m1911PerspectiveDelta; viewScale = $m1911ViewRows[0].CumulativeScale; worldScale = $m1911WorldRows[0].CumulativeScale }
	}
	negativeControls = [ordered]@{
		m870OversizeRatio = $m870OversizeRatio
		m870OversizeRejected = $m870OversizeRejected
		m870IdentityRatio = $m870IdentityRatio
		m870IdentityRejected = $m870IdentityRejected
		splitPartMutationRejected = $splitPartMutationRejected
		negativeWorldScaleRejected = $negativeWorldScaleRejected
		pistolDivergenceDelta = $deDivergenceDelta
		pistolDivergenceRejected = $deDivergenceRejected
	}
	thresholds = [ordered]@{ measuredRatioDrift = $allowedMeasuredRatioDrift; perspectiveDelta = $allowedPerspectiveDelta; pistolMinimum = $pistolClassMinimum; pistolMaximum = $pistolClassMaximum }
	failures = @($failures)
	exitCode = if ( $failures.Count -eq 0 ) { 0 } else { 1 }
}

$result | ConvertTo-Json -Compress -Depth 7
if ( $failures.Count -gt 0 ) { exit 1 }
