[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.1"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $repoRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Voltrex Light - Release Packaging Automation v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Run Tests
Write-Host ""
Write-Host "[1/5] Running test suite..." -ForegroundColor Yellow
dotnet test "$repoRoot\tests\voltrex-light.Tests\voltrex-light.Tests.csproj" -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit tests failed. Aborting packaging."
}
Write-Host "[OK] All unit tests passed successfully." -ForegroundColor Green

# 2. Publish Self-Contained WinUI 3 binaries
Write-Host ""
Write-Host "[2/5] Publishing self-contained Release x64 binary..." -ForegroundColor Yellow
dotnet publish "$repoRoot\voltrex-light.csproj" -c $Configuration -p:Platform=x64 -nr:false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Dotnet publish failed."
}
Write-Host "[OK] Published to bin\Release\net8.0-windows10.0.19041.0\win-x64\publish" -ForegroundColor Green

$publishDir = "$repoRoot\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

# Ensure WinUI 3 unpackaged resource files are present in the publish directory
$targetPri = "$repoRoot\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\voltrex-light.pri"
if (Test-Path $targetPri) {
    Copy-Item $targetPri "$publishDir\resources.pri" -Force
    Copy-Item $targetPri "$publishDir\voltrex-light.pri" -Force
}
if (Test-Path "$repoRoot\Assets") {
    Copy-Item "$repoRoot\Assets" "$publishDir\Assets" -Recurse -Force
}

$distDir = "$repoRoot\dist"
if (!(Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# 3. Create Portable ZIP
Write-Host ""
Write-Host "[3/5] Creating portable ZIP archive..." -ForegroundColor Yellow
$zipPath = "$distDir\voltrex-light-v$Version-win-x64-portable.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zipItem = Get-Item $zipPath
$zipMb = [math]::Round($zipItem.Length / 1MB, 2)
Write-Host "[OK] Created portable ZIP: $($zipItem.Name) ($zipMb MB)" -ForegroundColor Green

# 4. Check for Inno Setup compiler
Write-Host ""
Write-Host "[4/5] Checking for Inno Setup compiler (ISCC.exe)..." -ForegroundColor Yellow
$isccCandidates = @(
    "ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $isccPath = $candidate
        break
    }
    if (Test-Path $candidate) {
        $isccPath = $candidate
        break
    }
}

if ($isccPath) {
    Write-Host "Compiling Inno Setup installer using $isccPath..." -ForegroundColor Cyan
    & $isccPath "$repoRoot\installer\voltrex-light.iss"
    if ($LASTEXITCODE -eq 0) {
        $setupExe = "$distDir\voltrex-light-setup-$Version.exe"
        if (Test-Path $setupExe) {
            $setupItem = Get-Item $setupExe
            $setupMb = [math]::Round($setupItem.Length / 1MB, 2)
            Write-Host "[OK] Created setup installer: $($setupItem.Name) ($setupMb MB)" -ForegroundColor Green
        }
    } else {
        Write-Warning "Inno Setup compilation exited with code $LASTEXITCODE."
    }
} else {
    Write-Host "Notice: Inno Setup (ISCC.exe) was not found on local PATH or Program Files." -ForegroundColor DarkYellow
    Write-Host "The Inno Setup script is prepared at 'installer\voltrex-light.iss'." -ForegroundColor DarkYellow
    Write-Host "It will compile automatically in GitHub Actions CI/CD on tag push, or when Inno Setup 6 is installed." -ForegroundColor DarkYellow
}

# 5. Generate SHA256 Checksums
Write-Host ""
Write-Host "[5/5] Generating SHA256 checksums..." -ForegroundColor Yellow
$checksumFile = "$distDir\SHA256SUMS.txt"
$distFiles = Get-ChildItem -Path $distDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" }
$hashOutput = foreach ($file in $distFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
$hashOutput | Out-File -FilePath $checksumFile -Encoding ascii -Force
Write-Host "[OK] Checksums saved to dist\SHA256SUMS.txt" -ForegroundColor Green

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Release artifacts ready in: $distDir" -ForegroundColor Cyan
Get-ChildItem -Path $distDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host "==========================================================" -ForegroundColor Cyan

