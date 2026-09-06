<#
.SYNOPSIS
    Builds the official AutoScape Inno Setup Installer.
    Produces dist\AutoScape-Setup.exe with SHA256 checksum.
#>

[CmdletBinding()]
param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$installerDir = $PSScriptRoot
if (-not $installerDir) { $installerDir = (Get-Location).Path }
$repoRoot = Split-Path -Parent $installerDir
$issPath = Join-Path $installerDir 'AutoScape.iss'

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Cannot find AutoScape.iss at $issPath"
}

# 1. Locate or install Inno Setup Compiler (ISCC.exe)
Write-Step "Locating Inno Setup Compiler (ISCC.exe)..."

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not $isccPath) {
    Write-Host "Inno Setup not found. Attempting to install via winget..." -ForegroundColor Yellow
    winget install JRSoftware.InnoSetup --silent --accept-source-agreements --accept-package-agreements
    $isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}

if (-not $isccPath) {
    throw "ISCC.exe could not be found or installed. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}

Write-Host "Found compiler: $isccPath" -ForegroundColor Green

# 2. Determine Version
if (-not $Version) {
    try {
        Push-Location $repoRoot
        $commitCount = (git rev-list --count HEAD 2>$null)
        if ($commitCount -and ($commitCount.Trim() -match '^\d+$')) {
            $Version = "1.0.$($commitCount.Trim())"
        }
        Pop-Location
    } catch {
        Pop-Location -ErrorAction SilentlyContinue
    }
}
if (-not $Version) { $Version = "2.0.0" }

Write-Step "Target Version: $Version"


# 3. Compile Installer
Write-Step "Compiling Inno Setup Script..."
$distDir = Join-Path $repoRoot 'dist'
if (-not (Test-Path -LiteralPath $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }

$isccArgs = @(
    "/DMyAppVersion=$Version",
    "/O$distDir",
    $issPath
)

& $isccPath $isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}

# 4. Output Verification & Checksums
$setupExe = Join-Path $distDir 'AutoScape-Setup.exe'
if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Compiled output $setupExe not found"
}

$hash = (Get-FileHash -LiteralPath $setupExe -Algorithm SHA256).Hash.ToLowerInvariant()
$shaFile = Join-Path $distDir 'AutoScape-Setup.exe.sha256'
Set-Content -LiteralPath $shaFile -Value "$hash  AutoScape-Setup.exe" -Encoding ASCII

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Installer: $setupExe" -ForegroundColor White
Write-Host " Size     : $([Math]::Round((Get-Item $setupExe).Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host " SHA256   : $hash" -ForegroundColor Yellow
Write-Host " Checksum : $shaFile" -ForegroundColor White
Write-Host "========================================================`n" -ForegroundColor Green
