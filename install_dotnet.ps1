# Automatická instalace .NET 8.0 SDK (LTS)
Write-Host "===============================================" -ForegroundColor Green
Write-Host "  .NET 8.0 SDK Installer (LTS)" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""

# Zkontroluj, zda už není .NET SDK nainstalován
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host ".NET SDK je již nainstalován: verze $dotnetVersion" -ForegroundColor Green
        Write-Host "Můžete pokračovat s build_csharp.bat" -ForegroundColor Cyan
        Read-Host "Stiskněte Enter pro pokračování"
        exit 0
    }
} catch {
    # .NET není nainstalován, pokračujeme s instalací
}

Write-Host "Stahuji .NET 8.0 SDK (LTS)..." -ForegroundColor Yellow

# URL pro .NET 8.0 SDK installer (nejnovější LTS verze)
$installerUrl = "https://download.microsoft.com/download/6/0/f/60fc916d-d80b-4c14-8f3c-dd1b784448d2/dotnet-sdk-8.0.404-win-x64.exe"
$installerPath = "$env:TEMP\dotnet-sdk-installer.exe"

try {
    # Stáhni installer
    Write-Host "Stahování z: $installerUrl" -ForegroundColor Gray
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    
    Write-Host "Stahování dokončeno. Spouštím installer..." -ForegroundColor Green
    
    # Spusť installer s administrativními právy
    Write-Host "DŮLEŽITÉ: Installer vyžaduje administrativní oprávnění!" -ForegroundColor Red
    Write-Host "Klikněte 'Ano' v UAC dialogu, který se zobrazí." -ForegroundColor Yellow
    
    $process = Start-Process -FilePath $installerPath -ArgumentList "/quiet" -Wait -PassThru -Verb RunAs
    
    if ($process.ExitCode -eq 0) {
        Write-Host ""
        Write-Host "===============================================" -ForegroundColor Green
        Write-Host "  .NET SDK úspěšně nainstalován!" -ForegroundColor Green
        Write-Host "===============================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Pro dokončení instalace:" -ForegroundColor Cyan
        Write-Host "1. Zavřete tento PowerShell" -ForegroundColor White
        Write-Host "2. Otevřete nový PowerShell" -ForegroundColor White
        Write-Host "3. Spusťte: build_csharp.bat" -ForegroundColor White
        
    } else {
        Write-Host "Chyba při instalaci! Exit code: $($process.ExitCode)" -ForegroundColor Red
        Write-Host "Zkuste stáhnout a nainstalovat manuálně z:" -ForegroundColor Yellow
        Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "Chyba při stahování nebo instalaci: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Manuální instalace:" -ForegroundColor Yellow
    Write-Host "1. Přejděte na: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    Write-Host "2. Stáhněte 'SDK x64' pro Windows" -ForegroundColor White
    Write-Host "3. Spusťte installer jako administrator" -ForegroundColor White
} finally {
    # Vyčisti temp soubory
    if (Test-Path $installerPath) {
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Read-Host "Stiskněte Enter pro ukončení"