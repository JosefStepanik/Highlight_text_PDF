@echo off
REM ==========================================
REM  run_csharp.bat
REM  Spustí aplikaci v konfiguraci Release.
REM  Před spuštěním je nutné sestavit pomocí build_csharp.bat.
REM ==========================================
echo ==========================================
echo   PDF Text Highlighter - Starting...
echo ==========================================

REM Check if the executable exists
if not exist "bin\Release\net8.0-windows\PdfHighlighter.exe" (
    echo ERROR: Application not built yet!
    echo Please run build_csharp.bat first to build the application.
    echo.
    pause
    exit /b 1
)

echo Starting PDF Text Highlighter...
echo.

dotnet run --project PdfHighlighter.csproj --configuration Release