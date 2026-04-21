@echo off
echo ===============================================
echo   PDF Text Highlighter - DEBUG MODE
echo ===============================================
echo.
echo Spoustim Debug verzi aplikace...
echo.

REM Check if Debug executable exists
if not exist "bin\Debug\net8.0-windows\PdfHighlighter.exe" (
	echo DEBUG build nebyl nalezen.
	echo Spoustim sestaveni v Debug konfiguraci...
	dotnet build Highlight_text.sln --configuration Debug
	if %errorlevel% neq 0 (
		echo ERROR: Debug build selhal.
		pause
		exit /b 1
	)
)

REM Ukonci pripadne bezici instance
taskkill /f /im PdfHighlighter.exe >nul 2>&1

REM Spust aplikaci pres dotnet run (nevyzaduje instalovany Runtime)
dotnet run --project PdfHighlighter.csproj --configuration Debug

echo Aplikace spustena v Debug rezimu.
echo Debug informace:
echo - Zkontrolujte Output okno ve Visual Studio
echo - Nebo pouzijte DebugView (SysInternals)
echo - Nebo kliknete na "Debug" tlacitko v aplikaci
echo.
pause