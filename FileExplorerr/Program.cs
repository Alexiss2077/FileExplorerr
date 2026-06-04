using System;
using System.Windows.Forms;
using FileExplorerr.Export;

namespace FileExplorerr
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ── Register native C# exporters (Phases 2+) ─────────────────
            // Each phase adds one line here; no other file changes required.
            //
            //   Phase 2 (current): Excel  → ExcelExporter
            //   Phase 3 (TODO):    Word   → WordExporter
            //   Phase 4 (TODO):    PPTX   → PowerPointExporter
            //   Phase 5 (TODO):    PDF    → PdfExporter  + delete export_office.py
            OfficeExporterFactory.RegisterNativeExporters();

            // ── Catch unhandled exceptions on the UI thread ───────────────
            Application.ThreadException += (_, args) =>
                ShowFatalError(args.Exception);

            // ── Catch unhandled exceptions on background threads ──────────
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