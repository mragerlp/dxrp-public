[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testProject = Join-Path $repositoryRoot 'tools\lp-weapon-attachment-contract\LpWeaponAttachmentContract.Tests.csproj'
$testProgram = Join-Path $repositoryRoot 'tools\lp-weapon-attachment-contract\Program.cs'
$productionRoot = Join-Path $repositoryRoot 'game\Code\Addons\lifepunch\lpweapons\attachments'

$requiredProductionFiles = @(
    (Join-Path $repositoryRoot 'game\Code\Equipment\Weapon\WeaponPresentationContracts.cs')
    (Join-Path $productionRoot 'WeaponAttachmentContract.cs')
    (Join-Path $productionRoot 'WeaponAttachmentState.cs')
    (Join-Path $productionRoot 'WeaponAttachmentController.cs')
    (Join-Path $productionRoot 'WeaponAttachmentRenderController.cs')
)

$requiredExistingIntegrationFiles = @(
    (Join-Path $repositoryRoot 'game\Code\Equipment\DroppedEquipment.cs')
    (Join-Path $repositoryRoot 'game\Code\Equipment\Weapon\ShootWeaponComponent.cs')
)

$missingHarnessFiles = @(@($testProject, $testProgram) | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
$missingIntegrationFiles = @($requiredExistingIntegrationFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })

if ($missingHarnessFiles.Count -gt 0 -or $missingIntegrationFiles.Count -gt 0) {
    [ordered]@{
        contract = 'lp-weapon-attachment-runtime'
        status = 'HARNESS_ERROR'
        proofCeiling = 'pure-contract-and-source-shape'
        exitCode = 2
        missingHarnessFiles = @($missingHarnessFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($repositoryRoot, $_) })
        missingIntegrationFiles = @($missingIntegrationFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($repositoryRoot, $_) })
    } | ConvertTo-Json -Depth 5 -Compress
    exit 2
}

$missingProductionFiles = @($requiredProductionFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
if ($missingProductionFiles.Count -gt 0) {
    [ordered]@{
        contract = 'lp-weapon-attachment-runtime'
        status = 'RED'
        proofCeiling = 'pure-contract-and-source-shape'
        exitCode = 1
        reason = 'missing-production-contract'
        productionRoot = [System.IO.Path]::GetRelativePath($repositoryRoot, $productionRoot)
        missingProductionFiles = @($missingProductionFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($repositoryRoot, $_) })
        acceptance = @(
            'ruled weapon-kind compatibility'
            'Accessory grant identifier binds item identity to attachment kind'
            'one attachment per muzzle, optic, and rail slot'
            'host-authoritative replicated identity and laser state'
            'attach/detach inventory accounting and compensation'
            'post-mutation inventory quantity reconciliation'
            'CanDrop restoration in finally'
            'identity-bearing duplicate-drop merge refusal'
            'shot-time presentation byte with ordinary fallback'
            'addon-neutral shared presentation and drop interfaces'
            'first-person/third-person laser invariance'
            'FP resolves replicated state through ViewModel.Equipment and AdditionalRendererRoot'
            'TP resolves replicated state through ancestor Equipment and base renderer visibility'
            'replicated attachment state drives perspective-correct render visibility without transform ownership'
            'no ballistic stat coupling'
        )
    } | ConvertTo-Json -Depth 5 -Compress
    exit 1
}

$dotnetOutput = @(& dotnet run --project $testProject --configuration Release --nologo 2>&1)
$dotnetExitCode = $LASTEXITCODE
$dotnetOutput | ForEach-Object { Write-Output $_ }

[ordered]@{
    contract = 'lp-weapon-attachment-runtime'
    status = if ($dotnetExitCode -eq 0) { 'PASS' } else { 'FAIL' }
    proofCeiling = 'pure-contract-and-source-shape'
    exitCode = $dotnetExitCode
    command = 'dotnet run --project tools/lp-weapon-attachment-contract/LpWeaponAttachmentContract.Tests.csproj --configuration Release --nologo'
} | ConvertTo-Json -Compress

exit $dotnetExitCode
