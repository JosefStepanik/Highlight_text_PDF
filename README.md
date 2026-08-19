# PDF Text Highlighter pro Windows

Moderní aplikace v jazyce C# s Windows Forms pro zvýrazňování textu v PDF souborech. Nativní Windows aplikace s .NET 8.0.

## Funkce

- **Výběr PDF souboru**: Tlačítko pro výběr PDF souboru z file dialogu
- **Textové vyhledávání**: Textové pole pro zadání hledaných výrazů oddělených čárkou
- **Zvýrazňování**: Červené obdélníky kolem nalezených textů
- **Navigace**: Procházení stránek pomocí šipek nebo Page Up/Down
- **Zoom**: Přiblížení a oddálení pomocí +/- kláves

## Systémové požadavky

### .NET 8.0 Runtime (Doporuceno)
- **Stáhněte z**: https://dotnet.microsoft.com/download/dotnet/8.0
- **Typ**: .NET Desktop Runtime 8.0 nebo novější
- **Platforma**: Windows x64

### Alternativne: Standalone verze
- Použijte `publish.bat` pro vytvoření samostatné verze
- Nevyžaduje instalaci .NET Runtime
- Větší velikost souboru (~70MB)

## Skripty

Projekt vystaci se 2 batch soubory:

- `run_csharp.bat` - hlavni vyvojovy vstup pro build i spusteni
- `publish.bat` - vytvoreni standalone balicku pro distribuci

## Sestaveni a spusteni aplikace

### 1. Spusteni aplikace v Release rezimu (doporuceno)
```cmd
run_csharp.bat
```

### 2. Spusteni aplikace v Debug rezimu
```cmd
run_csharp.bat debug
```

### 3. Pouhe sestaveni bez spusteni
```cmd
run_csharp.bat build
```

### 4. Pouhe Debug sestaveni
```cmd
run_csharp.bat build-debug
```

### 5. Manualni sestaveni
```cmd
dotnet restore Highlight_text.sln
dotnet build Highlight_text.sln --configuration Release
```

### 6. Vytvoreni standalone verze
```cmd
publish.bat
```
Vytvoří samostatný EXE soubor, který nepotřebuje .NET Runtime.

### 7. Debug logovani vyhledavani (volitelne)
Vyhledávací logy lze zapnout bez změny kódu přes environment proměnné:

- `PDFHIGHLIGHTER_SEARCH_LOGS`: zapne základní search logy
- `PDFHIGHLIGHTER_SEARCH_LOGS_VERBOSE`: zapne detailní (verbose) search logy

#### PowerShell (pro aktuální okno)
```powershell
$env:PDFHIGHLIGHTER_SEARCH_LOGS = "1"
./run_csharp.bat debug
```

```powershell
$env:PDFHIGHLIGHTER_SEARCH_LOGS_VERBOSE = "1"
./run_csharp.bat debug
```

Vypnutí proměnných v aktuální PowerShell session:
```powershell
Remove-Item Env:PDFHIGHLIGHTER_SEARCH_LOGS -ErrorAction SilentlyContinue
Remove-Item Env:PDFHIGHLIGHTER_SEARCH_LOGS_VERBOSE -ErrorAction SilentlyContinue
```

#### CMD (pro aktuální okno)
```cmd
set PDFHIGHLIGHTER_SEARCH_LOGS=1
run_csharp.bat debug
```

```cmd
set PDFHIGHLIGHTER_SEARCH_LOGS_VERBOSE=1
run_csharp.bat debug
```

Poznámka: hodnoty `1`, `true`, `yes`, `on` jsou brány jako zapnuto (bez ohledu na velikost písmen).

## Spuštění aplikace

### 1. Pomoci batch souboru (doporuceno)
```cmd
run_csharp.bat
```

### 2. Primy Debug start
```cmd
run_csharp.bat debug
```

### 3. Prime spusteni pres dotnet CLI
```cmd
dotnet run --project PdfHighlighter.csproj --configuration Release
```

### 4. Standalone verze
```cmd
publish\PdfHighlighter.exe
```

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
├── MainForm.cs             # Sdílené proměnné a konstanty formuláře
├── MainForm.UI.cs          # Sestavení toolbaru, vieweru a status baru
├── MainForm.Events.cs      # Obsluha tlačítek a klávesových zkratek
├── MainForm.PdfHandling.cs # Načítání a vykreslování PDF
├── MainForm.Search.cs      # Hledání textu a výpočet highlightů
├── MainForm.Geometry.cs    # Převody souřadnic PDF -> obrazovka
├── favicon.ico             # Ikona aplikace
├── LOGO_1COLOR_SVG.svg     # Logo pro pravou část toolbaru
├── run_csharp.bat          # Hlavni build/run skript
├── publish.bat             # Vytvoreni standalone verze
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
   Měli byste vidět "Microsoft.WindowsDesktop.App 8.0.x"

2. **Reinstalujte .NET Desktop Runtime**:
   - Stáhněte z https://dotnet.microsoft.com/download/dotnet/8.0
   - Zvolte "Desktop Runtime"

### Chyba: "The framework 'Microsoft.WindowsDesktop.App' version '8.0.0' was not found"
```cmd
# Stáhněte a nainstalujte .NET 8.0 Desktop Runtime
# Nebo pouzijte standalone verzi:
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
dotnet restore Highlight_text.sln
dotnet build Highlight_text.sln --configuration Release
```

Pokud build selže chybou o zamčeném souboru PdfHighlighter.exe, zavřete běžící aplikaci a spusťte build znovu.

## Technické detaily

- **Jazyk**: C# (.NET 8.0)
- **GUI Framework**: Windows Forms
- **PDF knihovny**: iText7 + PdfiumViewer
- **Kompatibilita**: Windows 10/11 (.NET 8.0+)
- **Build systém**: MSBuild / dotnet CLI
- **Závislosti**: Automaticky spravované přes NuGet

### Použité NuGet balíčky:
- `itext7` - extrakce textu a pozic v PDF
- `PdfiumViewer` - renderování PDF stránek
- `PdfiumViewer.Native.x86_64.v8-xfa` - nativní PDFium knihovny
- `System.Drawing.Common` - Grafické operace
- `Svg` - vykreslení SVG loga do toolbaru

## Licence

Tento projekt je dostupný pod MIT licencí.

## Autor

Vytvořeno pro zvýrazňování textu v PDF dokumentech.