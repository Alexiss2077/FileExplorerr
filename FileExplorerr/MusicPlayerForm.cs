using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TagFile = TagLib.File;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  REPRODUCTOR DE MÚSICA INTEGRADO
    //  Basado en Stopify, adaptado al tema oscuro de FileExplorerr
    // ════════════════════════════════════════════════════════════════════════
    public class MusicPlayerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Panel topBar = null!;
        private Panel controlBar = null!;
        private Panel progressPanel = null!;
        private Panel volumePanel = null!;
        private Panel bottomBar = null!;
        private DataGridView grid = null!;
        private PictureBox coverBox = null!;
        private Panel rightPanel = null!;
        private RichTextBox lyricsBox = null!;
        private Label lblNowPlaying = null!;
        private Label lblTime = null!;
        private Label lblDuration = null!;
        private Label lblVolume = null!;
        private Label lblLyricsHeader = null!;
        private TrackBar seekBar = null!;
        private TrackBar volBar = null!;
        private Button btnPlay = null!, btnStop = null!, btnPrev = null!,
                       btnNext = null!, btnShuffle = null!, btnRepeat = null!,
                       btnLyrics = null!, btnAddFiles = null!;

        // ── Audio ────────────────────────────────────────────────────────────
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        private int currentIndex = -1;
        private bool isDraggingSeek = false;
        private System.Windows.Forms.Timer uiTimer = null!;

        // ── Modos ────────────────────────────────────────────────────────────
        private bool shuffleMode = false;
        private int repeatMode = 0; // 0=off, 1=list, 2=song
        private int[]? shuffleOrder;
        private int shufflePos = 0;

        // ── Extensiones soportadas ───────────────────────────────────────────
        internal static readonly string[] SupportedExtensions =
            { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".aiff" };

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════
        public MusicPlayerForm(string initialFile)
        {
            BuildUI();
            LoadFolder(initialFile);

            uiTimer = new System.Windows.Forms.Timer { Interval = 400 };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "Reproductor de Música";
            Size = new Size(1100, 700);
            MinimumSize = new Size(860, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 14, 20);
            ForeColor = Color.FromArgb(220, 232, 248);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            KeyDown += OnKeyDown;
            DoubleBuffered = true;

            // ── Top bar ─────────────────────────────────────────────────────
            topBar = new Panel
            {
                Height = 52,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(17, 23, 33)
            };
            topBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)),
                    0, topBar.Height - 1, topBar.Width, topBar.Height - 1);

            lblNowPlaying = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 139, 253),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Text = "Reproductor de Música"
            };

            btnAddFiles = MakeBtn("📁 Agregar", 110,
                Color.FromArgb(24, 32, 46), Color.FromArgb(38, 50, 70));
            btnAddFiles.Dock = DockStyle.Right;
            btnAddFiles.Click += (s, e) => AddFilesDialog();

            topBar.Controls.Add(lblNowPlaying);
            topBar.Controls.Add(btnAddFiles);

            // ── Right panel: cover + lyrics ──────────────────────────────────
            rightPanel = new Panel
            {
                Width = 280,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(14, 20, 30)
            };
            rightPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)),
                    0, 0, 0, rightPanel.Height);

            coverBox = new PictureBox
            {
                Height = 260,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(20, 26, 38),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            lblLyricsHeader = new Label
            {
                Height = 30,
                Dock = DockStyle.Top,
                Text = "  Letra",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 140, 180),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(17, 23, 33)
            };

            btnLyrics = MakeBtn("🔍 Buscar letra", 0,
                Color.FromArgb(20, 55, 100), Color.FromArgb(56, 139, 253));
            btnLyrics.Dock = DockStyle.Bottom;
            btnLyrics.Height = 32;
            btnLyrics.Click += async (s, e) => await SearchLyrics();

            lyricsBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(13, 18, 26),
                ForeColor = Color.FromArgb(200, 220, 245),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = ""
            };

            rightPanel.Controls.Add(lyricsBox);
            rightPanel.Controls.Add(lblLyricsHeader);
            rightPanel.Controls.Add(btnLyrics);
            rightPanel.Controls.Add(coverBox);

            // ── Grid ────────────────────────────────────────────────────────
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(10, 14, 20),
                GridColor = Color.FromArgb(28, 38, 54),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 28 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(17, 23, 33),
                    ForeColor = Color.FromArgb(110, 160, 210),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(17, 23, 33),
                    SelectionForeColor = Color.FromArgb(110, 160, 210)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(13, 18, 26),
                    ForeColor = Color.FromArgb(200, 220, 245),
                    Font = new Font("Segoe UI", 9F),
                    SelectionBackColor = Color.FromArgb(31, 70, 140),
                    SelectionForeColor = Color.White
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(15, 21, 31),
                    ForeColor = Color.FromArgb(200, 220, 245),
                    SelectionBackColor = Color.FromArgb(31, 70, 140),
                    SelectionForeColor = Color.White
                }
            };
            grid.Columns.Add("Title", "Título");
            grid.Columns.Add("Artist", "Artista");
            grid.Columns.Add("Album", "Álbum");
            grid.Columns.Add("Duration", "Duración");
            grid.Columns.Add("Path", "Ruta");
            grid.Columns["Path"]!.Visible = false;
            grid.Columns["Duration"]!.FillWeight = 40;
            grid.Columns["Title"]!.FillWeight = 100;
            grid.Columns["Artist"]!.FillWeight = 80;
            grid.Columns["Album"]!.FillWeight = 80;
            grid.CellDoubleClick += Grid_CellDoubleClick;

            // ── Progress panel ──────────────────────────────────────────────
            progressPanel = new Panel
            {
                Height = 36,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(14, 20, 30),
                Padding = new Padding(12, 0, 12, 0)
            };

            lblTime = new Label
            {
                Text = "0:00",
                Dock = DockStyle.Left,
                Width = 50,
                ForeColor = Color.FromArgb(56, 139, 253),
                Font = new Font("Cascadia Code", 8.5F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblDuration = new Label
            {
                Text = "0:00",
                Dock = DockStyle.Right,
                Width = 50,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Cascadia Code", 8.5F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            seekBar = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1000,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(14, 20, 30)
            };
            seekBar.MouseDown += (s, e) => isDraggingSeek = true;
            seekBar.MouseUp += (s, e) =>
            {
                isDraggingSeek = false;
                if (audioFile != null)
                    audioFile.CurrentTime = TimeSpan.FromSeconds(
                        seekBar.Value / 1000.0 * audioFile.TotalTime.TotalSeconds);
            };

            progressPanel.Controls.Add(seekBar);
            progressPanel.Controls.Add(lblDuration);
            progressPanel.Controls.Add(lblTime);

            // ── Control bar ─────────────────────────────────────────────────
            controlBar = new Panel
            {
                Height = 54,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(17, 23, 33)
            };
            controlBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)),
                    0, 0, controlBar.Width, 0);

            int cx = 12;
            btnPrev = AddCtrlBtn("⏮", ref cx);
            btnPrev.Click += async (s, e) => await ChangeTrack(-1);

            btnPlay = AddCtrlBtn("▶", ref cx);
            btnPlay.Width = 50;
            btnPlay.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            btnPlay.BackColor = Color.FromArgb(22, 100, 40);
            btnPlay.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnPlay.Click += async (s, e) => await TogglePlay();
            cx += 14; // extra space after wider button

            btnNext = AddCtrlBtn("⏭", ref cx);
            btnNext.Click += async (s, e) => await ChangeTrack(1);

            btnStop = AddCtrlBtn("⏹", ref cx);
            btnStop.Click += (s, e) => StopPlayback();

            // Separator
            cx += 8;
            controlBar.Controls.Add(new Panel
            {
                Location = new Point(cx, 10),
                Size = new Size(1, 34),
                BackColor = Color.FromArgb(38, 50, 70)
            });
            cx += 10;

            btnShuffle = AddCtrlBtn("🔀", ref cx);
            btnShuffle.Click += (s, e) => ToggleShuffle();

            btnRepeat = AddCtrlBtn("🔁", ref cx);
            btnRepeat.Click += (s, e) => ToggleRepeat();

            // Volume
            cx += 12;
            controlBar.Controls.Add(new Panel
            {
                Location = new Point(cx, 10),
                Size = new Size(1, 34),
                BackColor = Color.FromArgb(38, 50, 70)
            });
            cx += 10;

            lblVolume = new Label
            {
                Text = "🔊",
                Location = new Point(cx, 14),
                Size = new Size(30, 26),
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 11F)
            };
            controlBar.Controls.Add(lblVolume);
            cx += 30;

            volBar = new TrackBar
            {
                Location = new Point(cx, 14),
                Size = new Size(120, 26),
                Minimum = 0,
                Maximum = 100,
                Value = 70,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(17, 23, 33)
            };
            volBar.ValueChanged += (s, e) =>
            {
                if (outputDevice != null)
                    outputDevice.Volume = volBar.Value / 100f;
            };
            controlBar.Controls.Add(volBar);

            // ── Assembly ────────────────────────────────────────────────────
            Controls.Add(grid);
            Controls.Add(rightPanel);
            Controls.Add(progressPanel);
            Controls.Add(controlBar);
            Controls.Add(topBar);
        }

        private Button AddCtrlBtn(string text, ref int x)
        {
            var btn = MakeBtn(text, 38,
                Color.FromArgb(24, 32, 46), Color.FromArgb(38, 50, 70));
            btn.Location = new Point(x, 10);
            btn.Size = new Size(38, 34);
            btn.Font = new Font("Segoe UI", 13F);
            controlBar.Controls.Add(btn);
            x += 42;
            return btn;
        }

        private Button MakeBtn(string text, int width, Color bg, Color border)
        {
            var b = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = border;
            return b;
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
                .OrderBy(f => f)
                .ToList();

            int targetIndex = 0;
            for (int i = 0; i < audioFiles.Count; i++)
            {
                AddFileToGrid(audioFiles[i]);
                if (audioFiles[i].Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    targetIndex = i;
            }

            if (grid.Rows.Count > 0)
            {
                currentIndex = targetIndex;
                _ = PlayTrack(currentIndex);
            }
        }

        private void AddFileToGrid(string path)
        {
            try
            {
                var tag = TagFile.Create(path);
                grid.Rows.Add(
                    CleanTitle(tag.Tag.Title ?? Path.GetFileNameWithoutExtension(path)),
                    CleanArtist(tag.Tag.FirstPerformer ?? "Desconocido"),
                    tag.Tag.Album ?? "Desconocido",
                    tag.Properties.Duration.ToString(@"mm\:ss"),
                    path);
            }
            catch
            {
                grid.Rows.Add(
                    Path.GetFileNameWithoutExtension(path),
                    "Desconocido", "", "—", path);
            }
        }

        private void AddFilesDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Agregar archivos de audio",
                Filter = "Audio|*.mp3;*.wav;*.wma;*.m4a;*.flac;*.aac;*.ogg;*.opus;*.aiff|Todos|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            foreach (string f in dlg.FileNames)
                if (SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    AddFileToGrid(f);
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

            // Highlight row
            grid.ClearSelection();
            grid.Rows[index].Selected = true;
            if (grid.Rows[index].Cells[0].Visible)
                grid.CurrentCell = grid.Rows[index].Cells[0];

            string title = grid.Rows[index].Cells["Title"].Value?.ToString() ?? "";
            string artist = grid.Rows[index].Cells["Artist"].Value?.ToString() ?? "";
            lblNowPlaying.Text = $"♪  {artist} — {title}";
            Text = $"Reproductor — {artist} — {title}";

            // Cover
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reproducir:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                if (grid.Rows.Count == 0) return;
                int idx = grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : 0;
                await PlayTrack(idx);
                return;
            }
            if (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                SetPlayState(false);
            }
            else
            {
                outputDevice.Play();
                SetPlayState(true);
            }
        }

        private async Task ChangeTrack(int direction)
        {
            if (grid.Rows.Count == 0) return;

            if (repeatMode == 2) { await PlayTrack(currentIndex); return; }

            if (shuffleMode)
            {
                if (shuffleOrder == null || shuffleOrder.Length != grid.Rows.Count)
                    GenerateShuffleOrder();

                if (shufflePos >= shuffleOrder!.Length)
                {
                    if (repeatMode == 0) { StopPlayback(); return; }
                    GenerateShuffleOrder();
                }
                currentIndex = shuffleOrder![shufflePos++];
            }
            else
            {
                int next = currentIndex + direction;
                if (next >= grid.Rows.Count)
                {
                    if (repeatMode == 0) { StopPlayback(); return; }
                    currentIndex = 0;
                }
                else if (next < 0)
                    currentIndex = grid.Rows.Count - 1;
                else
                    currentIndex = next;
            }

            await PlayTrack(currentIndex);
        }

        private void SetPlayState(bool playing)
        {
            if (playing)
            {
                btnPlay.Text = "⏸";
                btnPlay.BackColor = Color.FromArgb(140, 100, 10);
                btnPlay.FlatAppearance.BorderColor = Color.FromArgb(180, 130, 20);
            }
            else
            {
                btnPlay.Text = "▶";
                btnPlay.BackColor = Color.FromArgb(22, 100, 40);
                btnPlay.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TIMER
        // ════════════════════════════════════════════════════════════════════
        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (audioFile == null || outputDevice == null) return;

            if (!isDraggingSeek)
            {
                double pos = audioFile.CurrentTime.TotalSeconds;
                double dur = audioFile.TotalTime.TotalSeconds;
                if (dur > 0)
                    seekBar.Value = Math.Min(seekBar.Maximum, (int)(pos / dur * 1000));
                lblTime.Text = FormatTime(pos);
            }

            // Auto-next
            if (audioFile.TotalTime.TotalSeconds - audioFile.CurrentTime.TotalSeconds <= 0.8
                && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                BeginInvoke(async () => await ChangeTrack(1));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MODOS
        // ════════════════════════════════════════════════════════════════════
        private void ToggleShuffle()
        {
            shuffleMode = !shuffleMode;
            btnShuffle.BackColor = shuffleMode
                ? Color.FromArgb(20, 70, 120) : Color.FromArgb(24, 32, 46);
            btnShuffle.ForeColor = shuffleMode
                ? Color.FromArgb(56, 139, 253) : Color.White;
            if (shuffleMode) GenerateShuffleOrder();
        }

        private void ToggleRepeat()
        {
            repeatMode = (repeatMode + 1) % 3;
            btnRepeat.Text = repeatMode switch
            {
                0 => "🔁",
                1 => "🔁",
                2 => "🔂",
                _ => "🔁"
            };
            btnRepeat.BackColor = repeatMode > 0
                ? Color.FromArgb(20, 70, 120) : Color.FromArgb(24, 32, 46);
            btnRepeat.ForeColor = repeatMode > 0
                ? Color.FromArgb(56, 139, 253) : Color.White;
        }

        private void GenerateShuffleOrder()
        {
            var rng = new Random();
            shuffleOrder = Enumerable.Range(0, grid.Rows.Count).OrderBy(_ => rng.Next()).ToArray();
            shufflePos = 0;
        }

        // ════════════════════════════════════════════════════════════════════
        //  COVER
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
                    using var ms = new MemoryStream(tag.Tag.Pictures[0].Data.Data);
                    coverBox.Image = Image.FromStream(ms);
                    return;
                }
            }
            catch { }

            // Intentar buscar cover online via iTunes
            try
            {
                string artist = grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? "";
                string title = grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)) return;

                string query = Uri.EscapeDataString($"{artist} {title}");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                client.DefaultRequestHeaders.Add("User-Agent", "FileExplorerr/1.0");

                string json = await client.GetStringAsync(
                    $"https://itunes.apple.com/search?term={query}&limit=3&entity=song");

                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");
                if (results.GetArrayLength() > 0)
                {
                    string coverUrl = results[0].GetProperty("artworkUrl100").GetString()!
                        .Replace("100x100", "600x600");
                    byte[] imgData = await client.GetByteArrayAsync(coverUrl);
                    using var ms = new MemoryStream(imgData);
                    coverBox.Image = Image.FromStream(ms);

                    // Guardar en el MP3
                    await Task.Run(() =>
                    {
                        try
                        {
                            var mp3 = TagFile.Create(path);
                            mp3.Tag.Pictures = new TagLib.IPicture[]
                            {
                                new TagLib.Picture(imgData)
                                {
                                    Type = TagLib.PictureType.FrontCover,
                                    MimeType = "image/png",
                                    Description = "Cover"
                                }
                            };
                            mp3.Save();
                        }
                        catch { }
                    });
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

            string artist = NormalizeForSearch(
                grid.Rows[currentIndex].Cells["Artist"].Value?.ToString() ?? "");
            string title = NormalizeForSearch(
                grid.Rows[currentIndex].Cells["Title"].Value?.ToString() ?? "");

            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            { lyricsBox.Text = "Sin datos de artista/título."; return; }

            lyricsBox.Text = "Buscando letra...";
            btnLyrics.Enabled = false;

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add("User-Agent", "FileExplorerr/1.0");

                var response = await client.GetAsync(
                    $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}");

                if (!response.IsSuccessStatusCode)
                { lyricsBox.Text = "No se encontró la letra."; return; }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("plainLyrics", out var lyr)
                    && !string.IsNullOrWhiteSpace(lyr.GetString()))
                    lyricsBox.Text = lyr.GetString()!;
                else
                    lyricsBox.Text = "No se encontró la letra.";
            }
            catch (Exception ex)
            {
                lyricsBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btnLyrics.Enabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EVENTOS
        // ════════════════════════════════════════════════════════════════════
        private async void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) await PlayTrack(e.RowIndex);
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                    await TogglePlay();
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Left:
                    if (audioFile != null)
                        audioFile.CurrentTime = TimeSpan.FromSeconds(
                            Math.Max(0, audioFile.CurrentTime.TotalSeconds - 5));
                    break;
                case Keys.Right:
                    if (audioFile != null)
                        audioFile.CurrentTime = TimeSpan.FromSeconds(
                            Math.Min(audioFile.TotalTime.TotalSeconds,
                                audioFile.CurrentTime.TotalSeconds + 5));
                    break;
                case Keys.Up:
                    volBar.Value = Math.Min(100, volBar.Value + 5);
                    break;
                case Keys.Down:
                    volBar.Value = Math.Max(0, volBar.Value - 5);
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static string FormatTime(double secs)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, secs));
            return t.Hours > 0
                ? $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }

        private static string CleanArtist(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Desconocido";
            return System.Text.RegularExpressions.Regex.Replace(s,
                @"\s*-?\s*(Topic|VEVO|Official)$", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        private static string CleanTitle(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Sin título";
            s = System.Text.RegularExpressions.Regex.Replace(s,
                @"\s*\([^)]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K|Visualizer|Explicit|Clean|Remaster)[^)]*?\)", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s,
                @"\s*\[[^\]]*?(Official|Audio|Video|Lyrics|Music Video|HD|4K)[^\]]*?\]", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return s.Trim();
        }

        private static string NormalizeForSearch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.ToLower().Trim();
            // Remove feat/ft
            foreach (var sep in new[] { " feat.", " feat ", " ft.", " ft ", " featuring " })
            {
                int i = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (i > 0) { s = s.Substring(0, i); break; }
            }
            return s.Trim();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer?.Stop();
                uiTimer?.Dispose();
                StopPlayback();
            }
            base.Dispose(disposing);
        }
    }
}