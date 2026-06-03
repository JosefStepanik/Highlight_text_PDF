@echo off
echo ===============================================
echo   PDF Text Highlighter - DEBUG MODE
echo ===============================================
echo.
echo Spoustim Debug verzi aplikace...
echo.

REM Ukonci pripadne bezici instance
taskkill /f /im PdfHighlighter.exe >nul 2>&1

echo Sestavuji aktualni Debug build...
dotnet build Highlight_text.sln --configuration Debug -t:Rebuild
if %errorlevel% neq 0 (
	echo ERROR: Debug build selhal.
	pause
	exit /b 1
)

REM Spust aplikaci primo z Debug vystupu
"bin\Debug\net8.0-windows\PdfHighlighter.exe"

echo Aplikace spustena v Debug rezimu.
echo Debug informace:
echo - Zkontrolujte Output okno ve Visual Studio
echo - Nebo pouzijte DebugView (SysInternals)
echo - Nebo kliknete na "Debug" tlacitko v aplikaci
echo.
pause