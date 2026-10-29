param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Test-Path "Environment.props")) {
        Write-Host "Environment.props nao encontrado. Copie Environment.props.example e ajuste AskaDir." -ForegroundColor Yellow
        exit 1
    }

    [xml]$props = Get-Content "Environment.props"
    $askaDir = $props.Project.PropertyGroup.AskaDir
    if (-not $askaDir) { $askaDir = "C:\Program Files (x86)\Steam\steamapps\common\ASKA" }
    $interop = Join-Path $askaDir "BepInEx\interop\Assembly-CSharp.dll"
    if (-not (Test-Path $interop)) {
        Write-Host "Interop nao encontrado: $interop" -ForegroundColor Red
        Write-Host "Abra o ASKA uma vez com o BepInEx 6 IL2CPP instalado para gerar as interop assemblies." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Building AI Companions - ASKA ($Configuration)..." -ForegroundColor Cyan
    dotnet build "AICompanionsAska.sln" -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build falhou (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Build OK." -ForegroundColor Green
    Write-Host "Confira BepInEx\LogOutput.log apos abrir o jogo." -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
