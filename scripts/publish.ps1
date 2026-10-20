# Builds a self-contained Roland Aira Compact T-8 Editor for Windows. Output: artifacts\T8Editor-win-x64.zip
param([string]$Rid = "win-x64", [string]$Version = "0.1.0")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$dir = "artifacts/publish/$Rid"
if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
dotnet publish src/T8.Editor/T8.Editor.csproj -c Release -r $Rid --self-contained -p:Version=$Version -o $dir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$zip = "artifacts/T8Editor-$Rid.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$dir/*" -DestinationPath $zip
Write-Host "Package: $zip"
