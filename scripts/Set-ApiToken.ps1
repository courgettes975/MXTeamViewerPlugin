<#
.SYNOPSIS
  Stores (or removes) the optional TeamViewer Web API token, encrypted with Windows DPAPI
  (CurrentUser). The token is read without echo and never written in clear anywhere.
  Same format as src\Security\SecretStore.cs (entropy "MXTeamViewerPlugin.v1").
  Then set "apiEnabled": true in config.json. The token is only used by the diagnostics (/ping).
#>
param([switch]$Remove)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$dir = Join-Path $env:LOCALAPPDATA 'MXTeamViewerPlugin\secrets'
$file = Join-Path $dir 'teamviewer-api-token.bin'

if ($Remove) {
    if (Test-Path $file) { Remove-Item $file -Force }
    Write-Host "Jeton supprimé."
    return
}

$secure = Read-Host -AsSecureString "Jeton Web API TeamViewer (script token)"
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    if ([string]::IsNullOrWhiteSpace($plain)) { throw "Jeton vide." }
    $entropy = [Text.Encoding]::UTF8.GetBytes('MXTeamViewerPlugin.v1')
    $blob = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($plain), $entropy, 'CurrentUser')
    New-Item -ItemType Directory -Force $dir | Out-Null
    [IO.File]::WriteAllBytes($file, $blob)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    $plain = $null
}
Write-Host "Jeton chiffré (DPAPI) : $file"
