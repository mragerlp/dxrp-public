[CmdletBinding()]
param(
	[string] $ProjectOverride = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = if ( [string]::IsNullOrWhiteSpace( $ProjectOverride ) )
{
	Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\glock\custom_pistol_9mm.wepanim'
}
elseif ( [System.IO.Path]::IsPathRooted( $ProjectOverride ) )
{
	$ProjectOverride
}
else
{
	Join-Path $repoRoot $ProjectOverride
}

$failures = [System.Collections.Generic.List[string]]::new()
$expectedScale = 0.3937008
$expectedSourceHash = '6c4643f635bca229126d2be5a22923f53f423bc01ce895b656571ae7bbd925fc'
$expectedIdleRootRotation = @(-0.0000000519631769, -0.0000000520006935, 0.708454072, 0.705756903)
$expectedFireRootRotation = @(-0.0000000519631769, -0.0000000520006935, 0.708454072, 0.705756903)
$expectedIdleRootPosition = @(10.79699, -4.300247, 3.999657)

function Add-Failure( [string] $Message )
{
	$failures.Add( $Message )
}

function Test-Near( [double] $Actual, [double] $Expected, [double] $Tolerance = 0.0000001 )
{
	return [Math]::Abs( $Actual - $Expected ) -le $Tolerance
}

function Read-Vector3( [string] $Value )
{
	$parts = @($Value.Split( ',' ))
	if ( $parts.Count -ne 3 )
	{
		throw "Expected Vector3 text, got '$Value'."
	}

	return @($parts | ForEach-Object {
		[double]::Parse( $_, [Globalization.CultureInfo]::InvariantCulture )
	})
}

function Read-Vector4( [string] $Value )
{
	$parts = @($Value.Split( ',' ))
	if ( $parts.Count -ne 4 )
	{
		throw "Expected Vector4 text, got '$Value'."
	}

	return @($parts | ForEach-Object {
		[double]::Parse( $_, [Globalization.CultureInfo]::InvariantCulture )
	})
}

if ( !(Test-Path -LiteralPath $projectPath -PathType Leaf) )
{
	throw "Missing Glock Weapon Animator project: $projectPath"
}

$root = Get-Content -LiteralPath $projectPath -Raw | ConvertFrom-Json -Depth 100
$document = $root.Document
if ( $null -eq $document )
{
	throw "Glock Weapon Animator project has no Document payload."
}

if ( $document.Source.SourceHash -cne $expectedSourceHash )
{
	Add-Failure "Source SHA-256 drifted from the pinned semantic FBX."
}
if ( $document.Source.SourceRootBoneName -cne 'weapon_root' )
{
	Add-Failure "Source root must remain weapon_root."
}
if ( ![bool]$document.Source.Compiled -or ![bool]$document.Source.PreviewHostCompiled )
{
	Add-Failure "Source model and preview host must both be compiled."
}

$scale = [double]$document.Calibration.UniformScale
if ( !(Test-Near $scale $expectedScale) )
{
	Add-Failure "Calibration UniformScale is $scale; expected $expectedScale."
}

$physicalScale = Read-Vector3 ([string]$document.Calibration.PhysicalTransform.Scale)
foreach ( $axis in 0..2 )
{
	if ( !(Test-Near $physicalScale[$axis] $expectedScale) )
	{
		Add-Failure "PhysicalTransform scale axis $axis is $($physicalScale[$axis]); expected $expectedScale."
	}
}

if ( ![bool]$document.Calibration.Confirmed )
{
	Add-Failure "Production-scale calibration is not confirmed."
}
if ( $null -eq $document.Calibration.Snapshot -or -not (Test-Near ([double]$document.Calibration.Snapshot.UniformScale) $expectedScale) )
{
	Add-Failure "Calibration snapshot does not pin the production scale."
}
if ( [int]$document.Calibration.Snapshot.Revision -ne [int]$document.Calibration.Revision )
{
	Add-Failure "Calibration snapshot revision does not match the live calibration revision."
}

foreach ( $kind in @('Grip', 'Muzzle', 'Eject') )
{
	$count = @($document.Calibration.Anchors | Where-Object Kind -ceq $kind).Count
	if ( $count -ne 1 )
	{
		Add-Failure "Expected exactly one $kind anchor; found $count."
	}
}

foreach ( $handName in @('PrimaryHand', 'SupportHand') )
{
	$hand = $document.Binding.$handName
	if ( ![bool]$hand.IsBound -or $hand.AttachedBone -cne 'weapon_root' )
	{
		Add-Failure "$handName must remain bound to weapon_root."
	}
}

$elbowPoleSpecs = @(
	[pscustomobject]@{
		BindingName = 'PrimaryElbowPole'
		TrackTarget = '@primary_elbow'
		ExpectedPosition = @(5.0, -12.0, -5.0)
		ExpectedIdlePosition = @(-1.91000152, -13.3996754, -13.5535984)
	},
	[pscustomobject]@{
		BindingName = 'SupportElbowPole'
		TrackTarget = '@support_elbow'
		ExpectedPosition = @(5.0, 12.0, -5.0)
		ExpectedIdlePosition = @(-1.70328808, -3.32620001, -4.83686495)
	}
)

$idleShoulderSpecs = @(
	[pscustomobject]@{
		TrackTarget = 'clavicle_R'
		ExpectedPosition = @(-1.42964041, -0.820115864, 2.59054518)
	},
	[pscustomobject]@{
		TrackTarget = 'clavicle_L'
		ExpectedPosition = @(-8.80687809, -6.20235491, 3.09197807)
	}
)

$idleClip = @($document.Clips | Where-Object Name -ceq 'Idle')
if ( $idleClip.Count -ne 1 )
{
	Add-Failure "Expected exactly one Idle clip; found $($idleClip.Count)."
}

$rootKeyCount = 0
foreach ( $clip in @($document.Clips | Where-Object Name -in @('Idle', 'Fire')) )
{
	$rootTracks = @($clip.Tracks | Where-Object Target -ceq 'weapon_root')
	if ( $rootTracks.Count -ne 1 )
	{
		Add-Failure "$($clip.Name) must contain exactly one weapon_root track."
		continue
	}

	foreach ( $key in @($rootTracks[0].Keys) )
	{
		$rootKeyCount++
		if ( $clip.Name -ceq 'Idle' )
		{
			$position = Read-Vector3 ([string]$key.Position)
			foreach ( $axis in 0..2 )
			{
				if ( !(Test-Near $position[$axis] $expectedIdleRootPosition[$axis]) )
				{
					Add-Failure "Idle weapon_root position axis $axis is $($position[$axis]); expected $($expectedIdleRootPosition[$axis])."
				}
			}
		}

		$rotation = Read-Vector4 ([string]$key.Rotation)
		$expectedRotation = if ( $clip.Name -ceq 'Idle' )
		{
			$expectedIdleRootRotation
		}
		else
		{
			$expectedFireRootRotation
		}
		$dot = 0.0
		foreach ( $axis in 0..3 )
		{
			$dot += $rotation[$axis] * $expectedRotation[$axis]
		}

		# The source-root bind quaternion is the measured host-space basis that composes
		# the generated muzzle to +X. Idle and Fire must share it so firing cannot flip
		# the pistol backward or present it side-on.
		if ( [Math]::Abs( $dot ) -lt 0.9999 )
		{
			Add-Failure "$($clip.Name) weapon_root key at $($key.Time)s does not match its approved host-space orientation."
		}
	}
}
if ( $rootKeyCount -ne 3 )
{
	Add-Failure "Expected exactly three authored weapon_root keys across Idle and Fire; found $rootKeyCount."
}

foreach ( $spec in $elbowPoleSpecs )
{
	$bindingPole = $document.Binding.($spec.BindingName)
	$bindingPosition = Read-Vector3 ([string]$bindingPole.Transform.Position)
	$bindingScale = Read-Vector3 ([string]$bindingPole.Transform.Scale)
	foreach ( $axis in 0..2 )
	{
		if ( !(Test-Near $bindingPosition[$axis] $spec.ExpectedPosition[$axis]) )
		{
			Add-Failure "$($spec.BindingName) position axis $axis is $($bindingPosition[$axis]); expected $($spec.ExpectedPosition[$axis])."
		}
		if ( !(Test-Near $bindingScale[$axis] 1.0) )
		{
			Add-Failure "$($spec.BindingName) scale axis $axis is $($bindingScale[$axis]); expected 1."
		}
	}

	if ( $idleClip.Count -eq 1 )
	{
		$tracks = @($idleClip[0].Tracks | Where-Object Target -ceq $spec.TrackTarget)
		if ( $tracks.Count -ne 1 -or @($tracks[0].Keys).Count -ne 1 )
		{
			Add-Failure "Expected exactly one $($spec.TrackTarget) Idle track with one key."
			continue
		}

		$keyPosition = Read-Vector3 ([string]$tracks[0].Keys[0].Position)
		$keyScale = Read-Vector3 ([string]$tracks[0].Keys[0].Scale)
		foreach ( $axis in 0..2 )
		{
			if ( !(Test-Near $keyPosition[$axis] $spec.ExpectedIdlePosition[$axis]) )
			{
				Add-Failure "$($spec.TrackTarget) Idle position axis $axis is $($keyPosition[$axis]); expected $($spec.ExpectedIdlePosition[$axis])."
			}
			if ( !(Test-Near $keyScale[$axis] 1.0) )
			{
				Add-Failure "$($spec.TrackTarget) Idle scale axis $axis is $($keyScale[$axis]); expected 1."
			}
		}
	}
}

if ( $idleClip.Count -eq 1 )
{
	foreach ( $spec in $idleShoulderSpecs )
	{
		$tracks = @($idleClip[0].Tracks | Where-Object Target -ceq $spec.TrackTarget)
		if ( $tracks.Count -ne 1 -or @($tracks[0].Keys).Count -ne 1 )
		{
			Add-Failure "Expected exactly one $($spec.TrackTarget) Idle track with one key."
			continue
		}

		$keyPosition = Read-Vector3 ([string]$tracks[0].Keys[0].Position)
		$keyScale = Read-Vector3 ([string]$tracks[0].Keys[0].Scale)
		foreach ( $axis in 0..2 )
		{
			if ( !(Test-Near $keyPosition[$axis] $spec.ExpectedPosition[$axis]) )
			{
				Add-Failure "$($spec.TrackTarget) Idle position axis $axis is $($keyPosition[$axis]); expected $($spec.ExpectedPosition[$axis])."
			}
			if ( !(Test-Near $keyScale[$axis] 1.0) )
			{
				Add-Failure "$($spec.TrackTarget) Idle scale axis $axis is $($keyScale[$axis]); expected 1."
			}
		}
	}
}

$defaultGripId = [string]$document.Binding.DefaultGripPoseId
$defaultGripCount = @($document.Binding.GripPoses | Where-Object Id -ceq $defaultGripId).Count
if ( [string]::IsNullOrWhiteSpace( $defaultGripId ) -or $defaultGripCount -ne 1 )
{
	Add-Failure "Exactly one saved grip pose must match DefaultGripPoseId."
}

$result = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	project = $projectPath
	expectedScale = $expectedScale
	actualScale = $scale
	anchors = @($document.Calibration.Anchors).Count
	gripPoses = @($document.Binding.GripPoses).Count
	rootKeys = $rootKeyCount
	failures = @($failures)
}

$result | ConvertTo-Json -Compress -Depth 6
if ( $failures.Count -gt 0 )
{
	exit 1
}
