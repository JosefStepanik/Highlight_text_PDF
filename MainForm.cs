using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using iText.Kernel.Pdf;
using PdfiumViewer;

namespace PdfHighlighter
{
    // ===== MainForm - Hlavní třída aplikace s deklaracemi polí a konstant =====
    public partial class MainForm : Form
    {
        // === UI ovládací prvky ===
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

        // === Stav aplikace a PDF dokumentu ===
        private iText.Kernel.Pdf.PdfDocument? pdfDocument;
        private PdfiumViewer.PdfDocument? pdfViewerDocument;
        private int currentPageIndex = 0;
        private float zoomFactor = 1.0f;
        private List<string> searchTerms = new List<string>();
        private string? currentPdfPath;
        private List<RectangleF> highlights = new List<RectangleF>();
        private HashSet<int> selectedHighlightIndices = new HashSet<int>();

        // === Konstanty pro vykreslení a hledání ===
        
        /// <summary>
        /// Konverzní faktor z PDF bodů (72 DPI) na obrazovku (96 DPI).
        /// Používá se k převodu souřadnic z PDF souřadnicového systému na obrazovku.
        /// </summary>
        private const float PointsToPixels = 96f / 72f;
        
        /// <summary>
        /// Rozšíření zvýraznění kolem textu v pixelech.
        /// Zajišťuje, aby zvýraznění nebyl příliš těsné kolem textu a zlepšuje čitelnost.
        /// </summary>
        private const float HighlightPaddingPx = 3f;
        
        /// <summary>
        /// Počet sousedních textových chunků, které se kombinují při hledání.
        /// Řeší problém, kdy je hledaný text rozdělen na více chunků (např. "R" v jednom, "114" v dalším).
        /// </summary>
        private const int CrossChunkWindowSize = 3;
        
        /// <summary>
        /// Minimální vzdálenost mezi chunky v pixelech pro rozpoznání, že spolu sousedí.
        /// Chrání před spojováním chunků, které jsou příliš daleko od sebe.
        /// </summary>
        private const float NeighborMinDistance = 4f;
        
        /// <summary>
        /// Faktor pro škálování tolerance sousedství vůči velikosti chunku.
        /// Větší chunky mají větší toleranci vzdálenosti (1.2x jejich velikost).
        /// </summary>
        private const float NeighborScaleFactor = 1.2f;
        
        /// <summary>
        /// Tolerance v pixelech pro rozpoznání duplicitních obdélníků zvýraznění.
        /// Pokud se dva obdélníky liší méně než tímto počtem pixelů, považují se za stejné.
        /// </summary>
        private const float SimilarRectTolerancePx = 1.5f;
        
        /// <summary>
        /// Maximální poměr plochy zvýraznění vůči ploše stránky (8%).
        /// Ochrana proti chybám: zvýraznění větší než 8% stránky je považováno za nekorektní.
        /// </summary>
        private const float MaxReasonableHighlightAreaRatio = 0.08f;

        public MainForm()
        {
            InitializeComponent();
        }
    }
}
