// =============================================================
// File: MainForm.Geometry.cs
// Purpose: Convert PDF coordinates to screen coordinates and normalize drawn rectangles.
// Contains: Coordinate transforms, highlight padding/clamping, and viewport-safe geometry helpers.
// Author: Josef Stepanik
// Created: 2026-04
// =============================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace PdfHighlighter
{
    // ===== Geometry & Coordinate Conversion - Konverze souřadnic a geometrie =====
    public partial class MainForm : Form
    {
        private RectangleF ConvertPdfCoordsToScreen(iText.Kernel.Geom.Rectangle pdfRect,
            iText.Kernel.Geom.Rectangle pdfPageSize)
        {
            // Musíme přesně trefit stejné měřítko jako použil PDF renderer.
            float scaleX;
            float scaleY;

            if (picPdfViewer.Image != null)
            {
                scaleX = picPdfViewer.Image.Width / (float)pdfPageSize.GetWidth();
                scaleY = picPdfViewer.Image.Height / (float)pdfPageSize.GetHeight();
            }
            else
            {
                scaleX = zoomFactor * PointsToPixels;
                scaleY = zoomFactor * PointsToPixels;
            }
            
            // PDF má počátek vlevo dole, WinForms vlevo nahoře -> převod osy Y.
            float x = (float)(pdfRect.GetX() * scaleX);
            float y = (float)((pdfPageSize.GetHeight() - pdfRect.GetY() - pdfRect.GetHeight()) * scaleY);
            float width = (float)(pdfRect.GetWidth() * scaleX);
            float height = (float)(pdfRect.GetHeight() * scaleY);
            
            return new RectangleF(x, y, width, height);
        }

        private RectangleF ExpandAndClampScreenRect(RectangleF rect, float padding)
        {
            // Mírné zvětšení zlepšuje čitelnost highlightu a zároveň držíme hranice obrázku.
            float left = rect.Left - padding;
            float top = rect.Top - padding;
            float right = rect.Right + padding;
            float bottom = rect.Bottom + padding;

            if (picPdfViewer.Image != null)
            {
                left = Math.Clamp(left, 0, picPdfViewer.Image.Width);
                top = Math.Clamp(top, 0, picPdfViewer.Image.Height);
                right = Math.Clamp(right, 0, picPdfViewer.Image.Width);
                bottom = Math.Clamp(bottom, 0, picPdfViewer.Image.Height);
            }

            float width = Math.Max(0, right - left);
            float height = Math.Max(0, bottom - top);
            return new RectangleF(left, top, width, height);
        }
    }
}
