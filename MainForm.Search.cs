// =============================================================
// File: MainForm.Search.cs
// Purpose: Find search terms in PDF text and compute highlight rectangles.
// Contains: Text extraction flow, token matching logic, chunk/character heuristics, and fallback strategies.
// Author: Josef Stepanik
// Created: 2026-04
// =============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace PdfHighlighter
{
    // ===== Text Search & Highlighting - Hledání textu a tvorba zvýraznění =====
    public partial class MainForm : Form
    {
        private const float HorizontalAngleToleranceDeg = 15f;
        private static readonly bool EnableVerboseSearchLogs = GetEnvironmentFlag("PDFHIGHLIGHTER_SEARCH_LOGS_VERBOSE");
        private static readonly bool EnableCoreSearchLogs = GetEnvironmentFlag("PDFHIGHLIGHTER_SEARCH_LOGS") || EnableVerboseSearchLogs;

        private static bool GetEnvironmentFlag(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static void LogDebug(string message)
        {
            if (!EnableCoreSearchLogs)
                return;

            AppLogger.Log(message);
        }

        private static void LogVerboseSearch(string message)
        {
            if (EnableVerboseSearchLogs)
                LogDebug(message);
        }

        private static Action<string>? CreateMatchDetailLogger()
        {
            return EnableVerboseSearchLogs
                ? message => LogDebug($"[MATCH-DETAIL] {message}")
                : null;
        }

        // Hlavní vstup pro přepočet highlightů na aktuálně zobrazené stránce.
        // Volá se po načtení PDF, změně zoomu i po zadání hledaného textu.
        private void UpdateHighlights()
        {
            highlights.Clear();
            highlightTerms.Clear();

            if (pdfViewerDocument == null || searchTerms.Count == 0 || currentPageIndex >= pdfViewerDocument.PageCount)
                return;

            try
            {
                // iText vrací text a geometrii v PDF souřadnicích (body).
                var page = pdfDocument!.GetPage(currentPageIndex + 1);
                var pdfPageSize = page.GetPageSize();
                
                // Každý hledaný výraz zpracujeme samostatně, aby bylo možné
                // kombinovat více tokenů na stejné stránce.
                foreach (var term in searchTerms)
                {
                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    var trimmedTerm = term.Trim();
                    
                    // DEBUG: Log what we're searching for
                    LogDebug($"[DEBUG] Hledám text: '{trimmedTerm}' na stránce {currentPageIndex + 1}");
                    
                    // Pro každý výraz najdeme přesné pozice a převedeme je na obrazovku.
                    var realPositions = FindRealTextPositions(page, trimmedTerm, pdfPageSize);
                    
                    // DEBUG: Log results
                    LogDebug($"[DEBUG] Nalezeno {realPositions.Count} pozic pro '{trimmedTerm}'");
                    
                    foreach (var rect in realPositions)
                    {
                        highlights.Add(rect);
                        highlightTerms.Add(trimmedTerm);
                    }
                }

                SyncSelectedHighlightIndices();
                
                picPdfViewer.Invalidate(); // Vynutí překreslení nových highlightů.
                ApplySearchSummaryStatus();
            }
            catch (Exception ex)
            {
                SetStatusMessage($"Chyba při hledání textu: {ex.Message}", true);
                // Nouzová varianta: ukáže aspoň orientační obdélníky.
                CreateFallbackHighlights();
            }
        }

        // Projídá všechny stránky dokumentu, pro každou hledá všechny termy a sestavuje souhrn:
        // které termy byly nalezeny na které stránce, které chybí, a které mají více výskytů.
        // Výsledek uloží do polní *InSummary pro pozdější obnovení stavového textu.
        private void RebuildDocumentSearchSummary()
        {
            hasSearchSummary = false;
            foundTermsByPageSummary.Clear();
            totalSearchTermsInSummary = 0;
            missingTermsInSummary.Clear();
            multipleOccurrenceTermsInSummary.Clear();
            searchStatusText = string.Empty;
            searchErrorText = string.Empty;

            if (pdfDocument == null || searchTerms.Count == 0)
                return;

            var normalizedTerms = searchTerms
                .Select(term => term.Trim())
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedTerms.Count == 0)
                return;

            selectedHighlightTerms.IntersectWith(normalizedTerms);

            var foundTermsByPage = new List<(int pageNumber, List<string> terms)>();
            var foundInDocument = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var occurrenceCountsByTerm = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int pageIndex = 0; pageIndex < pdfDocument.GetNumberOfPages(); pageIndex++)
            {
                var page = pdfDocument.GetPage(pageIndex + 1);
                var pdfPageSize = page.GetPageSize();
                var foundOnPage = new List<string>();

                foreach (var term in normalizedTerms)
                {
                    var realPositions = FindRealTextPositions(page, term, pdfPageSize);
                    if (realPositions.Count == 0)
                        continue;

                    foundOnPage.Add(term);
                    foundInDocument.Add(term);

                    if (!occurrenceCountsByTerm.TryGetValue(term, out int currentCount))
                        currentCount = 0;

                    occurrenceCountsByTerm[term] = currentCount + realPositions.Count;
                }

                if (foundOnPage.Count > 0)
                {
                    foundTermsByPage.Add((pageIndex + 1, foundOnPage));
                }
            }

            var missingTerms = normalizedTerms
                .Where(term => !foundInDocument.Contains(term))
                .ToList();

            var multipleOccurrenceTerms = normalizedTerms
                .Where(term => occurrenceCountsByTerm.TryGetValue(term, out int count) && count > 1)
                .Select(term => (term, count: occurrenceCountsByTerm[term]))
                .ToList();

            var (statusText, errorText) = BuildDocumentSearchStatusText(
                foundTermsByPage,
                normalizedTerms.Count,
                missingTerms,
                multipleOccurrenceTerms);
            foundTermsByPageSummary = foundTermsByPage;
            totalSearchTermsInSummary = normalizedTerms.Count;
            missingTermsInSummary = missingTerms;
            multipleOccurrenceTermsInSummary = multipleOccurrenceTerms;
            searchStatusText = statusText;
            searchErrorText = errorText;
            hasSearchSummary = true;
        }

        // Sestavuje stavový text "Nalezené výskyty na straně X..." a varovný text pro chybějící/opakující termy.
        // Ke každé stránce přidává sufíf "(vybráno: X)" s počtem zelenvě označených termů.
        private (string statusText, string errorText) BuildDocumentSearchStatusText(
            List<(int pageNumber, List<string> terms)> foundTermsByPage,
            int totalTerms,
            List<string> missingTerms,
            List<(string term, int count)> multipleOccurrenceTerms)
        {
            var warningParts = new List<string>();

            if (missingTerms.Count > 0)
            {
                warningParts.Add($"Nenalezeno v celém dokumentu ({missingTerms.Count}/{totalTerms}): {string.Join(", ", missingTerms)}");
            }

            if (multipleOccurrenceTerms.Count > 0)
            {
                var multipleSummary = string.Join(
                    ", ",
                    multipleOccurrenceTerms.Select(item => $"{item.term} ({item.count}x)"));
                warningParts.Add($"Více výskytů v dokumentu: {multipleSummary}");
            }

            string warningText = warningParts.Count == 0 ? string.Empty : string.Join(" | ", warningParts);

            if (foundTermsByPage.Count == 0)
            {
                string noMatchesText = $"Nalezené výskyty v dokumentu (0/{totalTerms}): žádné";
                return (noMatchesText, warningText);
            }

            var pageLines = new List<string>();
            for (int i = 0; i < foundTermsByPage.Count; i++)
            {
                var pageSummary = foundTermsByPage[i];
                string prefix = i == 0 ? "Nalezené výskyty na straně" : "na straně";
                int greenTermsCount = pageSummary.terms.Count(term => selectedHighlightTerms.Contains(term));
                pageLines.Add(
                    $"{prefix} {pageSummary.pageNumber} ({pageSummary.terms.Count}/{totalTerms}): {string.Join(", ", pageSummary.terms)} (vybráno: {greenTermsCount})");
            }

            return (string.Join(Environment.NewLine, pageLines), warningText);
        }

        // Znovu sestavuje a zobrazuje stavový text ze uložených souhrnných dat.
        // Volá se po každé změně výběru (kliknutí na highlight), aby se aktualizovaly počty "vybráno: X".
        private void ApplySearchSummaryStatus()
        {
            if (!hasSearchSummary)
                return;

            var (statusText, errorText) = BuildDocumentSearchStatusText(
                foundTermsByPageSummary,
                totalSearchTermsInSummary,
                missingTermsInSummary,
                multipleOccurrenceTermsInSummary);

            SetStatusWithErrors(statusText, errorText);
        }

        // Vrátí přesné obrazovkové obdélníky pro jeden konkrétní výraz.
        // Hledání probíhá ve dvou krocích:
        // 1. uvnitř jednotlivých chunků,
        // 2. přes okna sousedních chunků, pokud je text v PDF rozdělený.
        private List<RectangleF> FindRealTextPositions(iText.Kernel.Pdf.PdfPage page, string searchTerm,
            iText.Kernel.Geom.Rectangle pdfPageSize)
        {
            var results = new List<RectangleF>();
            
            try
            {
                // Text z PDF bývá rozdělený do "chunků" podle interní struktury dokumentu.
                var textChunks = GetTextChunksWithPositions(page);
                var trimmedTerm = searchTerm.Trim();

                if (string.IsNullOrEmpty(trimmedTerm))
                    return results;
                
                // DEBUG: Log extracted text chunks
                LogVerboseSearch($"[DEBUG] Extrahovano {textChunks.Count} text chunků z PDF");
                foreach (var chunk in textChunks.Take(10)) // Show first 10 for debugging
                {
                    LogVerboseSearch($"[DEBUG] Chunk: '{chunk.Text}' na pozici ({chunk.Rectangle.GetX():F1}, {chunk.Rectangle.GetY():F1})");
                }
                
                bool enforceWordBoundaries = trimmedTerm.All(char.IsLetterOrDigit);
                bool isShortAlnumSearchTerm = IsShortAlnumSearchTerm(trimmedTerm);

                if (enforceWordBoundaries)
                    return FindAlnumTermMatches(textChunks, trimmedTerm, pdfPageSize, isShortAlnumSearchTerm);

                // Primární hledání: najít výraz uvnitř jednotlivých chunků.
                var chunkBasedRects = FindChunkBasedMatches(textChunks, trimmedTerm, pdfPageSize, enforceWordBoundaries);
                AddUniqueScreenRects(results, chunkBasedRects);

                return results;

                // Ponecháno záměrně níže pro čitelnost helperu.
                // (Původní inline implementace byla přesunuta do FindChunkBasedMatches.)
            }
            catch (Exception ex)
            {
                SetStatusMessage($"Chyba při hledání '{searchTerm}': {ex.Message}", true);
            }
            
            return results;
        }

        private static bool IsShortAlnumSearchTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return false;

            var visibleChars = term
                .Where(ch => !char.IsControl(ch) && ch != '\u200B' && ch != '\uFEFF')
                .ToList();

            if (visibleChars.Count == 0 || visibleChars.Count > 2)
                return false;

            return visibleChars.All(char.IsLetterOrDigit);
        }

        private List<RectangleF> FindAlnumTermMatches(
            List<TextChunk> textChunks,
            string trimmedTerm,
            iText.Kernel.Geom.Rectangle pdfPageSize,
            bool isShortAlnumSearchTerm)
        {
            var results = new List<RectangleF>();

            if (isShortAlnumSearchTerm)
            {
                var shortRects = FindMatchesByCharacterFlow(textChunks, trimmedTerm, pdfPageSize, horizontalOnly: true);
                AddUniqueScreenRects(results, shortRects);

                // U krátkých tokenů zachováme bezpečný režim bez chunk fallbacku,
                // ale přidáme i svislé/lokální hledání, aby se našly značky typu svislé C2.
                var shortVerticalRects = FindMatchesByVerticalCharacterFlow(textChunks, trimmedTerm, pdfPageSize);
                AddUniqueScreenRects(results, shortVerticalRects);

                LogDebug($"[SHORT-CHAR-FLOW] term='{trimmedTerm}', horizontal={shortRects.Count}, vertical={shortVerticalRects.Count}, total={results.Count}");
                return results;
            }

            // Přesná tokenová logika pro běžný vodorovný text.
            var characterFlowRects = FindMatchesByCharacterFlow(textChunks, trimmedTerm, pdfPageSize, horizontalOnly: true);
            AddUniqueScreenRects(results, characterFlowRects);
            LogDebug($"[CHAR-FLOW] term='{trimmedTerm}', matches={characterFlowRects.Count}, chunkFallbackUsed={true}");

            // Fallback pro svislé/natočené texty přes znakový tok.
            // Díky tomu i zde hlídáme celé tokeny (R20 != R200).
            var rotatedRects = FindMatchesByVerticalCharacterFlow(textChunks, trimmedTerm, pdfPageSize);
            AddUniqueScreenRects(results, rotatedRects);
            LogDebug($"[ROTATED-FALLBACK] term='{trimmedTerm}', matches={rotatedRects.Count}");

            // Pokud znakový tok něco vynechá kvůli rekonstrukci linek,
            // vrátíme do hry i původní chunk/cross-chunk fallback se stejnými boundary pravidly.
            var chunkFallbackRects = FindChunkBasedMatches(textChunks, trimmedTerm, pdfPageSize, enforceWordBoundaries: true);
            AddUniqueScreenRects(results, chunkFallbackRects);
            LogDebug($"[CHUNK-FALLBACK] term='{trimmedTerm}', matches={chunkFallbackRects.Count}");

            return results;
        }

        private static void AddUniqueScreenRects(List<RectangleF> target, IEnumerable<RectangleF> candidates)
        {
            foreach (var candidate in candidates)
            {
                AddUniqueScreenRect(target, candidate);
            }
        }

        private List<RectangleF> FindChunkBasedMatches(
            List<TextChunk> textChunks,
            string trimmedTerm,
            iText.Kernel.Geom.Rectangle pdfPageSize,
            bool enforceWordBoundaries,
            bool rotatedOnly = false)
        {
            var results = new List<RectangleF>();

            // Krátké alfanumerické tokeny (např. C1) jsou v chunk fallbacku náchylné
            // na falešné zásahy uvnitř delších značek (C112). V tomto režimu je přeskočíme.
            if (enforceWordBoundaries && trimmedTerm.Length <= 2)
                return results;

            // Primární hledání: najít výraz uvnitř jednotlivých chunků.
                for (int chunkIndex = 0; chunkIndex < textChunks.Count; chunkIndex++)
                {
                    var chunk = textChunks[chunkIndex];

                    if (rotatedOnly && IsAngleNearHorizontal(chunk.AngleDeg))
                        continue;

                    char? leftNeighborChar = GetAdjacentChunkBoundaryChar(textChunks, chunkIndex, searchLeft: true);
                    char? rightNeighborChar = GetAdjacentChunkBoundaryChar(textChunks, chunkIndex, searchLeft: false);

                    LogVerboseSearch(
                        $"[MATCH-CHECK] term='{trimmedTerm}', mode=single, chunkIndex={chunkIndex}, chunkText='{chunk.Text}', leftNeighbor='{FormatDebugChar(leftNeighborChar)}', rightNeighbor='{FormatDebugChar(rightNeighborChar)}', enforceBoundaries={enforceWordBoundaries}");

                    var matches = FindTermMatchesInChunk(
                        chunk.Text,
                        trimmedTerm,
                        enforceWordBoundaries,
                        leftNeighborChar,
                        rightNeighborChar,
                        CreateMatchDetailLogger(),
                        $"single chunkIndex={chunkIndex}");
                    if (matches.Count == 0)
                        continue;

                    foreach (var match in matches)
                    {
                        // Přes substring vypočítáme co nejmenší PDF obdélník pouze pro nalezenou část,
                        // ne pro celý chunk. Tím se highlight lépe trefí na skutečný text.
                        var pdfRect = GetSubstringRectangle(chunk, match.start, match.length);
                        if (!IsReasonablePdfRect(pdfRect, pdfPageSize))
                            continue;

                        var screenRect = ExpandAndClampScreenRect(ConvertPdfCoordsToScreen(pdfRect, pdfPageSize), HighlightPaddingPx);
                        AddUniqueScreenRect(results, screenRect);
                    }
                }

                // Sekundární hledání: token může být rozdělen přes více sousedních chunků.
                var crossChunkRects = FindCrossChunkMatches(textChunks, trimmedTerm, pdfPageSize, rotatedOnly);
                foreach (var screenRect in crossChunkRects)
                {
                    AddUniqueScreenRect(results, screenRect);
                }

            return results;
        }

        // Vyhledá všechny pozice termu v jednom textovém chunku.
        // Vrací pouze index a délku, samotná geometrie se dopočítává až později.
        private static List<(int start, int length)> FindTermMatchesInChunk(
            string text,
            string term,
            bool enforceWordBoundaries,
            char? leftNeighborChar = null,
            char? rightNeighborChar = null,
            Action<string>? debugLog = null,
            string debugScope = "")
        {
            var matches = new List<(int start, int length)>();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return matches;

            int startIndex = 0;

            while (startIndex < text.Length)
            {
                int found = text.IndexOf(term, startIndex, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                    break;

                char firstMatchedChar = text[found];
                char lastMatchedChar = text[found + term.Length - 1];

                char? leftOuterChar = found == 0 ? leftNeighborChar : text[found - 1];
                char? rightOuterChar = found + term.Length >= text.Length
                    ? rightNeighborChar
                    : text[found + term.Length];

                bool isVeryShortAlphaNumericTerm = term.Length <= 2 && term.All(char.IsLetterOrDigit);

                bool leftBoundaryOk;
                bool rightBoundaryOk;
                if (isVeryShortAlphaNumericTerm)
                {
                    // U krátkých tokenů (např. C1) nepovolujeme hranici přes alnum přechody,
                    // jinak vznikají falešné zásahy uvnitř řetězců jako C99C1 nebo C112.
                    leftBoundaryOk = IsBoundaryChar(leftOuterChar);
                    rightBoundaryOk = IsBoundaryChar(rightOuterChar);
                }
                else
                {
                    leftBoundaryOk = IsBoundaryChar(leftOuterChar) || IsTokenBoundaryBetween(leftOuterChar, firstMatchedChar);
                    rightBoundaryOk = IsBoundaryChar(rightOuterChar) || IsTokenBoundaryBetween(lastMatchedChar, rightOuterChar);
                }

                bool boundaryOk = !enforceWordBoundaries || (leftBoundaryOk && rightBoundaryOk);

                debugLog?.Invoke(
                    $"scope='{debugScope}', term='{term}', text='{text}', foundAt={found}, leftBoundaryOk={leftBoundaryOk}, rightBoundaryOk={rightBoundaryOk}, accepted={boundaryOk}");

                if (boundaryOk)
                    matches.Add((found, term.Length));

                startIndex = found + Math.Max(1, term.Length);
            }

            return matches;
        }

        // Vrátí true, pokud je znak hranice tokenu (null = konec textu, nebo nealfanumerický).
        private static bool IsBoundaryChar(char? c)
        {
            return !c.HasValue || !char.IsLetterOrDigit(c.Value);
        }

        // V technických popisech bývají tokeny bez mezery nalepené za sebe,
        // např. "C140R6". Přechod písmeno<->číslo bereme jako hranici tokenu.
        private static bool IsTokenBoundaryBetween(char? leftChar, char? rightChar)
        {
            if (!leftChar.HasValue || !rightChar.HasValue)
                return false;

            bool leftIsLetter = char.IsLetter(leftChar.Value);
            bool leftIsDigit = char.IsDigit(leftChar.Value);
            bool rightIsLetter = char.IsLetter(rightChar.Value);
            bool rightIsDigit = char.IsDigit(rightChar.Value);

            return (leftIsLetter && rightIsDigit) || (leftIsDigit && rightIsLetter);
        }

        // Formátuje znak pro debug výstup - nahradí biele znaky čitelnou značkou (<space>, <tab>, atd.).
        private static string FormatDebugChar(char? c)
        {
            if (!c.HasValue)
                return "<none>";

            return c.Value switch
            {
                ' ' => "<space>",
                '\t' => "<tab>",
                '\r' => "<cr>",
                '\n' => "<lf>",
                _ => c.Value.ToString()
            };
        }

        // Hledá nejbližší sousední chunk vlevo nebo vpravo a vrací jeho okrajový znak.
        // Slouží k určení, zda má chunk na okraji sousedící alfanumerický znak (důležité pro boundary check).
        private static char? GetAdjacentChunkBoundaryChar(List<TextChunk> chunks, int index, bool searchLeft)
        {
            if (index < 0 || index >= chunks.Count)
                return null;

            var current = chunks[index];
            int step = searchLeft ? -1 : 1;
            int inspected = 0;
            const int boundaryProbeChunkLimit = 8;

            for (int i = index + step; i >= 0 && i < chunks.Count; i += step)
            {
                var candidate = chunks[i];
                if (string.IsNullOrEmpty(candidate.Text))
                    continue;

                inspected++;

                bool seemsContinuous = AreChunksNeighbors(current, candidate)
                                       || AreChunksLikelySameToken(current, candidate)
                                       || AreChunksLikelyContinuousText(current, candidate);

                if (seemsContinuous)
                {
                    return searchLeft
                        ? candidate.Text[candidate.Text.Length - 1]
                        : candidate.Text[0];
                }

                if (inspected >= boundaryProbeChunkLimit)
                    break;
            }

            return null;
        }

        // Rekonstruuje řádky textu ze znakové geometrie a hledá term v textu každé řádky.
        // Horizontální režim: zpracovává pouze znaky, které mají horizontální smer textu.
        private List<RectangleF> FindMatchesByCharacterFlow(
            List<TextChunk> chunks,
            string term,
            iText.Kernel.Geom.Rectangle pageRect,
            bool horizontalOnly)
        {
            var results = new List<RectangleF>();
            var pageCharacters = BuildPageCharacters(
                chunks,
                chunk => horizontalOnly ? IsAngleNearHorizontal(chunk.AngleDeg) : true);

            if (pageCharacters.Count == 0)
            {
                LogDebug($"[CHAR-FLOW] term='{term}', no usable characters extracted");
                return results;
            }

            var lines = BuildCharacterLines(pageCharacters);
            LogDebug($"[CHAR-FLOW] term='{term}', characters={pageCharacters.Count}, lines={lines.Count}");

            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                string lineText = BuildLineSearchText(line, out var characterMap);

                if (string.IsNullOrEmpty(lineText))
                    continue;

                LogVerboseSearch($"[CHAR-LINE] term='{term}', lineIndex={lineIndex}, text='{lineText}'");

                var matches = FindTermMatchesInChunk(
                    lineText,
                    term,
                    enforceWordBoundaries: true,
                    debugLog: CreateMatchDetailLogger(),
                    debugScope: $"char-flow lineIndex={lineIndex}");

                AddLineMatchesToResults(results, matches, characterMap, pageRect);
            }

            return results;
        }

        // Analogie FindMatchesByCharacterFlow pro otočený (svisle/šikmo) text.
        // Pro alfanumerické termy používá lokální sekvenci namísto line-based přístupu.
        private List<RectangleF> FindMatchesByVerticalCharacterFlow(
            List<TextChunk> chunks,
            string term,
            iText.Kernel.Geom.Rectangle pageRect)
        {
            var results = new List<RectangleF>();
            var pageCharacters = BuildPageCharacters(chunks, chunk => !IsAngleNearHorizontal(chunk.AngleDeg));
            bool isAlphaNumericTerm = term.All(char.IsLetterOrDigit);

            if (pageCharacters.Count == 0)
            {
                LogDebug($"[CHAR-FLOW-V] term='{term}', no usable rotated characters extracted");
                return results;
            }

            // U alfanumerických značek je line-based rekonstrukce pro rotovaný text
            // v tomto PDF příliš hlučná, proto používáme přesnější lokální geometrii.
            if (isAlphaNumericTerm)
            {
                AddLocalVerticalSequenceMatches(results, pageCharacters, term, pageRect);
                LogDebug($"[CHAR-FLOW-V] term='{term}', characters={pageCharacters.Count}, lines=0 (local-only)");
                return results;
            }

            var lines = BuildVerticalCharacterLines(pageCharacters);
            LogDebug($"[CHAR-FLOW-V] term='{term}', characters={pageCharacters.Count}, lines={lines.Count}");

            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                string lineText = BuildVerticalLineSearchText(line, out var characterMap);
                if (string.IsNullOrEmpty(lineText))
                    continue;

                LogVerboseSearch($"[CHAR-LINE-V] term='{term}', lineIndex={lineIndex}, text='{lineText}'");

                // Směr čtení u otočeného textu není v PDF konzistentní, proto testujeme oba směry.
                AddMatchesFromTextAndMap(results, term, pageRect, lineText, characterMap, $"char-flow-vertical lineIndex={lineIndex} forward");

                string reversedText = new string(lineText.Reverse().ToArray());
                var reversedMap = characterMap.AsEnumerable().Reverse().ToList();
                AddMatchesFromTextAndMap(results, term, pageRect, reversedText, reversedMap, $"char-flow-vertical lineIndex={lineIndex} reverse");
            }

            return results;
        }

        // Hledá sekvence otočených znaků tvořící hledaný term lokálním geometrickým přístupem.
        // Spouští se pro každý potenciální začátek (shoda prvního znaku termu).
        private void AddLocalVerticalSequenceMatches(
            List<RectangleF> results,
            List<PageCharacter> characters,
            string term,
            iText.Kernel.Geom.Rectangle pageRect)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                return;

            string normalizedTerm = term.ToLowerInvariant();
            char firstChar = normalizedTerm[0];
            var starts = characters
                .Where(c => char.ToLowerInvariant(c.Value) == firstChar)
                .ToList();

            var rawCandidates = new List<LocalVerticalMatchCandidate>();

            foreach (var start in starts)
            {
                var forwardCandidate = TryBuildLocalVerticalMatch(characters, normalizedTerm, pageRect, start, forward: true);
                var reverseCandidate = TryBuildLocalVerticalMatch(characters, normalizedTerm, pageRect, start, forward: false);
                var selected = SelectPreferredLocalVerticalCandidate(forwardCandidate, reverseCandidate);

                if (selected != null)
                    rawCandidates.Add(selected);
            }

            var mergedCandidates = MergeLocalVerticalCandidates(rawCandidates);
            foreach (var candidate in mergedCandidates)
            {
                AddUniqueScreenRect(results, candidate.ScreenRect);
            }

            LogDebug($"[LOCAL-V] term='{term}', starts={starts.Count}, matches={mergedCandidates.Count}");
        }

        // Pokusí se sestavit lokální sekvenci otočených znaků od počátečního znaku.
        // Vrací kandidnáta s obdelníkem a skóre kvality, nebo null pokud sekvence nesplnila podmínky.
        private LocalVerticalMatchCandidate? TryBuildLocalVerticalMatch(
            List<PageCharacter> allCharacters,
            string normalizedTerm,
            iText.Kernel.Geom.Rectangle pageRect,
            PageCharacter start,
            bool forward)
        {
            var used = new HashSet<PageCharacter> { start };
            var sequence = new List<PageCharacter> { start };
            var current = start;

            for (int i = 1; i < normalizedTerm.Length; i++)
            {
                char expected = normalizedTerm[i];
                var next = FindBestNextVerticalCharacter(allCharacters, current, start, expected, used, forward);
                if (next == null)
                    return null;

                sequence.Add(next);
                used.Add(next);
                current = next;
            }

            if (!HasConsistentSourceChunkOrder(sequence, normalizedTerm))
                return null;

            if (!HasLocalTokenBoundaries(allCharacters, sequence, forward))
                return null;

            float averageSpan = sequence.Average(c => Math.Max(c.Width, c.Height));
            float axisSpan = Math.Abs(sequence[0].AxisProjection - sequence[sequence.Count - 1].AxisProjection);
            float maxAxisSpan = Math.Max(20f, averageSpan * (normalizedTerm.Length + 0.6f));
            if (axisSpan > maxAxisSpan)
                return null;

            var rects = sequence.Select(c => c.Rectangle).ToList();
            var pdfRect = UnionPdfRectangles(rects);
            if (!IsReasonablePdfRect(pdfRect, pageRect))
                return null;

            var screenRect = ExpandAndClampScreenRect(ConvertPdfCoordsToScreen(pdfRect, pageRect), HighlightPaddingPx);
            string sequenceText = new string(sequence.Select(c => c.Value).ToArray());
            float quality = (float)(pdfRect.GetWidth() * pdfRect.GetHeight()) + axisSpan * 0.75f;

            return new LocalVerticalMatchCandidate
            {
                ScreenRect = screenRect,
                PdfRect = pdfRect,
                SequenceText = sequenceText,
                Forward = forward,
                QualityScore = quality,
                SourceConfidence = ComputeSourceConfidence(sequence)
            };
        }

        // Vybere lepšího z dopredného a zpětného kandidáta podle SourceConfidence a pak QualityScore.
        private static LocalVerticalMatchCandidate? SelectPreferredLocalVerticalCandidate(
            LocalVerticalMatchCandidate? forwardCandidate,
            LocalVerticalMatchCandidate? reverseCandidate)
        {
            if (forwardCandidate == null)
                return reverseCandidate;

            if (reverseCandidate == null)
                return forwardCandidate;

            if (forwardCandidate.SourceConfidence != reverseCandidate.SourceConfidence)
                return forwardCandidate.SourceConfidence > reverseCandidate.SourceConfidence ? forwardCandidate : reverseCandidate;

            return forwardCandidate.QualityScore <= reverseCandidate.QualityScore ? forwardCandidate : reverseCandidate;
        }

        // Spočítá důvěryhodnost sekvence (2 = přímá sousední znaky ze stejného chunku, 1 = stejný chunk, 0 = více chunků).
        private static int ComputeSourceConfidence(List<PageCharacter> sequence)
        {
            if (sequence.Count == 0)
                return 0;

            int sourceChunk = sequence[0].SourceChunkIndex;
            if (sourceChunk < 0 || sequence.Any(c => c.SourceChunkIndex != sourceChunk || c.SourceCharIndex < 0))
                return 0;

            var ordered = sequence.OrderBy(c => c.SourceCharIndex).ToList();
            bool contiguous = true;
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].SourceCharIndex != ordered[i - 1].SourceCharIndex + 1)
                {
                    contiguous = false;
                    break;
                }
            }

            return contiguous ? 2 : 1;
        }

        // Slučí dupliktní kandidáty (překrývající se obdelníky) do jedné sady preferovaných výsledků.
        private static List<LocalVerticalMatchCandidate> MergeLocalVerticalCandidates(List<LocalVerticalMatchCandidate> candidates)
        {
            var merged = new List<LocalVerticalMatchCandidate>();

            foreach (var candidate in candidates)
            {
                int equivalentIndex = merged.FindIndex(existing => AreEquivalentLocalCandidates(existing, candidate));
                if (equivalentIndex < 0)
                {
                    merged.Add(candidate);
                    continue;
                }

                var preferred = SelectPreferredLocalVerticalCandidate(merged[equivalentIndex], candidate);
                if (preferred != null)
                    merged[equivalentIndex] = preferred;
            }

            return merged;
        }

        // Vrátí true, pokud se dva kandidáti považují za ekvivalentní (překryv >= 72% menšího obdelníku).
        private static bool AreEquivalentLocalCandidates(LocalVerticalMatchCandidate a, LocalVerticalMatchCandidate b)
        {
            var intersection = RectangleF.Intersect(a.ScreenRect, b.ScreenRect);
            if (intersection.Width <= 0 || intersection.Height <= 0)
                return false;

            float intersectionArea = intersection.Width * intersection.Height;
            float aArea = a.ScreenRect.Width * a.ScreenRect.Height;
            float bArea = b.ScreenRect.Width * b.ScreenRect.Height;
            float minArea = Math.Max(1f, Math.Min(aArea, bArea));

            // Silný překryv značí stejný nalezený token přes jiný start/směr.
            return intersectionArea / minArea >= 0.72f;
        }

        // Pro daný aktuální znak (current) najde nejlepší následující/předchozí znak ve směru otočeného textu.
        // Hodnotití funkčí skóre: kombinace vzdálenosti po ose, vzdálenosti od normaly a úhlové odchylky.
        private static PageCharacter? FindBestNextVerticalCharacter(
            List<PageCharacter> allCharacters,
            PageCharacter current,
            PageCharacter anchor,
            char expected,
            HashSet<PageCharacter> used,
            bool forward)
        {
            PageCharacter? best = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in allCharacters)
            {
                if (used.Contains(candidate))
                    continue;

                if (char.ToLowerInvariant(candidate.Value) != expected)
                    continue;

                float angleDelta = SmallestAngleDelta(anchor.AngleDeg, candidate.AngleDeg);
                if (angleDelta > 20f)
                    continue;

                float spanCurrent = Math.Max(current.Width, current.Height);
                float spanCandidate = Math.Max(candidate.Width, candidate.Height);
                float normalTolerance = Math.Max(2.2f, Math.Min(spanCurrent, spanCandidate) * 0.55f);
                float normalDistance = Math.Abs(current.NormalProjection - candidate.NormalProjection);
                if (normalDistance > normalTolerance)
                    continue;

                float axisDelta = forward
                    ? current.AxisProjection - candidate.AxisProjection
                    : candidate.AxisProjection - current.AxisProjection;

                float expectedStep = (spanCurrent + spanCandidate) * 0.5f;
                float minStep = Math.Max(0.4f, expectedStep * 0.15f);
                float maxStep = Math.Max(8f, expectedStep * 1.35f);
                if (axisDelta <= minStep || axisDelta > maxStep)
                    continue;

                if (HasBlockingAlignedAlnumCharacter(allCharacters, current, candidate, used))
                    continue;

                float score = Math.Abs(axisDelta - expectedStep) * 2.2f + normalDistance * 3.0f + angleDelta;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        // Ověří, zda jsou znaky na okraji sekvence odděleny od sousedních alfanumerických znaků.
        // Kratší tokeny vyžadují striktnější oddelení než delší.
        private static bool HasLocalTokenBoundaries(List<PageCharacter> allCharacters, List<PageCharacter> sequence, bool forward)
        {
            if (sequence.Count == 0)
                return false;

            var first = sequence[0];
            var last = sequence[sequence.Count - 1];

            // Pro směr čtení určíme znak před tokenem a za tokenem podél osy textu.
            var before = FindAdjacentCharacterAlongAxis(allCharacters, first, lookHigherAxis: forward, sequence);
            var after = FindAdjacentCharacterAlongAxis(allCharacters, last, lookHigherAxis: !forward, sequence);

            bool isAlphaNumericTerm = sequence.All(c => char.IsLetterOrDigit(c.Value));
            if (isAlphaNumericTerm)
            {
                // Krátké tokeny (např. C6) jsou náchylné na falešné zásahy uvnitř C69/C68,
                // proto vyžadujeme striktní nealfanumerické ohraničení.
                if (sequence.Count <= 2)
                {
                    bool shortLeftOk = before == null || !char.IsLetterOrDigit(before.Value);
                    bool shortRightOk = after == null || !char.IsLetterOrDigit(after.Value);
                    bool sourceLeftOk = !HasAdjacentSourceAlnumOutsideSequence(allCharacters, sequence, first, direction: -1);
                    bool sourceRightOk = !HasAdjacentSourceAlnumOutsideSequence(allCharacters, sequence, last, direction: +1);
                    return shortLeftOk && shortRightOk && sourceLeftOk && sourceRightOk;
                }

                // U delších značek typu C77 blokujeme pouze těsně navazující alfanumerické sousedy
                // (např. 77C), ale vzdálenější znaky považujeme za jiný token.
                float averageSpan = sequence.Average(c => Math.Max(c.Width, c.Height));
                bool strictLeftOk = IsSeparatedTokenNeighbor(first, before, averageSpan);
                bool strictRightOk = IsSeparatedTokenNeighbor(last, after, averageSpan);
                return strictLeftOk && strictRightOk;
            }

            char firstChar = first.Value;
            char lastChar = last.Value;

            bool leftOk = before == null
                          || !char.IsLetterOrDigit(before.Value)
                          || IsTokenBoundaryBetween(before.Value, firstChar);

            bool rightOk = after == null
                           || !char.IsLetterOrDigit(after.Value)
                           || IsTokenBoundaryBetween(lastChar, after.Value);

            return leftOk && rightOk;
        }

        // Pro krátké alfanumerické tokeny ověřuje, zda na okraji sekvence
        // nepokračuje stejný zdrojový chunk dalším alfanumerickým znakem.
        // Tím zabráníme dílčím zásahům typu C1 uvnitř C112.
        private static bool HasAdjacentSourceAlnumOutsideSequence(
            List<PageCharacter> allCharacters,
            List<PageCharacter> sequence,
            PageCharacter edge,
            int direction)
        {
            if (edge.SourceChunkIndex < 0 || edge.SourceCharIndex < 0)
                return false;

            int targetSourceCharIndex = edge.SourceCharIndex + direction;
            if (targetSourceCharIndex < 0)
                return false;

            foreach (var candidate in allCharacters)
            {
                if (candidate.SourceChunkIndex != edge.SourceChunkIndex)
                    continue;

                if (candidate.SourceCharIndex != targetSourceCharIndex)
                    continue;

                if (sequence.Contains(candidate))
                    continue;

                if (!char.IsLetterOrDigit(candidate.Value))
                    continue;

                return true;
            }

            return false;
        }

        // Určí, zda je sousední znak oddělen od okraje sekvence (jiný chunk nebo nepřímý soused ve zdroji).
        private static bool IsSeparatedTokenNeighbor(PageCharacter edge, PageCharacter? neighbor, float averageSpan)
        {
            if (neighbor == null)
                return true;

            if (!char.IsLetterOrDigit(neighbor.Value))
                return true;

            bool hasSourceInfo = edge.SourceChunkIndex >= 0
                                 && neighbor.SourceChunkIndex >= 0
                                 && edge.SourceCharIndex >= 0
                                 && neighbor.SourceCharIndex >= 0;

            if (hasSourceInfo && edge.SourceChunkIndex == neighbor.SourceChunkIndex)
            {
                // Přímý soused ve stejném tokenovém chunku = nejspíš pokračování stejného řetězce (např. 77C).
                int sourceGap = Math.Abs(edge.SourceCharIndex - neighbor.SourceCharIndex);
                return sourceGap > 1;
            }

            // Znaky z jiných chunků mohou být geometricky blízko, ale patří jinému labelu.
            return true;
        }

        // Interní datová třída pro kandidnáty nalezené lokální vertikální sekvencí. Obsahuje skóre kvality
        // pro porovnání předního/zpětného směru a eliminaci duplicit.
        private sealed class LocalVerticalMatchCandidate
        {
            public RectangleF ScreenRect { get; set; }
            public iText.Kernel.Geom.Rectangle PdfRect { get; set; } = new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);
            public string SequenceText { get; set; } = string.Empty;
            public bool Forward { get; set; }
            public float QualityScore { get; set; }
            public int SourceConfidence { get; set; }
        }

        // Ověří, zda znaky v sekvenci pochazíjí ze stejného chunku a ve správném pořadí (kontiguoušní indexy).
        private static bool HasConsistentSourceChunkOrder(List<PageCharacter> sequence, string normalizedTerm)
        {
            if (sequence.Count == 0)
                return false;

            int sourceChunkIndex = sequence[0].SourceChunkIndex;
            if (sourceChunkIndex < 0)
                return true;

            // Ověření pořadí provedeme jen pokud všechny znaky pochází ze stejného chunku.
            if (sequence.Any(c => c.SourceChunkIndex != sourceChunkIndex || c.SourceCharIndex < 0))
                return true;

            var orderedBySource = sequence.OrderBy(c => c.SourceCharIndex).ToList();
            for (int i = 1; i < orderedBySource.Count; i++)
            {
                if (orderedBySource[i].SourceCharIndex != orderedBySource[i - 1].SourceCharIndex + 1)
                    return false;
            }

            string sourceOrderedText = new string(orderedBySource.Select(c => char.ToLowerInvariant(c.Value)).ToArray());
            return sourceOrderedText.Equals(normalizedTerm, StringComparison.Ordinal);
        }

        // Detekuje, zda mezi dvěma znaky existuje blokkující alfanumerický znak na stejné ose.
        // Chrání před spojením znaků, které prostřední znak dělí na jiné tokeny.
        private static bool HasBlockingAlignedAlnumCharacter(
            List<PageCharacter> allCharacters,
            PageCharacter from,
            PageCharacter to,
            HashSet<PageCharacter> excluded)
        {
            float minAxis = Math.Min(from.AxisProjection, to.AxisProjection);
            float maxAxis = Math.Max(from.AxisProjection, to.AxisProjection);

            float angleTolerance = 20f;
            float normalTolerance = Math.Max(2.4f, Math.Min(Math.Max(from.Width, from.Height), Math.Max(to.Width, to.Height)) * 0.7f);

            foreach (var candidate in allCharacters)
            {
                if (excluded.Contains(candidate) || ReferenceEquals(candidate, from) || ReferenceEquals(candidate, to))
                    continue;

                if (!char.IsLetterOrDigit(candidate.Value))
                    continue;

                if (candidate.AxisProjection <= minAxis + 0.05f || candidate.AxisProjection >= maxAxis - 0.05f)
                    continue;

                float fromAngle = SmallestAngleDelta(from.AngleDeg, candidate.AngleDeg);
                float toAngle = SmallestAngleDelta(to.AngleDeg, candidate.AngleDeg);
                if (fromAngle > angleTolerance || toAngle > angleTolerance)
                    continue;

                float normalDistanceFrom = Math.Abs(from.NormalProjection - candidate.NormalProjection);
                float normalDistanceTo = Math.Abs(to.NormalProjection - candidate.NormalProjection);
                if (normalDistanceFrom > normalTolerance || normalDistanceTo > normalTolerance)
                    continue;

                return true;
            }

            return false;
        }

        // Najde nejbližší znak podrél osy textu (před nebo za) mimo samotnou sekvenci.
        // Používá se k ověření, zda za/před tokenem následuje volné místo.
        private static PageCharacter? FindAdjacentCharacterAlongAxis(
            List<PageCharacter> allCharacters,
            PageCharacter edge,
            bool lookHigherAxis,
            List<PageCharacter> excluded)
        {
            PageCharacter? best = null;
            float bestAxisDistance = float.MaxValue;

            foreach (var candidate in allCharacters)
            {
                if (excluded.Contains(candidate))
                    continue;

                float angleDelta = SmallestAngleDelta(edge.AngleDeg, candidate.AngleDeg);
                if (angleDelta > 20f)
                    continue;

                float normalTolerance = Math.Max(2.5f, Math.Min(edge.Width, candidate.Width) * 0.7f);
                float normalDistance = Math.Abs(edge.NormalProjection - candidate.NormalProjection);
                if (normalDistance > normalTolerance)
                    continue;

                float axisDistance = lookHigherAxis
                    ? candidate.AxisProjection - edge.AxisProjection
                    : edge.AxisProjection - candidate.AxisProjection;

                if (axisDistance <= 0f || axisDistance > Math.Max(14f, Math.Max(edge.Height, candidate.Height) * 2.2f))
                    continue;

                if (axisDistance < bestAxisDistance)
                {
                    bestAxisDistance = axisDistance;
                    best = candidate;
                }
            }

            return best;
        }

        // Extrahuje jednotlivé znaky ze všech chunků s jejich pozicí v PDF.
        // Pokud chunk má geometrii pro všechny znaky, použije ji; jinak odhadně pozici z celkového obdelníku.
        private static List<PageCharacter> BuildPageCharacters(List<TextChunk> chunks, Func<TextChunk, bool> includeChunk)
        {
            var characters = new List<PageCharacter>();

            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                if (string.IsNullOrEmpty(chunk.Text))
                    continue;

                if (!includeChunk(chunk))
                    continue;

                bool canUseCharacterGeometry = chunk.CharacterRectangles.Count == chunk.Text.Length;
                for (int index = 0; index < chunk.Text.Length; index++)
                {
                    char value = chunk.Text[index];
                    if (char.IsControl(value))
                        continue;

                    var rect = canUseCharacterGeometry
                        ? chunk.CharacterRectangles[index]
                        : EstimateSubstringRectangle(chunk.Rectangle, chunk.Text, index, 1);

                    if (rect.GetWidth() <= 0 || rect.GetHeight() <= 0)
                        continue;

                    characters.Add(new PageCharacter
                    {
                        Value = value,
                        Rectangle = rect,
                        AngleDeg = chunk.AngleDeg,
                        SourceChunkIndex = chunkIndex,
                        SourceCharIndex = index
                    });
                }
            }

            return characters;
        }

        // Seskupí znaky do řádek podle Y souřadnice (s tolerancí). Každá řádka je seřazena zleva doprava.
        private static List<CharacterLine> BuildCharacterLines(List<PageCharacter> characters)
        {
            var orderedChars = characters
                .OrderByDescending(c => c.CenterY)
                .ThenBy(c => c.Left)
                .ToList();

            var lines = new List<CharacterLine>();

            foreach (var character in orderedChars)
            {
                CharacterLine? bestLine = null;
                float bestDistance = float.MaxValue;

                foreach (var line in lines)
                {
                    // Zvýšená tolerance pro lépe seskupování znaků na stejné řádce
                    float tolerance = Math.Max(3f, Math.Max(line.AverageHeight, character.Height) * 1.1f);
                    float distance = Math.Abs(line.AverageCenterY - character.CenterY);
                    if (distance > tolerance || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestLine = line;
                }

                if (bestLine == null)
                {
                    bestLine = new CharacterLine();
                    lines.Add(bestLine);
                }

                bestLine.AddCharacter(character);
            }

            foreach (var line in lines)
            {
                line.Characters.Sort((a, b) => a.Left.CompareTo(b.Left));
            }

            return lines
                .OrderByDescending(line => line.AverageCenterY)
                .ToList();
        }

        // Ze znakové řádky sestavuje hledací řetězec a mapu pro zpětné mapování pozíce na PDF obdelník.
        // Vkládá mezery, když je mezera mezi znaky dostatečně velká.
        private static string BuildLineSearchText(CharacterLine line, out List<PageCharacter?> characterMap)
        {
            characterMap = new List<PageCharacter?>();
            var chars = new List<char>();
            float avgWidth = line.AverageWidth;
            float spacingThreshold = Math.Max(1.0f, avgWidth * 0.5f);

            for (int i = 0; i < line.Characters.Count; i++)
            {
                var current = line.Characters[i];

                if (i > 0)
                {
                    var previous = line.Characters[i - 1];
                    float gap = current.Left - previous.Right;

                    // Přidáme mezeru, jen pokud je výrazně větší mezera
                    // Nezměňujeme věci, které by měly být spojeny
                    bool shouldInsertSpace = gap > spacingThreshold
                                             && !AreSourceAdjacentCharacters(previous, current);
                    if (shouldInsertSpace)
                    {
                        chars.Add(' ');
                        characterMap.Add(null);
                    }
                }

                chars.Add(current.Value);
                characterMap.Add(char.IsWhiteSpace(current.Value) ? null : current);
            }

            return new string(chars.ToArray());
        }

        // Seskupí otočené znaky do "svislých řádek" podle normálové projekce.
        // Analog fce k BuildCharacterLines, ale pracuje s projekcí místo Y souřadnicí.
        private static List<CharacterLine> BuildVerticalCharacterLines(List<PageCharacter> characters)
        {
            var orderedChars = characters
                .OrderBy(c => c.NormalProjection)
                .ThenByDescending(c => c.AxisProjection)
                .ToList();

            var lines = new List<CharacterLine>();

            foreach (var character in orderedChars)
            {
                CharacterLine? bestLine = null;
                float bestDistance = float.MaxValue;

                foreach (var line in lines)
                {
                    float angleDelta = SmallestAngleDelta(line.AverageAngleDeg, character.AngleDeg);
                    if (angleDelta > 25f)
                        continue;

                    // Zvýšená tolerance pro lepší seskupování svislých znaků
                    float tolerance = Math.Max(4f, Math.Max(line.AverageWidth, character.Width) * 1.5f);
                    float distance = Math.Abs(line.AverageNormalProjection - character.NormalProjection);
                    if (distance > tolerance || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestLine = line;
                }

                if (bestLine == null)
                {
                    bestLine = new CharacterLine();
                    lines.Add(bestLine);
                }

                bestLine.AddCharacter(character);
            }

            foreach (var line in lines)
            {
                line.Characters.Sort((a, b) => b.AxisProjection.CompareTo(a.AxisProjection));
            }

            return lines
                .OrderBy(line => line.AverageNormalProjection)
                .ToList();
        }

        // Analogie BuildLineSearchText pro otočené řádky. Používá AxisProjection místo X souřadnic.
        private static string BuildVerticalLineSearchText(CharacterLine line, out List<PageCharacter?> characterMap)
        {
            characterMap = new List<PageCharacter?>();
            var chars = new List<char>();
            float avgCharSize = line.Characters.Count == 0 ? 0f : line.Characters.Average(c => Math.Max(c.Width, c.Height));
            float spacingThreshold = Math.Max(0.5f, avgCharSize * 0.3f);

            for (int i = 0; i < line.Characters.Count; i++)
            {
                var current = line.Characters[i];

                if (i > 0)
                {
                    var previous = line.Characters[i - 1];
                    float centerDistance = Math.Abs(previous.AxisProjection - current.AxisProjection);
                    float previousSpan = Math.Max(previous.Width, previous.Height);
                    float currentSpan = Math.Max(current.Width, current.Height);

                    float effectiveGap = centerDistance - (previousSpan + currentSpan) * 0.5f;
                    
                    bool shouldInsertSpace = effectiveGap > spacingThreshold
                                             && !AreSourceAdjacentCharacters(previous, current);
                    if (shouldInsertSpace)
                    {
                        chars.Add(' ');
                        characterMap.Add(null);
                    }
                }

                chars.Add(current.Value);
                characterMap.Add(char.IsWhiteSpace(current.Value) ? null : current);
            }

            return new string(chars.ToArray());
        }

        // Pokud dva znaky pochází ze stejného chunku a navazují indexem,
        // nesmíme mezi ně uměle vkládat mezeru jen podle geometrie.
        private static bool AreSourceAdjacentCharacters(PageCharacter left, PageCharacter right)
        {
            return left.SourceChunkIndex >= 0
                   && right.SourceChunkIndex >= 0
                   && left.SourceCharIndex >= 0
                   && right.SourceCharIndex >= 0
                   && left.SourceChunkIndex == right.SourceChunkIndex
                   && Math.Abs(left.SourceCharIndex - right.SourceCharIndex) == 1;
        }

        // Vrátí nejmenší absolutní rozdíl dvou úhlů (s ohledem na přechod 180/-180 stupňů).
        private static float SmallestAngleDelta(float aDeg, float bDeg)
        {
            float diff = Math.Abs(NormalizeAngleToMinus180Plus180(aDeg - bDeg));
            return Math.Min(diff, 360f - diff);
        }

        // Spustí hledání termu v daném textu a výsledné shody mapuje zpět na obdelníky přes characterMap.
        private void AddMatchesFromTextAndMap(
            List<RectangleF> results,
            string term,
            iText.Kernel.Geom.Rectangle pageRect,
            string lineText,
            List<PageCharacter?> characterMap,
            string debugScope)
        {
            var matches = FindTermMatchesInChunk(
                lineText,
                term,
                enforceWordBoundaries: true,
                debugLog: CreateMatchDetailLogger(),
                debugScope: debugScope);

            AddLineMatchesToResults(results, matches, characterMap, pageRect);
        }

        // Převede seznam nalezených shod (start/délka) + mapu znaků na PDF obdelníky a přidá je do výsledků.
        private void AddLineMatchesToResults(
            List<RectangleF> results,
            List<(int start, int length)> matches,
            List<PageCharacter?> characterMap,
            iText.Kernel.Geom.Rectangle pageRect)
        {
            foreach (var match in matches)
            {
                var matchedRects = characterMap
                    .Skip(match.start)
                    .Take(match.length)
                    .Where(c => c != null)
                    .Select(c => c!.Rectangle)
                    .ToList();

                if (matchedRects.Count == 0)
                    continue;

                var pdfRect = UnionPdfRectangles(matchedRects);
                if (!IsReasonablePdfRect(pdfRect, pageRect))
                    continue;

                var screenRect = ExpandAndClampScreenRect(ConvertPdfCoordsToScreen(pdfRect, pageRect), HighlightPaddingPx);
                AddUniqueScreenRect(results, screenRect);
            }
        }

        // Druhá fáze hledání pro případy, kdy PDF rozdělí text do více chunků.
        // Typicky pomáhá pro reference jako R114, U2 nebo podobné tokeny.
        private List<RectangleF> FindCrossChunkMatches(
            List<TextChunk> chunks,
            string term,
            iText.Kernel.Geom.Rectangle pageRect,
            bool rotatedOnly)
        {
            var results = new List<RectangleF>();

            if (chunks.Count < 2)
                return results;

            // Posouváme okno přes chunky a postupně skládáme jejich text dohromady.
            for (int i = 0; i < chunks.Count; i++)
            {
                var windowChunks = new List<TextChunk>();
                var chunkOffsets = new List<int>();
                string combinedText = string.Empty;

                for (int j = i; j < chunks.Count && j < i + CrossChunkWindowSize; j++)
                {
                    if (rotatedOnly && IsAngleNearHorizontal(chunks[j].AngleDeg))
                        continue;

                    // Okno smí růst jen přes chunky, které dávají prostorově smysl.
                    if (j > i
                        && !AreChunksNeighbors(chunks[j - 1], chunks[j])
                        && !AreChunksLikelySameToken(chunks[j - 1], chunks[j])
                        && !AreChunksLikelyContinuousText(chunks[j - 1], chunks[j]))
                        break;

                    var current = chunks[j];
                    if (string.IsNullOrEmpty(current.Text))
                        continue;

                    chunkOffsets.Add(combinedText.Length);
                    windowChunks.Add(current);
                    combinedText += current.Text;

                    if (windowChunks.Count < 2)
                        continue;

                    bool enforceBoundaries = term.All(char.IsLetterOrDigit);
                    LogVerboseSearch(
                        $"[MATCH-CHECK] term='{term}', mode=cross, windowStart={i}, windowEnd={j}, combinedText='{combinedText}', enforceBoundaries={enforceBoundaries}");

                    var matches = FindTermMatchesInChunk(
                        combinedText,
                        term,
                        enforceBoundaries,
                        debugLog: CreateMatchDetailLogger(),
                        debugScope: $"cross window={i}-{j}");
                    foreach (var match in matches)
                    {
                        var rect = BuildWindowMatchRectangle(windowChunks, chunkOffsets, match.start, match.length);
                        if (!IsReasonablePdfRect(rect, pageRect))
                            continue;

                        var screenRect = ExpandAndClampScreenRect(ConvertPdfCoordsToScreen(rect, pageRect), HighlightPaddingPx);
                        AddUniqueScreenRect(results, screenRect);
                    }
                }
            }

            return results;
        }

        // Vrátí true, pokud je úhel textu blízký horizontále (do HorizontalAngleToleranceDeg stupňů od 0°/180°).
        private static bool IsAngleNearHorizontal(float angleDeg)
        {
            float normalized = NormalizeAngleToMinus180Plus180(angleDeg);
            float distanceToHorizontal = Math.Min(Math.Abs(normalized), Math.Abs(180f - Math.Abs(normalized)));
            return distanceToHorizontal <= HorizontalAngleToleranceDeg;
        }

        // Normálizuje úhel na rozsah (-180, +180] stupňů.
        private static float NormalizeAngleToMinus180Plus180(float angleDeg)
        {
            float normalized = angleDeg % 360f;
            if (normalized > 180f)
                normalized -= 360f;
            if (normalized < -180f)
                normalized += 360f;
            return normalized;
        }

        // Vypočítá PDF obdélník jen pro část textu uvnitř jednoho chunku.
        // Pokud máme k dispozici obdélníky jednotlivých znaků, použijeme je přednostně.
        private static iText.Kernel.Geom.Rectangle GetSubstringRectangle(TextChunk chunk, int matchStart, int matchLength)
        {
            if (matchLength <= 0)
                return new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);

            int availableCharRects = chunk.CharacterRectangles.Count;
            // Nejlepší přesnost je přes geometrii jednotlivých znaků.
            bool canUseCharacterGeometry = availableCharRects > 0 && availableCharRects == chunk.Text.Length;

            if (!canUseCharacterGeometry)
            {
                // Záloha pro případy, kdy iText neposkytne geometrii všech znaků.
                return EstimateSubstringRectangle(chunk.Rectangle, chunk.Text, matchStart, matchLength);
            }

            int start = Math.Clamp(matchStart, 0, availableCharRects - 1);
            int end = Math.Clamp(matchStart + matchLength - 1, start, availableCharRects - 1);

            return UnionPdfRectangles(chunk.CharacterRectangles.Skip(start).Take(end - start + 1));
        }

        // Nouzový odhad substringu podle poměru délky textu v chunku.
        // Je méně přesný než znaková geometrie, ale stále lepší než celý chunk.
        private static iText.Kernel.Geom.Rectangle EstimateSubstringRectangle(
            iText.Kernel.Geom.Rectangle chunkRect,
            string chunkText,
            int matchStart,
            int matchLength)
        {
            int textLength = Math.Max(1, chunkText.Length);
            float startRatio = Math.Clamp(matchStart / (float)textLength, 0f, 1f);
            float endRatio = Math.Clamp((matchStart + matchLength) / (float)textLength, 0f, 1f);

            float x = (float)(chunkRect.GetX() + chunkRect.GetWidth() * startRatio);
            float width = (float)(chunkRect.GetWidth() * (endRatio - startRatio));
            width = Math.Max(width, 1f);

            return new iText.Kernel.Geom.Rectangle(
                x,
                (float)chunkRect.GetY(),
                width,
                Math.Max((float)chunkRect.GetHeight(), 1f));
        }

        // Z nalezeného matchu v kombinovaném okně dopočítá, které části spadají
        // do kterých chunků, a výsledné obdélníky pak sjednotí.
        private static iText.Kernel.Geom.Rectangle BuildWindowMatchRectangle(
            List<TextChunk> windowChunks,
            List<int> chunkOffsets,
            int matchStart,
            int matchLength)
        {
            int matchEnd = matchStart + matchLength;
            var partialRects = new List<iText.Kernel.Geom.Rectangle>();

            for (int i = 0; i < windowChunks.Count; i++)
            {
                int chunkStart = chunkOffsets[i];
                int chunkEnd = chunkStart + windowChunks[i].Text.Length;

                int overlapStart = Math.Max(matchStart, chunkStart);
                int overlapEnd = Math.Min(matchEnd, chunkEnd);
                if (overlapEnd <= overlapStart)
                    continue;

                int localStart = overlapStart - chunkStart;
                int localLength = overlapEnd - overlapStart;
                partialRects.Add(GetSubstringRectangle(windowChunks[i], localStart, localLength));
            }

            return UnionPdfRectangles(partialRects);
        }

        // Sloučí více PDF obdélníků do jednoho obalového obdélníku.
        // Používá se jak pro znaky v chunku, tak pro více chunků v jednom matchi.
        private static iText.Kernel.Geom.Rectangle UnionPdfRectangles(IEnumerable<iText.Kernel.Geom.Rectangle> rects)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool hasValue = false;

            foreach (var rect in rects)
            {
                if (rect == null || rect.GetWidth() <= 0 || rect.GetHeight() <= 0)
                    continue;

                hasValue = true;
                minX = Math.Min(minX, (float)rect.GetX());
                minY = Math.Min(minY, (float)rect.GetY());
                maxX = Math.Max(maxX, (float)(rect.GetX() + rect.GetWidth()));
                maxY = Math.Max(maxY, (float)(rect.GetY() + rect.GetHeight()));
            }

            if (!hasValue)
                return new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);

            return new iText.Kernel.Geom.Rectangle(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        }

        private static bool AreChunksNeighbors(TextChunk a, TextChunk b)
        {
            // Heuristika sousednosti: chunky musí být blízko v prostoru,
            // aby jejich spojení nedělalo velké a nesmyslné obdélníky.
            float left = Math.Max((float)a.Rectangle.GetX(), (float)b.Rectangle.GetX());
            float right = Math.Min((float)(a.Rectangle.GetX() + a.Rectangle.GetWidth()), (float)(b.Rectangle.GetX() + b.Rectangle.GetWidth()));
            float top = Math.Max((float)a.Rectangle.GetY(), (float)b.Rectangle.GetY());
            float bottom = Math.Min((float)(a.Rectangle.GetY() + a.Rectangle.GetHeight()), (float)(b.Rectangle.GetY() + b.Rectangle.GetHeight()));

            float gapX = Math.Max(0f, left - right);
            float gapY = Math.Max(0f, top - bottom);
            float distance = (float)Math.Sqrt(gapX * gapX + gapY * gapY);

            float scale = Math.Max(
                Math.Max((float)a.Rectangle.GetWidth(), (float)a.Rectangle.GetHeight()),
                Math.Max((float)b.Rectangle.GetWidth(), (float)b.Rectangle.GetHeight()));

            return distance <= Math.Max(NeighborMinDistance, scale * NeighborScaleFactor);
        }

        // Mírně volnější varianta sousednosti pro případy, kdy je jeden token
        // rozdělený mezi dva chunky (např. "C11" + "5").
        private static bool AreChunksLikelySameToken(TextChunk a, TextChunk b)
        {
            if (string.IsNullOrEmpty(a.Text) || string.IsNullOrEmpty(b.Text))
                return false;

            bool tokenLike = char.IsLetterOrDigit(a.Text[a.Text.Length - 1]) && char.IsLetterOrDigit(b.Text[0]);
            if (!tokenLike)
                return false;

            float aCenterY = (float)(a.Rectangle.GetY() + a.Rectangle.GetHeight() * 0.5);
            float bCenterY = (float)(b.Rectangle.GetY() + b.Rectangle.GetHeight() * 0.5);
            float maxHeight = Math.Max((float)a.Rectangle.GetHeight(), (float)b.Rectangle.GetHeight());
            float verticalTolerance = Math.Max(1f, maxHeight * 0.8f);

            float aRight = (float)(a.Rectangle.GetX() + a.Rectangle.GetWidth());
            float bLeft = (float)b.Rectangle.GetX();
            float horizontalGap = Math.Abs(bLeft - aRight);
            float horizontalTolerance = Math.Max(NeighborMinDistance * 2f, maxHeight * 2.5f);

            return Math.Abs(aCenterY - bCenterY) <= verticalTolerance && horizontalGap <= horizontalTolerance;
        }

        // Pro některé PDF je text jednoho tokenu rozdělený i při větší mezeře,
        // proto používáme ještě volnější kontrolu kontinuity textu.
        private static bool AreChunksLikelyContinuousText(TextChunk a, TextChunk b)
        {
            float aCenterY = (float)(a.Rectangle.GetY() + a.Rectangle.GetHeight() * 0.5);
            float bCenterY = (float)(b.Rectangle.GetY() + b.Rectangle.GetHeight() * 0.5);
            float maxHeight = Math.Max((float)a.Rectangle.GetHeight(), (float)b.Rectangle.GetHeight());
            float verticalTolerance = Math.Max(2f, maxHeight * 1.5f);

            float aRight = (float)(a.Rectangle.GetX() + a.Rectangle.GetWidth());
            float bLeft = (float)b.Rectangle.GetX();
            float horizontalGap = Math.Abs(bLeft - aRight);
            float horizontalTolerance = Math.Max(NeighborMinDistance * 8f, maxHeight * 5f);

            return Math.Abs(aCenterY - bCenterY) <= verticalTolerance && horizontalGap <= horizontalTolerance;
        }

        // Zabraňuje přidání téměř stejných obdélníků, které by vznikly tím,
        // že stejný text najdeme více cestami.
        private static void AddUniqueScreenRect(List<RectangleF> target, RectangleF candidate)
        {
            if (candidate.Width <= 0 || candidate.Height <= 0)
                return;

            foreach (var existing in target)
            {
                bool similar = Math.Abs(existing.X - candidate.X) < SimilarRectTolerancePx
                               && Math.Abs(existing.Y - candidate.Y) < SimilarRectTolerancePx
                               && Math.Abs(existing.Width - candidate.Width) < SimilarRectTolerancePx
                               && Math.Abs(existing.Height - candidate.Height) < SimilarRectTolerancePx;
                if (similar)
                    return;

                var intersection = RectangleF.Intersect(existing, candidate);
                if (intersection.Width > 0 && intersection.Height > 0)
                {
                    float intersectionArea = intersection.Width * intersection.Height;
                    float existingArea = existing.Width * existing.Height;
                    float candidateArea = candidate.Width * candidate.Height;
                    float minArea = Math.Min(existingArea, candidateArea);

                    if (minArea > 0 && intersectionArea / minArea >= 0.82f)
                        return;
                }
            }

            target.Add(candidate);
        }

        private static bool IsReasonablePdfRect(iText.Kernel.Geom.Rectangle pdfRect, iText.Kernel.Geom.Rectangle pageRect)
        {
            float pageArea = (float)(pageRect.GetWidth() * pageRect.GetHeight());
            if (pageArea <= 0)
                return false;

            float rectArea = (float)(pdfRect.GetWidth() * pdfRect.GetHeight());
            float areaRatio = rectArea / pageArea;

            // Ochrana proti náhodně obřím obdélníkům z nekorektní geometrie chunku.
            return areaRatio <= MaxReasonableHighlightAreaRatio && pdfRect.GetWidth() > 0 && pdfRect.GetHeight() > 0;
        }

        // Extrahuje chunky z iText parseru včetně jejich pozice v PDF souřadnicích.
        private List<TextChunk> GetTextChunksWithPositions(iText.Kernel.Pdf.PdfPage page)
        {
            var chunks = new List<TextChunk>();
            var strategy = new TextChunkLocationStrategy();
            var processor = new PdfCanvasProcessor(strategy);
            processor.ProcessPageContent(page);
            return strategy.GetTextChunks();
        }

        private void CreateFallbackHighlights()
        {
            // Nouzové zvýraznění pouze pro diagnostiku při chybě hlavní logiky.
            for (int i = 0; i < Math.Min(searchTerms.Count, 6); i++)
            {
                var term = searchTerms[i];
                
                // Jednoduché rozložení obdélníků, aby bylo vidět, že kód běží.
                float x = 50 + (i % 3) * 200;
                float y = 50 + (i / 3) * 100;
                float width = Math.Max(term.Length * 8, 60);
                float height = 20;
                
                highlights.Add(new RectangleF(x, y, width, height));
                highlightTerms.Add(term);
            }

            SyncSelectedHighlightIndices();
            
            picPdfViewer.Invalidate();
        }

        // ===== Data Structures - Datové struktury pro extrakci textu =====

        // Jeden textový chunk včetně celkového obdélníku a obdélníků jednotlivých znaků.
        // CharacterRectangles je klíčové pro přesné highlighty na úrovni substringu.
        private class TextChunk
        {
            public string Text { get; set; } = "";
            public iText.Kernel.Geom.Rectangle Rectangle { get; set; } = new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);
            public List<iText.Kernel.Geom.Rectangle> CharacterRectangles { get; set; } = new List<iText.Kernel.Geom.Rectangle>();
            public float AngleDeg { get; set; }
        }

        // Jeden znak ze stránky PDF s jeho geometrií, úhlem textu a odkaz na zdrojový chunk.
        // AxisProjection a NormalProjection umòžní práci se značkováním v otočeném souřadnicovém systému.
        private class PageCharacter
        {
            public char Value { get; set; }
            public iText.Kernel.Geom.Rectangle Rectangle { get; set; } = new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);
            public float AngleDeg { get; set; }
            public int SourceChunkIndex { get; set; } = -1;
            public int SourceCharIndex { get; set; } = -1;
            public float Left => (float)Rectangle.GetX();
            public float Right => (float)(Rectangle.GetX() + Rectangle.GetWidth());
            public float Width => (float)Rectangle.GetWidth();
            public float Height => (float)Rectangle.GetHeight();
            public float CenterX => (float)(Rectangle.GetX() + Rectangle.GetWidth() * 0.5f);
            public float CenterY => (float)(Rectangle.GetY() + Rectangle.GetHeight() * 0.5f);
            public float AxisProjection
            {
                get
                {
                    float angleRad = AngleDeg * (float)Math.PI / 180f;
                    float dirX = (float)Math.Cos(angleRad);
                    float dirY = (float)Math.Sin(angleRad);
                    return CenterX * dirX + CenterY * dirY;
                }
            }
            public float NormalProjection
            {
                get
                {
                    float angleRad = AngleDeg * (float)Math.PI / 180f;
                    float normalX = (float)-Math.Sin(angleRad);
                    float normalY = (float)Math.Cos(angleRad);
                    return CenterX * normalX + CenterY * normalY;
                }
            }
        }

        // Skupina znaků seskupených na jednu textovou řádku. Pověl pomůcí vlastnosti průměru
        // pro snadnou detekci příslušnosti dalšího znaku do této řádky.
        private class CharacterLine
        {
            public List<PageCharacter> Characters { get; } = new List<PageCharacter>();
            private float sumCenterY;
            private float sumAngleDeg;
            private float sumNormalProjection;
            private float sumWidth;
            private float sumHeight;

            public void AddCharacter(PageCharacter character)
            {
                Characters.Add(character);
                sumCenterY += character.CenterY;
                sumAngleDeg += character.AngleDeg;
                sumNormalProjection += character.NormalProjection;
                sumWidth += character.Width;
                sumHeight += character.Height;
            }

            public float AverageCenterY => Characters.Count == 0 ? 0f : sumCenterY / Characters.Count;
            public float AverageAngleDeg => Characters.Count == 0 ? 0f : sumAngleDeg / Characters.Count;
            public float AverageNormalProjection => Characters.Count == 0 ? 0f : sumNormalProjection / Characters.Count;
            public float AverageWidth => Characters.Count == 0 ? 0f : sumWidth / Characters.Count;
            public float AverageHeight => Characters.Count == 0 ? 0f : sumHeight / Characters.Count;
        }
        
        // Listener iText událostí: sbírá text a jeho geometrii během průchodu stránky.
        // Tento objekt je napojený na PdfCanvasProcessor a dostává callback pro každý renderovaný text.
        private class TextChunkLocationStrategy : IEventListener
        {
            private readonly List<TextChunk> textChunks = new List<TextChunk>();
            
            public void EventOccurred(IEventData data, EventType type)
            {
                if (type == EventType.RENDER_TEXT && data is TextRenderInfo textInfo)
                {
                    var text = textInfo.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var charInfos = textInfo.GetCharacterRenderInfos();
                        var charRects = new List<iText.Kernel.Geom.Rectangle>();

                        // Pro každý znak si uložíme vlastní obdélník. Díky tomu lze později
                        // zvýraznit jen konkrétní podřetězec místo celého textového chunku.
                        foreach (var charInfo in charInfos)
                        {
                            charRects.Add(GetRectFromTextRenderInfo(charInfo));
                        }

                        // Když znakové obdélníky nejsou dostupné, spadneme aspoň na obdélník
                        // celého chunku, aby hledání bylo stále použitelné.
                        var rect = charRects.Count > 0
                            ? UnionPdfRectangles(charRects)
                            : GetRectFromTextRenderInfo(textInfo);

                        textChunks.Add(new TextChunk
                        {
                            Text = text,
                            Rectangle = rect,
                            CharacterRectangles = charRects,
                            AngleDeg = GetTextAngleDegrees(textInfo)
                        });
                        
                        // DEBUG: Log every text chunk (limit output for performance)
                        if (textChunks.Count <= 50) // Only log first 50 chunks
                        {
                            LogVerboseSearch($"[CHUNK] '{text}' -> ({rect.GetX():F1}, {rect.GetY():F1})");
                        }
                    }
                }
            }
            
            public ICollection<EventType> GetSupportedEvents()
            {
                return new HashSet<EventType> { EventType.RENDER_TEXT };
            }
            
            public List<TextChunk> GetTextChunks()
            {
                return textChunks;
            }

            // Převod textového renderovacího info objektu na obdélník v PDF souřadnicích.
            // iText dává k dispozici ascent/descent linky, ze kterých vezmeme obalový box.
            private static iText.Kernel.Geom.Rectangle GetRectFromTextRenderInfo(TextRenderInfo textInfo)
            {
                var ascent = textInfo.GetAscentLine();
                var descent = textInfo.GetDescentLine();

                float x1 = (float)ascent.GetStartPoint().Get(0);
                float y1 = (float)ascent.GetStartPoint().Get(1);
                float x2 = (float)ascent.GetEndPoint().Get(0);
                float y2 = (float)ascent.GetEndPoint().Get(1);
                float x3 = (float)descent.GetStartPoint().Get(0);
                float y3 = (float)descent.GetStartPoint().Get(1);
                float x4 = (float)descent.GetEndPoint().Get(0);
                float y4 = (float)descent.GetEndPoint().Get(1);

                float minX = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4));
                float maxX = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4));
                float minY = Math.Min(Math.Min(y1, y2), Math.Min(y3, y4));
                float maxY = Math.Max(Math.Max(y1, y2), Math.Max(y3, y4));

                return new iText.Kernel.Geom.Rectangle(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
            }

            private static float GetTextAngleDegrees(TextRenderInfo textInfo)
            {
                var baseline = textInfo.GetBaseline();
                float x1 = (float)baseline.GetStartPoint().Get(0);
                float y1 = (float)baseline.GetStartPoint().Get(1);
                float x2 = (float)baseline.GetEndPoint().Get(0);
                float y2 = (float)baseline.GetEndPoint().Get(1);

                float dx = x2 - x1;
                float dy = y2 - y1;
                if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                    return 0f;

                return (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            }
        }
    }
}
