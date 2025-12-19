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
            
            // Spuštění hlavní formy
            Application.Run(new MainForm());
        }
    }
}