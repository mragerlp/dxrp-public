[CmdletBinding()]
param(
	[string] $OutputOverride = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$outputPath = if ( [string]::IsNullOrWhiteSpace( $OutputOverride ) )
{
	Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\glock\generated\custom_pistol_9mm'
}
elseif ( [System.IO.Path]::IsPathRooted( $OutputOverride ) )
{
	$OutputOverride
}
else
{
	Join-Path $repoRoot $OutputOverride
}

$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Get-Sha256( [string] $Path )
{
	return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-JsonFile( [string] $Path, [string] $Label )
{
	if ( !(Test-Path -LiteralPath $Path -PathType Leaf) )
	{
		Add-Failure "Missing $Label at $Path."
		return $null
	}

	try
	{
		return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 100
	}
	catch
	{
		Add-Failure "Invalid JSON in $Label`: $($_.Exception.Message)"
		return $null
	}
}

function Get-ObjectProperty( [object] $Object, [string] $Name )
{
	if ( $null -eq $Object )
	{
		return $null
	}

	$property = $Object.PSObject.Properties[$Name]
	if ( $null -eq $property )
	{
		return $null
	}

	return $property.Value
}

function Find-NodesByName( [object] $Node, [string] $Name )
{
	if ( $null -eq $Node )
	{
		return
	}

	if ( (Get-ObjectProperty $Node 'Name') -ceq $Name )
	{
		$Node
	}

	foreach ( $child in @((Get-ObjectProperty $Node 'Children')) )
	{
		Find-NodesByName $child $Name
	}
}

function Find-ComponentsByType( [object] $Node, [string] $TypeName )
{
	if ( $null -eq $Node )
	{
		return
	}

	foreach ( $component in @((Get-ObjectProperty $Node 'Components')) )
	{
		if ( (Get-ObjectProperty $component '__type') -ceq $TypeName )
		{
			$component
		}
	}

	foreach ( $child in @((Get-ObjectProperty $Node 'Children')) )
	{
		Find-ComponentsByType $child $TypeName
	}
}

$manifestPath = Join-Path $outputPath 'weaponanim.manifest.json'
$manifest = Read-JsonFile $manifestPath 'Weapon Animator manifest'

if ( $null -ne $manifest )
{
	if ( [string]$manifest.GeneratorVersion -cne '2.1.0' )
	{
		Add-Failure "GeneratorVersion is '$($manifest.GeneratorVersion)'; expected 2.1.0."
	}
	if ( [string]::IsNullOrWhiteSpace( [string]$manifest.InputHash ) )
	{
		Add-Failure 'Manifest InputHash is empty.'
	}

	$seenPaths = [System.Collections.Generic.HashSet[string]]::new( [StringComparer]::OrdinalIgnoreCase )
	foreach ( $file in @($manifest.Files) )
	{
		$relativePath = [string]$file.RelativePath
		if ( [string]::IsNullOrWhiteSpace( $relativePath ) )
		{
			Add-Failure 'Manifest contains an empty RelativePath.'
			continue
		}
		if ( [System.IO.Path]::IsPathRooted( $relativePath ) -or ($relativePath -split '[\\/]') -contains '..' )
		{
			Add-Failure "Manifest path escapes the generated folder: $relativePath"
			continue
		}
		if ( !$seenPaths.Add( $relativePath ) )
		{
			Add-Failure "Manifest path is duplicated: $relativePath"
			continue
		}

		$filePath = Join-Path $outputPath ($relativePath -replace '/', '\')
		if ( !(Test-Path -LiteralPath $filePath -PathType Leaf) )
		{
			Add-Failure "Manifest-owned file is missing: $relativePath"
			continue
		}

		$actualHash = Get-Sha256 $filePath
		if ( $actualHash -cne ([string]$file.Sha256).ToLowerInvariant() )
		{
			Add-Failure "Manifest hash mismatch: $relativePath"
		}
	}

	foreach ( $severity in @('Error', 'Warning') )
	{
		$count = @($manifest.Diagnostics | Where-Object Severity -ceq $severity).Count
		if ( $count -gt 0 )
		{
			Add-Failure "Generation diagnostics contain $count $severity entry or entries."
		}
	}

	foreach ( $code in @('compile.ok', 'inspect.bind_scale_normalized', 'inspect.sequences', 'inspect.animgraph') )
	{
		if ( @($manifest.Diagnostics | Where-Object Code -ceq $code).Count -lt 1 )
		{
			Add-Failure "Generation diagnostics are missing $code."
		}
	}
}

$compiledCompanions = @(
	'custom_pistol_9mm_vm_bootstrap.vmdl_c',
	'custom_pistol_9mm_vm.vmdl_c',
	'custom_pistol_9mm.vanmgrph_c',
	'v_custom_pistol_9mm.prefab_c',
	'materials\custom_pistol_9mm_custompistol9mm.vmat_c',
	'materials\custom_pistol_9mm_magazin_bullet_magazine.vmat_c',
	'materials\custom_pistol_9mm_supressorver1_material.vmat_c'
)
foreach ( $relativePath in $compiledCompanions )
{
	$filePath = Join-Path $outputPath $relativePath
	if ( !(Test-Path -LiteralPath $filePath -PathType Leaf) -or (Get-Item -LiteralPath $filePath).Length -le 0 )
	{
		Add-Failure "Compiled companion is missing or empty: $relativePath"
	}
}

$modelPath = Join-Path $outputPath 'custom_pistol_9mm_vm.vmdl'
if ( Test-Path -LiteralPath $modelPath -PathType Leaf )
{
	$modelText = Get-Content -LiteralPath $modelPath -Raw
	foreach ( $requiredText in @(
		'filename = "addons/lifepunch/lpweapons/glock/source/custom_pistol_9mm_semantic.fbx"',
		'import_scale = 0.393701',
		'name = "fire"',
		'name = "idle"',
		'name = "muzzle"',
		'name = "eject"',
		'anim_graph_name = "addons/lifepunch/lpweapons/glock/generated/custom_pistol_9mm/custom_pistol_9mm.vanmgrph"'
	))
	{
		if ( !$modelText.Contains( $requiredText, [StringComparison]::Ordinal ) )
		{
			Add-Failure "Generated view model is missing: $requiredText"
		}
	}
}
else
{
	Add-Failure "Missing generated view model: $modelPath"
}

$graphPath = Join-Path $outputPath 'custom_pistol_9mm.vanmgrph'
if ( Test-Path -LiteralPath $graphPath -PathType Leaf )
{
	$graphText = Get-Content -LiteralPath $graphPath -Raw
	foreach ( $requiredText in @(
		'm_sName = "seq_Fire"',
		'm_sequenceName = "fire"',
		'm_name = "b_attack"',
		'm_name = "b_reload"',
		'm_name = "b_empty"',
		'm_name = "ironsights"',
		'm_name = "move_bob"',
		'm_name = "firing_mode"'
	))
	{
		if ( !$graphText.Contains( $requiredText, [StringComparison]::Ordinal ) )
		{
			Add-Failure "Generated AnimGraph is missing: $requiredText"
		}
	}
}
else
{
	Add-Failure "Missing generated AnimGraph: $graphPath"
}

$prefabPath = Join-Path $outputPath 'v_custom_pistol_9mm.prefab'
$prefab = Read-JsonFile $prefabPath 'generated Glock prefab'
$rootRenderer = $null
$armsRenderer = $null
if ( $null -ne $prefab )
{
	$renderers = @(Find-ComponentsByType $prefab.RootObject 'Sandbox.SkinnedModelRenderer')
	$rootRenderer = @($renderers | Where-Object Model -ceq 'addons/lifepunch/lpweapons/glock/generated/custom_pistol_9mm/custom_pistol_9mm_vm.vmdl') | Select-Object -First 1
	$armsRenderer = @($renderers | Where-Object Model -ceq 'models/first_person/v_first_person_arms_human.vmdl') | Select-Object -First 1

	if ( $null -eq $rootRenderer -or ![bool]$rootRenderer.UseAnimGraph )
	{
		Add-Failure 'Generated prefab lacks an AnimGraph-enabled generated Glock renderer.'
	}
	if ( $null -eq $armsRenderer -or ![bool]$armsRenderer.UseAnimGraph )
	{
		Add-Failure 'Generated prefab lacks AnimGraph-enabled first-person arms.'
	}
	elseif ( $null -eq $rootRenderer -or [string]$armsRenderer.BoneMergeTarget.component_id -cne [string]$rootRenderer.__guid )
	{
		Add-Failure 'Generated arms are not bone-merged to the generated Glock renderer.'
	}

	foreach ( $name in @('camera', 'muzzle', 'eject') )
	{
		if ( @(Find-NodesByName $prefab.RootObject $name).Count -ne 1 )
		{
			Add-Failure "Generated prefab must contain exactly one $name object."
		}
	}
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	output = $outputPath
	manifestFiles = if ( $null -eq $manifest ) { 0 } else { @($manifest.Files).Count }
	diagnostics = if ( $null -eq $manifest ) { 0 } else { @($manifest.Diagnostics).Count }
	compiledCompanions = $compiledCompanions.Count
	rootRenderer = $null -ne $rootRenderer
	armsRenderer = $null -ne $armsRenderer
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 6
if ( $failures.Count -gt 0 )
{
	exit 1
}
