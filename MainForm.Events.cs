using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace PdfHighlighter
{
    // ===== Event Handlers & Keyboard Shortcuts - Obsluha UI zdarosti a klávesových zkratek =====
    public partial class MainForm : Form
    {
        // Vykreslí všechny highlight obdélníky přes aktuálně zobrazenou stránku PDF.
        private void PicPdfViewer_Paint(object? sender, PaintEventArgs e)
        {
            if (highlights.Count == 0)
                return;

            // Samotné vykreslení highlightů nad renderovanou stránkou.
            using var highlightBrush = new SolidBrush(Color.FromArgb(50, Color.Red));
            using var highlightPen = new Pen(Color.Red, 2);
            using var selectedHighlightBrush = new SolidBrush(Color.FromArgb(95, Color.LightGreen));
            using var selectedHighlightPen = new Pen(Color.ForestGreen, 2);

            for (int i = 0; i < highlights.Count; i++)
            {
                var highlight = highlights[i];
                bool isSelected = selectedHighlightIndices.Contains(i);
                var fillBrush = isSelected ? selectedHighlightBrush : highlightBrush;
                var borderPen = isSelected ? selectedHighlightPen : highlightPen;

                e.Graphics.FillRectangle(fillBrush, highlight);
                e.Graphics.DrawRectangle(borderPen, System.Drawing.Rectangle.Round(highlight));
            }
        }

        // Levý klik uvnitř zvýraznění přepíná jeho stav (původní barva <-> světle zelená).
        private void PicPdfViewer_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || highlights.Count == 0)
                return;

            int clickedHighlightIndex = -1;
            for (int i = highlights.Count - 1; i >= 0; i--)
            {
                if (highlights[i].Contains(e.Location))
                {
                    clickedHighlightIndex = i;
                    break;
                }
            }

            if (clickedHighlightIndex < 0)
                return;

            if (selectedHighlightIndices.Contains(clickedHighlightIndex))
            {
                selectedHighlightIndices.Remove(clickedHighlightIndex);
            }
            else
            {
                selectedHighlightIndices.Add(clickedHighlightIndex);
            }

            picPdfViewer.Invalidate();
        }

        // Reakce na tlačítko pro hledání: načte zadané termy, přepočítá highlighty a aktualizuje stavový text.
        private void BtnHighlight_Click(object? sender, EventArgs e)
        {
            if (!IsButtonActive(btnHighlight))
                return;

            ParseSearchTerms();
            UpdateHighlights();
        }

        // Rozparsuje vstup z textboxu na jednotlivé hledané výrazy oddělené čárkou.
        private void ParseSearchTerms()
        {
            // Uživatel zadává výrazy oddělené čárkou.
            searchTerms.Clear();
            
            if (string.IsNullOrWhiteSpace(txtSearchTerms.Text))
                return;

            var terms = txtSearchTerms.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                var trimmed = term.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    searchTerms.Add(trimmed);
                }
            }
        }

        // Smaže všechna aktuální zvýraznění z obrazu i ze stavové informace.
        private void ClearHighlights()
        {
            highlights.Clear();
            selectedHighlightIndices.Clear();
            picPdfViewer.Invalidate();
            lblStatus.Text = "Zvýraznění vymazáno.";
        }

        // Otevře diagnostické okno se základními informacemi o PDF, stránce, zoomu a výsledcích hledání.
        private void BtnDebug_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null)
            {
                MessageBox.Show("Nejdříve otevřete PDF soubor!", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine($"=== DEBUG INFORMACE ===");
            debugInfo.AppendLine($"PDF soubor: {currentPdfPath ?? "Žádný"}");
            debugInfo.AppendLine($"Aktuální stránka: {currentPageIndex + 1} / {pdfDocument.GetNumberOfPages()}");
            debugInfo.AppendLine($"Zoom faktor: {zoomFactor:F2}");
            debugInfo.AppendLine($"Počet hledaných výrazů: {searchTerms.Count}");
            debugInfo.AppendLine($"Hledané výrazy: {string.Join(", ", searchTerms)}");
            debugInfo.AppendLine($"Počet zvýraznění: {highlights.Count}");
            
            if (pdfViewerDocument != null)
            {
                var pageSize = pdfViewerDocument.PageSizes[currentPageIndex];
                debugInfo.AppendLine($"Velikost stránky (PdfiumViewer): {pageSize.Width:F1} x {pageSize.Height:F1}");
            }
            
            var page = pdfDocument.GetPage(currentPageIndex + 1);
            var pdfPageSize = page.GetPageSize();
            debugInfo.AppendLine($"Velikost stránky (iText7): {pdfPageSize.GetWidth():F1} x {pdfPageSize.GetHeight():F1}");

            // Pro rychlou diagnostiku ukážeme i začátek extrahovaného textu stránky.
            var strategy = new SimpleTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
            debugInfo.AppendLine($"Délka extrahovaného textu: {pageText.Length} znaků");
            debugInfo.AppendLine($"Začátek textu: {pageText.Substring(0, Math.Min(100, pageText.Length)).Replace("\n", "\\n")}");
            
            // Show debug info
            MessageBox.Show(debugInfo.ToString(), "Debug informace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Also write to debug output
            System.Diagnostics.Debug.WriteLine(debugInfo.ToString());
        }

        // Přepne zobrazení na předchozí stránku a znovu vykreslí PDF i stav navigačních tlačítek.
        private void BtnPrevPage_Click(object? sender, EventArgs e)
        {
            if (!IsButtonActive(btnPrevPage))
                return;

            if (currentPageIndex > 0)
            {
                currentPageIndex--;
                UpdatePageInfo();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        // Přepne zobrazení na následující stránku a znovu vykreslí PDF i stav navigačních tlačítek.
        private void BtnNextPage_Click(object? sender, EventArgs e)
        {
            if (!IsButtonActive(btnNextPage))
                return;

            if (pdfDocument != null && currentPageIndex < pdfDocument.GetNumberOfPages() - 1)
            {
                currentPageIndex++;
                UpdatePageInfo();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        // Nastaví, zda mají být tlačítka Předchozí a Následující vizuálně aktivní podle aktuální stránky.
        private void UpdateNavigationButtons()
        {
            if (pdfDocument == null)
            {
                SetButtonActiveState(btnPrevPage, false);
                SetButtonActiveState(btnNextPage, false);
                return;
            }

            SetButtonActiveState(btnPrevPage, currentPageIndex > 0);
            SetButtonActiveState(btnNextPage, currentPageIndex < pdfDocument.GetNumberOfPages() - 1);
        }

        // Po změně zoom slideru přepočítá faktor přiblížení, znovu vyrenderuje stránku a aktualizuje status.
        private void TrackZoom_ValueChanged(object? sender, EventArgs e)
        {
            zoomFactor = trackZoom.Value / 100.0f;
            RenderCurrentPage();
            lblStatus.Text = $"Zoom: {trackZoom.Value}%";
        }

        // Umožní spustit hledání klávesou Enter přímo z pole pro zadání hledaného textu.
        private void TxtSearchTerms_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && IsButtonActive(btnHighlight))
            {
                BtnHighlight_Click(sender, e);
                e.SuppressKeyPress = true;
            }
        }

        // Při zavření formuláře korektně uvolní otevřené PDF dokumenty i bitmapu z vieweru.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up resources
            pdfDocument?.Close();
            pdfViewerDocument?.Dispose();
            picPdfViewer.Image?.Dispose();
            
            base.OnFormClosing(e);
        }

        // Zpracuje globální klávesové zkratky pro navigaci mezi stránkami, zoom a fokus na vyhledávání.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Globální klávesové zkratky pro rychlejší práci.
            switch (keyData)
            {
                case Keys.Left:
                case Keys.PageUp:
                    if (IsButtonActive(btnPrevPage))
                    {
                        BtnPrevPage_Click(this, EventArgs.Empty);
                        return true;
                    }
                    break;

                case Keys.Right:
                case Keys.PageDown:
                    if (IsButtonActive(btnNextPage))
                    {
                        BtnNextPage_Click(this, EventArgs.Empty);
                        return true;
                    }
                    break;

                case Keys.Add:
                case Keys.Oemplus:
                    if (trackZoom.Enabled && trackZoom.Value < trackZoom.Maximum)
                    {
                        trackZoom.Value = Math.Min(trackZoom.Maximum, trackZoom.Value + 25);
                        return true;
                    }
                    break;

                case Keys.Subtract:
                case Keys.OemMinus:
                    if (trackZoom.Enabled && trackZoom.Value > trackZoom.Minimum)
                    {
                        trackZoom.Value = Math.Max(trackZoom.Minimum, trackZoom.Value - 25);
                        return true;
                    }
                    break;

                case Keys.F | Keys.Control:
                    txtSearchTerms.Focus();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
