<#
.SYNOPSIS
  Builds the plugin + agent in Release and produces dist\MXTeamViewer_<version>.lplug4.
.DESCRIPTION
  1. dotnet build (Release, no dev .link)   2. logiplugintool pack   3. logiplugintool verify
  Requires: .NET SDK 10+, Logi Options+ (PluginApi.dll), LogiPluginTool
  (dotnet tool install --global LogiPluginTool).
#>
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

[xml]$proj = Get-Content (Join-Path $root 'src\MXTeamViewerPlugin.csproj')
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

if (-not (Test-Path 'src\package\metadata\Icon256x256.png')) { & (Join-Path $PSScriptRoot 'New-PackageIcon.ps1') }

if (-not (Get-Command logiplugintool -ErrorAction SilentlyContinue)) {
    throw "logiplugintool introuvable. Installez-le : dotnet tool install --global LogiPluginTool"
}

Write-Host "== Build $Configuration $version"
$out = Join-Path $root "bin\$Configuration"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet build 'src\MXTeamViewerPlugin.csproj' -c $Configuration --nologo -p:LinkPlugin=false
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

foreach ($f in "bin\MXTeamViewerPlugin.dll", "bin\agent\MXTeamViewerAgent.exe", "metadata\LoupedeckPackage.yaml", "metadata\Icon256x256.png") {
    if (-not (Test-Path (Join-Path $out $f))) { throw "Missing build output: $f" }
}
# Debug symbols are not shipped.
Get-ChildItem $out -Recurse -Filter *.pdb | Remove-Item -Force

New-Item -ItemType Directory -Force 'dist' | Out-Null
$package = Join-Path $root "dist\MXTeamViewer_$version.lplug4"
if (Test-Path $package) { Remove-Item $package -Force }
Write-Host "== Pack $package"
logiplugintool pack "$out\" $package
if ($LASTEXITCODE -ne 0) { throw "pack failed ($LASTEXITCODE)" }
logiplugintool verify $package
if ($LASTEXITCODE -ne 0) { throw "verify failed ($LASTEXITCODE)" }
Write-Host "Package ready: $package"
$package
