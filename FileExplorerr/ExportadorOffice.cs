// ============================================================================
//  ExportadorOffice.cs
//  Exportación a Excel (.xlsx), Word (.docx), PowerPoint (.pptx) y PDF
//
//  NuGet requeridos:
//    <PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />
//    <PackageReference Include="PdfSharp"               Version="6.1.0" />
// ============================================================================

using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// ── Aliases para evitar colisiones entre namespaces de OpenXml ──────────────
using OXml = DocumentFormat.OpenXml;
using Pkg = DocumentFormat.OpenXml.Packaging;

// Excel
using XL = DocumentFormat.OpenXml.Spreadsheet;

// Word
using WD = DocumentFormat.OpenXml.Wordprocessing;

// PowerPoint + Drawing
using PX = DocumentFormat.OpenXml.Presentation;
using DX = DocumentFormat.OpenXml.Drawing;

// PDF
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace FileExplorerr
{
    public static class ExportadorOffice
    {
        // ════════════════════════════════════════════════════════════════════
        //  PUNTO DE ENTRADA UNIFICADO
        // ════════════════════════════════════════════════════════════════════
        public static bool ExportarConDialogo(System.Data.DataTable? dt,
            string tituloReporte, string extension, IWin32Window? owner = null)
        {
            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string extU = extension.TrimStart('.').ToUpper();
            string filter = extension switch
            {
                ".xlsx" => "Excel (*.xlsx)|*.xlsx",
                ".docx" => "Word (*.docx)|*.docx",
                ".pptx" => "PowerPoint (*.pptx)|*.pptx",
                ".pdf" => "PDF (*.pdf)|*.pdf",
                _ => $"{extU} (*{extension})|*{extension}"
            };

            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar como {extU}",
                Filter = filter + "|Todos (*.*)|*.*",
                FileName = $"{Sanitizar(tituloReporte)}_{DateTime.Now:yyyyMMdd_HHmm}{extension}"
            };
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

            try
            {
                switch (extension)
                {
                    case ".xlsx": ExportarExcel(dt, tituloReporte, dlg.FileName); break;
                    case ".docx": ExportarWord(dt, tituloReporte, dlg.FileName); break;
                    case ".pptx": ExportarPowerPoint(dt, tituloReporte, dlg.FileName); break;
                    case ".pdf": ExportarPdf(dt, tituloReporte, dlg.FileName); break;
                    default: throw new NotSupportedException($"Formato '{extension}' no soportado.");
                }

                if (MessageBox.Show(
                        $"{extU} generado:\n{dlg.FileName}\n\n¿Abrir?",
                        "Exportación completa",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        { FileName = dlg.FileName, UseShellExecute = true });
                }
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
        //  EXCEL (.xlsx)
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarExcel(DataTable dt, string titulo, string ruta)
        {
            using var doc = Pkg.SpreadsheetDocument.Create(
                ruta, OXml.SpreadsheetDocumentType.Workbook);

            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new XL.Workbook();

            // Estilos
            var stylesPart = wbPart.AddNewPart<Pkg.WorkbookStylesPart>();
            stylesPart.Stylesheet = CrearEstilosExcel();
            stylesPart.Stylesheet.Save();

            // Hoja
            var wsPart = wbPart.AddNewPart<Pkg.WorksheetPart>();
            var sheetData = new XL.SheetData();
            wsPart.Worksheet = new XL.Worksheet(sheetData);

            var sheets = wbPart.Workbook.AppendChild(new XL.Sheets());
            sheets.AppendChild(new XL.Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = SanitizarHoja(titulo)
            });

            // Anchos de columna
            var cols = new XL.Columns();
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                int max = Math.Max(dt.Columns[c].ColumnName.Length,
                    dt.Rows.Cast<DataRow>()
                        .Select(r => (r[c]?.ToString() ?? "").Length)
                        .DefaultIfEmpty(0).Max());
                cols.AppendChild(new XL.Column
                {
                    Min = (uint)(c + 1),
                    Max = (uint)(c + 1),
                    Width = Math.Min(Math.Max(max + 3, 10), 50),
                    CustomWidth = true
                });
            }
            wsPart.Worksheet.InsertBefore(cols, sheetData);

            // Fila de encabezado (estilo 1)
            var headerRow = new XL.Row { RowIndex = 1 };
            for (int c = 0; c < dt.Columns.Count; c++)
                headerRow.AppendChild(new XL.Cell
                {
                    CellReference = LetraCol(c) + "1",
                    CellValue = new XL.CellValue(dt.Columns[c].ColumnName),
                    DataType = XL.CellValues.InlineString,
                    StyleIndex = 1u
                });
            sheetData.AppendChild(headerRow);

            // Filas de datos
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                var row = new XL.Row { RowIndex = (uint)(r + 2) };
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    string val = dt.Rows[r][c]?.ToString() ?? "";
                    bool esNum = double.TryParse(val,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double numVal);

                    var cell = new XL.Cell
                    {
                        CellReference = LetraCol(c) + (r + 2),
                        StyleIndex = (uint)(r % 2 == 0 ? 2 : 3)
                    };
                    if (esNum)
                    {
                        cell.CellValue = new XL.CellValue(
                            numVal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        cell.CellValue = new XL.CellValue(val);
                        cell.DataType = XL.CellValues.InlineString;
                    }
                    row.AppendChild(cell);
                }
                sheetData.AppendChild(row);
            }

            // AutoFilter
            wsPart.Worksheet.AppendChild(new XL.AutoFilter
            {
                Reference = $"A1:{LetraCol(dt.Columns.Count - 1)}1"
            });

            wsPart.Worksheet.Save();
            wbPart.Workbook.Save();
        }

        private static XL.Stylesheet CrearEstilosExcel()
        {
            var fonts = new XL.Fonts(
                new XL.Font(),
                new XL.Font(
                    new XL.Bold(),
                    new XL.FontSize { Val = 10 },
                    new XL.Color { Rgb = "FFFFFFFF" })
            );
            var fills = new XL.Fills(
                new XL.Fill(new XL.PatternFill { PatternType = XL.PatternValues.None }),
                new XL.Fill(new XL.PatternFill { PatternType = XL.PatternValues.Gray125 }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FF1A2A3A" }
                }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FFF4F6F8" }
                }),
                new XL.Fill(new XL.PatternFill
                {
                    PatternType = XL.PatternValues.Solid,
                    ForegroundColor = new XL.ForegroundColor { Rgb = "FFFFFFFF" }
                })
            );
            var borders = new XL.Borders(
                new XL.Border(),
                new XL.Border(
                    new XL.LeftBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.RightBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.TopBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin },
                    new XL.BottomBorder(new XL.Color { Auto = true }) { Style = XL.BorderStyleValues.Thin })
            );
            var csf = new XL.CellStyleFormats(new XL.CellFormat());
            var cf = new XL.CellFormats(
                new XL.CellFormat(),
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
                new XL.CellFormat { FillId = 3, BorderId = 1, ApplyFill = true, ApplyBorder = true },
                new XL.CellFormat { FillId = 4, BorderId = 1, ApplyFill = true, ApplyBorder = true }
            );
            return new XL.Stylesheet(fonts, fills, borders, csf, cf);
        }

        // ════════════════════════════════════════════════════════════════════
        //  WORD (.docx)
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarWord(DataTable dt, string titulo, string ruta)
        {
            using var doc = Pkg.WordprocessingDocument.Create(
                ruta, OXml.WordprocessingDocumentType.Document);

            var main = doc.AddMainDocumentPart();
            main.Document = new WD.Document();
            var body = main.Document.AppendChild(new WD.Body());

            // Título
            body.AppendChild(new WD.Paragraph(
                new WD.ParagraphProperties(
                    new WD.Justification { Val = WD.JustificationValues.Center },
                    new WD.SpacingBetweenLines { After = "160" }),
                new WD.Run(
                    new WD.RunProperties(
                        new WD.Bold(),
                        new WD.FontSize { Val = "36" },
                        new WD.Color { Val = "1A2A3A" }),
                    new WD.Text(titulo))));

            // Subtítulo
            body.AppendChild(new WD.Paragraph(
                new WD.ParagraphProperties(
                    new WD.Justification { Val = WD.JustificationValues.Center },
                    new WD.SpacingBetweenLines { After = "320" }),
                new WD.Run(
                    new WD.RunProperties(
                        new WD.Color { Val = "666677" },
                        new WD.FontSize { Val = "20" }),
                    new WD.Text(
                        $"{dt.Rows.Count} registros · {dt.Columns.Count} columnas · {DateTime.Now:dd/MM/yyyy HH:mm}"))));

            // Tabla
            var tbl = new WD.Table(
                new WD.TableProperties(
                    new WD.TableWidth { Width = "10000", Type = WD.TableWidthUnitValues.Dxa },
                    new WD.TableBorders(
                        new WD.TopBorder { Val = WD.BorderValues.Single, Size = 4, Color = "2E4057" },
                        new WD.BottomBorder { Val = WD.BorderValues.Single, Size = 4, Color = "2E4057" },
                        new WD.LeftBorder { Val = WD.BorderValues.Single, Size = 4, Color = "2E4057" },
                        new WD.RightBorder { Val = WD.BorderValues.Single, Size = 4, Color = "2E4057" },
                        new WD.InsideHorizontalBorder { Val = WD.BorderValues.Single, Size = 4, Color = "AAAAAA" },
                        new WD.InsideVerticalBorder { Val = WD.BorderValues.Single, Size = 4, Color = "AAAAAA" })));

            // Encabezado
            var hRow = new WD.TableRow();
            foreach (DataColumn col in dt.Columns)
            {
                var cell = new WD.TableCell(
                    new WD.TableCellProperties(
                        new WD.Shading { Fill = "1A2A3A", Val = WD.ShadingPatternValues.Clear }),
                    new WD.Paragraph(new WD.Run(
                        new WD.RunProperties(
                            new WD.Bold(),
                            new WD.Color { Val = "FFFFFF" },
                            new WD.FontSize { Val = "18" }),
                        new WD.Text(col.ColumnName))));
                hRow.AppendChild(cell);
            }
            tbl.AppendChild(hRow);

            // Datos
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                string fondo = r % 2 == 0 ? "F4F6F8" : "FFFFFF";
                var dRow = new WD.TableRow();
                foreach (DataColumn col in dt.Columns)
                {
                    var cell = new WD.TableCell(
                        new WD.TableCellProperties(
                            new WD.Shading { Fill = fondo, Val = WD.ShadingPatternValues.Clear }),
                        new WD.Paragraph(new WD.Run(
                            new WD.RunProperties(new WD.FontSize { Val = "18" }),
                            new WD.Text(dt.Rows[r][col]?.ToString() ?? ""))));
                    dRow.AppendChild(cell);
                }
                tbl.AppendChild(dRow);
            }

            body.AppendChild(tbl);
            body.AppendChild(new WD.SectionProperties(
                new WD.PageMargin { Top = 720, Bottom = 720, Left = 1080, Right = 1080 }));

            main.Document.Save();
        }

        // ════════════════════════════════════════════════════════════════════
        //  POWERPOINT (.pptx)
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarPowerPoint(DataTable dt, string titulo, string ruta)
        {
            using var pres = Pkg.PresentationDocument.Create(
                ruta, OXml.PresentationDocumentType.Presentation);

            var presPart = pres.AddPresentationPart();
            presPart.Presentation = new PX.Presentation(
                new PX.SlideSize { Type = PX.SlideSizeValues.Screen16x9, Cx = 12192000, Cy = 6858000 },
                new PX.SlideIdList(),
                new PX.SlideMasterIdList()
            );

            // SlideMaster mínimo
            var masterPart = presPart.AddNewPart<Pkg.SlideMasterPart>("rId1");
            masterPart.SlideMaster = new PX.SlideMaster(
                new PX.CommonSlideData(new PX.ShapeTree(
                    GrpSpPr(), GrpSpPrProps())),
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
                new PX.SlideLayoutIdList());

            // Tema mínimo
            var themePart = masterPart.AddNewPart<Pkg.ThemePart>("rIdTheme");
            themePart.Theme = CrearTema();

            // SlideLayout mínimo
            var layoutPart = masterPart.AddNewPart<Pkg.SlideLayoutPart>("rIdLayout");
            layoutPart.SlideLayout = new PX.SlideLayout(
                new PX.CommonSlideData(new PX.ShapeTree(GrpSpPr(), GrpSpPrProps())));

            masterPart.SlideMaster.SlideLayoutIdList!.AppendChild(
                new PX.SlideLayoutId { Id = 2049, RelationshipId = "rIdLayout" });
            masterPart.SlideMaster.Save();

            presPart.Presentation.SlideMasterIdList!.AppendChild(
                new PX.SlideMasterId { Id = 2048, RelationshipId = "rId1" });

            // Slides
            uint idCounter = 256;
            var slides = new System.Collections.Generic.List<(Pkg.SlidePart, string)>();

            // Portada — SlidePart creado con AddNewPart (único constructor válido)
            var portada = presPart.AddNewPart<Pkg.SlidePart>("rIdSlide0");
            portada.AddPart(layoutPart, "rIdLayout");
            RellenarSlidePortada(portada, titulo, dt.Columns.Count, dt.Rows.Count);
            slides.Add((portada, "rIdSlide0"));

            // Datos — 25 filas por slide, máximo 50 slides
            const int FPP = 25;
            int total = Math.Min((int)Math.Ceiling(dt.Rows.Count / (double)FPP), 50);
            total = Math.Max(1, total);

            for (int s = 0; s < total; s++)
            {
                int ini = s * FPP;
                int fin = Math.Min(ini + FPP, dt.Rows.Count);
                string rid = $"rIdSlide{s + 1}";
                var sp = presPart.AddNewPart<Pkg.SlidePart>(rid);
                sp.AddPart(layoutPart, "rIdLayout");
                RellenarSlideDatos(sp, dt, titulo, ini, fin, s + 1, total);
                slides.Add((sp, rid));
            }

            foreach (var (_, rid) in slides)
                presPart.Presentation.SlideIdList!.AppendChild(
                    new PX.SlideId { Id = idCounter++, RelationshipId = rid });

            presPart.Presentation.Save();
        }

        // helpers de PowerPoint
        private static PX.NonVisualGroupShapeProperties GrpSpPr() =>
            new PX.NonVisualGroupShapeProperties(
                new PX.NonVisualDrawingProperties { Id = 1, Name = "" },
                new PX.NonVisualGroupShapeDrawingProperties(),
                new PX.ApplicationNonVisualDrawingProperties());

        private static DX.TransformGroup GrpSpPrProps() =>
            new DX.TransformGroup();

        private static DX.Theme CrearTema() =>
            new DX.Theme(
                new DX.ThemeElements(
                    new DX.ColorScheme(
                        new DX.Dark1Color(new DX.SystemColor
                        { LastColor = "000000", Val = DX.SystemColorValues.WindowText }),
                        new DX.Light1Color(new DX.SystemColor
                        { LastColor = "FFFFFF", Val = DX.SystemColorValues.Window }),
                        new DX.Dark2Color(new DX.RgbColorModelHex { Val = "1A2A3A" }),
                        new DX.Light2Color(new DX.RgbColorModelHex { Val = "F4F6F8" }),
                        new DX.Accent1Color(new DX.RgbColorModelHex { Val = "48CAB4" }),
                        new DX.Accent2Color(new DX.RgbColorModelHex { Val = "3B82F6" }),
                        new DX.Accent3Color(new DX.RgbColorModelHex { Val = "F59E0B" }),
                        new DX.Accent4Color(new DX.RgbColorModelHex { Val = "EF4444" }),
                        new DX.Accent5Color(new DX.RgbColorModelHex { Val = "8B5CF6" }),
                        new DX.Accent6Color(new DX.RgbColorModelHex { Val = "10B981" }),
                        new DX.Hyperlink(new DX.RgbColorModelHex { Val = "3B82F6" }))
                    { Name = "ArcticFrost" },
                    new DX.FontScheme(
                        new DX.MajorFont(new DX.LatinFont { Typeface = "Segoe UI" }),
                        new DX.MinorFont(new DX.LatinFont { Typeface = "Segoe UI" }))
                    { Name = "Arctic" },
                    new DX.FormatScheme(
                        new DX.FillStyleList(
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }),
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }),
                            new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor })),
                        new DX.LineStyleList(
                            new DX.Outline(new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor })),
                            new DX.Outline(new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor })),
                            new DX.Outline(new DX.SolidFill(new DX.SchemeColor { Val = DX.SchemeColorValues.PhColor }))),
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

        private static void RellenarSlidePortada(Pkg.SlidePart sp, string titulo, int cols, int filas)
        {
            sp.Slide = new PX.Slide(
                new PX.CommonSlideData(
                    new PX.Background(new PX.BackgroundProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = "1A2A3A" }))),
                    new PX.ShapeTree(GrpSpPr(), GrpSpPrProps(),
                        CajaTexto(2, titulo,
                            609600, 2200000, 10972800, 1400000,
                            4400, true, "FFFFFF", DX.TextAlignmentTypeValues.Center),
                        CajaTexto(3,
                            $"{filas:N0} registros  ·  {cols} columnas\n{DateTime.Now:dd/MM/yyyy HH:mm}",
                            609600, 3750000, 10972800, 800000,
                            2000, false, "48CAB4", DX.TextAlignmentTypeValues.Center))));
            sp.Slide.Save();
        }

        private static void RellenarSlideDatos(Pkg.SlidePart sp, DataTable dt, string titulo,
            int ini, int fin, int numSlide, int totalSlides)
        {
            var tree = new PX.ShapeTree(GrpSpPr(), GrpSpPrProps(),
                CajaTexto(2,
                    $"{titulo}  (filas {ini + 1}–{fin}  /  slide {numSlide}/{totalSlides})",
                    304800, 180000, 11582400, 480000,
                    1300, true, "48CAB4", DX.TextAlignmentTypeValues.Left));

            // Tabla OpenXml Drawing
            int numCols = Math.Min(dt.Columns.Count, 12);
            long anchoCelda = 11582400L / numCols;

            var tblGrid = new DX.TableGrid();
            for (int c = 0; c < numCols; c++)
                tblGrid.AppendChild(new DX.GridColumn { Width = anchoCelda });

            var tbl = new DX.Table(
                new DX.TableProperties(
                    new DX.TableStyleId { Text = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}" }),
                tblGrid);

            // Encabezado
            var hRow = new DX.TableRow { Height = 370000L };
            for (int c = 0; c < numCols; c++)
            {
                string txt = c < dt.Columns.Count ? dt.Columns[c].ColumnName : "";
                hRow.AppendChild(CeldaTabla(txt, "1A2A3A", "FFFFFF", true, 1000));
            }
            tbl.AppendChild(hRow);

            // Datos
            for (int r = ini; r < fin; r++)
            {
                string fondo = r % 2 == 0 ? "1E2D3D" : "243040";
                var dRow = new DX.TableRow { Height = 300000L };
                for (int c = 0; c < numCols; c++)
                {
                    string val = c < dt.Columns.Count
                        ? dt.Rows[r][c]?.ToString() ?? "" : "";
                    dRow.AppendChild(CeldaTabla(val, fondo, "E2E8F0", false, 900));
                }
                tbl.AppendChild(dRow);
            }

            var gf = new PX.GraphicFrame(
                new PX.NonVisualGraphicFrameProperties(
                    new PX.NonVisualDrawingProperties { Id = 100, Name = $"Tabla{numSlide}" },
                    new PX.NonVisualGraphicFrameDrawingProperties(
                        new DX.GraphicFrameLocks { NoGrouping = true }),
                    new PX.ApplicationNonVisualDrawingProperties()),
                new PX.Transform(
                    new DX.Offset { X = 304800L, Y = 700000L },
                    new DX.Extents { Cx = 11582400L, Cy = 6100000L }),
                new DX.Graphic(
                    new DX.GraphicData(tbl)
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));

            tree.AppendChild(gf);

            sp.Slide = new PX.Slide(
                new PX.CommonSlideData(
                    new PX.Background(new PX.BackgroundProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = "0F1A24" }))),
                    tree));
            sp.Slide.Save();
        }

        private static PX.Shape CajaTexto(uint id, string texto,
            long x, long y, long cx, long cy,
            int fontSize, bool bold, string colorHex,
            DX.TextAlignmentTypeValues align)
        {
            return new PX.Shape(
                new PX.NonVisualShapeProperties(
                    new PX.NonVisualDrawingProperties { Id = id, Name = $"TB{id}" },
                    new PX.NonVisualShapeDrawingProperties(
                        new DX.ShapeLocks { NoGrouping = true }),
                    new PX.ApplicationNonVisualDrawingProperties()),
                new PX.ShapeProperties(
                    new DX.Transform2D(
                        new DX.Offset { X = x, Y = y },
                        new DX.Extents { Cx = cx, Cy = cy }),
                    new DX.PresetGeometry(new DX.AdjustValueList())
                    { Preset = DX.ShapeTypeValues.Rectangle }),
                new PX.TextBody(
                    new DX.BodyProperties { Anchor = DX.TextAnchoringTypeValues.Center },
                    new DX.ListStyle(),
                    new DX.Paragraph(
                        new DX.ParagraphProperties { Alignment = align },
                        new DX.Run(
                            new DX.RunProperties(
                                new DX.SolidFill(new DX.RgbColorModelHex { Val = colorHex }),
                                new DX.LatinFont { Typeface = "Segoe UI" })
                            { FontSize = fontSize, Bold = bold, Dirty = false },
                            new DX.Text(texto)))));
        }

        private static DX.TableCell CeldaTabla(string texto,
            string fondoHex, string textoHex, bool bold, int fontSize)
        {
            var cell = new DX.TableCell();
            cell.AppendChild(new DX.Paragraph(
                new DX.Run(
                    new DX.RunProperties(
                        new DX.SolidFill(new DX.RgbColorModelHex { Val = textoHex }),
                        new DX.LatinFont { Typeface = "Segoe UI" })
                    { FontSize = fontSize, Bold = bold, Dirty = false },
                    new DX.Text(Trunc(texto, 30)))));
            cell.AppendChild(new DX.TableCellProperties(
                new DX.SolidFill(new DX.RgbColorModelHex { Val = fondoHex }),
                new DX.TableCellBorders(
                    new DX.BottomBorder(
                        new DX.Outline(
                            new DX.SolidFill(new DX.RgbColorModelHex { Val = "2E4057" }))
                        { Width = 6350 }))));
            return cell;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF — PdfSharp 6.x
        // ════════════════════════════════════════════════════════════════════
        public static void ExportarPdf(DataTable dt, string titulo, string ruta)
        {
            var cFondo = XColor.FromArgb(0x1A, 0x2A, 0x3A);
            var cAccent = XColor.FromArgb(0x48, 0xCA, 0xB4);
            var cBlanco = XColors.White;
            var cPar = XColor.FromArgb(0xF4, 0xF6, 0xF8);
            var cImpar = XColors.White;
            var cTexto = XColor.FromArgb(0x1A, 0x2A, 0x3A);
            var cBorde = XColor.FromArgb(0xCC, 0xCC, 0xDD);

            using var pdf = new PdfDocument();
            pdf.Info.Title = titulo;
            pdf.Info.Author = "FileExplorerr";
            pdf.Info.Creator = "FileExplorerr / PdfSharp";

            var fTitulo = new XFont("Arial", 16, XFontStyleEx.Bold);
            var fSub = new XFont("Arial", 8, XFontStyleEx.Regular);
            var fHdr = new XFont("Arial", 7, XFontStyleEx.Bold);
            var fDat = new XFont("Arial", 6.5, XFontStyleEx.Regular);

            const double margen = 28;
            const double altoCab = 60;
            const double altoHdr = 20;
            const double altoCelda = 14;
            const double altoPie = 18;

            int numCols = dt.Columns.Count;
            double anchoUtil = 842 - margen * 2;   // A4 landscape ~842 pt

            // Anchos proporcionales
            var maxLen = new double[numCols];
            for (int c = 0; c < numCols; c++)
                maxLen[c] = dt.Columns[c].ColumnName.Length;
            foreach (DataRow row in dt.Rows)
                for (int c = 0; c < numCols; c++)
                    maxLen[c] = Math.Max(maxLen[c], (row[c]?.ToString() ?? "").Length);

            double sumLen = maxLen.Sum();
            var anchos = maxLen.Select(a => Math.Max(28, Math.Min(180,
                (a / Math.Max(sumLen, 1)) * anchoUtil))).ToArray();
            double totalAncho = anchos.Sum();
            if (totalAncho > anchoUtil)
            {
                double f = anchoUtil / totalAncho;
                for (int c = 0; c < numCols; c++) anchos[c] *= f;
            }
            double anchoTabla = anchos.Sum();

            double altoUtil = 595 - altoCab - altoHdr - altoPie - margen * 2;
            int fpPag = Math.Max(1, (int)(altoUtil / altoCelda));
            int totalPags = Math.Max(1, (int)Math.Ceiling(dt.Rows.Count / (double)fpPag));

            for (int p = 0; p < totalPags; p++)
            {
                var pag = pdf.AddPage();
                pag.Orientation = PdfSharp.PageOrientation.Landscape;
                pag.Size = PdfSharp.PageSize.A4;

                using var g = XGraphics.FromPdfPage(pag);
                double pw = pag.Width.Point;
                double ph = pag.Height.Point;

                // Encabezado de página
                g.DrawRectangle(new XSolidBrush(cFondo), 0, 0, pw, altoCab);
                g.DrawString(titulo, fTitulo, new XSolidBrush(cBlanco),
                    new XRect(margen, margen, pw - margen * 2, 24),
                    XStringFormats.CenterLeft);
                g.DrawString(
                    $"{dt.Rows.Count:N0} registros · {numCols} columnas · " +
                    $"{DateTime.Now:dd/MM/yyyy HH:mm} · Página {p + 1}/{totalPags}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(margen, margen + 26, pw - margen * 2, 16),
                    XStringFormats.CenterLeft);

                double y = altoCab + 4;

                // Encabezado de tabla
                g.DrawRectangle(new XSolidBrush(cFondo), margen, y, anchoTabla, altoHdr);
                double x = margen;
                for (int c = 0; c < numCols; c++)
                {
                    g.DrawString(Trunc(dt.Columns[c].ColumnName, 20), fHdr,
                        new XSolidBrush(cBlanco),
                        new XRect(x + 2, y + 2, anchos[c] - 4, altoHdr - 2),
                        XStringFormats.TopLeft);
                    x += anchos[c];
                }
                y += altoHdr;

                // Filas
                int iniF = p * fpPag;
                int finF = Math.Min(iniF + fpPag, dt.Rows.Count);

                for (int r = iniF; r < finF; r++)
                {
                    var fondo = r % 2 == 0 ? cPar : cImpar;
                    g.DrawRectangle(new XSolidBrush(fondo), margen, y, anchoTabla, altoCelda);
                    g.DrawLine(new XPen(cBorde, 0.25), margen, y + altoCelda, margen + anchoTabla, y + altoCelda);

                    x = margen;
                    for (int c = 0; c < numCols; c++)
                    {
                        string val = dt.Rows[r][c]?.ToString() ?? "";
                        g.DrawString(Trunc(val, 22), fDat, new XSolidBrush(cTexto),
                            new XRect(x + 2, y + 1, anchos[c] - 4, altoCelda),
                            XStringFormats.TopLeft);
                        // separador vertical
                        g.DrawLine(new XPen(cBorde, 0.25), x + anchos[c], y, x + anchos[c], y + altoCelda);
                        x += anchos[c];
                    }
                    y += altoCelda;
                }

                // Borde exterior tabla
                g.DrawRectangle(new XPen(cAccent, 0.8),
                    margen, altoCab + 4, anchoTabla,
                    altoHdr + (finF - iniF) * altoCelda);

                // Pie
                g.DrawString($"FileExplorerr SQL Viewer  ·  {titulo}  ·  {DateTime.Now:yyyy}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(margen, ph - margen - 12, pw - margen * 2, 14),
                    XStringFormats.BottomLeft);
                g.DrawString($"{p + 1} / {totalPags}",
                    fSub, new XSolidBrush(cAccent),
                    new XRect(margen, ph - margen - 12, pw - margen * 2, 14),
                    XStringFormats.BottomRight);
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

        private static string SanitizarHoja(string s)
        {
            var limpio = new string(s.Where(c =>
                c != '/' && c != '\\' && c != '[' && c != ']' &&
                c != '*' && c != '?' && c != ':').ToArray());
            return limpio.Length > 31 ? limpio[..31] : (limpio.Length > 0 ? limpio : "Hoja1");
        }

        private static string Sanitizar(string s) =>
            new string(s.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());

        private static string Trunc(string s, int max) =>
            s.Length > max ? s[..max] + "…" : s;
    }
}