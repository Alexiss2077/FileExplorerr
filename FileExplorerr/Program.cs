using System;
using System.Windows.Forms;
using FileExplorerr.Export;
using QuestPDF.Infrastructure;

namespace FileExplorerr
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── QuestPDF license (Community MIT — free under $1M revenue) ─
            QuestPDF.Settings.License = LicenseType.Community;

            // ── Register all native C# exporters ─────────────────────────
            // Phase 2: Excel  (.xlsx) — ClosedXML
            // Phase 3: Word   (.docx) — DocumentFormat.OpenXml
            // Phase 4: PPTX   (.pptx) — DocumentFormat.OpenXml
            // Phase 5: PDF    (.pdf)  — QuestPDF
            OfficeExporterFactory.RegisterNativeExporters();

            // ── Global exception handlers ─────────────────────────────────
            Application.ThreadException += (_, args) =>
                ShowFatalError(args.Exception);

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    ShowFatalError(ex);
            };

            Application.Run(new Form1());
        }

        private static void ShowFatalError(Exception ex)
        {
            MessageBox.Show(
                $"Se produjo un error inesperado:\n\n{ex.Message}\n\n" +
                "La aplicación intentará continuar. Si el problema persiste, reiníciala.",
                "Error inesperado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}