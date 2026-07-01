# Regenerates the WiX wizard images (BMP) from lyricsonthego.png. The dialog has a dark branding
# band on the LEFT (where WiX draws no text) and a WHITE area on the right (so WiX's dark wizard
# text stays readable); the banner is white with the logo on the right. System.Drawing only.

Add-Type -AssemblyName System.Drawing

$root     = Split-Path -Parent $PSScriptRoot
$logoPath = Join-Path $root 'lyricsonthego.png'
$outDir   = $PSScriptRoot

$dark   = [System.Drawing.Color]::FromArgb(0x0B, 0x0B, 0x0B)
$white  = [System.Drawing.Color]::FromArgb(0xED, 0xED, 0xED)
$gray   = [System.Drawing.Color]::FromArgb(0x9A, 0x9A, 0x9A)
$accent = [System.Drawing.Color]::FromArgb(0x17, 0xD3, 0x46)

$logo    = [System.Drawing.Image]::FromFile($logoPath)
$wbrush  = New-Object System.Drawing.SolidBrush($white)
$gbrush  = New-Object System.Drawing.SolidBrush($gray)
$dbrush  = New-Object System.Drawing.SolidBrush($dark)

# Dialog 493x312: left 164px dark band w/ branding, rest white.
$bmp = New-Object System.Drawing.Bitmap(493, 312)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'ClearTypeGridFit'
$g.Clear([System.Drawing.Color]::White)
$g.FillRectangle($dbrush, 0, 0, 164, 312)
$g.DrawImage($logo, 42, 46, 80, 80)
$fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
$g.DrawString('LyricsOnTheGo', (New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)), $wbrush, (New-Object System.Drawing.RectangleF(2, 142, 160, 24)), $fmt)
$g.DrawLine((New-Object System.Drawing.Pen($accent, 2)), 62, 170, 102, 170)
$g.DrawString('Real-time synced lyrics overlay', (New-Object System.Drawing.Font('Segoe UI', 8.5, [System.Drawing.FontStyle]::Regular)), $gbrush, (New-Object System.Drawing.RectangleF(10, 178, 144, 46)), $fmt)
$g.Dispose(); $bmp.Save((Join-Path $outDir 'wix-dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose()

# Banner 493x58: white, logo on the right (WiX draws the dark page title on the left).
$bmp = New-Object System.Drawing.Bitmap(493, 58)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
$g.Clear([System.Drawing.Color]::White)
$g.DrawImage($logo, 436, 8, 42, 42)
$g.Dispose(); $bmp.Save((Join-Path $outDir 'wix-banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose()

$logo.Dispose()
Write-Host "Wrote wix-banner.bmp and wix-dialog.bmp to $outDir"
