using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  EXCEL EXPORTER  (Phase 2)
    //  Generates .xlsx files from a DataTable using ClosedXML.
    //
    //  Features replicated from export_office.py (exportar_xlsx):
    //    ✔ Header row with Arctic Night background (#1A3A4A) + white text
    //    ✔ Alternating row colours (even #E8F4F8 / odd #FFFFFF)
    //    ✔ Column widths auto-sized by sampling up to 300 rows
    //    ✔ Header row frozen (freeze pane below row 1)
    //    ✔ Auto-filter on header row
    //    ✔ Cover / summary information row at the top of the sheet
    //    ✔ Grid borders with #AABBCC
    //    ✔ Cooperative cancellation (checks token every 3 000 rows)
    //    ✔ Progress reported 0 → 100
    //
    //  NuGet dependency:  ClosedXML (≥ 0.102.x)
    // ════════════════════════════════════════════════════════════════════════
    public sealed class ExcelExporter : IOfficeExporter
    {
        // ── Limits ────────────────────────────────────────────────────────
        private const int DefaultMaxRows = 1_048_575; // Excel hard limit (row 1 = header)
        private const int ColumnSampleCount = 300;       // rows sampled for auto-width
        private const int CheckCancelEvery = 3_000;     // rows between token checks

        // ── IOfficeExporter ───────────────────────────────────────────────
        public string SupportedExtension => ".xlsx";

        public async Task<ExportResult> ExportAsync(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress)
        {
            if (data is null) return ExportResult.Fail("DataTable es nulo.");
            if (options is null) return ExportResult.Fail("ExportOptions es nulo.");

            int maxRows = options.MaxRows > 0
                ? Math.Min(options.MaxRows, DefaultMaxRows)
                : DefaultMaxRows;

            try
            {
                // Run the heavy XML work on a thread-pool thread so the UI
                // timer and the progress bar remain responsive.
                var result = await Task.Run(() =>
                    BuildWorkbook(data, options, progress, maxRows),
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
                return ExportResult.Fail(ex,
                    $"Error al generar Excel:\n{ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WORKBOOK CONSTRUCTION  (runs on thread-pool)
        // ════════════════════════════════════════════════════════════════════

        private ExportResult BuildWorkbook(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress,
            int maxRows)
        {
            var ct = options.CancellationToken;

            progress?.Report(5);

            // ── Colour helpers ────────────────────────────────────────────
            var headerBg = XLColor.FromHtml($"#{options.HeaderBackground}");
            var headerFg = XLColor.FromHtml($"#{options.HeaderForeground}");
            var rowEvenBg = XLColor.FromHtml($"#{options.RowEvenBackground}");
            var rowOddBg = XLColor.FromHtml($"#{options.RowOddBackground}");
            var accentClr = XLColor.FromHtml($"#{options.AccentColor}");
            var borderClr = XLColor.FromHtml("#AABBCC");

            using var wb = new XLWorkbook();

            // ── Sheet name: sanitise title to ≤31 chars (Excel limit) ────
            string sheetName = SanitiseSheetName(options.Title);
            var ws = wb.AddWorksheet(sheetName);

            // ── Determine actual row count ────────────────────────────────
            int totalRows = data.Rows.Count;
            bool wasTruncated = totalRows > maxRows;
            int rowsToWrite = wasTruncated ? maxRows : totalRows;
            int colCount = data.Columns.Count;

            progress?.Report(10);

            // ── Info row (row 1) ──────────────────────────────────────────
            WriteInfoRow(ws, options, totalRows, rowsToWrite, colCount,
                         wasTruncated, accentClr);

            // ── Header row (row 2) ────────────────────────────────────────
            int headerRowNum = 2;
            WriteHeaderRow(ws, data, headerRowNum, colCount,
                           headerBg, headerFg, borderClr);

            progress?.Report(15);

            // ── Data rows (row 3 onward) ──────────────────────────────────
            int dataStartRow = headerRowNum + 1;
            WriteDataRows(ws, data, dataStartRow, rowsToWrite, colCount,
                          rowEvenBg, rowOddBg, borderClr,
                          progress, ct);

            progress?.Report(80);
            ct.ThrowIfCancellationRequested();

            // ── Column widths ─────────────────────────────────────────────
            AutoSizeColumns(ws, data, dataStartRow, rowsToWrite, colCount);

            progress?.Report(88);

            // ── Freeze pane below header ──────────────────────────────────
            ws.SheetView.FreezeRows(headerRowNum);

            // ── Auto-filter on header ─────────────────────────────────────
            if (colCount > 0)
            {
                var headerRange = ws.Range(
                    headerRowNum, 1,
                    headerRowNum, colCount);
                headerRange.SetAutoFilter();
            }

            // ── Workbook properties ───────────────────────────────────────
            wb.Properties.Author = options.Author;
            wb.Properties.Title = options.Title;
            wb.Properties.Created = DateTime.Now;

            progress?.Report(92);
            ct.ThrowIfCancellationRequested();

            // ── Save ──────────────────────────────────────────────────────
            // Ensure the directory exists (SaveFileDialog normally guarantees
            // this but we do not assume it).
            string? dir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            wb.SaveAs(options.OutputPath);

            progress?.Report(100);

            return ExportResult.Ok(options.OutputPath, rowsToWrite, wasTruncated);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SECTION WRITERS
        // ════════════════════════════════════════════════════════════════════

        private static void WriteInfoRow(
            IXLWorksheet ws,
            ExportOptions options,
            int totalRows,
            int rowsToWrite,
            int colCount,
            bool wasTruncated,
            XLColor accentClr)
        {
            // Merge columns 1..colCount (or at least 1..1 when no columns)
            int mergeEnd = Math.Max(colCount, 1);

            string infoText = wasTruncated
                ? $"{options.Title}  ·  Primeras {rowsToWrite:N0} de {totalRows:N0} filas  ·  {colCount} columnas"
                : $"{options.Title}  ·  {rowsToWrite:N0} filas  ·  {colCount} columnas";

            if (options.IncludeTimestamp)
                infoText += $"  ·  {DateTime.Now:dd/MM/yyyy HH:mm}";

            var cell = ws.Cell(1, 1);
            cell.Value = infoText;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = accentClr;
            cell.Style.Font.FontSize = 11;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            if (mergeEnd > 1)
                ws.Range(1, 1, 1, mergeEnd).Merge();
        }

        private static void WriteHeaderRow(
            IXLWorksheet ws,
            DataTable data,
            int row,
            int colCount,
            XLColor bg,
            XLColor fg,
            XLColor borderClr)
        {
            for (int c = 0; c < colCount; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = data.Columns[c].ColumnName;
                cell.Style.Fill.BackgroundColor = bg;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = fg;
                cell.Style.Font.FontSize = 10;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ApplyBorder(cell, borderClr);
            }

            ws.Row(row).Height = 20;
        }

        private static void WriteDataRows(
            IXLWorksheet ws,
            DataTable data,
            int startRow,
            int rowCount,
            int colCount,
            XLColor evenBg,
            XLColor oddBg,
            XLColor borderClr,
            IProgress<int>? progress,
            System.Threading.CancellationToken ct)
        {
            int total = rowCount;

            for (int r = 0; r < rowCount; r++)
            {
                ct.ThrowIfCancellationRequested();

                if (r > 0 && r % CheckCancelEvery == 0)
                {
                    // Report incremental progress between 15% and 80%.
                    int pct = 15 + (int)(65.0 * r / total);
                    progress?.Report(pct);
                }

                var xlRow = ws.Row(startRow + r);
                var fillColor = r % 2 == 0 ? evenBg : oddBg;
                var dataRow = data.Rows[r];

                for (int c = 0; c < colCount; c++)
                {
                    var cell = xlRow.Cell(c + 1);
                    SetCellValue(cell, dataRow[c]);
                    cell.Style.Fill.BackgroundColor = fillColor;
                    cell.Style.Font.FontSize = 9;
                    ApplyBorder(cell, borderClr);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns the most appropriate ClosedXML cell value type.
        /// Numbers are stored as doubles so Excel can sort/filter them.
        /// Blank cells are left empty (not set to "(vacío)" — that is a
        /// display concern handled by the quality viewer, not the export).
        /// </summary>
        private static void SetCellValue(IXLCell cell, object? raw)
        {
            string str = raw?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(str))
                return; // leave cell empty

            if (double.TryParse(str,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double num))
            {
                // ClosedXML / Excel cannot store NaN or ±Infinity as a numeric
                // cell value — the .xlsx format does not support them.
                // Fall through to the string branch so the raw text is preserved.
                if (!double.IsFinite(num))
                {
                    cell.Value = str;
                    return;
                }

                cell.Value = num;
                return;
            }

            if (DateTime.TryParse(str,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime dt))
            {
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
                return;
            }

            // Prefix long strings that start with '=' to prevent formula injection.
            cell.Value = str.StartsWith("=") ? "'" + str : str;
        }

        /// <summary>
        /// Computes column widths by sampling up to <see cref="ColumnSampleCount"/>
        /// evenly-spaced rows.  Clamps the result between 8 and 60 characters.
        /// </summary>
        private static void AutoSizeColumns(
            IXLWorksheet ws,
            DataTable data,
            int dataStartRow,
            int rowCount,
            int colCount)
        {
            if (colCount == 0) return;

            // Build a step size so we sample at most ColumnSampleCount rows.
            int step = Math.Max(1, rowCount / ColumnSampleCount);

            for (int c = 0; c < colCount; c++)
            {
                // Seed with the header length.
                int maxLen = data.Columns[c].ColumnName.Length;

                for (int r = 0; r < rowCount; r += step)
                {
                    string val = data.Rows[r][c]?.ToString() ?? string.Empty;
                    if (val.Length > maxLen)
                        maxLen = val.Length;
                }

                // ClosedXML width is measured in character units.
                double width = Math.Max(8, Math.Min(60, maxLen + 2));
                ws.Column(c + 1).Width = width;
            }
        }

        private static void ApplyBorder(IXLCell cell, XLColor color)
        {
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorderColor = color;
            cell.Style.Border.BottomBorderColor = color;
            cell.Style.Border.LeftBorderColor = color;
            cell.Style.Border.RightBorderColor = color;
        }

        /// <summary>
        /// Excel sheet names must be ≤ 31 characters and cannot contain
        /// certain special characters.
        /// </summary>
        private static string SanitiseSheetName(string title)
        {
            char[] invalid = { '\\', '/', '?', '*', '[', ']', ':' };
            string safe = new string(title
                .Select(ch => Array.IndexOf(invalid, ch) >= 0 ? '_' : ch)
                .ToArray());

            return safe.Length > 31 ? safe[..31] : safe;
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