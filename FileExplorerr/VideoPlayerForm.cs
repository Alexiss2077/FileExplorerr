using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace FileExplorerr
{
    public class VideoPlayerForm : Form
    {
        // ── LibVLC ────────────────────────────────────────────────────────────
        private LibVLC _libVLC = null!;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayer = null!;
        private VideoView videoView = null!;

        // ── Controles ─────────────────────────────────────────────────────────
        private Panel controlBar = null!, playlistPanel = null!, rightPanel = null!;
        private TrackBar seekBar = null!, volBar = null!;
        private Label timeLabel = null!;
        private Button btnPlayPause = null!, btnMute = null!;
        private Button btnLoop = null!, btnFullscreen = null!;
        private Button btnPrev = null!, btnNext = null!, btnStop = null!;
        private Button btnSeekBack = null!, btnSeekFwd = null!;
        private ComboBox speedCombo = null!;
        private ListView listView = null!;
        private Label[] propValues = null!;

        // ── Webcam ────────────────────────────────────────────────────────────
        private OpenCvSharp.VideoCapture? _videoCapture;
        private OpenCvSharp.VideoWriter? _videoWriter;
        private System.Windows.Forms.Timer? _frameTimer;
        private PictureBox? _camPictureBox;
        private bool _isCamRecording;
        private string? _camFilePath;
        private Button btnCamRecord = null!;
        private Label lblCamStatus = null!;
        private System.Windows.Forms.Timer _camTimer = null!;
        private int _camSeconds;
        private Form? _camForm;

        // ── GPS ───────────────────────────────────────────────────────────────
        private Panel gpsPanel = null!;
        private Label gpsLatLbl = null!, gpsLonLbl = null!, gpsAltLbl = null!, gpsDateLbl = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private Button btnGps = null!;
        private bool gpsVisible;
        private GpsReader.GpsData? _gpsData;
        private int _gpsLoadedFor = -1;

        // ── Estado ────────────────────────────────────────────────────────────
        private readonly string initialPath;
        private string _currentPath;
        private System.Windows.Forms.Timer uiTimer = null!;
        private bool isSeeking, isLooping, isFullscreen;
        private int currentIndex = -1;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorder;

        // ── Overlay de carga ──────────────────────────────────────────────────
        private Panel _loadingOverlay = null!;
        private Label _loadingLbl = null!;

        private void ShowLoadingOverlay(string msg = "Cargando...")
        {
            if (_loadingLbl != null) _loadingLbl.Text = msg;
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
                // Centrar label
                if (_loadingLbl != null)
                    _loadingLbl.Location = new Point(
                        (_loadingOverlay.Width - _loadingLbl.Width) / 2,
                        (_loadingOverlay.Height - _loadingLbl.Height) / 2);
            }
        }

        private void HideLoadingOverlay()
        {
            if (_loadingOverlay != null) _loadingOverlay.Visible = false;
        }

        // ── Colores del reproductor ───────────────────────────────────────────
        private static readonly Color CtrlBg = Color.FromArgb(14, 16, 22);
        private static readonly Color BtnDefault = Color.FromArgb(28, 32, 44);
        private static readonly Color BtnHover = Color.FromArgb(40, 46, 62);
        private static readonly Color AccentBlue = Color.FromArgb(96, 165, 250);
        private static readonly Color AccentBlueDim = Color.FromArgb(12, 36, 70);
        private static readonly Color LoopColor = Color.FromArgb(52, 211, 153);   // verde = bucle playlist
        private static readonly Color LoopColorDim = Color.FromArgb(10, 48, 36);
        private static readonly Color LoopOneColor = Color.FromArgb(251, 191, 36);   // ámbar = bucle 1 sola
        private static readonly Color LoopOneDim = Color.FromArgb(56, 42, 8);
        private static readonly Color PlayGreen = Color.FromArgb(30, 215, 96);
        private static readonly Color SeekFill = Color.FromArgb(96, 165, 250);

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTORES
        // ════════════════════════════════════════════════════════════════════
        public VideoPlayerForm(string? path = null)
        {
            initialPath = path ?? "";
            _currentPath = path ?? "";
            // NO inicializar LibVLC aquí — bloquea el hilo UI
            // Se inicializa en Load de forma asíncrona
            BuildUI();
            Load += async (s, e) => await InitLibVLCAsync();
        }

        // ── Inicialización asíncrona de LibVLC ───────────────────────────────
        private async Task InitLibVLCAsync()
        {
            // Mostrar overlay de carga
            ShowLoadingOverlay("Iniciando reproductor...");

            try
            {
                await Task.Run(() =>
                {
                    Core.Initialize();
                });

                // Crear LibVLC y MediaPlayer en el hilo UI (requisito de VLC)
                _libVLC = new LibVLC("--no-xlib");
                _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
                _mediaPlayer.EndReached += (s, e) => BeginInvoke(() => OnEndReached());

                // Asignar el MediaPlayer al VideoView ya creado
                videoView.MediaPlayer = _mediaPlayer;

                HideLoadingOverlay();

                // Si se abrió con un archivo, cargarlo ahora
                if (!string.IsNullOrEmpty(initialPath) && File.Exists(initialPath))
                {
                    AddFile(initialPath);
                    PlayAt(0);
                }
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                MessageBox.Show($"Error al iniciar el motor de video:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "FileExplorerr · Video";
            Size = new Size(1200, 760);
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 10, 14);
            ForeColor = Theme.TextPrimary;
            KeyPreview = true;
            KeyDown += Form_KeyDown;

            // LibVLC se inicializa en InitLibVLCAsync() al abrir el Form
            // para no bloquear el hilo UI

            // VideoView — MediaPlayer se asigna después de que LibVLC cargue
            videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // Overlay de carga (visible mientras LibVLC inicializa)
            _loadingOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 14),
                Visible = false
            };
            _loadingLbl = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = AccentBlue,
                BackColor = Color.Transparent,
                Text = "Iniciando reproductor..."
            };
            _loadingOverlay.Controls.Add(_loadingLbl);
            _loadingOverlay.Resize += (s, e) =>
            {
                if (_loadingLbl != null)
                    _loadingLbl.Location = new Point(
                        (_loadingOverlay.Width - _loadingLbl.Width) / 2,
                        (_loadingOverlay.Height - _loadingLbl.Height) / 2);
            };

            // ── Barra de progreso ─────────────────────────────────────────
            var seekWrap = new Panel
            {
                Height = 22,
                Dock = DockStyle.Bottom,
                BackColor = CtrlBg,
                Padding = new Padding(14, 8, 14, 0)
            };
            seekBar = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1000,
                TickStyle = TickStyle.None,
                BackColor = CtrlBg
            };
            seekBar.MouseDown += (s, e) => isSeeking = true;
            seekBar.MouseUp += (s, e) =>
            {
                isSeeking = false;
                if (_mediaPlayer.Length > 0)
                    _mediaPlayer.Position = seekBar.Value / 1000f;
            };
            seekWrap.Controls.Add(seekBar);

            // ── Barra de controles ────────────────────────────────────────
            controlBar = new Panel
            {
                Height = 70,
                Dock = DockStyle.Bottom,
                BackColor = CtrlBg
            };
            BuildControlBar();

            // ── Lista de reproducción ─────────────────────────────────────
            playlistPanel = new Panel
            {
                Height = 130,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgBase
            };
            BuildPlaylist();

            // ── Panel derecho: propiedades + GPS ──────────────────────────
            rightPanel = new Panel
            {
                Width = 260,
                Dock = DockStyle.Right,
                BackColor = Theme.BgSurface
            };
            BuildPropsPanel();
            BuildGpsPanel();
            gpsPanel.Visible = false;
            rightPanel.Controls.Add(gpsPanel);
            rightPanel.Controls.Add(propsPanel);

            // ── Contenedor del video ──────────────────────────────────────
            var content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(videoView);
            content.Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();

            Controls.Add(content);
            Controls.Add(rightPanel);
            Controls.Add(seekWrap);
            Controls.Add(controlBar);
            Controls.Add(playlistPanel);

            // ── Timer UI ─────────────────────────────────────────────────
            uiTimer = new System.Windows.Forms.Timer { Interval = 400 };
            uiTimer.Tick += (s, e) => UpdateProgress();
            uiTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONTROL BAR — REDISEÑADA
        // ════════════════════════════════════════════════════════════════════
        private void BuildControlBar()
        {
            // ── PLAY/PAUSE (centro, prominente) ───────────────────────────
            btnPlayPause = new Button
            {
                Text = "▶",
                Size = new Size(52, 52),
                BackColor = PlayGreen,
                ForeColor = Color.FromArgb(10, 10, 14),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnPlayPause.FlatAppearance.BorderSize = 0;
            btnPlayPause.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 240, 120);
            btnPlayPause.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 160, 60);
            btnPlayPause.Click += (s, e) => TogglePlay();

            // ── STOP ────────────────────────────────────────────────────
            btnStop = MakeCtrlBtn("⏹", 42, 42, AccentBlue, AccentBlueDim);
            btnStop.Font = new Font("Segoe UI", 16F);
            btnStop.Click += (s, e) => _mediaPlayer.Stop();
            new ToolTip().SetToolTip(btnStop, "Detener");

            // ── ANTERIOR ────────────────────────────────────────────────
            btnPrev = MakeCtrlBtn("⏮", 42, 42, Theme.TextSecondary, BtnDefault);
            btnPrev.Font = new Font("Segoe UI", 17F);
            btnPrev.Click += (s, e) => PrevTrack();
            new ToolTip().SetToolTip(btnPrev, "Anterior");

            // ── RETROCEDER 10s ───────────────────────────────────────────
            btnSeekBack = MakeCtrlBtn("⏪", 42, 42, Theme.TextSecondary, BtnDefault);
            btnSeekBack.Font = new Font("Segoe UI", 15F);
            btnSeekBack.Click += (s, e) => _mediaPlayer.Time = Math.Max(0, _mediaPlayer.Time - 10000);
            new ToolTip().SetToolTip(btnSeekBack, "Retroceder 10s");

            // ── AVANZAR 10s ──────────────────────────────────────────────
            btnSeekFwd = MakeCtrlBtn("⏩", 42, 42, Theme.TextSecondary, BtnDefault);
            btnSeekFwd.Font = new Font("Segoe UI", 15F);
            btnSeekFwd.Click += (s, e) => _mediaPlayer.Time = Math.Min(_mediaPlayer.Length, _mediaPlayer.Time + 10000);
            new ToolTip().SetToolTip(btnSeekFwd, "Avanzar 10s");

            // ── SIGUIENTE ────────────────────────────────────────────────
            btnNext = MakeCtrlBtn("⏭", 42, 42, Theme.TextSecondary, BtnDefault);
            btnNext.Font = new Font("Segoe UI", 17F);
            btnNext.Click += (s, e) => NextTrack();
            new ToolTip().SetToolTip(btnNext, "Siguiente");

            // ── BUCLE — 3 estados visuales distintos ─────────────────────
            //   OFF  → ↻  gris, sin fondo
            //   ALL  → ↻  verde con fondo  (repite toda la lista)
            //   ONE  → 🔂  ámbar con fondo (repite solo esta)
            btnLoop = MakeCtrlBtn("↻", 42, 42, Theme.TextMuted, BtnDefault);
            btnLoop.Font = new Font("Segoe UI", 16F);
            btnLoop.Click += (s, e) => ToggleLoop();
            new ToolTip().SetToolTip(btnLoop, "Bucle: OFF");

            // ── MUTE ─────────────────────────────────────────────────────
            btnMute = MakeCtrlBtn("🔉", 38, 38, Theme.TextSecondary, BtnDefault);
            btnMute.Font = new Font("Segoe UI", 15F);
            btnMute.Click += (s, e) => ToggleMute();
            new ToolTip().SetToolTip(btnMute, "Silenciar");

            // ── VOLUMEN ───────────────────────────────────────────────────
            volBar = new TrackBar
            {
                Size = new Size(100, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 70,
                TickStyle = TickStyle.None,
                BackColor = CtrlBg
            };
            volBar.ValueChanged += (s, e) => _mediaPlayer.Volume = volBar.Value;

            // ── TIEMPO ────────────────────────────────────────────────────
            timeLabel = new Label
            {
                Size = new Size(120, 22),
                Text = "0:00 / 0:00",
                Font = Theme.FontMonoSmall,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ── VELOCIDAD ─────────────────────────────────────────────────
            speedCombo = new ComboBox
            {
                Size = new Size(66, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BtnDefault,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmall
            };
            foreach (var s in new[] { "0.25×", "0.5×", "0.75×", "1×", "1.25×", "1.5×", "2×", "3×" })
                speedCombo.Items.Add(s);
            speedCombo.SelectedIndex = 3; // 1×
            speedCombo.SelectedIndexChanged += (s, e) =>
            {
                float[] rates = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f };
                _mediaPlayer.SetRate(rates[speedCombo.SelectedIndex]);
            };

            // ── WEBCAM ────────────────────────────────────────────────────
            btnCamRecord = new Button
            {
                Text = "📷",
                Size = new Size(42, 42),
                BackColor = Color.FromArgb(12, 36, 70),
                ForeColor = AccentBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 16F),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnCamRecord.FlatAppearance.BorderSize = 1;
            btnCamRecord.FlatAppearance.BorderColor = Color.FromArgb(96, 165, 250, 80);
            btnCamRecord.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 55, 100);
            btnCamRecord.Click += (s, e) => ToggleWebcam();
            new ToolTip().SetToolTip(btnCamRecord, "Abrir/cerrar webcam");

            lblCamStatus = new Label
            {
                Text = "",
                Width = 72,
                Height = 22,
                ForeColor = Theme.Coral,
                Font = Theme.FontMonoSmall,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ── PANTALLA COMPLETA ─────────────────────────────────────────
            btnFullscreen = MakeCtrlBtn("⛶", 42, 42, Theme.TextSecondary, BtnDefault);
            btnFullscreen.Font = new Font("Segoe UI", 16F);
            btnFullscreen.Click += (s, e) => ToggleFullscreen();
            new ToolTip().SetToolTip(btnFullscreen, "Pantalla completa (F)");

            // ── GPS toggle ────────────────────────────────────────────────
            // (se añade al right panel, no aquí)

            // ════════════════════════════════════════════════════════════
            //  POSICIONAMIENTO via Resize — sin solapamiento garantizado
            //  Layout:
            //    IZQUIERDA: Prev · SeekBack · Stop · Play · SeekFwd · Next
            //    CENTRO:    Tiempo · Velocidad
            //    DERECHA:   Bucle · Mute · Volumen · Webcam · Camstatus · Fullscreen
            // ════════════════════════════════════════════════════════════
            controlBar.Controls.AddRange(new Control[]
            {
                btnPrev, btnSeekBack, btnStop, btnPlayPause, btnSeekFwd, btnNext,
                timeLabel, speedCombo,
                btnLoop, btnMute, volBar, btnCamRecord, lblCamStatus, btnFullscreen
            });

            controlBar.Resize += (s, e) => LayoutControlBar();
            controlBar.HandleCreated += (s, e) => LayoutControlBar();
        }

        private void LayoutControlBar()
        {
            int h = controlBar.Height;          // 70
            int cy = (h - btnPlayPause.Height) / 2;  // centro vertical para play
            int cs = (h - 42) / 2;              // centro vertical botones 42px
            int cx = 14;                         // cursor X izquierda

            // ── Grupo izquierdo ───────────────────────────────────────────
            btnPrev.Location = new Point(cx, cs); cx += 50;
            btnSeekBack.Location = new Point(cx, cs); cx += 50;
            btnStop.Location = new Point(cx, cs); cx += 50;
            btnPlayPause.Location = new Point(cx, cy); cx += btnPlayPause.Width + 8;
            btnSeekFwd.Location = new Point(cx, cs); cx += 50;
            btnNext.Location = new Point(cx, cs); cx += 56;

            // ── Centro: tiempo y velocidad ────────────────────────────────
            timeLabel.Location = new Point(cx, (h - 22) / 2); cx += timeLabel.Width + 8;
            speedCombo.Location = new Point(cx, (h - 28) / 2);

            // ── Grupo derecho (posicionar desde la derecha) ───────────────
            int rx = controlBar.Width - 14;

            btnFullscreen.Location = new Point(rx - 42, cs); rx -= 50;
            lblCamStatus.Location = new Point(rx - lblCamStatus.Width, (h - 22) / 2); rx -= lblCamStatus.Width + 4;
            btnCamRecord.Location = new Point(rx - 42, cs); rx -= 52;
            volBar.Location = new Point(rx - 100, (h - 30) / 2); rx -= 106;
            btnMute.Location = new Point(rx - 38, (h - 38) / 2); rx -= 48;
            btnLoop.Location = new Point(rx - 42, cs);
        }

        // ── Factory: botón de control con colores explícitos ─────────────────
        private static Button MakeCtrlBtn(string text, int w, int hh, Color fg, Color bg)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(w, hh),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = BtnHover;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 60, 80);
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TOGGLE LOOP — 3 estados visuales claramente distintos
        //    0 = OFF   → ↻ gris sin fondo
        //    1 = ALL   → ↻ verde con fondo  (bucle de toda la lista)
        //    2 = ONE   → 🔂 ámbar con fondo (bucle de 1 sola canción)
        // ════════════════════════════════════════════════════════════════════
        private int _loopMode = 0;

        private void ToggleLoop()
        {
            _loopMode = (_loopMode + 1) % 3;
            isLooping = _loopMode > 0;   // compatibilidad con OnEndReached

            switch (_loopMode)
            {
                case 0:   // OFF
                    btnLoop.Text = "↻";
                    btnLoop.ForeColor = Theme.TextMuted;
                    btnLoop.BackColor = BtnDefault;
                    btnLoop.Font = new Font("Segoe UI", 16F);
                    new ToolTip().SetToolTip(btnLoop, "Bucle: OFF");
                    break;
                case 1:   // Repetir toda la lista
                    btnLoop.Text = "↻";
                    btnLoop.ForeColor = LoopColor;
                    btnLoop.BackColor = LoopColorDim;
                    btnLoop.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
                    new ToolTip().SetToolTip(btnLoop, "Repetir lista completa");
                    break;
                case 2:   // Repetir solo este video
                    btnLoop.Text = "🔂";
                    btnLoop.ForeColor = LoopOneColor;
                    btnLoop.BackColor = LoopOneDim;
                    btnLoop.Font = new Font("Segoe UI", 13F);
                    new ToolTip().SetToolTip(btnLoop, "Repetir este video");
                    break;
            }
        }

        private void ToggleMute()
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.Mute = !_mediaPlayer.Mute;
            btnMute.ForeColor = _mediaPlayer.Mute ? Theme.Coral : Theme.TextSecondary;
            btnMute.Text = _mediaPlayer.Mute ? "🔇" : "🔉";
        }

        private void TogglePlay()
        {
            if (_mediaPlayer == null) return;
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                btnPlayPause.Text = "▶";
            }
            else
            {
                _mediaPlayer.Play();
                btnPlayPause.Text = "⏸";
            }
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevWindowState = WindowState;
                prevBorder = FormBorderStyle;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                foreach (Control c in new Control[] { playlistPanel, controlBar, rightPanel })
                    c.Visible = false;
                isFullscreen = true;
            }
            else
            {
                FormBorderStyle = prevBorder;
                WindowState = prevWindowState;
                foreach (Control c in new Control[] { playlistPanel, controlBar, rightPanel })
                    c.Visible = true;
                isFullscreen = false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ON END REACHED — respeta _loopMode
        // ════════════════════════════════════════════════════════════════════
        private void OnEndReached()
        {
            switch (_loopMode)
            {
                case 2:   // repetir este video
                    PlayAt(currentIndex);
                    break;
                case 1:   // repetir lista
                    NextTrack();
                    break;
                default:  // sin bucle
                    if (currentIndex < listView.Items.Count - 1)
                        NextTrack();
                    // else se queda al final
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void BuildPlaylist()
        {
            var hdr = new Panel
            {
                Height = 32,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface
            };
            var title = new Label
            {
                Text = "  Lista de reproducción",
                Dock = DockStyle.Fill,
                Font = Theme.FontSmallBold,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var bAdd = Theme.MakeButton("＋ Agregar", 100, Theme.ButtonKind.Primary);
            bAdd.Dock = DockStyle.Right;
            bAdd.Height = 32;
            bAdd.Click += (s, e) => AddFilesDialog();
            hdr.Controls.Add(title);
            hdr.Controls.Add(bAdd);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = Theme.BgBase,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                BorderStyle = BorderStyle.None,
                MultiSelect = false,
                AllowDrop = true
            };
            listView.Columns.Add("#", 36);
            listView.Columns.Add("Archivo", 480);
            listView.Columns.Add("Tamaño", 80);
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0) PlayAt(listView.SelectedItems[0].Index);
            };
            listView.DragEnter += (s, e) => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            listView.DragDrop += (s, e) =>
            {
                if (e.Data!.GetDataPresent(DataFormats.FileDrop))
                    foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)!)
                        if (IsVideo(f)) AddFile(f);
            };

            playlistPanel.Controls.Add(listView);
            playlistPanel.Controls.Add(hdr);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PROPERTIES PANEL
        // ════════════════════════════════════════════════════════════════════
        private Panel propsPanel = null!;

        private void BuildPropsPanel()
        {
            propsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgSurface };

            var hdr = new Panel { Height = 42, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            var title = new Label
            {
                Text = "Propiedades",
                Dock = DockStyle.Fill,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent2,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            btnGps = Theme.MakeButton("📍 GPS", 80, Theme.ButtonKind.Success);
            btnGps.Dock = DockStyle.Right;
            btnGps.Height = 42;
            btnGps.Click += (s, e) => ToggleGps();
            hdr.Controls.Add(title);
            hdr.Controls.Add(btnGps);

            string[] keys = { "Archivo:", "Duración:", "Tamaño:", "Formato:", "Resolución:", "FPS:", "Video:", "Audio:", "Canal:", "Ruta:" };
            propValues = new Label[keys.Length];
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgSurface };
            int py = 12;
            for (int i = 0; i < keys.Length; i++)
            {
                int lh = (i == 0 || i == 9) ? 36 : 22;
                scroll.Controls.Add(new Label { Text = keys[i], Left = 10, Top = py, Width = 72, Height = lh, Font = Theme.FontSmall, ForeColor = Theme.TextMuted });
                propValues[i] = new Label { Text = "—", Left = 86, Top = py, Width = 162, Height = lh, Font = Theme.FontSmallBold, ForeColor = Theme.TextPrimary, AutoEllipsis = true };
                scroll.Controls.Add(propValues[i]);
                py += lh + 4;
            }
            propsPanel.Controls.Add(scroll);
            propsPanel.Controls.Add(hdr);
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS PANEL
        // ════════════════════════════════════════════════════════════════════
        private void BuildGpsPanel()
        {
            gpsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 18, 24) };
            var hdr = new Panel { Height = 42, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            var back = Theme.MakeIconButton("←"); back.Dock = DockStyle.Left; back.Width = 40;
            back.Click += (s, e) => ToggleGps();
            var title = new Label { Text = "Ubicación GPS", Dock = DockStyle.Fill, Font = Theme.FontBodyBold, ForeColor = Theme.Teal, TextAlign = ContentAlignment.MiddleCenter };
            hdr.Controls.Add(title); hdr.Controls.Add(back);

            var info = new Panel { Height = 100, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(10) };
            gpsLatLbl = GLbl("Lat:   —");
            gpsLonLbl = GLbl("Lon:  —");
            gpsAltLbl = GLbl("Alt:   —");
            gpsDateLbl = GLbl("Fecha: —");
            int gy = 6;
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl })
            { l.Left = 10; l.Top = gy; l.Width = 236; info.Controls.Add(l); gy += 22; }

            var openBtn = Theme.MakeButton("Abrir en Maps", 0, Theme.ButtonKind.Success);
            openBtn.Dock = DockStyle.Bottom;
            openBtn.Height = 34;
            openBtn.Click += (s, e) => OpenGpsInBrowser();

            mapBrowser = new System.Windows.Forms.WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false,
                IsWebBrowserContextMenuEnabled = false
            };
            gpsPanel.Controls.Add(mapBrowser);
            gpsPanel.Controls.Add(openBtn);
            gpsPanel.Controls.Add(info);
            gpsPanel.Controls.Add(hdr);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYBACK
        // ════════════════════════════════════════════════════════════════════
        private void PlayAt(int idx)
        {
            if (_mediaPlayer == null) return;   // LibVLC aún no terminó de cargar
            if (listView.Items.Count == 0) return;
            idx = Math.Clamp(idx, 0, listView.Items.Count - 1);
            currentIndex = idx;

            foreach (ListViewItem li in listView.Items)
            {
                li.BackColor = Theme.BgBase;
                li.ForeColor = Theme.TextPrimary;
                li.SubItems[0].Text = (li.Index + 1).ToString();
            }
            listView.Items[idx].BackColor = Color.FromArgb(22, 40, 60);
            listView.Items[idx].ForeColor = AccentBlue;
            listView.Items[idx].SubItems[0].Text = "▶";
            listView.Items[idx].EnsureVisible();

            string path = listView.Items[idx].Tag!.ToString()!;
            _currentPath = path;

            using var media = new Media(_libVLC, new Uri(path));
            _mediaPlayer.Play(media);
            _mediaPlayer.Volume = volBar.Value;

            btnPlayPause.Text = "⏸";
            Text = $"Video — {Path.GetFileName(path)}";

            System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
            {
                if (IsHandleCreated) BeginInvoke(() => LoadMetadata(path));
            });

            if (_gpsLoadedFor != idx) { _gpsData = null; _gpsLoadedFor = -1; }
            if (gpsVisible) LoadGps(path, idx);
        }

        private void NextTrack()
        {
            if (listView.Items.Count == 0) return;
            int next = (currentIndex + 1) % listView.Items.Count;
            PlayAt(next);
        }

        private void PrevTrack()
        {
            if (_mediaPlayer == null || listView.Items.Count == 0) return;
            if (_mediaPlayer.Time > 3000) { _mediaPlayer.Time = 0; return; }
            PlayAt(currentIndex == 0 ? listView.Items.Count - 1 : currentIndex - 1);
        }

        private void UpdateProgress()
        {
            try
            {
                if (_mediaPlayer == null) return;   // aún inicializando
                if (!_mediaPlayer.IsPlaying && _mediaPlayer.State == VLCState.Paused)
                    btnPlayPause.Text = "▶";
                else if (_mediaPlayer.IsPlaying)
                    btnPlayPause.Text = "⏸";

                long dur = _mediaPlayer.Length;
                long pos = _mediaPlayer.Time;
                if (!isSeeking && dur > 0)
                    seekBar.Value = (int)((double)pos / dur * 1000);
                timeLabel.Text = $"{FmtTime(pos / 1000.0)} / {FmtTime(dur / 1000.0)}";
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  METADATA
        // ════════════════════════════════════════════════════════════════════
        private void LoadMetadata(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                propValues[0].Text = fi.Name;
                propValues[2].Text = FmtSize(fi.Length);
                propValues[3].Text = fi.Extension.TrimStart('.').ToUpper();
                propValues[9].Text = fi.FullName;

                long durMs = _mediaPlayer.Length;
                propValues[1].Text = durMs > 0 ? FmtTime(durMs / 1000.0) : "—";

                propValues[4].Text = "—";
                propValues[5].Text = "—";
                propValues[6].Text = "—";
                propValues[7].Text = "—";
                propValues[8].Text = "—";

                using var probMedia = new Media(_libVLC, new Uri(path));
                probMedia.Parse(MediaParseOptions.ParseLocal).Wait(5000);

                foreach (var track in probMedia.Tracks)
                {
                    if (track.TrackType == TrackType.Video)
                    {
                        var vd = track.Data.Video;
                        propValues[4].Text = $"{vd.Width}×{vd.Height}";
                        if (vd.FrameRateDen > 0)
                            propValues[5].Text = $"{(double)vd.FrameRateNum / vd.FrameRateDen:0.##} fps";
                        propValues[6].Text = !string.IsNullOrWhiteSpace(track.Description)
                            ? track.Description
                            : (track.Codec != 0 ? FourCCToString(track.Codec) : "—");
                    }
                    else if (track.TrackType == TrackType.Audio)
                    {
                        var ad = track.Data.Audio;
                        propValues[7].Text = !string.IsNullOrWhiteSpace(track.Description)
                            ? track.Description
                            : (track.Codec != 0 ? FourCCToString(track.Codec) : "—");
                        string chStr = ad.Channels == 1 ? "Mono"
                                     : ad.Channels == 2 ? "Stereo"
                                     : $"{ad.Channels} ch";
                        propValues[8].Text = ad.Rate > 0 ? $"{ad.Rate} Hz · {chStr}" : chStr;
                    }
                }
            }
            catch { }
        }

        private static string FourCCToString(uint fourcc)
        {
            if (fourcc == 0) return "—";
            char[] chars = {
                (char)(fourcc & 0xFF),
                (char)((fourcc >> 8) & 0xFF),
                (char)((fourcc >> 16) & 0xFF),
                (char)((fourcc >> 24) & 0xFF)
            };
            string s = new string(chars).Trim('\0', ' ');
            return string.IsNullOrWhiteSpace(s) ? "—" : s.ToUpper();
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private void ToggleGps()
        {
            gpsVisible = !gpsVisible;
            propsPanel.Visible = !gpsVisible;
            gpsPanel.Visible = gpsVisible;
            if (gpsVisible && _gpsLoadedFor != currentIndex && !string.IsNullOrEmpty(_currentPath))
                LoadGps(_currentPath, currentIndex);
        }

        private void LoadGps(string path, int index)
        {
            _gpsData = GpsReader.Read(path);
            _gpsLoadedFor = index;

            if (_gpsData == null || !_gpsData.HasGps)
            {
                gpsLatLbl.Text = "Sin datos GPS";
                gpsLatLbl.ForeColor = Theme.Warning;
                gpsLonLbl.Visible = gpsAltLbl.Visible = gpsDateLbl.Visible = false;
                mapBrowser.DocumentText = "<html><body style='background:#0e1218;color:#555;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>Sin GPS</body></html>";
                return;
            }

            var g = _gpsData;
            gpsLatLbl.Text = $"Lat:   {g.LatString}";
            gpsLonLbl.Text = $"Lon:  {g.LonString}";
            gpsAltLbl.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsDateLbl.Text = $"Fecha: {g.Date ?? "—"}";
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl })
            { l.ForeColor = Theme.TextPrimary; l.Visible = true; }

            SetBrowserEmulation();
            string ls = g.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lo = g.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            mapBrowser.DocumentText = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/>
<meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>*{{margin:0;padding:0}}html,body,#map{{width:100%;height:100%;background:#121216}}</style>
</head><body><div id='map'></div><script>
var map=L.map('map',{{attributionControl:false}}).setView([{ls},{lo}],15);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19}}).addTo(map);
L.marker([{ls},{lo}]).addTo(map).bindPopup('{g.Latitude:F5}°, {g.Longitude:F5}°').openPopup();
</script></body></html>";
        }

        private void OpenGpsInBrowser()
        {
            if (_gpsData == null || !_gpsData.HasGps) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"https://www.google.com/maps?q={_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                UseShellExecute = true
            });
        }

        private static void SetBrowserEmulation()
        {
            try
            {
                string app = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
                    ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                key?.SetValue(app, 11001, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WEBCAM
        // ════════════════════════════════════════════════════════════════════
        private void ToggleWebcam()
        {
            if (_camForm != null && !_camForm.IsDisposed)
            {
                if (_isCamRecording) StopCamRecording(autoAdd: false);
                _frameTimer?.Stop(); _frameTimer?.Dispose();
                _videoCapture?.Release(); _videoCapture?.Dispose();
                _videoCapture = null;
                _camTimer?.Stop(); _camTimer?.Dispose(); _camTimer = null!;
                _camForm.Close(); _camForm = null;
                btnCamRecord.Text = "📷";
                btnCamRecord.BackColor = Color.FromArgb(12, 36, 70);
                btnCamRecord.ForeColor = AccentBlue;
                lblCamStatus.Text = "";
                return;
            }
            OpenWebcamWindow();
        }

        private void OpenWebcamWindow()
        {
            _camForm = new Form
            {
                Text = "Webcam — Grabación",
                Size = new Size(640, 500),
                MinimumSize = new Size(480, 380),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(Left + Width + 10, Top),
                BackColor = Color.FromArgb(10, 10, 14),
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody
            };

            _camPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            var bottomBar = new Panel { Height = 52, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(20, 22, 30), Padding = new Padding(12, 8, 12, 8) };

            var btnPreview = Theme.MakeButton("▶ Ver", 80, Theme.ButtonKind.Primary);
            var btnRec = Theme.MakeButton("⏺ Grabar", 96, Theme.ButtonKind.Danger);
            var lblTimer = new Label { Text = "", Left = 210, Top = 16, Size = new Size(120, 22), ForeColor = Theme.Coral, Font = Theme.FontMonoSmall };

            btnPreview.Location = new Point(12, 10); btnPreview.Height = 32;
            btnRec.Location = new Point(100, 10); btnRec.Height = 32;

            _camTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _camTimer.Tick += (s2, e2) =>
            {
                _camSeconds++;
                lblTimer.Text = $"● REC {_camSeconds / 60:D2}:{_camSeconds % 60:D2}";
                lblCamStatus.Text = lblTimer.Text;
            };

            btnPreview.Click += (s, e) => StartCamPreview();
            btnRec.Click += (s, e) =>
            {
                if (!_isCamRecording) { StartCamRecording(); if (_isCamRecording) btnRec.Text = "⏹ Detener"; }
                else { StopCamRecording(true); btnRec.Text = "⏺ Grabar"; }
            };

            bottomBar.Controls.Add(btnPreview);
            bottomBar.Controls.Add(btnRec);
            bottomBar.Controls.Add(lblTimer);

            _camForm.Controls.Add(_camPictureBox);
            _camForm.Controls.Add(bottomBar);

            _camForm.FormClosed += (s, e) =>
            {
                StopCamRecording(false);
                _frameTimer?.Stop(); _frameTimer?.Dispose();
                _videoCapture?.Release(); _videoCapture?.Dispose();
                _videoCapture = null; _camForm = null;
                lblCamStatus.Text = "";
                btnCamRecord.Text = "📷";
                btnCamRecord.BackColor = Color.FromArgb(12, 36, 70);
                btnCamRecord.ForeColor = AccentBlue;
            };

            _camForm.Show(this);
            StartCamPreview();
        }

        private void StartCamPreview()
        {
            if (_videoCapture != null && _videoCapture.IsOpened()) return;
            try
            {
                _videoCapture = new OpenCvSharp.VideoCapture(0);
                _videoCapture.Open(0);
                if (!_videoCapture.IsOpened())
                { MessageBox.Show("No se pudo conectar a la cámara.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

                _frameTimer = new System.Windows.Forms.Timer { Interval = 33 };
                _frameTimer.Tick += (s, e) =>
                {
                    if (_videoCapture != null && _videoCapture.IsOpened())
                    {
                        using var frame = new OpenCvSharp.Mat();
                        _videoCapture.Read(frame);
                        if (!frame.Empty())
                        {
                            var old = _camPictureBox!.Image;
                            _camPictureBox.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
                            old?.Dispose();
                            if (_isCamRecording && _videoWriter != null && !_videoWriter.IsDisposed)
                                _videoWriter.Write(frame);
                        }
                    }
                };
                _frameTimer.Start();
            }
            catch (Exception ex) { MessageBox.Show($"Error al iniciar cámara:\n{ex.Message}"); }
        }

        private void StartCamRecording()
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
            { MessageBox.Show("La cámara debe estar encendida para grabar."); return; }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "GrabacionesWebcam");
            Directory.CreateDirectory(folder);
            _camFilePath = Path.Combine(folder, $"webcam_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            try
            {
                int w = (int)_videoCapture.Get(OpenCvSharp.VideoCaptureProperties.FrameWidth);
                int h = (int)_videoCapture.Get(OpenCvSharp.VideoCaptureProperties.FrameHeight);
                _videoWriter = new OpenCvSharp.VideoWriter(_camFilePath, OpenCvSharp.FourCC.MP4V, 30, new OpenCvSharp.Size(w, h));
                _isCamRecording = true;
                _camSeconds = 0;
                _camTimer?.Start();
                lblCamStatus.Text = "● REC";
            }
            catch (Exception ex) { MessageBox.Show($"Error al preparar grabación:\n{ex.Message}"); }
        }

        private async void StopCamRecording(bool autoAdd)
        {
            if (!_isCamRecording) return;
            _isCamRecording = false;
            _camTimer?.Stop();
            await System.Threading.Tasks.Task.Delay(200);
            _videoWriter?.Release(); _videoWriter?.Dispose(); _videoWriter = null;
            _camSeconds = 0;
            if (lblCamStatus != null && !lblCamStatus.IsDisposed) lblCamStatus.Text = "";

            if (autoAdd && _camFilePath != null && File.Exists(_camFilePath))
            {
                string saved = _camFilePath; _camFilePath = null;
                AddFile(saved);
                PlayAt(listView.Items.Count - 1);
                MessageBox.Show($"Grabación guardada y añadida:\n{saved}", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYLIST HELPERS
        // ════════════════════════════════════════════════════════════════════
        private void AddFile(string path)
        {
            var fi = new FileInfo(path);
            var item = new ListViewItem((listView.Items.Count + 1).ToString()) { Tag = path };
            item.SubItems.Add(fi.Name);
            item.SubItems.Add(FmtSize(fi.Length));
            listView.Items.Add(item);
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Agregar videos",
                Filter = "Videos|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.flv;*.ts;*.3gp;*.m4v;*.mpg;*.mpeg|Todos|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                foreach (string f in dlg.FileNames) AddFile(f);
        }

        // ════════════════════════════════════════════════════════════════════
        //  KEYBOARD
        // ════════════════════════════════════════════════════════════════════
        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_mediaPlayer == null) return;   // aún inicializando
            switch (e.KeyCode)
            {
                case Keys.Space: TogglePlay(); e.Handled = true; break;
                case Keys.Left: _mediaPlayer.Time = Math.Max(0, _mediaPlayer.Time - 10000); break;
                case Keys.Right: _mediaPlayer.Time = Math.Min(_mediaPlayer.Length, _mediaPlayer.Time + 10000); break;
                case Keys.Up: volBar.Value = Math.Min(100, volBar.Value + 5); break;
                case Keys.Down: volBar.Value = Math.Max(0, volBar.Value - 5); break;
                case Keys.M: ToggleMute(); break;
                case Keys.F: ToggleFullscreen(); break;
                case Keys.Escape: if (isFullscreen) ToggleFullscreen(); break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static bool IsVideo(string p) =>
            new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
                    ".m4v", ".ts", ".3gp", ".mpg", ".mpeg", ".vob", ".divx", ".ogv" }
            .Contains(Path.GetExtension(p).ToLower());

        private static string FmtTime(double s)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, s));
            return t.Hours > 0
                ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }

        private static string FmtSize(long b)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        private static Label GLbl(string t) => new()
        {
            Text = t,
            Height = 20,
            Font = Theme.FontMonoSmall,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer?.Stop(); uiTimer?.Dispose();
                _camTimer?.Stop(); _camTimer?.Dispose();
                _frameTimer?.Stop(); _frameTimer?.Dispose();
                if (_isCamRecording) { _videoWriter?.Release(); _videoWriter?.Dispose(); }
                _videoCapture?.Release(); _videoCapture?.Dispose();
                _mediaPlayer?.Stop(); _mediaPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}