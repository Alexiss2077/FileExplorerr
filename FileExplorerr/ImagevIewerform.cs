using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  VISOR / EDITOR DE IMÁGENES
    //  Zoom · Rotar · Voltear · Recortar · Dibujar · Texto · Filtros · Color
    // ════════════════════════════════════════════════════════════════════════
    public class ImageViewerForm : Form
    {
        // ── Herramientas ─────────────────────────────────────────────────────
        private enum Tool { None, Crop, Draw, Erase, Text, ColorPicker }

        // ── Controles principales ────────────────────────────────────────────
        private Panel topToolbar = null!;
        private Panel leftToolbar = null!;
        private Panel canvasPanel = null!;
        private PictureBox canvas = null!;
        private Panel bottomBar = null!;
        private Label infoLabel = null!;

        // ── Panel GPS / Mapa ─────────────────────────────────────────────────
        private Panel gpsPanel = null!;
        private Label gpsLatLabel = null!;
        private Label gpsLonLabel = null!;
        private Label gpsAltLabel = null!;
        private Label gpsCameraLabel = null!;
        private Label gpsDateLabel = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private Button btnToggleGps = null!;
        private bool gpsVisible = false;

        // Grupos de botones en toolbar izquierdo
        private Button btnCrop = null!, btnDraw = null!, btnErase = null!,
                       btnText = null!, btnPicker = null!;

        // Controles de color y tamaño de pincel
        private Panel colorSwatch = null!;
        private TrackBar brushSizeBar = null!;
        private Label brushSizeLabel = null!;

        // ── Estado interno ───────────────────────────────────────────────────
        private readonly string imagePath;
        private Bitmap original = null!;   // copia intocable del original
        private Bitmap working = null!;    // bitmap sobre el que se edita
        private Bitmap display = null!;    // working escalado para mostrar

        private float zoom = 1f;
        private Point panOffset = Point.Empty;
        private Point panStart;
        private bool isPanning;

        private Tool currentTool = Tool.None;
        private Color drawColor = Color.Red;
        private int brushSize = 4;

        // Crop
        private bool isCropping;
        private Point cropStart, cropEnd;
        private Rectangle cropRect;

        // Draw / Erase
        private bool isDrawing;
        private Point lastDrawPt;

        // Texto
        private string pendingText = "";
        private Font textFont = new Font("Segoe UI", 14F, FontStyle.Bold);

        // Historial de deshacer
        private readonly Stack<Bitmap> undoStack = new();
        private const int MaxUndo = 20;

        // ════════════════════════════════════════════════════════════════════
        public ImageViewerForm(string path)
        {
            imagePath = path;
            BuildUI();
            LoadImage();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = $"Editor — {Path.GetFileName(imagePath)}";
            Size = new Size(1200, 800);
            MinimumSize = new Size(800, 550);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 14, 20);
            ForeColor = Color.FromArgb(220, 232, 248);
            KeyPreview = true;
            KeyDown += OnKeyDown;

            // ── Top toolbar ─────────────────────────────────────────────────
            topToolbar = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(17, 23, 33)
            };
            topToolbar.Paint += PaintBorder(topToolbar, bottom: true);

            int tx = 10;

            // Grupo: Archivo
            AddTopBtn(ref tx, "💾 Guardar copia", () => SaveCopy());
            AddTopSep(ref tx);

            // Grupo: Zoom
            AddTopBtn(ref tx, "🔍+", () => SetZoom(zoom * 1.25f));
            AddTopBtn(ref tx, "🔍−", () => SetZoom(zoom * 0.8f));
            AddTopBtn(ref tx, "1:1", () => ResetZoom());
            AddTopBtn(ref tx, "Ajustar", () => FitToWindow());
            AddTopSep(ref tx);

            // Grupo: Transformar
            AddTopBtn(ref tx, "↺ 90°", () => Rotate(-90));
            AddTopBtn(ref tx, "↻ 90°", () => Rotate(90));
            AddTopBtn(ref tx, "↔ Voltear H", () => Flip(true, false));
            AddTopBtn(ref tx, "↕ Voltear V", () => Flip(false, true));
            AddTopSep(ref tx);

            // Grupo: Filtros
            AddTopBtn(ref tx, "Escala grises", () => ApplyFilter(FilterType.Grayscale));
            AddTopBtn(ref tx, "Sepia", () => ApplyFilter(FilterType.Sepia));
            AddTopBtn(ref tx, "Invertir", () => ApplyFilter(FilterType.Invert));
            AddTopBtn(ref tx, "Brillo+", () => ApplyFilter(FilterType.BrightnessUp));
            AddTopBtn(ref tx, "Brillo−", () => ApplyFilter(FilterType.BrightnessDown));
            AddTopBtn(ref tx, "Contraste+", () => ApplyFilter(FilterType.ContrastUp));
            AddTopSep(ref tx);

            // Grupo: Edición
            AddTopBtn(ref tx, "↩ Deshacer", () => Undo(), Color.FromArgb(80, 50, 10), Color.FromArgb(200, 130, 30));
            AddTopBtn(ref tx, "♻ Restaurar", () => RestoreOriginal(), Color.FromArgb(60, 20, 20), Color.FromArgb(200, 60, 60));
            AddTopSep(ref tx);
            btnToggleGps = new Button
            {
                Text = "📍 GPS",
                Location = new Point(tx, 10),
                Height = 30,
                AutoSize = true,
                MinimumSize = new Size(60, 30),
                Padding = new Padding(6, 0, 6, 0),
                BackColor = Color.FromArgb(20, 60, 30),
                ForeColor = Color.FromArgb(80, 220, 120),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnToggleGps.FlatAppearance.BorderColor = Color.FromArgb(40, 160, 80);
            btnToggleGps.Click += (s, e) => ToggleGpsPanel();
            topToolbar.Controls.Add(btnToggleGps);

            // ── Left toolbar ────────────────────────────────────────────────
            leftToolbar = new Panel
            {
                Width = 64,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(14, 20, 30)
            };
            leftToolbar.Paint += PaintBorder(leftToolbar, right: true);

            int ty = 10;
            btnCrop = AddLeftBtn("✂\nRecortar", ref ty, () => SelectTool(Tool.Crop));
            btnDraw = AddLeftBtn("✏\nDibujar", ref ty, () => SelectTool(Tool.Draw));
            btnErase = AddLeftBtn("◻\nBorrador", ref ty, () => SelectTool(Tool.Erase));
            btnText = AddLeftBtn("T\nTexto", ref ty, () => SelectTool(Tool.Text));
            btnPicker = AddLeftBtn("💧\nColor", ref ty, () => SelectTool(Tool.ColorPicker));

            // Color swatch
            ty += 10;
            var swatchLabel = new Label { Text = "Color:", Left = 6, Top = ty, Width = 52, Height = 16, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(110, 140, 180), TextAlign = ContentAlignment.MiddleCenter };
            leftToolbar.Controls.Add(swatchLabel);
            ty += 18;

            colorSwatch = new Panel
            {
                Left = 8,
                Top = ty,
                Width = 48,
                Height = 28,
                BackColor = drawColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            colorSwatch.Click += (s, e) => PickColor();
            leftToolbar.Controls.Add(colorSwatch);
            ty += 36;

            // Brush size
            var bsLabel = new Label { Text = "Grosor:", Left = 4, Top = ty, Width = 56, Height = 16, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(110, 140, 180), TextAlign = ContentAlignment.MiddleCenter };
            leftToolbar.Controls.Add(bsLabel);
            ty += 17;

            brushSizeBar = new TrackBar
            {
                Left = 2,
                Top = ty,
                Width = 60,
                Height = 40,
                Minimum = 1,
                Maximum = 40,
                Value = brushSize,
                Orientation = Orientation.Horizontal,
                TickFrequency = 5,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(14, 20, 30)
            };
            brushSizeBar.ValueChanged += (s, e) => { brushSize = brushSizeBar.Value; brushSizeLabel.Text = brushSize.ToString(); };
            leftToolbar.Controls.Add(brushSizeBar);
            ty += 40;

            brushSizeLabel = new Label { Text = brushSize.ToString(), Left = 4, Top = ty, Width = 56, Height = 16, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 210, 255), TextAlign = ContentAlignment.MiddleCenter };
            leftToolbar.Controls.Add(brushSizeLabel);

            // ── Canvas ──────────────────────────────────────────────────────
            canvasPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 12, 18),
                AutoScroll = false
            };

            canvas = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Normal
            };

            // Eventos del canvas
            canvas.Paint += Canvas_Paint;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.MouseWheel += Canvas_MouseWheel;

            canvasPanel.Controls.Add(canvas);

            // ── Bottom bar ──────────────────────────────────────────────────
            bottomBar = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(17, 23, 33)
            };
            bottomBar.Paint += PaintBorder(bottomBar, top: true);

            infoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(110, 140, 180),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            bottomBar.Controls.Add(infoLabel);

            // ── GPS Panel (derecho, oculto por defecto) ──────────────────────
            gpsPanel = new Panel
            {
                Width = 320,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(12, 18, 28),
                Visible = false
            };
            gpsPanel.Paint += PaintBorder(gpsPanel, right: false);

            // Header GPS
            var gpsHeader = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(17, 26, 38)
            };
            gpsHeader.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, gpsHeader.Height - 1, gpsHeader.Width, gpsHeader.Height - 1);
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, 0, 0, gpsHeader.Height);
            };
            var gpsTitle = new Label
            {
                Text = "📍  Ubicación GPS",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 210, 120),
                TextAlign = ContentAlignment.MiddleCenter
            };
            gpsHeader.Controls.Add(gpsTitle);

            // Info labels
            var infoPanel = new Panel
            {
                Height = 160,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(14, 22, 34),
                Padding = new Padding(14, 10, 14, 10)
            };
            infoPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, infoPanel.Height - 1, infoPanel.Width, infoPanel.Height - 1);

            gpsLatLabel = MakeGpsLabel("Latitud:   —");
            gpsLonLabel = MakeGpsLabel("Longitud: —");
            gpsAltLabel = MakeGpsLabel("Altitud:   —");
            gpsCameraLabel = MakeGpsLabel("Cámara:  —");
            gpsDateLabel = MakeGpsLabel("Fecha:     —");

            gpsLatLabel.Top = 10;
            gpsLonLabel.Top = 38;
            gpsAltLabel.Top = 66;
            gpsCameraLabel.Top = 94;
            gpsDateLabel.Top = 122;
            foreach (var lbl in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel })
            {
                lbl.Left = 14; lbl.Width = 286;
                infoPanel.Controls.Add(lbl);
            }

            // Botón abrir en navegador
            var openMapBtn = new Button
            {
                Text = "🌐  Abrir en navegador",
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = Color.FromArgb(20, 55, 100),
                ForeColor = Color.FromArgb(100, 180, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            openMapBtn.FlatAppearance.BorderColor = Color.FromArgb(38, 80, 140);
            openMapBtn.Click += (s, e) => OpenInBrowser();

            // Mapa WebBrowser
            mapBrowser = new System.Windows.Forms.WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                AllowNavigation = true
            };

            gpsPanel.Controls.Add(mapBrowser);
            gpsPanel.Controls.Add(openMapBtn);
            gpsPanel.Controls.Add(infoPanel);
            gpsPanel.Controls.Add(gpsHeader);

            Controls.Add(canvasPanel);
            Controls.Add(gpsPanel);
            Controls.Add(leftToolbar);
            Controls.Add(topToolbar);
            Controls.Add(bottomBar);
        }

        // ── Helpers para crear controles ────────────────────────────────────
        private void AddTopBtn(ref int x, string text, Action click,
            Color? bg = null, Color? border = null)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 10),
                Height = 30,
                AutoSize = true,
                MinimumSize = new Size(40, 30),
                Padding = new Padding(6, 0, 6, 0),
                BackColor = bg ?? Color.FromArgb(24, 32, 46),
                ForeColor = Color.FromArgb(200, 220, 248),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = border ?? Color.FromArgb(38, 50, 70);
            btn.Click += (s, e) => click();
            topToolbar.Controls.Add(btn);
            x += btn.Width + 4;
        }

        private void AddTopSep(ref int x)
        {
            var sep = new Panel { Location = new Point(x, 12), Size = new Size(1, 26), BackColor = Color.FromArgb(38, 50, 70) };
            topToolbar.Controls.Add(sep);
            x += 9;
        }

        private Button AddLeftBtn(string text, ref int y, Action click)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(4, y),
                Size = new Size(56, 52),
                BackColor = Color.FromArgb(24, 32, 46),
                ForeColor = Color.FromArgb(200, 220, 248),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(38, 50, 70);
            btn.Click += (s, e) => click();
            leftToolbar.Controls.Add(btn);
            y += 58;
            return btn;
        }

        private static PaintEventHandler PaintBorder(Control ctrl,
            bool bottom = false, bool top = false, bool right = false)
        {
            return (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(38, 50, 70));
                if (bottom) e.Graphics.DrawLine(pen, 0, ctrl.Height - 1, ctrl.Width, ctrl.Height - 1);
                if (top) e.Graphics.DrawLine(pen, 0, 0, ctrl.Width, 0);
                if (right) e.Graphics.DrawLine(pen, ctrl.Width - 1, 0, ctrl.Width - 1, ctrl.Height);
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGA — multi-formato
        // ════════════════════════════════════════════════════════════════════

        // Todos los formatos soportados por el visor
        internal static readonly string[] SupportedExtensions =
        {
            ".jpg", ".jpeg", ".jfif", ".jpe",          // JPEG familia
            ".png",                                      // PNG
            ".gif",                                      // GIF (primer frame)
            ".bmp", ".dib",                              // BMP
            ".tiff", ".tif",                             // TIFF (primer frame)
            ".ico",                                      // Icono Windows
            ".webp",                                     // WebP (requiere codec Win10+)
            ".avif",                                     // AVIF (requiere codec Win11+)
            ".heic", ".heif",                            // HEIC/HEIF (requiere codec MS Store)
            ".emf", ".wmf",                              // Metaarchivos vectoriales
            ".svg",                                      // SVG (renderizado en WebBrowser)
            ".ppm", ".pgm", ".pbm",                      // Netpbm
            ".tga",                                      // TGA/Targa
            ".exr",                                      // OpenEXR (requiere codec)
            ".raw", ".cr2", ".cr3", ".nef", ".nrw",      // RAW Canon/Nikon (requiere codec)
            ".arw", ".srf", ".sr2",                      // RAW Sony
            ".orf",                                      // RAW Olympus
            ".rw2",                                      // RAW Panasonic
            ".dng",                                      // DNG (Adobe)
            ".pef",                                      // RAW Pentax
            ".raf",                                      // RAW Fuji
            ".3fr",                                      // RAW Hasselblad
        };

        private void LoadImage()
        {
            string ext = Path.GetExtension(imagePath).ToLower();

            try
            {
                Bitmap? bmp = null;

                // ── SVG: renderizar en WebBrowser embebido ───────────────────
                if (ext == ".svg")
                {
                    LoadSvg();
                    return;
                }

                // ── ICO: usar clase Icon para preservar todos los tamaños ────
                if (ext == ".ico")
                {
                    using var ico = new Icon(imagePath, new Size(256, 256));
                    bmp = ico.ToBitmap();
                }

                // ── TIFF multi-página: cargar primer frame ───────────────────
                else if (ext is ".tiff" or ".tif")
                {
                    using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var tmp = Image.FromStream(fs);
                    // Seleccionar primer frame de dimensión máxima
                    var dim = new System.Drawing.Imaging.FrameDimension(
                        tmp.FrameDimensionsList[0]);
                    tmp.SelectActiveFrame(dim, 0);
                    bmp = new Bitmap(tmp);
                }

                // ── GIF animado: extraer primer frame ────────────────────────
                else if (ext == ".gif")
                {
                    using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var tmp = Image.FromStream(fs);
                    bmp = new Bitmap(tmp.Width, tmp.Height);
                    using var g = Graphics.FromImage(bmp);
                    g.DrawImage(tmp, 0, 0);
                }

                // ── Metaarchivos vectoriales (EMF/WMF) ───────────────────────
                else if (ext is ".emf" or ".wmf")
                {
                    using var meta = new System.Drawing.Imaging.Metafile(imagePath);
                    int w = Math.Max(1, meta.Width); int h = Math.Max(1, meta.Height);
                    // Renderizar a 2x para mejor calidad
                    bmp = new Bitmap(w * 2, h * 2);
                    using var g = Graphics.FromImage(bmp);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawImage(meta, 0, 0, w * 2, h * 2);
                }

                // ── Todos los demás: Image.FromFile (usa codecs WIC de Windows) ─
                else
                {
                    using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var tmp = Image.FromStream(fs, true, true);
                    bmp = new Bitmap(tmp);
                }

                if (bmp == null) throw new InvalidOperationException("No se pudo decodificar la imagen.");

                original = bmp;
                working = new Bitmap(original);
                FitToWindow();
                UpdateInfo();
            }
            catch (Exception ex)
            {
                // Último intento: mostrar en WebBrowser para formatos como AVIF/HEIC si el browser los soporta
                if (TryLoadInBrowser(imagePath))
                    return;

                MessageBox.Show(
                    $"No se pudo abrir este archivo.\n\n{ex.Message}\n\n" +
                    "Algunos formatos (RAW, HEIC, AVIF) requieren codecs adicionales\n" +
                    "disponibles en la Microsoft Store.",
                    "Formato no compatible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
        }

        // ── SVG: sustituir canvas por WebBrowser ─────────────────────────────
        private void LoadSvg()
        {
            string svgContent = File.ReadAllText(imagePath);
            string html = $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<style>
  * {{margin:0;padding:0;box-sizing:border-box;}}
  html,body {{width:100%;height:100%;background:#080c12;display:flex;
             align-items:center;justify-content:center;overflow:hidden;}}
  svg {{max-width:100%;max-height:100%;}}
</style>
</head><body>
{svgContent}
</body></html>";

            // Reemplazar canvas por WebBrowser para SVG
            var svgBrowser = new System.Windows.Forms.WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false,
                IsWebBrowserContextMenuEnabled = false
            };
            canvasPanel.Controls.Remove(canvas);
            canvasPanel.Controls.Add(svgBrowser);
            svgBrowser.DocumentText = html;

            // Deshabilitar herramientas de dibujo para SVG
            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                btn.Enabled = false;

            var fi = new FileInfo(imagePath);
            infoLabel!.Text = $"  {fi.Name}   ·   SVG vectorial   ·   {FormatSize(fi.Length)}";
        }

        // Fallback: abrir en WebBrowser embebido (funciona para HEIC/AVIF en Edge-based systems)
        private bool TryLoadInBrowser(string path)
        {
            try
            {
                var fb = new System.Windows.Forms.WebBrowser
                {
                    Dock = DockStyle.Fill,
                    ScrollBarsEnabled = false,
                    IsWebBrowserContextMenuEnabled = false
                };
                canvasPanel.Controls.Remove(canvas);
                canvasPanel.Controls.Add(fb);
                fb.Navigate(path);

                foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                    btn.Enabled = false;

                var fi = new FileInfo(path);
                infoLabel!.Text = $"  {fi.Name}   ·   {FormatSize(fi.Length)}   ·   (modo compatibilidad)";
                return true;
            }
            catch { return false; }
        }

        private static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        private static Label MakeGpsLabel(string text) => new Label
        {
            Text = text,
            Height = 24,
            Font = new Font("Cascadia Code", 8.5F),
            ForeColor = Color.FromArgb(180, 210, 255),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private GpsReader.GpsData? _gpsData;

        private void ToggleGpsPanel()
        {
            gpsVisible = !gpsVisible;
            gpsPanel.Visible = gpsVisible;
            btnToggleGps.BackColor = gpsVisible
                ? Color.FromArgb(20, 100, 50)
                : Color.FromArgb(20, 60, 30);

            if (gpsVisible && _gpsData == null)
                LoadGps();
        }

        private void LoadGps()
        {
            _gpsData = GpsReader.Read(imagePath);

            if (_gpsData == null || !_gpsData.HasGps)
            {
                gpsLatLabel.Text = "Sin datos GPS en este archivo";
                gpsLatLabel.ForeColor = Color.FromArgb(160, 100, 60);
                gpsLonLabel.Visible = gpsAltLabel.Visible = gpsCameraLabel.Visible = gpsDateLabel.Visible = false;
                mapBrowser.DocumentText = "<html><body style='background:#0a0e14;color:#5a7090;font-family:Segoe UI;" +
                    "display:flex;align-items:center;justify-content:center;height:100vh;margin:0;font-size:14px'>" +
                    "<div style='text-align:center'>📭<br><br>Sin coordenadas GPS<br>en este archivo</div></body></html>";
                return;
            }

            var g = _gpsData;
            gpsLatLabel.Text = $"Lat:     {g.LatString}";
            gpsLonLabel.Text = $"Lon:    {g.LonString}";
            gpsAltLabel.Text = g.Altitude.HasValue ? $"Alt:     {g.Altitude.Value:0.0} m" : "Alt:     —";
            gpsCameraLabel.Text = $"Cámara: {g.CameraModel ?? "—"}";
            gpsDateLabel.Text = $"Fecha:  {g.Date ?? "—"}";

            foreach (var lbl in new[] { gpsLatLabel, gpsLonLabel, gpsAltLabel, gpsCameraLabel, gpsDateLabel })
            {
                lbl.ForeColor = Color.FromArgb(180, 210, 255);
                lbl.Visible = true;
            }

            LoadMap(g.Latitude, g.Longitude);
        }

        private void LoadMap(double lat, double lon)
        {
            // Fijar modo IE11 en el registro para el proceso actual
            SetBrowserEmulation();

            string html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<meta name='viewport' content='width=device-width, initial-scale=1'/>
<title>Map</title>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  html, body, #map {{ width:100%; height:100%; background:#0d1117; }}
</style>
</head>
<body>
<div id='map'></div>
<script>
  var map = L.map('map', {{ zoomControl:true, attributionControl:false }})
             .setView([{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                       {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}], 15);

  L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
    maxZoom: 19
  }}).addTo(map);

  var icon = L.divIcon({{
    html: '<div style=""width:22px;height:22px;border-radius:50% 50% 50% 0;background:#3a8bfd;border:3px solid #fff;transform:rotate(-45deg);box-shadow:0 2px 8px rgba(0,0,0,.5)""></div>',
    iconSize: [22, 22],
    iconAnchor: [11, 22],
    className: ''
  }});

  L.marker([{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},
             {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}], {{icon: icon}})
    .addTo(map)
    .bindPopup('<b>📍 Ubicación</b><br>{lat:F6}°, {lon:F6}°')
    .openPopup();
</script>
</body>
</html>";

            mapBrowser.DocumentText = html;
        }

        private void OpenInBrowser()
        {
            if (_gpsData == null || !_gpsData.HasGps) return;
            string url = $"https://www.google.com/maps?q={_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        // Necesario para que WebBrowser use IE11 y cargue Leaflet correctamente
        private static void SetBrowserEmulation()
        {
            try
            {
                string appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
                    ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                key?.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { /* Sin permisos de registro — el mapa puede mostrarse en modo compat */ }
        }
        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            if (working == null) return;
            var g = e.Graphics;
            g.InterpolationMode = zoom >= 1
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int dw = (int)(working.Width * zoom);
            int dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2;
            int oy = panOffset.Y + (canvas.Height - dh) / 2;

            // Cuadrícula de transparencia (fondo)
            DrawCheckerboard(g, ox, oy, dw, dh);

            g.DrawImage(working, ox, oy, dw, dh);

            // Rectángulo de recorte en progreso
            if (isCropping && cropRect.Width > 0 && cropRect.Height > 0)
            {
                int rx = (int)(cropRect.X * zoom) + ox;
                int ry = (int)(cropRect.Y * zoom) + oy;
                int rw = (int)(cropRect.Width * zoom);
                int rh = (int)(cropRect.Height * zoom);

                using var overlay = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                // Oscurecer fuera del rectángulo
                g.FillRectangle(overlay, ox, oy, rw == 0 ? dw : rx - ox, dh);
                g.FillRectangle(overlay, rx + rw, oy, dw - (rx - ox + rw), dh);
                g.FillRectangle(overlay, rx, oy, rw, ry - oy);
                g.FillRectangle(overlay, rx, ry + rh, rw, dh - (ry - oy + rh));

                using var borderPen = new Pen(Color.FromArgb(56, 139, 253), 2) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(borderPen, rx, ry, rw, rh);

                // Handles esquina
                using var hBrush = new SolidBrush(Color.FromArgb(56, 139, 253));
                int hs = 8;
                g.FillRectangle(hBrush, rx - hs / 2, ry - hs / 2, hs, hs);
                g.FillRectangle(hBrush, rx + rw - hs / 2, ry - hs / 2, hs, hs);
                g.FillRectangle(hBrush, rx - hs / 2, ry + rh - hs / 2, hs, hs);
                g.FillRectangle(hBrush, rx + rw - hs / 2, ry + rh - hs / 2, hs, hs);
            }
        }

        private static void DrawCheckerboard(Graphics g, int x, int y, int w, int h)
        {
            int sz = 12;
            for (int row = 0; row * sz < h; row++)
                for (int col = 0; col * sz < w; col++)
                {
                    bool light = (row + col) % 2 == 0;
                    using var b = new SolidBrush(light ? Color.FromArgb(40, 40, 45) : Color.FromArgb(28, 28, 32));
                    g.FillRectangle(b, x + col * sz, y + row * sz, sz, sz);
                }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOUSE
        // ════════════════════════════════════════════════════════════════════
        private Point CanvasToImage(Point p)
        {
            int dw = (int)(working.Width * zoom);
            int dh = (int)(working.Height * zoom);
            int ox = panOffset.X + (canvas.Width - dw) / 2;
            int oy = panOffset.Y + (canvas.Height - dh) / 2;
            return new Point(
                (int)((p.X - ox) / zoom),
                (int)((p.Y - oy) / zoom));
        }

        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            var imgPt = CanvasToImage(e.Location);

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && currentTool == Tool.None))
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
                    cropStart = imgPt;
                    cropEnd = imgPt;
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
            if (working == null) return;

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
                int w = Math.Min(working.Width - x, Math.Abs(cropEnd.X - cropStart.X));
                int h = Math.Min(working.Height - y, Math.Abs(cropEnd.Y - cropStart.Y));
                cropRect = new Rectangle(x, y, w, h);
                canvas.Invalidate();
                UpdateInfo($"Recorte: {w} × {h} px");
                return;
            }

            if (isDrawing && (currentTool == Tool.Draw || currentTool == Tool.Erase))
            {
                DrawLine(lastDrawPt, imgPt);
                lastDrawPt = imgPt;
                canvas.Invalidate();
            }

            // Info de cursor
            if (imgPt.X >= 0 && imgPt.Y >= 0 && imgPt.X < working.Width && imgPt.Y < working.Height)
            {
                var px = working.GetPixel(imgPt.X, imgPt.Y);
                UpdateInfo($"X:{imgPt.X} Y:{imgPt.Y}  |  R:{px.R} G:{px.G} B:{px.B}");
            }
        }

        private void Canvas_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isPanning) { isPanning = false; canvas.Cursor = Cursors.Default; return; }

            if (isCropping && e.Button == MouseButtons.Left)
            {
                isCropping = false;
                if (cropRect.Width > 4 && cropRect.Height > 4)
                    ConfirmCrop();
                else
                    canvas.Invalidate();
            }

            if (isDrawing) { isDrawing = false; canvas.Invalidate(); }
        }

        private void Canvas_MouseWheel(object? sender, MouseEventArgs e)
        {
            float factor = e.Delta > 0 ? 1.15f : 0.87f;
            SetZoom(zoom * factor);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HERRAMIENTAS
        // ════════════════════════════════════════════════════════════════════
        private void SelectTool(Tool t)
        {
            currentTool = currentTool == t ? Tool.None : t;
            isCropping = false;
            canvas.Invalidate();

            // Resaltar botón activo
            foreach (var btn in new[] { btnCrop, btnDraw, btnErase, btnText, btnPicker })
                btn.BackColor = Color.FromArgb(24, 32, 46);

            Button? active = currentTool switch
            {
                Tool.Crop => btnCrop,
                Tool.Draw => btnDraw,
                Tool.Erase => btnErase,
                Tool.Text => btnText,
                Tool.ColorPicker => btnPicker,
                _ => null
            };
            if (active != null)
                active.BackColor = Color.FromArgb(31, 70, 140);

            canvas.Cursor = currentTool switch
            {
                Tool.Draw or Tool.Erase => Cursors.Cross,
                Tool.Text => Cursors.IBeam,
                Tool.ColorPicker => Cursors.Cross,
                Tool.Crop => Cursors.Cross,
                _ => Cursors.Default
            };
        }

        // ── Dibujar / Borrador ───────────────────────────────────────────────
        private void DrawPoint(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= working.Width || p.Y >= working.Height) return;
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = currentTool == Tool.Erase ? Color.White : drawColor;
            using var br = new SolidBrush(c);
            int half = brushSize / 2;
            g.FillEllipse(br, p.X - half, p.Y - half, brushSize, brushSize);
        }

        private void DrawLine(Point from, Point to)
        {
            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = currentTool == Tool.Erase ? Color.White : drawColor;
            using var pen = new Pen(c, brushSize) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            // Clip al área de la imagen
            g.Clip = new Region(new Rectangle(0, 0, working.Width, working.Height));
            g.DrawLine(pen, from, to);
        }

        // ── Texto ────────────────────────────────────────────────────────────
        private void PlaceText(Point imgPt)
        {
            string? txt = InputDialog("Agregar texto", "Escribe el texto:");
            if (string.IsNullOrWhiteSpace(txt)) return;

            using var fontDlg = new FontDialog { Font = textFont, Color = drawColor, ShowColor = true };
            if (fontDlg.ShowDialog(this) == DialogResult.OK)
            {
                textFont = fontDlg.Font;
                drawColor = fontDlg.Color;
                colorSwatch.BackColor = drawColor;
            }

            using var g = Graphics.FromImage(working);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(drawColor);
            // Clip al área de la imagen
            var pt = new PointF(
                Math.Max(0, Math.Min(imgPt.X, working.Width - 4)),
                Math.Max(0, Math.Min(imgPt.Y, working.Height - 4)));
            g.DrawString(txt, textFont, brush, pt);
            canvas.Invalidate();
        }

        // ── Recortar ─────────────────────────────────────────────────────────
        private void ConfirmCrop()
        {
            var result = MessageBox.Show(
                $"¿Recortar a {cropRect.Width} × {cropRect.Height} px?",
                "Confirmar recorte", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes) { canvas.Invalidate(); return; }

            PushUndo();
            var cropped = new Bitmap(cropRect.Width, cropRect.Height);
            using (var g = Graphics.FromImage(cropped))
                g.DrawImage(working, new Rectangle(0, 0, cropRect.Width, cropRect.Height),
                    cropRect, GraphicsUnit.Pixel);

            working.Dispose();
            working = cropped;
            cropRect = Rectangle.Empty;
            FitToWindow();
            canvas.Invalidate();
            UpdateInfo();
        }

        // ── Color picker ─────────────────────────────────────────────────────
        private void PickColorFromImage(Point imgPt)
        {
            if (imgPt.X < 0 || imgPt.Y < 0 || imgPt.X >= working.Width || imgPt.Y >= working.Height) return;
            drawColor = working.GetPixel(imgPt.X, imgPt.Y);
            colorSwatch.BackColor = drawColor;
            SelectTool(Tool.None);
        }

        private void PickColor()
        {
            using var dlg = new ColorDialog { Color = drawColor, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                drawColor = dlg.Color;
                colorSwatch.BackColor = drawColor;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TRANSFORMACIONES
        // ════════════════════════════════════════════════════════════════════
        private void Rotate(int degrees)
        {
            PushUndo();
            var rotType = degrees == 90
                ? RotateFlipType.Rotate90FlipNone
                : RotateFlipType.Rotate270FlipNone;
            working.RotateFlip(rotType);
            FitToWindow();
        }

        private void Flip(bool horizontal, bool vertical)
        {
            PushUndo();
            var flipType = (horizontal, vertical) switch
            {
                (true, false) => RotateFlipType.RotateNoneFlipX,
                (false, true) => RotateFlipType.RotateNoneFlipY,
                (true, true) => RotateFlipType.RotateNoneFlipXY,
                _ => RotateFlipType.RotateNoneFlipNone
            };
            working.RotateFlip(flipType);
            canvas.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTROS DE COLOR
        // ════════════════════════════════════════════════════════════════════
        private enum FilterType { Grayscale, Sepia, Invert, BrightnessUp, BrightnessDown, ContrastUp }

        private void ApplyFilter(FilterType filter)
        {
            PushUndo();
            var bmp = new Bitmap(working.Width, working.Height);

            for (int y = 0; y < working.Height; y++)
            {
                for (int x = 0; x < working.Width; x++)
                {
                    Color c = working.GetPixel(x, y);
                    Color nc = filter switch
                    {
                        FilterType.Grayscale => GrayPixel(c),
                        FilterType.Sepia => SepiaPixel(c),
                        FilterType.Invert => Color.FromArgb(c.A, 255 - c.R, 255 - c.G, 255 - c.B),
                        FilterType.BrightnessUp => Clamp(c, 30, 30, 30),
                        FilterType.BrightnessDown => Clamp(c, -30, -30, -30),
                        FilterType.ContrastUp => ContrastPixel(c, 1.4f),
                        _ => c
                    };
                    bmp.SetPixel(x, y, nc);
                }
            }

            working.Dispose();
            working = bmp;
            canvas.Invalidate();
        }

        private static Color GrayPixel(Color c)
        {
            int g = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
            return Color.FromArgb(c.A, g, g, g);
        }

        private static Color SepiaPixel(Color c)
        {
            int r = Clamp255((int)(c.R * 0.393 + c.G * 0.769 + c.B * 0.189));
            int g = Clamp255((int)(c.R * 0.349 + c.G * 0.686 + c.B * 0.168));
            int b = Clamp255((int)(c.R * 0.272 + c.G * 0.534 + c.B * 0.131));
            return Color.FromArgb(c.A, r, g, b);
        }

        private static Color Clamp(Color c, int dr, int dg, int db) =>
            Color.FromArgb(c.A, Clamp255(c.R + dr), Clamp255(c.G + dg), Clamp255(c.B + db));

        private static Color ContrastPixel(Color c, float factor)
        {
            int r = Clamp255((int)((c.R - 128) * factor + 128));
            int g = Clamp255((int)((c.G - 128) * factor + 128));
            int b = Clamp255((int)((c.B - 128) * factor + 128));
            return Color.FromArgb(c.A, r, g, b);
        }

        private static int Clamp255(int v) => Math.Max(0, Math.Min(255, v));

        // ════════════════════════════════════════════════════════════════════
        //  ZOOM & PAN
        // ════════════════════════════════════════════════════════════════════
        private void SetZoom(float z)
        {
            zoom = Math.Max(0.05f, Math.Min(20f, z));
            canvas.Invalidate();
            UpdateInfo();
        }

        private void ResetZoom()
        {
            zoom = 1f;
            panOffset = Point.Empty;
            canvas.Invalidate();
            UpdateInfo();
        }

        private void FitToWindow()
        {
            if (working == null) return;
            float zx = (float)(canvas.Width > 0 ? canvas.Width : 800) / working.Width;
            float zy = (float)(canvas.Height > 0 ? canvas.Height : 600) / working.Height;
            zoom = Math.Min(zx, zy) * 0.95f;
            panOffset = Point.Empty;
            canvas.Invalidate();
            UpdateInfo();
        }

        // ════════════════════════════════════════════════════════════════════
        //  HISTORIAL
        // ════════════════════════════════════════════════════════════════════
        private void PushUndo()
        {
            if (undoStack.Count >= MaxUndo) { var old = undoStack.ToArray()[^1]; old.Dispose(); }
            undoStack.Push(new Bitmap(working));
        }

        private void Undo()
        {
            if (undoStack.Count == 0) { UpdateInfo("Sin acciones para deshacer"); return; }
            working.Dispose();
            working = undoStack.Pop();
            canvas.Invalidate();
            UpdateInfo();
        }

        private void RestoreOriginal()
        {
            if (MessageBox.Show("¿Restaurar la imagen original? Se perderán todos los cambios.",
                    "Restaurar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            undoStack.Clear();
            working.Dispose();
            working = new Bitmap(original);
            FitToWindow();
        }

        // ════════════════════════════════════════════════════════════════════
        //  GUARDAR
        // ════════════════════════════════════════════════════════════════════
        private void SaveCopy()
        {
            string dir = Path.GetDirectoryName(imagePath)!;
            string name = Path.GetFileNameWithoutExtension(imagePath);

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar copia editada",
                InitialDirectory = dir,
                FileName = $"{name}_editada",
                Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp|Todos|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string ext = Path.GetExtension(dlg.FileName).ToLower();
                ImageFormat fmt = ext switch
                {
                    ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                    ".bmp" => ImageFormat.Bmp,
                    _ => ImageFormat.Png
                };
                working.Save(dlg.FileName, fmt);
                MessageBox.Show($"Imagen guardada:\n{dlg.FileName}", "Guardado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TECLADO
        // ════════════════════════════════════════════════════════════════════
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Add or Keys.Oemplus:
                    SetZoom(zoom * 1.15f); break;
                case Keys.Subtract or Keys.OemMinus:
                    SetZoom(zoom * 0.87f); break;
                case Keys.D0 or Keys.NumPad0 when e.Control:
                    FitToWindow(); break;
                case Keys.Z when e.Control:
                    Undo(); break;
                case Keys.S when e.Control:
                    SaveCopy(); break;
                case Keys.Escape:
                    if (currentTool != Tool.None) SelectTool(Tool.None);
                    else Close();
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private void UpdateInfo(string? extra = null)
        {
            if (working == null) return;
            var fi = new FileInfo(imagePath);
            string base_ = $"  {fi.Name}   ·   {working.Width} × {working.Height} px   ·   Zoom {zoom:P0}   ·   " +
                           $"Ctrl+Z deshacer  ·  Ctrl+S guardar  ·  Esc limpiar herramienta";
            infoLabel.Text = extra != null ? $"  {extra}" : base_;
        }

        private string? InputDialog(string title, string prompt)
        {
            using var dlg = new Form
            {
                Text = title,
                Width = 400,
                Height = 150,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(17, 23, 33),
                ForeColor = Color.FromArgb(220, 232, 248)
            };
            var lbl = new Label { Text = prompt, Left = 12, Top = 14, Width = 370, ForeColor = Color.FromArgb(110, 140, 180), Font = new Font("Segoe UI", 9.5F) };
            var txt = new TextBox { Left = 12, Top = 38, Width = 372, BackColor = Color.FromArgb(24, 32, 46), ForeColor = Color.FromArgb(220, 232, 248), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F) };
            var ok = new Button { Text = "OK", Left = 210, Top = 76, Width = 82, Height = 30, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(31, 90, 180), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderColor = Color.FromArgb(56, 139, 253);
            var cancel = new Button { Text = "Cancelar", Left = 300, Top = 76, Width = 84, Height = 30, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(24, 32, 46), ForeColor = Color.FromArgb(220, 232, 248), FlatStyle = FlatStyle.Flat };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(38, 50, 70);
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok; dlg.CancelButton = cancel;
            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                original?.Dispose();
                working?.Dispose();
                display?.Dispose();
                foreach (var b in undoStack) b?.Dispose();
                undoStack.Clear();
                textFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}