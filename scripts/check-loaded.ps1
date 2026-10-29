param(
    [string]$AskaDir = "C:\Program Files (x86)\Steam\steamapps\common\ASKA"
)

$ErrorActionPreference = "Stop"
$logPath = Join-Path $AskaDir "BepInEx\LogOutput.log"

if (-not (Test-Path $logPath)) {
    Write-Host "Log nao encontrado: $logPath" -ForegroundColor Red
    Write-Host "Abra o jogo uma vez com o BepInEx instalado." -ForegroundColor Yellow
    exit 1
}

Write-Host "== BepInEx ==" -ForegroundColor Cyan
Get-Content $logPath -TotalCount 12

Write-Host ""
Write-Host "== AI Companions - ASKA ==" -ForegroundColor Cyan
$lines = Select-String -Path $logPath -Pattern "\[AIC\]|AI Companions - ASKA"
if ($lines) {
    $lines | Select-Object -Last 40 | ForEach-Object { $_.Line }
} else {
    Write-Host "Nenhuma linha [AIC] encontrada. O plugin nao carregou?" -ForegroundColor Red
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
