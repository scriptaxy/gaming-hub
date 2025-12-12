@echo off
echo ========================================
echo Building Synktra Companion EXE
echo ========================================
echo.

cd /d "%~dp0SynktraCompanion"

echo Cleaning previous builds...
dotnet clean -c Release

echo.
echo Building Release version...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "..\publish"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo.
echo EXE location: %~dp0publish\SynktraCompanion.exe
echo.
echo The EXE is self-contained and includes:
echo  - .NET Runtime
echo  - All dependencies
echo  - Single file (no installation needed)
echo.

dir "..\publish\SynktraCompanion.exe"

echo.
pause
