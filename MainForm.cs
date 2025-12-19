using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PdfiumViewer;

namespace PdfHighlighter
{
    public partial class MainForm : Form
    {
        private Button btnSelectPdf = null!;
        private TextBox txtSearchTerms = null!;
        private Button btnHighlight = null!;

        private Label lblStatus = null!;
        private Button btnPrevPage = null!;
        private Button btnNextPage = null!;
        private Label lblPageInfo = null!;
        private TrackBar trackZoom = null!;
        private PictureBox picPdfViewer = null!;
        private ScrollableControl scrollContainer = null!;

        // PDF related fields
        private iText.Kernel.Pdf.PdfDocument? pdfDocument;
        private PdfiumViewer.PdfDocument? pdfViewerDocument;
        private int currentPageIndex = 0;
        private float zoomFactor = 1.0f;
        private List<string> searchTerms = new List<string>();
        private string? currentPdfPath;
        private List<RectangleF> highlights = new List<RectangleF>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "PDF Text Highlighter - Vizuální viewer";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(900, 600);

            // Main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Toolbar
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // PDF viewer
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status bar

            // Toolbar
            var toolbar = CreateToolbar();
            mainPanel.Controls.Add(toolbar, 0, 0);

            // PDF viewer area
            var viewerPanel = CreatePdfViewer();
            mainPanel.Controls.Add(viewerPanel, 0, 1);

            // Status bar
            var statusBar = CreateStatusBar();
            mainPanel.Controls.Add(statusBar, 0, 2);

            this.Controls.Add(mainPanel);
            this.ResumeLayout(false);
        }

        private Panel CreateToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 2
            };

            // First row - File selection and navigation
            var lblFile = new Label
            {
                Text = "PDF soubor:",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 5, 10, 5)
            };
            layout.Controls.Add(lblFile, 0, 0);

            btnSelectPdf = new Button
            {
                Text = "Vybrat PDF soubor...",
                AutoSize = true,
                Margin = new Padding(0, 0, 10, 5)
            };
            btnSelectPdf.Click += BtnSelectPdf_Click;
            layout.Controls.Add(btnSelectPdf, 1, 0);

            btnPrevPage = new Button
            {
                Text = "◀ Předchozí",
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(0, 0, 5, 5)
            };
            btnPrevPage.Click += BtnPrevPage_Click;
            layout.Controls.Add(btnPrevPage, 2, 0);

            btnNextPage = new Button
            {
                Text = "Následující ▶",
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(0, 0, 10, 5)
            };
            btnNextPage.Click += BtnNextPage_Click;
            layout.Controls.Add(btnNextPage, 3, 0);

            lblPageInfo = new Label
            {
                Text = "Žádný PDF",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 5, 10, 5)
            };
            layout.Controls.Add(lblPageInfo, 4, 0);

            // Zoom controls
            var lblZoom = new Label
            {
                Text = "Zoom:",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 5, 5, 5)
            };
            layout.Controls.Add(lblZoom, 5, 0);

            trackZoom = new TrackBar
            {
                Minimum = 25,
                Maximum = 300,
                Value = 100,
                TickFrequency = 25,
                Width = 150,
                Enabled = false
            };
            trackZoom.ValueChanged += TrackZoom_ValueChanged;
            layout.Controls.Add(trackZoom, 6, 0);

            // Second row - Search
            var lblSearch = new Label
            {
                Text = "Hledané výrazy (oddělené čárkou):",
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 5, 10, 0)
            };
            layout.Controls.Add(lblSearch, 0, 1);

            txtSearchTerms = new TextBox
            {
                PlaceholderText = "například: slovo1, dlouhá fráze, jiný text",
                Width = 300,
                Margin = new Padding(0, 0, 10, 0)
            };
            txtSearchTerms.KeyDown += TxtSearchTerms_KeyDown;
            layout.Controls.Add(txtSearchTerms, 1, 1);
            layout.SetColumnSpan(txtSearchTerms, 2);

            btnHighlight = new Button
            {
                Text = "🔍 Zvýraznit text",
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnHighlight.Click += BtnHighlight_Click;
            layout.Controls.Add(btnHighlight, 3, 1);

            var btnClear = new Button
            {
                Text = "Vymazat",
                AutoSize = true,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnClear.Click += (s, e) => { ClearHighlights(); };
            layout.Controls.Add(btnClear, 4, 1);
            
            var btnDebug = new Button
            {
                Text = "🔧 Debug",
                AutoSize = true,
                BackColor = Color.Orange,
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnDebug.Click += BtnDebug_Click;
            layout.Controls.Add(btnDebug, 5, 1);

            toolbar.Controls.Add(layout);
            return toolbar;
        }

        private Panel CreatePdfViewer()
        {
            var viewerContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Gray,
                AutoScroll = true
            };

            scrollContainer = new ScrollableControl
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Gray
            };

            picPdfViewer = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            picPdfViewer.Paint += PicPdfViewer_Paint;

            scrollContainer.Controls.Add(picPdfViewer);
            viewerContainer.Controls.Add(scrollContainer);

            return viewerContainer;
        }

        private Panel CreateStatusBar()
        {
            var statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control
            };

            lblStatus = new Label
            {
                Text = "Připraven. Vyberte PDF soubor pro začátek.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            statusPanel.Controls.Add(lblStatus);
            return statusPanel;
        }

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

        private void LoadPdfFile(string filePath)
        {
            try
            {
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
                    btnHighlight.Enabled = true;
                    btnPrevPage.Enabled = false;
                    btnNextPage.Enabled = pdfDocument.GetNumberOfPages() > 1;
                    trackZoom.Enabled = true;

                    UpdatePageInfo();
                    RenderCurrentPage();

                    lblStatus.Text = $"Načten PDF: {System.IO.Path.GetFileName(filePath)} ({pdfDocument.GetNumberOfPages()} stránek)";
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
                lblStatus.Text = "Chyba při načítání PDF souboru.";
            }
        }

        private void RenderCurrentPage()
        {
            if (pdfViewerDocument == null || currentPageIndex >= pdfViewerDocument.PageCount)
                return;

            try
            {
                // Calculate size based on zoom
                var pageSize = pdfViewerDocument.PageSizes[currentPageIndex];
                int renderWidth = (int)(pageSize.Width * zoomFactor * 96 / 72); // Convert points to pixels
                int renderHeight = (int)(pageSize.Height * zoomFactor * 96 / 72);

                // Render page
                using var image = pdfViewerDocument.Render(currentPageIndex, renderWidth, renderHeight, 96, 96, false);
                
                // Set rendered image
                picPdfViewer.Image?.Dispose();
                picPdfViewer.Image = new Bitmap(image);

                // Update highlights
                UpdateHighlights();

                // Center image
                CenterImage();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Chyba při renderování stránky: {ex.Message}";
            }
        }

        private void UpdateHighlights()
        {
            highlights.Clear();

            if (pdfViewerDocument == null || searchTerms.Count == 0 || currentPageIndex >= pdfViewerDocument.PageCount)
                return;

            try
            {
                var pageSize = pdfViewerDocument.PageSizes[currentPageIndex];
                
                // Get text and positions from current page using iText7
                var page = pdfDocument!.GetPage(currentPageIndex + 1);
                var pdfPageSize = page.GetPageSize();
                
                foreach (var term in searchTerms)
                {
                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    var trimmedTerm = term.Trim();
                    
                    // DEBUG: Log what we're searching for
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Hledám text: '{trimmedTerm}' na stránce {currentPageIndex + 1}");
                    
                    // Find REAL positions of text using LocationTextExtractionStrategy
                    var realPositions = FindRealTextPositions(page, trimmedTerm, pdfPageSize, pageSize);
                    
                    // DEBUG: Log results
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Nalezeno {realPositions.Count} pozic pro '{trimmedTerm}'");
                    
                    foreach (var rect in realPositions)
                    {
                        highlights.Add(rect);
                        // DEBUG: Log rectangle coordinates
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Přidán obdélník: X={rect.X:F1}, Y={rect.Y:F1}, W={rect.Width:F1}, H={rect.Height:F1}");
                    }
                }
                
                lblStatus.Text = $"Nalezeno {highlights.Count} skutečných pozic textu.";

                picPdfViewer.Invalidate(); // Trigger repaint
                
                if (highlights.Count > 0)
                {
                    lblStatus.Text = $"Nalezeno {highlights.Count} výskytů na stránce {currentPageIndex + 1}.";
                }
                else
                {
                    lblStatus.Text = "Žádné výskyty hledaných výrazů na této stránce.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Chyba při hledání textu: {ex.Message}";
                // Fallback k jednoduchému zobrazení
                CreateFallbackHighlights();
            }
        }

        private List<RectangleF> FindRealTextPositions(iText.Kernel.Pdf.PdfPage page, string searchTerm, 
            iText.Kernel.Geom.Rectangle pdfPageSize, SizeF screenPageSize)
        {
            var results = new List<RectangleF>();
            
            try
            {
                // Use LocationTextExtractionStrategy to get text with coordinates
                var strategy = new LocationTextExtractionStrategy();
                var extractedText = PdfTextExtractor.GetTextFromPage(page, strategy);
                
                // Get all text chunks with their positions
                var textChunks = GetTextChunksWithPositions(page);
                
                // DEBUG: Log extracted text chunks
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Extrahovano {textChunks.Count} text chunků z PDF");
                foreach (var chunk in textChunks.Take(10)) // Show first 10 for debugging
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Chunk: '{chunk.Text}' na pozici ({chunk.Rectangle.GetX():F1}, {chunk.Rectangle.GetY():F1})");
                }
                
                // Find matches in the extracted chunks
                foreach (var chunk in textChunks)
                {
                    if (chunk.Text.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] NALEZEN! Text '{chunk.Text}' obsahuje '{searchTerm}'");
                        
                        // Convert PDF coordinates to screen coordinates
                        var screenRect = ConvertPdfCoordsToScreen(chunk.Rectangle, pdfPageSize, screenPageSize);
                        results.Add(screenRect);
                        
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] PDF pozice: ({chunk.Rectangle.GetX():F1}, {chunk.Rectangle.GetY():F1}, {chunk.Rectangle.GetWidth():F1}, {chunk.Rectangle.GetHeight():F1})");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Obrazovka pozice: ({screenRect.X:F1}, {screenRect.Y:F1}, {screenRect.Width:F1}, {screenRect.Height:F1})");
                    }
                }
                
                if (results.Count == 0)
                {
                    lblStatus.Text = $"Text '{searchTerm}' nebyl nalezen na stránce.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Chyba při hledání '{searchTerm}': {ex.Message}";
            }
            
            return results;
        }
        
        private List<TextChunk> GetTextChunksWithPositions(iText.Kernel.Pdf.PdfPage page)
        {
            var chunks = new List<TextChunk>();
            var strategy = new TextChunkLocationStrategy();
            var processor = new PdfCanvasProcessor(strategy);
            processor.ProcessPageContent(page);
            return strategy.GetTextChunks();
        }
        
        private RectangleF ConvertPdfCoordsToScreen(iText.Kernel.Geom.Rectangle pdfRect, 
            iText.Kernel.Geom.Rectangle pdfPageSize, SizeF screenPageSize)
        {
            // Scale factors from PDF points to screen pixels
            float scaleX = screenPageSize.Width / pdfPageSize.GetWidth() * zoomFactor;
            float scaleY = screenPageSize.Height / pdfPageSize.GetHeight() * zoomFactor;
            
            // Convert coordinates - PDF uses bottom-left origin, screen uses top-left
            float x = (float)(pdfRect.GetX() * scaleX);
            float y = (float)((pdfPageSize.GetHeight() - pdfRect.GetY() - pdfRect.GetHeight()) * scaleY);
            float width = (float)(pdfRect.GetWidth() * scaleX);
            float height = (float)(pdfRect.GetHeight() * scaleY);
            
            return new RectangleF(x, y, width, height);
        }
        
        private void CreateFallbackHighlights()
        {
            // Fallback - create visible rectangles in different positions
            var pageSize = pdfViewerDocument?.PageSizes[currentPageIndex] ?? new SizeF(600, 800);
            
            for (int i = 0; i < Math.Min(searchTerms.Count, 6); i++)
            {
                var term = searchTerms[i];
                
                // Simple fallback positioning
                float x = 50 + (i % 3) * 200;
                float y = 50 + (i / 3) * 100;
                float width = Math.Max(term.Length * 8, 60);
                float height = 20;
                
                highlights.Add(new RectangleF(x, y, width, height));
            }
            
            picPdfViewer.Invalidate();
        }






        // Class to represent text chunk with position
        private class TextChunk
        {
            public string Text { get; set; } = "";
            public iText.Kernel.Geom.Rectangle Rectangle { get; set; } = new iText.Kernel.Geom.Rectangle(0, 0, 0, 0);
        }
        
        // Strategy to capture text chunks with their exact positions
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
                        // Get bounding rectangle of the text
                        var baseline = textInfo.GetBaseline();
                        var ascentLine = textInfo.GetAscentLine();
                        
                        var rect = new iText.Kernel.Geom.Rectangle(
                            Math.Min(baseline.GetStartPoint().Get(0), ascentLine.GetStartPoint().Get(0)),
                            Math.Min(baseline.GetStartPoint().Get(1), ascentLine.GetStartPoint().Get(1)),
                            Math.Abs(baseline.GetEndPoint().Get(0) - baseline.GetStartPoint().Get(0)),
                            Math.Abs(ascentLine.GetEndPoint().Get(1) - baseline.GetStartPoint().Get(1))
                        );
                        
                        textChunks.Add(new TextChunk { Text = text, Rectangle = rect });
                        
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
        }
        
        private void PicPdfViewer_Paint(object? sender, PaintEventArgs e)
        {
            if (highlights.Count == 0)
                return;

            // Draw highlight rectangles
            using var highlightBrush = new SolidBrush(Color.FromArgb(100, Color.Red));
            using var highlightPen = new Pen(Color.Red, 2);

            foreach (var highlight in highlights)
            {
                e.Graphics.FillRectangle(highlightBrush, highlight);
                e.Graphics.DrawRectangle(highlightPen, System.Drawing.Rectangle.Round(highlight));
            }
        }

        private void BtnHighlight_Click(object? sender, EventArgs e)
        {
            ParseSearchTerms();
            UpdateHighlights();
            
            if (highlights.Count > 0)
            {
                lblStatus.Text = $"Nalezeno {highlights.Count} výskytů hledaných výrazů na této stránce.";
            }
            else
            {
                lblStatus.Text = "Žádné výskyty hledaných výrazů nebyly nalezeny na této stránce.";
            }
        }

        private void ParseSearchTerms()
        {
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

        private void ClearHighlights()
        {
            highlights.Clear();
            picPdfViewer.Invalidate();
            lblStatus.Text = "Zvýraznění vymazáno.";
        }
        
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
            
            if (pdfDocument != null)
            {
                var page = pdfDocument.GetPage(currentPageIndex + 1);
                var pdfPageSize = page.GetPageSize();
                debugInfo.AppendLine($"Velikost stránky (iText7): {pdfPageSize.GetWidth():F1} x {pdfPageSize.GetHeight():F1}");
                
                // Extract some text for debugging
                var strategy = new SimpleTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                debugInfo.AppendLine($"Délka extrahovaného textu: {pageText.Length} znaků");
                debugInfo.AppendLine($"Začátek textu: {pageText.Substring(0, Math.Min(100, pageText.Length)).Replace("\n", "\\n")}");
            }
            
            // Show debug info
            MessageBox.Show(debugInfo.ToString(), "Debug informace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            // Also write to debug output
            System.Diagnostics.Debug.WriteLine(debugInfo.ToString());
        }

        private void BtnPrevPage_Click(object? sender, EventArgs e)
        {
            if (currentPageIndex > 0)
            {
                currentPageIndex--;
                UpdatePageInfo();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        private void BtnNextPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument != null && currentPageIndex < pdfDocument.GetNumberOfPages() - 1)
            {
                currentPageIndex++;
                UpdatePageInfo();
                RenderCurrentPage();
                UpdateNavigationButtons();
            }
        }

        private void UpdateNavigationButtons()
        {
            if (pdfDocument == null)
            {
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                return;
            }

            btnPrevPage.Enabled = currentPageIndex > 0;
            btnNextPage.Enabled = currentPageIndex < pdfDocument.GetNumberOfPages() - 1;
        }

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

        private void TrackZoom_ValueChanged(object? sender, EventArgs e)
        {
            zoomFactor = trackZoom.Value / 100.0f;
            RenderCurrentPage();
            lblStatus.Text = $"Zoom: {trackZoom.Value}%";
        }

        private void TxtSearchTerms_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && btnHighlight.Enabled)
            {
                BtnHighlight_Click(sender, e);
                e.SuppressKeyPress = true;
            }
        }

        private void CenterImage()
        {
            if (picPdfViewer.Image == null || scrollContainer == null)
                return;

            int x = Math.Max(0, (scrollContainer.ClientSize.Width - picPdfViewer.Width) / 2);
            int y = Math.Max(0, (scrollContainer.ClientSize.Height - picPdfViewer.Height) / 2);
            
            picPdfViewer.Location = new System.Drawing.Point(x, y);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up resources
            pdfDocument?.Close();
            pdfViewerDocument?.Dispose();
            picPdfViewer.Image?.Dispose();
            
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle keyboard shortcuts
            switch (keyData)
            {
                case Keys.Left:
                case Keys.PageUp:
                    if (btnPrevPage.Enabled)
                    {
                        BtnPrevPage_Click(this, EventArgs.Empty);
                        return true;
                    }
                    break;

                case Keys.Right:
                case Keys.PageDown:
                    if (btnNextPage.Enabled)
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