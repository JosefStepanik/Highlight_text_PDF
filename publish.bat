@echo off
REM =====================================================
REM  publish.bat
REM  Vytvoří standalone (self-contained) verzi aplikace pro Windows x64.
REM  Výsledek nevyžaduje instalaci .NET Runtime na cílovém počítači.
REM  Výstup: publish\PdfHighlighter.exe (+ závislosti)
REM =====================================================
echo =====================================================
echo   PDF Text Highlighter - Publish Standalone
echo =====================================================
echo.

REM Check if .NET SDK is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
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
dotnet publish PdfHighlighter.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish

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
echo Standalone application created in: publish\
echo File: publish\PdfHighlighter.exe
echo.
echo This version includes all dependencies and doesn't require
echo .NET Runtime to be installed on the target computer.
echo.
echo Distribute the whole publish\ folder.
echo.
pause