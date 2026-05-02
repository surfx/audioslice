[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

$path_script = $PSScriptRoot
Set-Location $path_script

& "$PSScriptRoot\kill.ps1"
Set-Location "$PSScriptRoot\MediaSlice"
dotnet publish MediaSlice.csproj -c Release -r win-x64 --self-contained

$publish_path = "$PSScriptRoot\MediaSlice\bin\Release\net10.0-windows10.0.17763\win-x64\publish\"
if (Test-Path $publish_path) {
    explorer.exe $publish_path
}

Set-Location $path_script