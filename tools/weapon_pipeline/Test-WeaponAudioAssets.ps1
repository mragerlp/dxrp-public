[CmdletBinding()]
param(
	[string[]] $WeaponNames = @(
		'ak47',
		'aks74u',
		'ar15',
		'deserteagle',
		'm870',
		'm1911',
		'sr25'
	),
	[string[]] $RequiredAudioWeapons = @(
		'ak47',
		'aks74u',
		'ar15',
		'deserteagle'
	)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$assetsRoot = Join-Path $repoRoot 'game\Assets'
$ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
$failures = [System.Collections.Generic.List[string]]::new()
$referencedSources = [System.Collections.Generic.HashSet[string]]::new(
	[System.StringComparer]::OrdinalIgnoreCase
)
$soundEventCount = 0
$waveCount = 0
$decodedWaveCount = 0
$weaponRows = [System.Collections.Generic.List[object]]::new()

if ( $null -eq $ffprobe )
{
	$failures.Add( 'ffprobe is required to prove that source audio files decode.' )
}

function Resolve-SoundSource( [string] $AssetPath )
{
	$normalized = $AssetPath.Replace( '\', '/' )
	if ( $normalized -notmatch '(?i)\.vsnd$' )
	{
		return $null
	}

	foreach ( $extension in @('.wav', '.mp3', '.ogg') )
	{
		$candidate = [IO.Path]::ChangeExtension( $normalized, $extension )
		$diskPath = Join-Path $assetsRoot ($candidate.Replace( '/', '\' ))
		if ( Test-Path -LiteralPath $diskPath -PathType Leaf )
		{
			return [ordered]@{
				AssetPath = $candidate
				DiskPath = $diskPath
			}
		}
	}

	return $null
}

function Test-WaveDecode( [IO.FileInfo] $Wave )
{
	if ( $null -eq $ffprobe )
	{
		return
	}

	$output = & $ffprobe.Source `
		-v error `
		-show_entries 'stream=codec_type,codec_name,sample_rate,channels:format=duration' `
		-of json `
		$Wave.FullName 2>&1
	$exitCode = $LASTEXITCODE
	if ( $exitCode -ne 0 )
	{
		$failures.Add( "ffprobe failed for $($Wave.FullName) with exit $exitCode`: $($output -join ' ')" )
		return
	}

	try
	{
		$probe = ($output -join [Environment]::NewLine) | ConvertFrom-Json
	}
	catch
	{
		$failures.Add( "ffprobe returned invalid JSON for $($Wave.FullName): $($_.Exception.Message)" )
		return
	}

	$audio = @($probe.streams | Where-Object { $_.codec_type -eq 'audio' })
	if ( $audio.Count -ne 1 )
	{
		$failures.Add( "$($Wave.FullName) exposes $($audio.Count) audio streams; expected exactly one." )
		return
	}
	if ( [int] $audio[0].channels -lt 1 -or [int] $audio[0].sample_rate -lt 1 )
	{
		$failures.Add( "$($Wave.FullName) has invalid channel or sample-rate metadata." )
		return
	}

	$duration = 0.0
	if ( $null -eq $probe.format -or
		![double]::TryParse(
			[string] $probe.format.duration,
			[Globalization.NumberStyles]::Float,
			[Globalization.CultureInfo]::InvariantCulture,
			[ref] $duration
		) -or
		$duration -le 0 )
	{
		$failures.Add( "$($Wave.FullName) has no positive decoded duration." )
		return
	}

	$script:decodedWaveCount++
}

foreach ( $weaponName in $WeaponNames )
{
	$weaponRoot = Join-Path $assetsRoot "addons\lifepunch\lpweapons\$weaponName"
	if ( !(Test-Path -LiteralPath $weaponRoot -PathType Container) )
	{
		$failures.Add( "Missing weapon root: addons/lifepunch/lpweapons/$weaponName" )
		continue
	}

	$soundEvents = @(
		Get-ChildItem -LiteralPath $weaponRoot -Recurse -File -Filter '*.sound'
	)
	$waves = @(
		Get-ChildItem -LiteralPath $weaponRoot -Recurse -File -Filter '*.wav'
	)
	$soundEventCount += $soundEvents.Count
	$waveCount += $waves.Count

	if ( $RequiredAudioWeapons -contains $weaponName -and $soundEvents.Count -eq 0 )
	{
		$failures.Add( "$weaponName is required to have audio but has no sound events." )
	}
	if ( $RequiredAudioWeapons -contains $weaponName -and $waves.Count -eq 0 )
	{
		$failures.Add( "$weaponName is required to have audio but has no source waves." )
	}

	foreach ( $soundEvent in $soundEvents )
	{
		try
		{
			$document = Get-Content -LiteralPath $soundEvent.FullName -Raw | ConvertFrom-Json
		}
		catch
		{
			$failures.Add( "$($soundEvent.FullName) is not valid sound-event JSON: $($_.Exception.Message)" )
			continue
		}

		$sounds = @($document.Sounds)
		if ( $sounds.Count -eq 0 )
		{
			$failures.Add( "$($soundEvent.FullName) has no source sounds." )
			continue
		}

		foreach ( $sound in $sounds )
		{
			$source = Resolve-SoundSource ([string] $sound)
			if ( $null -eq $source )
			{
				$failures.Add( "$($soundEvent.FullName) cannot resolve source asset $sound." )
				continue
			}
			[void] $referencedSources.Add( $source.AssetPath )
		}
	}

	foreach ( $wave in $waves )
	{
		Test-WaveDecode $wave
	}

	$weaponRows.Add( [ordered]@{
		weapon = $weaponName
		soundEvents = $soundEvents.Count
		sourceWaves = $waves.Count
		requiredNow = $RequiredAudioWeapons -contains $weaponName
	} )
}

$allWaveAssets = @(
	foreach ( $weaponName in $WeaponNames )
	{
		$weaponRoot = Join-Path $assetsRoot "addons\lifepunch\lpweapons\$weaponName"
		if ( Test-Path -LiteralPath $weaponRoot -PathType Container )
		{
			foreach ( $wave in Get-ChildItem -LiteralPath $weaponRoot -Recurse -File -Filter '*.wav' )
			{
				$wave.FullName.Substring( $assetsRoot.Length + 1 ).Replace( '\', '/' )
			}
		}
	}
)
$unreferencedSources = @(
	$allWaveAssets | Where-Object { !$referencedSources.Contains( $_ ) } | Sort-Object
)
$activePrefabText = @(
	foreach ( $weaponName in $WeaponNames )
	{
		$equipmentRoot = Join-Path $assetsRoot "addons\lifepunch\lpweapons\$weaponName\equipment"
		if ( Test-Path -LiteralPath $equipmentRoot -PathType Container )
		{
			foreach ( $prefab in Get-ChildItem -LiteralPath $equipmentRoot -Recurse -File -Filter '*.prefab' )
			{
				Get-Content -LiteralPath $prefab.FullName -Raw
			}
		}
	}
) -join [Environment]::NewLine
$unreferencedSoundEvents = @(
	foreach ( $weaponName in $WeaponNames )
	{
		$weaponRoot = Join-Path $assetsRoot "addons\lifepunch\lpweapons\$weaponName"
		if ( Test-Path -LiteralPath $weaponRoot -PathType Container )
		{
			foreach ( $soundEvent in Get-ChildItem -LiteralPath $weaponRoot -Recurse -File -Filter '*.sound' )
			{
				$assetPath = $soundEvent.FullName.Substring( $assetsRoot.Length + 1 ).Replace( '\', '/' )
				if ( !$activePrefabText.Contains( $assetPath ) )
				{
					$assetPath
				}
			}
		}
	}
)
$unreferencedSoundEvents = @($unreferencedSoundEvents | Sort-Object)
$missingAudioWeapons = @(
	$weaponRows | Where-Object { $_.soundEvents -eq 0 } | ForEach-Object weapon
)

$m870PrefabPath = Join-Path $assetsRoot 'addons\lifepunch\lpweapons\m870\equipment\w_m870\w_m870.prefab'
$m870EmptyShellCue = $false
if ( !(Test-Path -LiteralPath $m870PrefabPath -PathType Leaf) )
{
	$failures.Add( 'Missing M870 world prefab for reload-audio validation.' )
}
else
{
	$m870Prefab = Get-Content -LiteralPath $m870PrefabPath -Raw
	$shellCue = [regex]::Escape( 'gameplay/equipment/weapons/spaghelli/sounds/reload/shotgun_load.sound' )
	$m870EmptyShellCue = $m870Prefab -match `
		('"EmptyReloadSounds"\s*:\s*\{{\s*"0\.2"\s*:\s*"{0}"\s*\}}' -f $shellCue)
	if ( !$m870EmptyShellCue )
	{
		$failures.Add( 'M870 empty reload must play the shell-insert cue at 0.2 seconds.' )
	}
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	weapons = @($weaponRows)
	soundEvents = $soundEventCount
	sourceWaves = $waveCount
	decodedWaves = $decodedWaveCount
	referencedSources = $referencedSources.Count
	unreferencedSources = $unreferencedSources
	unreferencedSoundEvents = $unreferencedSoundEvents
	missingAudioWeapons = $missingAudioWeapons
	m870EmptyShellCue = $m870EmptyShellCue
	failures = @($failures)
}

$summary | ConvertTo-Json -Depth 6 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

Write-Output ((
	'RESULT weapon_audio=PASS weapons={0} sound_events={1} source_waves={2} ' +
	'decoded_waves={3} referenced_sources={4} unreferenced_events={5} missing_audio_weapons={6} m870_empty_shell_cue=1'
) -f $WeaponNames.Count, $soundEventCount, $waveCount, $decodedWaveCount,
	$referencedSources.Count, $unreferencedSoundEvents.Count,
	($missingAudioWeapons -join ','))
