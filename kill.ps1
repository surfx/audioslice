[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

$path_script = $PSScriptRoot
Set-Location $path_script

# Encerra todas as instâncias do AudioSlice.exe
$processName = "AudioSlice"
$processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

if ($processes) {
    Write-Host "🛑 Encerrando $processName..." -ForegroundColor Yellow
    Stop-Process -Name $processName -Force
    # Pequena pausa para o SO liberar os handles dos arquivos
    Start-Sleep -Milliseconds 500
}

Set-Location $path_script