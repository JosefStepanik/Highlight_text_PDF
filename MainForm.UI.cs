// =============================================================
// File: MainForm.UI.cs
// Purpose: Build and style the WinForms user interface.
// Contains: Layout initialization, toolbar, viewer, and status bar.
// Author: Josef Stepanik
// Created: 2026-04
// =============================================================
// GLE colour scheme: #1A1A1A (dark gray), #2252A4 (blue), #00A1D1 (lighter blue), #2D2D2D (dark gray for viewer), #FF8C00 (orange for debug button)
// =============================================================

using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using Svg;

namespace PdfHighlighter
{
    // ===== UI Building - Vytvoření uživatelského rozhraní =====
    public partial class MainForm : Form
    {
        // GLE theme colors (single source of truth)
        private static readonly Color GLEDarkGray = Color.FromArgb(0x1A, 0x1A, 0x1A);      // #1A1A1A
        private static readonly Color GLEBlue = Color.FromArgb(0x22, 0x52, 0xA4);          // #2252A4
        private static readonly Color GLELightBlue = Color.FromArgb(0x00, 0xA1, 0xD1);     // #00A1D1
        private static readonly Color GLEViewerGray = Color.FromArgb(0x2D, 0x2D, 0x2D);    // #2D2D2D
        private static readonly Color GLEDebugOrange = Color.FromArgb(0xFF, 0x8C, 0x00);   // #FF8C00
        private static readonly Color GLEText = Color.White;
        private static readonly Color HighlightGreen = Color.FromArgb(0x00, 0xE6, 0x76);  // vivid green #00E676

        // Nastaví primární vizuelní styl (GLE světlemodrou barvu, bílý text, tenký okraj) na tlačítko.
        private void ApplyPrimaryButtonStyle(Button button)
        {
            button.BackColor = GLELightBlue;
            button.ForeColor = GLEText;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = GLEText;
            button.UseVisualStyleBackColor = false;
        }

        // Nastaví tlačítko do aktivního nebo neaktivního vizuelního stavu bez deaktivace (Enabled zůstává true).
        // Aktivní stav = světlemodrou, neaktivní = tmavě modrou. Stav je uložen v button.Tag pro pozdejší kontrolu.
        private void SetButtonActiveState(Button button, bool isActive)
        {
            button.Enabled = true;
            button.Tag = isActive;
            button.BackColor = isActive ? GLELightBlue : GLEBlue;
            button.ForeColor = GLEText;
            button.FlatAppearance.BorderColor = GLEText;
        }

        // Vrátí true, pokud bylo tlačítko před tím označeno jako aktivní přes SetButtonActiveState.
        private bool IsButtonActive(Button button)
        {
            return button.Tag is bool isActive && isActive;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "PDF Text Highlighter - G.L.Electronic component highlighting tool";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(900, 600);
            this.BackColor = GLEDarkGray;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 136)); // Toolbar
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // PDF viewer
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Status bar

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

        // Vytvoří hlavní panel nástrojové lišty se dvěma řádky: výběr souboru/navigace nahoře a vyhledávání dole.
        private Panel CreateToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GLEBlue,
                Padding = new Padding(12, 10, 12, 10)
            };

            toolbar.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            var toolbarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

            var topRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                Margin = new Padding(0)
            };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var bottomRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                Margin = new Padding(0)
            };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12F));

            // First row - File selection and navigation
            var lblFile = new Label
            {
                Text = "PDF soubor:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = GLEText
            };
            topRow.Controls.Add(lblFile, 0, 0);

            btnSelectPdf = new Button
            {
                Text = "Vybrat PDF soubor...",
                AutoSize = false,
                Size = new Size(126, 28),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0)
            };
            ApplyPrimaryButtonStyle(btnSelectPdf);
            btnSelectPdf.Click += BtnSelectPdf_Click;
            topRow.Controls.Add(btnSelectPdf, 1, 0);

            btnPrevPage = new Button
            {
                Text = "◀ Předchozí",
                AutoSize = false,
                Size = new Size(102, 28),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 6, 0)
            };
            ApplyPrimaryButtonStyle(btnPrevPage);
            SetButtonActiveState(btnPrevPage, false);
            btnPrevPage.Click += BtnPrevPage_Click;
            topRow.Controls.Add(btnPrevPage, 2, 0);

            btnNextPage = new Button
            {
                Text = "Následující ▶",
                AutoSize = false,
                Size = new Size(116, 28),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 12, 0)
            };
            ApplyPrimaryButtonStyle(btnNextPage);
            SetButtonActiveState(btnNextPage, false);
            btnNextPage.Click += BtnNextPage_Click;
            topRow.Controls.Add(btnNextPage, 3, 0);

            lblPageInfo = new Label
            {
                Text = "Žádný PDF",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 18, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = GLEText
            };
            topRow.Controls.Add(lblPageInfo, 4, 0);

            // Zoom controls
            var lblZoom = new Label
            {
                Text = "Zoom:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = GLEText
            };
            topRow.Controls.Add(lblZoom, 5, 0);

            trackZoom = new TrackBar
            {
                Minimum = 25,
                Maximum = 300,
                Value = 100,
                TickFrequency = 25,
                Width = 150,
                Anchor = AnchorStyles.Left,
                Enabled = false
            };
            trackZoom.ValueChanged += TrackZoom_ValueChanged;
            topRow.Controls.Add(trackZoom, 6, 0);

            // Second row - Search
            var lblSearch = new Label
            {
                Text = "Hledané výrazy (oddělené čárkou):",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = GLEText
            };
            bottomRow.Controls.Add(lblSearch, 0, 0);

            txtSearchTerms = new TextBox
            {
                PlaceholderText = "například: slovo1, dlouhá fráze, jiný text",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = GLEViewerGray,
                ForeColor = GLEText,
                Multiline = true,
                MinimumSize = new Size(150, 60),
                ScrollBars = ScrollBars.Vertical
            };
            txtSearchTerms.KeyDown += TxtSearchTerms_KeyDown;
            bottomRow.Controls.Add(txtSearchTerms, 1, 0);

            btnHighlight = new Button
            {
                Text = "🔍 Zvýraznit text",
                AutoSize = false,
                Size = new Size(118, 28),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0)
            };
            ApplyPrimaryButtonStyle(btnHighlight);
            SetButtonActiveState(btnHighlight, false);
            btnHighlight.Click += BtnHighlight_Click;
            bottomRow.Controls.Add(btnHighlight, 2, 0);

            var btnClear = new Button
            {
                Text = "Vymazat",
                AutoSize = false,
                Size = new Size(88, 28),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0)
            };
            ApplyPrimaryButtonStyle(btnClear);
            btnClear.Click += (s, e) => { ClearHighlights(); };
            bottomRow.Controls.Add(btnClear, 3, 0);
            
            var btnDebug = new Button
            {
                Text = "🔧 Debug",
                AutoSize = false,
                Size = new Size(92, 28),
                BackColor = GLEDebugOrange,
                ForeColor = GLEText,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 0),
                FlatStyle = FlatStyle.Flat
            };
            btnDebug.FlatAppearance.BorderSize = 1;
            btnDebug.FlatAppearance.BorderColor = GLEText;
            btnDebug.Click += BtnDebug_Click;
            bottomRow.Controls.Add(btnDebug, 4, 0);

            contentLayout.Controls.Add(topRow, 0, 0);
            contentLayout.Controls.Add(bottomRow, 0, 1);

            toolbarLayout.Controls.Add(contentLayout, 0, 0);
            toolbarLayout.Controls.Add(CreateLogoControl(), 1, 0);
            toolbar.Controls.Add(toolbarLayout);
            return toolbar;
        }

        // Načte SVG logo z disku a zobrazí ho vpravo v toolbar panelu.
        // Pokud soubor neexistuje nebo se nepodaří načíst, použije textový fallback.
        private Control CreateLogoControl()
        {
            var logoHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GLEBlue,
                Margin = new Padding(0)
            };

            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGO_1COLOR_SVG.svg");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "LOGO_1COLOR_SVG.svg");
                logoPath = Path.GetFullPath(logoPath);
            }

            if (!File.Exists(logoPath))
            {
                var fallbackLabel = new Label
                {
                    Text = "G.L.Electronic",
                    Dock = DockStyle.Right,
                    AutoSize = false,
                    Width = 210,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = GLEText
                };
                logoHost.Controls.Add(fallbackLabel);
                return logoHost;
            }

            var logoImage = LoadSvgLogoImage(logoPath, 190, 54);
            if (logoImage == null)
            {
                var fallbackLabel = new Label
                {
                    Text = "G.L.Electronic",
                    Dock = DockStyle.Right,
                    AutoSize = false,
                    Width = 210,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = GLEText
                };
                logoHost.Controls.Add(fallbackLabel);
                return logoHost;
            }

            var logoBox = new PictureBox
            {
                Dock = DockStyle.Right,
                Width = 210,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = GLEBlue,
                Image = logoImage,
                Margin = new Padding(0)
            };

            logoHost.Controls.Add(logoBox);
            return logoHost;
        }

        // Otevře SVG soubor pomocí knihovny Svg a vyrenduje ho do bitmapy se zadanými rozměry.
        // Vrátí null, pokud otevření nebo renderování selhá.
        private static Image? LoadSvgLogoImage(string svgPath, int maxWidth, int maxHeight)
        {
            try
            {
                var svgDocument = SvgDocument.Open(svgPath);
                svgDocument.Width = maxWidth;
                svgDocument.Height = maxHeight;
                return svgDocument.Draw(maxWidth, maxHeight);
            }
            catch
            {
                return null;
            }
        }

        // Vytvoří panel pro zobrazování PDF stránek. Používá ScrollableControl pro posouvání
        // a PictureBox jako plošku pro renderovanou bitmapu a překryvání highlighťů.
        private Panel CreatePdfViewer()
        {
            var viewerContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GLEViewerGray,
                AutoScroll = true
            };

            scrollContainer = new ScrollableControl
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = GLEViewerGray
            };

            picPdfViewer = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Visible = false
            };
            picPdfViewer.Paint += PicPdfViewer_Paint;
            picPdfViewer.MouseClick += PicPdfViewer_MouseClick;

            scrollContainer.Controls.Add(picPdfViewer);
            viewerContainer.Controls.Add(scrollContainer);

            return viewerContainer;
        }

        // Vytvoří stavový řádek se dvěma sloupcě: vlevo RichTextBox pro stavové zprávy (s podporou barvení),
        // vpravo Label pro chybové zprávy. Výška se automaticky přizpůsobuje obsahu.
        private Panel CreateStatusBar()
        {
            var statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GLEBlue,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 30)
            };

            var statusLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));  // Pozitivní zpráva
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));  // Chyby

            lblStatus = new RichTextBox
            {
                Text = "Připraven. Vyberte PDF soubor pro začátek.",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = GLEBlue,
                ForeColor = GLEText,
                ScrollBars = RichTextBoxScrollBars.None,
                DetectUrls = false,
                Multiline = true,
                TabStop = false,
                Margin = new Padding(0),
                SelectionAlignment = HorizontalAlignment.Left
            };
            lblStatus.SelectAll();
            lblStatus.SelectionIndent = 8;
            lblStatus.DeselectAll();
            statusLayout.Controls.Add(lblStatus, 0, 0);

            lblStatusErrors = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 4, 10, 4),
                ForeColor = Color.Red
            };
            statusLayout.Controls.Add(lblStatusErrors, 1, 0);

            statusPanel.Controls.Add(statusLayout);

            // Přizpůsobení výšky status baru obsahu
            statusLayout.Layout += (_, _) =>
            {
                // Zvětší řádek na výšku nejvyššího labelu
                int maxHeight = Math.Max(
                    MeasureControlTextHeight(lblStatus.Text, lblStatus.Font, statusLayout.ColumnStyles[0].Width > 0
                        ? (int)(statusLayout.ClientSize.Width * 0.55f) - 14 : 300),
                    MeasureLabelHeight(lblStatusErrors, statusLayout.ColumnStyles[1].Width > 0
                        ? (int)(statusLayout.ClientSize.Width * 0.45f) - 14 : 200)
                );
                int rowHeight = Math.Max(30, maxHeight + 8);
                if (statusLayout.RowStyles.Count == 0)
                    statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                else
                    statusLayout.RowStyles[0] = new RowStyle(SizeType.Absolute, rowHeight);
            };

            return statusPanel;
        }

        // Změří výšku textu v Labelu pro daný dostupný šířku pomocí GDI MeasureString.
        private static int MeasureLabelHeight(Label lbl, int availableWidth)
        {
            if (string.IsNullOrEmpty(lbl.Text) || availableWidth <= 0)
                return 0;
            using var g = lbl.CreateGraphics();
            var size = g.MeasureString(lbl.Text, lbl.Font,
                new SizeF(availableWidth, float.MaxValue),
                StringFormat.GenericDefault);
            return (int)Math.Ceiling(size.Height);
        }

        // Změří výšku textu pomocí TextRenderer (vhodnější pro RichTextBox a ovládací prvky bez WordWrapu).
        private static int MeasureControlTextHeight(string text, Font font, int availableWidth)
        {
            if (string.IsNullOrEmpty(text) || availableWidth <= 0)
                return 0;

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.Left;
            var measured = TextRenderer.MeasureText(text, font, new Size(availableWidth, int.MaxValue), flags);
            return measured.Height;
        }
    }
}
