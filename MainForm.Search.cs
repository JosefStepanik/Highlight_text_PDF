using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace PdfHighlighter
{
    // ===== Text Search & Highlighting - Hledání textu a tvorba zvýraznění =====
    public partial class MainForm : Form
    {
        // Hlavní vstup pro přepočet highlightů na aktuálně zobrazené stránce.
        // Volá se po načtení PDF, změně zoomu i po zadání hledaného textu.
        private void UpdateHighlights()
        {
            highlights.Clear();
            selectedHighlightIndices.Clear();

            if (pdfViewerDocument == null || searchTerms.Count == 0 || currentPageIndex >= pdfViewerDocument.PageCount)
                return;

            try
            {
                int totalFoundOccurrences = 0;
                int foundTermsCount = 0;
                var missingTerms = new List<string>();

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
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Hledám text: '{trimmedTerm}' na stránce {currentPageIndex + 1}");
                    
                    // Pro každý výraz najdeme přesné pozice a převedeme je na obrazovku.
                    var realPositions = FindRealTextPositions(page, trimmedTerm, pdfPageSize);

                    if (realPositions.Count > 0)
                    {
                        foundTermsCount++;
                    }
                    else
                    {
                        missingTerms.Add(trimmedTerm);
                    }
                    totalFoundOccurrences += realPositions.Count;
                    
                    // DEBUG: Log results
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Nalezeno {realPositions.Count} pozic pro '{trimmedTerm}'");
                    
                    foreach (var rect in realPositions)
                    {
                        highlights.Add(rect);
                        // DEBUG: Log rectangle coordinates
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Přidán obdélník: X={rect.X:F1}, Y={rect.Y:F1}, W={rect.Width:F1}, H={rect.Height:F1}");
                    }
                }
                
                picPdfViewer.Invalidate(); // Vynutí překreslení nových highlightů.

                lblStatus.Text = BuildSearchStatusText(totalFoundOccurrences, foundTermsCount, searchTerms.Count, missingTerms);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Chyba při hledání textu: {ex.Message}";
                // Nouzová varianta: ukáže aspoň orientační obdélníky.
                CreateFallbackHighlights();
            }
        }

        private string BuildSearchStatusText(int foundOccurrences, int foundTerms, int totalTerms, List<string> missingTerms)
        {
            string occurrencesPart = foundOccurrences == 1
                ? "Nalezen 1 výskyt"
                : $"Nalezeno {foundOccurrences} výskytů";

            string termsPart = $"nalezené výrazy: {foundTerms}/{totalTerms}";

            if (missingTerms.Count == 0)
                return $"{occurrencesPart} na této stránce ({termsPart}).";

            return $"{occurrencesPart} na této stránce ({termsPart}). Nenalezeno: {string.Join(", ", missingTerms)}.";
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
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Extrahovano {textChunks.Count} text chunků z PDF");
                foreach (var chunk in textChunks.Take(10)) // Show first 10 for debugging
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Chunk: '{chunk.Text}' na pozici ({chunk.Rectangle.GetX():F1}, {chunk.Rectangle.GetY():F1})");
                }
                
                bool enforceWordBoundaries = trimmedTerm.All(char.IsLetterOrDigit);
                if (enforceWordBoundaries)
                {
                    var characterFlowRects = FindMatchesByCharacterFlow(textChunks, trimmedTerm, pdfPageSize);
                    foreach (var screenRect in characterFlowRects)
                    {
                        AddUniqueScreenRect(results, screenRect);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[CHAR-FLOW] term='{trimmedTerm}', matches={characterFlowRects.Count}, chunkFallbackUsed={false}");

                    return results;
                }

                // Primární hledání: najít výraz uvnitř jednotlivých chunků.
                for (int chunkIndex = 0; chunkIndex < textChunks.Count; chunkIndex++)
                {
                    var chunk = textChunks[chunkIndex];
                    char? leftNeighborChar = GetAdjacentChunkBoundaryChar(textChunks, chunkIndex, searchLeft: true);
                    char? rightNeighborChar = GetAdjacentChunkBoundaryChar(textChunks, chunkIndex, searchLeft: false);

                    System.Diagnostics.Debug.WriteLine(
                        $"[MATCH-CHECK] term='{trimmedTerm}', mode=single, chunkIndex={chunkIndex}, chunkText='{chunk.Text}', leftNeighbor='{FormatDebugChar(leftNeighborChar)}', rightNeighbor='{FormatDebugChar(rightNeighborChar)}', enforceBoundaries={enforceWordBoundaries}");

                    var matches = FindTermMatchesInChunk(
                        chunk.Text,
                        trimmedTerm,
                        enforceWordBoundaries,
                        leftNeighborChar,
                        rightNeighborChar,
                        message => System.Diagnostics.Debug.WriteLine($"[MATCH-DETAIL] {message}"),
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

                        System.Diagnostics.Debug.WriteLine($"[DEBUG] NALEZEN! '{searchTerm}' v chunku '{chunk.Text}'");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PDF pozice: ({pdfRect.GetX():F1}, {pdfRect.GetY():F1}, {pdfRect.GetWidth():F1}, {pdfRect.GetHeight():F1})");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Obrazovka pozice: ({screenRect.X:F1}, {screenRect.Y:F1}, {screenRect.Width:F1}, {screenRect.Height:F1})");
                    }
                }

                // Sekundární hledání: token může být rozdělen přes více sousedních chunků.
                var crossChunkRects = FindCrossChunkMatches(textChunks, trimmedTerm, pdfPageSize);
                foreach (var screenRect in crossChunkRects)
                {
                    AddUniqueScreenRect(results, screenRect);
                }
                
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Chyba při hledání '{searchTerm}': {ex.Message}";
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

                bool leftBoundaryOk = IsBoundaryChar(text, found - 1);
                if (found == 0)
                    leftBoundaryOk = IsBoundaryChar(leftNeighborChar);

                bool rightBoundaryOk = IsBoundaryChar(text, found + term.Length);
                if (found + term.Length >= text.Length)
                    rightBoundaryOk = IsBoundaryChar(rightNeighborChar);

                bool boundaryOk = !enforceWordBoundaries || (leftBoundaryOk && rightBoundaryOk);

                debugLog?.Invoke(
                    $"scope='{debugScope}', term='{term}', text='{text}', foundAt={found}, leftBoundaryOk={leftBoundaryOk}, rightBoundaryOk={rightBoundaryOk}, accepted={boundaryOk}");

                if (boundaryOk)
                    matches.Add((found, term.Length));

                startIndex = found + Math.Max(1, term.Length);
            }

            return matches;
        }

        private static bool IsBoundaryChar(string text, int index)
        {
            if (index < 0 || index >= text.Length)
                return true;

            return !char.IsLetterOrDigit(text[index]);
        }

        private static bool IsBoundaryChar(char? c)
        {
            return !c.HasValue || !char.IsLetterOrDigit(c.Value);
        }

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

        private List<RectangleF> FindMatchesByCharacterFlow(
            List<TextChunk> chunks,
            string term,
            iText.Kernel.Geom.Rectangle pageRect)
        {
            var results = new List<RectangleF>();
            var pageCharacters = BuildPageCharacters(chunks);

            if (pageCharacters.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAR-FLOW] term='{term}', no usable characters extracted");
                return results;
            }

            var lines = BuildCharacterLines(pageCharacters);
            System.Diagnostics.Debug.WriteLine($"[CHAR-FLOW] term='{term}', characters={pageCharacters.Count}, lines={lines.Count}");

            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                string lineText = BuildLineSearchText(line, out var characterMap);

                if (string.IsNullOrEmpty(lineText))
                    continue;

                System.Diagnostics.Debug.WriteLine($"[CHAR-LINE] term='{term}', lineIndex={lineIndex}, text='{lineText}'");

                var matches = FindTermMatchesInChunk(
                    lineText,
                    term,
                    enforceWordBoundaries: true,
                    debugLog: message => System.Diagnostics.Debug.WriteLine($"[MATCH-DETAIL] {message}"),
                    debugScope: $"char-flow lineIndex={lineIndex}");

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

            return results;
        }

        private static List<PageCharacter> BuildPageCharacters(List<TextChunk> chunks)
        {
            var characters = new List<PageCharacter>();

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrEmpty(chunk.Text))
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
                        Rectangle = rect
                    });
                }
            }

            return characters;
        }

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
                    float tolerance = Math.Max(2f, Math.Max(line.AverageHeight, character.Height) * 0.7f);
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

                bestLine.Characters.Add(character);
            }

            foreach (var line in lines)
            {
                line.Characters.Sort((a, b) => a.Left.CompareTo(b.Left));
            }

            return lines
                .OrderByDescending(line => line.AverageCenterY)
                .ToList();
        }

        private static string BuildLineSearchText(CharacterLine line, out List<PageCharacter?> characterMap)
        {
            characterMap = new List<PageCharacter?>();
            var chars = new List<char>();

            for (int i = 0; i < line.Characters.Count; i++)
            {
                var current = line.Characters[i];

                if (i > 0)
                {
                    var previous = line.Characters[i - 1];
                    float gap = current.Left - previous.Right;
                    float spacingThreshold = Math.Max(1.5f, Math.Max(previous.Width, current.Width) * 0.8f);
                    if (gap > spacingThreshold)
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

        // Druhá fáze hledání pro případy, kdy PDF rozdělí text do více chunků.
        // Typicky pomáhá pro reference jako R114, U2 nebo podobné tokeny.
        private List<RectangleF> FindCrossChunkMatches(
            List<TextChunk> chunks,
            string term,
            iText.Kernel.Geom.Rectangle pageRect)
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
                    System.Diagnostics.Debug.WriteLine(
                        $"[MATCH-CHECK] term='{term}', mode=cross, windowStart={i}, windowEnd={j}, combinedText='{combinedText}', enforceBoundaries={enforceBoundaries}");

                    var matches = FindTermMatchesInChunk(
                        combinedText,
                        term,
                        enforceBoundaries,
                        debugLog: message => System.Diagnostics.Debug.WriteLine($"[MATCH-DETAIL] {message}"),
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
            }
            
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
        }

        private class PageCharacter
        {
            public char Value { get; set; }
            public iText.Kernel.Geom.Rectangle Rectangle { get; set; } = new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);
            public float Left => (float)Rectangle.GetX();
            public float Right => (float)(Rectangle.GetX() + Rectangle.GetWidth());
            public float Height => (float)Rectangle.GetHeight();
            public float CenterY => (float)(Rectangle.GetY() + Rectangle.GetHeight() * 0.5f);
        }

        private class CharacterLine
        {
            public List<PageCharacter> Characters { get; } = new List<PageCharacter>();
            public float AverageCenterY => Characters.Count == 0 ? 0f : Characters.Average(c => c.CenterY);
            public float AverageHeight => Characters.Count == 0 ? 0f : Characters.Average(c => c.Height);
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
                            CharacterRectangles = charRects
                        });
                        
                        // DEBUG: Log every text chunk (limit output for performance)
                        if (textChunks.Count <= 50) // Only log first 50 chunks
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHUNK] '{text}' -> ({rect.GetX():F1}, {rect.GetY():F1})");
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
        }
    }
}
