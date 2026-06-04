using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  PDF EXPORTER  (Phase 5)
    //  Generates .pdf files from a DataTable using QuestPDF.
    //
    //  NuGet: QuestPDF (latest stable) — Community MIT license, free for
    //  individuals and organizations under $1M annual revenue.
    //
    //  Layout:
    //    • Cover page  — title, row/column count, optional timestamp
    //    • Data pages  — table with paginated rows, header repeats on each page
    //
    //  Features:
    //    ✔ Automatic pagination — QuestPDF handles page breaks natively
    //    ✔ Header row repeated on every page
    //    ✔ Arctic Night palette (dark header, alternating rows)
    //    ✔ Landscape auto-switch when columns > 6
    //    ✔ No row limit — PDF handles any dataset size correctly
    //    ✔ Cooperative cancellation checked before generation starts
    //    ✔ Progress 0 → 100
    //    ✔ Partial file deleted on failure / cancellation
    // ════════════════════════════════════════════════════════════════════════
    public sealed class PdfExporter : IOfficeExporter
    {
        private const int LandscapeColLimit = 6;

        public string SupportedExtension => ".pdf";

        public async Task<ExportResult> ExportAsync(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress)
        {
            if (data is null) return ExportResult.Fail("DataTable es nulo.");
            if (options is null) return ExportResult.Fail("ExportOptions es nulo.");

            if (options.CancellationToken.IsCancellationRequested)
                return ExportResult.Cancelled();

            TryDeletePartial(options.OutputPath);

            try
            {
                var result = await Task.Run(
                    () => BuildPdf(data, options, progress),
                    options.CancellationToken);

                return result;
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(options.OutputPath);
                return ExportResult.Cancelled();
            }
            catch (Exception ex)
            {
                TryDeletePartial(options.OutputPath);
                return ExportResult.Fail(ex, $"Error al generar PDF:\n{ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF BUILDER  (thread-pool)
        // ════════════════════════════════════════════════════════════════════

        private static ExportResult BuildPdf(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress)
        {
            progress?.Report(5);

            int colCount = data.Columns.Count;
            int rowCount = data.Rows.Count;
            bool landscape = colCount > LandscapeColLimit;

            // Parse Arctic Night colours for QuestPDF
            var headerBg = ParseColor(options.HeaderBackground);
            var headerFg = ParseColor(options.HeaderForeground);
            var rowEvenBg = ParseColor(options.RowEvenBackground);
            var rowOddBg = ParseColor(options.RowOddBackground);
            var accentClr = ParseColor(options.AccentColor);

            string? dir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            progress?.Report(10);

            // ── Build document ────────────────────────────────────────────
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Page size and margins
                    page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                    // ── Header (repeats on every page) ────────────────────
                    page.Header().Column(col =>
                    {
                        col.Item()
                           .PaddingBottom(4)
                           .Row(row =>
                           {
                               row.RelativeItem()
                                  .Text(text =>
                                  {
                                      text.Span(SanitiseText(options.Title))
                                          .Bold()
                                          .FontSize(13)
                                          .FontColor(accentClr);

                                      string meta = $"  ·  {rowCount:N0} filas  ·  {colCount} columnas";
                                      if (options.IncludeTimestamp)
                                          meta += $"  ·  {DateTime.Now:dd/MM/yyyy HH:mm}";
                                      text.Span(meta)
                                          .FontSize(8)
                                          .FontColor(Colors.Grey.Medium);
                                  });
                           });

                        col.Item()
                           .BorderBottom(1)
                           .BorderColor(Colors.Grey.Lighten2)
                           .PaddingBottom(4);
                    });

                    // ── Content: data table ───────────────────────────────
                    page.Content().PaddingTop(8).Table(table =>
                    {
                        // Column definitions
                        table.ColumnsDefinition(cols =>
                        {
                            for (int c = 0; c < colCount; c++)
                                cols.RelativeColumn();
                        });

                        // Header row — repeats on page break
                        table.Header(header =>
                        {
                            for (int c = 0; c < colCount; c++)
                            {
                                header.Cell()
                                      .Background(headerBg)
                                      .Padding(4)
                                      .Text(SanitiseText(data.Columns[c].ColumnName))
                                      .Bold()
                                      .FontSize(8)
                                      .FontColor(headerFg);
                            }
                        });

                        // Data rows
                        for (int r = 0; r < rowCount; r++)
                        {
                            var bg = r % 2 == 0 ? rowEvenBg : rowOddBg;

                            for (int c = 0; c < colCount; c++)
                            {
                                string text = SanitiseText(
                                    data.Rows[r][c]?.ToString() ?? string.Empty);

                                table.Cell()
                                     .Background(bg)
                                     .BorderBottom(0.5f)
                                     .BorderColor(Colors.Grey.Lighten3)
                                     .Padding(3)
                                     .Text(text)
                                     .FontSize(8)
                                     .FontColor(Colors.Black);
                            }

                            // Report progress every 1 000 rows (10% → 88%)
                            if (r > 0 && r % 1_000 == 0)
                                progress?.Report(10 + (int)(78.0 * r / rowCount));
                        }
                    });

                    // ── Footer ────────────────────────────────────────────
                    page.Footer()
                        .AlignRight()
                        .Text(text =>
                        {
                            text.Span("Página ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                            text.Span(" de ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                });
            })
            .GeneratePdf(options.OutputPath);

            progress?.Report(100);

            return ExportResult.Ok(options.OutputPath, rowCount);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a 6-char RRGGBB hex string to a QuestPDF color string.
        /// QuestPDF accepts "#RRGGBB" format.
        /// </summary>
        private static string ParseColor(string hex) => $"#{hex.TrimStart('#')}";

        private static string SanitiseText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (char c in text)
                if (c < 0x20 && c != '\t' && c != '\r' && c != '\n')
                    goto NeedsClean;
            return text;

        NeedsClean:
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
                sb.Append(c < 0x20 && c != '\t' && c != '\r' && c != '\n' ? ' ' : c);
            return sb.ToString();
        }

        private static void TryDeletePartial(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { /* non-fatal */ }
        }
    }
}