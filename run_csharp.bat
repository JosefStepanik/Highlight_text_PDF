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
echo Features:
echo - Click "Vybrat PDF soubor..." to select a PDF file
echo - Enter search terms separated by commas
echo - Click "Zvýraznit text" to highlight matching text
echo - Use navigation buttons or arrow keys for pages
echo - Use zoom slider or +/- keys to zoom
echo.

cd bin\Release\net8.0-windows
start "" "PdfHighlighter.exe"

echo Application started successfully!
echo If you encounter any issues, check that .NET 6.0 Runtime is installed.
echo.
timeout /t 3 /nobreak >nul