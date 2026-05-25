// ============================================================================
//  ExportadorOffice.cs
//  NuGet:
//    <PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />
//    <PackageReference Include="PdfSharp"               Version="6.1.0" />
// ============================================================================

using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using OXml = DocumentFormat.OpenXml;
using Pkg = DocumentFormat.OpenXml.Packaging;
using XL = DocumentFormat.OpenXml.Spreadsheet;
using WD = DocumentFormat.OpenXml.Wordprocessing;
using PX = DocumentFormat.OpenXml.Presentation;
using DX = DocumentFormat.OpenXml.Drawing;

using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace FileExplorerr
{
    public static class ExportadorOffice
    {
        // ════════════════════════════════════════════════════════════════════
        //  PUNTO DE ENTRADA
        // ════════════════════════════════════════════════════════════════════
        public static bool ExportarConDialogo(DataTable? dt,
            string titulo, string ext, IWin32Window? owner = null)
        {
            if (dt == null || dt.Rows.Count == 0)
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
                FileName = $"{NombreSeguro(titulo)}_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

            try
            {
                switch (ext)
                {
                    case ".xlsx": ExportarExcel(dt, titulo, dlg.FileName); break;
                    case ".docx": ExportarWord(dt, titulo, dlg.FileName); break;
                    case ".pptx": ExportarPowerPoint(dt, titulo, dlg.FileName); break;
                    case ".pdf": ExportarPdf(dt, titulo, dlg.FileName); break;
                }

                if (MessageBox.Show($"{extU} generado:\n{dlg.FileName}\n\n¿Abrir?",
                        "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                    == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        { FileName = dlg.FileName, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXCEL
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarExcel(DataTable dt, string titulo, string ruta)
        {
            using var doc = Pkg.SpreadsheetDocument.Create(
                ruta, OXml.SpreadsheetDocumentType.Workbook);

            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new XL.Workbook();

            var ssPart = wbPart.AddNewPart<Pkg.SharedStringTablePart>();
            ssPart.SharedStringTable = new XL.SharedStringTable();

            var stylesPart = wbPart.AddNewPart<Pkg.WorkbookStylesPart>();
            stylesPart.Stylesheet = CrearEstilos();
            stylesPart.Stylesheet.Save();

            var wsPart = wbPart.AddNewPart<Pkg.WorksheetPart>();
            var sheetData = new XL.SheetData();

            var sheets = wbPart.Workbook.AppendChild(new XL.Sheets());
            sheets.AppendChild(new XL.Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = HojaNombre(titulo)
            });

            var cols = new XL.Columns();
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                int maxLen = dt.Columns[c].ColumnName.Length;
                foreach (DataRow r in dt.Rows)
                    maxLen = Math.Max(maxLen, (r[c]?.ToString() ?? "").Length);
                cols.AppendChild(new XL.Column
                {
                    Min = (uint)(c + 1),
                    Max = (uint)(c + 1),
                    Width = Math.Min(Math.Max(maxLen + 2, 8), 45),
                    CustomWidth = true
                });
            }

            var ws = new XL.Worksheet();
            ws.AppendChild(cols);
            ws.AppendChild(sheetData);
            wsPart.Worksheet = ws;

            int ssIdx = 0;
            int AddSS(string texto)
            {
                ssPart.SharedStringTable.AppendChild(
                    new XL.SharedStringItem(new XL.Text(texto)));
                return ssIdx++;
            }

            var hRow = new XL.Row { RowIndex = 1 };
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                hRow.AppendChild(new XL.Cell
                {
                    CellReference = LetraCol(c) + "1",
                    DataType = XL.CellValues.SharedString,
                    CellValue = new XL.CellValue(AddSS(dt.Columns[c].ColumnName).ToString()),
                    StyleIndex = 1u
                });
            }
            sheetData.AppendChild(hRow);

            for (int r = 0; r < dt.Rows.Count; r++)
            {
                uint rowIdx = (uint)(r + 2);
                uint style = (uint)(r % 2 == 0 ? 2 : 3);
                var dRow = new XL.Row { RowIndex = rowIdx };
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    string val = dt.Rows[r][c]?.ToString() ?? "";
                    string cRef = LetraCol(c) + rowIdx;
                    bool esNum = double.TryParse(val,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double numVal);

                    dRow.AppendChild(esNum
                        ? new XL.Cell
                        {
                            CellReference = cRef,
                            CellValue = new XL.CellValue(numVal.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)),
                            StyleIndex = style
                        }
                        : new XL.Cell
                        {
                            CellReference = cRef,
                            DataType = XL.CellValues.SharedString,
                            CellValue = new XL.CellValue(AddSS(val).ToString()),
                            StyleIndex = style
                        });
                }
                sheetData.AppendChild(dRow);
            }

            wsPart.Worksheet.AppendChild(new XL.AutoFilter
            { Reference = $"A1:{LetraCol(dt.Columns.Count - 1)}1" });

            ssPart.SharedStringTable.Save();
            wsPart.Worksheet.Save();
            wbPart.Workbook.Save();
        }

        private static XL.Stylesheet CrearEstilos()
        {
            var fonts = new XL.Fonts(
                new XL.Font(new XL.FontName { Val = "Calibri" }, new XL.FontSize { Val = 11 }),
                new XL.Font(new XL.Bold(), new XL.FontName { Val = "Calibri" },
                    new XL.FontSize { Val = 11 }, new XL.Color { Rgb = "FFFFFFFF" })
            )
            { Count = 2 };
            var fills = new XL.Fills(
                new XL.Fill(new XL.PatternFill { PatternType = XL.PatternValues.None }),
                new XL.Fill(new XL.PatternFill { PatternType = XL.PatternValues.Gray125 }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FF1A3A4A" },
                    BackgroundColor = new XL.BackgroundColor { Indexed = 64 }
                }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FFE8F4F8" },
                    BackgroundColor = new XL.BackgroundColor { Indexed = 64 }
                }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FFFFFFFF" },
                    BackgroundColor = new XL.BackgroundColor { Indexed = 64 }
                })
            )
            { Count = 5 };
            var borders = new XL.Borders(
                new XL.Border(),
                new XL.Border(
                    new XL.LeftBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.RightBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.TopBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.BottomBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin })
            )
            { Count = 2 };
            var csf = new XL.CellStyleFormats(new XL.CellFormat()) { Count = 1 };
            var cf = new XL.CellFormats(
                new XL.CellFormat { FontId = 0, FillId = 0, BorderId = 0 },
                new XL.CellFormat
                {
                    FontId = 1,
                    FillId = 2,
                    BorderId = 1,
                    ApplyFont = true,
                    ApplyFill = true,
                    ApplyBorder = true,
                    Alignment = new XL.Alignment { Horizontal = XL.HorizontalAlignmentValues.Center }
                },
                new XL.CellFormat { FontId = 0, FillId = 3, BorderId = 1, ApplyFill = true, ApplyBorder = true },
                new XL.CellFormat { FontId = 0, FillId = 4, BorderId = 1, ApplyFill = true, ApplyBorder = true }
            )
            { Count = 4 };
            return new XL.Stylesheet(fonts, fills, borders, csf, cf);
        }

        // ════════════════════════════════════════════════════════════════════
        //  WORD
        //  Estrategia: construir el XML del body como string puro y cargarlo
        //  con XDocument para garantizar un OOXML válido que Word acepte.
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarWord(DataTable dt, string titulo, string ruta)
        {
            using var doc = Pkg.WordprocessingDocument.Create(
                ruta, OXml.WordprocessingDocumentType.Document);

            var main = doc.AddMainDocumentPart();

            // Settings obligatorio
            var sp = main.AddNewPart<Pkg.DocumentSettingsPart>();
            sp.Settings = new WD.Settings(
                new WD.Compatibility(new WD.CompatibilitySetting
                {
                    Name = WD.CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15"
                }));
            sp.Settings.Save();

            var body = new WD.Body();

            // ── Título ───────────────────────────────────────────────────────
            body.AppendChild(new WD.Paragraph(
                new WD.ParagraphProperties(
                    new WD.Justification { Val = WD.JustificationValues.Center },
                    new WD.ParagraphMarkRunProperties(
                        new WD.Bold(), new WD.FontSize { Val = "40" },
                        new WD.Color { Val = "1A3A4A" }),
                    new WD.SpacingBetweenLines { Before = "0", After = "200" }),
                new WD.Run(
                    new WD.RunProperties(
                        new WD.Bold(), new WD.FontSize { Val = "40" },
                        new WD.Color { Val = "1A3A4A" }),
                    new WD.Text(titulo))));

            // ── Subtítulo ────────────────────────────────────────────────────
            body.AppendChild(new WD.Paragraph(
                new WD.ParagraphProperties(
                    new WD.Justification { Val = WD.JustificationValues.Center },
                    new WD.SpacingBetweenLines { Before = "0", After = "300" }),
                new WD.Run(
                    new WD.RunProperties(
                        new WD.FontSize { Val = "20" },
                        new WD.Color { Val = "48CAB4" }),
                    new WD.Text(
                        $"{dt.Rows.Count:N0} registros  ·  {dt.Columns.Count} columnas  ·  {DateTime.Now:dd/MM/yyyy HH:mm}"))));

            // ── Tabla ────────────────────────────────────────────────────────
            // Calcular anchos proporcionales en twips (total ~9000 twips para márgenes normales)
            int totalCols = dt.Columns.Count;
            // Ancho en twips por columna (distribuido uniformemente)
            int anchoCelda = Math.Max(800, 9000 / Math.Max(1, totalCols));

            var tbl = new WD.Table();

            tbl.AppendChild(new WD.TableProperties(
                new WD.TableWidth { Width = (anchoCelda * totalCols).ToString(), Type = WD.TableWidthUnitValues.Dxa },
                new WD.TableLayout { Type = WD.TableLayoutValues.Fixed },
                new WD.TableBorders(
                    new WD.TopBorder { Val = WD.BorderValues.Single, Size = 4, Color = "1A3A4A" },
                    new WD.BottomBorder { Val = WD.BorderValues.Single, Size = 4, Color = "1A3A4A" },
                    new WD.LeftBorder { Val = WD.BorderValues.Single, Size = 4, Color = "1A3A4A" },
                    new WD.RightBorder { Val = WD.BorderValues.Single, Size = 4, Color = "1A3A4A" },
                    new WD.InsideHorizontalBorder { Val = WD.BorderValues.Single, Size = 4, Color = "AABBCC" },
                    new WD.InsideVerticalBorder { Val = WD.BorderValues.Single, Size = 4, Color = "AABBCC" })));

            // Encabezado
            var hRow = new WD.TableRow();
            foreach (DataColumn col in dt.Columns)
                hRow.AppendChild(CeldaWord(col.ColumnName, anchoCelda, "1A3A4A", "FFFFFF", bold: true));
            tbl.AppendChild(hRow);

            // Datos
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                string fondo = r % 2 == 0 ? "E8F4F8" : "FFFFFF";
                var dRow = new WD.TableRow();
                foreach (DataColumn col in dt.Columns)
                    dRow.AppendChild(CeldaWord(dt.Rows[r][col]?.ToString() ?? "",
                        anchoCelda, fondo, "1A1A2A", bold: false));
                tbl.AppendChild(dRow);
            }

            body.AppendChild(tbl);

            // Sección: orientación landscape si hay muchas columnas
            var sectPr = new WD.SectionProperties();
            if (totalCols > 6)
            {
                sectPr.AppendChild(new WD.PageSize
                {
                    Width = 15840,  // A4 landscape
                    Height = 12240,
                    Orient = WD.PageOrientationValues.Landscape
                });
                sectPr.AppendChild(new WD.PageMargin
                { Top = 720, Bottom = 720, Left = 720, Right = 720 });
            }
            else
            {
                sectPr.AppendChild(new WD.PageMargin
                { Top = 900, Bottom = 900, Left = 1080, Right = 1080 });
            }
            body.AppendChild(sectPr);

            main.Document = new WD.Document(body);
            main.Document.Save();
        }

        private static WD.TableCell CeldaWord(string texto, int anchoTwips,
            string fondoHex, string textoHex, bool bold)
        {
            var tcp = new WD.TableCellProperties(
                new WD.TableCellWidth { Width = anchoTwips.ToString(), Type = WD.TableWidthUnitValues.Dxa },
                new WD.Shading { Fill = fondoHex, Val = WD.ShadingPatternValues.Clear, Color = "auto" });

            var rp = new WD.RunProperties(
                new WD.Color { Val = textoHex },
                new WD.FontSize { Val = "18" });
            if (bold) rp.AppendChild(new WD.Bold());

            var cell = new WD.TableCell();
            cell.AppendChild(tcp);
            cell.AppendChild(new WD.Paragraph(
                new WD.ParagraphProperties(
                    new WD.SpacingBetweenLines { Before = "0", After = "0" },
                    new WD.Indentation { Left = "60", Right = "60" }),
                new WD.Run(rp,
                    new WD.Text(texto)
                    { Space = OXml.SpaceProcessingModeValues.Preserve })));
            return cell;
        }

        // ════════════════════════════════════════════════════════════════════
        //  POWERPOINT
        //  Enfoque: XML puro inyectado en las partes para garantizar
        //  compatibilidad con Office y LibreOffice.
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarPowerPoint(DataTable dt, string titulo, string ruta)
        {
            using var pres = Pkg.PresentationDocument.Create(
                ruta, OXml.PresentationDocumentType.Presentation);

            var presPart = pres.AddPresentationPart();

            // ── Tema ─────────────────────────────────────────────────────────
            var masterPart = presPart.AddNewPart<Pkg.SlideMasterPart>("rIdMaster");
            var themePart = masterPart.AddNewPart<Pkg.ThemePart>("rIdTheme");
            themePart.Theme = TemaMinimo();
            themePart.Theme.Save();

            // ── SlideLayout mínimo ────────────────────────────────────────────
            var layoutPart = masterPart.AddNewPart<Pkg.SlideLayoutPart>("rIdLayout");
            layoutPart.SlideLayout = new PX.SlideLayout(
                new PX.CommonSlideData(
                    new PX.ShapeTree(NvGrp(), GrpSp())),
                new PX.ColorMapOverride(new DX.MasterColorMapping()))
            { Type = PX.SlideLayoutValues.Blank, Preserve = true };
            layoutPart.SlideLayout.Save();

            // ── SlideMaster mínimo ────────────────────────────────────────────
            masterPart.SlideMaster = new PX.SlideMaster(
                new PX.CommonSlideData(
                    new PX.ShapeTree(NvGrp(), GrpSp())),
                new PX.ColorMap
                {
                    Background1 = DX.ColorSchemeIndexValues.Light1,
                    Text1 = DX.ColorSchemeIndexValues.Dark1,
                    Background2 = DX.ColorSchemeIndexValues.Light2,
                    Text2 = DX.ColorSchemeIndexValues.Dark2,
                    Accent1 = DX.ColorSchemeIndexValues.Accent1,
                    Accent2 = DX.ColorSchemeIndexValues.Accent2,
                    Accent3 = DX.ColorSchemeIndexValues.Accent3,
                    Accent4 = DX.ColorSchemeIndexValues.Accent4,
                    Accent5 = DX.ColorSchemeIndexValues.Accent5,
                    Accent6 = DX.ColorSchemeIndexValues.Accent6,
                    Hyperlink = DX.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = DX.ColorSchemeIndexValues.FollowedHyperlink
                },
                new PX.SlideLayoutIdList(
                    new PX.SlideLayoutId { Id = 2049u, RelationshipId = "rIdLayout" }));
            masterPart.SlideMaster.Save();

            // ── Presentation ──────────────────────────────────────────────────
            presPart.Presentation = new PX.Presentation(
                new PX.SlideSize { Cx = 12192000, Cy = 6858000, Type = PX.SlideSizeValues.Screen16x9 },
                new PX.SlideMasterIdList(
                    new PX.SlideMasterId { Id = 2048u, RelationshipId = "rIdMaster" }),
                new PX.SlideIdList());

            uint sid = 256;

            // ── Portada ───────────────────────────────────────────────────────
            var spPortada = presPart.AddNewPart<Pkg.SlidePart>("rIdSlide0");
            spPortada.AddPart(layoutPart, "rIdLayout");
            spPortada.Slide = SlidePortada(titulo, dt.Columns.Count, dt.Rows.Count);
            spPortada.Slide.Save();
            presPart.Presentation.SlideIdList!.AppendChild(
                new PX.SlideId { Id = sid++, RelationshipId = "rIdSlide0" });

            // ── Slides de datos ───────────────────────────────────────────────
            const int FPP = 18;
            int total = Math.Max(1, Math.Min(60,
                (int)Math.Ceiling(dt.Rows.Count / (double)FPP)));

            for (int s = 0; s < total; s++)
            {
                int ini = s * FPP;
                int fin = Math.Min(ini + FPP, dt.Rows.Count);
                string rid = $"rIdSlide{s + 1}";

                var sp2 = presPart.AddNewPart<Pkg.SlidePart>(rid);
                sp2.AddPart(layoutPart, "rIdLayout");
                sp2.Slide = SlideDatos(dt, titulo, ini, fin, s + 1, total);
                sp2.Slide.Save();
                presPart.Presentation.SlideIdList.AppendChild(
                    new PX.SlideId { Id = sid++, RelationshipId = rid });
            }

            presPart.Presentation.Save();
        }

        // helpers PowerPoint
        private static PX.NonVisualGroupShapeProperties NvGrp() =>
            new PX.NonVisualGroupShapeProperties(
                new PX.NonVisualDrawingProperties { Id = 1u, Name = "" },
                new PX.NonVisualGroupShapeDrawingProperties(),
                new PX.ApplicationNonVisualDrawingProperties());

        private static PX.GroupShapeProperties GrpSp() =>
            new PX.GroupShapeProperties(new DX.TransformGroup(
                new DX.Offset { X = 0, Y = 0 },
                new DX.Extents { Cx = 0, Cy = 0 },
                new DX.ChildOffset { X = 0, Y = 0 },
                new DX.ChildExtents { Cx = 0, Cy = 0 }));

        private static PX.Slide SlidePortada(string titulo, int cols, int filas)
        {
            return new PX.Slide(
                new PX.CommonSlideData(
                    new PX.Background(new PX.BackgroundProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = "1A3A4A" }))),
                    new PX.ShapeTree(
                        NvGrp(), GrpSp(),
                        Forma(2, titulo, 914400, 2000000, 10363200, 1500000,
                            4800, true, "FFFFFF", DX.TextAlignmentTypeValues.Center),
                        Forma(3,
                            $"{filas:N0} registros  ·  {cols} columnas\n{DateTime.Now:dd/MM/yyyy HH:mm}",
                            914400, 3700000, 10363200, 900000,
                            2000, false, "48CAB4", DX.TextAlignmentTypeValues.Center))),
                new PX.ColorMapOverride(new DX.MasterColorMapping()));
        }

        private static PX.Slide SlideDatos(DataTable dt, string titulo,
            int ini, int fin, int num, int total)
        {
            int maxCols = Math.Min(dt.Columns.Count, 10);
            long cw = 11582400L / maxCols;

            var tblGrid = new DX.TableGrid();
            for (int c = 0; c < maxCols; c++)
                tblGrid.AppendChild(new DX.GridColumn { Width = cw });

            var tbl = new DX.Table();
            tbl.AppendChild(new DX.TableProperties(
                new DX.TableStyleId
                { Text = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}" }));
            tbl.AppendChild(tblGrid);

            // Encabezado
            var hRow = new DX.TableRow { Height = 400000L };
            for (int c = 0; c < maxCols; c++)
                hRow.AppendChild(CeldaPptx(
                    c < dt.Columns.Count ? dt.Columns[c].ColumnName : "",
                    "1A3A4A", "FFFFFF", bold: true, fs: 1100));
            tbl.AppendChild(hRow);

            // Datos
            for (int r = ini; r < fin; r++)
            {
                string bg = r % 2 == 0 ? "1E3040" : "263545";
                var dRow = new DX.TableRow { Height = 300000L };
                for (int c = 0; c < maxCols; c++)
                    dRow.AppendChild(CeldaPptx(
                        c < dt.Columns.Count ? (dt.Rows[r][c]?.ToString() ?? "") : "",
                        bg, "D8E8F0", bold: false, fs: 900));
                tbl.AppendChild(dRow);
            }

            var gf = new PX.GraphicFrame(
                new PX.NonVisualGraphicFrameProperties(
                    new PX.NonVisualDrawingProperties { Id = 10u, Name = $"T{num}" },
                    new PX.NonVisualGraphicFrameDrawingProperties(
                        new DX.GraphicFrameLocks { NoGrouping = true }),
                    new PX.ApplicationNonVisualDrawingProperties()),
                new PX.Transform(
                    new DX.Offset { X = 304800L, Y = 800000L },
                    new DX.Extents { Cx = 11582400L, Cy = 5800000L }),
                new DX.Graphic(
                    new DX.GraphicData(tbl)
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));

            return new PX.Slide(
                new PX.CommonSlideData(
                    new PX.Background(new PX.BackgroundProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = "0F2030" }))),
                    new PX.ShapeTree(
                        NvGrp(), GrpSp(),
                        Forma(2,
                            $"{titulo}  —  filas {ini + 1}–{fin}  ({num}/{total})",
                            304800, 160000, 11582400, 560000,
                            1300, true, "48CAB4", DX.TextAlignmentTypeValues.Left),
                        gf)),
                new PX.ColorMapOverride(new DX.MasterColorMapping()));
        }

        private static PX.Shape Forma(uint id, string txt,
            long x, long y, long cx, long cy,
            int fs, bool bold, string hex, DX.TextAlignmentTypeValues alin)
        {
            return new PX.Shape(
                new PX.NonVisualShapeProperties(
                    new PX.NonVisualDrawingProperties { Id = id, Name = $"S{id}" },
                    new PX.NonVisualShapeDrawingProperties(
                        new DX.ShapeLocks { NoGrouping = true }),
                    new PX.ApplicationNonVisualDrawingProperties()),
                new PX.ShapeProperties(
                    new DX.Transform2D(
                        new DX.Offset { X = x, Y = y },
                        new DX.Extents { Cx = cx, Cy = cy }),
                    new DX.PresetGeometry(new DX.AdjustValueList())
                    { Preset = DX.ShapeTypeValues.Rectangle },
                    new DX.NoFill()),
                new PX.TextBody(
                    new DX.BodyProperties { Anchor = DX.TextAnchoringTypeValues.Center },
                    new DX.ListStyle(),
                    new DX.Paragraph(
                        new DX.ParagraphProperties { Alignment = alin },
                        new DX.Run(
                            new DX.RunProperties(
                                new DX.SolidFill(new DX.RgbColorModelHex { Val = hex }),
                                new DX.LatinFont { Typeface = "+mj-lt" })
                            { FontSize = fs, Bold = bold, Dirty = false },
                            new DX.Text(txt)))));
        }

        private static DX.TableCell CeldaPptx(string txt, string bg, string fg,
            bool bold, int fs)
        {
            var cell = new DX.TableCell();
            cell.AppendChild(new DX.Paragraph(
                new DX.Run(
                    new DX.RunProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = fg }),
                        new DX.LatinFont { Typeface = "+mn-lt" })
                    { FontSize = fs, Bold = bold, Dirty = false },
                    new DX.Text(Trunc(txt, 25)))));
            cell.AppendChild(new DX.TableCellProperties(
                new DX.SolidFill(new DX.RgbColorModelHex { Val = bg })));
            return cell;
        }

        private static DX.Theme TemaMinimo()
        {
            return new DX.Theme(
                new DX.ThemeElements(
                    new DX.ColorScheme(
                        new DX.Dark1Color(new DX.SystemColor
                        { LastColor = "000000", Val = DX.SystemColorValues.WindowText }),
                        new DX.Light1Color(new DX.SystemColor
                        { LastColor = "FFFFFF", Val = DX.SystemColorValues.Window }),
                        new DX.Dark2Color(new DX.RgbColorModelHex { Val = "1A3A4A" }),
                        new DX.Light2Color(new DX.RgbColorModelHex { Val = "E8F4F8" }),
                        new DX.Accent1Color(new DX.RgbColorModelHex { Val = "48CAB4" }),
                        new DX.Accent2Color(new DX.RgbColorModelHex { Val = "3B82F6" }),
                        new DX.Accent3Color(new DX.RgbColorModelHex { Val = "F59E0B" }),
                        new DX.Accent4Color(new DX.RgbColorModelHex { Val = "EF4444" }),
                        new DX.Accent5Color(new DX.RgbColorModelHex { Val = "8B5CF6" }),
                        new DX.Accent6Color(new DX.RgbColorModelHex { Val = "10B981" }),
                        new DX.Hyperlink(new DX.RgbColorModelHex { Val = "3B82F6" }))
                    { Name = "Arctic" },
                    new DX.FontScheme(
                        new DX.MajorFont(new DX.LatinFont { Typeface = "Calibri" }),
                        new DX.MinorFont(new DX.LatinFont { Typeface = "Calibri" }))
                    { Name = "Arctic" },
                    new DX.FormatScheme(
                        new DX.FillStyleList(
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }),
                            new DX.GradientFill(new DX.GradientStopList()),
                            new DX.GradientFill(new DX.GradientStopList())),
                        new DX.LineStyleList(
                            new DX.Outline { Width = 6350 },
                            new DX.Outline { Width = 12700 },
                            new DX.Outline { Width = 19050 }),
                        new DX.EffectStyleList(
                            new DX.EffectStyle(new DX.EffectList()),
                            new DX.EffectStyle(new DX.EffectList()),
                            new DX.EffectStyle(new DX.EffectList())),
                        new DX.BackgroundFillStyleList(
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }),
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }),
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor })))
                    { Name = "Arctic" }))
            { Name = "Arctic" };
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF — paginación correcta, una celda por línea
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarPdf(DataTable dt, string titulo, string ruta)
        {
            using var pdf = new PdfDocument();
            pdf.Info.Title = titulo;
            pdf.Info.Creator = "FileExplorerr";

            var fTit = new XFont("Arial", 14, XFontStyleEx.Bold);
            var fSub = new XFont("Arial", 7.5, XFontStyleEx.Regular);
            var fHdr = new XFont("Arial", 7.5, XFontStyleEx.Bold);
            var fDat = new XFont("Arial", 6.5, XFontStyleEx.Regular);

            var cHdrBg = XColor.FromArgb(26, 58, 74);
            var cPar = XColor.FromArgb(232, 244, 248);
            var cImp = XColors.White;
            var cTxtD = XColor.FromArgb(20, 20, 40);
            var cAccent = XColor.FromArgb(72, 202, 180);
            var cBorde = XColor.FromArgb(180, 200, 215);

            const double MRG = 25;
            const double ALT_BND = 52;
            const double ALT_HDR = 17;
            const double ALT_ROW = 13;
            const double ALT_PIE = 15;

            int n = dt.Columns.Count;

            // Calcular anchos de columna una sola vez
            // (se ajustan en cada página según pw)
            var maxCh = new double[n];
            for (int c = 0; c < n; c++)
                maxCh[c] = Math.Max(dt.Columns[c].ColumnName.Length, 4);
            foreach (DataRow row in dt.Rows)
                for (int c = 0; c < n; c++)
                    maxCh[c] = Math.Max(maxCh[c], (row[c]?.ToString() ?? "").Length);

            int rowIdx = 0;
            int pageNum = 0;
            int totalR = dt.Rows.Count;

            while (rowIdx < totalR || pageNum == 0)
            {
                // Crear página A4 landscape
                var pag = pdf.AddPage();
                pag.Orientation = PdfSharp.PageOrientation.Landscape;
                pag.Size = PdfSharp.PageSize.A4;
                pageNum++;

                double pw = pag.Width.Point;   // ~841.9
                double ph = pag.Height.Point;  // ~595.3

                using var g = XGraphics.FromPdfPage(pag);

                double wu = pw - MRG * 2;

                // Anchos proporcionales ajustados a wu
                double suma = maxCh.Sum();
                var anchP = maxCh.Select(ch =>
                    Math.Max(22.0, Math.Min(140.0, (ch / suma) * wu))).ToArray();
                double tAncho = anchP.Sum();
                if (tAncho > wu)
                {
                    double f = wu / tAncho;
                    for (int i = 0; i < n; i++) anchP[i] *= f;
                    tAncho = wu;
                }

                // Banda encabezado de página
                g.DrawRectangle(new XSolidBrush(cHdrBg), 0, 0, pw, ALT_BND);
                g.DrawString(titulo, fTit, new XSolidBrush(XColors.White),
                    new XRect(MRG, 8, pw - MRG * 2, 20), XStringFormats.TopLeft);
                g.DrawString(
                    $"{totalR:N0} registros · {n} columnas · {DateTime.Now:dd/MM/yyyy HH:mm} · Pág. {pageNum}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(MRG, 30, pw - MRG * 2, 14), XStringFormats.TopLeft);

                double y = ALT_BND + 3;

                // Encabezado de tabla
                g.DrawRectangle(new XSolidBrush(cHdrBg), MRG, y, tAncho, ALT_HDR);
                double x = MRG;
                for (int c = 0; c < n; c++)
                {
                    string h = Trunc(dt.Columns[c].ColumnName, 18);
                    g.DrawString(h, fHdr, new XSolidBrush(XColors.White),
                        new XRect(x + 2, y + 2, anchP[c] - 4, ALT_HDR - 2),
                        XStringFormats.TopLeft);
                    if (c < n - 1)
                        g.DrawLine(new XPen(XColor.FromArgb(50, 100, 120), 0.4),
                            x + anchP[c], y, x + anchP[c], y + ALT_HDR);
                    x += anchP[c];
                }
                y += ALT_HDR;

                double yMax = ph - ALT_PIE - MRG;

                // Filas de datos
                while (rowIdx < totalR && y + ALT_ROW <= yMax)
                {
                    var cFondo = rowIdx % 2 == 0 ? cPar : cImp;
                    g.DrawRectangle(new XSolidBrush(cFondo), MRG, y, tAncho, ALT_ROW);

                    x = MRG;
                    for (int c = 0; c < n; c++)
                    {
                        string val = Trunc(dt.Rows[rowIdx][c]?.ToString() ?? "", 28);
                        g.DrawString(val, fDat, new XSolidBrush(cTxtD),
                            new XRect(x + 2, y + 1, anchP[c] - 4, ALT_ROW - 1),
                            XStringFormats.TopLeft);
                        if (c < n - 1)
                            g.DrawLine(new XPen(cBorde, 0.25),
                                x + anchP[c], y, x + anchP[c], y + ALT_ROW);
                        x += anchP[c];
                    }
                    g.DrawLine(new XPen(cBorde, 0.25), MRG, y + ALT_ROW, MRG + tAncho, y + ALT_ROW);

                    y += ALT_ROW;
                    rowIdx++;
                }

                // Borde exterior tabla
                g.DrawRectangle(new XPen(cAccent, 0.7),
                    MRG, ALT_BND + 3, tAncho, y - (ALT_BND + 3));

                // Pie
                g.DrawString($"FileExplorerr · {titulo} · {DateTime.Now:yyyy}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(MRG, ph - MRG - 11, pw - MRG * 2, 12), XStringFormats.BottomLeft);
                g.DrawString($"Pág. {pageNum}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(MRG, ph - MRG - 11, pw - MRG * 2, 12), XStringFormats.BottomRight);

                if (rowIdx >= totalR) break;
            }

            pdf.Save(ruta);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string LetraCol(int idx)
        {
            string r = "";
            for (idx++; idx > 0; idx = (idx - 1) / 26)
                r = (char)('A' + (idx - 1) % 26) + r;
            return r;
        }

        private static string HojaNombre(string s)
        {
            var v = new string(s.Where(c =>
                c != '/' && c != '\\' && c != '[' && c != ']' &&
                c != '*' && c != '?' && c != ':').ToArray());
            return v.Length > 31 ? v[..31] : (v.Length > 0 ? v : "Hoja1");
        }

        private static string NombreSeguro(string s) =>
            new string(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

        private static string Trunc(string s, int max) =>
            s.Length > max ? s[..max] + "…" : s;
    }
}