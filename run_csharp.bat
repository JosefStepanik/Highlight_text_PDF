@echo off
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

"bin\Release\net8.0-windows\PdfHighlighter.exe"