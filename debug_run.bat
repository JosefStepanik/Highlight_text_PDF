@echo off
echo ===============================================
echo   PDF Text Highlighter - DEBUG MODE
echo ===============================================
echo.
echo Spoustim aplikaci s debug vystupem...
echo Debug informace budou zobrazeny v konzole.
echo.

cd "bin\Release\net8.0-windows"

:: Spusť aplikaci s debug výstupem
start "" PdfHighlighter.exe

echo Aplikace spustena. Debug informace:
echo - Zkontrolujte Output okno ve Visual Studio
echo - Nebo pouzijte DebugView (SysInternals)
echo - Nebo kliknete na "Debug" tlacitko v aplikaci
echo.
pause