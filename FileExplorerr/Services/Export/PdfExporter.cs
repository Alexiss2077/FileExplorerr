using System;
using System.Data;
using System.IO;
using System.Threading;
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
    //  QuestPDF.GeneratePdf() is synchronous and single-threaded — it builds
    //  the entire layout in one pass before writing.  For large/wide datasets
    //  this can take several seconds.  A background animation timer keeps the
    //  progress bar moving so the user knows the app is working.
    //
    //  No row or column limit — QuestPDF paginates automatically.
    //  Landscape auto-switch when columns > 6.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class PdfExporter : IOfficeExporter
    {
        private const int LandscapeColLimit = 6;

        // Cell ceiling: rows × cols above this will take too long.
        // 500 000 cells ≈ 5 000 rows × 100 cols or 10 000 rows × 50 cols.
        // For larger datasets Excel is the right tool.
        private const int MaxCells = 500_000;

        // Font / padding tuned for wide datasets (≥ 20 cols)
        private const float FontSizeHeader = 7f;
        private const float FontSizeData = 6.5f;
        private const float PaddingHeader = 3f;
        private const float PaddingData = 2f;

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

            // ── Fail fast: 2.5 M cells would take 15+ minutes ────────────
            int totalCells = data.Rows.Count * data.Columns.Count;
            if (totalCells > MaxCells)
            {
                int maxRows = MaxCells / Math.Max(data.Columns.Count, 1);
                return ExportResult.Fail(
                    $"El dataset tiene {data.Rows.Count:N0} filas × {data.Columns.Count} columnas" +
                    $" = {totalCells:N0} celdas.\n\n" +
                    $"PDF puede procesar hasta {MaxCells:N0} celdas ({maxRows:N0} filas con {data.Columns.Count} columnas).\n\n" +
                    "Para datasets grandes usa Excel (.xlsx) que no tiene este límite.");
            }

            TryDeletePartial(options.OutputPath);

            try
            {
                // Start a background animation timer so the progress bar
                // keeps moving while QuestPDF works synchronously.
                using var animCts = CancellationTokenSource
                    .CreateLinkedTokenSource(options.CancellationToken);

                int animPct = 10;
                var animTask = Task.Run(async () =>
                {
                    while (!animCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(600, animCts.Token).ConfigureAwait(false);
                        animPct = Math.Min(animPct + 1, 88);
                        progress?.Report(animPct);
                    }
                }, animCts.Token);

                progress?.Report(5);

                // Run the synchronous QuestPDF generation on the thread-pool.
                ExportResult result = await Task.Run(
                    () => BuildPdf(data, options),
                    options.CancellationToken);

                // Stop the animation and jump to 100%.
                animCts.Cancel();
                try { await animTask; } catch (OperationCanceledException) { }

                if (result.Success)
                    progress?.Report(100);

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
        //  PDF BUILDER  (thread-pool, synchronous QuestPDF call)
        // ════════════════════════════════════════════════════════════════════

        private static ExportResult BuildPdf(
            DataTable data,
            ExportOptions options)
        {
            int colCount = data.Columns.Count;
            int rowCount = data.Rows.Count;
            bool landscape = colCount > LandscapeColLimit;

            var headerBg = ParseColor(options.HeaderBackground);
            var headerFg = ParseColor(options.HeaderForeground);
            var rowEvenBg = ParseColor(options.RowEvenBackground);
            var rowOddBg = ParseColor(options.RowOddBackground);
            var accentClr = ParseColor(options.AccentColor);

            string? dir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(1f, Unit.Centimetre);
                    page.DefaultTextStyle(t =>
                        t.FontFamily("Arial").FontSize(FontSizeData));

                    // ── Header (every page) ───────────────────────────────
                    page.Header().Column(col =>
                    {
                        col.Item()
                           .PaddingBottom(3)
                           .Row(row =>
                           {
                               row.RelativeItem().Text(text =>
                               {
                                   text.Span(SanitiseText(options.Title))
                                       .Bold().FontSize(11).FontColor(accentClr);

                                   string meta =
                                       $"  ·  {rowCount:N0} filas  ·  {colCount} col.";
                                   if (options.IncludeTimestamp)
                                       meta += $"  ·  {DateTime.Now:dd/MM/yyyy HH:mm}";

                                   text.Span(meta)
                                       .FontSize(7)
                                       .FontColor(Colors.Grey.Medium);
                               });
                           });

                        col.Item()
                           .BorderBottom(0.5f)
                           .BorderColor(Colors.Grey.Lighten2)
                           .PaddingBottom(3);
                    });

                    // ── Table ─────────────────────────────────────────────
                    page.Content().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            for (int c = 0; c < colCount; c++)
                                cols.RelativeColumn();
                        });

                        // Header row — repeats on every page break
                        table.Header(header =>
                        {
                            for (int c = 0; c < colCount; c++)
                            {
                                header.Cell()
                                      .Background(headerBg)
                                      .Padding(PaddingHeader)
                                      .Text(SanitiseText(data.Columns[c].ColumnName))
                                      .Bold()
                                      .FontSize(FontSizeHeader)
                                      .FontColor(headerFg);
                            }
                        });

                        // Data rows
                        for (int r = 0; r < rowCount; r++)
                        {
                            string bg = r % 2 == 0 ? rowEvenBg : rowOddBg;

                            for (int c = 0; c < colCount; c++)
                            {
                                string text = SanitiseText(
                                    data.Rows[r][c]?.ToString() ?? string.Empty);

                                table.Cell()
                                     .Background(bg)
                                     .BorderBottom(0.3f)
                                     .BorderColor(Colors.Grey.Lighten3)
                                     .Padding(PaddingData)
                                     .Text(text)
                                     .FontSize(FontSizeData)
                                     .FontColor(Colors.Black);
                            }
                        }
                    });

                    // ── Footer ────────────────────────────────────────────
                    page.Footer()
                        .AlignRight()
                        .Text(text =>
                        {
                            text.Span("Página ")
                                .FontSize(6).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber()
                                .FontSize(6).FontColor(Colors.Grey.Medium);
                            text.Span(" de ")
                                .FontSize(6).FontColor(Colors.Grey.Medium);
                            text.TotalPages()
                                .FontSize(6).FontColor(Colors.Grey.Medium);
                        });
                });
            })
            .GeneratePdf(options.OutputPath);

            return ExportResult.Ok(options.OutputPath, rowCount);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

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