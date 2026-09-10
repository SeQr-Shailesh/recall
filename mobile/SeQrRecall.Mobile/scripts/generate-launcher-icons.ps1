<#
.SYNOPSIS
Regenerates the Android launcher icons from the SeQr Recall logo.

.DESCRIPTION
Writes ic_launcher.png (square) and ic_launcher_round.png (circle-masked) into every
mipmap density folder. Run this after replacing src/assets/logo.png.

.EXAMPLE
pwsh -File scripts/generate-launcher-icons.ps1
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$ResDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
if (-not $Source) {
    $Source = Join-Path $projectRoot 'src\assets\logo.png'
}
if (-not $ResDirectory) {
    $ResDirectory = Join-Path $projectRoot 'android\app\src\main\res'
}

$densities = [ordered]@{
    'mipmap-mdpi'    = 48
    'mipmap-hdpi'    = 72
    'mipmap-xhdpi'   = 96
    'mipmap-xxhdpi'  = 144
    'mipmap-xxxhdpi' = 192
}

function Save-Icon {
    param(
        [System.Drawing.Image]$Image,
        [int]$Size,
        [string]$Path,
        [switch]$Round
    )

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

            if ($Round) {
                $circle = New-Object System.Drawing.Drawing2D.GraphicsPath
                $circle.AddEllipse(0, 0, $Size, $Size)
                $graphics.SetClip($circle)
                $circle.Dispose()
            }

            $graphics.DrawImage($Image, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

$sourcePath = (Resolve-Path $Source).Path
$resPath = (Resolve-Path $ResDirectory).Path
$logo = [System.Drawing.Image]::FromFile($sourcePath)
try {
    foreach ($density in $densities.Keys) {
        $size = $densities[$density]
        $directory = Join-Path $resPath $density
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        Save-Icon -Image $logo -Size $size -Path (Join-Path $directory 'ic_launcher.png')
        Save-Icon -Image $logo -Size $size -Path (Join-Path $directory 'ic_launcher_round.png') -Round
        Write-Host "$density -> ${size}px"
    }
}
finally {
    $logo.Dispose()
}
