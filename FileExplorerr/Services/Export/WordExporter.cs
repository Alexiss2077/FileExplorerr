using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WdColor = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  WORD EXPORTER  (Phase 3)
    //
    //  Uses the DOM approach (no OpenXmlWriter) which is the most reliable
    //  pattern for DocumentFormat.OpenXml 2.20.0.
    //  Row limit: 500 — Word is a word processor, not a spreadsheet.
    //  Datasets above the limit are truncated; a warning + Excel suggestion
    //  is shown in the cover paragraph and in the result dialog.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class WordExporter : IOfficeExporter
    {
        private const int MaxRows = 500;
        private const int MaxCols = 20;    // >20 cols → use Excel
        private const int MaxCells = 8_000; // rows × cols ceiling
        private const int LandscapeColLimit = 6;
        private const int CheckCancelEvery = 100;

        // twips (1 440 = 1 inch)
        private const uint PortraitW = 11_906;
        private const uint PortraitH = 16_838;
        private const uint PortraitM = 1_134;

        private const uint LandscapeW = 16_838;
        private const uint LandscapeH = 11_906;
        private const uint LandscapeM = 720;

        private const int MinColW = 400;
        private const int MaxColW = 2_880;

        public string SupportedExtension => ".docx";

        public async Task<ExportResult> ExportAsync(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress)
        {
            if (data is null) return ExportResult.Fail("DataTable es nulo.");
            if (options is null) return ExportResult.Fail("ExportOptions es nulo.");

            int rows = data.Rows.Count;
            int cols = data.Columns.Count;

            // ── Fail fast: give the user a clear message instead of a
            //    corrupt file.  Excel handles large/wide datasets better.
            if (cols > MaxCols)
                return ExportResult.Fail(
                    $"El dataset tiene {cols} columnas.\n\n" +
                    $"Word soporta hasta {MaxCols} columnas de forma confiable.\n\n" +
                    "Exporta con Excel (.xlsx) para conservar todas las columnas.");

            int totalCells = Math.Min(rows, MaxRows) * cols;
            if (totalCells > MaxCells)
                return ExportResult.Fail(
                    $"El dataset tiene {rows:N0} filas × {cols} columnas " +
                    $"({totalCells:N0} celdas).\n\n" +
                    $"Word soporta hasta {MaxCells:N0} celdas ({MaxCells / cols} filas con {cols} columnas).\n\n" +
                    "Exporta con Excel (.xlsx) para el dataset completo.");

            int rowsToWrite = Math.Min(rows, MaxRows);
            bool wasTruncated = rows > MaxRows;

            TryDeletePartial(options.OutputPath);

            try
            {
                var result = await Task.Run(
                    () => BuildDocument(data, options, progress, rowsToWrite, wasTruncated),
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
                return ExportResult.Fail(ex, $"Error al generar Word:\n{ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUILD  (thread-pool, DOM approach)
        // ════════════════════════════════════════════════════════════════════

        private ExportResult BuildDocument(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress,
            int rowsToWrite,
            bool wasTruncated)
        {
            var ct = options.CancellationToken;
            int colCount = data.Columns.Count;
            int totalRows = data.Rows.Count;
            bool landscape = colCount > LandscapeColLimit;

            uint pageW = landscape ? LandscapeW : PortraitW;
            uint margin = landscape ? LandscapeM : PortraitM;
            int colW = colCount > 0
                ? Math.Max(MinColW, Math.Min(MaxColW, (int)(pageW - margin * 2) / colCount))
                : MaxColW;

            string? dir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            progress?.Report(5);

            // ── Create the package ────────────────────────────────────────
            using var wordDoc = WordprocessingDocument.Create(
                options.OutputPath,
                WordprocessingDocumentType.Document);

            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            progress?.Report(10);

            // ── 1. Cover paragraph ────────────────────────────────────────
            body.AppendChild(BuildCoverParagraph(
                options, totalRows, rowsToWrite, colCount, wasTruncated));

            progress?.Report(15);
            ct.ThrowIfCancellationRequested();

            // ── 2. Table ──────────────────────────────────────────────────
            var tbl = new Table();

            tbl.AppendChild(new TableProperties(
                new TableWidth
                {
                    Width = (colW * colCount).ToString(),
                    Type = TableWidthUnitValues.Dxa
                },
                new TableBorders(
                    MakeBorder<TopBorder>(),
                    MakeBorder<BottomBorder>(),
                    MakeBorder<LeftBorder>(),
                    MakeBorder<RightBorder>(),
                    MakeBorder<InsideHorizontalBorder>(),
                    MakeBorder<InsideVerticalBorder>()),
                new TableLook { Val = "04A0" }));

            var grid = new TableGrid();
            for (int c = 0; c < colCount; c++)
                grid.AppendChild(new GridColumn { Width = colW.ToString() });
            tbl.AppendChild(grid);

            // Header
            tbl.AppendChild(BuildHeaderRow(data, options, colW));

            // Data rows
            for (int r = 0; r < rowsToWrite; r++)
            {
                ct.ThrowIfCancellationRequested();

                if (r > 0 && r % CheckCancelEvery == 0)
                    progress?.Report(15 + (int)(73.0 * r / rowsToWrite));

                tbl.AppendChild(
                    BuildDataRow(data.Rows[r], options, colW, colCount, r % 2 == 0));
            }

            body.AppendChild(tbl);

            progress?.Report(90);
            ct.ThrowIfCancellationRequested();

            // ── 3. SectionProperties — last child of Body (OOXML rule) ────
            body.AppendChild(new SectionProperties(
                new PageSize
                {
                    Width = landscape ? LandscapeW : PortraitW,
                    Height = landscape ? LandscapeH : PortraitH,
                    Orient = landscape
                        ? PageOrientationValues.Landscape
                        : PageOrientationValues.Portrait
                },
                new PageMargin
                {
                    Top = (int)margin,
                    Bottom = (int)margin,
                    Left = margin,
                    Right = margin
                }));

            // ── 4. Save ───────────────────────────────────────────────────
            mainPart.Document.Save();

            // PackageProperties after Save, before Dispose
            wordDoc.PackageProperties.Title = options.Title;
            wordDoc.PackageProperties.Creator = options.Author;
            wordDoc.PackageProperties.Created = DateTime.UtcNow;

            // wordDoc.Dispose() called by using → closes zip correctly

            progress?.Report(100);

            return ExportResult.Ok(options.OutputPath, rowsToWrite, wasTruncated);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ROW / CELL BUILDERS
        // ════════════════════════════════════════════════════════════════════

        private static Paragraph BuildCoverParagraph(
            ExportOptions options,
            int totalRows, int rowsToWrite, int colCount, bool wasTruncated)
        {
            string subtitle = wasTruncated
                ? $"  |  Primeras {rowsToWrite:N0} de {totalRows:N0} filas" +
                  $"  |  {colCount} col." +
                  $"  |  ⚠ Truncado — usa Excel para el dataset completo"
                : $"  |  {rowsToWrite:N0} filas  |  {colCount} col.";

            if (options.IncludeTimestamp)
                subtitle += $"  |  {DateTime.Now:dd/MM/yyyy HH:mm}";

            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines
                    {
                        After = "160",
                        Line = "276",
                        LineRule = LineSpacingRuleValues.Auto
                    }),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize { Val = "28" },
                        new WdColor { Val = options.AccentColor }),
                    new Text(SanitiseText(options.Title))
                    { Space = SpaceProcessingModeValues.Preserve }),
                new Run(
                    new RunProperties(
                        new FontSize { Val = "18" },
                        new WdColor { Val = "888888" }),
                    new Text(subtitle)
                    { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static TableRow BuildHeaderRow(
            DataTable data, ExportOptions options, int colW)
        {
            var row = new TableRow(
                new TableRowProperties(
                    new TableHeader(),
                    new TableRowHeight
                    { Val = 400, HeightType = HeightRuleValues.AtLeast }));

            for (int c = 0; c < data.Columns.Count; c++)
                row.AppendChild(BuildCell(
                    data.Columns[c].ColumnName, colW,
                    options.HeaderBackground, options.HeaderForeground,
                    bold: true, fontSize: "18"));

            return row;
        }

        private static TableRow BuildDataRow(
            DataRow dataRow, ExportOptions options,
            int colW, int colCount, bool isEven)
        {
            var row = new TableRow(
                new TableRowProperties(
                    new TableRowHeight
                    { Val = 340, HeightType = HeightRuleValues.AtLeast }));

            string bg = isEven ? options.RowEvenBackground : options.RowOddBackground;

            for (int c = 0; c < colCount; c++)
                row.AppendChild(BuildCell(
                    dataRow[c]?.ToString() ?? string.Empty,
                    colW, bg, "1A1A2A",
                    bold: false, fontSize: "16"));

            return row;
        }

        private static TableCell BuildCell(
            string text, int colW,
            string bgHex, string fgHex,
            bool bold, string fontSize)
        {
            text = SanitiseText(text);

            var runProps = new RunProperties(
                new FontSize { Val = fontSize },
                new WdColor { Val = fgHex });

            if (bold) runProps.AppendChild(new Bold());

            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth
                    { Width = colW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading
                    { Val = ShadingPatternValues.Clear, Fill = bgHex, Color = "auto" },
                    new TableCellMargin(
                        new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        runProps,
                        new Text(text)
                        { Space = SpaceProcessingModeValues.Preserve })));
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static T MakeBorder<T>() where T : BorderType, new() =>
            new T { Val = BorderValues.Single, Size = 4, Space = 0, Color = "AABBCC" };

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