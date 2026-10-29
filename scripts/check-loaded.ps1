param(
    [string]$AskaDir = "C:\Program Files (x86)\Steam\steamapps\common\ASKA"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$logPath = Join-Path $AskaDir "BepInEx\LogOutput.log"
$dllPath = Join-Path $AskaDir "BepInEx\plugins\AI-Companions-Aska\AICompanionsAska.dll"

if (-not (Test-Path $logPath)) {
    Write-Host "Log nao encontrado: $logPath" -ForegroundColor Red
    Write-Host "Abra o jogo uma vez com o BepInEx instalado." -ForegroundColor Yellow
    exit 1
}

$expected = $null
$csproj = Join-Path $root "src\AICompanionsAska\AICompanionsAska.csproj"
if (Test-Path $csproj) {
    [xml]$proj = Get-Content $csproj
    $expected = $proj.Project.PropertyGroup.Version
}

Write-Host "== Esperado ==" -ForegroundColor Cyan
Write-Host "csproj: $expected"
if (Test-Path $dllPath) {
    $dllVersion = (Get-Item $dllPath).VersionInfo.FileVersion
    Write-Host "DLL deployada: $dllVersion ($((Get-Item $dllPath).LastWriteTime))"
} else {
    Write-Host "DLL deployada: AUSENTE ($dllPath)" -ForegroundColor Red
}

Write-Host ""
Write-Host "== BepInEx ==" -ForegroundColor Cyan
Get-Content $logPath -TotalCount 12

Write-Host ""
Write-Host "== AI Companions - ASKA ==" -ForegroundColor Cyan
$lines = Select-String -Path $logPath -Pattern "\[AIC\]|AI Companions - ASKA"
if ($lines) {
    $lines | Select-Object -Last 40 | ForEach-Object { $_.Line }
} else {
    Write-Host "Nenhuma linha [AIC] encontrada. O plugin nao carregou." -ForegroundColor Red
}

Write-Host ""
Write-Host "== Erros recentes ==" -ForegroundColor Cyan
$errors = Select-String -Path $logPath -Pattern "Exception|Error|Fatal|MissingMethod|FileLoad|0x800711C7|coreclr" |
    Select-Object -Last 25
if ($errors) {
    $errors | ForEach-Object { $_.Line }
} else {
    Write-Host "Nenhum erro encontrado." -ForegroundColor Green
}

if (-not $lines) { exit 1 }

if ($expected) {
    $loaded = $lines | Where-Object { $_.Line -match "AI Companions - ASKA\s+([0-9]+\.[0-9]+\.[0-9]+)" } |
        Select-Object -First 1
    if ($loaded -and $loaded.Line -match $expected) {
        Write-Host ""
        Write-Host "Versao carregada confere: $expected" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "Versao carregada NAO confere com a esperada ($expected)." -ForegroundColor Yellow
        exit 1
    }
}
