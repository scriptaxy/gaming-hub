# Synktra Companion - Build and Package Script
# Requires: .NET 9 SDK, Inno Setup 6 (optional)

param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
[string]$Runtime = "win-x64",
    [switch]$SkipInstaller,
    [switch]$DownloadViGEm
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   Synktra Companion - Build and Package" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir
$OutputDir = Join-Path $ProjectDir "bin\$Configuration\net9.0-windows\$Runtime\publish"
$InstallerDir = Join-Path $ProjectDir "Installer"
$DependenciesDir = Join-Path $InstallerDir "Dependencies"
$InnoSetup = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# Ensure we're in the right directory
if (-not (Test-Path (Join-Path $ProjectDir "SynktraCompanion.csproj"))) {
    Write-Host "ERROR: Please run this script from the SynktraCompanion directory" -ForegroundColor Red
    exit 1
}

# Step 1: Clean
Write-Host "[1/6] Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
if (Test-Path (Join-Path $InstallerDir "Output")) { Remove-Item -Recurse -Force (Join-Path $InstallerDir "Output") }

# Step 2: Restore
Write-Host "[2/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore packages" -ForegroundColor Red
    exit 1
}

# Step 3: Build
Write-Host "[3/6] Building application..." -ForegroundColor Yellow
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

# Step 4: Publish
Write-Host "[4/6] Publishing application..." -ForegroundColor Yellow
dotnet publish -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
 -p:Version=$Version `
    --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed" -ForegroundColor Red
    exit 1
}

# Step 5: Download ViGEmBus installer
Write-Host "[5/6] Preparing dependencies..." -ForegroundColor Yellow
if (-not (Test-Path $DependenciesDir)) { New-Item -ItemType Directory -Path $DependenciesDir | Out-Null }

$ViGEmPath = Join-Path $DependenciesDir "ViGEmBus_Setup.exe"
$ViGEmUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"

if (-not (Test-Path $ViGEmPath) -or $DownloadViGEm) {
    Write-Host "  Downloading ViGEmBus driver installer..." -ForegroundColor Gray
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($ViGEmUrl, $ViGEmPath)
        Write-Host "  ViGEmBus installer downloaded successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "  WARNING: Could not download ViGEmBus installer" -ForegroundColor Yellow
        Write-Host "    The installer will attempt to download it during setup" -ForegroundColor Yellow
    }
}
else {
    Write-Host "  ViGEmBus installer already present" -ForegroundColor Gray
}

# Step 6: Build installer
Write-Host "[6/6] Building installer..." -ForegroundColor Yellow
if (-not $SkipInstaller) {
    if (Test-Path $InnoSetup) {
        & $InnoSetup (Join-Path $InstallerDir "SynktraCompanion.iss")
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Installer build failed" -ForegroundColor Red
      exit 1
      }
        
        Write-Host ""
        Write-Host "============================================" -ForegroundColor Green
        Write-Host "   BUILD COMPLETE!" -ForegroundColor Green
     Write-Host "============================================" -ForegroundColor Green
      Write-Host ""
      Write-Host "Installer: " -NoNewline
        Write-Host (Join-Path $InstallerDir "Output\SynktraCompanion_Setup_$Version.exe") -ForegroundColor Cyan
    }
    else {
        Write-Host "WARNING: Inno Setup not found at $InnoSetup" -ForegroundColor Yellow
        Write-Host "         Install from: https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Published files: $OutputDir" -ForegroundColor Cyan
    }
}
else {
    Write-Host "Skipping installer (--SkipInstaller)" -ForegroundColor Gray
    Write-Host "Published files: $OutputDir" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Build artifacts:" -ForegroundColor White
Write-Host "  Published app: $OutputDir" -ForegroundColor Gray
if (Test-Path (Join-Path $InstallerDir "Output")) {
    Get-ChildItem (Join-Path $InstallerDir "Output") | ForEach-Object {
        Write-Host "  Installer: $($_.FullName)" -ForegroundColor Gray
    }
}
