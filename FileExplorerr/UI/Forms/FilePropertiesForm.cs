using System;
using System.Drawing;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public class FilePropertiesForm : Form
    {
        private readonly string _path;
        private readonly bool   _isDirectory;

        private Panel _loadingPanel = null!;
        private Label _loadingLabel = null!;

        public FilePropertiesForm(string path)
        {
            _path        = path;
            _isDirectory = Directory.Exists(path);
            BuildUI();
            _ = LoadPropertiesAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text            = $"Propiedades — {Path.GetFileName(_path)}";
            Size            = new Size(480, 620);
            MinimumSize     = new Size(420, 520);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.FromArgb(18, 18, 22);
            ForeColor       = Color.FromArgb(230, 230, 236);
            Font            = new Font("Segoe UI", 9.5F);

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel
            {
                Height    = 72,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(26, 26, 32),
                Padding   = new Padding(16, 0, 0, 0)
            };

            string emoji    = _isDirectory ? "📁" : GetFileEmoji(Path.GetExtension(_path));
            string fileName = Path.GetFileName(_path);
            if (string.IsNullOrEmpty(fileName)) fileName = _path;

            var iconLabel = new Label
            {
                Text      = emoji,
                Font      = new Font("Segoe UI", 26F),
                Size      = new Size(54, 54),
                Location  = new Point(14, 10),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var nameLabel = new Label
            {
                Text        = fileName,
                Font        = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor   = Color.FromArgb(230, 230, 236),
                Location    = new Point(76, 14),
                Size        = new Size(370, 24),
                AutoEllipsis = true
            };
            var typeLabel = new Label
            {
                Text      = _isDirectory ? "Carpeta" : DescribeType(Path.GetExtension(_path)),
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 140, 180),
                Location  = new Point(76, 40),
                Size      = new Size(370, 18)
            };
            header.Controls.AddRange(new Control[] { iconLabel, nameLabel, typeLabel });

            var divider = new Panel
            {
                Height    = 1,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(44, 44, 54)
            };

            // ── Scrollable content area (populated asynchronously) ────────────
            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.FromArgb(18, 18, 22),
                Padding    = new Padding(16, 12, 16, 12),
                Tag        = "scroll"
            };

            // ── Loading overlay ───────────────────────────────────────────────
            _loadingPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 22),
                Visible   = true
            };
            _loadingLabel = new Label
            {
                Text      = "Cargando propiedades...",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(100, 140, 180),
                Font      = new Font("Segoe UI", 9.5F)
            };
            _loadingPanel.Controls.Add(_loadingLabel);

            // ── Bottom bar ────────────────────────────────────────────────────
            var bottomPanel = new Panel
            {
                Height    = 48,
                Dock      = DockStyle.Bottom,
                BackColor = Color.FromArgb(26, 26, 32),
                Padding   = new Padding(8)
            };

            var btnClose = new Button
            {
                Text      = "Cerrar",
                Size      = new Size(90, 32),
                BackColor = Color.FromArgb(34, 34, 42),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5F),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top
            };
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(54, 54, 64);
            btnClose.Click += (_, _) => Close();
            btnClose.Location = new Point(bottomPanel.Width - 106, 8);
            bottomPanel.Resize += (_, _) => btnClose.Location = new Point(bottomPanel.Width - 106, 8);

            var hintLabel = new Label
            {
                Text      = "Doble clic en un valor para copiarlo",
                Left      = 12,
                Top       = 16,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(70, 70, 88)
            };
            bottomPanel.Controls.AddRange(new Control[] { btnClose, hintLabel });

            Controls.Add(scroll);
            Controls.Add(_loadingPanel);
            Controls.Add(divider);
            Controls.Add(header);
            Controls.Add(bottomPanel);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC DATA LOAD
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadPropertiesAsync()
        {
            var rows = new System.Collections.Generic.List<(string Key, string Value, Color? Color)>();

            try
            {
                await Task.Run(() => CollectProperties(rows));
            }
            catch (Exception ex)
            {
                rows.Add(("Error", ex.Message, Color.FromArgb(220, 95, 85)));
            }

            if (!IsHandleCreated || IsDisposed) return;

            Panel? scroll = null;
            foreach (Control c in Controls)
                if (c is Panel p && p.Tag?.ToString() == "scroll") { scroll = p; break; }

            if (scroll is null) return;

            scroll.SuspendLayout();
            int y = 10;

            foreach (var (key, value, color) in rows)
            {
                if (key == "---")
                {
                    var sep = new Panel
                    {
                        Left      = 0,
                        Top       = y + 4,
                        Width     = 430,
                        Height    = 1,
                        BackColor = Color.FromArgb(38, 38, 48)
                    };
                    scroll.Controls.Add(sep);
                    y += 14;
                    continue;
                }

                if (key.StartsWith("##"))
                {
                    var sectionLabel = new Label
                    {
                        Text      = key[2..],
                        Left      = 0,
                        Top       = y,
                        Width     = 430,
                        Height    = 22,
                        Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(72, 202, 188)
                    };
                    scroll.Controls.Add(sectionLabel);
                    y += 26;
                    continue;
                }

                var lblKey = new Label
                {
                    Text      = key,
                    Left      = 0,
                    Top       = y,
                    Width     = 140,
                    Height    = 20,
                    Font      = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(100, 130, 170)
                };
                var lblValue = new Label
                {
                    Text        = value,
                    Left        = 148,
                    Top         = y,
                    Width       = 282,
                    Height      = 20,
                    Font        = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor   = color ?? Color.FromArgb(220, 220, 230),
                    AutoEllipsis = true,
                    Cursor      = Cursors.IBeam
                };
                lblValue.DoubleClick += (_, _) =>
                {
                    Clipboard.SetText(lblValue.Text);
                    ShowCopiedToast(lblValue.Text);
                };

                scroll.Controls.AddRange(new Control[] { lblKey, lblValue });
                y += 24;
            }

            scroll.ResumeLayout(true);
            _loadingPanel.Visible = false;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PROPERTY COLLECTION (runs on background thread)
        // ════════════════════════════════════════════════════════════════════
        private void CollectProperties(System.Collections.Generic.List<(string, string, Color?)> rows)
        {
            void Add(string k, string v, Color? c = null) => rows.Add((k, v, c));
            void Sep() => rows.Add(("---", string.Empty, null));
            void Section(string t) => rows.Add(("##" + t, string.Empty, null));

            Section("General");

            if (_isDirectory)
            {
                CollectDirectoryProperties(rows, Add, Sep, Section);
            }
            else
            {
                CollectFileProperties(rows, Add, Sep, Section);
            }
        }

        private void CollectDirectoryProperties(
            System.Collections.Generic.List<(string, string, Color?)> rows,
            Action<string, string, Color?> add,
            Action sep,
            Action<string> section)
        {
            var di = new DirectoryInfo(_path);
            add("Nombre",       di.Name, null);
            add("Tipo",         "Carpeta de archivos", null);
            add("Ubicación",    di.Parent?.FullName ?? _path, null);
            add("Ruta completa", di.FullName, null);
            sep();

            section("Contenido");
            var (fileCount, dirCount, totalSize) = CountContents(di);
            add("Archivos",    fileCount.ToString("N0"), null);
            add("Subcarpetas", dirCount.ToString("N0"), null);
            add("Tamaño total",
                $"{FileSize.Format(totalSize)}  ({totalSize:N0} bytes)",
                Color.FromArgb(140, 210, 170));
            sep();

            section("Fechas");
            add("Creación",      di.CreationTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            add("Modificación",  di.LastWriteTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            add("Último acceso", di.LastAccessTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            sep();

            section("Atributos");
            add("Oculta",     di.Attributes.HasFlag(FileAttributes.Hidden)   ? "Sí" : "No", null);
            add("Solo lectura", di.Attributes.HasFlag(FileAttributes.ReadOnly) ? "Sí" : "No", null);
            add("Sistema",    di.Attributes.HasFlag(FileAttributes.System)   ? "Sí" : "No", null);
        }

        private void CollectFileProperties(
            System.Collections.Generic.List<(string, string, Color?)> rows,
            Action<string, string, Color?> add,
            Action sep,
            Action<string> section)
        {
            var fi  = new FileInfo(_path);
            string ext = fi.Extension.ToLowerInvariant();

            add("Nombre",        fi.Name, null);
            add("Extensión",     string.IsNullOrEmpty(fi.Extension) ? "(sin extensión)" : fi.Extension.ToUpper(), null);
            add("Tipo",          DescribeType(ext), null);
            add("Ubicación",     fi.DirectoryName ?? string.Empty, null);
            add("Ruta completa", fi.FullName, null);
            sep();

            section("Tamaño");
            add("En disco",   FileSize.Format(fi.Length), Color.FromArgb(140, 210, 170));
            add("Bytes exactos", $"{fi.Length:N0} bytes", null);
            add("Kilobytes",  $"{fi.Length / 1024.0:N2} KB", null);
            add("Megabytes",  $"{fi.Length / 1_048_576.0:N4} MB", null);
            sep();

            section("Fechas");
            add("Creación",      fi.CreationTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            add("Modificación",  fi.LastWriteTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            add("Último acceso", fi.LastAccessTime.ToString("dd/MM/yyyy   HH:mm:ss"), null);
            sep();

            section("Atributos");
            add("Solo lectura", fi.IsReadOnly ? "Sí" : "No",
                fi.IsReadOnly ? Color.FromArgb(220, 160, 80) : null);
            add("Oculto",     fi.Attributes.HasFlag(FileAttributes.Hidden)     ? "Sí" : "No", null);
            add("Sistema",    fi.Attributes.HasFlag(FileAttributes.System)     ? "Sí" : "No", null);
            add("Archivo",    fi.Attributes.HasFlag(FileAttributes.Archive)    ? "Sí" : "No", null);
            add("Comprimido", fi.Attributes.HasFlag(FileAttributes.Compressed) ? "Sí" : "No", null);
            add("Cifrado",    fi.Attributes.HasFlag(FileAttributes.Encrypted)  ? "Sí" : "No",
                fi.Attributes.HasFlag(FileAttributes.Encrypted)
                    ? Color.FromArgb(220, 160, 80) : null);
            sep();

            // Type-specific metadata.
            switch (FileExtensions.Categorise(ext))
            {
                case FileCategory.Image:    LoadImageProps(fi, rows);  break;
                case FileCategory.Audio:    LoadAudioProps(fi, rows);  break;
                case FileCategory.Video:    LoadVideoProps(fi, rows);  break;
                case FileCategory.Text:     LoadTextProps(fi, rows);   break;
            }

            section("Seguridad");
            try
            {
                var acl   = fi.GetAccessControl();
                var owner = acl.GetOwner(typeof(NTAccount));
                add("Propietario", owner?.ToString() ?? "Desconocido", null);
            }
            catch (UnauthorizedAccessException)
            {
                add("Propietario", "Sin acceso", null);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TYPE-SPECIFIC PROPERTY LOADERS
        // ════════════════════════════════════════════════════════════════════
        private static void LoadImageProps(
            FileInfo fi,
            System.Collections.Generic.List<(string, string, Color?)> rows)
        {
            rows.Add(("##Imagen", string.Empty, null));
            try
            {
                using var fs  = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = System.Drawing.Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false);
                rows.Add(("Resolución",   $"{img.Width} × {img.Height} px", null));
                rows.Add(("DPI horizontal", $"{img.HorizontalResolution:0} dpi", null));
                rows.Add(("DPI vertical",   $"{img.VerticalResolution:0} dpi", null));
                rows.Add(("Formato",       img.RawFormat.ToString(), null));
                rows.Add(("Profundidad",   $"{System.Drawing.Image.GetPixelFormatSize(img.PixelFormat)} bpp", null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilePropertiesForm] Image metadata: {ex.Message}");
                rows.Add(("Resolución", "No disponible", null));
            }
            rows.Add(("---", string.Empty, null));
        }

        private static void LoadAudioProps(
            FileInfo fi,
            System.Collections.Generic.List<(string, string, Color?)> rows)
        {
            rows.Add(("##Audio", string.Empty, null));
            try
            {
                var tag = TagLib.File.Create(fi.FullName);
                rows.Add(("Título",  string.IsNullOrWhiteSpace(tag.Tag.Title)          ? "—" : tag.Tag.Title, null));
                rows.Add(("Artista", string.IsNullOrWhiteSpace(tag.Tag.FirstPerformer) ? "—" : tag.Tag.FirstPerformer, null));
                rows.Add(("Álbum",   string.IsNullOrWhiteSpace(tag.Tag.Album)          ? "—" : tag.Tag.Album, null));
                rows.Add(("Año",     tag.Tag.Year == 0 ? "—" : tag.Tag.Year.ToString(), null));
                rows.Add(("Género",  tag.Tag.Genres?.Length > 0 ? tag.Tag.Genres[0] : "—", null));
                rows.Add(("Pista #", tag.Tag.Track == 0 ? "—" : tag.Tag.Track.ToString(), null));
                rows.Add(("Duración",    TimeSpanFormat.Format(tag.Properties.Duration), null));
                rows.Add(("Bitrate",     $"{tag.Properties.AudioBitrate} kbps", null));
                rows.Add(("Sample rate", $"{tag.Properties.AudioSampleRate} Hz", null));
                rows.Add(("Canales", tag.Properties.AudioChannels == 1 ? "Mono"
                                   : tag.Properties.AudioChannels == 2 ? "Estéreo"
                                   : $"{tag.Properties.AudioChannels} canales", null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilePropertiesForm] Audio metadata: {ex.Message}");
                rows.Add(("Metadatos", "No disponibles", null));
            }
            rows.Add(("---", string.Empty, null));
        }

        private static void LoadVideoProps(
            FileInfo fi,
            System.Collections.Generic.List<(string, string, Color?)> rows)
        {
            rows.Add(("##Video", string.Empty, null));
            try
            {
                var tag = TagLib.File.Create(fi.FullName);
                if (tag.Properties is not null)
                {
                    rows.Add(("Duración", TimeSpanFormat.Format(tag.Properties.Duration), null));
                    if (tag.Properties.VideoWidth > 0)
                        rows.Add(("Resolución",
                            $"{tag.Properties.VideoWidth} × {tag.Properties.VideoHeight} px", null));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilePropertiesForm] Video metadata: {ex.Message}");
                rows.Add(("Metadatos", "No disponibles", null));
            }
            rows.Add(("---", string.Empty, null));
        }
        private const long TextPreviewBytes = 512 * 1024;
        private static void LoadTextProps(

            FileInfo fi,
            System.Collections.Generic.List<(string, string, Color?)> rows)
        {

            


            rows.Add(("##Contenido de texto", string.Empty, null));
            try
            {
                long readBytes = Math.Min(fi.Length, TextPreviewBytes);
                byte[] buffer    = new byte[readBytes];

                using var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Read(buffer, 0, (int)readBytes);

                string encoding = DetectEncoding(buffer);
                string content  = System.Text.Encoding.UTF8.GetString(buffer);

                int lines = 1, words = 0;
                bool inWord = false;
                foreach (char c in content)
                {
                    if (c == '\n') lines++;
                    if (char.IsLetterOrDigit(c))
                    {
                        if (!inWord) { words++; inWord = true; }
                    }
                    else
                    {
                        inWord = false;
                    }
                }

                bool isTruncated = fi.Length > TextPreviewBytes;
                string approx    = isTruncated ? " (aprox.)" : string.Empty;

                rows.Add(("Encoding",   encoding, null));
                rows.Add(("Líneas",     $"{lines:N0}{approx}", null));
                rows.Add(("Palabras",   $"{words:N0}{approx}", null));
                rows.Add(("Caracteres", $"{fi.Length:N0}", null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilePropertiesForm] Text metadata: {ex.Message}");
                rows.Add(("Contenido", "No disponible", null));
            }
            rows.Add(("---", string.Empty, null));
        }

        // ════════════════════════════════════════════════════════════════════
        //  TOAST
        // ════════════════════════════════════════════════════════════════════
        private void ShowCopiedToast(string value)
        {
            var toast = new Form
            {
                Size            = new Size(220, 36),
                FormBorderStyle = FormBorderStyle.None,
                StartPosition   = FormStartPosition.Manual,
                BackColor       = Color.FromArgb(40, 100, 80),
                TopMost         = true,
                Opacity         = 0.92
            };
            toast.Location = new Point(Left + (Width - toast.Width) / 2, Top + Height - 70);
            toast.Controls.Add(new Label
            {
                Text      = "✔  Copiado al portapapeles",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(160, 240, 200),
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            });
            toast.Show(this);

            var timer = new System.Windows.Forms.Timer { Interval = 1400 };
            timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); toast.Close(); };
            timer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static (int files, int dirs, long size) CountContents(DirectoryInfo di)
        {
            int files = 0, dirs = 0;
            long size = 0;
            try
            {
                foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    files++;
                    size += f.Length;
                }
                foreach (var _ in di.EnumerateDirectories("*", SearchOption.AllDirectories))
                    dirs++;
            }
            catch (UnauthorizedAccessException)
            {
                // Return whatever we managed to count.
            }
            return (files, dirs, size);
        }

        private static string DetectEncoding(byte[] buffer)
        {
            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return "UTF-8 (con BOM)";
            if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                return "UTF-16 LE";
            if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                return "UTF-16 BE";
            return "UTF-8";
        }

        private static string GetFileEmoji(string ext) => ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp"
                or ".webp" or ".tiff" or ".ico" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".aac"
                or ".ogg" or ".m4a" or ".wma"   => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov"
                or ".wmv" or ".webm" or ".flv"  => "🎬",
            ".pdf"                               => "📄",
            ".doc" or ".docx"                   => "📝",
            ".xls" or ".xlsx"                   => "📊",
            ".ppt" or ".pptx"                   => "📋",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".exe" or ".msi"                     => "⚙️",
            ".cs" or ".py" or ".js" or ".ts"
                or ".html" or ".css" or ".json" => "💻",
            ".txt" or ".log" or ".md" or ".csv" or ".xml" => "📃",
            _                                   => "📄"
        };

        private static string DescribeType(string ext) => ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "Imagen JPEG",
            ".png"            => "Imagen PNG",
            ".gif"            => "Imagen GIF animada",
            ".bmp"            => "Imagen de mapa de bits",
            ".webp"           => "Imagen WebP",
            ".tiff" or ".tif" => "Imagen TIFF",
            ".ico"            => "Ícono de Windows",
            ".svg"            => "Gráfico vectorial SVG",
            ".mp3"            => "Audio MP3",
            ".wav"            => "Audio WAV sin comprimir",
            ".flac"           => "Audio FLAC sin pérdida",
            ".aac"            => "Audio AAC",
            ".ogg"            => "Audio OGG Vorbis",
            ".m4a"            => "Audio M4A (MPEG-4)",
            ".wma"            => "Audio Windows Media",
            ".mp4"            => "Video MPEG-4",
            ".avi"            => "Video AVI",
            ".mkv"            => "Video Matroska",
            ".mov"            => "Video QuickTime",
            ".wmv"            => "Video Windows Media",
            ".webm"           => "Video WebM",
            ".pdf"            => "Documento PDF",
            ".doc"            => "Documento Word (antiguo)",
            ".docx"           => "Documento Word",
            ".xls"            => "Hoja de cálculo Excel (antiguo)",
            ".xlsx"           => "Hoja de cálculo Excel",
            ".ppt"            => "Presentación PowerPoint (antiguo)",
            ".pptx"           => "Presentación PowerPoint",
            ".zip"            => "Archivo comprimido ZIP",
            ".rar"            => "Archivo comprimido RAR",
            ".7z"             => "Archivo comprimido 7-Zip",
            ".exe"            => "Aplicación ejecutable",
            ".msi"            => "Instalador de Windows",
            ".txt"            => "Documento de texto plano",
            ".csv"            => "Valores separados por comas",
            ".json"           => "Datos JSON",
            ".xml"            => "Documento XML",
            ".html"           => "Página web HTML",
            ".css"            => "Hoja de estilos CSS",
            ".cs"             => "Código fuente C#",
            ".py"             => "Código fuente Python",
            ".js"             => "Código JavaScript",
            ".md"             => "Documento Markdown",
            ".log"            => "Archivo de registro (log)",
            _ => string.IsNullOrEmpty(ext)
                    ? "Archivo sin extensión"
                    : $"Archivo {ext.ToUpper()}"
        };
    }
}