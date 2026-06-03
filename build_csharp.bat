@echo off
REM ===============================================
REM  build_csharp.bat
REM  Sestaví aplikaci v konfiguraci Release.
REM  Vyžaduje nainstalované .NET 8.0 SDK.
REM  Výstup: bin\Release\net8.0-windows\PdfHighlighter.exe
REM ===============================================
echo ===============================================
echo   PDF Text Highlighter - C# Build Script
echo ===============================================
echo.

REM Check if .NET SDK is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo.
    echo Please install .NET 8.0 SDK ^(LTS^) from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo .NET SDK found: 
dotnet --version
echo.

REM Clean previous builds
echo Cleaning previous builds...
if exist "bin" rmdir /s /q bin
if exist "obj" rmdir /s /q obj
echo.

REM Restore NuGet packages
echo Restoring NuGet packages...
dotnet restore Highlight_text.sln
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore NuGet packages!
    pause
    exit /b 1
)
echo.

REM Build the application
echo Building PDF Text Highlighter...
dotnet build Highlight_text.sln --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ===============================================
echo   BUILD SUCCESSFUL!
echo ===============================================
echo.
echo Application built successfully!
echo Location: bin\Release\net8.0-windows\PdfHighlighter.exe
echo.
echo To run the application:
echo   - Use: run_csharp.bat
echo   - Or navigate to: bin\Release\net8.0-windows\
echo   - And run: PdfHighlighter.exe
echo.
pause