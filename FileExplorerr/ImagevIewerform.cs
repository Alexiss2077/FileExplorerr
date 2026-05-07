using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FileExplorerr
{
    public class ImageViewerForm : Form
    {
        private enum Tool { None, Crop, Draw, Erase, Text, ColorPicker }

        private Panel topToolbar = null!, leftToolbar = null!, canvasPanel = null!;
        private PictureBox canvas = null!;
        private Label infoLabel = null!;
        private Button btnCrop = null!, btnDraw = null!, btnErase = null!, btnText = null!, btnPicker = null!, btnToggleGps = null!;
        private Panel colorSwatch = null!;
        private TrackBar brushSizeBar = null!;

        // GPS
        private Panel gpsPanel = null!;
        private Label gpsLatLabel = null!, gpsLonLabel = null!, gpsAltLabel = null!, gpsCameraLabel = null!, gpsDateLabel = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private bool gpsVisible;
        private GpsReader.GpsData? _gpsData;

        private readonly string imagePath;
        private Bitmap original = null!, working = null!;
        private Bitmap? display;
        private float zoom = 1f;
        private Point panOffset, panStart;
        private bool isPanning;
        private Tool currentTool = Tool.None;
        private Color drawColor = Color.FromArgb(220, 95, 85);
        private int brushSize = 4;
        private bool isCropping, isDrawing;
        private Point cropStart, cropEnd, lastDrawPt;
        private Rectangle cropRect;
        private Font textFont = new("Segoe UI", 14F, FontStyle.Bold);
        private readonly Stack<Bitmap> undoStack = new();

        // Text tool settings
        private string textFontFamily = "Segoe UI";
        private float textFontSize = 14F;
        private FontStyle textFontStyle = FontStyle.Bold;
        private Color textColor = Color.White;

        internal static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".gif", ".bmp", ".dib",
            ".tiff", ".tif", ".ico", ".webp", ".avif", ".heic", ".heif",
            ".emf", ".wmf", ".svg", ".ppm", ".pgm", ".pbm", ".tga", ".exr",
            ".raw", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
            ".orf", ".rw2", ".dng", ".pef", ".raf", ".3fr",
        };

        public ImageViewerForm(string path) { imagePath = path; BuildUI(); LoadImage(); }

        private void BuildUI()
        {
            Text = $"Imagen — {Path.GetFileName(imagePath)}";
            Size = new Size(1100, 740);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase; ForeColor = Theme.TextPrimary;
            KeyPreview = true; KeyDown += OnKeyDown;

            // ═══ TOP ═════════════════════════════════════════════════════════
            topToolbar = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Theme.BgSurface };
            int tx = 8;
            AddTopBtn(ref tx, "Guardar", () => SaveCopy(), Theme.ButtonKind.Success);
            tx += 8;
            AddTopBtn(ref tx, "+", () => SetZoom(zoom * 1.25f));
            AddTopBtn(ref tx, "−", () => SetZoom(zoom * 0.8f));
            AddTopBtn(ref tx, "1:1", () => ResetZoom());
            AddTopBtn(ref tx, "Ajustar", () => FitToWindow());
            tx += 8;
            AddTopBtn(ref tx, "↺", () => Rotate(-90));
            AddTopBtn(ref tx, "↻", () => Rotate(90));
            AddTopBtn(ref tx, "↔", () => Flip(true, false));
            AddTopBtn(ref tx, "↕", () => Flip(false, true));
            tx += 8;
            AddTopBtn(ref tx, "Grises", () => ApplyFilter(FilterType.Grayscale));
            AddTopBtn(ref tx, "Sepia", () => ApplyFilter(FilterType.Sepia));
            AddTopBtn(ref tx, "Invertir", () => ApplyFilter(FilterType.Invert));
            tx += 8;
            AddTopBtn(ref tx, "Deshacer", () => Undo(), Theme.ButtonKind.Default);
            AddTopBtn(ref tx, "Restaurar", () => RestoreOriginal(), Theme.ButtonKind.Danger);

            // ═══ LEFT ════════════════════════════════════════════════════════
            leftToolbar = new Panel { Width = 56, Dock = DockStyle.Left, BackColor = Theme.BgSurface };
            int ty = 8;
            btnCrop = AddLeftBtn("✂", ref ty, () => SelectTool(Tool.Crop));
            btnDraw = AddLeftBtn("✏", ref ty, () => SelectTool(Tool.Draw));
            btnErase = AddLeftBtn("◻", ref ty, () => SelectTool(Tool.Erase));
            btnText = AddLeftBtn("T", ref ty, () => SelectTool(Tool.Text));
            btnPicker = AddLeftBtn("◉", ref ty, () => SelectTool(Tool.ColorPicker));

            ty += 8;
            colorSwatch = new Panel { Left = 6, Top = ty, Width = 44, Height = 24, BackColor = drawColor, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            colorSwatch.Click += (s, e) => { using var dlg = new ColorDialog { Color = drawColor, FullOpen = true }; if (dlg.ShowDialog(this) == DialogResult.OK) { drawColor = dlg.Color; colorSwatch.BackColor = drawColor; } };
            leftToolbar.Controls.Add(colorSwatch);
            ty += 32;

            brushSizeBar = new TrackBar { Left = 2, Top = ty, Width = 52, Height = 36, Minimum = 1, Maximum = 40, Value = brushSize, Orientation = Orientation.Horizontal, TickStyle = TickStyle.None, BackColor = Theme.BgSurface };
            brushSizeBar.ValueChanged += (s, e) => brushSize = brushSizeBar.Value;
            leftToolbar.Controls.Add(brushSizeBar);
            ty += 44;

            var sep = new Panel { Left = 6, Top = ty, Width = 44, Height = 1, BackColor = Theme.Border };
            leftToolbar.Controls.Add(sep); ty += 10;

            btnToggleGps = new Button { Text = "📍", Location = new Point(4, ty), Size = new Size(48, 40), BackColor = Theme.SuccessDim, ForeColor = Theme.Success, FlatStyle = FlatStyle.Flat, Font = Theme.FontIcon, Cursor = Cursors.Hand };
            btnToggleGps.FlatAppearance.BorderColor = Theme.Success;
            btnToggleGps.Click += (s, e) => ToggleGpsPanel();
            leftToolbar.Controls.Add(btnToggleGps);

            // ═══ CANVAS ══════════════════════════════════════════════════════
            canvasPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 16) };
            canvas = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 16), SizeMode = PictureBoxSizeMode.Normal };
            typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(canvas, true);
            canvas.Resize += (s, e) => canvas.Invalidate();
            canvas.Paint += Canvas_Paint;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.MouseWheel += (s, e) => SetZoom(zoom * (e.Delta > 0 ? 1.15f : 0.87f));
            canvasPanel.Controls.Add(canvas);

            // ═══ BOTTOM ══════════════════════════════════════════════════════
            var bottomBar = new Panel { Height = 26, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };
            infoLabel = new Label { Dock = DockStyle.Fill, Font = Theme.FontSmall, ForeColor = Theme.TextMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            bottomBar.Controls.Add(infoLabel);

            // ═══ GPS PANEL ═══════════════════════════════════════════════════
            gpsPanel = new Panel { Width = 280, Dock = DockStyle.Right, BackColor = Theme.BgSurface, Visible = false };
            var gpsHeader = new Panel { Height = 38, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            gpsHeader.Controls.Add(new Label { Text = "Ubicación GPS", Dock = DockStyle.Fill, Font = Theme.FontBodyBold, ForeColor = Theme.Success, TextAlign = ContentAlignment.MiddleCenter });

            var infoPanel = new Panel { Height = 140, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(12, 8, 12, 8) };
            gpsLatLabel = MakeGpsLbl("Lat:   —"); gpsLonLabel = MakeGpsLbl("Lon:  —"); gpsAltLabel = MakeGpsLbl("Alt:   —"); gpsCameraLabel = MakeGpsLbl("Cam:  —"); gpsDateLabel = MakeGpsLbl("Fecha: —");
            int gy = 8;
            foreach (var l in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel }) { l.Left = 12; l.Top = gy; l.Width = 252; infoPanel.Controls.Add(l); gy += 24; }

            // Botón para agregar/editar GPS
            var setGpsBtn = Theme.MakeButton("Agregar GPS", 0, Theme.ButtonKind.Primary);
            setGpsBtn.Dock = DockStyle.Bottom; setGpsBtn.Height = 32;
            setGpsBtn.Click += (s, e) => SetGpsCoordinates();

            var openMapBtn = Theme.MakeButton("Abrir en Maps", 0, Theme.ButtonKind.Primary);
            openMapBtn.Dock = DockStyle.Bottom; openMapBtn.Height = 32;
            openMapBtn.Click += (s, e) => { if (_gpsData?.HasGps == true) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = $"https://www.google.com/maps?q={_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}", UseShellExecute = true }); };

            mapBrowser = new System.Windows.Forms.WebBrowser { Dock = DockStyle.Fill, ScrollBarsEnabled = false, IsWebBrowserContextMenuEnabled = false };
            gpsPanel.Controls.Add(mapBrowser);
            gpsPanel.Controls.Add(openMapBtn);
            gpsPanel.Controls.Add(setGpsBtn);
            gpsPanel.Controls.Add(infoPanel);
            gpsPanel.Controls.Add(gpsHeader);

            Controls.Add(canvasPanel); Controls.Add(gpsPanel); Controls.Add(leftToolbar); Controls.Add(topToolbar); Controls.Add(bottomBar);
        }

        private void AddTopBtn(ref int x, string text, Action click, Theme.ButtonKind kind = Theme.ButtonKind.Default)
        {
            var btn = Theme.MakeButton(text, 0, kind);
            btn.Location = new Point(x, 7); btn.Height = 30;
            btn.Click += (s, e) => click();
            topToolbar.Controls.Add(btn); x += btn.Width + 3;
        }

        private Button AddLeftBtn(string text, ref int y, Action click)
        {
            var btn = new Button { Text = text, Location = new Point(4, y), Size = new Size(48, 40), BackColor = Theme.BgElevated, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat, Font = Theme.FontBody, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = Theme.Border;
            btn.Click += (s, e) => click();
            leftToolbar.Controls.Add(btn); y += 46;
            return btn;
        }

        private static Label MakeGpsLbl(string text) => new() { Text = text, Height = 22, Font = Theme.FontMonoSmall, ForeColor = Theme.TextPrimary, BackColor = Color.Transparent, AutoEllipsis = true };

        // ════════════════════════════════════════════════════════════════════
        //  LOAD
        // ════════════════════════════════════════════════════════════════════
        private void LoadImage()
        {
            string ext = Path.GetExtension(imagePath).ToLower();
            try
            {
                Bitmap? bmp = null;
                if (ext == ".svg") { LoadSvg(); return; }
                if (ext == ".ico") { using var ico = new Icon(imagePath, new Size(256, 256)); bmp = ico.ToBitmap(); }
                else if (ext is ".tiff" or ".tif") { using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var tmp = Image.FromStream(fs); bmp = new Bitmap(tmp); }
                else if (ext is ".emf" or ".wmf") { using var meta = new Metafile(imagePath); bmp = new Bitmap(meta.Width * 2, meta.Height * 2); using var g = Graphics.FromImage(bmp); g.DrawImage(meta, 0, 0, bmp.Width, bmp.Height); }
                else { using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var tmp = Image.FromStream(fs, true, true); bmp = new Bitmap(tmp); }
                if (bmp == null) throw new InvalidOperationException("No se pudo decodificar.");
                original = bmp; working = new Bitmap(original); FitToWindow(); UpdateInfo();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); Close(); }
        }

        private void LoadSvg()
        {
            var svgBrowser = new System.Windows.Forms.WebBrowser { Dock = DockStyle.Fill, ScrollBarsEnabled = false };
            canvasPanel.Controls.Remove(canvas); canvasPanel.Controls.Add(svgBrowser);
            svgBrowser.DocumentText = $"<!DOCTYPE html><html><head><style>*{{margin:0;padding:0}}html,body{{width:100%;height:100%;background:#0c0c10;display:flex;align-items:center;justify-content:center}}svg{{max-width:100%;max-height:100%}}</style></head><body>{File.ReadAllText(imagePath)}</body></html>";
            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker }) btn.Enabled = false;
            infoLabel.Text = $"  {Path.GetFileName(imagePath)}  ·  SVG";
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private void ToggleGpsPanel()
        {
            gpsVisible = !gpsVisible; gpsPanel.Visible = gpsVisible;
            if (gpsVisible && _gpsData == null) LoadGps();
        }

        private void LoadGps()
        {
            _gpsData = GpsReader.Read(imagePath);
            if (_gpsData == null || !_gpsData.HasGps)
            {
                gpsLatLabel.Text = "Sin datos GPS";
                gpsLatLabel.ForeColor = Theme.Warning;
                gpsLonLabel.Text = "Usa 'Agregar GPS' para añadir";
                gpsLonLabel.ForeColor = Theme.TextMuted;
                gpsLonLabel.Visible = true;
                gpsAltLabel.Visible = gpsCameraLabel.Visible = gpsDateLabel.Visible = false;
                return;
            }
            var g = _gpsData;
            gpsLatLabel.Text = $"Lat:   {g.LatString}"; gpsLonLabel.Text = $"Lon:  {g.LonString}";
            gpsAltLabel.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsCameraLabel.Text = $"Cam:  {g.CameraModel ?? "—"}"; gpsDateLabel.Text = $"Fecha: {g.Date ?? "—"}";
            foreach (var l in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel }) { l.ForeColor = Theme.TextPrimary; l.Visible = true; }
            SetBrowserEmulation();
            string ls = g.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lo = g.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            mapBrowser.DocumentText = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/><meta http-equiv='X-UA-Compatible' content='IE=edge'/><link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/><script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script><style>*{{margin:0;padding:0}}html,body,#map{{width:100%;height:100%;background:#121216}}</style></head><body><div id='map'></div><script>var map=L.map('map',{{attributionControl:false}}).setView([{ls},{lo}],15);L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19}}).addTo(map);L.marker([{ls},{lo}]).addTo(map).bindPopup('{g.Latitude:F5}°, {g.Longitude:F5}°').openPopup();</script></body></html>";
        }

        /// <summary>
        /// Diálogo para agregar o editar coordenadas GPS en los metadatos EXIF de la imagen
        /// </summary>
        private void SetGpsCoordinates()
        {
            string extLow = Path.GetExtension(imagePath).ToLower();
            if (extLow != ".jpg" && extLow != ".jpeg" && extLow != ".tiff" && extLow != ".tif")
            {
                MessageBox.Show("Solo se pueden escribir coordenadas GPS en archivos JPEG y TIFF.",
                    "Formato no soportado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new GpsEditDialog(_gpsData);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                GpsWriter.WriteGps(imagePath, dlg.Latitude, dlg.Longitude, dlg.Altitude);

                // Recargar GPS
                _gpsData = null;
                LoadGps();

                MessageBox.Show(
                    $"GPS guardado:\nLat: {dlg.Latitude:F6}\nLon: {dlg.Longitude:F6}" +
                    (dlg.Altitude.HasValue ? $"\nAlt: {dlg.Altitude.Value:F1} m" : ""),
                    "GPS actualizado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al escribir GPS:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SetBrowserEmulation()
        { try { string app = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe"; using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true) ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"); key?.SetValue(app, 11001, Microsoft.Win32.RegistryValueKind.DWord); } catch { } }

        // ════════════════════════════════════════════════════════════════════
        //  CANVAS
        // ════════════════════════════════════════════════════════════════════
        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            if (working == null) return;
            var g = e.Graphics;
            g.Clear(Color.FromArgb(12, 12, 16));
            g.InterpolationMode = zoom >= 1 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            int dw = (int)(working.Width * zoom), dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2, oy = panOffset.Y + (canvas.Height - dh) / 2;
            g.DrawImage(working, ox, oy, dw, dh);

            if (isCropping && cropRect.Width > 0 && cropRect.Height > 0)
            {
                int rx = (int)(cropRect.X * zoom) + ox, ry = (int)(cropRect.Y * zoom) + oy;
                int rw = (int)(cropRect.Width * zoom), rh = (int)(cropRect.Height * zoom);
                using var overlay = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                g.FillRectangle(overlay, ox, oy, rx - ox, dh);
                g.FillRectangle(overlay, rx + rw, oy, dw - (rx - ox + rw), dh);
                g.FillRectangle(overlay, rx, oy, rw, ry - oy);
                g.FillRectangle(overlay, rx, ry + rh, rw, dh - (ry - oy + rh));
                using var borderPen = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(borderPen, rx, ry, rw, rh);
            }
        }

        private Point CanvasToImage(Point p)
        {
            int dw = (int)(working.Width * zoom), dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2, oy = panOffset.Y + (canvas.Height - dh) / 2;
            return new Point((int)((p.X - ox) / zoom), (int)((p.Y - oy) / zoom));
        }

        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            var imgPt = CanvasToImage(e.Location);
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && currentTool == Tool.None))
            { isPanning = true; panStart = e.Location; canvas.Cursor = Cursors.SizeAll; return; }
            if (e.Button != MouseButtons.Left) return;
            switch (currentTool)
            {
                case Tool.Crop: isCropping = true; cropStart = cropEnd = imgPt; cropRect = Rectangle.Empty; break;
                case Tool.Draw: case Tool.Erase: PushUndo(); isDrawing = true; lastDrawPt = imgPt; DrawPoint(imgPt); break;
                case Tool.Text: PushUndo(); PlaceText(imgPt); break;
                case Tool.ColorPicker: PickColorFromImage(imgPt); break;
            }
        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e)
        {
            if (working == null) return;
            var imgPt = CanvasToImage(e.Location);
            if (isPanning) { panOffset.X += e.X - panStart.X; panOffset.Y += e.Y - panStart.Y; panStart = e.Location; canvas.Invalidate(); return; }
            if (isCropping)
            {
                cropEnd = imgPt;
                int x = Math.Max(0, Math.Min(cropStart.X, cropEnd.X)), y = Math.Max(0, Math.Min(cropStart.Y, cropEnd.Y));
                cropRect = new Rectangle(x, y, Math.Min(working.Width - x, Math.Abs(cropEnd.X - cropStart.X)), Math.Min(working.Height - y, Math.Abs(cropEnd.Y - cropStart.Y)));
                canvas.Invalidate(); return;
            }
            if (isDrawing) { DrawLine(lastDrawPt, imgPt); lastDrawPt = imgPt; canvas.Invalidate(); }
        }

        private void Canvas_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isPanning) { isPanning = false; canvas.Cursor = Cursors.Default; return; }
            if (isCropping && e.Button == MouseButtons.Left) { isCropping = false; if (cropRect.Width > 4 && cropRect.Height > 4) ConfirmCrop(); else canvas.Invalidate(); }
            if (isDrawing) { isDrawing = false; canvas.Invalidate(); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TOOLS
        // ════════════════════════════════════════════════════════════════════
        private void SelectTool(Tool t)
        {
            currentTool = currentTool == t ? Tool.None : t;
            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker }) btn.BackColor = Theme.BgElevated;
            Button? active = currentTool switch { Tool.Crop => btnCrop, Tool.Draw => btnDraw, Tool.Erase => btnErase, Tool.Text => btnText, Tool.ColorPicker => btnPicker, _ => null };
            if (active != null) active.BackColor = Theme.AccentBg;
            canvas.Cursor = currentTool switch { Tool.Draw or Tool.Erase or Tool.Crop or Tool.ColorPicker => Cursors.Cross, Tool.Text => Cursors.IBeam, _ => Cursors.Default };
        }

        private void DrawPoint(Point p) { if (p.X < 0 || p.Y < 0 || p.X >= working.Width || p.Y >= working.Height) return; using var g = Graphics.FromImage(working); g.SmoothingMode = SmoothingMode.AntiAlias; using var br = new SolidBrush(currentTool == Tool.Erase ? Color.White : drawColor); g.FillEllipse(br, p.X - brushSize / 2, p.Y - brushSize / 2, brushSize, brushSize); }
        private void DrawLine(Point from, Point to) { using var g = Graphics.FromImage(working); g.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(currentTool == Tool.Erase ? Color.White : drawColor, brushSize) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLine(pen, from, to); }

        private void PlaceText(Point imgPt)
        {
            using var dlg = new TextToolDialog(textFontFamily, textFontSize, textFontStyle, textColor);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            textFontFamily = dlg.SelectedFontFamily;
            textFontSize = dlg.SelectedFontSize;
            textFontStyle = dlg.SelectedFontStyle;
            textColor = dlg.SelectedColor;
            textFont?.Dispose();
            textFont = new Font(textFontFamily, textFontSize, textFontStyle);

            if (string.IsNullOrWhiteSpace(dlg.TextContent)) return;

            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(textColor);
            g.DrawString(dlg.TextContent, textFont, brush, new PointF(Math.Max(0, imgPt.X), Math.Max(0, imgPt.Y)));
            canvas.Invalidate();
        }

        private void ConfirmCrop()
        {
            if (MessageBox.Show($"¿Recortar a {cropRect.Width}×{cropRect.Height}?", "Recortar", MessageBoxButtons.YesNo) != DialogResult.Yes) { canvas.Invalidate(); return; }
            PushUndo();
            var cropped = new Bitmap(cropRect.Width, cropRect.Height);
            using (var g = Graphics.FromImage(cropped)) g.DrawImage(working, new Rectangle(0, 0, cropRect.Width, cropRect.Height), cropRect, GraphicsUnit.Pixel);
            working.Dispose(); working = cropped; cropRect = Rectangle.Empty; FitToWindow();
        }

        private void PickColorFromImage(Point p) { if (p.X >= 0 && p.Y >= 0 && p.X < working.Width && p.Y < working.Height) { drawColor = working.GetPixel(p.X, p.Y); colorSwatch.BackColor = drawColor; } SelectTool(Tool.None); }

        // ════════════════════════════════════════════════════════════════════
        //  TRANSFORMS & FILTERS
        // ════════════════════════════════════════════════════════════════════
        private void Rotate(int deg) { PushUndo(); working.RotateFlip(deg == 90 ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate270FlipNone); FitToWindow(); }
        private void Flip(bool h, bool v) { PushUndo(); working.RotateFlip((h, v) switch { (true, false) => RotateFlipType.RotateNoneFlipX, (false, true) => RotateFlipType.RotateNoneFlipY, _ => RotateFlipType.RotateNoneFlipNone }); canvas.Invalidate(); }

        private enum FilterType { Grayscale, Sepia, Invert, BrightnessUp, BrightnessDown, ContrastUp }
        private void ApplyFilter(FilterType filter)
        {
            PushUndo();
            var bmp = new Bitmap(working.Width, working.Height);
            for (int y = 0; y < working.Height; y++) for (int x = 0; x < working.Width; x++)
            {
                Color c = working.GetPixel(x, y);
                Color nc = filter switch
                {
                    FilterType.Grayscale => GrayPixel(c),
                    FilterType.Sepia => SepiaPixel(c),
                    FilterType.Invert => Color.FromArgb(c.A, 255 - c.R, 255 - c.G, 255 - c.B),
                    _ => c
                };
                bmp.SetPixel(x, y, nc);
            }
            working.Dispose(); working = bmp; canvas.Invalidate();
        }
        private static Color GrayPixel(Color c) { int g = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114); return Color.FromArgb(c.A, g, g, g); }
        private static Color SepiaPixel(Color c) => Color.FromArgb(c.A, Clamp255((int)(c.R * .393 + c.G * .769 + c.B * .189)), Clamp255((int)(c.R * .349 + c.G * .686 + c.B * .168)), Clamp255((int)(c.R * .272 + c.G * .534 + c.B * .131)));
        private static int Clamp255(int v) => Math.Max(0, Math.Min(255, v));

        // ════════════════════════════════════════════════════════════════════
        //  ZOOM / UNDO / SAVE
        // ════════════════════════════════════════════════════════════════════
        private void SetZoom(float z) { zoom = Math.Max(0.05f, Math.Min(20f, z)); canvas.Invalidate(); UpdateInfo(); }
        private void ResetZoom() { zoom = 1f; panOffset = Point.Empty; canvas.Invalidate(); }
        private void FitToWindow() { if (working == null) return; zoom = Math.Min((float)(canvas.Width > 0 ? canvas.Width : 800) / working.Width, (float)(canvas.Height > 0 ? canvas.Height : 600) / working.Height) * 0.95f; panOffset = Point.Empty; canvas.Invalidate(); UpdateInfo(); }
        private void PushUndo() { if (undoStack.Count >= 20) undoStack.ToArray()[^1].Dispose(); undoStack.Push(new Bitmap(working)); }
        private void Undo() { if (undoStack.Count == 0) return; working.Dispose(); working = undoStack.Pop(); canvas.Invalidate(); }
        private void RestoreOriginal() { if (MessageBox.Show("¿Restaurar original?", "Restaurar", MessageBoxButtons.YesNo) != DialogResult.Yes) return; undoStack.Clear(); working.Dispose(); working = new Bitmap(original); FitToWindow(); }

        private void SaveCopy()
        {
            using var dlg = new SaveFileDialog { Title = "Guardar copia", FileName = Path.GetFileNameWithoutExtension(imagePath) + "_editada", Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { working.Save(dlg.FileName, Path.GetExtension(dlg.FileName).ToLower() switch { ".jpg" or ".jpeg" => ImageFormat.Jpeg, ".bmp" => ImageFormat.Bmp, _ => ImageFormat.Png }); MessageBox.Show("Guardado.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Add or Keys.Oemplus: SetZoom(zoom * 1.15f); break;
                case Keys.Subtract or Keys.OemMinus: SetZoom(zoom * 0.87f); break;
                case Keys.Z when e.Control: Undo(); break;
                case Keys.S when e.Control: SaveCopy(); break;
                case Keys.Escape: if (currentTool != Tool.None) SelectTool(Tool.None); else Close(); break;
            }
        }

        private void UpdateInfo() { if (working != null) infoLabel.Text = $"  {Path.GetFileName(imagePath)}  ·  {working.Width}×{working.Height}  ·  {zoom:P0}"; }

        protected override void Dispose(bool disposing)
        { if (disposing) { original?.Dispose(); working?.Dispose(); display?.Dispose(); foreach (var b in undoStack) b?.Dispose(); undoStack.Clear(); textFont?.Dispose(); } base.Dispose(disposing); }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO PARA EDITAR/AGREGAR GPS
    // ════════════════════════════════════════════════════════════════════════
    internal class GpsEditDialog : Form
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double? Altitude { get; private set; }

        private TextBox txtLat = null!, txtLon = null!, txtAlt = null!;

        public GpsEditDialog(GpsReader.GpsData? existing)
        {
            Text = existing?.HasGps == true ? "Editar coordenadas GPS" : "Agregar coordenadas GPS";
            Size = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(18, 18, 22);
            ForeColor = Color.FromArgb(230, 230, 236);
            Font = new Font("Segoe UI", 9.5F);

            var header = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Color.FromArgb(26, 26, 32) };
            header.Controls.Add(new Label
            {
                Text = "📍  Coordenadas GPS",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(82, 196, 120),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            var lblInfo = new Label
            {
                Text = "Ingresa las coordenadas en formato decimal.\nEjemplo: Lat 27.057918, Lon -101.543602",
                Location = new Point(14, 56),
                Size = new Size(380, 36),
                ForeColor = Color.FromArgb(140, 140, 156),
                Font = new Font("Segoe UI", 8.5F)
            };

            AddLabel("Latitud:", 14, 100);
            txtLat = AddField(120, 98, 260, existing?.HasGps == true ? existing.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

            AddLabel("Longitud:", 14, 136);
            txtLon = AddField(120, 134, 260, existing?.HasGps == true ? existing.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

            AddLabel("Altitud (m):", 14, 172);
            txtAlt = AddField(120, 170, 120, existing?.Altitude.HasValue == true ? existing.Altitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");

            var btnSave = new Button
            {
                Text = "Guardar GPS",
                Location = new Point(170, 210),
                Size = new Size(120, 36),
                BackColor = Color.FromArgb(22, 100, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(300, 210),
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(34, 34, 42),
                ForeColor = Color.FromArgb(230, 230, 236),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(44, 44, 54);

            Controls.Add(header);
            Controls.Add(lblInfo);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(txtLat.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
                Math.Abs(lat) > 90)
            {
                MessageBox.Show("Latitud inválida. Debe ser un número entre -90 y 90.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (!double.TryParse(txtLon.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double lon) ||
                Math.Abs(lon) > 180)
            {
                MessageBox.Show("Longitud inválida. Debe ser un número entre -180 y 180.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Latitude = lat;
            Longitude = lon;

            if (!string.IsNullOrWhiteSpace(txtAlt.Text) &&
                double.TryParse(txtAlt.Text.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double alt))
                Altitude = alt;
            else
                Altitude = null;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 9F)
            });
        }

        private TextBox AddField(int x, int y, int width, string value)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BackColor = Color.FromArgb(34, 34, 42),
                ForeColor = Color.FromArgb(230, 230, 236),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Cascadia Code", 10F),
                Text = value
            };
            Controls.Add(txt);
            return txt;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO DE TEXTO — fuente, tamaño, estilo, color
    // ════════════════════════════════════════════════════════════════════════
    internal class TextToolDialog : Form
    {
        public string TextContent { get; private set; } = "";
        public string SelectedFontFamily { get; private set; }
        public float SelectedFontSize { get; private set; }
        public FontStyle SelectedFontStyle { get; private set; }
        public Color SelectedColor { get; private set; }

        private TextBox txtContent = null!;
        private ComboBox cmbFont = null!;
        private NumericUpDown nudSize = null!;
        private CheckBox chkBold = null!, chkItalic = null!, chkUnderline = null!;
        private Panel colorSwatch = null!;
        private Label previewLabel = null!;
        private Color currentColor;

        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color BgHeader = Color.FromArgb(26, 26, 32);
        private static readonly Color BorderColor = Color.FromArgb(44, 44, 54);
        private static readonly Color AccentColor = Color.FromArgb(72, 202, 188);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);

        public TextToolDialog(string fontFamily, float fontSize, FontStyle fontStyle, Color color)
        {
            SelectedFontFamily = fontFamily;
            SelectedFontSize = fontSize;
            SelectedFontStyle = fontStyle;
            SelectedColor = color;
            currentColor = color;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Insertar texto";
            Size = new Size(520, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = BgForm; ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            var header = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = BgHeader };
            header.Controls.Add(new Label
            {
                Text = "✏  Insertar texto en imagen",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            AddLabel("Texto:", 14, 58);
            txtContent = new TextBox
            {
                Location = new Point(110, 56),
                Size = new Size(384, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            txtContent.TextChanged += (s, e) => UpdatePreview();

            AddLabel("Fuente:", 14, 100);
            cmbFont = new ComboBox
            {
                Location = new Point(110, 98),
                Size = new Size(230, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };
            foreach (var fam in new[] {
                "Segoe UI", "Arial", "Times New Roman", "Courier New", "Verdana",
                "Georgia", "Trebuchet MS", "Impact", "Comic Sans MS", "Tahoma",
                "Calibri", "Cambria", "Consolas", "Cascadia Code", "Palatino Linotype"
            })
                cmbFont.Items.Add(fam);

            int fontIdx = cmbFont.Items.IndexOf(SelectedFontFamily);
            cmbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;
            cmbFont.SelectedIndexChanged += (s, e) => UpdatePreview();

            AddLabel("Tamaño:", 354, 100);
            nudSize = new NumericUpDown
            {
                Location = new Point(420, 98),
                Size = new Size(74, 28),
                Minimum = 6,
                Maximum = 200,
                Value = (decimal)Math.Clamp(SelectedFontSize, 6, 200),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F)
            };
            nudSize.ValueChanged += (s, e) => UpdatePreview();

            AddLabel("Estilo:", 14, 142);
            chkBold = MakeCheck("Negrita", 110, 140);
            chkItalic = MakeCheck("Cursiva", 188, 140);
            chkUnderline = MakeCheck("Subrayado", 266, 140);
            chkBold.Checked = (SelectedFontStyle & FontStyle.Bold) != 0;
            chkItalic.Checked = (SelectedFontStyle & FontStyle.Italic) != 0;
            chkUnderline.Checked = (SelectedFontStyle & FontStyle.Underline) != 0;
            chkBold.CheckedChanged += (s, e) => UpdatePreview();
            chkItalic.CheckedChanged += (s, e) => UpdatePreview();
            chkUnderline.CheckedChanged += (s, e) => UpdatePreview();

            AddLabel("Color:", 14, 184);
            colorSwatch = new Panel
            {
                Location = new Point(110, 182),
                Size = new Size(44, 26),
                BackColor = currentColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            colorSwatch.Click += (s, e) =>
            {
                using var dlg = new ColorDialog { Color = currentColor, FullOpen = true };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    currentColor = dlg.Color;
                    colorSwatch.BackColor = currentColor;
                    UpdatePreview();
                }
            };

            var btnColorPick = new Button
            {
                Text = "Elegir color...",
                Location = new Point(162, 181),
                Size = new Size(110, 28),
                BackColor = BgField,
                ForeColor = TextSec,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnColorPick.FlatAppearance.BorderColor = BorderColor;
            btnColorPick.Click += (s, e) =>
            {
                using var dlg = new ColorDialog { Color = currentColor, FullOpen = true };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    currentColor = dlg.Color;
                    colorSwatch.BackColor = currentColor;
                    UpdatePreview();
                }
            };

            var previewBox = new Panel
            {
                Location = new Point(14, 222),
                Size = new Size(480, 90),
                BackColor = Color.FromArgb(10, 10, 14),
                BorderStyle = BorderStyle.FixedSingle
            };
            previewLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Text = "Vista previa"
            };
            previewBox.Controls.Add(previewLabel);

            var lblPreviewHint = new Label
            {
                Text = "Vista previa",
                Location = new Point(14, 208),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = TextSec
            };

            var btnOk = new Button
            {
                Text = "Insertar",
                Location = new Point(280, 326),
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(22, 100, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(390, 326),
                Size = new Size(100, 36),
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = BorderColor;

            Controls.Add(header);
            Controls.Add(txtContent);
            Controls.Add(cmbFont);
            Controls.Add(nudSize);
            Controls.Add(chkBold);
            Controls.Add(chkItalic);
            Controls.Add(chkUnderline);
            Controls.Add(colorSwatch);
            Controls.Add(btnColorPick);
            Controls.Add(lblPreviewHint);
            Controls.Add(previewBox);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdatePreview();
            txtContent.Focus();
        }

        private void UpdatePreview()
        {
            try
            {
                FontStyle style = FontStyle.Regular;
                if (chkBold?.Checked == true) style |= FontStyle.Bold;
                if (chkItalic?.Checked == true) style |= FontStyle.Italic;
                if (chkUnderline?.Checked == true) style |= FontStyle.Underline;

                string family = cmbFont?.SelectedItem?.ToString() ?? "Segoe UI";
                float size = (float)(nudSize?.Value ?? 14);

                previewLabel.Font?.Dispose();
                previewLabel.Font = new Font(family, Math.Min(size, 40), style);
                previewLabel.ForeColor = currentColor;
                previewLabel.Text = string.IsNullOrWhiteSpace(txtContent?.Text) ? "Vista previa" : txtContent.Text;
            }
            catch { }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            TextContent = txtContent.Text;
            SelectedFontFamily = cmbFont.SelectedItem?.ToString() ?? "Segoe UI";
            SelectedFontSize = (float)nudSize.Value;
            SelectedColor = currentColor;

            SelectedFontStyle = FontStyle.Regular;
            if (chkBold.Checked) SelectedFontStyle |= FontStyle.Bold;
            if (chkItalic.Checked) SelectedFontStyle |= FontStyle.Italic;
            if (chkUnderline.Checked) SelectedFontStyle |= FontStyle.Underline;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F)
            });
        }

        private CheckBox MakeCheck(string text, int x, int y)
        {
            var chk = new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = TextPri,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            Controls.Add(chk);
            return chk;
        }
    }
}