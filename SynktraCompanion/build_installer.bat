@echo off
setlocal enabledelayedexpansion

echo ============================================
echo   Synktra Companion - Build and Package
echo ============================================
echo.

:: Configuration
set VERSION=1.0.0
set CONFIG=Release
set RUNTIME=win-x64
set OUTPUT_DIR=bin\%CONFIG%\net9.0-windows\%RUNTIME%\publish
set INSTALLER_DIR=Installer
set DEPENDENCIES_DIR=%INSTALLER_DIR%\Dependencies
set INNO_SETUP="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

:: Check if we're in the right directory
if not exist "SynktraCompanion.csproj" (
 echo ERROR: Please run this script from the SynktraCompanion directory
    pause
    exit /b 1
)

:: Step 1: Clean previous build
echo [1/5] Cleaning previous build...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%INSTALLER_DIR%\Output" rmdir /s /q "%INSTALLER_DIR%\Output"

:: Step 2: Restore packages
echo [2/5] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Failed to restore packages
    pause
  exit /b 1
)

:: Step 3: Build and publish
echo [3/5] Building and publishing application...
dotnet publish -c %CONFIG% -r %RUNTIME% --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

:: Step 4: Download ViGEmBus installer if not present
echo [4/5] Preparing dependencies...
if not exist "%DEPENDENCIES_DIR%" mkdir "%DEPENDENCIES_DIR%"

if not exist "%DEPENDENCIES_DIR%\ViGEmBus_Setup.exe" (
    echo Downloading ViGEmBus driver installer...
    powershell -Command "& { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe' -OutFile '%DEPENDENCIES_DIR%\ViGEmBus_Setup.exe' }"
    if errorlevel 1 (
        echo WARNING: Could not download ViGEmBus installer
        echo          The installer will attempt to download it during setup
 ) else (
        echo ViGEmBus installer downloaded successfully
    )
) else (
    echo ViGEmBus installer already present
)

:: Step 5: Build installer
echo [5/5] Building installer...
if exist %INNO_SETUP% (
    %INNO_SETUP% "%INSTALLER_DIR%\SynktraCompanion.iss"
    if errorlevel 1 (
        echo ERROR: Installer build failed
     pause
     exit /b 1
    )
    echo.
echo ============================================
    echo   BUILD COMPLETE!
    echo ============================================
    echo.
    echo Installer created: %INSTALLER_DIR%\Output\SynktraCompanion_Setup_%VERSION%.exe
  echo.
) else (
    echo WARNING: Inno Setup not found at %INNO_SETUP%
echo          Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php
    echo          Or build the installer manually
    echo.
    echo Published files are at: %OUTPUT_DIR%
)

echo.
pause
