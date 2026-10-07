# Builds the plugin and packs it into dist\QuickLook.Plugin.PlaylistNav.qlplugin
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = "bin\Release"
New-Item -ItemType Directory -Force dist | Out-Null
$zip = "dist\QuickLook.Plugin.PlaylistNav.zip"
Compress-Archive -Force -DestinationPath $zip -Path "$out\QuickLook.Plugin.PlaylistNav.dll", "$out\QuickLook.Plugin.Metadata.config"
Move-Item -Force $zip "dist\QuickLook.Plugin.PlaylistNav.qlplugin"
Write-Host "dist\QuickLook.Plugin.PlaylistNav.qlplugin"
