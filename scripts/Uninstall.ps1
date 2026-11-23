<#
.SYNOPSIS
  Uninstalls MX TeamViewer from Logi Options+.
.PARAMETER RemoveData
  Also deletes %LOCALAPPDATA%\MXTeamViewerPlugin (config.json, logs, DPAPI secrets, diagnostics).
  Captures in Documents\TeamViewer Captures are never deleted.
#>
param([switch]$RemoveData)
$ErrorActionPreference = 'Continue'

Get-Process MXTeamViewerAgent -ErrorAction SilentlyContinue | Stop-Process -Force

$link = Join-Path $env:LOCALAPPDATA 'Logi\LogiPluginService\Plugins\MXTeamViewerPlugin.link'
if (Test-Path $link) { Remove-Item $link -Force; Write-Host "Lien développeur supprimé" }

if (Get-Command logiplugintool -ErrorAction SilentlyContinue) {
    logiplugintool uninstall MXTeamViewer
} else {
    Write-Warning "logiplugintool absent : désinstallez 'MX TeamViewer' depuis Logi Options+ > Marketplace > Installés."
}

if ($RemoveData) {
    $data = Join-Path $env:LOCALAPPDATA 'MXTeamViewerPlugin'
    if (Test-Path $data) { Remove-Item $data -Recurse -Force; Write-Host "Données supprimées : $data" }
}
Write-Host "Désinstallation terminée."
