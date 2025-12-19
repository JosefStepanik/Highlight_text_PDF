# PDF Text Highlighter pro Windows

Moderní aplikace v jazyce C# s Windows Forms pro zvýrazňování textu v PDF souborech. Nativní Windows aplikace s .NET 6.0.

## Funkce

- **Výběr PDF souboru**: Tlačítko pro výběr PDF souboru z file dialogu
- **Textové vyhledávání**: Textové pole pro zadání hledaných výrazů oddělených čárkou
- **Zvýrazňování**: Červené obdélníky kolem nalezených textů
- **Navigace**: Procházení stránek pomocí šipek nebo Page Up/Down
- **Zoom**: Přiblížení a oddálení pomocí +/- kláves

## Systémové požadavky

### .NET 6.0 Runtime (Doporučeno)
- **Stáhněte z**: https://dotnet.microsoft.com/download/dotnet/6.0
- **Typ**: .NET Desktop Runtime 6.0 nebo novější
- **Platforma**: Windows x64

### Alternativně: Standalone verze
- Použijte `publish.bat` pro vytvoření samostatné verze
- Nevyžaduje instalaci .NET Runtime
- Větší velikost souboru (~70MB)

## Sestavení aplikace

### 1. Rychlé sestavení (doporučeno)
```cmd
build_csharp.bat
```

### 2. Manuální sestavení
```cmd
dotnet restore
dotnet build --configuration Release
```

### 3. Vytvoření standalone verze
```cmd
publish.bat
```
Vytvoří samostatný EXE soubor, který nepotřebuje .NET Runtime.

### 4. Vývojářská verze
```cmd
dotnet build --configuration Debug
```

## Spuštění aplikace

### 1. Pomocí batch souboru (doporučeno)
```cmd
run_csharp.bat
```

### 2. Přímé spuštění
```cmd
bin\Release\net6.0-windows\PdfHighlighter.exe
```

### 3. Standalone verze
```cmd
publish\PdfHighlighter.exe
```

### 4. Z Visual Studio
- Otevřete `PdfHighlighter.csproj`
- Stiskněte F5 nebo Ctrl+F5

## Použití

1. **Vyberte PDF soubor**: Klikněte na tlačítko "Vyberte PDF soubor" a vyberte PDF dokument
2. **Zadejte hledané výrazy**: Do textového pole zadejte slova nebo fráze oddělené čárkou
   - Příklad: `slovo1, dlouhá fráze, jiné slovo`
3. **Zvýrazněte text**: Klikněte na tlačítko "Zvýraznit"
4. **Navigace**: 
   - Šipky vlevo/vpravo nebo Page Up/Down pro změnu stránky
   - Klávesy + a - pro zoom in/out

## Klávesové zkratky

- **Page Down / →**: Následující stránka
- **Page Up / ←**: Předchozí stránka  
- **+**: Přiblížení (zoom in)
- **-**: Oddálení (zoom out)

## Struktura C# projektu

```
├── PdfHighlighter.csproj   # .NET projekt soubor
├── Program.cs              # Vstupní bod aplikace
├── MainForm.cs             # Hlavní formulář s GUI
├── build_csharp.bat        # Build skript
├── run_csharp.bat          # Spouštěcí skript
├── publish.bat             # Vytvoření standalone verze
├── bin/                    # Sestavené soubory
├── obj/                    # Dočasné build soubory
├── publish/                # Standalone verze
└── README.md               # Dokumentace
```

## Řešení problémů

### Aplikace se nespustí
1. **Zkontrolujte .NET Runtime**:
   ```cmd
   dotnet --list-runtimes
   ```
   Měli byste vidět "Microsoft.WindowsDesktop.App 6.0.x"

2. **Reinstalujte .NET Desktop Runtime**:
   - Stáhněte z https://dotnet.microsoft.com/download/dotnet/6.0
   - Zvolte "Desktop Runtime"

### Chyba: "The framework 'Microsoft.WindowsDesktop.App' version '6.0.0' was not found"
```cmd
# Stáhněte a nainstalujte .NET 6.0 Desktop Runtime
# Nebo použijte standalone verzi:
publish.bat
```

### Chyba při načítání PDF
- Zkontrolujte, zda je PDF soubor platný
- Zkuste jiný PDF soubor
- Restartujte aplikaci

### Pomalé vykreslování
- Snižte zoom pomocí trackbaru
- Zkontrolujte velikost PDF souboru
- Zavřete ostatní aplikace pro uvolnění paměti

### Chyba sestavení
```cmd
# Vyčistěte projekt a zkuste znovu
dotnet clean
dotnet restore
dotnet build
```

## Technické detaily

- **Jazyk**: C# (.NET 6.0)
- **GUI Framework**: Windows Forms
- **PDF knihovna**: PDFium.NET SDK
- **Kompatibilita**: Windows 10/11 (.NET 6.0+)
- **Build systém**: MSBuild / dotnet CLI
- **Závislosti**: Automaticky spravované přes NuGet

### Použité NuGet balíčky:
- `PDFium.NET.SDK` - PDF rendering a vyhledávání
- `System.Drawing.Common` - Grafické operace

## Licence

Tento projekt je dostupný pod MIT licencí.

## Autor

Vytvořeno pro zvýrazňování textu v PDF dokumentech.