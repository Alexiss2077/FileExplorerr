using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using WMPLib;
using Microsoft.Win32;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  WRAPPER WMP — extiende AxHost directamente, sin AxInterop.WMPLib
    //  No necesita la referencia COM "aximp", solo "tlbimp"
    // ════════════════════════════════════════════════════════════════════════
    [DesignTimeVisible(false)]
    internal sealed class WmpControl : System.Windows.Forms.AxHost
    {
        private IWMPPlayer4? _ocx;

        // CLSID del control ActiveX de Windows Media Player
        public WmpControl() : base("6BF52A52-394A-11D3-B153-00C04F79FAA6")
        {
            Dock = DockStyle.Fill;
        }

        protected override void AttachInterfaces()
        {
            try
            {
                _ocx = GetOcx() as IWMPPlayer4;
                if (_ocx == null) return;
                _ocx.uiMode = "none";
                _ocx.stretchToFit = true;
            }
            catch { }
        }

        public IWMPPlayer4? Player => _ocx;

        public string URL
        {
            set { try { if (_ocx != null) _ocx.URL = value; } catch { } }
            get { try { return _ocx?.URL ?? ""; } catch { return ""; } }
        }

        public void Play() { try { _ocx?.controls.play(); } catch { } }
        public void Pause() { try { _ocx?.controls.pause(); } catch { } }
        public void Stop() { try { _ocx?.controls.stop(); } catch { } }

        public double CurrentPosition
        {
            get { try { return _ocx?.controls.currentPosition ?? 0; } catch { return 0; } }
            set { try { if (_ocx?.controls != null) _ocx.controls.currentPosition = value; } catch { } }
        }

        public double Duration
        {
            get { try { return _ocx?.currentMedia?.duration ?? 0; } catch { return 0; } }
        }

        public int Volume
        {
            get { try { return _ocx?.settings.volume ?? 70; } catch { return 70; } }
            set { try { if (_ocx?.settings != null) _ocx.settings.volume = value; } catch { } }
        }

        public bool Mute
        {
            get { try { return _ocx?.settings.mute ?? false; } catch { return false; } }
            set { try { if (_ocx?.settings != null) _ocx.settings.mute = value; } catch { } }
        }

        public double Rate
        {
            set { try { if (_ocx?.settings != null) _ocx.settings.rate = value; } catch { } }
        }

        public WMPPlayState PlayState
        {
            get { try { return _ocx?.playState ?? WMPPlayState.wmppsStopped; } catch { return WMPPlayState.wmppsStopped; } }
        }

        public IWMPMedia? NewMedia(string path)
        {
            try { return _ocx?.newMedia(path); } catch { return null; }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  REPRODUCTOR DE VIDEO
    // ════════════════════════════════════════════════════════════════════════
    public class VideoPlayerForm : Form
    {
        static readonly Color BgDeep = Color.FromArgb(10, 14, 20);
        static readonly Color BgSurface = Color.FromArgb(17, 23, 33);
        static readonly Color BgRaised = Color.FromArgb(24, 32, 46);
        static readonly Color Accent = Color.FromArgb(56, 139, 253);
        static readonly Color TextPri = Color.FromArgb(220, 232, 248);
        static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        static readonly Color Border = Color.FromArgb(38, 50, 70);

        private WmpControl wmp = null!;
        private Panel videoPanel = null!;
        private Panel rightPanel = null!;
        private Panel propsPanel = null!;
        private Panel gpsPanel = null!;
        private Panel seekPanel = null!;
        private Panel controlBar = null!;
        private Panel playlistSection = null!;
        private TrackBar seekBar = null!;
        private Label timeLabel = null!;
        private Button btnPlayPause = null!;
        private Button btnMute = null!;
        private TrackBar volBar = null!;
        private Label volLabel = null!;
        private ComboBox speedCombo = null!;
        private Button btnShuffle = null!;
        private Button btnLoop = null!;
        private Button btnFullscreen = null!;
        private Button btnGps = null!;
        private Label[] propValues = null!;
        private ListView listView = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private Label gpsLatLbl = null!, gpsLonLbl = null!,
                      gpsAltLbl = null!, gpsDateLbl = null!;

        private readonly string initialPath;
        private System.Windows.Forms.Timer uiTimer = null!;
        private bool isSeeking, isLooping, isShuffled, gpsVisible;
        private int currentIndex = -1;
        private WMPPlayState lastState = WMPPlayState.wmppsStopped;
        private GpsReader.GpsData? _gpsData;
        private int _gpsLoadedFor = -1;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorder;
        private bool isFullscreen;

        public VideoPlayerForm(string path)
        {
            initialPath = path;
            _currentPath = path;
            SetBrowserEmulation();
            BuildUI();
            AddFile(path);
            PlayAt(0);
        }

        private void BuildUI()
        {
            Text = $"VideoPlayer — {Path.GetFileName(initialPath)}";
            Size = new Size(1300, 800);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDeep; ForeColor = TextPri;
            KeyPreview = true;
            KeyDown += Form_KeyDown;

            // ── WMP ──────────────────────────────────────────────────────────
            wmp = new WmpControl();
            ((ISupportInitialize)wmp).BeginInit();
            videoPanel = new Panel { BackColor = Color.Black, Dock = DockStyle.Fill };
            videoPanel.Controls.Add(wmp);
            ((ISupportInitialize)wmp).EndInit();

            // ── Seek bar ─────────────────────────────────────────────────────
            seekPanel = new Panel
            {
                Height = 22,
                Dock = DockStyle.Bottom,
                BackColor = BgDeep,
                Padding = new Padding(0, 4, 0, 0)
            };
            seekBar = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1000,
                TickStyle = TickStyle.None,
                BackColor = BgDeep
            };
            seekBar.MouseDown += (s, e) => isSeeking = true;
            seekBar.MouseUp += (s, e) =>
            {
                isSeeking = false;
                double dur = wmp.Duration;
                if (dur > 0) wmp.CurrentPosition = seekBar.Value / 1000.0 * dur;
            };
            seekPanel.Controls.Add(seekBar);

            // ── Control bar ──────────────────────────────────────────────────
            controlBar = new Panel { Height = 50, Dock = DockStyle.Bottom, BackColor = BgSurface };
            controlBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0, controlBar.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, controlBar.Height - 1, controlBar.Width, controlBar.Height - 1);
            };
            BuildControlBar();

            // ── Playlist ─────────────────────────────────────────────────────
            playlistSection = new Panel { Height = 140, Dock = DockStyle.Bottom, BackColor = BgDeep };
            playlistSection.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, playlistSection.Width, 0);
            BuildPlaylist();

            // ── Right panel ──────────────────────────────────────────────────
            rightPanel = new Panel { Width = 290, Dock = DockStyle.Right, BackColor = BgSurface };
            rightPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, rightPanel.Height);
            BuildPropsPanel();
            BuildGpsPanel();
            propsPanel.Dock = DockStyle.Fill;
            gpsPanel.Dock = DockStyle.Fill;
            gpsPanel.Visible = false;
            rightPanel.Controls.Add(gpsPanel);
            rightPanel.Controls.Add(propsPanel);

            var content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(videoPanel);

            Controls.Add(content);
            Controls.Add(rightPanel);
            Controls.Add(seekPanel);
            Controls.Add(controlBar);
            Controls.Add(playlistSection);

            uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            uiTimer.Tick += (s, e) => UpdateProgress();
            uiTimer.Start();
        }

        // ────────────────────────────────────────────────────────────────────
        private void BuildControlBar()
        {
            int cx = 8;
            var btnPrev = CBtn("⏮", ref cx); btnPrev.Click += (s, e) => PrevTrack();
            var btnStop = CBtn("⏹", ref cx); btnStop.Click += (s, e) => wmp.Stop();
            btnPlayPause = CBtn("▶", ref cx); btnPlayPause.Click += (s, e) => TogglePlay();
            var btnNext = CBtn("⏭", ref cx); btnNext.Click += (s, e) => NextTrack();

            Sep(ref cx);

            btnMute = CBtn("🔊", ref cx);
            btnMute.Click += (s, e) => ToggleMute();

            volBar = new TrackBar
            {
                Location = new Point(cx, 12),
                Size = new Size(80, 26),
                Minimum = 0,
                Maximum = 100,
                Value = 70,
                TickStyle = TickStyle.None,
                BackColor = BgSurface
            };
            volBar.ValueChanged += (s, e) =>
            {
                wmp.Volume = volBar.Value;
                volLabel.Text = volBar.Value.ToString();
            };
            controlBar.Controls.Add(volBar); cx += 84;

            volLabel = ILbl(ref cx, "70", 34);
            timeLabel = ILbl(ref cx, "0:00 / 0:00", 112);
            Sep(ref cx);

            controlBar.Controls.Add(new Label
            {
                Location = new Point(cx, 16),
                Size = new Size(28, 18),
                Text = "vel:",
                Font = new Font("Segoe UI", 8F),
                ForeColor = TextSec
            });
            cx += 32;

            speedCombo = new ComboBox
            {
                Location = new Point(cx, 12),
                Size = new Size(64, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgRaised,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            foreach (var s in new[] { "0.25x", "0.5x", "0.75x", "1x", "1.25x", "1.5x", "2x", "3x" })
                speedCombo.Items.Add(s);
            speedCombo.SelectedIndex = 3;
            speedCombo.SelectedIndexChanged += (s, e) =>
            {
                double[] r = { 0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3 };
                wmp.Rate = r[speedCombo.SelectedIndex];
            };
            controlBar.Controls.Add(speedCombo);

            btnShuffle = RBtn("🔀"); btnShuffle.Click += (s, e) => ToggleShuffle();
            btnLoop = RBtn("↺"); btnLoop.Click += (s, e) => ToggleLoop();
            btnFullscreen = RBtn("⛶"); btnFullscreen.Click += (s, e) => ToggleFullscreen();
            controlBar.Resize += (s, e) => PosRight();
            PosRight();
        }

        private Button CBtn(string t, ref int x)
        {
            var b = Btn(t, BgRaised, Border);
            b.Location = new Point(x, 9); b.Size = new Size(34, 32);
            b.Font = new Font("Segoe UI", 13F);
            controlBar.Controls.Add(b); x += 38; return b;
        }

        private void Sep(ref int x)
        {
            controlBar.Controls.Add(new Panel
            { Location = new Point(x, 10), Size = new Size(1, 30), BackColor = Border });
            x += 9;
        }

        private Label ILbl(ref int x, string t, int w)
        {
            var l = new Label
            {
                Location = new Point(x, 15),
                Size = new Size(w, 20),
                Text = t,
                Font = new Font("Cascadia Code", 8.5F),
                ForeColor = TextPri
            };
            controlBar.Controls.Add(l); x += w + 4; return l;
        }

        private Button RBtn(string t)
        {
            var b = Btn(t, BgRaised, Border);
            b.Size = new Size(34, 32); b.Font = new Font("Segoe UI", 13F);
            b.ForeColor = TextSec; b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            controlBar.Controls.Add(b); return b;
        }

        private void PosRight()
        {
            int rx = controlBar.Width - 8;
            foreach (var b in new[] { btnFullscreen, btnLoop, btnShuffle })
            { b.Location = new Point(rx - b.Width, 9); rx -= b.Width + 4; }
        }

        // ────────────────────────────────────────────────────────────────────
        private void BuildPlaylist()
        {
            var hdr = new Panel { Height = 34, Dock = DockStyle.Top, BackColor = BgSurface };
            hdr.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0, hdr.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };
            var title = new Label
            {
                Text = "LISTA DE REPRODUCCIÓN",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 150, 200),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            var bAdd = SBtn("+ Agregar", Color.FromArgb(20, 55, 80), Accent);
            var bRemove = SBtn("Quitar", BgRaised, Border);
            var bClear = SBtn("Limpiar", BgRaised, Border);
            bAdd.Dock = bRemove.Dock = bClear.Dock = DockStyle.Right;
            bAdd.Click += (s, e) => AddFilesDialog();
            bRemove.Click += (s, e) => RemoveSelected();
            bClear.Click += (s, e) => listView.Items.Clear();
            hdr.Controls.Add(title);
            hdr.Controls.Add(bAdd); hdr.Controls.Add(bRemove); hdr.Controls.Add(bClear);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = BgDeep,
                ForeColor = TextPri,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                MultiSelect = false,
                AllowDrop = true
            };
            listView.Columns.Add("#", 36);
            listView.Columns.Add("Archivo", 500);
            listView.Columns.Add("Duración", 80);
            listView.Columns.Add("Tamaño", 80);
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    PlayAt(listView.SelectedItems[0].Index);
            };
            listView.DragEnter += (s, e) =>
                e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Copy : DragDropEffects.None;
            listView.DragDrop += (s, e) =>
            {
                if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
                foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)!)
                    if (IsVideo(f)) AddFile(f);
            };
            playlistSection.Controls.Add(listView);
            playlistSection.Controls.Add(hdr);
        }

        // ────────────────────────────────────────────────────────────────────
        private void BuildPropsPanel()
        {
            propsPanel = new Panel { BackColor = BgSurface };
            var hdr = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = BgRaised };
            hdr.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            var title = new Label
            {
                Text = "PROPIEDADES",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Accent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnGps = new Button
            {
                Text = "📍",
                Dock = DockStyle.Right,
                Width = 44,
                BackColor = Color.FromArgb(18, 55, 28),
                ForeColor = Color.FromArgb(80, 220, 120),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F),
                Cursor = Cursors.Hand
            };
            btnGps.FlatAppearance.BorderColor = Color.FromArgb(35, 130, 65);
            btnGps.Click += (s, e) => ToggleGps();
            hdr.Controls.Add(title); hdr.Controls.Add(btnGps);

            string[] keys = {
                "Archivo:","Duración:","Tamaño:","Formato:",
                "Resolución:","FPS:","Video codec:","Audio codec:","Audio:","Ruta:"
            };
            propValues = new Label[keys.Length];
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgSurface };
            int py = 12;
            for (int i = 0; i < keys.Length; i++)
            {
                bool tall = i == 0 || i == 9; int h = tall ? 38 : 22;
                scroll.Controls.Add(new Label
                {
                    Text = keys[i],
                    Left = 12,
                    Top = py,
                    Width = 90,
                    Height = h,
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = TextSec,
                    AutoEllipsis = true
                });
                propValues[i] = new Label
                {
                    Text = "—",
                    Left = 106,
                    Top = py,
                    Width = 168,
                    Height = h,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = TextPri,
                    AutoEllipsis = true
                };
                scroll.Controls.Add(propValues[i]);
                py += h + 6;
            }
            propsPanel.Controls.Add(scroll);
            propsPanel.Controls.Add(hdr);
        }

        // ────────────────────────────────────────────────────────────────────
        private void BuildGpsPanel()
        {
            gpsPanel = new Panel { BackColor = Color.FromArgb(12, 18, 28) };
            var hdr = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Color.FromArgb(17, 26, 38) };
            hdr.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            var back = Btn("◄", BgRaised, Border);
            back.Dock = DockStyle.Left; back.Width = 44;
            back.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            back.Click += (s, e) => ToggleGps();
            hdr.Controls.Add(new Label
            {
                Text = "📍  Ubicación GPS",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 210, 120),
                TextAlign = ContentAlignment.MiddleCenter
            });
            hdr.Controls.Add(back);

            var info = new Panel
            {
                Height = 114,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(14, 22, 34)
            };
            info.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, info.Height - 1, info.Width, info.Height - 1);
            gpsLatLbl = GLbl("Lat:   —"); gpsLonLbl = GLbl("Lon:  —");
            gpsAltLbl = GLbl("Alt:   —"); gpsDateLbl = GLbl("Fecha: —");
            int gy = 8;
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl })
            { l.Left = 12; l.Top = gy; l.Width = 262; info.Controls.Add(l); gy += 26; }

            var openBtn = Btn("🌐  Abrir en Google Maps", Color.FromArgb(20, 55, 100), Color.FromArgb(38, 80, 140));
            openBtn.Dock = DockStyle.Bottom; openBtn.Height = 34;
            openBtn.ForeColor = Color.FromArgb(100, 180, 255);
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
            if (listView.Items.Count == 0) return;
            idx = Math.Clamp(idx, 0, listView.Items.Count - 1);
            currentIndex = idx;

            foreach (ListViewItem li in listView.Items)
            {
                li.BackColor = BgDeep; li.ForeColor = TextPri;
                li.SubItems[0].Text = (li.Index + 1).ToString();
            }
            listView.Items[idx].BackColor = Color.FromArgb(18, 45, 85);
            listView.Items[idx].ForeColor = Accent;
            listView.Items[idx].SubItems[0].Text = "▶";
            listView.Items[idx].EnsureVisible();

            string path = listView.Items[idx].Tag!.ToString()!;
            _currentPath = path;
            wmp.URL = path;
            wmp.Play();
            btnPlayPause.Text = "⏸";
            Text = $"VideoPlayer — {Path.GetFileName(path)}";
            LoadMetadata(path);

            if (_gpsLoadedFor != idx) { _gpsData = null; _gpsLoadedFor = -1; }
            if (gpsVisible) LoadGps(path, idx);
        }

        private void TogglePlay()
        {
            if (wmp.PlayState == WMPPlayState.wmppsPlaying)
            { wmp.Pause(); btnPlayPause.Text = "▶"; }
            else
            { wmp.Play(); btnPlayPause.Text = "⏸"; }
        }

        private void NextTrack()
        {
            if (listView.Items.Count == 0) return;
            PlayAt(isShuffled
                ? new Random().Next(listView.Items.Count)
                : (currentIndex + 1) % listView.Items.Count);
        }

        private void PrevTrack()
        {
            if (listView.Items.Count == 0) return;
            if (wmp.CurrentPosition > 3) { wmp.CurrentPosition = 0; return; }
            PlayAt(currentIndex == 0 ? listView.Items.Count - 1 : currentIndex - 1);
        }

        private void UpdateProgress()
        {
            try
            {
                var state = wmp.PlayState;
                if (state != lastState)
                {
                    if (state == WMPPlayState.wmppsMediaEnded)
                    {
                        if (isLooping) { wmp.CurrentPosition = 0; wmp.Play(); }
                        else NextTrack();
                    }
                    btnPlayPause.Text = state == WMPPlayState.wmppsPlaying ? "⏸" : "▶";
                    lastState = state;
                }
                double dur = wmp.Duration;
                double pos = wmp.CurrentPosition;
                if (!isSeeking && dur > 0)
                    seekBar.Value = (int)(pos / dur * 1000);
                timeLabel.Text = $"{FmtTime(pos)} / {FmtTime(dur)}";
            }
            catch { }
        }

        private void ToggleMute()
        {
            wmp.Mute = !wmp.Mute;
            btnMute.Text = wmp.Mute ? "🔇" : "🔊";
            btnMute.ForeColor = wmp.Mute ? Color.FromArgb(200, 60, 60) : TextPri;
        }

        private void ToggleShuffle()
        {
            isShuffled = !isShuffled;
            btnShuffle.BackColor = isShuffled ? Color.FromArgb(20, 55, 100) : BgRaised;
            btnShuffle.ForeColor = isShuffled ? Accent : TextSec;
        }

        private void ToggleLoop()
        {
            isLooping = !isLooping;
            btnLoop.BackColor = isLooping ? Color.FromArgb(20, 55, 100) : BgRaised;
            btnLoop.ForeColor = isLooping ? Accent : TextSec;
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevWindowState = WindowState; prevBorder = FormBorderStyle;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                foreach (Control c in new Control[] { playlistSection, controlBar, seekPanel, rightPanel })
                    c.Visible = false;
                isFullscreen = true; btnFullscreen.BackColor = Color.FromArgb(20, 55, 100);
            }
            else
            {
                FormBorderStyle = prevBorder; WindowState = prevWindowState;
                foreach (Control c in new Control[] { playlistSection, controlBar, seekPanel, rightPanel })
                    c.Visible = true;
                isFullscreen = false; btnFullscreen.BackColor = BgRaised;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void AddFile(string path)
        {
            var fi = new FileInfo(path);
            string dur = "—";
            try { var m = wmp.NewMedia(path); if (m != null) dur = FmtTime(m.duration); } catch { }
            var item = new ListViewItem((listView.Items.Count + 1).ToString()) { Tag = path };
            item.SubItems.Add(fi.Name); item.SubItems.Add(dur); item.SubItems.Add(FmtSize(fi.Length));
            listView.Items.Add(item);
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Agregar videos",
                Filter = "Videos|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.3gp;*.mpg|Todos|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            foreach (string f in dlg.FileNames) AddFile(f);
        }

        private void RemoveSelected()
        {
            if (listView.SelectedItems.Count == 0) return;
            listView.Items.RemoveAt(listView.SelectedItems[0].Index);
            for (int i = 0; i < listView.Items.Count; i++)
                listView.Items[i].SubItems[0].Text = (i + 1).ToString();
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

                IWMPMedia? media = wmp.NewMedia(path);
                if (media != null)
                {
                    propValues[1].Text = FmtTime(media.duration);
                    string w = media.getItemInfo("WM/VideoWidth");
                    string h = media.getItemInfo("WM/VideoHeight");
                    propValues[4].Text = (w.Length > 0 && h.Length > 0) ? $"{w} × {h}" : "—";
                    string fr = media.getItemInfo("WM/VideoFrameRate");
                    propValues[5].Text = fr.Length > 0 && double.TryParse(fr,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double frd)
                        ? $"{frd / 1000.0:0.##} fps" : "—";
                    propValues[6].Text = FallbackMeta(media, "WM/VideoCodec", "WM/EncodingSettings");
                    propValues[7].Text = FallbackMeta(media, "WM/AudioCodec");
                    string sr = media.getItemInfo("WM/AudioSampleRate");
                    string ch = media.getItemInfo("WM/AudioChannels");
                    string cs = ch == "1" ? "Mono" : ch == "2" ? "Stereo" : (ch.Length > 0 ? ch + " ch" : "");
                    propValues[8].Text = sr.Length > 0 ? $"{sr} Hz{(cs.Length > 0 ? " · " + cs : "")}" : "—";
                }
                if (propValues[4].Text == "—" || propValues[5].Text == "—")
                    ReadShellMeta(path);
            }
            catch { }
        }

        private static string FallbackMeta(IWMPMedia m, params string[] keys)
        {
            foreach (var k in keys)
            { string v = m.getItemInfo(k); if (!string.IsNullOrWhiteSpace(v)) return v; }
            return "—";
        }

        private void ReadShellMeta(string path)
        {
            try
            {
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
                dynamic fld = sh.NameSpace(Path.GetDirectoryName(path));
                dynamic itm = fld.ParseName(Path.GetFileName(path));
                string res = (string)fld.GetDetailsOf(itm, 285);
                string fps = (string)fld.GetDetailsOf(itm, 292);
                string dur = (string)fld.GetDetailsOf(itm, 27);
                if (!string.IsNullOrWhiteSpace(res) && propValues[4].Text == "—") propValues[4].Text = res;
                if (!string.IsNullOrWhiteSpace(fps) && propValues[5].Text == "—") propValues[5].Text = fps;
                if (!string.IsNullOrWhiteSpace(dur) && propValues[1].Text == "—") propValues[1].Text = dur;
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private string _currentPath = "";

        private void ToggleGps()
        {
            gpsVisible = !gpsVisible;
            propsPanel.Visible = !gpsVisible;
            gpsPanel.Visible = gpsVisible;
            btnGps.BackColor = gpsVisible ? Color.FromArgb(20, 100, 45) : Color.FromArgb(18, 55, 28);

            if (!gpsVisible) return;

            // Usar _currentPath en lugar de acceder al listView por índice
            if (string.IsNullOrEmpty(_currentPath)) return;
            if (_gpsLoadedFor != currentIndex)
                LoadGps(_currentPath, currentIndex);
        }

        private void LoadGps(string path, int index)
        {
            _gpsData = GpsReader.Read(path); _gpsLoadedFor = index;
            if (_gpsData == null || !_gpsData.HasGps)
            {
                gpsLatLbl.Text = "Sin datos GPS en este video";
                gpsLatLbl.ForeColor = Color.FromArgb(160, 100, 60);
                gpsLonLbl.Visible = gpsAltLbl.Visible = gpsDateLbl.Visible = false;
                mapBrowser.DocumentText = "<html><body style='background:#0a0e14;color:#5a7090;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'><div style='text-align:center;font-size:14px'>📭<br><br>Sin coordenadas GPS</div></body></html>";
                return;
            }
            var g = _gpsData;
            gpsLatLbl.Text = $"Lat:   {g.LatString}";
            gpsLonLbl.Text = $"Lon:  {g.LonString}";
            gpsAltLbl.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsDateLbl.Text = $"Fecha: {g.Date ?? "—"}";
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl })
            { l.ForeColor = Color.FromArgb(180, 210, 255); l.Visible = true; }
            LoadMap(g.Latitude, g.Longitude);
        }

        private void LoadMap(double lat, double lon)
        {
            string ls = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lo = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            mapBrowser.DocumentText = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'/><meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>*{{margin:0;padding:0}}html,body,#map{{width:100%;height:100%;background:#0d1117}}</style>
</head><body><div id='map'></div><script>
var map=L.map('map',{{attributionControl:false}}).setView([{ls},{lo}],15);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19}}).addTo(map);
var ic=L.divIcon({{html:'<div style=""width:20px;height:20px;border-radius:50% 50% 50% 0;background:#3a8bfd;border:3px solid #fff;transform:rotate(-45deg)""></div>',iconSize:[20,20],iconAnchor:[10,20],className:''}});
L.marker([{ls},{lo}],{{icon:ic}}).addTo(map).bindPopup('📍 {lat:F5}°, {lon:F5}°').openPopup();
</script></body></html>";
        }

        private void OpenGpsInBrowser()
        {
            if (_gpsData == null || !_gpsData.HasGps) return;
            string url = $"https://www.google.com/maps?q={_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        // ════════════════════════════════════════════════════════════════════
        //  KEYBOARD
        // ════════════════════════════════════════════════════════════════════
        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space: TogglePlay(); e.Handled = true; break;
                case Keys.Left: wmp.CurrentPosition = Math.Max(0, wmp.CurrentPosition - 10); break;
                case Keys.Right: wmp.CurrentPosition = Math.Min(wmp.Duration, wmp.CurrentPosition + 10); break;
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
        private static bool IsVideo(string p)
        {
            return new[] {".mp4",".avi",".mkv",".mov",".wmv",".flv",
                          ".webm",".m4v",".ts",".3gp",".mpg",".mpeg",".vob"}
                .Contains(Path.GetExtension(p).ToLower());
        }

        private static string FmtTime(double s)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, s));
            return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
        }

        private static string FmtSize(long b)
        {
            string[] u = { "B", "KB", "MB", "GB" }; double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        private static Label GLbl(string t) => new Label
        {
            Text = t,
            Height = 22,
            Font = new Font("Cascadia Code", 8F),
            ForeColor = Color.FromArgb(180, 210, 255),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        private Button Btn(string t, Color bg, Color brd)
        {
            var b = new Button
            {
                Text = t,
                BackColor = bg,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = brd; return b;
        }

        private Button SBtn(string t, Color bg, Color brd)
        { var b = Btn(t, bg, brd); b.Height = 26; b.Width = 82; return b; }

        private static void SetBrowserEmulation()
        {
            try
            {
                string app = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
                    ?? Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                key?.SetValue(app, 11001, RegistryValueKind.DWord);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer?.Stop(); uiTimer?.Dispose();
                try { wmp?.Stop(); } catch { }
                wmp?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}