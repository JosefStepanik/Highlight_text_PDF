using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PdfHighlighter
{
    // Transparent overlay that follows the system cursor and draws an orbiting icon around it.
    internal class CursorOrbitOverlay : Form
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CURSORINFO pci);

        private const int IDC_ARROW = 32512;

        // NOTE: overlay lifetime is controlled by the caller via ShowOverlay/CloseOverlay.

        private readonly System.Windows.Forms.Timer timer;
        private float angleDeg = 0f;
        private readonly Bitmap iconBitmap;
        private IntPtr ownerHandle = IntPtr.Zero;
        private IntPtr arrowCursorHandle = IntPtr.Zero;
        private int arrowStreak = 0;
        public CursorOrbitOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 64;
            Height = 64;
            BackColor = Color.Magenta; // use magenta as transparent color key
            TransparencyKey = BackColor;

            // Load favicon.ico or fallback to app icon
            Bitmap? bmp = null;
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var favPath = System.IO.Path.Combine(exeDir, "favicon.ico");
                if (System.IO.File.Exists(favPath))
                {
                    using var ico = new Icon(favPath);
                    bmp = ico.ToBitmap();
                }
                else
                {
                    using var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (ico != null)
                        bmp = ico.ToBitmap();
                }
            }
            catch { bmp = null; }

            iconBitmap = bmp ?? new Bitmap(24, 24);

            // Increase overlay size based on icon dimensions so the orbiting icon is not clipped.
            try
            {
                int preferred = Math.Max(128, Math.Max(iconBitmap.Width, iconBitmap.Height) * 5);
                Width = preferred;
                Height = preferred;
            }
            catch { }

            timer = new System.Windows.Forms.Timer { Interval = 30 };
            timer.Tick += (s, e) => { angleDeg += 9f; if (angleDeg >= 360f) angleDeg -= 360f; UpdatePositionAndRedraw(); CheckCloseConditions(); };

            // load standard arrow cursor handle for comparison
            try { arrowCursorHandle = LoadCursor(IntPtr.Zero, IDC_ARROW); } catch { arrowCursorHandle = IntPtr.Zero; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO icon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // Creates a 1x1 transparent cursor and returns its handle (must be destroyed with DestroyIcon).
        public static IntPtr CreateBlankCursorHandle()
        {
            using var bmp = new Bitmap(1, 1);
            bmp.MakeTransparent();
            IntPtr hBmp = bmp.GetHbitmap(Color.Transparent);

            var iconInfo = new ICONINFO();
            iconInfo.fIcon = false;
            iconInfo.xHotspot = 0;
            iconInfo.yHotspot = 0;
            iconInfo.hbmMask = hBmp;
            iconInfo.hbmColor = hBmp;

            IntPtr hCursor = CreateIconIndirect(ref iconInfo);
            // cleanup GDI bitmap
            DeleteObject(hBmp);
            return hCursor;
        }

        public static void DestroyCursorHandle(IntPtr hCursor)
        {
            try
            {
                if (hCursor != IntPtr.Zero)
                    DestroyIcon(hCursor);
            }
            catch { }
        }

        private void UpdatePositionAndRedraw()
        {
            if (GetCursorPos(out var p))
            {
                // place overlay centered on cursor
                var screen = Screen.FromPoint(p);
                int x = p.X - Width / 2;
                int y = p.Y - Height / 2;
                // Ensure overlay stays on same monitor
                Location = new Point(x, y);
                try { AppLogger.Log($"CursorOrbitOverlay: cursor=({p.X},{p.Y}) overlay=({Location.X},{Location.Y})"); } catch { }
            }
            Invalidate();
        }

        private bool paintedOnce = false;

        private void CheckCloseConditions()
        {
            try
            {
                // Close if owner window is no longer foreground
                if (ownerHandle != IntPtr.Zero)
                {
                    var fg = GetForegroundWindow();
                    if (fg != ownerHandle)
                    {
                        BeginInvoke((Action)(() => { try { Close(); } catch { } }));
                        return;
                    }
                }

                // Check system cursor handle; if it's the standard arrow for several consecutive ticks, close overlay.
                try
                {
                    if (arrowCursorHandle != IntPtr.Zero)
                    {
                        var info = new CURSORINFO();
                        info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));
                        if (GetCursorInfo(ref info))
                        {
                            if (info.hCursor == arrowCursorHandle)
                            {
                                arrowStreak++;
                                if (arrowStreak >= 5)
                                {
                                    try { AppLogger.Log($"CursorOrbitOverlay: arrowStreak={arrowStreak} closing overlay"); } catch { }
                                    BeginInvoke((Action)(() => { try { Close(); } catch { } }));
                                    return;
                                }
                            }
                            else
                            {
                                if (arrowStreak != 0) { try { AppLogger.Log("CursorOrbitOverlay: arrowStreak reset"); } catch { } }
                                arrowStreak = 0;
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        public static void HideSystemCursor()
        {
            try { ShowCursor(false); } catch { }
        }

        public static void ShowSystemCursor()
        {
            try { ShowCursor(true); } catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            timer.Start();
            try { AppLogger.Log("CursorOrbitOverlay: OnLoad - started"); } catch { }
            try { BringToFront(); TopMost = true; SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW); } catch { }
            try { overlayReady?.Set(); } catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_NOACTIVATE (0x08000000) prevents the window from receiving focus when shown.
                // WS_EX_TOOLWINDOW (0x00000080) hides the window from Alt-Tab.
                const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);
                const int WS_EX_TOOLWINDOW = 0x00000080;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            iconBitmap.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (!paintedOnce)
            {
                paintedOnce = true;
                try { AppLogger.Log("CursorOrbitOverlay: first OnPaint"); } catch { }
            }
            try { AppLogger.Log($"CursorOrbitOverlay: OnPaint iconSize={iconBitmap.Width}x{iconBitmap.Height} angle={angleDeg:F1}"); } catch { }

            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;
            // ensure radius leaves enough room so the icon doesn't get drawn outside the client area
            int baseRadius = Math.Min(cx, cy) - Math.Max(iconBitmap.Width, iconBitmap.Height) / 2 - 6;
            // shrink the circle by one third (use ~66% of base radius)
            int radius = (int)(baseRadius * 2.0 / 3.0);
            if (radius < 8) radius = Math.Max(8, Math.Min(cx, cy) - 6);

            using (var pen = new Pen(Color.FromArgb(220, 0, 161, 209), 2))
            {
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
            }

            double rad = angleDeg * Math.PI / 180.0;
            float ix = cx + (float)(Math.Cos(rad) * radius) - iconBitmap.Width / 2f;
            float iy = cy + (float)(Math.Sin(rad) * radius) - iconBitmap.Height / 2f;

            // Draw the favicon image (if visible)
            try
            {
                g.DrawImage(iconBitmap, ix, iy, iconBitmap.Width, iconBitmap.Height);
            }
            catch { }

            // (debug red dot removed)
        }

        // Thread management
        private static Thread? overlayThread;
        private static CursorOrbitOverlay? instance;
        private static ManualResetEvent? overlayReady;

        public static void ShowOverlay(IntPtr ownerWindowHandle = default)
        {
            if (overlayThread != null && overlayThread.IsAlive)
                return;

            overlayReady = new ManualResetEvent(false);
            overlayThread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    instance = new CursorOrbitOverlay();
                    if (ownerWindowHandle != IntPtr.Zero)
                        instance.ownerHandle = ownerWindowHandle;
                    AppLogger.Log("CursorOrbitOverlay: thread started");
                    Application.Run(instance);
                }
                catch (Exception ex)
                {
                    try { AppLogger.Log("CursorOrbitOverlay thread error: " + ex.Message); } catch { }
                }
            });
            overlayThread.IsBackground = true;
            overlayThread.SetApartmentState(ApartmentState.STA);
            overlayThread.Start();

            // Wait until overlay signals it's loaded
            try { overlayReady?.WaitOne(3000); } catch { }
        }

        public static void CloseOverlay()
        {
            try
            {
                if (instance != null && !instance.IsDisposed)
                {
                    instance.BeginInvoke((Action)(() => { try { instance.Close(); } catch { } }));
                }
            }
            catch { }
            finally
            {
                overlayThread = null;
                instance = null;
            }
        }
    }
}
