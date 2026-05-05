using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
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
        private Label lblNowPlaying = null!, lblArtist = null!;
        private Label lblTime = null!, lblDuration = null!;
        private TrackBar seekBar = null!, volBar = null!;
        private Button btnPlay = null!, btnPrev = null!, btnNext = null!,
                       btnShuffle = null!, btnRepeat = null!, btnLyrics = null!;

        // ── Audio ────────────────────────────────────────────────────────────
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        private int currentIndex = -1;
        private bool isDraggingSeek;
        private System.Windows.Forms.Timer uiTimer = null!;

        // ── Modos ────────────────────────────────────────────────────────────
        private bool shuffleMode;
        private int repeatMode; // 0=off, 1=list, 2=song
        private int[]? shuffleOrder;
        private int shufflePos;

        internal static readonly string[] SupportedExtensions =
            { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".aiff" };

        public MusicPlayerForm(string initialFile)
        {
            BuildUI();
            LoadFolder(initialFile);
            uiTimer = new System.Windows.Forms.Timer { Interval = 400 };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void BuildUI()
        {
            Text = "Música";
            Size = new Size(1000, 640);
            MinimumSize = new Size(780, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;
            KeyPreview = true;
            KeyDown += OnKeyDown;
            DoubleBuffered = true;

            // ═══ TOP BAR ═════════════════════════════════════════════════════
            topBar = new Panel { Height = 48, Dock = DockStyle.Top, BackColor = Theme.BgSurface };

            lblNowPlaying = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Text = "Música"
            };

            var btnOpen = Theme.MakeButton("Abrir carpeta", 110, Theme.ButtonKind.Primary);
            btnOpen.Dock = DockStyle.Right;
            btnOpen.Click += (s, e) => OpenFolderDialog();

            var btnAdd = Theme.MakeButton("+ Agregar", 90);
            btnAdd.Dock = DockStyle.Right;
            btnAdd.Click += (s, e) => AddFilesDialog();

            var btnLoadPlaylist = Theme.MakeButton("Cargar playlist", 120);
            btnLoadPlaylist.Dock = DockStyle.Right;
            btnLoadPlaylist.Click += (s, e) => LoadPlaylist();

            var btnSavePlaylist = Theme.MakeButton("Guardar playlist", 130);
            btnSavePlaylist.Dock = DockStyle.Right;
            btnSavePlaylist.Click += (s, e) => SavePlaylist();

            topBar.Controls.Add(lblNowPlaying);
            topBar.Controls.Add(btnOpen);
            topBar.Controls.Add(btnAdd);
            topBar.Controls.Add(btnLoadPlaylist);
            topBar.Controls.Add(btnSavePlaylist);

            // ═══ RIGHT PANEL ═════════════════════════════════════════════════
            rightPanel = new Panel { Width = 260, Dock = DockStyle.Right, BackColor = Theme.BgSurface };

            coverBox = new PictureBox
            {
                Height = 240,
                Dock = DockStyle.Top,
                BackColor = Theme.BgElevated,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            lblArtist = new Label
            {
                Height = 36,
                Dock = DockStyle.Top,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.BgElevated,
                Text = ""
            };

            // ── Botones Editar Tags / Eliminar ──────────────────────────────
            var actionPanel = new Panel
            {
                Height = 36,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(4, 3, 4, 3)
            };

            var btnEditTags = Theme.MakeButton("Editar tags", 0, Theme.ButtonKind.Primary);
            btnEditTags.Dock = DockStyle.Left;
            btnEditTags.Width = 124;
            btnEditTags.Height = 30;
            btnEditTags.Click += async (s, e) => await EditTags();

            var btnRemove = Theme.MakeButton("Quitar", 0, Theme.ButtonKind.Danger);
            btnRemove.Dock = DockStyle.Right;
            btnRemove.Width = 100;
            btnRemove.Height = 30;
            btnRemove.Click += (s, e) => RemoveSelected();

            actionPanel.Controls.Add(btnEditTags);
            actionPanel.Controls.Add(btnRemove);

            var lyricsHeader = new Label
            {
                Height = 28,
                Dock = DockStyle.Top,
                Text = "  Letra",
                Font = Theme.FontSmallBold,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Theme.BgSurface
            };

            btnLyrics = Theme.MakeButton("Buscar letra", 0, Theme.ButtonKind.Primary);
            btnLyrics.Dock = DockStyle.Bottom;
            btnLyrics.Height = 32;
            btnLyrics.Click += async (s, e) => await SearchLyrics();

            lyricsBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgBase,
                ForeColor = Theme.TextSecondary,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = ""
            };

            rightPanel.Controls.Add(lyricsBox);
            rightPanel.Controls.Add(lyricsHeader);
            rightPanel.Controls.Add(btnLyrics);
            rightPanel.Controls.Add(actionPanel);
            rightPanel.Controls.Add(lblArtist);
            rightPanel.Controls.Add(coverBox);

            // ═══ GRID ════════════════════════════════════════════════════════
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both
            };
            Theme.StyleGrid(grid);
            grid.Columns.Add("Title", "Título");
            grid.Columns.Add("Artist", "Artista");
            grid.Columns.Add("Album", "Álbum");
            grid.Columns.Add("Duration", "Duración");
            grid.Columns.Add("Path", "Ruta");
            grid.Columns["Path"]!.Visible = false;
            grid.Columns["Duration"]!.FillWeight = 35;
            grid.Columns["Title"]!.FillWeight = 100;
            grid.Columns["Artist"]!.FillWeight = 80;
            grid.Columns["Album"]!.FillWeight = 70;
            grid.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await PlayTrack(e.RowIndex); };
            grid.AllowDrop = true;
            grid.DragEnter += (s, e) => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            grid.DragDrop += (s, e) =>
            {
                if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
                foreach (string f in (string[])e.Data.GetData(DataFormats.FileDrop)!)
                    if (SupportedExtensions.Contains(Path.GetExtension(f).ToLower())) AddFileToGrid(f);
            };

            // ═══ PROGRESS ════════════════════════════════════════════════════
            progressPanel = new Panel { Height = 30, Dock = DockStyle.Bottom, BackColor = Theme.BgBase, Padding = new Padding(12, 0, 12, 0) };
            lblTime = new Label { Text = "0:00", Dock = DockStyle.Left, Width = 44, ForeColor = Theme.Accent, Font = Theme.FontMonoSmall, TextAlign = ContentAlignment.MiddleCenter };
            lblDuration = new Label { Text = "0:00", Dock = DockStyle.Right, Width = 44, ForeColor = Theme.TextMuted, Font = Theme.FontMonoSmall, TextAlign = ContentAlignment.MiddleCenter };
            seekBar = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, BackColor = Theme.BgBase };
            seekBar.MouseDown += (s, e) => isDraggingSeek = true;
            seekBar.MouseUp += (s, e) => { isDraggingSeek = false; if (audioFile != null) audioFile.CurrentTime = TimeSpan.FromSeconds(seekBar.Value / 1000.0 * audioFile.TotalTime.TotalSeconds); };
            progressPanel.Controls.Add(seekBar);
            progressPanel.Controls.Add(lblDuration);
            progressPanel.Controls.Add(lblTime);

            // ═══ CONTROL BAR ═════════════════════════════════════════════════
            controlBar = new Panel { Height = 50, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };

            int cx = 12;
            btnPrev = AddCtrlBtn("⏮", ref cx);
            btnPrev.Click += async (s, e) => await ChangeTrack(-1);

            btnPlay = AddCtrlBtn("▶", ref cx);
            btnPlay.Width = 46; btnPlay.Font = Theme.FontIconBig;
            btnPlay.BackColor = Theme.AccentDim;
            btnPlay.FlatAppearance.BorderColor = Theme.Accent;
            btnPlay.Click += async (s, e) => await TogglePlay();
            cx += 10;

            btnNext = AddCtrlBtn("⏭", ref cx);
            btnNext.Click += async (s, e) => await ChangeTrack(1);

            cx += 16;
            btnShuffle = AddCtrlBtn("⇄", ref cx);
            btnShuffle.ForeColor = Theme.TextMuted;
            btnShuffle.Click += (s, e) => ToggleShuffle();

            btnRepeat = AddCtrlBtn("↻", ref cx);
            btnRepeat.ForeColor = Theme.TextMuted;
            btnRepeat.Click += (s, e) => ToggleRepeat();

            cx += 20;
            var volIcon = new Label { Text = "♪", Location = new Point(cx, 16), Size = new Size(20, 20), ForeColor = Theme.TextMuted, Font = Theme.FontBody };
            controlBar.Controls.Add(volIcon); cx += 22;
            volBar = new TrackBar { Location = new Point(cx, 12), Size = new Size(100, 26), Minimum = 0, Maximum = 100, Value = 70, TickStyle = TickStyle.None, BackColor = Theme.BgSurface };
            volBar.ValueChanged += (s, e) => { if (outputDevice != null) outputDevice.Volume = volBar.Value / 100f; };
            controlBar.Controls.Add(volBar);

            // ═══ ASSEMBLY ════════════════════════════════════════════════════
            Controls.Add(grid);
            Controls.Add(rightPanel);
            Controls.Add(progressPanel);
            Controls.Add(controlBar);
            Controls.Add(topBar);
        }

        private Button AddCtrlBtn(string text, ref int x)
        {
            var btn = Theme.MakeIconButton(text);
            btn.Location = new Point(x, 9);
            btn.Size = new Size(36, 32);
            controlBar.Controls.Add(btn);
            x += 40;
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGAR
        // ════════════════════════════════════════════════════════════════════
        private void LoadFolder(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (dir == null) return;
            grid.Rows.Clear();
            var audioFiles = Directory.GetFiles(dir).Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower())).OrderBy(f => f).ToList();
            int target = 0;
            for (int i = 0; i < audioFiles.Count; i++)
            { AddFileToGrid(audioFiles[i]); if (audioFiles[i].Equals(filePath, StringComparison.OrdinalIgnoreCase)) target = i; }
            if (grid.Rows.Count > 0) { currentIndex = target; _ = PlayTrack(currentIndex); }
        }

        private void AddFileToGrid(string path)
        {
            try
            {
                var tag = TagFile.Create(path);
                grid.Rows.Add(CleanTitle(tag.Tag.Title ?? Path.GetFileNameWithoutExtension(path)),
                    CleanArtist(tag.Tag.FirstPerformer ?? "—"), tag.Tag.Album ?? "—",
                    tag.Properties.Duration.ToString(@"mm\:ss"), path);
            }
            catch { grid.Rows.Add(Path.GetFileNameWithoutExtension(path), "—", "", "—", path); }
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog { Title = "Agregar audio", Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|Todos|*.*", Multiselect = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            foreach (string f in dlg.FileNames) if (SupportedExtensions.Contains(Path.GetExtension(f).ToLower())) AddFileToGrid(f);
        }

        private void OpenFolderDialog()
        {
            using var dlg = new FolderBrowserDialog { Description = "Carpeta con música" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            StopPlayback(); grid.Rows.Clear(); currentIndex = -1;
            foreach (string f in Directory.GetFiles(dlg.SelectedPath).Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower())).OrderBy(f => f))
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
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
            if (grid.Rows[index].Cells[0].Visible) grid.CurrentCell = grid.Rows[index].Cells[0];

            string title = grid.Rows[index].Cells["Title"].Value?.ToString() ?? "";
            string artist = grid.Rows[index].Cells["Artist"].Value?.ToString() ?? "";
            lblNowPlaying.Text = title;
            lblArtist.Text = artist;
            Text = $"Música — {title}";
            lyricsBox.Text = "";
            await LoadCover(path);

            try
            {
                audioFile = new AudioFileReader(path);
                outputDevice = new WaveOutEvent();
                outputDevice.Init(audioFile);
                outputDevice.Volume = volBar.Value / 100f;
                outputDevice.Play();
                seekBar.Maximum = 1000;
                lblDuration.Text = FormatTime(audioFile.TotalTime.TotalSeconds);
                SetPlayState(true);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void StopPlayback()
        {
            outputDevice?.Stop(); outputDevice?.Dispose(); outputDevice = null;
            audioFile?.Dispose(); audioFile = null;
            seekBar.Value = 0; lblTime.Text = "0:00";
            SetPlayState(false);
        }

        private async Task TogglePlay()
        {
            if (outputDevice == null || audioFile == null)
            { if (grid.Rows.Count > 0) await PlayTrack(grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : 0); return; }
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
                if (shufflePos >= shuffleOrder!.Length) { if (repeatMode == 0) { StopPlayback(); return; } GenerateShuffleOrder(); }
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
            btnPlay.BackColor = playing ? Theme.WarningDim : Theme.AccentDim;
            btnPlay.FlatAppearance.BorderColor = playing ? Theme.Warning : Theme.Accent;
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (audioFile == null || outputDevice == null) return;
            if (!isDraggingSeek)
            {
                double pos = audioFile.CurrentTime.TotalSeconds, dur = audioFile.TotalTime.TotalSeconds;
                if (dur > 0) seekBar.Value = Math.Min(seekBar.Maximum, (int)(pos / dur * 1000));
                lblTime.Text = FormatTime(pos);
            }
            if (audioFile.TotalTime.TotalSeconds - audioFile.CurrentTime.TotalSeconds <= 0.8 && outputDevice.PlaybackState == PlaybackState.Playing)
            { outputDevice.Pause(); BeginInvoke(async () => await ChangeTrack(1)); }
        }

        private void ToggleShuffle()
        {
            shuffleMode = !shuffleMode;
            btnShuffle.BackColor = shuffleMode ? Theme.AccentBg : Theme.BgElevated;
            btnShuffle.ForeColor = shuffleMode ? Theme.Accent : Theme.TextMuted;
            if (shuffleMode) GenerateShuffleOrder();
        }

        private void ToggleRepeat()
        {
            repeatMode = (repeatMode + 1) % 3;
            btnRepeat.Text = repeatMode == 2 ? "↺" : "↻";
            btnRepeat.BackColor = repeatMode > 0 ? Theme.AccentBg : Theme.BgElevated;
            btnRepeat.ForeColor = repeatMode > 0 ? Theme.Accent : Theme.TextMuted;
        }

        private void GenerateShuffleOrder()
        { var rng = new Random(); shuffleOrder = Enumerable.Range(0, grid.Rows.Count).OrderBy(_ => rng.Next()).ToArray(); shufflePos = 0; }

        // ════════════════════════════════════════════════════════════════════
        //  COVER
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadCover(string path)
        {
            coverBox.Image?.Dispose(); coverBox.Image = null;
            try { var tag = TagFile.Create(path); if (tag.Tag.Pictures.Length > 0) { using var ms = new MemoryStream(tag.Tag.Pictures[0].Data.Data); coverBox.Image = System.Drawing.Image.FromStream(ms); return; } } catch { }
            try
            {
                string artist = grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? "";
                string title = grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)) return;
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                client.DefaultRequestHeaders.Add("User-Agent", "FileExplorerr/1.0");
                string json = await client.GetStringAsync($"https://itunes.apple.com/search?term={Uri.EscapeDataString($"{artist} {title}")}&limit=3&entity=song");
                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");
                if (results.GetArrayLength() > 0)
                {
                    string coverUrl = results[0].GetProperty("artworkUrl100").GetString()!.Replace("100x100", "600x600");
                    byte[] imgData = await client.GetByteArrayAsync(coverUrl);
                    using var ms = new MemoryStream(imgData);
                    coverBox.Image = System.Drawing.Image.FromStream(ms);
                    await Task.Run(() => { try { var mp3 = TagFile.Create(path); mp3.Tag.Pictures = new TagLib.IPicture[] { new TagLib.Picture(imgData) { Type = TagLib.PictureType.FrontCover, MimeType = "image/png" } }; mp3.Save(); } catch { } });
                }
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  LETRA
        // ════════════════════════════════════════════════════════════════════
        private async Task SearchLyrics()
        {
            if (currentIndex < 0 || currentIndex >= grid.Rows.Count) return;
            string artist = NormalizeForSearch(grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? "");
            string title = NormalizeForSearch(grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? "");
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)) { lyricsBox.Text = "Sin datos."; return; }
            lyricsBox.Text = "Buscando..."; btnLyrics.Enabled = false;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add("User-Agent", "FileExplorerr/1.0");
                var response = await client.GetAsync($"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}");
                if (!response.IsSuccessStatusCode) { lyricsBox.Text = "No encontrada."; return; }
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                lyricsBox.Text = doc.RootElement.TryGetProperty("plainLyrics", out var lyr) && !string.IsNullOrWhiteSpace(lyr.GetString()) ? lyr.GetString()! : "No encontrada.";
            }
            catch (Exception ex) { lyricsBox.Text = $"Error: {ex.Message}"; }
            finally { btnLyrics.Enabled = true; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  GUARDAR PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void SavePlaylist()
        {
            if (grid.Rows.Count == 0) { MessageBox.Show("No hay canciones.", "Sin canciones", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using var dlg = new SaveFileDialog { Title = "Guardar Playlist", Filter = "Playlist (*.txt)|*.txt|Todos|*.*", FileName = "playlist.txt" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var rutas = new List<string>();
                foreach (DataGridViewRow row in grid.Rows)
                { string? ruta = row.Cells["Path"].Value?.ToString(); if (!string.IsNullOrEmpty(ruta)) rutas.Add(ruta); }
                File.WriteAllLines(dlg.FileName, rutas, System.Text.Encoding.UTF8);
                MessageBox.Show($"Playlist guardada:\n{dlg.FileName}\n\n{rutas.Count} canción(es)", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGAR PLAYLIST
        // ════════════════════════════════════════════════════════════════════
        private void LoadPlaylist()
        {
            using var dlg = new OpenFileDialog { Title = "Cargar Playlist", Filter = "Playlist (*.txt)|*.txt|Todos|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                StopPlayback(); grid.Rows.Clear(); currentIndex = -1;
                int cargadas = 0;
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
        //  EDITAR TAGS
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
                using var dlg = new TagEditDialog(
                    tag.Tag.Title ?? "", tag.Tag.FirstPerformer ?? "", tag.Tag.Album ?? "",
                    tag.Tag.Year, tag.Tag.Track,
                    tag.Tag.Genres?.Length > 0 ? tag.Tag.Genres[0] : "");

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

        // ════════════════════════════════════════════════════════════════════
        //  QUITAR DE LA LISTA
        // ════════════════════════════════════════════════════════════════════
        private void RemoveSelected()
        {
            if (grid.Rows.Count == 0 || grid.SelectedRows.Count == 0) return;
            int idx = grid.SelectedRows[0].Index;
            string title = grid.Rows[idx].Cells["Title"].Value?.ToString() ?? "";
            string artist = grid.Rows[idx].Cells["Artist"].Value?.ToString() ?? "";

            if (MessageBox.Show($"¿Quitar de la lista?\n\n{artist} — {title}", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (currentIndex == idx) { StopPlayback(); currentIndex = -1; }
            grid.Rows.RemoveAt(idx);
            if (currentIndex > idx) currentIndex--;
            if (grid.Rows.Count > 0) grid.Rows[Math.Min(idx, grid.Rows.Count - 1)].Selected = true;
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
        private static string FormatTime(double secs) { var t = TimeSpan.FromSeconds(Math.Max(0, secs)); return t.Hours > 0 ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}"; }

        private static string CleanArtist(string s)
        { if (string.IsNullOrWhiteSpace(s)) return "—"; return System.Text.RegularExpressions.Regex.Replace(s, @"\s*-?\s*(Topic|VEVO|Official)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim(); }

        private static string CleanTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Sin título";
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\([^)]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K|Visualizer|Explicit|Clean|Remaster)[^)]*?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\[[^\]]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K)[^\]]*?\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s.Trim();
        }

        private static string NormalizeForSearch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.ToLower().Trim();
            foreach (var sep in new[] { " feat.", " feat ", " ft.", " ft ", " featuring " })
            { int i = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase); if (i > 0) { s = s.Substring(0, i); break; } }
            return s.Trim();
        }

        protected override void Dispose(bool disposing) { if (disposing) { uiTimer?.Stop(); uiTimer?.Dispose(); StopPlayback(); } base.Dispose(disposing); }
    }
}