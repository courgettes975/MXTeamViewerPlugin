<#
.SYNOPSIS
  Installs MX TeamViewer into Logi Options+.
.PARAMETER Dev
  Developer install: Debug build + .link file in %LOCALAPPDATA%\Logi\LogiPluginService\Plugins
  pointing to bin\Debug (rebuild = hot reload). Otherwise: Release package + logiplugintool install.
.PARAMETER Package
  Install an existing .lplug4 instead of building one.
#>
param([switch]$Dev, [string]$Package)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

# Prerequisites ---------------------------------------------------------------------------------
if (-not (Test-Path 'C:\Program Files\Logi\LogiPluginService\PluginApi.dll')) {
    throw "Logi Options+ (Logi Plugin Service) n'est pas installé."
}
$tv = Get-ItemProperty 'HKLM:\SOFTWARE\TeamViewer' -ErrorAction SilentlyContinue
if ($tv) { Write-Host "TeamViewer $($tv.Version) détecté : $($tv.InstallationDirectory)" }
else { Write-Warning "TeamViewer non détecté : le plugin s'installe, renseignez teamviewerPath dans config.json." }

if ($Dev) {
    Write-Host "== Installation développeur (.link)"
    dotnet build (Join-Path $root 'src\MXTeamViewerPlugin.csproj') -c Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} else {
    if (-not $Package) { $Package = & (Join-Path $PSScriptRoot 'Build-Package.ps1') | Select-Object -Last 1 }
    # A previous dev .link would shadow the installed package.
    $link = Join-Path $env:LOCALAPPDATA 'Logi\LogiPluginService\Plugins\MXTeamViewerPlugin.link'
    if (Test-Path $link) { Remove-Item $link -Force; Write-Host "Ancien lien développeur supprimé" }
    Write-Host "== logiplugintool install $Package"
    logiplugintool install $Package
    if ($LASTEXITCODE -ne 0) {
        # logiplugintool can time out waiting for the service's confirmation while the files are
        # already installed and loaded: trust the installed manifest version.
        $expected = (logiplugintool metadata $Package | Select-String '"version":\s*"([^"]+)"').Matches.Groups[1].Value
        $manifest = Join-Path $env:LOCALAPPDATA 'Logi\LogiPluginService\Plugins\MXTeamViewer\metadata\LoupedeckPackage.yaml'
        $installed = if (Test-Path $manifest) { (Select-String -Path $manifest -Pattern '^version:\s*(\S+)').Matches.Groups[1].Value }
        if ($expected -and $installed -eq $expected) {
            Write-Warning "logiplugintool a expiré, mais la version $installed est bien installée."
        } else {
            throw "install failed ($LASTEXITCODE). Vous pouvez aussi double-cliquer le fichier .lplug4."
        }
    }
}

Start-Process 'loupedeck:plugin/MXTeamViewer/reload' -ErrorAction SilentlyContinue
Write-Host ""
Write-Host "Installé. Dans Logi Options+ : MX Creative Keypad > Actions > MX TeamViewer."
Write-Host "Configuration : $env:LOCALAPPDATA\MXTeamViewerPlugin\config.json (créée au premier chargement)"
