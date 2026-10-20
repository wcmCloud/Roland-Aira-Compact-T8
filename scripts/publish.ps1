# Builds self-contained Roland Aira Compact T-8 Editor packages for Windows (ARM64 and x64).
# Usage: scripts\publish.ps1 [-Rids win-x64,win-arm64] [-Version 1.0.0]
# Output: artifacts\T8Editor-<version>-<rid>.zip. For macOS and Linux packages use scripts/publish.sh.
param([string[]]$Rids = @("win-x64", "win-arm64"), [string]$Version = "")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

if (-not $Version) {
    $Version = ([xml](Get-Content src/T8.Editor/T8.Editor.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}

foreach ($Rid in $Rids) {
    $dir = "artifacts/publish/$Rid"
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    dotnet publish src/T8.Editor/T8.Editor.csproj -c Release -r $Rid --self-contained -p:Version=$Version -p:DebugType=None -o $dir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $zip = "artifacts/T8Editor-$Version-$Rid.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path "$dir/*" -DestinationPath $zip
    Write-Host "Package: $zip"
}
