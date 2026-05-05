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
    [DesignTimeVisible(false)]
    internal sealed class WmpControl : System.Windows.Forms.AxHost
    {
        private IWMPPlayer4? _ocx;
        public WmpControl() : base("6BF52A52-394A-11D3-B153-00C04F79FAA6") { Dock = DockStyle.Fill; }
        protected override void AttachInterfaces()
        { try { _ocx = GetOcx() as IWMPPlayer4; if (_ocx != null) { _ocx.uiMode = "none"; _ocx.stretchToFit = true; } } catch { } }
        public IWMPPlayer4? Player => _ocx;
        public string URL { set { try { if (_ocx != null) _ocx.URL = value; } catch { } } get { try { return _ocx?.URL ?? ""; } catch { return ""; } } }
        public void Play() { try { _ocx?.controls.play(); } catch { } }
        public void Pause() { try { _ocx?.controls.pause(); } catch { } }
        public void Stop() { try { _ocx?.controls.stop(); } catch { } }
        public double CurrentPosition { get { try { return _ocx?.controls.currentPosition ?? 0; } catch { return 0; } } set { try { if (_ocx?.controls != null) _ocx.controls.currentPosition = value; } catch { } } }
        public double Duration { get { try { return _ocx?.currentMedia?.duration ?? 0; } catch { return 0; } } }
        public int Volume { get { try { return _ocx?.settings.volume ?? 70; } catch { return 70; } } set { try { if (_ocx?.settings != null) _ocx.settings.volume = value; } catch { } } }
        public bool Mute { get { try { return _ocx?.settings.mute ?? false; } catch { return false; } } set { try { if (_ocx?.settings != null) _ocx.settings.mute = value; } catch { } } }
        public double Rate { set { try { if (_ocx?.settings != null) _ocx.settings.rate = value; } catch { } } }
        public WMPPlayState PlayState { get { try { return _ocx?.playState ?? WMPPlayState.wmppsStopped; } catch { return WMPPlayState.wmppsStopped; } } }
        public IWMPMedia? NewMedia(string path) { try { return _ocx?.newMedia(path); } catch { return null; } }
    }

    public class VideoPlayerForm : Form
    {
        private WmpControl wmp = null!;
        private Panel videoPanel = null!, controlBar = null!, playlistPanel = null!, rightPanel = null!;
        private TrackBar seekBar = null!, volBar = null!;
        private Label timeLabel = null!;
        private Button btnPlayPause = null!, btnMute = null!, btnLoop = null!, btnFullscreen = null!;
        private ComboBox speedCombo = null!;
        private ListView listView = null!;
        private Label[] propValues = null!;

        private readonly string initialPath;
        private string _currentPath;
        private System.Windows.Forms.Timer uiTimer = null!;
        private bool isSeeking, isLooping, isFullscreen;
        private int currentIndex = -1;
        private WMPPlayState lastState = WMPPlayState.wmppsStopped;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorder;

        // GPS
        private Panel gpsPanel = null!;
        private Label gpsLatLbl = null!, gpsLonLbl = null!, gpsAltLbl = null!, gpsDateLbl = null!;
        private System.Windows.Forms.WebBrowser mapBrowser = null!;
        private Button btnGps = null!;
        private bool gpsVisible;
        private GpsReader.GpsData? _gpsData;
        private int _gpsLoadedFor = -1;

        public VideoPlayerForm(string path)
        {
            initialPath = path; _currentPath = path;
            SetBrowserEmulation();
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
            BackColor = Theme.BgBase; ForeColor = Theme.TextPrimary;
            KeyPreview = true;
            KeyDown += Form_KeyDown;

            // WMP
            wmp = new WmpControl();
            ((ISupportInitialize)wmp).BeginInit();
            videoPanel = new Panel { BackColor = Color.Black, Dock = DockStyle.Fill };
            videoPanel.Controls.Add(wmp);
            ((ISupportInitialize)wmp).EndInit();

            // Seek
            var seekPanel = new Panel { Height = 20, Dock = DockStyle.Bottom, BackColor = Theme.BgBase };
            seekBar = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, BackColor = Theme.BgBase };
            seekBar.MouseDown += (s, e) => isSeeking = true;
            seekBar.MouseUp += (s, e) => { isSeeking = false; if (wmp.Duration > 0) wmp.CurrentPosition = seekBar.Value / 1000.0 * wmp.Duration; };
            seekPanel.Controls.Add(seekBar);

            // Control bar
            controlBar = new Panel { Height = 48, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };
            BuildControlBar();

            // Playlist
            playlistPanel = new Panel { Height = 120, Dock = DockStyle.Bottom, BackColor = Theme.BgBase };
            BuildPlaylist();

            // Right panel
            rightPanel = new Panel { Width = 260, Dock = DockStyle.Right, BackColor = Theme.BgSurface };
            BuildPropsPanel();
            BuildGpsPanel();
            gpsPanel.Visible = false;
            rightPanel.Controls.Add(gpsPanel);
            rightPanel.Controls.Add(propsPanel);

            var content = new Panel { Dock = DockStyle.Fill };
            content.Controls.Add(videoPanel);

            Controls.Add(content);
            Controls.Add(rightPanel);
            Controls.Add(seekPanel);
            Controls.Add(controlBar);
            Controls.Add(playlistPanel);

            uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            uiTimer.Tick += (s, e) => UpdateProgress();
            uiTimer.Start();
        }

        private void BuildControlBar()
        {
            int cx = 8;
            var btnPrev = CBtn("⏮", ref cx); btnPrev.Click += (s, e) => PrevTrack();
            var btnStop = CBtn("⏹", ref cx); btnStop.Click += (s, e) => wmp.Stop();
            btnPlayPause = CBtn("▶", ref cx); btnPlayPause.Click += (s, e) => TogglePlay();
            btnPlayPause.Width = 44; btnPlayPause.BackColor = Theme.AccentDim;
            btnPlayPause.FlatAppearance.BorderColor = Theme.Accent;
            var btnNext = CBtn("⏭", ref cx); btnNext.Click += (s, e) => NextTrack();

            cx += 8;
            btnMute = CBtn("♪", ref cx); btnMute.Click += (s, e) => ToggleMute();

            volBar = new TrackBar { Location = new Point(cx, 12), Size = new Size(80, 24), Minimum = 0, Maximum = 100, Value = 70, TickStyle = TickStyle.None, BackColor = Theme.BgSurface };
            volBar.ValueChanged += (s, e) => wmp.Volume = volBar.Value;
            controlBar.Controls.Add(volBar); cx += 84;

            timeLabel = new Label { Location = new Point(cx, 15), Size = new Size(110, 18), Text = "0:00 / 0:00", Font = Theme.FontMonoSmall, ForeColor = Theme.TextSecondary };
            controlBar.Controls.Add(timeLabel); cx += 114;

            speedCombo = new ComboBox { Location = new Point(cx, 12), Size = new Size(60, 24), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgElevated, ForeColor = Theme.TextPrimary, FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall };
            foreach (var s in new[] { "0.5x", "0.75x", "1x", "1.25x", "1.5x", "2x" }) speedCombo.Items.Add(s);
            speedCombo.SelectedIndex = 2;
            speedCombo.SelectedIndexChanged += (s, e) => { double[] r = { 0.5, 0.75, 1, 1.25, 1.5, 2 }; wmp.Rate = r[speedCombo.SelectedIndex]; };
            controlBar.Controls.Add(speedCombo);

            btnLoop = Theme.MakeIconButton("↻"); btnLoop.ForeColor = Theme.TextMuted;
            btnFullscreen = Theme.MakeIconButton("⛶"); btnFullscreen.ForeColor = Theme.TextMuted;
            btnLoop.Size = btnFullscreen.Size = new Size(34, 32);
            btnLoop.Anchor = btnFullscreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoop.Click += (s, e) => ToggleLoop();
            btnFullscreen.Click += (s, e) => ToggleFullscreen();
            controlBar.Controls.Add(btnLoop); controlBar.Controls.Add(btnFullscreen);
            controlBar.Resize += (s, e) => { btnFullscreen.Location = new Point(controlBar.Width - 42, 8); btnLoop.Location = new Point(controlBar.Width - 80, 8); };
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
            listView.DragDrop += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)!) if (IsVideo(f)) AddFile(f); };

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
            foreach (ListViewItem li in listView.Items) { li.BackColor = Theme.BgBase; li.ForeColor = Theme.TextPrimary; li.SubItems[0].Text = (li.Index + 1).ToString(); }
            listView.Items[idx].BackColor = Theme.BgSelected; listView.Items[idx].ForeColor = Theme.Accent;
            listView.Items[idx].SubItems[0].Text = "▶"; listView.Items[idx].EnsureVisible();

            string path = listView.Items[idx].Tag!.ToString()!;
            _currentPath = path;
            wmp.URL = path; wmp.Play();
            btnPlayPause.Text = "⏸";
            Text = $"Video — {Path.GetFileName(path)}";
            LoadMetadata(path);
            if (_gpsLoadedFor != idx) { _gpsData = null; _gpsLoadedFor = -1; }
            if (gpsVisible) LoadGps(path, idx);
        }

        private void TogglePlay()
        {
            if (wmp.PlayState == WMPPlayState.wmppsPlaying) { wmp.Pause(); btnPlayPause.Text = "▶"; }
            else { wmp.Play(); btnPlayPause.Text = "⏸"; }
        }

        private void NextTrack() { if (listView.Items.Count > 0) PlayAt((currentIndex + 1) % listView.Items.Count); }
        private void PrevTrack() { if (listView.Items.Count == 0) return; if (wmp.CurrentPosition > 3) { wmp.CurrentPosition = 0; return; } PlayAt(currentIndex == 0 ? listView.Items.Count - 1 : currentIndex - 1); }

        private void UpdateProgress()
        {
            try
            {
                var state = wmp.PlayState;
                if (state != lastState)
                {
                    lastState = state;
                    if (state == WMPPlayState.wmppsStopped)
                    { if (isLooping) BeginInvoke(() => { wmp.CurrentPosition = 0; wmp.Play(); btnPlayPause.Text = "⏸"; }); else NextTrack(); return; }
                    btnPlayPause.Text = state == WMPPlayState.wmppsPlaying ? "⏸" : "▶";
                }
                double dur = wmp.Duration, pos = wmp.CurrentPosition;
                if (!isSeeking && dur > 0) seekBar.Value = (int)(pos / dur * 1000);
                timeLabel.Text = $"{FmtTime(pos)} / {FmtTime(dur)}";
            }
            catch { }
        }

        private void ToggleMute() { wmp.Mute = !wmp.Mute; btnMute.ForeColor = wmp.Mute ? Theme.Danger : Theme.TextPrimary; }
        private void ToggleLoop() { isLooping = !isLooping; btnLoop.BackColor = isLooping ? Theme.AccentBg : Theme.BgElevated; btnLoop.ForeColor = isLooping ? Theme.Accent : Theme.TextMuted; }
        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            { prevWindowState = WindowState; prevBorder = FormBorderStyle; FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Maximized; foreach (Control c in new Control[] { playlistPanel, controlBar, rightPanel }) c.Visible = false; isFullscreen = true; }
            else
            { FormBorderStyle = prevBorder; WindowState = prevWindowState; foreach (Control c in new Control[] { playlistPanel, controlBar, rightPanel }) c.Visible = true; isFullscreen = false; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void AddFile(string path)
        {
            var fi = new FileInfo(path);
            var item = new ListViewItem((listView.Items.Count + 1).ToString()) { Tag = path };
            item.SubItems.Add(fi.Name); item.SubItems.Add(FmtSize(fi.Length));
            listView.Items.Add(item);
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog { Title = "Agregar videos", Filter = "Videos|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm|Todos|*.*", Multiselect = true };
            if (dlg.ShowDialog(this) == DialogResult.OK) foreach (string f in dlg.FileNames) AddFile(f);
        }

        // ════════════════════════════════════════════════════════════════════
        //  METADATA
        // ════════════════════════════════════════════════════════════════════
        private void LoadMetadata(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                propValues[0].Text = fi.Name; propValues[2].Text = FmtSize(fi.Length);
                propValues[3].Text = fi.Extension.TrimStart('.').ToUpper();
                propValues[9].Text = fi.FullName;
                IWMPMedia? media = wmp.NewMedia(path);
                if (media != null)
                {
                    propValues[1].Text = FmtTime(media.duration);
                    string w = media.getItemInfo("WM/VideoWidth"), h = media.getItemInfo("WM/VideoHeight");
                    propValues[4].Text = (w.Length > 0 && h.Length > 0) ? $"{w}×{h}" : "—";
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

                // Fallback para MOV/MP4 de iPhone
                string ext = Path.GetExtension(path).ToLower();
                if ((propValues[6].Text == "—" || propValues[7].Text == "—" || propValues[8].Text == "—")
                    && ext is ".mov" or ".mp4" or ".m4v" or ".3gp")
                {
                    var (vc, ac, ai) = ReadMovCodecs(path);
                    if (propValues[6].Text == "—" && vc != "—") propValues[6].Text = vc;
                    if (propValues[7].Text == "—" && ac != "—") propValues[7].Text = ac;
                    if (propValues[8].Text == "—" && ai != "—") propValues[8].Text = ai;
                }
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

        // ── Leer codec / audio directamente de átomos MP4/MOV ───────────────
        private static (string VideoCodec, string AudioCodec, string AudioInfo) ReadMovCodecs(string path)
        {
            string vc = "—", ac = "—", ai = "—";
            try
            {
                long fileSize = new FileInfo(path).Length;
                int readBytes = (int)Math.Min(fileSize, 8 * 1024 * 1024);
                byte[] data = new byte[readBytes];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    fs.Read(data, 0, readBytes);

                byte[] stsdMarker = System.Text.Encoding.ASCII.GetBytes("stsd");
                int pos = 0;
                while (pos < data.Length - 8)
                {
                    int found = IndexOfFrom(data, stsdMarker, pos);
                    if (found < 0) break;
                    int entryStart = found + 8 + 4;
                    if (entryStart + 8 > data.Length) { pos = found + 1; continue; }
                    string fourcc = System.Text.Encoding.ASCII.GetString(data, entryStart + 4, 4).Trim();
                    if (IsVideoFourcc(fourcc) && vc == "—") vc = FourccToName(fourcc);
                    else if (IsAudioFourcc(fourcc) && ac == "—") ac = FourccToName(fourcc);
                    pos = found + 1;
                }

                byte[] mp4aMarker = System.Text.Encoding.ASCII.GetBytes("mp4a");
                int mp4aPos = IndexOfFrom(data, mp4aMarker, 0);
                if (mp4aPos >= 0)
                {
                    int p = mp4aPos + 4 + 6 + 2 + 8;
                    if (p + 6 <= data.Length)
                    {
                        int channels = (data[p] << 8) | data[p + 1];
                        int srInt = (data[p + 4] << 8) | data[p + 5];
                        string chStr = channels == 1 ? "Mono" : channels == 2 ? "Stereo" : $"{channels} ch";
                        if (srInt > 0) ai = $"{srInt} Hz · {chStr}";
                        else ai = chStr;
                        if (ac == "—") ac = "AAC";
                    }
                }
            }
            catch { }
            return (vc, ac, ai);
        }

        private static bool IsVideoFourcc(string f) =>
            f is "avc1" or "hvc1" or "hev1" or "dvh1" or "dvhe" or
                 "mp4v" or "xvid" or "divx" or "vp08" or "vp09" or "av01";

        private static bool IsAudioFourcc(string f) =>
            f is "mp4a" or "ac-3" or "ec-3" or "Opus" or "opus" or
                 "sowt" or "twos" or "lpcm" or "alaw" or "ulaw" or "alac";

        private static string FourccToName(string f) => f.Trim() switch
        {
            "avc1" or "avc2" or "avc3" or "avc4" => "H264",
            "hvc1" or "hev1" => "H265 (HEVC)",
            "dvh1" or "dvhe" => "Dolby Vision",
            "mp4v" => "MPEG-4",
            "xvid" => "Xvid",
            "divx" => "DivX",
            "vp08" => "VP8",
            "vp09" => "VP9",
            "av01" => "AV1",
            "mp4a" => "AAC",
            "ac-3" => "AC-3 (Dolby)",
            "ec-3" => "E-AC-3",
            "Opus" or "opus" => "Opus",
            "alac" => "ALAC",
            "sowt" or "twos" or "lpcm" => "PCM",
            _ => f.Trim()
        };

        private static int IndexOfFrom(byte[] source, byte[] pattern, int startAt)
        {
            int limit = source.Length - pattern.Length;
            for (int i = startAt; i <= limit; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (source[i + j] != pattern[j]) { found = false; break; }
                if (found) return i;
            }
            return -1;
        }

        // ════════════════════════════════════════════════════════════════════
        //  GPS
        // ════════════════════════════════════════════════════════════════════
        private void ToggleGps()
        {
            gpsVisible = !gpsVisible;
            propsPanel.Visible = !gpsVisible; gpsPanel.Visible = gpsVisible;
            if (gpsVisible && _gpsLoadedFor != currentIndex && !string.IsNullOrEmpty(_currentPath))
                LoadGps(_currentPath, currentIndex);
        }

        private void LoadGps(string path, int index)
        {
            _gpsData = GpsReader.Read(path); _gpsLoadedFor = index;
            if (_gpsData == null || !_gpsData.HasGps)
            {
                gpsLatLbl.Text = "Sin datos GPS"; gpsLatLbl.ForeColor = Theme.Warning;
                gpsLonLbl.Visible = gpsAltLbl.Visible = gpsDateLbl.Visible = false;
                mapBrowser.DocumentText = $"<html><body style='background:#{Theme.BgBase.R:X2}{Theme.BgBase.G:X2}{Theme.BgBase.B:X2};color:#888;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'>Sin GPS</body></html>";
                return;
            }
            var g = _gpsData;
            gpsLatLbl.Text = $"Lat:   {g.LatString}"; gpsLonLbl.Text = $"Lon:  {g.LonString}";
            gpsAltLbl.Text = g.Altitude.HasValue ? $"Alt:   {g.Altitude.Value:0.0} m" : "Alt:   —";
            gpsDateLbl.Text = $"Fecha: {g.Date ?? "—"}";
            foreach (var l in new[] { gpsLatLbl, gpsLonLbl, gpsAltLbl, gpsDateLbl }) { l.ForeColor = Theme.TextPrimary; l.Visible = true; }
            LoadMap(g.Latitude, g.Longitude);
        }

        private void LoadMap(double lat, double lon)
        {
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
            { FileName = $"https://www.google.com/maps?q={_gpsData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_gpsData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}", UseShellExecute = true });
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
        private static bool IsVideo(string p) => new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".3gp" }.Contains(Path.GetExtension(p).ToLower());
        private static string FmtTime(double s) { var t = TimeSpan.FromSeconds(Math.Max(0, s)); return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}"; }
        private static string FmtSize(long b) { string[] u = { "B", "KB", "MB", "GB" }; double v = b; int i = 0; while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; } return $"{v:0.##} {u[i]}"; }
        private static Label GLbl(string t) => new() { Text = t, Height = 20, Font = Theme.FontMonoSmall, ForeColor = Theme.TextPrimary, BackColor = Color.Transparent, AutoEllipsis = true };

        private static void SetBrowserEmulation()
        { try { string app = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe"; using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"); key?.SetValue(app, 11001, RegistryValueKind.DWord); } catch { } }

        protected override void Dispose(bool disposing)
        { if (disposing) { uiTimer?.Stop(); uiTimer?.Dispose(); try { wmp?.Stop(); } catch { } wmp?.Dispose(); } base.Dispose(disposing); }
    }
}