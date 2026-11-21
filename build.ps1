$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetsDir = Join-Path $root "assets"
$logoPng = Join-Path $assetsDir "monolith_logo.png"
$icoPath = Join-Path $assetsDir "monolith.ico"
$outExe = Join-Path $root "Monolith.exe"

if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir | Out-Null
}

Add-Type -AssemblyName System.Drawing

if (Test-Path $logoPng) {
    # Build true multi-resolution PNG-compressed Windows .ico (16, 24, 32, 48, 64, 128, 256 px) from monolith_logo.png
    $srcImg = [System.Drawing.Image]::FromFile($logoPng)
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $pngChunks = New-Object System.Collections.Generic.List[byte[]]

    foreach ($sz in $sizes) {
        $b = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($b)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($srcImg, 0, 0, $sz, $sz)
        $ms = New-Object System.IO.MemoryStream
        $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngChunks.Add($ms.ToArray())
        $ms.Dispose(); $g.Dispose(); $b.Dispose()
    }
    $srcImg.Dispose()

    $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    # ICONDIR header: reserved=0, type=1 (ICO), count
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Count)

    $offset = 6 + ($sizes.Count * 16)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dimByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
        $bytes = $pngChunks[$i]
        $bw.Write($dimByte)              # width
        $bw.Write($dimByte)              # height
        $bw.Write([byte]0)               # color count
        $bw.Write([byte]0)               # reserved
        $bw.Write([uint16]1)             # planes
        $bw.Write([uint16]32)            # bpp
        $bw.Write([uint32]$bytes.Length) # size
        $bw.Write([uint32]$offset)       # offset
        $offset += $bytes.Length
    }
    for ($i = 0; $i -lt $pngChunks.Count; $i++) {
        $bw.Write($pngChunks[$i])
    }
    $bw.Close(); $fs.Close()
}
else {
    # Fallback programmatic Monolith crystal icon if monolith_logo.png is absent
    $bmp = New-Object System.Drawing.Bitmap(64, 64)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $orbRect = New-Object System.Drawing.Rectangle(2, 2, 60, 60)
    $orbBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point(64, 64)),
        [System.Drawing.Color]::FromArgb(255, 48, 72, 124),
        [System.Drawing.Color]::FromArgb(255, 10, 14, 26))
    $g.FillEllipse($orbBrush, $orbRect)
    $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(195, 240, 248, 255), 2.2)
    $g.DrawEllipse($rimPen, $orbRect)
    $slabBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(22, 12)),
        (New-Object System.Drawing.Point(42, 52)),
        [System.Drawing.Color]::FromArgb(255, 252, 254, 255),
        [System.Drawing.Color]::FromArgb(255, 96, 165, 250))
    $g.FillRectangle($slabBrush, 22, 12, 20, 40)
    $hIcon = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
    $icon.Save($fs); $fs.Close()
    $g.Dispose(); $bmp.Dispose()
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $fw "WPF"

$sources = Get-ChildItem (Join-Path $root "src") -Filter "*.cs" | ForEach-Object { $_.FullName }

$argsList = @(
    "/nologo",
    "/codepage:65001",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/out:$outExe",
    "/win32icon:$icoPath",
    "/win32manifest:$(Join-Path $root 'app.manifest')",
    "/r:$(Join-Path $wpf 'PresentationFramework.dll')",
    "/r:$(Join-Path $wpf 'PresentationCore.dll')",
    "/r:$(Join-Path $wpf 'WindowsBase.dll')",
    "/r:$(Join-Path $wpf 'UIAutomationClient.dll')",
    "/r:$(Join-Path $wpf 'UIAutomationTypes.dll')",
    "/r:$(Join-Path $fw 'System.Xaml.dll')",
    "/r:$(Join-Path $fw 'System.Drawing.dll')",
    "/r:$(Join-Path $fw 'System.Windows.Forms.dll')",
    "/r:$(Join-Path $fw 'System.Web.Extensions.dll')",
    "/r:$(Join-Path $fw 'System.Management.dll')",
    "/r:$(Join-Path $fw 'System.Data.dll')",
    "/r:$(Join-Path $fw 'System.Core.dll')",
    "/r:$(Join-Path $fw 'System.dll')"
) + $sources

Write-Host "Compiling Monolith.exe with $($sources.Count) source files..."
& $csc @argsList
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

$info = Get-Item $outExe
Write-Host "SUCCESS: Built $($info.FullName) ($($info.Length) bytes)"
