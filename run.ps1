[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

$path_script = $PSScriptRoot
Set-Location $path_script

& "$PSScriptRoot\kill.ps1"
Set-Location "$PSScriptRoot\MediaSlice"
dotnet run

Set-Location $path_script