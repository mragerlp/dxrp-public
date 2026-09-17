[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$probeDir = Join-Path $PSScriptRoot 'civic-length'
$speechPath = Join-Path $repoRoot 'game\Code\Addons\lifepunch\lpmorpheus\LpMorpheusSpeech.cs'
$failures = [System.Collections.Generic.List[string]]::new()

if ( !(Test-Path -LiteralPath $speechPath -PathType Leaf) )
{
	Write-Output 'RESULT civic_length=FAIL missing LpMorpheusSpeech.cs'
	exit 1
}

Push-Location -LiteralPath $probeDir
try
{
	$build = & dotnet build --nologo -v q
	if ( $LASTEXITCODE -ne 0 )
	{
		$failures.Add( 'civic-length probe failed to compile' )
		$build | ForEach-Object { $failures.Add( [string]$_ ) }
	}
	else
	{
		$run = & dotnet run --no-build --nologo
		if ( $LASTEXITCODE -ne 0 )
		{
			$failures.Add( "civic-length probe exited $LASTEXITCODE : $run" )
		}
		else
		{
			Write-Output $run
		}
	}
}
finally
{
	Pop-Location
}

if ( $failures.Count -gt 0 )
{
	$failures | ForEach-Object { Write-Output $_ }
	Write-Output 'RESULT civic_length=FAIL'
	exit 1
}

Write-Output 'RESULT civic_length=PASS'
