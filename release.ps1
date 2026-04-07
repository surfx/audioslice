[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

$path_script = $PSScriptRoot
Set-Location $path_script

& "$PSScriptRoot\kill.ps1"
Set-Location "$PSScriptRoot\AudioSlice"
dotnet publish AudioSlice.csproj -c Release -r win-x64 --self-contained

# "D:\projetos\c_sharp\audioslice\AudioSlice\bin\Release\net10.0-windows\win-x64\publish\AudioSlice.exe"

Set-Location $path_script