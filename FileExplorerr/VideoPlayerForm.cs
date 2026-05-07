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
        // ── LibVLC ───────────────────────────────────────────────────────────
        private LibVLC _libVLC = null!;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayer = null!;
        private VideoView videoView = null!;

        // ── Controles ────────────────────────────────────────────────────────
        private Panel controlBar = null!, playlistPanel = null!, rightPanel = null!;
        private TrackBar seekBar = null!, volBar = null!;
        private Label timeLabel = null!;
        private Button btnPlayPause = null!, btnMute = null!, btnLoop = null!, btnFullscreen = null!;
        private ComboBox speedCombo = null!;
        private ListView listView = null!;
        private Label[] propValues = null!;

        // ── Estado ───────────────────────────────────────────────────────────
        private readonly string initialPath;
        private string _currentPath;
        private System.Windows.Forms.Timer uiTimer = null!;
        private bool isSeeking, isLooping, isFullscreen;
        private int currentIndex = -1;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorder;
        private bool _endReached;

        // ── GPS ──────────────────────────────────────────────────────────────
        private Panel gpsPanel = null!;
        private Label gpsLatLbl = null!, gpsLonLbl = null!, gpsAltLbl = null!, gpsDateLbl = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private Button btnGps = null!;
        private bool gpsVisible;
        private GpsReader.GpsData? _gpsData;
        private int _gpsLoadedFor = -1;

        public VideoPlayerForm(string path)
        {
            initialPath = path;
            _currentPath = path;
            Core.Initialize();
            BuildUI();
            AddFile(path);
            PlayAt(0);
        }

        private void BuildUI()
        {
            Text = $"Video — {Path.GetFileName(initialPath)}";
            Size = new Size(1200, 750);
            MinimumSize = new Size(860, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            KeyPreview = true;
            KeyDown += Form_KeyDown;

            // ── LibVLC init ──────────────────────────────────────────────────
            _libVLC = new LibVLC("--no-xlib");
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            _mediaPlayer.EndReached += (s, e) => BeginInvoke(() => OnEndReached());
            _mediaPlayer.TimeChanged += (s, e) => { /* handled by timer */ };

            // ── VideoView ────────────────────────────────────────────────────
            videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                MediaPlayer = _mediaPlayer
            };

            // ── Seek ─────────────────────────────────────────────────────────
            var seekPanel = new Panel { Height = 20, Dock = DockStyle.Bottom, BackColor = Theme.BgBase };
            seekBar = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, BackColor = Theme.BgBase };
            seekBar.MouseDown += (s, e) => isSeeking = true;
            seekBar.MouseUp += (s, e) =>
            {
                isSeeking = false;
                if (_mediaPlayer.Length > 0)
                    _mediaPlayer.Position = seekBar.Value / 1000f;
            };
            seekPanel.Controls.Add(seekBar);

            // ── Control bar ──────────────────────────────────────────────────
            controlBar = new Panel { Height = 48, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };
            BuildControlBar();

            // ── Playlist ─────────────────────────────────────────────────────
            playlistPanel = new Panel { Height = 120, Dock = DockStyle.Bottom, BackColor = Theme.BgBase };
            BuildPlaylist();

            // ── Right panel ──────────────────────────────────────────────────
            rightPanel = new Panel { Width = 260, Dock = DockStyle.Right, BackColor = Theme.BgSurface };
            BuildPropsPanel();
            BuildGpsPanel();
            gpsPanel.Visible = false;
            rightPanel.Controls.Add(gpsPanel);
            rightPanel.Controls.Add(propsPanel);

            // ── Assembly ─────────────────────────────────────────────────────
            var content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(videoView);

            Controls.Add(content);
            Controls.Add(rightPanel);
            Controls.Add(seekPanel);
            Controls.Add(controlBar);
            Controls.Add(playlistPanel);

            uiTimer = new System.Windows.Forms.Timer { Interval = 400 };
            uiTimer.Tick += (s, e) => UpdateProgress();
            uiTimer.Start();
        }

        private void BuildControlBar()
        {
            int cx = 8;
            var btnPrev = CBtn("⏮", ref cx); btnPrev.Click += (s, e) => PrevTrack();
            var btnStop = CBtn("⏹", ref cx); btnStop.Click += (s, e) => _mediaPlayer.Stop();
            btnPlayPause = CBtn("▶", ref cx); btnPlayPause.Click += (s, e) => TogglePlay();
            btnPlayPause.Width = 44; btnPlayPause.BackColor = Theme.AccentDim;
            btnPlayPause.FlatAppearance.BorderColor = Theme.Accent;
            var btnNext = CBtn("⏭", ref cx); btnNext.Click += (s, e) => NextTrack();

            cx += 8;
            btnMute = CBtn("♪", ref cx); btnMute.Click += (s, e) => ToggleMute();

            volBar = new TrackBar { Location = new Point(cx, 12), Size = new Size(80, 24), Minimum = 0, Maximum = 100, Value = 70, TickStyle = TickStyle.None, BackColor = Theme.BgSurface };
            volBar.ValueChanged += (s, e) => _mediaPlayer.Volume = volBar.Value;
            controlBar.Controls.Add(volBar); cx += 84;

            timeLabel = new Label { Location = new Point(cx, 15), Size = new Size(110, 18), Text = "0:00 / 0:00", Font = Theme.FontMonoSmall, ForeColor = Theme.TextSecondary };
            controlBar.Controls.Add(timeLabel); cx += 114;

            speedCombo = new ComboBox { Location = new Point(cx, 12), Size = new Size(60, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgElevated, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall };
            foreach (var s in new[] { "0.5x", "0.75x", "1x", "1.25x", "1.5x", "2x" }) speedCombo.Items.Add(s);
            speedCombo.SelectedIndex = 2;
            speedCombo.SelectedIndexChanged += (s, e) =>
            {
                float[] rates = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
                _mediaPlayer.SetRate(rates[speedCombo.SelectedIndex]);
            };
            controlBar.Controls.Add(speedCombo);

            btnLoop = Theme.MakeIconButton("↻"); btnLoop.ForeColor = Theme.TextMuted;
            btnFullscreen = Theme.MakeIconButton("⛶"); btnFullscreen.ForeColor = Theme.TextMuted;
            btnLoop.Size = btnFullscreen.Size = new Size(34, 32);
            btnLoop.Anchor = btnFullscreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoop.Click += (s, e) => ToggleLoop();
            btnFullscreen.Click += (s, e) => ToggleFullscreen();
            controlBar.Controls.Add(btnLoop);
            controlBar.Controls.Add(btnFullscreen);
            controlBar.Resize += (s, e) =>
            {
                btnFullscreen.Location = new Point(controlBar.Width - 42, 8);
                btnLoop.Location = new Point(controlBar.Width - 80, 8);
            };
        }

        private Button CBtn(string t, ref int x)
        {
            var b = Theme.MakeIconButton(t);
            b.Location = new Point(x, 8); b.Size = new Size(34, 32);
            controlBar.Controls.Add(b); x += 38; return b;
        }

        private void BuildPlaylist()
        {
            var hdr = new Panel { Height = 30, Dock = DockStyle.Top, BackColor = Theme.BgSurface };
            var title = new Label { Text = "  Lista de reproducción", Dock = DockStyle.Fill, Font = Theme.FontSmallBold, ForeColor = Theme.TextMuted, TextAlign = ContentAlignment.MiddleLeft };
            var bAdd = Theme.MakeButton("+ Agregar", 90, Theme.ButtonKind.Primary); bAdd.Dock = DockStyle.Right; bAdd.Click += (s, e) => AddFilesDialog();
            hdr.Controls.Add(title); hdr.Controls.Add(bAdd);

            listView = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, BackColor = Theme.BgBase, ForeColor = Theme.TextPrimary, Font = Theme.FontBody, BorderStyle = BorderStyle.None, MultiSelect = false, AllowDrop = true };
            listView.Columns.Add("#", 30); listView.Columns.Add("Archivo", 480); listView.Columns.Add("Tamaño", 80);
            listView.DoubleClick += (s, e) => { if (listView.SelectedItems.Count > 0) PlayAt(listView.SelectedItems[0].Index); };
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

        private Panel propsPanel = null!;
        private void BuildPropsPanel()
        {
            propsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgSurface };
            var hdr = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            var title = new Label { Text = "Propiedades", Dock = DockStyle.Fill, Font = Theme.FontBodyBold, ForeColor = Theme.Accent, TextAlign = ContentAlignment.MiddleCenter };
            btnGps = Theme.MakeIconButton("📍", Theme.ButtonKind.Success); btnGps.Dock = DockStyle.Right; btnGps.Width = 40;
            btnGps.Click += (s, e) => ToggleGps();
            hdr.Controls.Add(title); hdr.Controls.Add(btnGps);

            string[] keys = { "Archivo:", "Duración:", "Tamaño:", "Formato:", "Resolución:", "FPS:", "Video:", "Audio:", "Canal:", "Ruta:" };
            propValues = new Label[keys.Length];
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgSurface };
            int py = 10;
            for (int i = 0; i < keys.Length; i++)
            {
                int h = (i == 0 || i == 9) ? 34 : 20;
                scroll.Controls.Add(new Label { Text = keys[i], Left = 10, Top = py, Width = 70, Height = h, Font = Theme.FontSmall, ForeColor = Theme.TextMuted });
                propValues[i] = new Label { Text = "—", Left = 84, Top = py, Width = 164, Height = h, Font = Theme.FontSmallBold, ForeColor = Theme.TextPrimary, AutoEllipsis = true };
                scroll.Controls.Add(propValues[i]);
                py += h + 4;
            }
            propsPanel.Controls.Add(scroll); propsPanel.Controls.Add(hdr);
        }

        private void BuildGpsPanel()
        {
            gpsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 18, 24) };
            var hdr = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            var back = Theme.MakeIconButton("←"); back.Dock = DockStyle.Left; back.Width = 40; back.Click += (s, e) => ToggleGps();
            var title = new Label { Text = "Ubicación GPS", Dock = DockStyle.Fill, Font = Theme.FontBodyBold, ForeColor = Theme.Success, TextAlign = ContentAlignment.MiddleCenter };
            hdr.Controls.Add(title); hdr.Controls.Add(back);

            var info = new Panel { Height = 100, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(10) };
            gpsLatLbl = GLbl("Lat:  —"); gpsLonLbl = GLbl("Lon: —"); gpsAltLbl = GLbl("Alt:  —"); gpsDateLbl = GLbl("Fecha: —");
            int gy = 6;
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl }) { l.Left = 10; l.Top = gy; l.Width = 236; info.Controls.Add(l); gy += 22; }

            var openBtn = Theme.MakeButton("Abrir en Maps", 0, Theme.ButtonKind.Primary);
            openBtn.Dock = DockStyle.Bottom; openBtn.Height = 32;
            openBtn.Click += (s, e) => OpenGpsInBrowser();

            mapBrowser = new System.Windows.Forms.WebBrowser { Dock = DockStyle.Fill, ScrollBarsEnabled = false, IsWebBrowserContextMenuEnabled = false };
            gpsPanel.Controls.Add(mapBrowser); gpsPanel.Controls.Add(openBtn); gpsPanel.Controls.Add(info); gpsPanel.Controls.Add(hdr);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYBACK
        // ════════════════════════════════════════════════════════════════════
        private void PlayAt(int idx)
        {
            if (listView.Items.Count == 0) return;
            idx = Math.Clamp(idx, 0, listView.Items.Count - 1);
            currentIndex = idx;
            _endReached = false;

            foreach (ListViewItem li in listView.Items)
            {
                li.BackColor = Theme.BgBase;
                li.ForeColor = Theme.TextPrimary;
                li.SubItems[0].Text = (li.Index + 1).ToString();
            }
            listView.Items[idx].BackColor = Theme.BgSelected;
            listView.Items[idx].ForeColor = Theme.Accent;
            listView.Items[idx].SubItems[0].Text = "▶";
            listView.Items[idx].EnsureVisible();

            string path = listView.Items[idx].Tag!.ToString()!;
            _currentPath = path;

            using var media = new Media(_libVLC, new Uri(path));
            _mediaPlayer.Play(media);
            _mediaPlayer.Volume = volBar.Value;

            btnPlayPause.Text = "⏸";
            Text = $"Video — {Path.GetFileName(path)}";

            // Cargar metadatos con un pequeño retraso para que VLC parsee
            System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
            {
                if (IsHandleCreated)
                    BeginInvoke(() => LoadMetadata(path));
            });

            if (_gpsLoadedFor != idx) { _gpsData = null; _gpsLoadedFor = -1; }
            if (gpsVisible) LoadGps(path, idx);
        }

        private void TogglePlay()
        {
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

        private void OnEndReached()
        {
            if (isLooping)
            {
                PlayAt(currentIndex);
            }
            else
            {
                NextTrack();
            }
        }

        private void NextTrack()
        {
            if (listView.Items.Count > 0)
                PlayAt((currentIndex + 1) % listView.Items.Count);
        }

        private void PrevTrack()
        {
            if (listView.Items.Count == 0) return;
            if (_mediaPlayer.Time > 3000)
            {
                _mediaPlayer.Time = 0;
                return;
            }
            PlayAt(currentIndex == 0 ? listView.Items.Count - 1 : currentIndex - 1);
        }

        private void UpdateProgress()
        {
            try
            {
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

        private void ToggleMute()
        {
            _mediaPlayer.Mute = !_mediaPlayer.Mute;
            btnMute.ForeColor = _mediaPlayer.Mute ? Theme.Danger : Theme.TextPrimary;
        }

        private void ToggleLoop()
        {
            isLooping = !isLooping;
            btnLoop.BackColor = isLooping ? Theme.AccentBg : Theme.BgElevated;
            btnLoop.ForeColor = isLooping ? Theme.Accent : Theme.TextMuted;
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
        //  PLAYLIST
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
                Filter = "Videos|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.flv;*.ts;*.3gp;*.m4v;*.mpg;*.mpeg;*.vob|Todos|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                foreach (string f in dlg.FileNames) AddFile(f);
        }

        // ════════════════════════════════════════════════════════════════════
        //  METADATA — usa LibVLC tracks para obtener info de codec
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

                // Duración
                long durMs = _mediaPlayer.Length;
                propValues[1].Text = durMs > 0 ? FmtTime(durMs / 1000.0) : "—";

                // Video tracks — usar Media parseada para obtener info de tracks
                propValues[4].Text = "—";
                propValues[5].Text = "—";
                propValues[6].Text = "—";
                propValues[7].Text = "—";
                propValues[8].Text = "—";

                using var probMedia = new Media(_libVLC, new Uri(path));
                probMedia.Parse(MediaParseOptions.ParseLocal).Wait(5000);

                foreach (var track in probMedia.Tracks)
                {
                    if (track.TrackType == LibVLCSharp.Shared.TrackType.Video)
                    {
                        var vd = track.Data.Video;
                        propValues[4].Text = $"{vd.Width}×{vd.Height}";

                        if (vd.FrameRateDen > 0)
                            propValues[5].Text = $"{(double)vd.FrameRateNum / vd.FrameRateDen:0.##} fps";

                        propValues[6].Text = !string.IsNullOrWhiteSpace(track.Description)
                            ? track.Description
                            : (track.Codec != 0 ? FourCCToString(track.Codec) : "—");
                    }
                    else if (track.TrackType == LibVLCSharp.Shared.TrackType.Audio)
                    {
                        var ad = track.Data.Audio;

                        propValues[7].Text = !string.IsNullOrWhiteSpace(track.Description)
                            ? track.Description
                            : (track.Codec != 0 ? FourCCToString(track.Codec) : "—");

                        string chStr = ad.Channels == 1 ? "Mono" : ad.Channels == 2 ? "Stereo" : $"{ad.Channels} ch";
                        propValues[8].Text = ad.Rate > 0 ? $"{ad.Rate} Hz · {chStr}" : chStr;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Convierte un codec FourCC (uint) a string legible
        /// </summary>
        private static string FourCCToString(uint fourcc)
        {
            if (fourcc == 0) return "—";
            char[] chars = new char[4];
            chars[0] = (char)(fourcc & 0xFF);
            chars[1] = (char)((fourcc >> 8) & 0xFF);
            chars[2] = (char)((fourcc >> 16) & 0xFF);
            chars[3] = (char)((fourcc >> 24) & 0xFF);
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
                gpsLatLbl.Text = "Sin datos GPS"; gpsLatLbl.ForeColor = Theme.Warning;
                gpsLonLbl.Visible = gpsAltLbl.Visible = gpsDateLbl.Visible = false;
                mapBrowser.DocumentText = $"<html><body style='background:#{Theme.BgBase.R:X2}{Theme.BgBase.G:X2}{Theme.BgBase.B:X2};color:#888;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>Sin GPS</body></html>";
                return;
            }

            var g = _gpsData;
            gpsLatLbl.Text = $"Lat:   {g.LatString}";
            gpsLonLbl.Text = $"Lon:  {g.LonString}";
            gpsAltLbl.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsDateLbl.Text = $"Fecha: {g.Date ?? "—"}";
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl })
            {
                l.ForeColor = Theme.TextPrimary;
                l.Visible = true;
            }
            LoadMap(g.Latitude, g.Longitude);
        }

        private void LoadMap(double lat, double lon)
        {
            SetBrowserEmulation();
            string ls = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lo = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            mapBrowser.DocumentText = $@"<!DOCTYPE html><html><head><meta charset='utf-8'/><meta http-equiv='X-UA-Compatible' content='IE=edge'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/><script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>*{{margin:0;padding:0}}html,body,#map{{width:100%;height:100%;background:#121216}}</style></head><body><div id='map'></div><script>
var map=L.map('map',{{attributionControl:false}}).setView([{ls},{lo}],15);L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{maxZoom:19}}).addTo(map);
L.marker([{ls},{lo}]).addTo(map).bindPopup('{lat:F5}°, {lon:F5}°').openPopup();</script></body></html>";
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
        //  KEYBOARD
        // ════════════════════════════════════════════════════════════════════
        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                    TogglePlay();
                    e.Handled = true;
                    break;
                case Keys.Left:
                    _mediaPlayer.Time = Math.Max(0, _mediaPlayer.Time - 10000);
                    break;
                case Keys.Right:
                    _mediaPlayer.Time = Math.Min(_mediaPlayer.Length, _mediaPlayer.Time + 10000);
                    break;
                case Keys.Up:
                    volBar.Value = Math.Min(100, volBar.Value + 5);
                    break;
                case Keys.Down:
                    volBar.Value = Math.Max(0, volBar.Value - 5);
                    break;
                case Keys.M:
                    ToggleMute();
                    break;
                case Keys.F:
                    ToggleFullscreen();
                    break;
                case Keys.Escape:
                    if (isFullscreen) ToggleFullscreen();
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static bool IsVideo(string p) =>
            new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".3gp",
                    ".mpg", ".mpeg", ".vob", ".divx", ".ogv", ".m2ts", ".mts" }
            .Contains(Path.GetExtension(p).ToLower());

        private static string FmtTime(double s)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, s));
            return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
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
                uiTimer?.Stop();
                uiTimer?.Dispose();
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}