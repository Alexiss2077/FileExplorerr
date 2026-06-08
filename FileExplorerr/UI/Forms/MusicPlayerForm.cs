using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using TagFile = TagLib.File;

namespace FileExplorerr
{
    public class MusicPlayerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Panel topBar = null!, controlBar = null!, progressPanel = null!;
        private DataGridView grid = null!;
        private PictureBox coverBox = null!;
        private Panel rightPanel = null!;
        private RichTextBox lyricsBox = null!;
        private Label lblNowPlaying = null!, lblArtist = null!, lblAlbum = null!;
        private Label lblTime = null!, lblDuration = null!;
        private TrackBar seekBar = null!, volBar = null!;
        private Button btnPlay = null!, btnPrev = null!, btnNext = null!;
        private Button btnShuffle = null!, btnRepeat = null!, btnLyrics = null!;
        private Button btnMute = null!;
        private Label lblVolPct = null!;

        // ── Audio ─────────────────────────────────────────────────────────────
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        private int currentIndex = -1;
        private bool isDraggingSeek;
        private System.Windows.Forms.Timer uiTimer = null!;

        // ── Grabación ─────────────────────────────────────────────────────────
        private WaveInEvent? waveIn;
        private readonly List<byte[]> recordedChunks = new();
        private WaveFormat? recordFormat;
        private string recordAudioPath = "";
        private bool isRecording = false;
        private Button btnRecordAudio = null!, btnStopRecordAudio = null!;
        private Label lblRecordStatus = null!;
        private System.Windows.Forms.Timer recordTimer = null!;
        private int recordSeconds = 0;

        // ── Modos ─────────────────────────────────────────────────────────────
        private bool shuffleMode;
        private int repeatMode;   // 0=off, 1=all, 2=one
        private int[]? shuffleOrder;
        private int shufflePos;
        private bool isMuted;

        internal static readonly string[] SupportedExtensions =
            { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".aiff" };

        // ── Colores específicos del reproductor ───────────────────────────────
        private static readonly Color SpotifyGreen = Color.FromArgb(30, 215, 96);
        private static readonly Color SpotifyGreenDim = Color.FromArgb(18, 80, 40);
        private static readonly Color NowPlayingBg = Color.FromArgb(22, 26, 38);
        private static readonly Color TrackHoverBg = Color.FromArgb(30, 34, 50);
        private static readonly Color TrackPlayingBg = Color.FromArgb(28, 40, 56);
        private static readonly Color ControlBarBg = Color.FromArgb(16, 18, 26);
        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

        // P/Invokes necesarios — agrégalos junto al DllImport que ya tienes
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hwnd, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        public MusicPlayerForm(string initialFile)
        {
            BuildUI();
            LoadFolder(initialFile);
            uiTimer = new System.Windows.Forms.Timer { Interval = 300 };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "FileExplorerr · Música";
            Size = new Size(1080, 720);
            MinimumSize = new Size(840, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;
            KeyPreview = true;
            KeyDown += OnKeyDown;
            DoubleBuffered = true;

            // ═══ TOP BAR ════════════════════════════════════════════════════
            topBar = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = Theme.BgSurface };

            var logoLbl = new Label
            {
                Text = "🎵  Música",
                Left = 18,
                Top = 14,
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = SpotifyGreen,
                BackColor = Color.Transparent
            };

            lblNowPlaying = new Label
            {
                Left = 120,
                Top = 8,
                Width = 500,
                Height = 20,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            lblAlbum = new Label
            {
                Left = 120,
                Top = 28,
                Width = 500,
                Height = 16,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            // Botones de acción en el top
            var btnOpen = MakeTopBtn("📂 Abrir carpeta");
            var btnAdd = MakeTopBtn("＋ Agregar");
            var btnLoadPlaylist = MakeTopBtn("📂 Playlist");
            var btnSavePlaylist = MakeTopBtn("💾 Guardar");

            btnOpen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadPlaylist.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSavePlaylist.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnOpen.Click += (s, e) => OpenFolderDialog();
            btnAdd.Click += (s, e) => AddFilesDialog();
            btnLoadPlaylist.Click += (s, e) => LoadPlaylist();
            btnSavePlaylist.Click += (s, e) => SavePlaylist();

            // Posicionar desde la derecha
            topBar.Resize += (s, e) =>
            {
                int x = topBar.Width - 12;
                foreach (var b in new[] { btnOpen, btnSavePlaylist, btnLoadPlaylist, btnAdd })
                {
                    x -= b.Width + 6;
                    b.Location = new Point(x, 10);
                }

                // Calcular el espacio que queda libre entre la izquierda y el primer botón
                int espacioDisponible = x - lblNowPlaying.Left - 20;

                if (espacioDisponible < 50) espacioDisponible = 50; // Evitar anchos negativos o muy chicos

                // Asignar el nuevo ancho dinámico a los labels
                lblNowPlaying.Width = espacioDisponible;
                lblAlbum.Width = espacioDisponible;
            };

            topBar.Controls.AddRange(new Control[] { logoLbl, lblNowPlaying, lblAlbum, btnOpen, btnAdd, btnLoadPlaylist, btnSavePlaylist });

            // ═══ RIGHT PANEL (carátula + letra) ════════════════════════════
            rightPanel = new Panel { Width = 260, Dock = DockStyle.Right, BackColor = NowPlayingBg };
            BuildRightPanel();

            // ═══ LISTA DE CANCIONES ═════════════════════════════════════════
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            StyleMusicGrid();

            // ── CAMBIO: Solución para los scrollbars rebeldes del DataGridView ──
            grid.HandleCreated += (s, e) =>
            {
                ApplyDarkScrollBars(grid);
                // Forzar el tema en los controles internos (barras) que ya existan
                foreach (Control c in grid.Controls)
                {
                    if (c is ScrollBar) ApplyDarkScrollBars(c);
                }
            };

            // Estar atentos por si el grid decide crear las barras después
            grid.ControlAdded += (s, e) =>
            {
                if (e.Control is ScrollBar)
                {
                    e.Control.HandleCreated += (ss, ee) => ApplyDarkScrollBars(e.Control);
                    if (e.Control.IsHandleCreated) ApplyDarkScrollBars(e.Control);
                }
            };
            // ─────────────────────────────────────────────────────────────────────

            grid.Columns.Add("Num", "#");
            grid.Columns.Add("Title", "Título");
            grid.Columns.Add("Artist", "Artista");
            grid.Columns.Add("Album", "Álbum");
            grid.Columns.Add("Duration", "Duración");
            grid.Columns.Add("Path", "Ruta");

            grid.Columns["Num"]!.FillWeight = 18;
            grid.Columns["Num"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns["Path"]!.Visible = false;
            grid.Columns["Duration"]!.FillWeight = 28;
            grid.Columns["Title"]!.FillWeight = 100;
            grid.Columns["Artist"]!.FillWeight = 70;
            grid.Columns["Album"]!.FillWeight = 65;

            grid.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await PlayTrack(e.RowIndex); };
            grid.CellFormatting += Grid_CellFormatting;
            grid.AllowDrop = true;
            grid.DragEnter += (s, e) => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            grid.DragDrop += (s, e) =>
            {
                if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
                foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)!)
                    if (SupportedExtensions.Contains(Path.GetExtension(f).ToLower())) AddFileToGrid(f);
            };

            // ═══ BARRA DE PROGRESO ══════════════════════════════════════════
            progressPanel = new Panel
            {
                Height = 28,
                Dock = DockStyle.Bottom,
                BackColor = ControlBarBg,
                Padding = new Padding(14, 0, 14, 0)
            };
            lblTime = new Label { Text = "0:00", Dock = DockStyle.Left, Width = 42, ForeColor = Theme.TextMuted, Font = Theme.FontMonoSmall, TextAlign = ContentAlignment.MiddleCenter };
            lblDuration = new Label { Text = "0:00", Dock = DockStyle.Right, Width = 42, ForeColor = Theme.TextMuted, Font = Theme.FontMonoSmall, TextAlign = ContentAlignment.MiddleCenter };
            seekBar = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, BackColor = ControlBarBg };
            seekBar.MouseDown += (s, e) => isDraggingSeek = true;
            seekBar.MouseUp += (s, e) =>
            {
                isDraggingSeek = false;
                if (audioFile != null) audioFile.CurrentTime = TimeSpan.FromSeconds(seekBar.Value / 1000.0 * audioFile.TotalTime.TotalSeconds);
            };
            progressPanel.Controls.Add(seekBar);
            progressPanel.Controls.Add(lblDuration);
            progressPanel.Controls.Add(lblTime);

            // ═══ BARRA DE CONTROLES (estilo Spotify) ════════════════════════
            controlBar = new Panel
            {
                Height = 90,
                Dock = DockStyle.Bottom,
                BackColor = ControlBarBg
            };
            BuildControlBar();

            Controls.Add(grid);
            Controls.Add(rightPanel);
            Controls.Add(progressPanel);
            Controls.Add(controlBar);
            Controls.Add(topBar);
        }

        // ── Panel derecho ────────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            // Carátula cuadrada
            coverBox = new PictureBox
            {
                Height = 260,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(28, 32, 48),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Artista / álbum bajo la carátula
            lblArtist = new Label
            {
                Height = 42,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = SpotifyGreen,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = NowPlayingBg,
                Text = ""
            };

            var actionPanel = new Panel
            {
                Height = 38,
                Dock = DockStyle.Top,
                BackColor = NowPlayingBg,
                Padding = new Padding(8, 4, 8, 4)
            };
            var btnEditTags = MakeSmallBtn("✏️ Tags", Theme.AccentBg, Theme.Accent2);
            var btnRemove = MakeSmallBtn("✕ Quitar", Theme.CoralDim, Theme.Coral);
            btnEditTags.Dock = DockStyle.Left; btnEditTags.Width = 108; btnEditTags.Height = 30;
            btnRemove.Dock = DockStyle.Right; btnRemove.Width = 90; btnRemove.Height = 30;
            btnEditTags.Click += async (s, e) => await EditTags();
            btnRemove.Click += (s, e) => RemoveSelected();
            actionPanel.Controls.Add(btnEditTags);
            actionPanel.Controls.Add(btnRemove);

            // Letra
            var lyricsHdr = new Label
            {
                Height = 26,
                Dock = DockStyle.Top,
                Text = "  Letra",
                Font = Theme.FontSmallBold,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = NowPlayingBg
            };
            btnLyrics = MakeSmallBtn("🔍 Buscar letra", SpotifyGreenDim, SpotifyGreen);
            btnLyrics.Dock = DockStyle.Bottom;
            btnLyrics.Height = 34;
            btnLyrics.Click += async (s, e) => await SearchLyrics();

            lyricsBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = NowPlayingBg,
                ForeColor = Theme.TextSecondary,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            lyricsBox.HandleCreated += (s, e) => ApplyDarkScrollBars(lyricsBox);

            rightPanel.Controls.Add(lyricsBox);
            rightPanel.Controls.Add(lyricsHdr);
            rightPanel.Controls.Add(btnLyrics);
            rightPanel.Controls.Add(actionPanel);
            rightPanel.Controls.Add(lblArtist);
            rightPanel.Controls.Add(coverBox);
        }

        private void ApplyDarkScrollBars(Control control)
        {
            // Primero aplica al control mismo
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);

            // Luego busca los scrollbars hijos y aplica también a ellos
            EnumChildWindows(control.Handle, (hwnd, lParam) =>
            {
                var className = new System.Text.StringBuilder(256);
                GetClassName(hwnd, className, 256);
                if (className.ToString() == "ScrollBar")
                    SetWindowTheme(hwnd, "DarkMode_Explorer", null);
                return true;
            }, IntPtr.Zero);
        }

        // ── Barra de controles estilo Spotify ────────────────────────────────
        private void BuildControlBar()
        {
            // ── BOTÓN PLAY (central, grande, redondo) ────────────────────────
            btnPlay = new Button
            {
                Text = "▶",
                Size = new Size(56, 56),
                BackColor = SpotifyGreen,
                ForeColor = Color.FromArgb(10, 10, 14),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnPlay.FlatAppearance.BorderSize = 0;
            btnPlay.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 240, 120);
            btnPlay.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 180, 70);
            btnPlay.Click += async (s, e) => await TogglePlay();

            // ── BOTÓN ANTERIOR ────────────────────────────────────────────────
            btnPrev = MakeCtrlBtn("⏮", false);
            btnPrev.Size = new Size(44, 44);
            btnPrev.Font = new Font("Segoe UI", 18F);
            btnPrev.Click += async (s, e) => await ChangeTrack(-1);

            // ── BOTÓN SIGUIENTE ───────────────────────────────────────────────
            btnNext = MakeCtrlBtn("⏭", false);
            btnNext.Size = new Size(44, 44);
            btnNext.Font = new Font("Segoe UI", 18F);
            btnNext.Click += async (s, e) => await ChangeTrack(1);

            // ── SHUFFLE ───────────────────────────────────────────────────────
            btnShuffle = MakeCtrlBtn("⇄", false);
            btnShuffle.Size = new Size(40, 40);
            btnShuffle.Font = new Font("Segoe UI", 16F);
            btnShuffle.Click += (s, e) => ToggleShuffle();

            // ── REPEAT ────────────────────────────────────────────────────────
            btnRepeat = MakeCtrlBtn("↻", false);
            btnRepeat.Size = new Size(40, 40);
            btnRepeat.Font = new Font("Segoe UI", 16F);
            btnRepeat.Click += (s, e) => ToggleRepeat();
            new ToolTip().SetToolTip(btnRepeat, "Repetir: OFF");

            // ── Posicionar grupo central mediante Resize ──────────────────────
            controlBar.Resize += (s, e) =>
            {
                int cy = (controlBar.Height - btnPlay.Height) / 2;
                int centerX = controlBar.Width / 2;

                btnPlay.Location = new Point(centerX - btnPlay.Width / 2, cy);
                btnPrev.Location = new Point(centerX - btnPlay.Width / 2 - 56, cy + 6);
                btnNext.Location = new Point(centerX + btnPlay.Width / 2 + 12, cy + 6);
                btnShuffle.Location = new Point(centerX - btnPlay.Width / 2 - 108, cy + 8);
                btnRepeat.Location = new Point(centerX + btnPlay.Width / 2 + 68, cy + 8);
            };

            controlBar.Controls.AddRange(new Control[] { btnShuffle, btnPrev, btnPlay, btnNext, btnRepeat });

            // ── VOLUMEN (grupo derecho) ───────────────────────────────────────
            btnMute = MakeCtrlBtn("🔉", false);
            btnMute.Size = new Size(38, 38);
            btnMute.Font = new Font("Segoe UI", 16F);
            btnMute.Click += (s, e) => ToggleMute();

            volBar = new TrackBar
            {
                Size = new Size(100, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 70,
                TickStyle = TickStyle.None,
                BackColor = ControlBarBg
            };
            volBar.ValueChanged += (s, e) =>
            {
                if (outputDevice != null) outputDevice.Volume = volBar.Value / 100f;
                if (lblVolPct != null) lblVolPct.Text = $"{volBar.Value}%";
            };

            lblVolPct = new Label
            {
                Text = "70%",
                Width = 36,
                Height = 22,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontMonoSmall,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ── GRABACIÓN (grupo derecho, extremo) ───────────────────────────
            btnRecordAudio = new Button
            {
                Text = "🎙",
                Size = new Size(38, 38),
                BackColor = Theme.CoralDim,
                ForeColor = Theme.Coral,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 15F),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnRecordAudio.FlatAppearance.BorderSize = 1;
            btnRecordAudio.FlatAppearance.BorderColor = Color.FromArgb(248, 113, 113, 80);
            btnRecordAudio.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 40, 40);
            btnRecordAudio.Click += BtnRecordAudio_Click;
            new ToolTip().SetToolTip(btnRecordAudio, "Grabar audio del micrófono");

            btnStopRecordAudio = new Button
            {
                Text = "⏹",
                Size = new Size(38, 38),
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 15F),
                Cursor = Cursors.Hand,
                Enabled = false,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnStopRecordAudio.FlatAppearance.BorderSize = 1;
            btnStopRecordAudio.FlatAppearance.BorderColor = Theme.Border;
            btnStopRecordAudio.FlatAppearance.MouseOverBackColor = Theme.BgHover;
            btnStopRecordAudio.Click += BtnStopRecordAudio_Click;
            new ToolTip().SetToolTip(btnStopRecordAudio, "Detener grabación");

            lblRecordStatus = new Label
            {
                Text = "",
                Width = 72,
                Height = 22,
                ForeColor = Theme.Coral,
                Font = Theme.FontMonoSmall,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Posicionar grupo derecho mediante Resize
            controlBar.Resize += (s, e) =>
            {
                int ry = (controlBar.Height - 38) / 2;
                int rx = controlBar.Width - 14;

                lblRecordStatus.Location = new Point(rx - lblRecordStatus.Width, ry + 8);
                rx -= lblRecordStatus.Width + 2;
                btnStopRecordAudio.Location = new Point(rx - 38, ry);
                rx -= 44;
                btnRecordAudio.Location = new Point(rx - 38, ry);
                rx -= 48;

                // separador visual (solo reposiciona, no dibuja)
                rx -= 12;

                lblVolPct.Location = new Point(rx - 36, ry + 8);
                rx -= 40;
                volBar.Location = new Point(rx - 100, ry + 4);
                rx -= 106;
                btnMute.Location = new Point(rx - 38, ry);
            };

            controlBar.Controls.AddRange(new Control[]
            {
                btnMute, volBar, lblVolPct,
                btnRecordAudio, btnStopRecordAudio, lblRecordStatus
            });
        }

        // ── Estilo Spotify para el grid ──────────────────────────────────────
        private void StyleMusicGrid()
        {
            grid.BackgroundColor = Theme.BgBase;
            grid.GridColor = Color.FromArgb(255, 255, 255, 6);
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 52;   // filas más altas para el estilo Spotify
            grid.AllowUserToAddRows = false;

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(18, 20, 28),
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmallBold,
                SelectionBackColor = Color.FromArgb(18, 20, 28),
                SelectionForeColor = Theme.TextMuted,
                Padding = new Padding(10, 0, 0, 0)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.BgBase,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                SelectionBackColor = TrackHoverBg,
                SelectionForeColor = Theme.TextPrimary,
                Padding = new Padding(10, 0, 0, 0)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(16, 18, 24),
                ForeColor = Theme.TextPrimary,
                SelectionBackColor = TrackHoverBg,
                SelectionForeColor = Theme.TextPrimary
            };
        }

        // ── Resaltado de la canción en reproducción ──────────────────────────
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            bool isPlaying = e.RowIndex == currentIndex;

            if (isPlaying)
            {
                e.CellStyle.BackColor = TrackPlayingBg;
                e.CellStyle.SelectionBackColor = TrackPlayingBg;

                // Columna "#" muestra el ícono de onda en lugar del número
                if (grid.Columns[e.ColumnIndex].Name == "Num")
                {
                    e.Value = "▶";
                    e.FormattingApplied = true;
                    e.CellStyle.ForeColor = SpotifyGreen;
                    e.CellStyle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = Theme.TextPrimary;
                    // Título en verde Spotify
                    if (grid.Columns[e.ColumnIndex].Name == "Title")
                        e.CellStyle.ForeColor = SpotifyGreen;
                }
            }
            else
            {
                // Columna "#" muestra el número de pista
                if (grid.Columns[e.ColumnIndex].Name == "Num")
                {
                    e.Value = (e.RowIndex + 1).ToString();
                    e.FormattingApplied = true;
                    e.CellStyle.ForeColor = Theme.TextMuted;
                    e.CellStyle.Font = Theme.FontSmall;
                }
            }
        }

        // ── Factory buttons ──────────────────────────────────────────────────
        private static Button MakeTopBtn(string text)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = false,
                Width = 120,
                Height = 32,
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmall,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Theme.Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Theme.BgHover;
            return btn;
        }

        private static Button MakeCtrlBtn(string text, bool active)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(30, 30),
                BackColor = Color.Transparent,
                ForeColor = active ? SpotifyGreen : Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 15F),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            return btn;
        }

        private static Button MakeSmallBtn(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Height = 30,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmall,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = fg;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(fg.R / 2, fg.G / 2, fg.B / 2);
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGAR CARPETA
        // ════════════════════════════════════════════════════════════════════
        private void LoadFolder(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (dir == null) return;
            grid.Rows.Clear();
            var audioFiles = Directory.GetFiles(dir)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f).ToList();
            int target = 0;
            for (int i = 0; i < audioFiles.Count; i++)
            {
                AddFileToGrid(audioFiles[i]);
                if (audioFiles[i].Equals(filePath, StringComparison.OrdinalIgnoreCase)) target = i;
            }
            if (grid.Rows.Count > 0)
            {
                currentIndex = target;
                _ = PlayTrack(currentIndex);
            }
        }

        private void AddFileToGrid(string path)
        {
            try
            {
                var tag = TagFile.Create(path);
                grid.Rows.Add(
                    (grid.Rows.Count + 1).ToString(),
                    CleanTitle(tag.Tag.Title ?? Path.GetFileNameWithoutExtension(path)),
                    CleanArtist(tag.Tag.FirstPerformer ?? "—"),
                    tag.Tag.Album ?? "—",
                    tag.Properties.Duration.ToString(@"mm\:ss"),
                    path);
            }
            catch
            {
                grid.Rows.Add(
                    (grid.Rows.Count + 1).ToString(),
                    Path.GetFileNameWithoutExtension(path),
                    "—", "", "—", path);
            }
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Agregar audio",
                Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|Todos|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            foreach (string f in dlg.FileNames)
                if (SupportedExtensions.Contains(Path.GetExtension(f).ToLower())) AddFileToGrid(f);
        }

        private void OpenFolderDialog()
        {
            using var dlg = new FolderBrowserDialog { Description = "Carpeta con música" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            StopPlayback(); grid.Rows.Clear(); currentIndex = -1;
            foreach (string f in Directory.GetFiles(dlg.SelectedPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f))
                AddFileToGrid(f);
            if (grid.Rows.Count > 0) { currentIndex = 0; _ = PlayTrack(0); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  REPRODUCCIÓN
        // ════════════════════════════════════════════════════════════════════
        private async Task PlayTrack(int index)
        {
            if (index < 0 || index >= grid.Rows.Count) return;
            StopPlayback();
            string path = grid.Rows[index].Cells["Path"].Value?.ToString() ?? "";
            if (!File.Exists(path)) return;
            currentIndex = index;

            // Resaltar fila en el grid
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
            if (grid.FirstDisplayedScrollingRowIndex > index || index >= grid.FirstDisplayedScrollingRowIndex + grid.DisplayedRowCount(false))
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 2);
            grid.Invalidate(); // refresca el CellFormatting

            // Actualizar info en topbar
            string title = grid.Rows[index].Cells["Title"].Value?.ToString() ?? "";
            string artist = grid.Rows[index].Cells["Artist"].Value?.ToString() ?? "";
            string album = grid.Rows[index].Cells["Album"].Value?.ToString() ?? "";
            lblNowPlaying.Text = title;
            lblAlbum.Text = $"{artist}  ·  {album}";
            lblArtist.Text = artist;
            Text = $"Música — {title}";
            lyricsBox.Text = "";

            await LoadCover(path);

            try
            {
                audioFile = new AudioFileReader(path);
                outputDevice = new WaveOutEvent();
                outputDevice.Init(audioFile);
                outputDevice.Volume = isMuted ? 0 : volBar.Value / 100f;
                outputDevice.Play();
                seekBar.Maximum = 1000;
                lblDuration.Text = TimeSpanFormat.Format(audioFile.TotalTime.TotalSeconds);
                SetPlayState(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopPlayback()
        {
            outputDevice?.Stop();
            outputDevice?.Dispose();
            outputDevice = null;
            audioFile?.Dispose();
            audioFile = null;
            seekBar.Value = 0;
            lblTime.Text = "0:00";
            SetPlayState(false);
        }

        private async Task TogglePlay()
        {
            if (outputDevice == null || audioFile == null)
            {
                if (grid.Rows.Count > 0)
                    await PlayTrack(grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : 0);
                return;
            }
            if (outputDevice.PlaybackState == PlaybackState.Playing) { outputDevice.Pause(); SetPlayState(false); }
            else { outputDevice.Play(); SetPlayState(true); }
        }

        private async Task ChangeTrack(int dir)
        {
            if (grid.Rows.Count == 0) return;
            if (repeatMode == 2) { await PlayTrack(currentIndex); return; }
            if (shuffleMode)
            {
                if (shuffleOrder == null || shuffleOrder.Length != grid.Rows.Count) GenerateShuffleOrder();
                if (shufflePos >= shuffleOrder!.Length)
                {
                    if (repeatMode == 0) { StopPlayback(); return; }
                    GenerateShuffleOrder();
                }
                currentIndex = shuffleOrder![shufflePos++];
            }
            else
            {
                int next = currentIndex + dir;
                if (next >= grid.Rows.Count) { if (repeatMode == 0) { StopPlayback(); return; } currentIndex = 0; }
                else if (next < 0) currentIndex = grid.Rows.Count - 1;
                else currentIndex = next;
            }
            await PlayTrack(currentIndex);
        }

        private void SetPlayState(bool playing)
        {
            btnPlay.Text = playing ? "⏸" : "▶";
            btnPlay.BackColor = SpotifyGreen;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (audioFile == null || outputDevice == null) return;
            if (!isDraggingSeek)
            {
                double pos = audioFile.CurrentTime.TotalSeconds;
                double dur = audioFile.TotalTime.TotalSeconds;
                if (dur > 0) seekBar.Value = Math.Min(seekBar.Maximum, (int)(pos / dur * 1000));
                lblTime.Text = TimeSpanFormat.Format(pos);
            }
            if (audioFile.TotalTime.TotalSeconds - audioFile.CurrentTime.TotalSeconds <= 0.8
                && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                BeginInvoke(async () => await ChangeTrack(1));
            }
        }

        private void ToggleShuffle()
        {
            shuffleMode = !shuffleMode;
            btnShuffle.ForeColor = shuffleMode ? SpotifyGreen : Theme.TextSecondary;
            btnShuffle.BackColor = shuffleMode ? SpotifyGreenDim : Color.Transparent;
            btnShuffle.Font = shuffleMode
                ? new Font("Segoe UI", 16F, FontStyle.Bold)
                : new Font("Segoe UI", 16F);
            new ToolTip().SetToolTip(btnShuffle, shuffleMode ? "Aleatorio: ON" : "Aleatorio: OFF");
            if (shuffleMode) GenerateShuffleOrder();
        }

        private void ToggleRepeat()
        {
            repeatMode = (repeatMode + 1) % 3;
            switch (repeatMode)
            {
                case 0: // OFF — ↻ gris, sin fondo
                    btnRepeat.Text = "↻";
                    btnRepeat.ForeColor = Theme.TextMuted;
                    btnRepeat.BackColor = Color.Transparent;
                    btnRepeat.Font = new Font("Segoe UI", 16F);
                    new ToolTip().SetToolTip(btnRepeat, "Repetir: OFF");
                    break;

                case 1: // REPETIR LISTA — ↻ verde con punto indicador
                    btnRepeat.Text = "↻";
                    btnRepeat.ForeColor = SpotifyGreen;
                    btnRepeat.BackColor = SpotifyGreenDim;
                    btnRepeat.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
                    new ToolTip().SetToolTip(btnRepeat, "Repetir lista completa");
                    break;

                case 2: // REPETIR UNA — ícono completamente diferente, ámbar
                    btnRepeat.Text = "🔂";   // ícono de "repeat one" universal
                    btnRepeat.ForeColor = Theme.Amber;
                    btnRepeat.BackColor = Theme.AmberDim;
                    btnRepeat.Font = new Font("Segoe UI", 14F);
                    new ToolTip().SetToolTip(btnRepeat, "Repetir esta canción");
                    break;
            }
        }

        private void ToggleMute()
        {
            isMuted = !isMuted;
            if (outputDevice != null) outputDevice.Volume = isMuted ? 0 : volBar.Value / 100f;
            btnMute.ForeColor = isMuted ? Theme.Coral : Theme.TextSecondary;
            btnMute.Text = isMuted ? "🔇" : "🔉";
        }

        private void GenerateShuffleOrder()
        {
            var rng = new Random();
            shuffleOrder = Enumerable.Range(0, grid.Rows.Count).OrderBy(_ => rng.Next()).ToArray();
            shufflePos = 0;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARÁTULA / LETRA
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadCover(string path)
        {
            coverBox.Image?.Dispose();
            coverBox.Image = null;

            try
            {
                var tag = TagFile.Create(path);
                if (tag.Tag.Pictures.Length > 0)
                {
                    
                    var ms = new System.IO.MemoryStream(tag.Tag.Pictures[0].Data.Data);
                    coverBox.Image = System.Drawing.Image.FromStream(ms);
                    return;
                }
            }
            catch { }

            try
            {
                string artist = grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? string.Empty;
                string title = grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? string.Empty;

                byte[]? imgData = await CoverSearchService.FetchFromITunesAsync(artist, title);
                if (imgData is not null && imgData.Length > 0)
                {
                    
                    var ms = new System.IO.MemoryStream(imgData);
                    coverBox.Image = System.Drawing.Image.FromStream(ms);

                    await Task.Run(() =>
                    {
                        try
                        {
                            var mp3 = TagLib.File.Create(path);
                            mp3.Tag.Pictures = new TagLib.IPicture[]
                            {
                        new TagLib.Picture(imgData)
                        {
                            Type     = TagLib.PictureType.FrontCover,
                            MimeType = "image/png"
                        }
                            };
                            mp3.Save();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[MusicPlayerForm.LoadCover] tag write: {ex.Message}");
                        }
                    });
                }
            }
            catch { }
        }
        private async Task SearchLyrics()
        {
            if (currentIndex < 0 || currentIndex >= grid.Rows.Count) return;

            string artist = grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? string.Empty;
            string title = grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? string.Empty;

            lyricsBox.Text = "Buscando...";
            btnLyrics.Enabled = false;

            try
            {
                var result = await LyricsService.SearchAsync(artist, title);
                lyricsBox.Text = result.Found ? result.Lyrics : result.ErrorMessage;
            }
            finally
            {
                btnLyrics.Enabled = true;
            }
        }
        // ════════════════════════════════════════════════════════════════════
        //  PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void SavePlaylist()
        {
            if (grid.Rows.Count == 0) { MessageBox.Show("No hay canciones.", "Sin canciones", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using var dlg = new SaveFileDialog { Title = "Guardar Playlist", Filter = "Playlist (*.txt)|*.txt", FileName = "playlist.txt" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var rutas = new List<string>();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string? ruta = row.Cells["Path"].Value?.ToString();
                    if (!string.IsNullOrEmpty(ruta)) rutas.Add(ruta);
                }
                File.WriteAllLines(dlg.FileName, rutas, System.Text.Encoding.UTF8);
                MessageBox.Show($"Playlist guardada:\n{dlg.FileName}\n\n{rutas.Count} canción(es)", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void LoadPlaylist()
        {
            using var dlg = new OpenFileDialog { Title = "Cargar Playlist", Filter = "Playlist (*.txt)|*.txt" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                StopPlayback(); grid.Rows.Clear(); currentIndex = -1; int cargadas = 0;
                foreach (string linea in File.ReadAllLines(dlg.FileName))
                {
                    string ruta = linea.Trim();
                    if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) continue;
                    if (!SupportedExtensions.Contains(Path.GetExtension(ruta).ToLower())) continue;
                    AddFileToGrid(ruta); cargadas++;
                }
                if (grid.Rows.Count > 0) { currentIndex = 0; _ = PlayTrack(0); }
                lblNowPlaying.Text = cargadas > 0 ? $"Playlist cargada — {cargadas} canción(es)" : "No se encontraron archivos válidos";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EDITAR TAGS / QUITAR
        // ════════════════════════════════════════════════════════════════════
        private async Task EditTags()
        {
            if (grid.Rows.Count == 0 || grid.SelectedRows.Count == 0) return;
            int idx = grid.SelectedRows[0].Index;
            string path = grid.Rows[idx].Cells["Path"].Value?.ToString() ?? "";
            if (!File.Exists(path)) return;
            bool wasPlaying = audioFile != null && idx == currentIndex && outputDevice?.PlaybackState == PlaybackState.Playing;
            if (audioFile != null && idx == currentIndex) StopPlayback();
            try
            {
                var tag = TagFile.Create(path);
                // Obtener la carátula actual del PictureBox para pasarla al diálogo
                Image? coverActual = coverBox.Image;

                using var dlg = new TagEditDialog(
                    tag.Tag.Title ?? "", tag.Tag.FirstPerformer ?? "",
                    tag.Tag.Album ?? "", tag.Tag.Year, tag.Tag.Track,
                    tag.Tag.Genres?.Length > 0 ? tag.Tag.Genres[0] : "",
                    coverActual);   // ← aquí
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    tag.Tag.Title = dlg.Titulo;
                    tag.Tag.Performers = new[] { dlg.Artista };
                    tag.Tag.Album = dlg.Album;
                    tag.Tag.Year = dlg.Anio;
                    tag.Tag.Track = dlg.NumPista;
                    if (!string.IsNullOrWhiteSpace(dlg.Genero)) tag.Tag.Genres = new[] { dlg.Genero };
                    tag.Save();
                    grid.Rows[idx].Cells["Title"].Value = dlg.Titulo;
                    grid.Rows[idx].Cells["Artist"].Value = dlg.Artista;
                    grid.Rows[idx].Cells["Album"].Value = dlg.Album;
                    if (idx == currentIndex) lblNowPlaying.Text = dlg.Titulo;
                }
                if (wasPlaying) await PlayTrack(currentIndex);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RemoveSelected()
        {
            if (grid.Rows.Count == 0 || grid.SelectedRows.Count == 0) return;
            int idx = grid.SelectedRows[0].Index;
            if (MessageBox.Show($"¿Quitar de la lista?\n\n{grid.Rows[idx].Cells["Artist"].Value} — {grid.Rows[idx].Cells["Title"].Value}",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (currentIndex == idx) { StopPlayback(); currentIndex = -1; }
            grid.Rows.RemoveAt(idx);
            if (currentIndex > idx) currentIndex--;
            if (grid.Rows.Count > 0) grid.Rows[Math.Min(idx, grid.Rows.Count - 1)].Selected = true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  GRABACIÓN
        // ════════════════════════════════════════════════════════════════════
        private void BtnRecordAudio_Click(object? sender, EventArgs e)
        {
            if (isRecording) return;
            if (WaveIn.DeviceCount == 0)
            {
                MessageBox.Show("No se encontró ningún micrófono.", "Sin dispositivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                var caps = WaveIn.GetCapabilities(0);
                WaveFormat formato;
                if (caps.Channels >= 2 && caps.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_44S16))
                    formato = new WaveFormat(44100, 16, 2);
                else if (caps.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_44M16))
                    formato = new WaveFormat(44100, 16, 1);
                else
                    formato = new WaveFormat(8000, 16, 1);

                recordFormat = formato;
                recordedChunks.Clear();
                waveIn = new WaveInEvent { DeviceNumber = 0, WaveFormat = recordFormat, BufferMilliseconds = 100 };
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;
                waveIn.StartRecording();

                isRecording = true; recordSeconds = 0;
                recordTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                recordTimer.Tick += (s2, e2) =>
                {
                    recordSeconds++;
                    lblRecordStatus.Text = $"● {recordSeconds / 60:D2}:{recordSeconds % 60:D2}";
                };
                recordTimer.Start();

                btnRecordAudio.Enabled = false;
                btnStopRecordAudio.Enabled = true;
                btnStopRecordAudio.ForeColor = Theme.Coral;
                lblRecordStatus.Text = "● 00:00";
                lblNowPlaying.Text = "🎙 Grabando...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar grabación:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LimpiarEstadoGrabacion();
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;
            var copia = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, copia, 0, e.BytesRecorded);
            recordedChunks.Add(copia);
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (IsHandleCreated) BeginInvoke(new Action(EscribirYReproducirGrabacion));
        }

        private void BtnStopRecordAudio_Click(object? sender, EventArgs e)
        {
            if (!isRecording) return;
            waveIn?.StopRecording();
        }

        private void EscribirYReproducirGrabacion()
        {
            recordTimer?.Stop(); recordTimer?.Dispose(); recordTimer = null!;
            isRecording = false;
            LimpiarEstadoGrabacion();

            long totalBytes = recordedChunks.Sum(c => (long)c.Length);
            if (totalBytes < 4096 || recordFormat == null)
            { lblNowPlaying.Text = "Grabación vacía."; recordedChunks.Clear(); return; }

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "GrabacionesFileExplorerr");
                Directory.CreateDirectory(folder);
                recordAudioPath = Path.Combine(folder, $"grabacion_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                using (var writer = new WaveFileWriter(recordAudioPath, recordFormat))
                    foreach (var chunk in recordedChunks) writer.Write(chunk, 0, chunk.Length);
                recordedChunks.Clear();

                var fi = new FileInfo(recordAudioPath);
                if (!fi.Exists || fi.Length < 1024) { lblNowPlaying.Text = "Error: el archivo WAV quedó vacío."; return; }

                AddFileToGrid(recordAudioPath);
                lblNowPlaying.Text = $"Grabado: {Path.GetFileName(recordAudioPath)} ({fi.Length / 1024} KB)";
                _ = PlayTrack(grid.Rows.Count - 1);
            }
            catch (Exception ex)
            {
                recordedChunks.Clear();
                MessageBox.Show($"Error al guardar la grabación:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LimpiarEstadoGrabacion()
        {
            try { waveIn?.Dispose(); } catch { }
            waveIn = null;
            btnRecordAudio.Enabled = true;
            btnStopRecordAudio.Enabled = false;
            btnStopRecordAudio.ForeColor = Theme.TextMuted;
            lblRecordStatus.Text = "";
        }

        // ════════════════════════════════════════════════════════════════════
        //  TECLADO
        // ════════════════════════════════════════════════════════════════════
        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space: await TogglePlay(); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Left: if (audioFile != null) audioFile.CurrentTime = TimeSpan.FromSeconds(Math.Max(0, audioFile.CurrentTime.TotalSeconds - 5)); break;
                case Keys.Right: if (audioFile != null) audioFile.CurrentTime = TimeSpan.FromSeconds(Math.Min(audioFile.TotalTime.TotalSeconds, audioFile.CurrentTime.TotalSeconds + 5)); break;
                case Keys.Up: volBar.Value = Math.Min(100, volBar.Value + 5); break;
                case Keys.Down: volBar.Value = Math.Max(0, volBar.Value - 5); break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string CleanArtist(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "—";
            return System.Text.RegularExpressions.Regex.Replace(s, @"\s*-?\s*(Topic|VEVO|Official)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        private static string CleanTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Sin título";
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\([^)]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K|Visualizer|Explicit|Clean|Remaster)[^)]*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\[[^\]]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K)[^\]]*?\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s.Trim();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer?.Stop(); uiTimer?.Dispose();
                recordTimer?.Stop(); recordTimer?.Dispose();
                StopPlayback();
                if (isRecording) { try { waveIn?.StopRecording(); } catch { } }
                try { waveIn?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}