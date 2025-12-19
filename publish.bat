@echo off
echo =====================================================
echo   PDF Text Highlighter - Publish Standalone
echo =====================================================
echo.

REM Check if .NET SDK is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 6.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Creating standalone executable...
echo This will create a self-contained version that doesn't require .NET Runtime.
echo.

REM Clean previous publish
if exist "publish" rmdir /s /q publish

REM Publish self-contained
echo Publishing standalone application...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o publish

if %errorlevel% neq 0 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)

echo.
echo =====================================================
echo   STANDALONE VERSION CREATED!
echo =====================================================
echo.
echo Standalone executable created in: publish\
echo File: PdfHighlighter.exe (approximately 60-80 MB)
echo.
echo This version includes all dependencies and doesn't require
echo .NET Runtime to be installed on the target computer.
echo.
echo You can distribute just the PdfHighlighter.exe file!
echo.
pause