using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FileExplorerr.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  POWERPOINT EXPORTER  (Phase 4)
    //
    //  Generates .pptx files from a DataTable using DocumentFormat.OpenXml.
    //  No additional NuGet packages — DocumentFormat.OpenXml 2.20.0 is
    //  already present from Phase 1.
    //
    //  Slide layout:
    //    Slide 1  — Cover: title, row/column summary, timestamp
    //    Slides 2+ — Data: one table per slide, RowsPerSlide rows each
    //
    //  Limits (same philosophy as WordExporter — fail fast, stay clean):
    //    MaxRows     : 500   rows total across all slides
    //    MaxCols     : 20    columns
    //    RowsPerSlide: 18    rows per data slide (readable at 18pt)
    //
    //  Features:
    //    ✔ Arctic Night theme (dark cover, light data slides)
    //    ✔ Header row with #1A3A4A background on every data slide
    //    ✔ Alternating row shading (#E8F4F8 / #FFFFFF)
    //    ✔ Slide number in footer of every data slide
    //    ✔ Cooperative cancellation every slide
    //    ✔ Progress 0 → 100
    //    ✔ Partial file deleted on failure / cancellation
    // ════════════════════════════════════════════════════════════════════════
    public sealed class PowerPointExporter : IOfficeExporter
    {
        // ── Limits ────────────────────────────────────────────────────────
        private const int MaxRows = 500;
        private const int MaxCols = 20;
        private const int RowsPerSlide = 18;

        // ── Slide geometry in EMU (1 inch = 914 400 EMU) ──────────────────
        private const long SlideW = 9_144_000;  // 10 inches
        private const long SlideH = 6_858_000;  //  7.5 inches

        // Margins
        private const long MarginX = 457_200;   // 0.5 in
        private const long MarginY = 342_900;   // 0.375 in

        // Content area
        private const long ContentW = SlideW - MarginX * 2;
        private const long ContentH = SlideH - MarginY * 2;

        // ── IOfficeExporter ───────────────────────────────────────────────
        public string SupportedExtension => ".pptx";

        public async Task<ExportResult> ExportAsync(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress)
        {
            if (data is null) return ExportResult.Fail("DataTable es nulo.");
            if (options is null) return ExportResult.Fail("ExportOptions es nulo.");

            int rows = data.Rows.Count;
            int cols = data.Columns.Count;

            // ── Fail fast ─────────────────────────────────────────────────
            if (cols > MaxCols)
                return ExportResult.Fail(
                    $"El dataset tiene {cols} columnas.\n\n" +
                    $"PowerPoint soporta hasta {MaxCols} columnas de forma confiable.\n\n" +
                    "Exporta con Excel (.xlsx) para conservar todas las columnas.");

            int rowsToWrite = Math.Min(rows, MaxRows);
            bool wasTruncated = rows > MaxRows;

            TryDeletePartial(options.OutputPath);

            try
            {
                var result = await Task.Run(
                    () => BuildPresentation(data, options, progress, rowsToWrite, wasTruncated),
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
                return ExportResult.Fail(ex, $"Error al generar PowerPoint:\n{ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRESENTATION BUILDER  (thread-pool)
        // ════════════════════════════════════════════════════════════════════

        private ExportResult BuildPresentation(
            DataTable data,
            ExportOptions options,
            IProgress<int>? progress,
            int rowsToWrite,
            bool wasTruncated)
        {
            var ct = options.CancellationToken;
            int colCount = data.Columns.Count;
            int totalRows = data.Rows.Count;
            int slideCount = (int)Math.Ceiling((double)rowsToWrite / RowsPerSlide);

            string? dir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            progress?.Report(5);

            using var pptDoc = PresentationDocument.Create(
                options.OutputPath,
                PresentationDocumentType.Presentation);

            // ── Package structure ─────────────────────────────────────────
            var presentationPart = pptDoc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(),
                new P.SlideIdList(),
                new P.SlideSize { Cx = (Int32Value)SlideW, Cy = (Int32Value)SlideH },
                new P.NotesSize { Cx = 6_858_000, Cy = 9_144_000 });

            // Add a minimal slide master (required by the spec)
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var master = BuildSlideMaster();
            masterPart.SlideMaster = master;

            // ThemePart — required by PowerPoint; without it the file
            // triggers "repair" on open.
            var themePart = masterPart.AddNewPart<ThemePart>();
            themePart.Theme = BuildTheme();

            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = BuildSlideLayout();
            master.SlideLayoutIdList = new P.SlideLayoutIdList(
                new P.SlideLayoutId
                {
                    Id = 2049,
                    RelationshipId = masterPart.GetIdOfPart(layoutPart)
                });
            masterPart.SlideMaster.Save();

            presentationPart.Presentation.SlideMasterIdList!.AppendChild(
                new P.SlideMasterId
                {
                    Id = 2048,
                    RelationshipId = presentationPart.GetIdOfPart(masterPart)
                });

            progress?.Report(10);

            // ── Slide 1: Cover ────────────────────────────────────────────
            var coverPart = AddSlidePartToPresentation(presentationPart, layoutPart);
            coverPart.Slide = BuildCoverSlide(
                options, totalRows, rowsToWrite, colCount, wasTruncated);
            coverPart.Slide.Save();

            progress?.Report(15);
            ct.ThrowIfCancellationRequested();

            // ── Slides 2+: Data ───────────────────────────────────────────
            for (int s = 0; s < slideCount; s++)
            {
                ct.ThrowIfCancellationRequested();

                int startRow = s * RowsPerSlide;
                int endRow = Math.Min(startRow + RowsPerSlide, rowsToWrite);

                var slidePart = AddSlidePartToPresentation(presentationPart, layoutPart);
                slidePart.Slide = BuildDataSlide(
                    data, options, colCount, startRow, endRow,
                    slideNum: s + 1, totalSlides: slideCount);
                slidePart.Slide.Save();

                progress?.Report(15 + (int)(80.0 * (s + 1) / slideCount));
            }

            // ── ViewProperties — prevents "repair on open" warning ────────
            var viewPropsPart = presentationPart.AddNewPart<ViewPropertiesPart>();
            viewPropsPart.ViewProperties = new P.ViewProperties(
                new P.NormalViewProperties(
                    new P.RestoredLeft { Size = 15620 },
                    new P.RestoredTop { Size = 94660 }),
                new P.SlideViewProperties(
                    new P.CommonSlideViewProperties(
                        new P.CommonViewProperties(
                            new P.ScaleFactor(
                                new A.ScaleX { Numerator = 1, Denominator = 1 },
                                new A.ScaleY { Numerator = 1, Denominator = 1 }),
                            new P.Origin { X = 0, Y = 0 }))
                    { SnapToGrid = true }));
            viewPropsPart.ViewProperties.Save();

            // ── PresentationProperties — required part, prevents repair warning ──
            var presProps = presentationPart.AddNewPart<PresentationPropertiesPart>();
            presProps.PresentationProperties = new P.PresentationProperties();
            presProps.PresentationProperties.Save();

            // ── Document properties ───────────────────────────────────────
            presentationPart.Presentation.Save();
            pptDoc.PackageProperties.Title = options.Title;
            pptDoc.PackageProperties.Creator = options.Author;
            pptDoc.PackageProperties.Created = DateTime.UtcNow;

            // using → Dispose → flush zip → file complete on disk

            progress?.Report(100);

            return ExportResult.Ok(options.OutputPath, rowsToWrite, wasTruncated);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SLIDE REGISTRATION HELPER
        // ════════════════════════════════════════════════════════════════════

        private static SlidePart AddSlidePartToPresentation(
            PresentationPart pressPart,
            SlideLayoutPart layoutPart)
        {
            var slidePart = pressPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layoutPart);

            var slideIdList = pressPart.Presentation.SlideIdList!;
            uint maxId = slideIdList.Elements<P.SlideId>().Any()
                ? slideIdList.Elements<P.SlideId>().Max(s => s.Id!.Value)
                : 255;

            slideIdList.AppendChild(new P.SlideId
            {
                Id = maxId + 1,
                RelationshipId = pressPart.GetIdOfPart(slidePart)
            });

            return slidePart;
        }

        // ════════════════════════════════════════════════════════════════════
        //  COVER SLIDE
        // ════════════════════════════════════════════════════════════════════

        private static P.Slide BuildCoverSlide(
            ExportOptions options,
            int totalRows, int rowsToWrite, int colCount, bool wasTruncated)
        {
            string subtitle = wasTruncated
                ? $"{rowsToWrite:N0} de {totalRows:N0} filas  ·  {colCount} columnas" +
                  $"  ·  ⚠ Truncado — usa Excel para el dataset completo"
                : $"{rowsToWrite:N0} filas  ·  {colCount} columnas";

            if (options.IncludeTimestamp)
                subtitle += $"  ·  {DateTime.Now:dd/MM/yyyy HH:mm}";

            long titleY = MarginY;
            long titleH = 1_600_000;
            long subtitleY = titleY + titleH + 200_000;
            long subtitleH = 600_000;

            var slide = new P.Slide(
                new P.CommonSlideData(
                    new P.Background(
                        new P.BackgroundProperties(
                            MakeSolidFill("1A3A4A"))),
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        // Title
                        MakeTextBox(2, "title",
                            MarginX, titleY, ContentW, titleH,
                            SanitiseText(options.Title),
                            fontSize: 4000,
                            bold: true,
                            colorHex: options.AccentColor,
                            align: A.TextAlignmentTypeValues.Left),
                        // Subtitle
                        MakeTextBox(3, "subtitle",
                            MarginX, subtitleY, ContentW, subtitleH,
                            SanitiseText(subtitle),
                            fontSize: 1800,
                            bold: false,
                            colorHex: "AACCDD",
                            align: A.TextAlignmentTypeValues.Left))),
                new P.ColorMapOverride(new A.MasterColorMapping()));

            return slide;
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA SLIDE
        // ════════════════════════════════════════════════════════════════════

        private static P.Slide BuildDataSlide(
            DataTable data,
            ExportOptions options,
            int colCount,
            int startRow,
            int endRow,
            int slideNum,
            int totalSlides)
        {
            // Slide header label (top-left, small)
            string slideLabel =
                $"{options.Title}  ·  filas {startRow + 1}–{endRow}" +
                $"  ({slideNum}/{totalSlides})";

            long labelH = 380_000;
            long tableY = MarginY + labelH + 150_000;
            long tableH = ContentH - labelH - 150_000;

            // Column width: distribute evenly
            long colW = colCount > 0 ? ContentW / colCount : ContentW;

            // Row height: distribute table height among (dataRows + 1 header)
            int visibleRows = endRow - startRow + 1; // +1 for header
            long rowH = tableH / Math.Max(visibleRows, 1);
            rowH = Math.Max(rowH, 300_000); // min 0.33 in

            var shapeTree = new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup()),
                // Slide label
                MakeTextBox(2, "label",
                    MarginX, MarginY, ContentW, labelH,
                    SanitiseText(slideLabel),
                    fontSize: 1200,
                    bold: false,
                    colorHex: "48CAB4",
                    align: A.TextAlignmentTypeValues.Left));

            // Table graphic frame
            shapeTree.AppendChild(BuildTableFrame(
                data, options, colCount, startRow, endRow,
                MarginX, tableY, ContentW, rowH, colW,
                frameId: 3));

            return new P.Slide(
                new P.CommonSlideData(
                    new P.Background(
                        new P.BackgroundProperties(
                            MakeSolidFill("F0F2FF"))),
                    shapeTree),
                new P.ColorMapOverride(new A.MasterColorMapping()));
        }

        // ════════════════════════════════════════════════════════════════════
        //  TABLE GRAPHIC FRAME
        // ════════════════════════════════════════════════════════════════════

        private static P.GraphicFrame BuildTableFrame(
            DataTable data,
            ExportOptions options,
            int colCount,
            int startRow,
            int endRow,
            long x, long y, long w, long rowH, long colW,
            uint frameId)
        {
            int dataRowCount = endRow - startRow;
            int totalRows = dataRowCount + 1; // +1 header
            long tableH = rowH * totalRows;

            // Build the A.Table
            var table = new A.Table();

            // Table properties
            table.AppendChild(new A.TableProperties
            {
                FirstRow = true,
                BandRow = true
            });

            // Column widths
            var tblGrid = new A.TableGrid();
            for (int c = 0; c < colCount; c++)
                tblGrid.AppendChild(new A.GridColumn { Width = colW });
            table.AppendChild(tblGrid);

            // Header row
            table.AppendChild(BuildTableRow(
                Enumerable.Range(0, colCount)
                          .Select(c => data.Columns[c].ColumnName)
                          .ToArray(),
                rowH, colW,
                bgHex: options.HeaderBackground,
                fgHex: options.HeaderForeground,
                bold: true,
                fontSize: 1100));

            // Data rows
            for (int r = startRow; r < endRow; r++)
            {
                bool isEven = (r - startRow) % 2 == 0;
                string bg = isEven ? options.RowEvenBackground : options.RowOddBackground;

                var cells = Enumerable.Range(0, colCount)
                    .Select(c => SanitiseText(data.Rows[r][c]?.ToString() ?? string.Empty))
                    .ToArray();

                table.AppendChild(BuildTableRow(
                    cells, rowH, colW,
                    bgHex: bg,
                    fgHex: "1A1A2A",
                    bold: false,
                    fontSize: 1000));
            }

            // Wrap in GraphicFrame
            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(
                    new P.NonVisualDrawingProperties
                    { Id = frameId, Name = $"Table {frameId}" },
                    new P.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties
                    { UserDrawn = true }),
                new P.Transform(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = w, Cy = tableH }),
                new A.Graphic(
                    new A.GraphicData(table)
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/table"
                    }));
        }

        // ── Table row ─────────────────────────────────────────────────────

        private static A.TableRow BuildTableRow(
            string[] cells,
            long rowH,
            long colW,
            string bgHex,
            string fgHex,
            bool bold,
            int fontSize)
        {
            var row = new A.TableRow { Height = rowH };

            foreach (string text in cells)
            {
                var tc = new A.TableCell();

                // Cell fill
                var fill = new A.TableCellProperties(
                    new A.SolidFill(
                        new A.RgbColorModelHex { Val = bgHex }),
                    new A.LeftBorderLineProperties(
                        new A.SolidFill(
                            new A.RgbColorModelHex { Val = "AABBCC" }))
                    { Width = 9525 },
                    new A.RightBorderLineProperties(
                        new A.SolidFill(
                            new A.RgbColorModelHex { Val = "AABBCC" }))
                    { Width = 9525 },
                    new A.TopBorderLineProperties(
                        new A.SolidFill(
                            new A.RgbColorModelHex { Val = "AABBCC" }))
                    { Width = 9525 },
                    new A.BottomBorderLineProperties(
                        new A.SolidFill(
                            new A.RgbColorModelHex { Val = "AABBCC" }))
                    { Width = 9525 });

                // Cell text
                var runProps = new A.RunProperties
                {
                    Language = "es-MX",
                    FontSize = fontSize,
                    Bold = bold,
                    Dirty = false
                };
                runProps.AppendChild(
                    new A.SolidFill(
                        new A.RgbColorModelHex { Val = fgHex }));

                var para = new A.Paragraph(
                    new A.ParagraphProperties
                    { Alignment = A.TextAlignmentTypeValues.Left },
                    new A.Run(runProps, new A.Text(text)));

                var txBody = new A.TextBody(
                    new A.BodyProperties
                    {
                        Anchor = A.TextAnchoringTypeValues.Center,
                        LeftInset = 91440,
                        RightInset = 91440,
                        TopInset = 45720,
                        BottomInset = 45720
                    },
                    new A.ListStyle(),
                    para);

                tc.AppendChild(txBody);
                tc.AppendChild(fill);
                row.AppendChild(tc);
            }

            return row;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TEXT BOX SHAPE
        // ════════════════════════════════════════════════════════════════════

        private static P.Shape MakeTextBox(
            uint id,
            string name,
            long x, long y, long cx, long cy,
            string text,
            int fontSize,
            bool bold,
            string colorHex,
            A.TextAlignmentTypeValues align)
        {
            var runProps = new A.RunProperties
            {
                Language = "es-MX",
                FontSize = fontSize,
                Bold = bold,
                Dirty = false
            };
            runProps.AppendChild(
                new A.SolidFill(
                    new A.RgbColorModelHex { Val = colorHex }));

            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualShapeDrawingProperties(
                        new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = x, Y = y },
                        new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList())
                    { Preset = A.ShapeTypeValues.Rectangle },
                    new A.NoFill()),
                new P.TextBody(
                    new A.BodyProperties
                    {
                        Wrap = A.TextWrappingValues.Square,
                        LeftInset = 0,
                        RightInset = 0,
                        TopInset = 0,
                        BottomInset = 0,
                        Anchor = A.TextAnchoringTypeValues.Center
                    },
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.ParagraphProperties { Alignment = align },
                        new A.Run(runProps, new A.Text(text)))));
        }

        // ════════════════════════════════════════════════════════════════════
        //  MINIMAL SLIDE MASTER + LAYOUT  (required by OOXML spec)
        // ════════════════════════════════════════════════════════════════════

        private static P.SlideMaster BuildSlideMaster() =>
            new P.SlideMaster(
                new P.CommonSlideData(
                    new P.Background(
                        new P.BackgroundProperties(
                            MakeSolidFill("FFFFFF"))),
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()))),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1,
                    Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2,
                    Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1,
                    Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3,
                    Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5,
                    Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                },
                // TextStyles is required by the OOXML spec on SlideMaster
                new P.TextStyles(
                    new P.TitleStyle(),
                    new P.BodyStyle(),
                    new P.OtherStyle()));

        /// <summary>
        /// Minimal theme required by every SlideMasterPart.
        /// Without a ThemePart PowerPoint flags the file for repair on open.
        /// </summary>
        private static A.Theme BuildTheme() =>
            new A.Theme(
                new A.ThemeElements(
                    new A.ColorScheme(
                        new A.Dark1Color(new A.SystemColor
                        { LastColor = "000000", Val = A.SystemColorValues.WindowText }),
                        new A.Light1Color(new A.SystemColor
                        { LastColor = "FFFFFF", Val = A.SystemColorValues.Window }),
                        new A.Dark2Color(new A.RgbColorModelHex { Val = "1A3A4A" }),
                        new A.Light2Color(new A.RgbColorModelHex { Val = "F0F2FF" }),
                        new A.Accent1Color(new A.RgbColorModelHex { Val = "48CAB4" }),
                        new A.Accent2Color(new A.RgbColorModelHex { Val = "7C6FF7" }),
                        new A.Accent3Color(new A.RgbColorModelHex { Val = "60A5FA" }),
                        new A.Accent4Color(new A.RgbColorModelHex { Val = "F87171" }),
                        new A.Accent5Color(new A.RgbColorModelHex { Val = "FBBF24" }),
                        new A.Accent6Color(new A.RgbColorModelHex { Val = "F472B6" }),
                        new A.Hyperlink(new A.RgbColorModelHex { Val = "48CAB4" }),
                        new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "7C6FF7" }))
                    { Name = "ArcticNight" },
                    new A.FontScheme(
                        new A.MajorFont(
                            new A.LatinFont { Typeface = "Calibri" },
                            new A.EastAsianFont { Typeface = "" },
                            new A.ComplexScriptFont { Typeface = "" }),
                        new A.MinorFont(
                            new A.LatinFont { Typeface = "Calibri" },
                            new A.EastAsianFont { Typeface = "" },
                            new A.ComplexScriptFont { Typeface = "" }))
                    { Name = "ArcticNight" },
                    new A.FormatScheme(
                        new A.FillStyleList(
                            new A.NoFill(),
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                            new A.GradientFill(new A.GradientStopList())),
                        new A.LineStyleList(
                            new A.Outline(
                                new A.SolidFill(
                                    new A.SchemeColor { Val = A.SchemeColorValues.PhColor }))
                            { Width = 6350 },
                            new A.Outline() { Width = 12700 },
                            new A.Outline() { Width = 19050 }),
                        new A.EffectStyleList(
                            new A.EffectStyle(new A.EffectList()),
                            new A.EffectStyle(new A.EffectList()),
                            new A.EffectStyle(new A.EffectList())),
                        new A.BackgroundFillStyleList(
                            new A.NoFill(),
                            new A.NoFill(),
                            new A.SolidFill(
                                new A.SchemeColor { Val = A.SchemeColorValues.PhColor })))
                    { Name = "ArcticNight" }),
                new A.ObjectDefaults(),
                new A.ExtraColorSchemeList())
            { Name = "ArcticNight" };

        private static P.SlideLayout BuildSlideLayout() =>
            new P.SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()))),
                new P.ColorMapOverride(new A.MasterColorMapping()))
            { Type = P.SlideLayoutValues.Blank, Preserve = true };

        // ════════════════════════════════════════════════════════════════════
        //  DRAWING HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static A.SolidFill MakeSolidFill(string hex) =>
            new A.SolidFill(new A.RgbColorModelHex { Val = hex });

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

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