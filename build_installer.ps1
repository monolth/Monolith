$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $root "installer"
$distDir = Join-Path $root "dist"

# 1. Build fresh Monolith.exe first
Write-Host "Step 1/3: Building fresh Monolith.exe..."
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) { throw "Monolith.exe build failed!" }

$appExe = Join-Path $root "Monolith.exe"
$icoPath = Join-Path $root "assets\monolith.ico"
$gzPath = Join-Path $installerDir "Monolith.exe.gz"

# 2. GZip-compress Monolith.exe as embedded payload
Write-Host "Step 2/3: Compressing Monolith.exe payload..."
$inBytes = [System.IO.File]::ReadAllBytes($appExe)
$outFs = New-Object System.IO.FileStream($gzPath, [System.IO.FileMode]::Create)
$gz = New-Object System.IO.Compression.GZipStream($outFs, [System.IO.Compression.CompressionMode]::Compress)
$gz.Write($inBytes, 0, $inBytes.Length)
$gz.Close()
$outFs.Close()
Write-Host "Compressed payload: $($inBytes.Length) -> $((Get-Item $gzPath).Length) bytes"

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

# 3. Compile standalone single-file MonolithSetup.exe
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $fw "WPF"

$setupExe = Join-Path $distDir "MonolithSetup.exe"
$sources = Get-ChildItem $installerDir -Filter "*.cs" | ForEach-Object { $_.FullName }

$argsList = @(
    "/nologo",
    "/codepage:65001",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/out:$setupExe",
    "/win32icon:$icoPath",
    "/win32manifest:$(Join-Path $root 'app.manifest')",
    "/resource:$gzPath,MonolithPayload.exe.gz",
    "/resource:$icoPath,MonolithIcon.ico",
    "/r:$(Join-Path $wpf 'PresentationFramework.dll')",
    "/r:$(Join-Path $wpf 'PresentationCore.dll')",
    "/r:$(Join-Path $wpf 'WindowsBase.dll')",
    "/r:$(Join-Path $fw 'System.Xaml.dll')",
    "/r:$(Join-Path $fw 'System.Windows.Forms.dll')",
    "/r:$(Join-Path $fw 'System.Core.dll')",
    "/r:$(Join-Path $fw 'System.dll')"
) + $sources

Write-Host "Step 3/3: Compiling standalone MonolithSetup.exe..."
& $csc @argsList
if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE"
}

Copy-Item $setupExe (Join-Path $root "MonolithSetup.exe") -Force

$info = Get-Item $setupExe
Write-Host "SUCCESS: Built standalone installer $($info.FullName) ($($info.Length) bytes)"
