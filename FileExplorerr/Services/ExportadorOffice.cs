// ════════════════════════════════════════════════════════════════════════════
//  ExportadorOffice.cs  (all phases complete — Python fully removed)
//
//  All formats are now handled by native C# exporters:
//    .xlsx → ExcelExporter       (ClosedXML)
//    .docx → WordExporter        (DocumentFormat.OpenXml)
//    .pptx → PowerPointExporter  (DocumentFormat.OpenXml)
//    .pdf  → PdfExporter         (QuestPDF)
//
//  Public API is UNCHANGED — all call-sites require zero modifications.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileExplorerr.Export;

namespace FileExplorerr
{
    public static class ExportadorOffice
    {
        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT  (unchanged signature)
        // ════════════════════════════════════════════════════════════════════

        public static bool ExportarConDialogo(
            DataTable? dt,
            string titulo,
            string ext,
            IWin32Window? owner = null)
        {
            if (dt is null || dt.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string extU = ext.TrimStart('.').ToUpper();
            string filter = ext switch
            {
                ".xlsx" => "Excel (*.xlsx)|*.xlsx",
                ".docx" => "Word (*.docx)|*.docx",
                ".pptx" => "PowerPoint (*.pptx)|*.pptx",
                ".pdf" => "PDF (*.pdf)|*.pdf",
                _ => $"{extU} (*{ext})|*{ext}"
            };

            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar como {extU}",
                Filter = filter + "|Todos (*.*)|*.*",
                FileName = $"{SafeFileName(titulo)}_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

            _ = ExportarAsync(dt.Copy(), titulo, ext, dlg.FileName, owner as Form);
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC EXPORT PIPELINE
        // ════════════════════════════════════════════════════════════════════

        private static async Task ExportarAsync(
            DataTable dt,
            string titulo,
            string ext,
            string outputPath,
            Form? ownerForm)
        {
            var cts = new CancellationTokenSource();
            var progress = new ExportProgressForm(
                titulo, ext.TrimStart('.').ToUpper(), cts);
            progress.Show(ownerForm);

            try
            {
                var exporter = OfficeExporterFactory.Resolve(ext);

                var opts = ExportOptions.For(outputPath, titulo)
                                        .WithCancellation(cts.Token)
                                        .Build();

                var uiProgress = new Progress<int>(pct =>
                {
                    if (!progress.IsDisposed)
                        progress.Invoke(() => progress.SetStatus(null, pct));
                });

                progress.SetStatus("Generando archivo...", 5);

                ExportResult result = await exporter.ExportAsync(dt, opts, uiProgress);

                if (result.Success)
                {
                    progress.SetStatus("¡Listo!", 100);
                    await Task.Delay(400, CancellationToken.None);

                    // Capture values before Invoke to avoid closure issues.
                    string finalPath = result.OutputPath;
                    string finalExt = Path.GetExtension(finalPath).ToUpperInvariant();
                    bool truncated = result.WasTruncated;
                    int rowsWritten = result.RowsWritten;

                    long sizeKb = 0;
                    try { sizeKb = new FileInfo(finalPath).Length / 1024; } catch { }

                    string msg = truncated
                        ? $"Exportado ({sizeKb:N0} KB) — {rowsWritten:N0} filas:\n{finalPath}\n\n" +
                          $"⚠ El dataset fue truncado a las primeras {rowsWritten:N0} filas.\n" +
                          $"Para el dataset completo exporta con Excel (.xlsx).\n\n¿Abrir {finalExt}?"
                        : $"Exportado ({sizeKb:N0} KB):\n{finalPath}\n\n¿Abrir?";

                    progress.Invoke(() =>
                    {
                        progress.Close();
                        if (MessageBox.Show(msg, "Exportación completa",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            { FileName = finalPath, UseShellExecute = true });
                        }
                    });
                }
                else
                {
                    progress.Invoke(() =>
                    {
                        progress.Close();
                        MessageBox.Show(result.ErrorMessage, "Error al exportar",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                progress.Invoke(() => progress.Close());
            }
            catch (Exception ex)
            {
                progress.Invoke(() =>
                {
                    progress.Close();
                    MessageBox.Show($"Error inesperado al exportar:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                cts.Dispose();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DIRECT-CALL OVERLOADS  (called by modules that bypass the dialog)
        // ════════════════════════════════════════════════════════════════════

        public static void ExportarExcel(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, ".xlsx", ruta);

        public static void ExportarWord(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, ".docx", ruta);

        public static void ExportarPowerPoint(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, ".pptx", ruta);

        public static void ExportarPdf(DataTable dt, string titulo, string ruta) =>
            ExportarDirecto(dt, titulo, ".pdf", ruta);

        private static void ExportarDirecto(
            DataTable dt, string titulo, string ext, string outputPath)
        {
            var exporter = OfficeExporterFactory.Resolve(ext);
            var opts = ExportOptions.For(outputPath, titulo).Build();
            var result = exporter.ExportAsync(dt, opts, null)
                                   .GetAwaiter().GetResult();

            if (!result.Success)
                throw new InvalidOperationException(result.ErrorMessage);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static string SafeFileName(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
    }
}