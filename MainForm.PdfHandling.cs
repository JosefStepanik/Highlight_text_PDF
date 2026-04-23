// =============================================================
// File: MainForm.PdfHandling.cs
// Purpose: Load, render, and navigate PDF documents.
// Contains: PDF file selection/loading, page rendering, zoom/page updates, and navigation state sync.
// Author: Josef Stepanik
// Created: 2026-04
// =============================================================

using System;
using System.Windows.Forms;
using iText.Kernel.Pdf;

namespace PdfHighlighter
{
    // ===== PDF Handling - Načtení a vykreslení PDF =====
    public partial class MainForm : Form
    {
        // Otevře dialog pro výběr PDF souboru a předá cestu další metodě LoadPdfFile.
        private void BtnSelectPdf_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF soubory (*.pdf)|*.pdf|Všechny soubory (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                Title = "Vyberte PDF soubor"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                LoadPdfFile(openFileDialog.FileName);
            }
        }

        // Načte PDF soubor dvěma způsoby: iText7 pro extrakci textu, PdfiumViewer pro renderování.
        // Před načtením vynuluje veškerý stav výběru a série hledání z předchozího PDF.
        private void LoadPdfFile(string filePath)
        {
            try
            {
                selectedHighlightIndices.Clear();
                selectedHighlightTerms.Clear();
                foundTermsByPageSummary.Clear();
                totalSearchTermsInSummary = 0;
                missingTermsInSummary.Clear();
                multipleOccurrenceTermsInSummary.Clear();
                hasSearchSummary = false;
                searchStatusText = string.Empty;
                searchErrorText = string.Empty;

                // Close previous documents
                pdfDocument?.Close();
                pdfViewerDocument?.Dispose();

                // Load for text extraction (iText)
                pdfDocument = new iText.Kernel.Pdf.PdfDocument(new PdfReader(filePath));
                
                // Load for rendering (PdfiumViewer)
                pdfViewerDocument = PdfiumViewer.PdfDocument.Load(filePath);
                
                currentPdfPath = filePath;
                currentPageIndex = 0;

                if (pdfDocument.GetNumberOfPages() > 0)
                {
                    SetButtonActiveState(btnHighlight, true);
                    SetButtonActiveState(btnPrevPage, false);
                    SetButtonActiveState(btnNextPage, pdfDocument.GetNumberOfPages() > 1);
                    trackZoom.Enabled = true;

                    UpdatePageInfo();
                    RenderCurrentPage();

                    SetStatusMessage($"Načten PDF: {System.IO.Path.GetFileName(filePath)} ({pdfDocument.GetNumberOfPages()} stránek)");
                }
                else
                {
                    MessageBox.Show("PDF soubor neobsahuje žádné stránky.", "Chyba", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při načítání PDF souboru:\n{ex.Message}", 
                              "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatusMessage("Chyba při načítání PDF souboru.", true);
            }
        }

        // Vyrenderuje aktuální stránku do bitmapy přes PdfiumViewer se zohledněním aktuálního zoomu,
        // uloží ji do picPdfViewer a přepočítá highlighty i středové vyřazení obrazu.
        private void RenderCurrentPage()
        {
            if (pdfViewerDocument == null || currentPageIndex >= pdfViewerDocument.PageCount)
                return;

            try
            {
                // Velikost renderu počítáme v pixelech podle zoomu.
                var pageSize = pdfViewerDocument.PageSizes[currentPageIndex];
                int renderWidth = (int)(pageSize.Width * zoomFactor * PointsToPixels);
                int renderHeight = (int)(pageSize.Height * zoomFactor * PointsToPixels);

                // Render page
                using var image = pdfViewerDocument.Render(currentPageIndex, renderWidth, renderHeight, 96, 96, false);
                
                // Set rendered image
                picPdfViewer.Image?.Dispose();
                picPdfViewer.Image = new System.Drawing.Bitmap(image);
                picPdfViewer.Visible = true;
                picPdfViewer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

                // Update highlights
                UpdateHighlights();

                // Center image
                CenterImage();
            }
            catch (Exception ex)
            {
                SetStatusMessage($"Chyba při renderování stránky: {ex.Message}", true);
            }
        }

        // Aktualizuje popisek s číslem stránky (např. "Stránka 3 z 10").
        private void UpdatePageInfo()
        {
            if (pdfDocument == null)
            {
                lblPageInfo.Text = "Žádný PDF";
            }
            else
            {
                lblPageInfo.Text = $"Stránka {currentPageIndex + 1} z {pdfDocument.GetNumberOfPages()}";
            }
        }

        private void CenterImage()
        {
            if (picPdfViewer.Image == null || scrollContainer == null)
                return;

            // Zarovnání renderované stránky doprostřed viewportu.
            int x = Math.Max(0, (scrollContainer.ClientSize.Width - picPdfViewer.Width) / 2);
            int y = Math.Max(0, (scrollContainer.ClientSize.Height - picPdfViewer.Height) / 2);
            
            picPdfViewer.Location = new System.Drawing.Point(x, y);
        }
    }
}
