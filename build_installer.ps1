$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $root "installer"
$distDir = Join-Path $root "dist"

# 1. Build fresh Monolith.exe first
Write-Host "Step 1/2: Building fresh Monolith.exe..."
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) { throw "Monolith.exe build failed!" }

$appExe = Join-Path $root "Monolith.exe"
$icoPath = Join-Path $root "assets\monolith.ico"
$issPath = Join-Path $installerDir "Monolith.iss"
$oldGz = Join-Path $installerDir "Monolith.exe.gz"
if (Test-Path $oldGz) { Remove-Item $oldGz -Force -ErrorAction SilentlyContinue }

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}
$setupExe = Join-Path $distDir "MonolithSetup.exe"

# 2. Check for official Inno Setup 6 Compiler (ISCC.exe - pre-installed on GitHub Actions windows-latest!)
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $null
foreach ($c in $isccCandidates) {
    if (Test-Path $c) { $iscc = $c; break }
}
if (-not $iscc) {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

if ($iscc -and (Test-Path $issPath)) {
    Write-Host "Step 2/2: Compiling official Inno Setup 6 installer via $iscc..."
    & $iscc "/Q" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup (ISCC.exe) compilation failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Host "Step 2/2: Compiling clean uncompressed standalone MonolithSetup.exe (local fallback)..."
    $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
    $wpf = Join-Path $fw "WPF"
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
        "/resource:$appExe,MonolithPayload.exe",
        "/resource:$icoPath,MonolithIcon.ico",
        "/r:$(Join-Path $wpf 'PresentationFramework.dll')",
        "/r:$(Join-Path $wpf 'PresentationCore.dll')",
        "/r:$(Join-Path $wpf 'WindowsBase.dll')",
        "/r:$(Join-Path $fw 'System.Xaml.dll')",
        "/r:$(Join-Path $fw 'System.Windows.Forms.dll')",
        "/r:$(Join-Path $fw 'System.Core.dll')",
        "/r:$(Join-Path $fw 'System.dll')"
    ) + $sources

    & $csc @argsList
    if ($LASTEXITCODE -ne 0) {
        throw "Installer compilation failed with exit code $LASTEXITCODE"
    }
}

Copy-Item $setupExe (Join-Path $root "MonolithSetup.exe") -Force
$info = Get-Item $setupExe
Write-Host "SUCCESS: Built standalone installer $($info.FullName) ($($info.Length) bytes)"
