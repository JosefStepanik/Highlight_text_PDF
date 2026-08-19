@echo off
setlocal

REM ===============================================
REM  run_csharp.bat
REM  Hlavni vyvojovy skript.
REM  Bez parametru spusti aplikaci v Release rezimu.
REM  Parametry: debug | build | build-debug | help
REM ===============================================

set "MODE=%~1"
if /I "%MODE%"=="" set "MODE=release"

if /I "%MODE%"=="help" goto :usage
if /I "%MODE%"=="release" goto :run_release
if /I "%MODE%"=="debug" goto :run_debug
if /I "%MODE%"=="build" goto :build_release
if /I "%MODE%"=="build-debug" goto :build_debug

echo ERROR: Unknown mode '%MODE%'.
echo.
goto :usage

:run_release
call :check_dotnet || exit /b 1
echo ===============================================
echo   PDF Text Highlighter - Run Release
echo ===============================================
echo.
dotnet run --project PdfHighlighter.csproj --configuration Release
exit /b %errorlevel%

:run_debug
call :check_dotnet || exit /b 1
echo ===============================================
echo   PDF Text Highlighter - Run Debug
echo ===============================================
echo.
echo Tip: Search logy lze zapnout pres PDFHIGHLIGHTER_SEARCH_LOGS=1
echo.
dotnet run --project PdfHighlighter.csproj --configuration Debug
exit /b %errorlevel%

:build_release
call :check_dotnet || exit /b 1
echo ===============================================
echo   PDF Text Highlighter - Build Release
echo ===============================================
echo.
dotnet build Highlight_text.sln --configuration Release
exit /b %errorlevel%

:build_debug
call :check_dotnet || exit /b 1
echo ===============================================
echo   PDF Text Highlighter - Build Debug
echo ===============================================
echo.
dotnet build Highlight_text.sln --configuration Debug
exit /b %errorlevel%

:check_dotnet
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
exit /b 0

:usage
echo Usage:
echo   run_csharp.bat              ^(run Release^)
echo   run_csharp.bat debug        ^(run Debug^)
echo   run_csharp.bat build        ^(build Release only^)
echo   run_csharp.bat build-debug  ^(build Debug only^)
echo   publish.bat                 ^(create standalone package^)
exit /b 1