using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public class ImageViewerForm : Form
    {
        // ── Tool enumeration ─────────────────────────────────────────────────
        private enum Tool { None, Crop, Draw, Erase, Text, ColorPicker }

        // ── Constants ────────────────────────────────────────────────────────
        private const int MaxUndoStates = 20;
        private const float MinZoom = 0.05f;
        private const float MaxZoom = 20f;
        private const float ZoomInFactor = 1.15f;
        private const float ZoomOutFactor = 0.87f;
        private const int DefaultBrushSize = 4;
        private const int ToastDisplayMs = 1400;
        private const int GpsLoadDelayMs = 0;     // LoadGpsAsync is already awaited

        // ── Controls ─────────────────────────────────────────────────────────
        private Panel topToolbar = null!;
        private Panel leftToolbar = null!;
        private Panel canvasPanel = null!;
        private PictureBox canvas = null!;
        private Label infoLabel = null!;
        private Button btnCrop = null!;
        private Button btnDraw = null!;
        private Button btnErase = null!;
        private Button btnText = null!;
        private Button btnPicker = null!;
        private Button btnToggleGps = null!;
        private Panel colorSwatch = null!;
        private TrackBar brushSizeBar = null!;

        // ── GPS ──────────────────────────────────────────────────────────────
        private Panel gpsPanel = null!;
        private Label gpsLatLabel = null!;
        private Label gpsLonLabel = null!;
        private Label gpsAltLabel = null!;
        private Label gpsCameraLabel = null!;
        private Label gpsDateLabel = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private bool gpsVisible;
        private GpsData? _gpsData;

        // ── Image state ──────────────────────────────────────────────────────
        private readonly string imagePath;
        private Bitmap original = null!;
        private Bitmap working = null!;
        private Bitmap? display;
        private float zoom = 1f;
        private Point panOffset;
        private Point panStart;
        private bool isPanning;
        private Tool currentTool = Tool.None;
        private Color drawColor = Color.FromArgb(220, 95, 85);
        private int brushSize = DefaultBrushSize;
        private bool isCropping;
        private bool isDrawing;
        private Point cropStart;
        private Point cropEnd;
        private Point lastDrawPt;
        private Rectangle cropRect;
        private Font textFont = new("Segoe UI", 14F, FontStyle.Bold);
        private readonly Stack<Bitmap> undoStack = new();

        // ── Text tool settings ───────────────────────────────────────────────
        private string textFontFamily = "Segoe UI";
        private float textFontSize = 14F;
        private FontStyle textFontStyle = FontStyle.Bold;
        private Color textColor = Color.White;

        // ── Loading overlay ──────────────────────────────────────────────────
        private Panel _loadingOverlay = null!;
        private Label _loadingLabel = null!;

        internal static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".gif", ".bmp", ".dib",
            ".tiff", ".tif", ".ico", ".webp", ".avif", ".heic", ".heif",
            ".emf", ".wmf", ".svg", ".ppm", ".pgm", ".pbm", ".tga", ".exr",
            ".raw", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
            ".orf", ".rw2", ".dng", ".pef", ".raf", ".3fr",
        };

        public ImageViewerForm(string path)
        {
            imagePath = path;
            BuildUI();
            LoadImage();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = $"Imagen — {Path.GetFileName(imagePath)}";
            Size = new Size(1100, 740);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            BuildTopToolbar();
            BuildLeftToolbar();
            BuildCanvas();
            BuildGpsPanel();

            var bottomBar = new Panel
            {
                Height = 26,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface
            };
            infoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            bottomBar.Controls.Add(infoLabel);

            Controls.Add(canvasPanel);
            Controls.Add(gpsPanel);
            Controls.Add(leftToolbar);
            Controls.Add(topToolbar);
            Controls.Add(bottomBar);
        }

        private void BuildTopToolbar()
        {
            topToolbar = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Theme.BgSurface };
            int x = 8;
            AddTopBtn(ref x, "Guardar", () => SaveCopy(), Theme.ButtonKind.Success);
            x += 8;
            AddTopBtn(ref x, "+", () => SetZoom(zoom * ZoomInFactor));
            AddTopBtn(ref x, "−", () => SetZoom(zoom * ZoomOutFactor));
            AddTopBtn(ref x, "1:1", ResetZoom);
            AddTopBtn(ref x, "Ajustar", FitToWindow);
            x += 8;
            AddTopBtn(ref x, "↺", () => RotateAsync(-90));
            AddTopBtn(ref x, "↻", () => RotateAsync(90));
            AddTopBtn(ref x, "↔", () => FlipAsync(horizontal: true, vertical: false));
            AddTopBtn(ref x, "↕", () => FlipAsync(horizontal: false, vertical: true));
            x += 8;
            AddTopBtn(ref x, "Grises", () => _ = ApplyFilterAsync(FilterType.Grayscale));
            AddTopBtn(ref x, "Sepia", () => _ = ApplyFilterAsync(FilterType.Sepia));
            AddTopBtn(ref x, "Invertir", () => _ = ApplyFilterAsync(FilterType.Invert));
            x += 8;
            AddTopBtn(ref x, "Deshacer", Undo, Theme.ButtonKind.Default);
            AddTopBtn(ref x, "Restaurar", RestoreOriginal, Theme.ButtonKind.Danger);
        }

        private void BuildLeftToolbar()
        {
            leftToolbar = new Panel { Width = 56, Dock = DockStyle.Left, BackColor = Theme.BgSurface };
            int y = 8;
            btnCrop = AddLeftBtn("✂", ref y, () => SelectTool(Tool.Crop));
            btnDraw = AddLeftBtn("✏", ref y, () => SelectTool(Tool.Draw));
            btnErase = AddLeftBtn("◻", ref y, () => SelectTool(Tool.Erase));
            btnText = AddLeftBtn("T", ref y, () => SelectTool(Tool.Text));
            btnPicker = AddLeftBtn("◉", ref y, () => SelectTool(Tool.ColorPicker));

            y += 8;
            colorSwatch = new Panel
            {
                Left = 6,
                Top = y,
                Width = 44,
                Height = 24,
                BackColor = drawColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            colorSwatch.Click += (_, _) =>
            {
                using var dlg = new ColorDialog { Color = drawColor, FullOpen = true };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    drawColor = dlg.Color;
                    colorSwatch.BackColor = drawColor;
                }
            };
            leftToolbar.Controls.Add(colorSwatch);
            y += 32;

            brushSizeBar = new TrackBar
            {
                Left = 2,
                Top = y,
                Width = 52,
                Height = 36,
                Minimum = 1,
                Maximum = 40,
                Value = brushSize,
                Orientation = Orientation.Horizontal,
                TickStyle = TickStyle.None,
                BackColor = Theme.BgSurface
            };
            brushSizeBar.ValueChanged += (_, _) => brushSize = brushSizeBar.Value;
            leftToolbar.Controls.Add(brushSizeBar);
            y += 44;

            var sep = new Panel { Left = 6, Top = y, Width = 44, Height = 1, BackColor = Theme.Border };
            leftToolbar.Controls.Add(sep);
            y += 10;

            btnToggleGps = new Button
            {
                Text = "📍",
                Location = new Point(4, y),
                Size = new Size(48, 40),
                BackColor = Theme.SuccessDim,
                ForeColor = Theme.Success,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontIcon,
                Cursor = Cursors.Hand
            };
            btnToggleGps.FlatAppearance.BorderColor = Theme.Success;
            btnToggleGps.Click += (_, _) => ToggleGpsPanel();
            leftToolbar.Controls.Add(btnToggleGps);
        }

        private void BuildCanvas()
        {
            canvasPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 16) };
            canvas = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 16),
                SizeMode = PictureBoxSizeMode.Normal
            };
            // Enable double buffering via reflection (WinForms quirk).
            typeof(PictureBox)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(canvas, true);

            canvas.Resize += (_, _) => canvas.Invalidate();
            canvas.Paint += Canvas_Paint;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.MouseWheel += (_, e) => SetZoom(zoom * (e.Delta > 0 ? ZoomInFactor : ZoomOutFactor));

            // Loading overlay.
            _loadingOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(180, 12, 12, 16),
                Visible = false
            };
            _loadingLabel = new Label
            {
                Text = "Procesando...",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            _loadingOverlay.Controls.Add(_loadingLabel);
            _loadingOverlay.Resize += (_, _) => CenterLoadingLabel();

            canvasPanel.Controls.Add(canvas);
            canvasPanel.Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();
        }

        private void BuildGpsPanel()
        {
            gpsPanel = new Panel { Width = 280, Dock = DockStyle.Right, BackColor = Theme.BgSurface, Visible = false };

            var gpsHeader = new Panel { Height = 38, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            gpsHeader.Controls.Add(new Label
            {
                Text = "Ubicación GPS",
                Dock = DockStyle.Fill,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Success,
                TextAlign = ContentAlignment.MiddleCenter
            });

            var infoPanel = new Panel
            {
                Height = 140,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(12, 8, 12, 8)
            };
            gpsLatLabel = MakeGpsLabel("Lat:   —");
            gpsLonLabel = MakeGpsLabel("Lon:  —");
            gpsAltLabel = MakeGpsLabel("Alt:   —");
            gpsCameraLabel = MakeGpsLabel("Cam:  —");
            gpsDateLabel = MakeGpsLabel("Fecha: —");

            int gy = 8;
            foreach (var lbl in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel })
            {
                lbl.Left = 12;
                lbl.Top = gy;
                lbl.Width = 252;
                infoPanel.Controls.Add(lbl);
                gy += 24;
            }

            var btnSetGps = Theme.MakeButton("Agregar GPS", 0, Theme.ButtonKind.Primary);
            btnSetGps.Dock = DockStyle.Bottom;
            btnSetGps.Height = 32;
            btnSetGps.Click += (_, _) => SetGpsCoordinates();

            var btnOpenMap = Theme.MakeButton("Abrir en Maps", 0, Theme.ButtonKind.Primary);
            btnOpenMap.Dock = DockStyle.Bottom;
            btnOpenMap.Height = 32;
            btnOpenMap.Click += (_, _) =>
            {
                if (_gpsData?.HasGps == true)
                    OpenBrowserUrl(
                        $"https://www.google.com/maps?q=" +
                        $"{_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            };

            mapBrowser = new System.Windows.Forms.WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false,
                IsWebBrowserContextMenuEnabled = false
            };

            gpsPanel.Controls.Add(mapBrowser);
            gpsPanel.Controls.Add(btnOpenMap);
            gpsPanel.Controls.Add(btnSetGps);
            gpsPanel.Controls.Add(infoPanel);
            gpsPanel.Controls.Add(gpsHeader);
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMAGE LOADING
        // ════════════════════════════════════════════════════════════════════
        private async void LoadImage()
        {
            string ext = Path.GetExtension(imagePath).ToLowerInvariant();
            if (ext == ".svg") { LoadSvg(); return; }

            ShowLoading("Cargando imagen...");
            SetToolsEnabled(false);

            try
            {
                Bitmap? bmp = await Task.Run(() => LoadBitmapFromDisk(imagePath, ext));

                if (bmp is null)
                    throw new InvalidOperationException("No se pudo decodificar la imagen.");

                original = bmp;
                working = new Bitmap(original);
                FitToWindow();
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al abrir imagen",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
            finally
            {
                HideLoading();
                SetToolsEnabled(true);
            }
        }

        private static Bitmap? LoadBitmapFromDisk(string path, string ext)
        {
            try
            {
                if (ext == ".ico")
                {
                    using var ico = new Icon(path, new Size(256, 256));
                    return ico.ToBitmap();
                }
                if (ext is ".tiff" or ".tif")
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var tmp = Image.FromStream(fs);
                    return new Bitmap(tmp);
                }
                if (ext is ".emf" or ".wmf")
                {
                    using var meta = new Metafile(path);
                    var bmp = new Bitmap(meta.Width * 2, meta.Height * 2);
                    using var g = Graphics.FromImage(bmp);
                    g.DrawImage(meta, 0, 0, bmp.Width, bmp.Height);
                    return bmp;
                }
                using var fs2 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var tmp2 = Image.FromStream(fs2, useEmbeddedColorManagement: true, validateImageData: true);
                return new Bitmap(tmp2);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageViewerForm] LoadBitmap: {ex.Message}");
                return null;
            }
        }

        private void SetToolsEnabled(bool enabled)
        {
            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                if (btn is not null) btn.Enabled = enabled;
            foreach (Control c in topToolbar.Controls)
                if (c is Button b) b.Enabled = enabled;
        }

        private void LoadSvg()
        {
            var svgBrowser = new System.Windows.Forms.WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false
            };
            canvasPanel.Controls.Remove(canvas);
            canvasPanel.Controls.Add(svgBrowser);

            string svgContent = File.ReadAllText(imagePath);
            svgBrowser.DocumentText =
                "<!DOCTYPE html><html><head>" +
                "<style>*{margin:0;padding:0}html,body{width:100%;height:100%;" +
                "background:#0c0c10;display:flex;align-items:center;justify-content:center}" +
                "svg{max-width:100%;max-height:100%}</style></head>" +
                $"<body>{svgContent}</body></html>";

            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                btn.Enabled = false;

            infoLabel.Text = $"  {Path.GetFileName(imagePath)}  ·  SVG";
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private async void ToggleGpsPanel()
        {
            gpsVisible = !gpsVisible;
            gpsPanel.Visible = gpsVisible;
            if (gpsVisible && _gpsData is null) await LoadGpsAsync();
        }

        private async Task LoadGpsAsync()
        {
            _gpsData = await Task.Run(() => GpsReader.Read(imagePath));

            if (_gpsData is null || !_gpsData.HasGps)
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
            gpsLatLabel.Text = $"Lat:   {g.LatString}";
            gpsLonLabel.Text = $"Lon:  {g.LonString}";
            gpsAltLabel.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsCameraLabel.Text = $"Cam:  {g.CameraModel ?? "—"}";
            gpsDateLabel.Text = $"Fecha: {g.Date ?? "—"}";

            foreach (var lbl in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel })
            {
                lbl.ForeColor = Theme.TextPrimary;
                lbl.Visible = true;
            }

            // Use the shared BrowserHelper instead of the former private method.
            BrowserHelper.SetEdgeEmulation();

            string ls = g.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lo = g.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            mapBrowser.DocumentText = BuildLeafletHtml(ls, lo, $"{g.Latitude:F5}°, {g.Longitude:F5}°");
        }

        private void SetGpsCoordinates()
        {
            string extLow = Path.GetExtension(imagePath).ToLowerInvariant();
            if (extLow != ".jpg" && extLow != ".jpeg" && extLow != ".tiff" && extLow != ".tif")
            {
                MessageBox.Show(
                    "Solo se pueden escribir coordenadas GPS en archivos JPEG y TIFF.",
                    "Formato no soportado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new GpsEditDialog(_gpsData);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                GpsWriter.WriteGps(imagePath, dlg.Latitude, dlg.Longitude, dlg.Altitude);
                _gpsData = null;
                _ = LoadGpsAsync();
                MessageBox.Show(
                    $"GPS guardado:\nLat: {dlg.Latitude:F6}\nLon: {dlg.Longitude:F6}" +
                    (dlg.Altitude.HasValue ? $"\nAlt: {dlg.Altitude.Value:F1} m" : string.Empty),
                    "GPS actualizado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al escribir GPS:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CANVAS — paint and mouse handling
        // ════════════════════════════════════════════════════════════════════
        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            if (working is null) return;

            var g = e.Graphics;
            g.Clear(Color.FromArgb(12, 12, 16));
            g.InterpolationMode = zoom >= 1
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBicubic;

            int dw = (int)(working.Width * zoom);
            int dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2;
            int oy = panOffset.Y + (canvas.Height - dh) / 2;
            g.DrawImage(working, ox, oy, dw, dh);

            if (isCropping && cropRect.Width > 0 && cropRect.Height > 0)
                DrawCropOverlay(g, ox, oy, dw, dh);
        }

        private void DrawCropOverlay(Graphics g, int ox, int oy, int dw, int dh)
        {
            int rx = (int)(cropRect.X * zoom) + ox;
            int ry = (int)(cropRect.Y * zoom) + oy;
            int rw = (int)(cropRect.Width * zoom);
            int rh = (int)(cropRect.Height * zoom);

            using var overlay = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillRectangle(overlay, ox, oy, rx - ox, dh);
            g.FillRectangle(overlay, rx + rw, oy, dw - (rx - ox + rw), dh);
            g.FillRectangle(overlay, rx, oy, rw, ry - oy);
            g.FillRectangle(overlay, rx, ry + rh, rw, dh - (ry - oy + rh));

            using var borderPen = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(borderPen, rx, ry, rw, rh);
        }

        private Point CanvasToImage(Point p)
        {
            int dw = (int)(working.Width * zoom);
            int dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2;
            int oy = panOffset.Y + (canvas.Height - dh) / 2;
            return new Point((int)((p.X - ox) / zoom), (int)((p.Y - oy) / zoom));
        }

        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            var imgPt = CanvasToImage(e.Location);

            if (e.Button == MouseButtons.Middle ||
               (e.Button == MouseButtons.Left && currentTool == Tool.None))
            {
                isPanning = true;
                panStart = e.Location;
                canvas.Cursor = Cursors.SizeAll;
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            switch (currentTool)
            {
                case Tool.Crop:
                    isCropping = true;
                    cropStart = cropEnd = imgPt;
                    cropRect = Rectangle.Empty;
                    break;
                case Tool.Draw:
                case Tool.Erase:
                    PushUndo();
                    isDrawing = true;
                    lastDrawPt = imgPt;
                    DrawPoint(imgPt);
                    break;
                case Tool.Text:
                    PushUndo();
                    PlaceText(imgPt);
                    break;
                case Tool.ColorPicker:
                    PickColorFromImage(imgPt);
                    break;
            }
        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e)
        {
            if (working is null) return;

            var imgPt = CanvasToImage(e.Location);

            if (isPanning)
            {
                panOffset.X += e.X - panStart.X;
                panOffset.Y += e.Y - panStart.Y;
                panStart = e.Location;
                canvas.Invalidate();
                return;
            }

            if (isCropping)
            {
                cropEnd = imgPt;
                int x = Math.Max(0, Math.Min(cropStart.X, cropEnd.X));
                int y = Math.Max(0, Math.Min(cropStart.Y, cropEnd.Y));
                cropRect = new Rectangle(
                    x, y,
                    Math.Min(working.Width - x, Math.Abs(cropEnd.X - cropStart.X)),
                    Math.Min(working.Height - y, Math.Abs(cropEnd.Y - cropStart.Y)));
                canvas.Invalidate();
                return;
            }

            if (isDrawing)
            {
                DrawLine(lastDrawPt, imgPt);
                lastDrawPt = imgPt;
                canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                isPanning = false;
                canvas.Cursor = Cursors.Default;
                return;
            }
            if (isCropping && e.Button == MouseButtons.Left)
            {
                isCropping = false;
                if (cropRect.Width > 4 && cropRect.Height > 4)
                    ConfirmCrop();
                else
                    canvas.Invalidate();
            }
            if (isDrawing)
            {
                isDrawing = false;
                canvas.Invalidate();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DRAWING TOOLS
        // ════════════════════════════════════════════════════════════════════
        private void SelectTool(Tool t)
        {
            currentTool = (currentTool == t) ? Tool.None : t;

            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                btn.BackColor = Theme.BgElevated;

            Button? active = currentTool switch
            {
                Tool.Crop => btnCrop,
                Tool.Draw => btnDraw,
                Tool.Erase => btnErase,
                Tool.Text => btnText,
                Tool.ColorPicker => btnPicker,
                _ => null
            };
            if (active is not null) active.BackColor = Theme.AccentBg;

            canvas.Cursor = currentTool switch
            {
                Tool.Draw or Tool.Erase or Tool.Crop or Tool.ColorPicker => Cursors.Cross,
                Tool.Text => Cursors.IBeam,
                _ => Cursors.Default
            };
        }

        private void DrawPoint(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= working.Width || p.Y >= working.Height) return;
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(currentTool == Tool.Erase ? Color.White : drawColor);
            g.FillEllipse(br, p.X - brushSize / 2, p.Y - brushSize / 2, brushSize, brushSize);
        }

        private void DrawLine(Point from, Point to)
        {
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(currentTool == Tool.Erase ? Color.White : drawColor, brushSize)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, from, to);
        }

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
            g.DrawString(dlg.TextContent, textFont, brush,
                new PointF(Math.Max(0, imgPt.X), Math.Max(0, imgPt.Y)));
            canvas.Invalidate();
        }

        private void ConfirmCrop()
        {
            if (MessageBox.Show(
                    $"¿Recortar a {cropRect.Width}×{cropRect.Height}?",
                    "Recortar",
                    MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                canvas.Invalidate();
                return;
            }

            PushUndo();
            var cropped = new Bitmap(cropRect.Width, cropRect.Height);
            using (var g = Graphics.FromImage(cropped))
                g.DrawImage(working,
                    new Rectangle(0, 0, cropRect.Width, cropRect.Height),
                    cropRect,
                    GraphicsUnit.Pixel);

            working.Dispose();
            working = cropped;
            cropRect = Rectangle.Empty;
            FitToWindow();
        }

        private void PickColorFromImage(Point p)
        {
            if (p.X >= 0 && p.Y >= 0 && p.X < working.Width && p.Y < working.Height)
            {
                drawColor = working.GetPixel(p.X, p.Y);
                colorSwatch.BackColor = drawColor;
            }
            SelectTool(Tool.None);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC TRANSFORMS
        // ════════════════════════════════════════════════════════════════════
        private async void RotateAsync(int degrees)
        {
            PushUndo();
            ShowLoading("Rotando...");
            SetToolsEnabled(false);

            var src = new Bitmap(working);
            var rotated = await Task.Run(() =>
            {
                src.RotateFlip(degrees == 90
                    ? RotateFlipType.Rotate90FlipNone
                    : RotateFlipType.Rotate270FlipNone);
                return src;
            });

            working.Dispose();
            working = rotated;
            HideLoading();
            SetToolsEnabled(true);
            FitToWindow();
        }

        private async void FlipAsync(bool horizontal, bool vertical)
        {
            PushUndo();
            ShowLoading("Volteando...");
            SetToolsEnabled(false);

            var src = new Bitmap(working);
            var flipped = await Task.Run(() =>
            {
                src.RotateFlip((horizontal, vertical) switch
                {
                    (true, false) => RotateFlipType.RotateNoneFlipX,
                    (false, true) => RotateFlipType.RotateNoneFlipY,
                    _ => RotateFlipType.RotateNoneFlipNone
                });
                return src;
            });

            working.Dispose();
            working = flipped;
            HideLoading();
            SetToolsEnabled(true);
            canvas.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC FILTERS — LockBits (no GetPixel/SetPixel)
        // ════════════════════════════════════════════════════════════════════
        private enum FilterType { Grayscale, Sepia, Invert }

        private async Task ApplyFilterAsync(FilterType filter)
        {
            PushUndo();
            ShowLoading("Aplicando filtro...");
            SetToolsEnabled(false);

            int w = working.Width;
            int h = working.Height;
            byte[] pixels = BitmapToBytes(working, out PixelFormat fmt);

            byte[] result = await Task.Run(() => ApplyFilterToBytes(pixels, w, h, filter));

            Bitmap newBmp = BytesToBitmap(result, w, h, fmt);
            working.Dispose();
            working = newBmp;

            HideLoading();
            SetToolsEnabled(true);
            canvas.Invalidate();
        }

        private static byte[] BitmapToBytes(Bitmap bmp, out PixelFormat fmt)
        {
            fmt = PixelFormat.Format32bppArgb;
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, fmt);
            int size = Math.Abs(bd.Stride) * bmp.Height;
            byte[] data = new byte[size];
            Marshal.Copy(bd.Scan0, data, 0, size);
            bmp.UnlockBits(bd);
            return data;
        }

        private static Bitmap BytesToBitmap(byte[] data, int w, int h, PixelFormat fmt)
        {
            var bmp = new Bitmap(w, h, fmt);
            var rect = new Rectangle(0, 0, w, h);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, fmt);
            Marshal.Copy(data, 0, bd.Scan0, data.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        private static byte[] ApplyFilterToBytes(byte[] pixels, int w, int h, FilterType filter)
        {
            // Format32bppArgb byte order: B G R A
            byte[] result = new byte[pixels.Length];
            int stride = w * 4;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    byte b = pixels[i], gch = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];

                    switch (filter)
                    {
                        case FilterType.Grayscale:
                            byte gray = (byte)(r * 0.299 + gch * 0.587 + b * 0.114);
                            result[i] = gray; result[i + 1] = gray; result[i + 2] = gray; result[i + 3] = a;
                            break;
                        case FilterType.Sepia:
                            result[i] = Clamp255((int)(r * 0.272 + gch * 0.534 + b * 0.131));
                            result[i + 1] = Clamp255((int)(r * 0.349 + gch * 0.686 + b * 0.168));
                            result[i + 2] = Clamp255((int)(r * 0.393 + gch * 0.769 + b * 0.189));
                            result[i + 3] = a;
                            break;
                        case FilterType.Invert:
                            result[i] = (byte)(255 - b);
                            result[i + 1] = (byte)(255 - gch);
                            result[i + 2] = (byte)(255 - r);
                            result[i + 3] = a;
                            break;
                    }
                }
            }
            return result;
        }

        private static byte Clamp255(int v) => (byte)Math.Max(0, Math.Min(255, v));

        // ════════════════════════════════════════════════════════════════════
        //  ZOOM / UNDO / SAVE
        // ════════════════════════════════════════════════════════════════════
        private void SetZoom(float z)
        {
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, z));
            canvas.Invalidate();
            UpdateInfo();
        }

        private void ResetZoom()
        {
            zoom = 1f;
            panOffset = Point.Empty;
            canvas.Invalidate();
        }

        private void FitToWindow()
        {
            if (working is null) return;
            int cw = canvas.Width > 0 ? canvas.Width : 800;
            int ch = canvas.Height > 0 ? canvas.Height : 600;
            zoom = Math.Min((float)cw / working.Width, (float)ch / working.Height) * 0.95f;
            panOffset = Point.Empty;
            canvas.Invalidate();
            UpdateInfo();
        }

        private void PushUndo()
        {
            if (undoStack.Count >= MaxUndoStates)
            {
                // Dispose the oldest entry before it falls off.
                var arr = undoStack.ToArray();
                arr[^1].Dispose();
            }
            undoStack.Push(new Bitmap(working));
        }

        private void Undo()
        {
            if (undoStack.Count == 0) return;
            working.Dispose();
            working = undoStack.Pop();
            canvas.Invalidate();
        }

        private void RestoreOriginal()
        {
            if (MessageBox.Show("¿Restaurar original?", "Restaurar",
                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            while (undoStack.Count > 0)
                undoStack.Pop().Dispose();

            working.Dispose();
            working = new Bitmap(original);
            FitToWindow();
        }

        private void SaveCopy()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Guardar copia",
                FileName = Path.GetFileNameWithoutExtension(imagePath) + "_editada",
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var format = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Png
                };
                working.Save(dlg.FileName, format);
                MessageBox.Show("Guardado.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error al guardar",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Add or Keys.Oemplus: SetZoom(zoom * ZoomInFactor); break;
                case Keys.Subtract or Keys.OemMinus: SetZoom(zoom * ZoomOutFactor); break;
                case Keys.Z when e.Control: Undo(); break;
                case Keys.S when e.Control: SaveCopy(); break;
                case Keys.Escape:
                    if (currentTool != Tool.None) SelectTool(Tool.None);
                    else Close();
                    break;
            }
        }

        private void UpdateInfo()
        {
            if (working is not null)
                infoLabel.Text = $"  {Path.GetFileName(imagePath)}  ·  " +
                                 $"{working.Width}×{working.Height}  ·  {zoom:P0}";
        }

        // ════════════════════════════════════════════════════════════════════
        //  LOADING OVERLAY
        // ════════════════════════════════════════════════════════════════════
        private void CenterLoadingLabel()
        {
            if (_loadingLabel is null || _loadingOverlay is null) return;
            _loadingLabel.Location = new Point(
                (_loadingOverlay.Width - _loadingLabel.Width) / 2,
                (_loadingOverlay.Height - _loadingLabel.Height) / 2);
        }

        private void ShowLoading(string text = "Procesando...")
        {
            if (InvokeRequired) { Invoke(() => ShowLoading(text)); return; }
            _loadingLabel.Text = text;
            _loadingOverlay.Visible = true;
            CenterLoadingLabel();
            _loadingOverlay.BringToFront();
        }

        private void HideLoading()
        {
            if (InvokeRequired) { Invoke(HideLoading); return; }
            _loadingOverlay.Visible = false;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SMALL HELPERS
        // ════════════════════════════════════════════════════════════════════
        private void AddTopBtn(ref int x, string text, Action click,
                               Theme.ButtonKind kind = Theme.ButtonKind.Default)
        {
            var btn = Theme.MakeButton(text, 0, kind);
            btn.Location = new Point(x, 7);
            btn.Height = 30;
            btn.Click += (_, _) => click();
            topToolbar.Controls.Add(btn);
            x += btn.Width + 3;
        }

        private Button AddLeftBtn(string text, ref int y, Action click)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(4, y),
                Size = new Size(48, 40),
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Theme.Border;
            btn.Click += (_, _) => click();
            leftToolbar.Controls.Add(btn);
            y += 46;
            return btn;
        }

        private static Label MakeGpsLabel(string text) => new()
        {
            Text = text,
            Height = 22,
            Font = Theme.FontMonoSmall,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        private static string BuildLeafletHtml(string lat, string lon, string popupText) =>
            $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>*{{margin:0;padding:0}}html,body,#map{{width:100%;height:100%;background:#121216}}</style>
</head><body><div id='map'></div><script>
var map=L.map('map',{{attributionControl:false}}).setView([{lat},{lon}],15);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19}}).addTo(map);
L.marker([{lat},{lon}]).addTo(map).bindPopup('{popupText}').openPopup();
</script></body></html>";

        private static void OpenBrowserUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageViewerForm] OpenUrl: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DISPOSE
        // ════════════════════════════════════════════════════════════════════
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                original?.Dispose();
                working?.Dispose();
                display?.Dispose();
                while (undoStack.Count > 0)
                    undoStack.Pop()?.Dispose();
                textFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GPS EDIT DIALOG
    // ════════════════════════════════════════════════════════════════════════
    internal class GpsEditDialog : Form
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double? Altitude { get; private set; }

        private TextBox _txtLat = null!;
        private TextBox _txtLon = null!;
        private TextBox _txtAlt = null!;

        public GpsEditDialog(GpsData? existing)
        {
            Text = existing?.HasGps == true ? "Editar coordenadas GPS" : "Agregar coordenadas GPS";
            Size = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
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

            AddFieldLabel("Latitud:", 14, 100);
            _txtLat = AddField(120, 98, 260,
                existing?.HasGps == true
                    ? existing.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty);

            AddFieldLabel("Longitud:", 14, 136);
            _txtLon = AddField(120, 134, 260,
                existing?.HasGps == true
                    ? existing.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty);

            AddFieldLabel("Altitud (m):", 14, 172);
            _txtAlt = AddField(120, 170, 120,
                existing?.Altitude.HasValue == true
                    ? existing.Altitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty);

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

            Controls.AddRange(new Control[] { header, lblInfo, btnSave, btnCancel });
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(_txtLat.Text.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double lat) || Math.Abs(lat) > 90)
            {
                MessageBox.Show("Latitud inválida. Debe ser un número entre -90 y 90.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (!double.TryParse(_txtLon.Text.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double lon) || Math.Abs(lon) > 180)
            {
                MessageBox.Show("Longitud inválida. Debe ser un número entre -180 y 180.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Latitude = lat;
            Longitude = lon;
            Altitude = double.TryParse(_txtAlt.Text.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double alt) ? alt : null;
        }

        private void AddFieldLabel(string text, int x, int y) =>
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 9F)
            });

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
    //  TEXT TOOL DIALOG
    // ════════════════════════════════════════════════════════════════════════
    internal class TextToolDialog : Form
    {
        public string TextContent { get; private set; } = string.Empty;
        public string SelectedFontFamily { get; private set; }
        public float SelectedFontSize { get; private set; }
        public FontStyle SelectedFontStyle { get; private set; }
        public Color SelectedColor { get; private set; }

        private TextBox _txtContent = null!;
        private ComboBox _cmbFont = null!;
        private NumericUpDown _nudSize = null!;
        private CheckBox _chkBold = null!;
        private CheckBox _chkItalic = null!;
        private CheckBox _chkUnderline = null!;
        private Panel _colorSwatch = null!;
        private Label _previewLabel = null!;
        private Color _currentColor;

        // ── Static colour palette ─────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color BgHeader = Color.FromArgb(26, 26, 32);
        private static readonly Color BorderCol = Color.FromArgb(44, 44, 54);
        private static readonly Color AccentCol = Color.FromArgb(72, 202, 188);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);

        // ── Common font families ──────────────────────────────────────────
        private static readonly string[] FontFamilies =
        {
            "Segoe UI", "Arial", "Times New Roman", "Courier New", "Verdana",
            "Georgia", "Trebuchet MS", "Impact", "Comic Sans MS", "Tahoma",
            "Calibri", "Cambria", "Consolas", "Cascadia Code", "Palatino Linotype"
        };

        public TextToolDialog(string fontFamily, float fontSize, FontStyle fontStyle, Color color)
        {
            SelectedFontFamily = fontFamily;
            SelectedFontSize = fontSize;
            SelectedFontStyle = fontStyle;
            SelectedColor = color;
            _currentColor = color;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Insertar texto";
            Size = new Size(520, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            var header = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = BgHeader };
            header.Controls.Add(new Label
            {
                Text = "✏  Insertar texto en imagen",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentCol,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            AddLabel("Texto:", 14, 58);
            _txtContent = new TextBox
            {
                Location = new Point(110, 56),
                Size = new Size(384, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            _txtContent.TextChanged += (_, _) => UpdatePreview();

            AddLabel("Fuente:", 14, 100);
            _cmbFont = new ComboBox
            {
                Location = new Point(110, 98),
                Size = new Size(230, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };
            foreach (var fam in FontFamilies) _cmbFont.Items.Add(fam);
            int fontIdx = _cmbFont.Items.IndexOf(SelectedFontFamily);
            _cmbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;
            _cmbFont.SelectedIndexChanged += (_, _) => UpdatePreview();

            AddLabel("Tamaño:", 354, 100);
            _nudSize = new NumericUpDown
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
            _nudSize.ValueChanged += (_, _) => UpdatePreview();

            AddLabel("Estilo:", 14, 142);
            _chkBold = MakeCheckBox("Negrita", 110, 140);
            _chkItalic = MakeCheckBox("Cursiva", 188, 140);
            _chkUnderline = MakeCheckBox("Subrayado", 266, 140);
            _chkBold.Checked = (SelectedFontStyle & FontStyle.Bold) != 0;
            _chkItalic.Checked = (SelectedFontStyle & FontStyle.Italic) != 0;
            _chkUnderline.Checked = (SelectedFontStyle & FontStyle.Underline) != 0;
            _chkBold.CheckedChanged += (_, _) => UpdatePreview();
            _chkItalic.CheckedChanged += (_, _) => UpdatePreview();
            _chkUnderline.CheckedChanged += (_, _) => UpdatePreview();

            AddLabel("Color:", 14, 184);
            _colorSwatch = new Panel
            {
                Location = new Point(110, 182),
                Size = new Size(44, 26),
                BackColor = _currentColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            _colorSwatch.Click += PickColor;

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
            btnColorPick.FlatAppearance.BorderColor = BorderCol;
            btnColorPick.Click += PickColor;

            var previewBox = new Panel
            {
                Location = new Point(14, 222),
                Size = new Size(480, 90),
                BackColor = Color.FromArgb(10, 10, 14),
                BorderStyle = BorderStyle.FixedSingle
            };
            _previewLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Text = "Vista previa"
            };
            previewBox.Controls.Add(_previewLabel);

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
            btnCancel.FlatAppearance.BorderColor = BorderCol;

            Controls.AddRange(new Control[]
            {
                header, _txtContent, _cmbFont, _nudSize,
                _chkBold, _chkItalic, _chkUnderline,
                _colorSwatch, btnColorPick,
                lblPreviewHint, previewBox,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            UpdatePreview();
            _txtContent.Focus();
        }

        private void PickColor(object? sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _currentColor, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _currentColor = dlg.Color;
                _colorSwatch.BackColor = _currentColor;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            try
            {
                FontStyle style = FontStyle.Regular;
                if (_chkBold?.Checked == true) style |= FontStyle.Bold;
                if (_chkItalic?.Checked == true) style |= FontStyle.Italic;
                if (_chkUnderline?.Checked == true) style |= FontStyle.Underline;

                string family = _cmbFont?.SelectedItem?.ToString() ?? "Segoe UI";
                float size = (float)(_nudSize?.Value ?? 14);

                _previewLabel.Font?.Dispose();
                _previewLabel.Font = new Font(family, Math.Min(size, 40), style);
                _previewLabel.ForeColor = _currentColor;
                _previewLabel.Text = string.IsNullOrWhiteSpace(_txtContent?.Text)
                    ? "Vista previa"
                    : _txtContent.Text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextToolDialog] UpdatePreview: {ex.Message}");
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            TextContent = _txtContent.Text;
            SelectedFontFamily = _cmbFont.SelectedItem?.ToString() ?? "Segoe UI";
            SelectedFontSize = (float)_nudSize.Value;
            SelectedColor = _currentColor;
            SelectedFontStyle = FontStyle.Regular;
            if (_chkBold.Checked) SelectedFontStyle |= FontStyle.Bold;
            if (_chkItalic.Checked) SelectedFontStyle |= FontStyle.Italic;
            if (_chkUnderline.Checked) SelectedFontStyle |= FontStyle.Underline;
        }

        private void AddLabel(string text, int x, int y) =>
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F)
            });

        private CheckBox MakeCheckBox(string text, int x, int y)
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