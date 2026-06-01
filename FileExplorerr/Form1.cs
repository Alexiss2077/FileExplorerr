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
        // ── Estado del explorador ────────────────────────────────────────────
        private string currentPath = "";
        private readonly Stack<string> navigationHistory = new();
        private readonly Stack<string> navigationForward = new();
        private ListViewItem? dragHighlightedItem;
        private int sortColumn = -1;

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.U4)] public int wFunc;
            public string? pFrom;
            public string? pTo;
            public short fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string? lpszProgressTitle;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
        private const int FO_DELETE = 3;
        private const int FOF_ALLOWUNDO = 0x40;
        private const int FOF_NOCONFIRMATION = 0x10;

        private bool SendToRecycleBin(string path)
        {
            try
            {
                var op = new SHFILEOPSTRUCT
                {
                    hwnd = Handle,
                    wFunc = FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = (short)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION)
                };
                return SHFileOperation(ref op) == 0;
            }
            catch { return false; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════
        public Form1()
        {
            InitializeComponent();
            BuildUI();
            HandleCreated += (s, e) => ApplyDarkTitleBar();
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
            imageList.Images.Add("folder", MakeFolderIcon());
            imageList.Images.Add("file", MakeFileIcon());
            imageList.Images.Add("image", MakeImageIcon());
            imageList.Images.Add("audio", MakeAudioIcon());
            imageList.Images.Add("video", MakeVideoIcon());
            imageList.Images.Add("text", MakeTextIcon());

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
                Height = 54,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(12, 8, 12, 8)
            };

            // Logo
            appLogoLabel = new Label
            {
                Text = "  FileExplorerr",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Accent2,
                AutoSize = true,
                Location = new Point(12, 14),
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
                // Si hay audio en la carpeta actual lo carga; si no, abre vacío igual
                new MusicPlayerForm(file ?? "").Show();
            };
            navVideo.Click += (s, e) =>
            {
                SwitchPage("video");
                string? file = GetFirstVideoFile();
                // Si hay video en la carpeta actual lo carga; si no, abre vacío igual
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

            // Botón SQL a la derecha
            var sqlQuickBtn = new Button
            {
                Text = "🗄️  Abrir SQL",
                Height = 32,
                AutoSize = true,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Theme.SkyDim,
                ForeColor = Theme.Sky,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontSmallBold,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            sqlQuickBtn.FlatAppearance.BorderColor = Color.FromArgb(96, 165, 250, 80);
            sqlQuickBtn.Click += (s, e) => new SqlViewerForm().Show();

            topNavBar.Controls.Add(appLogoLabel);
            topNavBar.Controls.Add(sep);
            topNavBar.Controls.Add(tabFlow);

            // Posicionar sqlQuickBtn a la derecha tras resize
            topNavBar.Controls.Add(sqlQuickBtn);
            topNavBar.Resize += (s, e) =>
            {
                sqlQuickBtn.Location = new Point(topNavBar.Width - sqlQuickBtn.Width - 12, 11);
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

            // Resetear todas las pestañas
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
            try
            {
                return Directory.GetFiles(currentPath)
                    .FirstOrDefault(x => MusicPlayerForm.SupportedExtensions
                        .Contains(Path.GetExtension(x).ToLower()));
            }
            catch { return null; }
        }

        private string? GetFirstVideoFile()
        {
            string[] exts = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".3gp", ".mpg", ".mpeg" };
            try
            {
                return Directory.GetFiles(currentPath)
                    .FirstOrDefault(x => exts.Contains(Path.GetExtension(x).ToLower()));
            }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PÁGINA EXPLORER
        // ════════════════════════════════════════════════════════════════════
        private void BuildExplorerPage()
        {
            explorerPage = new Panel { BackColor = Theme.BgBase };

            // ── Toolbar del explorador ────────────────────────────────────────
            var toolbar = new Panel
            {
                Height = 54,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface
            };

            // Botones de navegación — posicionamiento absoluto vía Resize
            backButton = MakeNavBtn("←", "Atrás");
            forwardButton = MakeNavBtn("→", "Adelante");
            upButton = MakeNavBtn("↑", "Subir nivel");
            refreshButton = MakeNavBtn("↻", "Actualizar (F5)");

            backButton.Click += (s, e) => GoBack();
            forwardButton.Click += (s, e) => GoForward();
            upButton.Click += (s, e) => GoUp();
            refreshButton.Click += (s, e) => RefreshView();

            // Barra de dirección — se posiciona correctamente en LayoutToolbar()
            var addrPanel = new Panel
            {
                Height = 34,
                BackColor = Theme.BgElevated,
                Location = new Point(-500, 0)   // fuera de pantalla hasta el primer layout
            };
            var addrIcon = new Label
            {
                Text = "📂",
                Width = 28,
                Height = 34,
                Location = new Point(6, 0),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 13F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            addressBar = new TextBox
            {
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody
            };
            addressBar.KeyDown += AddressBar_KeyDown;
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
            newFolderButton.Click += (s, e) => CreateFolder();
            exportCsvButton.Click += async (s, e) => await ExportCsvAsync();

            // Añadir todos los controles al toolbar
            toolbar.Controls.AddRange(new Control[]
            {
                backButton, forwardButton, upButton, refreshButton,
                addrPanel, newFolderButton, exportCsvButton
            });

            // Posicionamiento absoluto — se recalcula en cada Resize
            void LayoutToolbar()
            {
                if (toolbar.Width < 200) return;   // no hacer nada hasta tener ancho real
                int h = toolbar.Height;
                int cy = (h - 34) / 2;
                int x = 10;

                backButton.SetBounds(x, cy, 34, 34); x += 38;
                forwardButton.SetBounds(x, cy, 34, 34); x += 38;
                upButton.SetBounds(x, cy, 34, 34); x += 38;
                refreshButton.SetBounds(x, cy, 34, 34); x += 44;

                int rx = toolbar.Width - 10;
                exportCsvButton.SetBounds(rx - exportCsvButton.Width, cy, exportCsvButton.Width, 34);
                rx -= exportCsvButton.Width + 6;
                newFolderButton.SetBounds(rx - newFolderButton.Width, cy, newFolderButton.Width, 34);
                rx -= newFolderButton.Width + 8;

                if (rx > x)   // solo si hay espacio real para la barra de dirección
                    addrPanel.SetBounds(x, cy, rx - x, 34);
            }

            toolbar.Resize += (s, e) => LayoutToolbar();
            this.Resize += (s, e) => LayoutToolbar();
            this.Shown += (s, e) => BeginInvoke((Action)LayoutToolbar);

            // ── Body: sidebar + listview + right panel ────────────────────────
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
                    // posición inicial correcta una vez que el panel tenga tamaño
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

            // Accesos rápidos
            AddHeader("ACCESOS RÁPIDOS");
            AddItem("🏠", "Inicio", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            AddItem("🖥️", "Escritorio", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
            AddItem("📄", "Documentos", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
            AddItem("🖼️", "Imágenes", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));
            AddItem("🎵", "Música", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)));
            AddItem("🎬", "Videos", () => NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)));
            AddItem("⬇️", "Descargas", () => NavigateToPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));

            // Herramientas
            AddHeader("HERRAMIENTAS");
            AddItem("🗄️", "SQL / Base de datos", () => new SqlViewerForm().Show());
            AddItem("📊", "Exportar CSV", () => _ = ExportCsvAsync());

            // Dispositivos
            AddHeader("DISPOSITIVOS");
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                AddItem("💽", $"{drive.Name}  {drive.VolumeLabel}".TrimEnd(), () => NavigateToPath(drive.RootDirectory.FullName));

            scroll.Controls.SetChildIndex(scroll.Controls[0], scroll.Controls.Count - 1); // reset Z-order

            // Relayout para que DockStyle.Top funcione con el scroll inverso
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
            listView.Columns.Add("Nombre", 280);
            listView.Columns.Add("Tipo", 100);
            listView.Columns.Add("Tamaño", 90);
            listView.Columns.Add("Info", 200);
            listView.Columns.Add("Modificado", 140);

            listView.DrawColumnHeader += ListView_DrawColumnHeader;
            listView.DrawItem += (s, e) => e.DrawDefault = true;
            listView.DrawSubItem += (s, e) => e.DrawDefault = true;
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    OpenEntry(listView.SelectedItems[0].Tag!.ToString()!);
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

        // ── Cabecera del ListView con estilo personalizado ────────────────────
        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var bg = new SolidBrush(Theme.BgSurface);
            e.Graphics.FillRectangle(bg, e.Bounds);
            using var line = new Pen(Color.FromArgb(255, 255, 255, 12));
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var rect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
            using var br = new SolidBrush(Theme.TextMuted);
            using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(e.Header!.Text, Theme.FontSmallBold, br, rect, sf);
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

            // Papelera — cargamos ambos íconos (vacía y llena) desde shell32
            recycleIconBox = new PictureBox
            {
                Size = new Size(36, 36),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Dock = DockStyle.Right,
                AllowDrop = true
            };

            // Precargar ícono vacío (31) y lleno (32)
            try
            {
                string shell32 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");

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

            miOpen.Click += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag!.ToString()!); };
            miNewFolder.Click += (s, e) => CreateFolder();
            miRename.Click += (s, e) => RenameSelected();
            miDelete.Click += (s, e) => DeleteSelected();
            miRefresh.Click += (s, e) => RefreshView();
            miProps.Click += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0)
                    new FilePropertiesForm(listView.SelectedItems[0].Tag!.ToString()!).Show(this);
            };

            contextMenu.Items.AddRange(new ToolStripItem[]
                { miOpen, sep1, miNewFolder, sep2, miRename, miDelete, sep3, miProps, sep4, miRefresh });
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

        // Clic simple: expande/colapsa carpetas en el mismo panel
        private void InfoTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is not NodeTag nt) return;
            if (nt.Path == null || nt.Path == "__dummy__") return;

            if (nt.Kind == NodeKind.Folder)
            {
                // Expandir o colapsar sin navegar el explorador principal
                if (e.Node.IsExpanded)
                    e.Node.Collapse();
                else
                    e.Node.Expand();
            }
            else if (nt.Kind == NodeKind.File && File.Exists(nt.Path))
            {
                // Clic simple en archivo: seleccionarlo visualmente nada más
                infoTree.SelectedNode = e.Node;
            }
        }

        // Doble clic en ARCHIVO: abrirlo. Doble clic en CARPETA: navegar en el explorador
        private void InfoTree_NodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is not NodeTag nt) return;
            if (nt.Path == null || nt.Path == "__dummy__") return;

            if (nt.Kind == NodeKind.File && File.Exists(nt.Path))
                OpenEntry(nt.Path);
            // Las carpetas ya se manejan con clic simple (expand/collapse)
            // Si quieres navegar al explorador con doble clic en carpeta, descomenta:
            // else if (nt.Kind == NodeKind.Folder && Directory.Exists(nt.Path))
            //     NavigateToPath(nt.Path);
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
        //  CREAR / RENOMBRAR / ELIMINAR
        // ════════════════════════════════════════════════════════════════════
        private void CreateFolder()
        {
            string? name = InputDialog("Nueva carpeta", "Nombre:");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            { MessageBox.Show("Nombre no válido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string newDir = Path.Combine(currentPath, name);
            if (Directory.Exists(newDir)) { MessageBox.Show("Ya existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try { Directory.CreateDirectory(newDir); LoadDirectory(currentPath); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RenameSelected()
        {
            if (listView.SelectedItems.Count == 0) return;
            string oldPath = listView.SelectedItems[0].Tag!.ToString()!;
            string oldName = Path.GetFileName(oldPath);
            string? newName = InputDialog("Renombrar", "Nuevo nombre:", oldName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
            string newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);
            try
            {
                if (File.Exists(oldPath)) File.Move(oldPath, newPath);
                else if (Directory.Exists(oldPath)) Directory.Move(oldPath, newPath);
                LoadDirectory(currentPath);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DeleteSelected()
        {
            if (listView.SelectedItems.Count == 0) return;
            string[] paths = listView.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag!.ToString()!).ToArray();
            string msg = paths.Length == 1
                ? $"¿Eliminar \"{Path.GetFileName(paths[0])}\"?"
                : $"¿Eliminar {paths.Length} elementos?";
            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (string p in paths) SendToRecycleBin(p);
            LoadDirectory(currentPath);
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
            MoveItems((string[])e.Data.GetData(DataFormats.FileDrop)!, target.Tag!.ToString()!);
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
            // Cambiar al ícono de papelera LLENA (igual que Windows)
            if (_recycleIconFull != null) recycleIconBox.Image = _recycleIconFull;
        }

        private void RecycleDragOver(DragEventArgs e)
            => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;

        private void RecycleDragLeave()
        {
            recycleDropPanel.BackColor = Theme.RecycleBg;
            recyclePanelLabel.ForeColor = Theme.TextMuted;
            recyclePanelLabel.Text = "Papelera";
            // Restaurar ícono de papelera VACÍA
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
            foreach (string p in paths) SendToRecycleBin(p);
            LoadDirectory(currentPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOVER ARCHIVOS
        // ════════════════════════════════════════════════════════════════════
        private void MoveItems(string[] sources, string targetDir)
        {
            foreach (string src in sources)
            {
                try
                {
                    string name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar));
                    string dest = Path.Combine(targetDir, name);
                    if (dest.Equals(src, StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(src))
                    {
                        if (File.Exists(dest))
                        {
                            var r = MessageBox.Show($"Ya existe \"{name}\". ¿Sobreescribir?", "Conflicto",
                                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (r == DialogResult.Cancel) return;
                            if (r == DialogResult.No) continue;
                            File.Delete(dest);
                        }
                        File.Move(src, dest);
                    }
                    else if (Directory.Exists(src))
                    {
                        if (targetDir.StartsWith(src + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        { MessageBox.Show("No se puede mover dentro de sí misma.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); continue; }
                        Directory.Move(src, dest);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
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
                addressBar.Text = currentPath;
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
            addressBar.Text = currentPath;
            UpdateNavButtons();
            LoadDirectory(currentPath);
        }

        private void GoForward()
        {
            if (navigationForward.Count == 0) return;
            navigationHistory.Push(currentPath);
            currentPath = navigationForward.Pop();
            addressBar.Text = currentPath;
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

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                NavigateToPath(addressBar.Text);
                e.Handled = e.SuppressKeyPress = true;
            }
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
                        string info = await Task.Run(() => DirInfoDetailed(d.FullName), token);
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
                    var item = new ListViewItem(f.Name) { ImageKey = IconKey(f.Extension), Tag = f.FullName };
                    item.SubItems.Add(FileTypeName(f.Extension));
                    item.SubItems.Add(FormatSize(f.Length));
                    item.SubItems.Add(f.Extension.ToUpper().TrimStart('.'));
                    item.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy  HH:mm"));
                    listView.Items.Add(item);
                }
                listView.EndUpdate();

                if (!token.IsCancellationRequested)
                {
                    var stats = CsvIndexer.ClassifyFiles(files.ToArray());
                    statusLabel.Text = "  " + BuildStatusText(stats, dirs.Count);
                    UpdateRightPanel(path);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) statusLabel.Text = $"  Error: {ex.Message}"; }
            finally { if (!token.IsCancellationRequested) Cursor = Cursors.Default; }
        }

        // ── Barra de estado con etiquetas descriptivas completas ─────────────
        private static string BuildStatusText(FileStats s, int folders)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (folders > 0)
                parts.Add($"📁 {folders} carpeta{(folders != 1 ? "s" : "")}");

            int total = s.Total;
            if (total > 0)
                parts.Add($"📄 {total} archivo{(total != 1 ? "s" : "")}");

            if (s.Images > 0)
                parts.Add($"🖼️ {s.Images} imág{(s.Images != 1 ? "enes" : "en")}");

            if (s.Audio > 0)
                parts.Add($"🎵 {s.Audio} audio{(s.Audio != 1 ? "s" : "")}");

            if (s.Video > 0)
                parts.Add($"🎬 {s.Video} video{(s.Video != 1 ? "s" : "")}");

            if (s.Text > 0)
                parts.Add($"📝 {s.Text} texto{(s.Text != 1 ? "s" : "")}");

            if (s.Other > 0)
                parts.Add($"📦 {s.Other} otro{(s.Other != 1 ? "s" : "")}");

            return parts.Count > 0
                ? string.Join("  ·  ", parts)
                : "Carpeta vacía";
        }

        private string DirInfoDetailed(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                var files = di.GetFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).ToArray();
                var subs = di.GetDirectories().Where(d => (d.Attributes & FileAttributes.Hidden) == 0).ToArray();
                return CsvIndexer.ClassifyFiles(files).ToInfoColumn(subs.Length);
            }
            catch { return "Sin acceso"; }
        }

        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn) { sortColumn = e.Column; listView.Sorting = SortOrder.Ascending; }
            else listView.Sorting = listView.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            listView.Sort();
            listView.ListViewItemSorter = new LvComparer(e.Column, listView.Sorting);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ABRIR ARCHIVO
        // ════════════════════════════════════════════════════════════════════
        private void OpenEntry(string path)
        {
            if (Directory.Exists(path)) { NavigateToPath(path); return; }
            if (!File.Exists(path)) return;

            string ext = Path.GetExtension(path).ToLower();
            try
            {
                if (ext == ".txt")
                {
                    using var dlg = new Form
                    {
                        Text = "Abrir como...",
                        Width = 360,
                        Height = 170,
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false,
                        BackColor = Theme.BgSurface,
                        ForeColor = Theme.TextPrimary
                    };
                    var lbl = new Label { Text = $"¿Cómo deseas abrir \"{Path.GetFileName(path)}\"?", Left = 14, Top = 20, Width = 320, ForeColor = Theme.TextSecondary, Font = Theme.FontBody };
                    var btnTbl = Theme.MakeButton("Visor de tabla", 140); btnTbl.Left = 14; btnTbl.Top = 70; btnTbl.Click += (s, e) => { dlg.Tag = "table"; dlg.DialogResult = DialogResult.OK; };
                    var btnNote = Theme.MakeButton("Bloc de notas", 140, Theme.ButtonKind.Primary); btnNote.Left = 164; btnNote.Top = 70; btnNote.Click += (s, e) => { dlg.Tag = "notepad"; dlg.DialogResult = DialogResult.OK; };
                    dlg.Controls.AddRange(new Control[] { lbl, btnTbl, btnNote });
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (dlg.Tag?.ToString() == "notepad") new NotepadForm(path).Show();
                        else new FileViewerForm(path).Show();
                    }
                }
                else if (new[] { ".csv", ".json", ".xml", ".log" }.Contains(ext)) new FileViewerForm(path).Show();
                else if (ImageViewerForm.SupportedExtensions.Contains(ext)) new ImageViewerForm(path).Show();
                else if (MusicPlayerForm.SupportedExtensions.Contains(ext)) new MusicPlayerForm(path).Show();
                else if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".3gp", ".mpg", ".mpeg", ".vob", ".divx" }.Contains(ext))
                    new VideoPlayerForm(path).Show();
                else Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  INPUT DIALOG
        // ════════════════════════════════════════════════════════════════════
        private string? InputDialog(string title, string prompt, string def = "")
        {
            using Form dlg = new()
            {
                Text = title,
                Width = 440,
                Height = 170,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary
            };
            var lbl = new Label { Text = prompt, Left = 14, Top = 20, Width = 400, ForeColor = Theme.TextSecondary, Font = Theme.FontBody };
            var txt = Theme.MakeTextBox(); txt.Text = def; txt.Left = 14; txt.Top = 48; txt.Width = 400; txt.SelectAll();
            var ok = Theme.MakeButton("Aceptar", 90, Theme.ButtonKind.Primary); ok.Left = 218; ok.Top = 92; ok.DialogResult = DialogResult.OK;
            var cancel = Theme.MakeButton("Cancelar", 90); cancel.Left = 318; cancel.Top = 92; cancel.DialogResult = DialogResult.Cancel;
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = (IButtonControl)ok;
            dlg.CancelButton = (IButtonControl)cancel;
            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private string IconKey(string ext)
        {
            ext = ext.ToLower();
            if (new[] { ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".tiff", ".tif", ".ico", ".webp", ".avif", ".heic", ".svg", ".raw", ".cr2", ".nef", ".arw", ".dng" }.Contains(ext)) return "image";
            if (new[] { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg", ".opus", ".aiff" }.Contains(ext)) return "audio";
            if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".ts", ".m4v", ".3gp" }.Contains(ext)) return "video";
            if (new[] { ".txt", ".csv", ".json", ".xml", ".log", ".ini", ".config", ".md", ".cs", ".py", ".js", ".html", ".css" }.Contains(ext)) return "text";
            return "file";
        }

        private string FileTypeName(string ext)
        {
            ext = ext.ToLower();
            var map = new Dictionary<string, string>
            {
                {".txt","Texto"},{".csv","CSV"},{".json","JSON"},{".xml","XML"},{".md","Markdown"},{".log","Log"},
                {".cs","C#"},{".py","Python"},{".js","JavaScript"},{".html","HTML"},{".css","CSS"},
                {".jpg","JPEG"},{".jpeg","JPEG"},{".png","PNG"},{".gif","GIF"},{".bmp","BMP"},{".svg","SVG"},{".webp","WebP"},{".ico","Icono"},
                {".mp3","MP3"},{".wav","WAV"},{".flac","FLAC"},{".aac","AAC"},{".m4a","M4A"},{".ogg","OGG"},
                {".mp4","MP4"},{".avi","AVI"},{".mkv","MKV"},{".mov","MOV"},{".wmv","WMV"},{".webm","WebM"},
                {".pdf","PDF"},{".doc","Word"},{".docx","Word"},{".xls","Excel"},{".xlsx","Excel"},{".ppt","PowerPoint"},{".pptx","PowerPoint"},
                {".zip","ZIP"},{".rar","RAR"},{".7z","7-Zip"},{".exe","Ejecutable"},{".msi","Instalador"},
            };
            return map.TryGetValue(ext, out var t) ? t : "Archivo";
        }

        private static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        // ── Íconos ───────────────────────────────────────────────────────────
        private static Icon MakeFolderIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(251, 191, 36));
            g.FillRectangle(b, 4, 12, 24, 16);
            g.FillPolygon(b, new[] { new Point(4, 12), new Point(10, 8), new Point(16, 12) });
            return Icon.FromHandle(bmp.GetHicon());
        }
        private static Icon MakeFileIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var body = new SolidBrush(Color.FromArgb(90, 96, 128));
            using var fold = new SolidBrush(Color.FromArgb(167, 139, 250));
            g.FillRectangle(body, 8, 4, 16, 24);
            g.FillPolygon(fold, new[] { new Point(24, 4), new Point(24, 10), new Point(18, 4) });
            return Icon.FromHandle(bmp.GetHicon());
        }
        private static Icon MakeImageIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(12, 44, 78));
            using var sun = new SolidBrush(Color.FromArgb(251, 191, 36));
            using var mnt = new SolidBrush(Color.FromArgb(96, 165, 250));
            g.FillRectangle(bg, 6, 6, 20, 20);
            g.FillEllipse(sun, 10, 9, 6, 6);
            g.FillPolygon(mnt, new[] { new Point(6, 26), new Point(12, 17), new Point(18, 22), new Point(26, 26) });
            return Icon.FromHandle(bmp.GetHicon());
        }
        private static Icon MakeAudioIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.FillEllipse(b, 8, 18, 8, 8);
            g.FillRectangle(b, 14, 8, 2, 14);
            g.FillEllipse(b, 16, 8, 6, 6);
            return Icon.FromHandle(bmp.GetHicon());
        }
        private static Icon MakeVideoIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(72, 24, 60));
            using var play = new SolidBrush(Color.FromArgb(244, 114, 182));
            g.FillRectangle(bg, 6, 10, 14, 12);
            g.FillPolygon(play, new[] { new Point(20, 13), new Point(26, 16), new Point(20, 19) });
            return Icon.FromHandle(bmp.GetHicon());
        }
        private static Icon MakeTextIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var page = new SolidBrush(Color.FromArgb(28, 32, 48));
            g.FillRectangle(page, 8, 4, 16, 24);
            using var pen = new Pen(Color.FromArgb(124, 111, 247), 2);
            g.DrawLine(pen, 11, 10, 21, 10);
            g.DrawLine(pen, 11, 14, 21, 14);
            g.DrawLine(pen, 11, 18, 21, 18);
            g.DrawLine(pen, 11, 22, 18, 22);
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MENU RENDERER MINIMALISTA (Arctic Night)
    // ════════════════════════════════════════════════════════════════════════
    internal class MinimalMenuRenderer : ToolStripProfessionalRenderer
    {
        public MinimalMenuRenderer() : base(new MinimalColorTable()) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            => e.Graphics.FillRectangle(new SolidBrush(e.Item.Selected ? Theme.BgHover : Theme.BgElevated), new Rectangle(Point.Empty, e.Item.Size));
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(new Pen(Color.FromArgb(255, 255, 255, 14)), 8, y, e.Item.Width - 8, y);
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.FillRectangle(new SolidBrush(Theme.BgElevated), e.AffectedBounds);
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            => e.Graphics.DrawRectangle(new Pen(Color.FromArgb(255, 255, 255, 15)), new Rectangle(e.AffectedBounds.X, e.AffectedBounds.Y, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1));
    }

    internal class MinimalColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.BgHover;
        public override Color MenuItemBorder => Color.FromArgb(255, 255, 255, 15);
        public override Color MenuBorder => Color.FromArgb(255, 255, 255, 15);
        public override Color ToolStripDropDownBackground => Theme.BgElevated;
        public override Color ImageMarginGradientBegin => Theme.BgSurface;
        public override Color ImageMarginGradientMiddle => Theme.BgSurface;
        public override Color ImageMarginGradientEnd => Theme.BgSurface;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COMPARADOR PARA LISTVIEW
    // ════════════════════════════════════════════════════════════════════════
    internal class LvComparer : System.Collections.IComparer
    {
        private readonly int col;
        private readonly SortOrder order;
        public LvComparer(int col, SortOrder order) { this.col = col; this.order = order; }
        public int Compare(object? x, object? y)
        {
            int r = string.Compare(((ListViewItem)x!).SubItems[col].Text, ((ListViewItem)y!).SubItems[col].Text, StringComparison.CurrentCulture);
            return order == SortOrder.Descending ? -r : r;
        }
    }
}