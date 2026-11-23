<#
.SYNOPSIS
  Generates src\package\metadata\Icon256x256.png (plugin icon shown in Logi Options+).
  Generic "remote control" pictogram (blue disc + two opposed arrows): not the TeamViewer trademark logo.
  Runs in Windows PowerShell 5.1 or PowerShell 7 on Windows (System.Drawing).
#>
param([string]$Output = (Join-Path $PSScriptRoot '..\src\package\metadata\Icon256x256.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(18, 20, 24))

$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 140, 255))
$g.FillEllipse($blue, 28, 28, 200, 200)

$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 18
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap 3, 3
$g.DrawLine($pen, 70, 105, 180, 105)
$g.DrawLine($pen, 186, 151, 76, 151)

$dir = Split-Path $Output -Parent
New-Item -ItemType Directory -Force $dir | Out-Null
$bmp.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Icon written: $Output"
