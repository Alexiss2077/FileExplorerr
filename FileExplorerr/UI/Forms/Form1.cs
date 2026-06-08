using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;

namespace FileExplorerr
{
    public partial class Form1 : Form
    {
        // ── Estado del usuario ───────────────────────────────────────────────
        private UserProfile? _currentUser;
        private AccountButton _accountButton = null!;

        // ── Estado del explorador ────────────────────────────────────────────
        private string currentPath = "";
        private readonly Stack<string> navigationHistory = new();
        private readonly Stack<string> navigationForward = new();
        private ListViewItem? dragHighlightedItem;
        private int sortColumn = -1;

        // Autocompletado personalizado (Modo Oscuro)
        private ListBox suggestionBox = null!;

        // Íconos de la papelera (vacía / llena) — se cargan desde shell32
        private Bitmap? _recycleIconEmpty;
        private Bitmap? _recycleIconFull;

        private CancellationTokenSource _loadCts = new();
        private CancellationTokenSource _searchCts = new();

        // ── Página activa ────────────────────────────────────────────────────
        private string _activePage = "explorer";

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("dwmapi.dll")]

        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private static int ToBgr(Color c) => c.B << 16 | c.G << 8 | c.R;

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string? pszSubIdList);

        private static void ApplyDarkScrollBar(Control control)
        {
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int bg = ToBgr(Theme.BgSurface);
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref bg, sizeof(int));
                int tx = ToBgr(Theme.Accent2);
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref tx, sizeof(int));
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════
        public Form1() : this(null) { }

        public Form1(UserProfile? user)
        {
            _currentUser = user;
            InitializeComponent();
            BuildUI();
            HandleCreated += (s, e) => ApplyDarkTitleBar();

            // Actualizar el botón de cuenta
            if (_accountButton != null)
                _accountButton.SetProfile(_currentUser);

            NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            BackColor = Theme.BgBase;

            // ── ImageList ────────────────────────────────────────────────────
            imageList = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            imageList.Images.Add("folder", FileIconFactory.MakeFolderIcon());
            imageList.Images.Add("file", FileIconFactory.MakeFileIcon());
            imageList.Images.Add("image", FileIconFactory.MakeImageIcon());
            imageList.Images.Add("audio", FileIconFactory.MakeAudioIcon());
            imageList.Images.Add("video", FileIconFactory.MakeVideoIcon());
            imageList.Images.Add("text", FileIconFactory.MakeTextIcon());
            imageList.Images.Add("archive", FileIconFactory.MakeArchiveIcon());

            BuildTopNav();
            BuildExplorerPage();

            // pageContainer: apila todas las páginas
            pageContainer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgBase };

            explorerPage.Dock = DockStyle.Fill;
            pageContainer.Controls.Add(explorerPage);

            Controls.Add(pageContainer);
            Controls.Add(topNavBar);

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };
        }

        // ════════════════════════════════════════════════════════════════════
        //  BARRA DE NAVEGACIÓN SUPERIOR
        // ════════════════════════════════════════════════════════════════════
        private void BuildTopNav()
        {
            topNavBar = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(12, 8, 12, 8)
            };

            // Logo
            appLogoLabel = new Label
            {
                Text = "  FileExplorerr",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Accent2,
                AutoSize = true,
                Location = new Point(12, 10),
                BackColor = Color.Transparent
            };

            // Separador
            var sep = new Panel { Left = 190, Top = 10, Width = 1, Height = 34, BackColor = Theme.Border };

            // Pestañas
            navExplorer = MakeNavTabButton("📁", "Explorador", true);
            navMusic = MakeNavTabButton("🎵", "Música", false);
            navVideo = MakeNavTabButton("🎬", "Video", false);
            navSql = MakeNavTabButton("🗄️", "SQL", false);

            navExplorer.Click += (s, e) => SwitchPage("explorer");
            navMusic.Click += (s, e) =>
            {
                SwitchPage("music");
                string? file = GetFirstAudioFile();
                new MusicPlayerForm(file ?? "").Show();
            };
            navVideo.Click += (s, e) =>
            {
                SwitchPage("video");
                string? file = GetFirstVideoFile();
                new VideoPlayerForm(file).Show();
            };
            navSql.Click += (s, e) => { SwitchPage("sql"); new SqlViewerForm().Show(); };

            // Flow para las pestañas
            var tabFlow = new FlowLayoutPanel
            {
                Left = 200,
                Top = 7,
                Height = 40,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            tabFlow.Controls.AddRange(new Control[] { navExplorer, navMusic, navVideo, navSql });

            // Botón de Cuenta
            _accountButton = new AccountButton
            {
                Size = new Size(190, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _accountButton.SignOutRequested += OnSignOutRequested;
            _accountButton.SwitchAccountRequested += OnSwitchAccountRequested;

            if (_currentUser != null)
                _accountButton.SetProfile(_currentUser);

            topNavBar.Controls.Add(appLogoLabel);
            topNavBar.Controls.Add(sep);
            topNavBar.Controls.Add(tabFlow);
            topNavBar.Controls.Add(_accountButton);

            topNavBar.Resize += (s, e) =>
            {
                _accountButton.Location = new Point(topNavBar.Width - 200, 10);
            };
        }

        private Button MakeNavTabButton(string icon, string label, bool active)
        {
            var btn = new Button
            {
                Text = $"{icon}  {label}",
                Height = 36,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = active ? Theme.AccentBg : Color.Transparent,
                ForeColor = active ? Theme.Accent2 : Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontNavTab,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = active ? 1 : 0;
            btn.FlatAppearance.BorderColor = active
                ? Color.FromArgb(124, 111, 247, 80)
                : Theme.BgSurface;
            btn.FlatAppearance.MouseOverBackColor = Theme.BgHover;
            return btn;
        }

        private void SwitchPage(string page)
        {
            _activePage = page;
            foreach (var (btn, pg) in new[] {
                (navExplorer,"explorer"), (navMusic,"music"),
                (navVideo,"video"),       (navSql,"sql") })
            {
                bool on = pg == page;
                btn.BackColor = on ? Theme.AccentBg : Theme.BgSurface;
                btn.ForeColor = on ? Theme.Accent2 : Theme.TextMuted;
                btn.FlatAppearance.BorderSize = on ? 1 : 0;
                btn.FlatAppearance.BorderColor = on
                    ? Color.FromArgb(124, 111, 247, 80)
                    : Theme.BgSurface;
            }
        }

        private string? GetFirstAudioFile()
        {
            try { return Directory.GetFiles(currentPath).FirstOrDefault(x => FileExtensions.Audio.Contains(Path.GetExtension(x))); }
            catch { return null; }
        }

        private string? GetFirstVideoFile()
        {
            try { return Directory.GetFiles(currentPath).FirstOrDefault(x => FileExtensions.Video.Contains(Path.GetExtension(x))); }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MANEJO DE EVENTOS DE CUENTA
        // ════════════════════════════════════════════════════════════════════
        private void OnSignOutRequested(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("¿Cerrar sesión de FileExplorerr?\n\nDeberás iniciar sesión la próxima vez.", "Cerrar sesión", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            SessionManager.Clear();
            var loginForm = new LoginForm();
            Hide();

            if (loginForm.ShowDialog() == DialogResult.OK && loginForm.LoggedInUser != null)
            {
                _currentUser = loginForm.LoggedInUser;
                _accountButton.SetProfile(_currentUser);
                Show();
            }
            else { Application.Exit(); }
        }

        private void OnSwitchAccountRequested(object? sender, EventArgs e)
        {
            SessionManager.Clear();
            OnSignOutRequested(sender, e);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PÁGINA EXPLORER
        // ════════════════════════════════════════════════════════════════════
        private void BuildExplorerPage()
        {
            explorerPage = new Panel { BackColor = Theme.BgBase };

            // ── Toolbar del explorador ────────────────────────────────────────
            var toolbar = new Panel { Height = 54, Dock = DockStyle.Top, BackColor = Theme.BgSurface };

            backButton = MakeNavBtn("←", "Atrás");
            forwardButton = MakeNavBtn("→", "Adelante");
            upButton = MakeNavBtn("↑", "Subir nivel");
            refreshButton = MakeNavBtn("↻", "Actualizar (F5)");

            backButton.Click += (s, e) => GoBack();
            forwardButton.Click += (s, e) => GoForward();
            upButton.Click += (s, e) => GoUp();
            refreshButton.Click += (s, e) => RefreshView();

            var addrPanel = new Panel { Height = 34, BackColor = Theme.BgElevated, Location = new Point(-500, 0) };
            var addrIcon = new Label { Text = "📂", Width = 28, Height = 34, Location = new Point(6, 0), BackColor = Color.Transparent, Font = new Font("Segoe UI", 13F), TextAlign = ContentAlignment.MiddleCenter };

            addressBar = new TextBox
            {
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                AutoCompleteMode = AutoCompleteMode.None // Autocompletado nativo desactivado
            };

            addressBar.KeyDown += AddressBar_KeyDown;
            addressBar.TextChanged += AddressBar_TextChanged; // Suscripción para mostrar la lista

            addrPanel.Controls.Add(addrIcon);
            addrPanel.Controls.Add(addressBar);
            addrPanel.Resize += (s, e) =>
            {
                addressBar.Width = addrPanel.Width - 38;
                addressBar.Location = new Point(36, (addrPanel.Height - addressBar.Height) / 2);
            };

            newFolderButton = Theme.MakeButton("＋ Carpeta", 100);
            exportCsvButton = Theme.MakeButton("↓ Exportar CSV", 126, Theme.ButtonKind.Primary);
            newFolderButton.Height = 34;
            exportCsvButton.Height = 34;

            newFolderButton.Click += (s, e) => FileOperationService.CreateFolder(currentPath, this, () => LoadDirectory(currentPath));
            exportCsvButton.Click += async (s, e) => await ExportCsvAsync();

            toolbar.Controls.AddRange(new Control[] { backButton, forwardButton, upButton, refreshButton, addrPanel, newFolderButton, exportCsvButton });

            void LayoutToolbar()
            {
                if (toolbar.Width < 200) return;
                int h = toolbar.Height, cy = (h - 34) / 2, x = 10;
                backButton.SetBounds(x, cy, 34, 34); x += 38;
                forwardButton.SetBounds(x, cy, 34, 34); x += 38;
                upButton.SetBounds(x, cy, 34, 34); x += 38;
                refreshButton.SetBounds(x, cy, 34, 34); x += 44;

                int rx = toolbar.Width - 10;
                exportCsvButton.SetBounds(rx - exportCsvButton.Width, cy, exportCsvButton.Width, 34);
                rx -= exportCsvButton.Width + 6;
                newFolderButton.SetBounds(rx - newFolderButton.Width, cy, newFolderButton.Width, 34);
                rx -= newFolderButton.Width + 8;

                if (rx > x) addrPanel.SetBounds(x, cy, rx - x, 34);
            }

            toolbar.Resize += (s, e) => LayoutToolbar();
            this.Resize += (s, e) => LayoutToolbar();
            this.Shown += (s, e) => BeginInvoke((Action)LayoutToolbar);

            // ── Creación del Menú Flotante Personalizado ──────────────────────
            suggestionBox = new ListBox
            {
                Visible = false,
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                BorderStyle = BorderStyle.FixedSingle,
                ItemHeight = 26,
                IntegralHeight = false
            };
            suggestionBox.Click += SuggestionBox_Click;
            suggestionBox.KeyDown += SuggestionBox_KeyDown;
            this.Controls.Add(suggestionBox); // Agregar a la ventana para que flote sobre todo

            var bodyPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgBase };
            BuildExplorerSidebar();
            BuildListView();
            BuildRightPanel();
            BuildStatusBar();
            BuildContextMenu();

            listView.ContextMenuStrip = contextMenu;

            bodyPanel.Controls.Add(listView);
            bodyPanel.Controls.Add(rightInfoPanel);
            bodyPanel.Controls.Add(explorerSidebar);

            explorerPage.Controls.Add(bodyPanel);
            explorerPage.Controls.Add(statusBar);
            explorerPage.Controls.Add(toolbar);
        }

        // ════════════════════════════════════════════════════════════════════
        //  LÓGICA DEL AUTOCOMPLETADO PERSONALIZADO
        // ════════════════════════════════════════════════════════════════════
        private void AddressBar_TextChanged(object? sender, EventArgs e)
        {
            string input = addressBar.Text;

            // Ocultar si está vacío o no parece una ruta
            if (string.IsNullOrWhiteSpace(input) || !input.Contains("\\"))
            {
                suggestionBox.Visible = false;
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(input) ?? "";
                string partial = Path.GetFileName(input) ?? "";

                if (input.EndsWith("\\"))
                {
                    dir = input;
                    partial = "";
                }

                if (Directory.Exists(dir))
                {
                    var matches = Directory.GetDirectories(dir)
                        .Where(d => Path.GetFileName(d).StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (matches.Length > 0)
                    {
                        suggestionBox.Items.Clear();
                        suggestionBox.Items.AddRange(matches);

                        // Posicionar debajo de addressBar de forma absoluta en el formulario
                        Point pt = addressBar.PointToScreen(new Point(0, addressBar.Height));
                        suggestionBox.Location = this.PointToClient(pt);

                        suggestionBox.Width = addressBar.Width + 34; // +34 para cubrir el ícono también
                        suggestionBox.Height = Math.Min(matches.Length * suggestionBox.ItemHeight + 4, 200);

                        suggestionBox.BringToFront();
                        suggestionBox.Visible = true;
                    }
                    else
                    {
                        suggestionBox.Visible = false;
                    }
                }
                else
                {
                    suggestionBox.Visible = false;
                }
            }
            catch
            {
                suggestionBox.Visible = false;
            }
        }

        private void SuggestionBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptSuggestion();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                suggestionBox.Visible = false;
                addressBar.Focus();
                e.Handled = true;
            }
        }

        private void SuggestionBox_Click(object? sender, EventArgs e)
        {
            if (suggestionBox.SelectedItem != null)
            {
                AcceptSuggestion();
            }
        }

        private void AcceptSuggestion()
        {
            if (suggestionBox.SelectedItem != null)
            {
                addressBar.TextChanged -= AddressBar_TextChanged;
                addressBar.Text = suggestionBox.SelectedItem.ToString();
                addressBar.SelectionStart = addressBar.Text.Length;
                addressBar.TextChanged += AddressBar_TextChanged;

                suggestionBox.Visible = false;
                addressBar.Focus();
            }
        }

        private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
        {
            // Bajar a la lista
            if (suggestionBox.Visible && e.KeyCode == Keys.Down)
            {
                suggestionBox.Focus();
                if (suggestionBox.Items.Count > 0)
                    suggestionBox.SelectedIndex = 0;
                e.Handled = true;
                return;
            }

            // Ocultar lista
            if (suggestionBox.Visible && e.KeyCode == Keys.Escape)
            {
                suggestionBox.Visible = false;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                suggestionBox.Visible = false;
                NavigateToPath(addressBar.Text);
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        // ── Botón de navegación pequeño ──────────────────────────────────────
        private static Button MakeNavBtn(string text, string tip)
        {
            var btn = new Button
            {
                Text = text,
                Width = 34,
                Height = 34,
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13F),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderColor = Theme.Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Theme.BgHover;
            new ToolTip().SetToolTip(btn, tip);
            return btn;
        }

        // ── Sidebar izquierdo del explorador ─────────────────────────────────
        private void BuildExplorerSidebar()
        {
            explorerSidebar = new Panel
            {
                Width = 220,
                Dock = DockStyle.Left,
                BackColor = Theme.BgSurface,
                Padding = new Padding(0, 8, 0, 0)
            };

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.HandleCreated += (s, e) => ApplyDarkScrollBar(scroll);

            void AddHeader(string text)
            {
                var lbl = new Label
                {
                    Text = text,
                    AutoSize = false,
                    Height = 26,
                    Dock = DockStyle.Top,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Theme.TextMuted,
                    BackColor = Color.Transparent,
                    Padding = new Padding(16, 6, 0, 0),
                    TextAlign = ContentAlignment.BottomLeft
                };
                scroll.Controls.Add(lbl);
            }

            void AddItem(string icon, string label, Action? click = null, string? badge = null)
            {
                var p = new Panel
                {
                    Height = 36,
                    Dock = DockStyle.Top,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Padding = new Padding(12, 0, 8, 0)
                };
                var ico = new Label
                {
                    Text = icon,
                    Width = 24,
                    Height = 36,
                    Left = 12,
                    Font = new Font("Segoe UI", 14F),
                    ForeColor = Theme.TextSecondary,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                var lbl = new Label
                {
                    Text = label,
                    Left = 42,
                    Height = 36,
                    Width = badge != null ? 110 : 150,
                    Font = Theme.FontBody,
                    ForeColor = Theme.TextSecondary,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                p.Controls.Add(ico);
                p.Controls.Add(lbl);

                if (badge != null)
                {
                    var bdg = new Label
                    {
                        Text = badge,
                        AutoSize = true,
                        Left = 150,
                        Top = 9,
                        Font = new Font("Segoe UI", 8F),
                        ForeColor = Theme.TextMuted,
                        BackColor = Theme.BgElevated,
                        Padding = new Padding(4, 1, 4, 1)
                    };
                    p.Controls.Add(bdg);
                    p.Resize += (s, e) => bdg.Left = p.Width - bdg.Width - 8;
                    p.HandleCreated += (s, e) => bdg.Left = p.Width - bdg.Width - 8;
                }

                void Hover(bool on)
                {
                    p.BackColor = on ? Theme.BgHover : Color.Transparent;
                    lbl.ForeColor = on ? Theme.TextPrimary : Theme.TextSecondary;
                }
                p.MouseEnter += (s, e) => Hover(true);
                p.MouseLeave += (s, e) => Hover(false);
                lbl.MouseEnter += (s, e) => Hover(true);
                lbl.MouseLeave += (s, e) => Hover(false);

                if (click != null)
                {
                    p.Click += (s, e) => click();
                    lbl.Click += (s, e) => click();
                    ico.Click += (s, e) => click();
                }

                scroll.Controls.Add(p);
            }

            AddHeader("ACCESOS RÁPIDOS");
            AddItem("🏠", "Inicio", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            AddItem("🖥️", "Escritorio", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
            AddItem("📄", "Documentos", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
            AddItem("🖼️", "Imágenes", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));
            AddItem("🎵", "Música", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)));
            AddItem("🎬", "Videos", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)));
            AddItem("⬇️", "Descargas", () => NavigateToPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));

            AddHeader("HERRAMIENTAS");
            AddItem("🗄️", "SQL / Base de datos", () => new SqlViewerForm().Show());
            AddItem("📊", "Exportar CSV", () => _ = ExportCsvAsync());

            AddHeader("DISPOSITIVOS");
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                AddItem("💽", $"{drive.Name}  {drive.VolumeLabel}".TrimEnd(), () => NavigateToPath(drive.RootDirectory.FullName));

            scroll.Controls.SetChildIndex(scroll.Controls[0], scroll.Controls.Count - 1);
            explorerSidebar.Controls.Add(scroll);
        }

        // ── ListView central ──────────────────────────────────────────────────
        private void BuildListView()
        {
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
                SmallImageList = imageList,
                LargeImageList = imageList,
                AllowDrop = true,
                OwnerDraw = true
            };
            listView.HandleCreated += (s, e) => ApplyDarkScrollBar(listView);

            listView.Columns.Add("Nombre", 280);
            listView.Columns.Add("Tipo", 100);
            listView.Columns.Add("Tamaño", 90);
            listView.Columns.Add("Info", 200);
            listView.Columns.Add("Modificado", 140);

            // Columna fantasma que absorbe el espacio sobrante — sin texto, sin datos
            var colFill = listView.Columns.Add("", 0);

            listView.Resize += (s, e) =>
            {
                int used = 0;
                for (int i = 0; i < listView.Columns.Count - 1; i++)
                    used += listView.Columns[i].Width;
                int remaining = listView.ClientSize.Width - used;
                listView.Columns[listView.Columns.Count - 1].Width = Math.Max(0, remaining);
            };

            listView.DrawColumnHeader += ListView_DrawColumnHeader;
            listView.DrawItem += (s, e) => e.DrawDefault = true;
            listView.DrawSubItem += (s, e) => e.DrawDefault = true;
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    FileOpener.Open(listView.SelectedItems[0].Tag!.ToString()!, this, NavigateToPath);
            };
            listView.ColumnClick += ListView_ColumnClick;
            listView.ItemDrag += ListView_ItemDrag;
            listView.DragEnter += ListView_DragEnter;
            listView.DragOver += ListView_DragOver;
            listView.DragDrop += ListView_DragDrop;
            listView.DragLeave += (s, e) => ClearDragHighlight();
            listView.MouseClick += ListView_MouseClick;
            listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };
        }

        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var bg = new SolidBrush(Theme.BgSurface);
            e.Graphics.FillRectangle(bg, e.Bounds);
            using var line = new Pen(Color.FromArgb(255, 255, 255, 12));
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var rect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
            using var br = new SolidBrush(Theme.TextSecondary);              // más claro
            using var font = new Font("Segoe UI", 10F, FontStyle.Bold);     // más grande
            using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(e.Header!.Text, font, br, rect, sf);
        }

        // ── Panel derecho (árbol de carpeta) ──────────────────────────────────
        private void BuildRightPanel()
        {
            rightInfoPanel = new Panel
            {
                Width = 260,
                Dock = DockStyle.Right,
                BackColor = Theme.BgSurface
            };

            var hdr = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Theme.BgElevated };
            folderNameLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Carpeta",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent2,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                BackColor = Color.Transparent
            };
            hdr.Controls.Add(folderNameLabel);

            var srchPanel = new Panel { Height = 46, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(8, 7, 8, 7) };
            searchBox = new TextBox
            {
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBody,
                PlaceholderText = "🔍  Buscar en carpeta...",
                Height = 32,
                Dock = DockStyle.Fill
            };
            var goBtn = new Button
            {
                Text = "Ir",
                Width = 36,
                Height = 32,
                Dock = DockStyle.Right,
                BackColor = Theme.AccentBg,
                ForeColor = Theme.Accent2,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmallBold,
                Cursor = Cursors.Hand
            };
            goBtn.FlatAppearance.BorderColor = Color.FromArgb(124, 111, 247, 80);
            goBtn.FlatAppearance.BorderSize = 1;
            goBtn.Click += (s, e) => _ = SearchInPanelAsync(searchBox.Text);
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = SearchInPanelAsync(searchBox.Text); };
            srchPanel.Controls.Add(searchBox);
            srchPanel.Controls.Add(goBtn);

            infoTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                BorderStyle = BorderStyle.None,
                ShowLines = false,
                ShowPlusMinus = true,
                ShowRootLines = false,
                FullRowSelect = true,
                Scrollable = true,
                Indent = 18,
                ItemHeight = 26,
                DrawMode = TreeViewDrawMode.OwnerDrawAll
            };
            infoTree.HandleCreated += (s, e) => ApplyDarkScrollBar(infoTree);

            infoTree.BeforeExpand += InfoTree_BeforeExpand;
            infoTree.NodeMouseClick += InfoTree_NodeMouseClick;
            infoTree.NodeMouseDoubleClick += InfoTree_NodeDoubleClick;
            infoTree.DrawNode += InfoTree_DrawNode;

            rightInfoPanel.Controls.Add(infoTree);
            rightInfoPanel.Controls.Add(srchPanel);
            rightInfoPanel.Controls.Add(hdr);
        }

        // ── Barra de estado ───────────────────────────────────────────────────
        private void BuildStatusBar()
        {
            statusBar = new Panel
            {
                Height = 36,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface,
                Padding = new Padding(14, 0, 14, 0)
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall,
                BackColor = Color.Transparent
            };

            recycleIconBox = new PictureBox
            {
                Size = new Size(36, 36),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Dock = DockStyle.Right,
                AllowDrop = true
            };

            try
            {
                string shell32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
                IntPtr hEmpty = ExtractIcon(IntPtr.Zero, shell32, 31);
                IntPtr hFull = ExtractIcon(IntPtr.Zero, shell32, 32);

                if (hEmpty != IntPtr.Zero) _recycleIconEmpty = Icon.FromHandle(hEmpty).ToBitmap();
                if (hFull != IntPtr.Zero) _recycleIconFull = Icon.FromHandle(hFull).ToBitmap();
                recycleIconBox.Image = _recycleIconEmpty;
            }
            catch { }

            recyclePanelLabel = new Label
            {
                Text = "Papelera",
                Width = 64,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall,
                BackColor = Color.Transparent,
                AllowDrop = true
            };

            recycleDropPanel = new Panel
            {
                Width = 110,
                Dock = DockStyle.Right,
                BackColor = Theme.RecycleBg,
                AllowDrop = true
            };
            recycleDropPanel.Controls.Add(recyclePanelLabel);
            recycleDropPanel.Controls.Add(recycleIconBox);

            foreach (var ctl in new Control[] { recycleDropPanel, recycleIconBox, recyclePanelLabel })
            {
                ctl.DragEnter += (s, e) => RecycleDragEnter(e);
                ctl.DragOver += (s, e) => RecycleDragOver(e);
                ctl.DragLeave += (s, e) => RecycleDragLeave();
                ctl.DragDrop += (s, e) => RecycleDragDrop(e);
            }

            statusBar.Controls.Add(statusLabel);
            statusBar.Controls.Add(recycleDropPanel);
        }

        // ════════════════════════════════════════════════════════════════════
        //  MENÚ CONTEXTUAL
        // ════════════════════════════════════════════════════════════════════
        private void BuildContextMenu()
        {
            contextMenu = new ContextMenuStrip { Font = Theme.FontBody };
            contextMenu.BackColor = Theme.BgElevated;
            contextMenu.ForeColor = Theme.TextPrimary;
            contextMenu.Renderer = new MinimalMenuRenderer();

            var miOpen = new ToolStripMenuItem("Abrir") { ForeColor = Theme.TextPrimary };
            var sep1 = new ToolStripSeparator();
            var miNewFolder = new ToolStripMenuItem("Nueva carpeta") { ForeColor = Theme.TextPrimary };
            var sep2 = new ToolStripSeparator();
            var miRename = new ToolStripMenuItem("Renombrar") { ForeColor = Theme.TextPrimary };
            var miDelete = new ToolStripMenuItem("Eliminar") { ForeColor = Theme.Coral };
            var sep3 = new ToolStripSeparator();
            var miProps = new ToolStripMenuItem("Propiedades") { ForeColor = Color.FromArgb(167, 139, 250) };
            var sep4 = new ToolStripSeparator();
            var miRefresh = new ToolStripMenuItem("Actualizar  (F5)") { ForeColor = Theme.Accent2 };
            sep5 = new ToolStripSeparator();
            miCompress = new ToolStripMenuItem("📦 Comprimir selección...") { ForeColor = Theme.Accent2 };
            miExtractHere = new ToolStripMenuItem("📂 Extraer aquí") { ForeColor = Theme.Teal };
            miExtractTo = new ToolStripMenuItem("📁 Extraer en...") { ForeColor = Theme.Teal };

            miOpen.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    FileOpener.Open(listView.SelectedItems[0].Tag!.ToString()!, this, NavigateToPath);
            };
            miNewFolder.Click += (s, e) => FileOperationService.CreateFolder(currentPath, this, () => LoadDirectory(currentPath));
            miRename.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    FileOperationService.RenameSelected(listView.SelectedItems[0].Tag!.ToString()!, this, () => LoadDirectory(currentPath));
            };
            miDelete.Click += (s, e) =>
            {
                var paths = listView.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag!.ToString()!).ToArray();
                FileOperationService.DeleteSelected(paths, Handle, this, () => LoadDirectory(currentPath));
            };
            miRefresh.Click += (s, e) => RefreshView();
            miProps.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    new FilePropertiesForm(listView.SelectedItems[0].Tag!.ToString()!).Show(this);
            };

            miCompress.Click += (s, e) =>
            {
                var selected = listView.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag!.ToString()!).ToArray();
                CompressionService.Compress(selected, this, () => LoadDirectory(currentPath));
            };

            miExtractHere.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count == 0) return;
                string path = listView.SelectedItems[0].Tag!.ToString()!;
                CompressionService.Extract(path, this, extractHere: true, onRefresh: () => LoadDirectory(currentPath));
            };

            miExtractTo.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count == 0) return;
                string path = listView.SelectedItems[0].Tag!.ToString()!;
                CompressionService.Extract(path, this, extractHere: false, onRefresh: () => LoadDirectory(currentPath));
            };

            contextMenu.Items.AddRange(new ToolStripItem[] { miOpen, sep1, miNewFolder, sep2, miRename, miDelete, sep3, miProps, sep4, miRefresh, sep5, miCompress, miExtractHere, miExtractTo });
        }

        private void ListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            bool sel = listView.SelectedItems.Count > 0;
            contextMenu.Items[0].Visible = sel;
            contextMenu.Items[1].Visible = sel;
            contextMenu.Items[3].Visible = sel;
            contextMenu.Items[4].Visible = sel;
            contextMenu.Items[5].Visible = sel;
            contextMenu.Items[6].Visible = sel;
            contextMenu.Items[7].Visible = sel;
            contextMenu.Items[8].Visible = sel;

            bool hasSelection = listView.SelectedItems.Count > 0;
            bool isArchive = hasSelection && FileExtensions.Archive.Contains(Path.GetExtension(listView.SelectedItems[0].Tag?.ToString() ?? ""));

            miCompress.Visible = hasSelection;
            miExtractHere.Visible = isArchive;
            miExtractTo.Visible = isArchive;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PANEL DERECHO — TREEVIEW
        // ════════════════════════════════════════════════════════════════════
        private static readonly (string Label, string[] Exts)[] FileGroups =
        {
            ("Imágenes",   new[]{".jpg",".jpeg",".png",".gif",".bmp",".ico",".webp",".tiff"}),
            ("Audio",      new[]{".mp3",".wav",".wma",".m4a",".flac",".aac",".ogg"}),
            ("Video",      new[]{".mp4",".avi",".mkv",".mov",".wmv",".flv",".webm"}),
            ("Texto",      new[]{".txt",".json",".xml",".csv",".log",".ini",".md",
                                 ".cs",".py",".js",".ts",".html",".css",".config"}),
            ("Documentos", new[]{".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf"}),
            ("Otros",      Array.Empty<string>()),
        };

        private void UpdateRightPanel(string path)
        {
            if (infoTree == null) return;
            infoTree.BeginUpdate();
            infoTree.Nodes.Clear();
            try { folderNameLabel.Text = new DirectoryInfo(path).Name; } catch { }
            try
            {
                var di = new DirectoryInfo(path);
                var subdirs = di.GetDirectories().Where(d => (d.Attributes & FileAttributes.Hidden) == 0).OrderBy(d => d.Name).ToArray();
                var files = di.GetFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).ToArray();

                if (subdirs.Length > 0)
                {
                    var grp = MakeGroupNode("Carpetas", subdirs.Length, NodeKind.Header);
                    foreach (var d in subdirs)
                    {
                        var dn = MakeFolderNode(d.Name, d.FullName);
                        PopulateFolderNodeDummy(dn, d.FullName);
                        grp.Nodes.Add(dn);
                    }
                    grp.Expand();
                    infoTree.Nodes.Add(grp);
                }

                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (label, exts) in FileGroups)
                {
                    var matched = exts.Length == 0
                        ? files.Where(f => !used.Contains(f.FullName)).ToArray()
                        : files.Where(f => exts.Contains(f.Extension.ToLower()) && !used.Contains(f.FullName)).ToArray();
                    if (matched.Length == 0) continue;
                    foreach (var f in matched) used.Add(f.FullName);
                    var grp = MakeGroupNode(label, matched.Length, NodeKind.Category);
                    foreach (var f in matched.OrderBy(x => x.Name)) grp.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                    infoTree.Nodes.Add(grp);
                }

                if (infoTree.Nodes.Count == 0) infoTree.Nodes.Add(MakeDimNode("Carpeta vacía"));
            }
            catch { infoTree.Nodes.Add(MakeDimNode("Sin acceso")); }
            infoTree.EndUpdate();
        }

        private async Task SearchInPanelAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { UpdateRightPanel(currentPath); return; }
            _searchCts.Cancel();
            _searchCts = new CancellationTokenSource();
            var cts = _searchCts;

            infoTree.BeginUpdate();
            infoTree.Nodes.Clear();
            infoTree.Nodes.Add(MakeDimNode("Buscando..."));
            infoTree.EndUpdate();
            folderNameLabel.Text = $"🔍 \"{query}\"";

            DirectoryInfo[]? dirs = null;
            FileInfo[]? fls = null;
            try
            {
                (dirs, fls) = await Task.Run(() =>
                {
                    var di = new DirectoryInfo(currentPath);
                    var d = di.GetDirectories("*", SearchOption.AllDirectories)
                        .Where(x => (x.Attributes & FileAttributes.Hidden) == 0 &&
                                    x.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Name).ToArray();
                    cts.Token.ThrowIfCancellationRequested();
                    var f = di.GetFiles("*", SearchOption.AllDirectories)
                        .Where(x => (x.Attributes & FileAttributes.Hidden) == 0 &&
                                    x.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Name).ToArray();
                    return (d, f);
                }, cts.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                infoTree.BeginUpdate(); infoTree.Nodes.Clear();
                infoTree.Nodes.Add(MakeDimNode("Error: " + ex.Message));
                infoTree.EndUpdate(); return;
            }
            if (cts.IsCancellationRequested) return;

            infoTree.BeginUpdate(); infoTree.Nodes.Clear();
            if (dirs!.Length == 0 && fls!.Length == 0) { infoTree.Nodes.Add(MakeDimNode("Sin resultados")); infoTree.EndUpdate(); return; }

            if (dirs.Length > 0)
            {
                var rn = MakeGroupNode("Carpetas", dirs.Length, NodeKind.Header);
                foreach (var d in dirs) { var dn = MakeFolderNode(d.Name, d.FullName); PopulateFolderNodeDummy(dn, d.FullName); rn.Nodes.Add(dn); }
                rn.Expand(); infoTree.Nodes.Add(rn);
            }
            if (fls!.Length > 0)
            {
                var rn = MakeGroupNode("Archivos", fls.Length, NodeKind.Header);
                foreach (var f in fls) rn.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                rn.Expand(); infoTree.Nodes.Add(rn);
            }
            infoTree.EndUpdate();
        }

        private void PopulateFolderNodeDummy(TreeNode node, string folderPath)
        {
            try
            {
                var di = new DirectoryInfo(folderPath);
                bool has = di.GetDirectories().Any(d => (d.Attributes & FileAttributes.Hidden) == 0) ||
                           di.GetFiles().Any(f => (f.Attributes & FileAttributes.Hidden) == 0);
                if (has) node.Nodes.Add(new TreeNode("__dummy__") { Tag = new NodeTag(NodeKind.Dim, "__dummy__") });
            }
            catch { }
        }

        private void InfoTree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node?.Tag is not NodeTag nt || nt.Kind != NodeKind.Folder || nt.Path == null) return;
            if (node.Nodes.Count == 1 && node.Nodes[0].Tag is NodeTag dt && dt.Path == "__dummy__")
            {
                infoTree.BeginUpdate(); node.Nodes.Clear();
                try
                {
                    var di = new DirectoryInfo(nt.Path);
                    var subdirs = di.GetDirectories().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToArray();
                    var files = di.GetFiles().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToArray();
                    foreach (var sub in subdirs) { var sn = MakeFolderNode(sub.Name, sub.FullName); PopulateFolderNodeDummy(sn, sub.FullName); node.Nodes.Add(sn); }
                    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (label, exts) in FileGroups)
                    {
                        var matched = exts.Length == 0
                            ? files.Where(f => !used.Contains(f.FullName)).ToArray()
                            : files.Where(f => exts.Contains(f.Extension.ToLower()) && !used.Contains(f.FullName)).ToArray();
                        if (matched.Length == 0) continue;
                        foreach (var f in matched) used.Add(f.FullName);
                        var grp = MakeGroupNode(label, matched.Length, NodeKind.Category);
                        foreach (var f in matched) grp.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                        node.Nodes.Add(grp);
                    }
                    if (node.Nodes.Count == 0) node.Nodes.Add(MakeDimNode("Vacía"));
                }
                catch { node.Nodes.Add(MakeDimNode("Sin acceso")); }
                infoTree.EndUpdate();
            }
        }

        private void InfoTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is not NodeTag nt) return;
            if (nt.Path == null || nt.Path == "__dummy__") return;

            if (nt.Kind == NodeKind.Folder)
            {
                if (e.Node.IsExpanded) e.Node.Collapse();
                else e.Node.Expand();
            }
            else if (nt.Kind == NodeKind.File && File.Exists(nt.Path))
            {
                infoTree.SelectedNode = e.Node;
            }
        }

        private void InfoTree_NodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is not NodeTag nt) return;
            if (nt.Path == null || nt.Path == "__dummy__") return;

            if (nt.Kind == NodeKind.File && File.Exists(nt.Path))
                FileOpener.Open(nt.Path, this, NavigateToPath);
        }

        private enum NodeKind { Header, Category, Folder, File, Dim }
        private record NodeTag(NodeKind Kind, string? Path = null);

        private void InfoTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null) return;
            NodeKind kind = e.Node.Tag is NodeTag nt ? nt.Kind : NodeKind.Dim;
            Color fg = kind switch
            {
                NodeKind.Header => Theme.Accent2,
                NodeKind.Category => Theme.TextSecondary,
                NodeKind.Folder => Color.FromArgb(251, 191, 36),
                NodeKind.File => Theme.TextPrimary,
                _ => Theme.TextMuted
            };
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            var rowRect = new Rectangle(0, e.Bounds.Top, infoTree.Width, e.Bounds.Height);
            using var bgBrush = new SolidBrush(selected ? Theme.BgSelected : Theme.BgSurface);
            e.Graphics.FillRectangle(bgBrush, rowRect);
            int indent = (e.Node.Level + 1) * infoTree.Indent;
            int textX = indent + 4;
            int textY = e.Bounds.Top + (e.Bounds.Height - 14) / 2;
            if (e.Node.Nodes.Count > 0 || (e.Node.Tag is NodeTag nt2 && nt2.Kind == NodeKind.Folder))
            {
                int btnX = indent - 14, btnY = e.Bounds.Top + (e.Bounds.Height - 8) / 2;
                using var signBrush = new SolidBrush(Theme.TextMuted);
                e.Graphics.DrawString(e.Node.IsExpanded ? "−" : "+", Theme.FontSmallBold, signBrush, btnX - 2, btnY - 2);
            }
            FontStyle fs = kind == NodeKind.Header ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font("Segoe UI", 8.5F, fs);
            using var br = new SolidBrush(fg);
            e.Graphics.DrawString(e.Node.Text, font, br, textX, textY);
        }

        private static TreeNode MakeGroupNode(string label, int count, NodeKind kind)
            => new($"{label}  ({count})") { Tag = new NodeTag(kind) };
        private static TreeNode MakeFolderNode(string name, string path)
            => new("▸ " + name) { Tag = new NodeTag(NodeKind.Folder, path) };
        private static TreeNode MakeFileNode(string name, string path)
            => new("  " + name) { Tag = new NodeTag(NodeKind.File, path) };
        private static TreeNode MakeDimNode(string text)
            => new("  " + text) { Tag = new NodeTag(NodeKind.Dim) };

        // ════════════════════════════════════════════════════════════════════
        //  REFRESH / EXPORT
        // ════════════════════════════════════════════════════════════════════
        private void RefreshView() { if (!string.IsNullOrEmpty(currentPath)) LoadDirectory(currentPath); }

        private async Task ExportCsvAsync()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Exportar índice CSV",
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"indice_{Path.GetFileName(currentPath)}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                InitialDirectory = currentPath
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            exportCsvButton.Enabled = false;
            exportCsvButton.Text = "Generando...";
            var progress = new Progress<string>(f => { if (IsHandleCreated) BeginInvoke(() => statusLabel.Text = $"  Indexando: {f}"); });
            try
            {
                string csv = await CsvIndexer.GenerateAsync(currentPath, progress);
                await File.WriteAllTextAsync(dlg.FileName, csv, System.Text.Encoding.UTF8);
                statusLabel.Text = $"  Exportado → {Path.GetFileName(dlg.FileName)}";
                if (MessageBox.Show($"CSV generado:\n{dlg.FileName}\n\n¿Abrir?", "Exportación", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { exportCsvButton.Enabled = true; exportCsvButton.Text = "↓ Exportar CSV"; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DRAG & DROP
        // ════════════════════════════════════════════════════════════════════
        private void ListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            string[] paths = listView.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag!.ToString()!).ToArray();
            if (paths.Length > 0)
                listView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Move | DragDropEffects.Copy);
        }

        private void ListView_DragEnter(object sender, DragEventArgs e)
            => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.None; return; }
            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            var hovered = listView.GetItemAt(pt.X, pt.Y);
            string[] dragged = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (dragHighlightedItem != null && dragHighlightedItem != hovered)
            { dragHighlightedItem.BackColor = Theme.BgBase; dragHighlightedItem.ForeColor = Theme.TextPrimary; dragHighlightedItem = null; }
            bool valid = hovered != null && Directory.Exists(hovered.Tag!.ToString()) && !dragged.Contains(hovered.Tag!.ToString());
            if (valid) { e.Effect = DragDropEffects.Move; hovered!.BackColor = Theme.DragTarget; hovered.ForeColor = Theme.Accent2; dragHighlightedItem = hovered; }
            else e.Effect = DragDropEffects.None;
        }

        private void ListView_DragDrop(object sender, DragEventArgs e)
        {
            ClearDragHighlight();
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            var target = listView.GetItemAt(pt.X, pt.Y);
            if (target == null || !Directory.Exists(target.Tag!.ToString())) return;

            FileOperationService.MoveItems(
                (string[])e.Data.GetData(DataFormats.FileDrop)!,
                target.Tag!.ToString()!,
                this,
                () => LoadDirectory(currentPath));
        }

        private void ClearDragHighlight()
        {
            if (dragHighlightedItem == null) return;
            dragHighlightedItem.BackColor = Theme.BgBase;
            dragHighlightedItem.ForeColor = Theme.TextPrimary;
            dragHighlightedItem = null;
        }

        private void RecycleDragEnter(DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effect = DragDropEffects.Move;
            recycleDropPanel.BackColor = Theme.RecycleHot;
            recyclePanelLabel.ForeColor = Theme.Coral;
            recyclePanelLabel.Text = "Soltar para eliminar";
            if (_recycleIconFull != null) recycleIconBox.Image = _recycleIconFull;
        }

        private void RecycleDragOver(DragEventArgs e)
            => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;

        private void RecycleDragLeave()
        {
            recycleDropPanel.BackColor = Theme.RecycleBg;
            recyclePanelLabel.ForeColor = Theme.TextMuted;
            recyclePanelLabel.Text = "Papelera";
            if (_recycleIconEmpty != null) recycleIconBox.Image = _recycleIconEmpty;
        }
        private void RecycleDragDrop(DragEventArgs e)
        {
            RecycleDragLeave();
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            string msg = paths.Length == 1
                ? $"¿Eliminar \"{Path.GetFileName(paths[0])}\"?"
                : $"¿Eliminar {paths.Length} elementos?";
            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (string p in paths)
                FileOperationService.SendToRecycleBin(p, Handle);

            LoadDirectory(currentPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  NAVEGACIÓN
        // ════════════════════════════════════════════════════════════════════
        private void NavigateToPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                { MessageBox.Show("La ruta no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                if (!string.IsNullOrEmpty(currentPath) && currentPath != path)
                {
                    navigationHistory.Push(currentPath);
                    navigationForward.Clear();
                }

                currentPath = path;

                // Desuscribir temporalmente para que no salga el menú al cambiar de carpeta programáticamente
                if (addressBar != null)
                {
                    addressBar.TextChanged -= AddressBar_TextChanged;
                    addressBar.Text = currentPath;
                    addressBar.TextChanged += AddressBar_TextChanged;
                }

                UpdateNavButtons();
                LoadDirectory(currentPath);
            }
            catch (UnauthorizedAccessException) { MessageBox.Show("Sin permisos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void GoBack()
        {
            if (navigationHistory.Count == 0) return;
            navigationForward.Push(currentPath);
            currentPath = navigationHistory.Pop();

            addressBar.TextChanged -= AddressBar_TextChanged;
            addressBar.Text = currentPath;
            addressBar.TextChanged += AddressBar_TextChanged;

            UpdateNavButtons();
            LoadDirectory(currentPath);
        }

        private void GoForward()
        {
            if (navigationForward.Count == 0) return;
            navigationHistory.Push(currentPath);
            currentPath = navigationForward.Pop();

            addressBar.TextChanged -= AddressBar_TextChanged;
            addressBar.Text = currentPath;
            addressBar.TextChanged += AddressBar_TextChanged;

            UpdateNavButtons();
            LoadDirectory(currentPath);
        }

        private void GoUp()
        {
            try { var p = Directory.GetParent(currentPath); if (p != null) NavigateToPath(p.FullName); }
            catch { }
        }

        private void UpdateNavButtons()
        {
            backButton.ForeColor = navigationHistory.Count > 0 ? Theme.TextPrimary : Theme.TextMuted;
            forwardButton.ForeColor = navigationForward.Count > 0 ? Theme.TextPrimary : Theme.TextMuted;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGA DE DIRECTORIO (async)
        // ════════════════════════════════════════════════════════════════════
        private async void LoadDirectory(string path)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            listView.Items.Clear();
            statusLabel.Text = "  Cargando...";
            Cursor = Cursors.WaitCursor;

            try
            {
                var di = new DirectoryInfo(path);
                var (dirs, files) = await Task.Run(() =>
                {
                    var d = di.GetDirectories().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToList();
                    var f = di.GetFiles().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToList();
                    return (d, f);
                }, token);

                if (token.IsCancellationRequested) return;

                var sem = new SemaphoreSlim(8);
                var dirInfoTasks = dirs.Select(async d =>
                {
                    await sem.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return (d, "(cancelado)");
                        string info = await Task.Run(() => FileTypeHelper.FolderInfoColumn(d.FullName), token);
                        return (d, info);
                    }
                    finally { sem.Release(); }
                }).ToList();

                var dirInfoResults = await Task.WhenAll(dirInfoTasks);
                if (token.IsCancellationRequested) return;

                listView.BeginUpdate();
                foreach (var (d, info) in dirInfoResults)
                {
                    if (token.IsCancellationRequested) break;
                    var item = new ListViewItem(d.Name) { ImageKey = "folder", Tag = d.FullName };
                    item.SubItems.Add("Carpeta");
                    item.SubItems.Add("");
                    item.SubItems.Add(info);
                    item.SubItems.Add(d.LastWriteTime.ToString("dd/MM/yyyy  HH:mm"));
                    listView.Items.Add(item);
                }
                foreach (var f in files)
                {
                    if (token.IsCancellationRequested) break;
                    var item = new ListViewItem(f.Name) { ImageKey = FileIconFactory.IconKey(f.Extension), Tag = f.FullName };
                    item.SubItems.Add(FileTypeHelper.TypeName(f.Extension));
                    item.SubItems.Add(FileSize.Format(f.Length));
                    item.SubItems.Add(f.Extension.ToUpper().TrimStart('.'));
                    item.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy  HH:mm"));
                    listView.Items.Add(item);
                }
                listView.EndUpdate();

                if (!token.IsCancellationRequested)
                {
                    var stats = CsvIndexer.ClassifyFiles(files.ToArray());
                    statusLabel.Text = "  " + FileIconFactory.BuildStatusText(stats, dirs.Count);
                    UpdateRightPanel(path);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) statusLabel.Text = $"  Error: {ex.Message}"; }
            finally { if (!token.IsCancellationRequested) Cursor = Cursors.Default; }
        }

        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn) { sortColumn = e.Column; listView.Sorting = SortOrder.Ascending; }
            else listView.Sorting = listView.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            listView.Sort();
            listView.ListViewItemSorter = new LvComparer(e.Column, listView.Sorting);
        }
    }
}