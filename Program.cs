// =============================================================
// File: Program.cs
// Purpose: Bootstrap the WinForms application lifecycle.
// Contains: Application startup configuration, MainForm launch, and logger init/shutdown orchestration.
// Author: Josef Stepanik
// Created: 2026-04
// =============================================================

using System;
using System.Windows.Forms;

namespace PdfHighlighter
{
    internal static class Program
    {
        /// <summary>
        /// Hlavní vstupní bod aplikace.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Povolení vizuálních stylů Windows
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppLogger.Initialize();
            
            // Spuštění hlavní formy
            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                AppLogger.Shutdown();
            }
        }
    }
}