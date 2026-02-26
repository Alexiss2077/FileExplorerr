using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  TEMA
    // ════════════════════════════════════════════════════════════════════════
    internal static class Theme
    {
        public static readonly Color BgDeep = Color.FromArgb(10, 14, 20);
        public static readonly Color BgSurface = Color.FromArgb(17, 23, 33);
        public static readonly Color BgRaised = Color.FromArgb(24, 32, 46);

        public static readonly Color AccentBlue = Color.FromArgb(56, 139, 253);
        public static readonly Color AccentBlueDark = Color.FromArgb(31, 90, 180);
        public static readonly Color AccentBlueHover = Color.FromArgb(80, 160, 255);
        public static readonly Color AccentGreen = Color.FromArgb(35, 134, 54);
        public static readonly Color AccentGreenDark = Color.FromArgb(22, 100, 40);

        public static readonly Color Border = Color.FromArgb(38, 50, 70);
        public static readonly Color BorderSoft = Color.FromArgb(30, 42, 60);

        public static readonly Color TextPrimary = Color.FromArgb(220, 232, 248);
        public static readonly Color TextSecondary = Color.FromArgb(110, 140, 180);
        public static readonly Color TextOnAccent = Color.White;

        public static readonly Color DragFolder = Color.FromArgb(28, 60, 110);
        public static readonly Color RecycleBg = Color.FromArgb(20, 28, 42);
        public static readonly Color RecycleHot = Color.FromArgb(120, 20, 20);
    }

    public partial class Form1 : Form
    {
        // ── Estado 
        private string currentPath = "";
        private Stack<string> navigationHistory = new();
        private ListViewItem? dragHighlightedItem;
        private int sortColumn = -1;
        private PictureBox recycleIconBox = null!;




        /// ///////////////////////////

        // ── P/Invoke 
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        private Icon GetRecycleBinIcon(bool full = false)
        {
            try
            {
                string shell32 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, shell32, full ? 32 : 31);
                if (hIcon != IntPtr.Zero) return Icon.FromHandle(hIcon);
            }
            catch { }
            return SystemIcons.WinLogo;
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

        private bool SendToRecycleBin(string path)  // Envía el archivo/carpeta a la papelera usando SHFileOperation
        {
            try
            {
                var op = new SHFILEOPSTRUCT
                {
                    hwnd = this.Handle,
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
            navigationHistory = new Stack<string>();
            NavigateToPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE LA INTERFAZ
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            this.BackColor = Theme.BgDeep;

            //ImageList 
            imageList = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            imageList.Images.Add("folder", MakeFolderIcon());
            imageList.Images.Add("file", MakeFileIcon());
            imageList.Images.Add("image", MakeImageIcon());
            imageList.Images.Add("audio", MakeAudioIcon());
            imageList.Images.Add("video", MakeVideoIcon());
            imageList.Images.Add("text", MakeTextIcon());

            //        Botón Atrás 
            backButton = new Button
            {
                Text = "◄",
                Location = new Point(10, 12),
                Size = new Size(36, 30),
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            backButton.FlatAppearance.BorderColor = Theme.Border;
            backButton.Click += (s, e) => GoBack();

            // ── Botón Subir 
            upButton = new Button
            {
                Text = "▲",
                Location = new Point(50, 12),
                Size = new Size(36, 30),
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            upButton.FlatAppearance.BorderColor = Theme.Border;
            upButton.Click += (s, e) => GoUp();

            // ── Barra de dirección 
            addressBar = new TextBox
            {
                Location = new Point(92, 13),
                Size = new Size(560, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            addressBar.KeyDown += AddressBar_KeyDown;

            //Panel superior 
            Panel topPanel = new Panel
            {
                Height = 55,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface
            };
            topPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1),
                    0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);

            // Panel derecho con botones agrupados
            var rightPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 12, 10, 0)
            };

            // Botón Nueva Carpeta 
            newFolderButton = new Button
            {
                Text = "📁  Nueva carpeta",
                Size = new Size(144, 30),
                BackColor = Theme.AccentBlueDark,
                ForeColor = Theme.TextOnAccent,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            newFolderButton.FlatAppearance.BorderColor = Theme.AccentBlue;
            newFolderButton.Click += (s, e) => CreateFolder();

            //            Botón Refresh
            refreshButton = new Button
            {
                Text = "⟳",
                Size = new Size(36, 30),
                BackColor = Theme.BgRaised,
                ForeColor = Theme.AccentBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            refreshButton.FlatAppearance.BorderColor = Theme.Border;
            refreshButton.Click += (s, e) => RefreshView();
            new ToolTip().SetToolTip(refreshButton, "Actualizar directorio (F5)");

            // Botón Exportar CSV   
            exportCsvButton = new Button
            {
                Text = "📊  Exportar CSV",
                Size = new Size(140, 30),
                BackColor = Theme.AccentGreenDark,
                ForeColor = Theme.TextOnAccent,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            exportCsvButton.FlatAppearance.BorderColor = Theme.AccentGreen;
            exportCsvButton.Click += async (s, e) => await ExportCsvAsync();

            rightPanel.Controls.Add(newFolderButton);
            rightPanel.Controls.Add(refreshButton);
            rightPanel.Controls.Add(exportCsvButton);

            // Panel izquierdo: botones nav + barra de dirección
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            backButton.Location = new Point(10, 12);
            upButton.Location = new Point(50, 12);

            addressBar.Location = new Point(92, 13);
            addressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            addressBar.Size = new Size(Math.Max(100, leftPanel.Width - 100), 28);

            leftPanel.Controls.Add(backButton);
            leftPanel.Controls.Add(upButton);
            leftPanel.Controls.Add(addressBar);

            leftPanel.Resize += (s, e) =>
                addressBar.Width = Math.Max(100, leftPanel.Width - 100);

            topPanel.Controls.Add(leftPanel);
            topPanel.Controls.Add(rightPanel);

            // ── ListView 
            listView = new DarkListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                SmallImageList = imageList,
                LargeImageList = imageList,
                AllowDrop = true
            };
            listView.Columns.Add("Nombre", 350);
            listView.Columns.Add("Tipo", 120);
            listView.Columns.Add("Tamaño", 100);
            listView.Columns.Add("Contenido / Info", 280);
            listView.Columns.Add("Fecha modificación", 150);

            // Header personalizado
            listView.OwnerDraw = true;
            listView.DrawColumnHeader += (s, e) =>
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(17, 23, 33));
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

                using var linePen = new Pen(Color.FromArgb(56, 139, 253), 1);
                e.Graphics.DrawLine(linePen,
                    e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);

                using var sepPen = new Pen(Color.FromArgb(38, 50, 70), 1);
                e.Graphics.DrawLine(sepPen,
                    e.Bounds.Right - 1, e.Bounds.Top + 4,
                    e.Bounds.Right - 1, e.Bounds.Bottom - 4);

                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var textRect = new Rectangle(
                    e.Bounds.Left + 10, e.Bounds.Top,
                    e.Bounds.Width - 14, e.Bounds.Height);
                using var textBrush = new SolidBrush(Color.FromArgb(110, 160, 210));
                e.Graphics.DrawString(e.Header.Text,
                    new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    textBrush, textRect, sf);
            };
            listView.DrawItem += (s, e) => e.DrawDefault = true;
            listView.DrawSubItem += (s, e) => e.DrawDefault = true;

            listView.DoubleClick += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag!.ToString()!); };
            listView.ColumnClick += ListView_ColumnClick;
            listView.ItemDrag += ListView_ItemDrag;
            listView.DragEnter += ListView_DragEnter;
            listView.DragOver += ListView_DragOver;
            listView.DragDrop += ListView_DragDrop;
            listView.DragLeave += (s, e) => ClearDragHighlight();
            listView.MouseClick += ListView_MouseClick;

            // Atajo F5 para refresh
            listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };

            //            Menú contextual 
            BuildContextMenu();
            listView.ContextMenuStrip = contextMenu;

            //             Label estado 
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(190, 210, 240),
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI Emoji", 11F)
            };

            //                 Panel papelera 
            recycleIconBox = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Padding = new Padding(7),
                AllowDrop = true,
                Image = GetRecycleBinIcon(false).ToBitmap()
            };
            recycleIconBox.DragEnter += (s, e) => RecycleDragEnter(e);
            recycleIconBox.DragOver += (s, e) => RecycleDragOver(e);
            recycleIconBox.DragLeave += (s, e) => RecycleDragLeave();
            recycleIconBox.DragDrop += (s, e) => RecycleDragDrop(e);

            recyclePanelLabel = new Label
            {
                Text = "Arrastrar para eliminar",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextSecondary,
                Font = new Font("Segoe UI", 8.5F),
                AllowDrop = true
            };
            recyclePanelLabel.DragEnter += (s, e) => RecycleDragEnter(e);
            recyclePanelLabel.DragOver += (s, e) => RecycleDragOver(e);
            recyclePanelLabel.DragLeave += (s, e) => RecycleDragLeave();
            recyclePanelLabel.DragDrop += (s, e) => RecycleDragDrop(e);

            recycleDropPanel = new Panel
            {
                Width = 230,
                Dock = DockStyle.Right,
                BackColor = Theme.RecycleBg,
                Cursor = Cursors.Hand,
                AllowDrop = true
            };
            recycleDropPanel.Controls.Add(recyclePanelLabel);
            recycleDropPanel.Controls.Add(recycleIconBox);
            recycleDropPanel.DragEnter += (s, e) => RecycleDragEnter(e);
            recycleDropPanel.DragOver += (s, e) => RecycleDragOver(e);
            recycleDropPanel.DragLeave += (s, e) => RecycleDragLeave();
            recycleDropPanel.DragDrop += (s, e) => RecycleDragDrop(e);
            recycleDropPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1), 0, 0, 0, recycleDropPanel.Height);

            Panel bottomPanel = new Panel
            {
                Height = 54,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface
            };
            bottomPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1), 0, 0, bottomPanel.Width, 0);
            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(recycleDropPanel);

            // Panel derecho: Buscar Carpeta 
            rightInfoPanel = new Panel
            {
                Width = 320,
                Dock = DockStyle.Right,
                BackColor = Theme.BgSurface,
                Padding = new Padding(0)
            };
            rightInfoPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1), 0, 0, 0, rightInfoPanel.Height);

            // Header del panel derecho
            var rightHeader = new Panel
            {
                Height = 48,
                Dock = DockStyle.Top,
                BackColor = Theme.BgRaised,
                Padding = new Padding(12, 0, 0, 0)
            };
            rightHeader.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1),
                    0, rightHeader.Height - 1, rightHeader.Width, rightHeader.Height - 1);

            folderNameLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Buscar Carpeta",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.AccentBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            rightHeader.Controls.Add(folderNameLabel);

            // Barra de búsqueda
            var searchPanel = new Panel
            {
                Height = 42,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(8, 6, 8, 6)
            };
            searchPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Theme.Border, 1),
                    0, searchPanel.Height - 1, searchPanel.Width, searchPanel.Height - 1);

            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "Buscar en carpetas..."
            };

            var searchBtn = new Button
            {
                Text = "Buscar",
                Dock = DockStyle.Right,
                Width = 70,
                BackColor = Theme.AccentBlueDark,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            searchBtn.FlatAppearance.BorderColor = Theme.AccentBlue;
            searchBtn.Click += (s, e) => SearchInPanel(searchBox.Text);
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SearchInPanel(searchBox.Text); };

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(searchBtn);

            // Área de contenido — TreeView expandib
            infoTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                HotTracking = true,
                Scrollable = true,
                Indent = 16,
                ItemHeight = 22
            };
            infoTree.BeforeExpand += InfoTree_BeforeExpand;
            infoTree.NodeMouseDoubleClick += InfoTree_NodeDoubleClick;
            infoTree.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            infoTree.DrawNode += InfoTree_DrawNode;

            rightInfoPanel.Controls.Add(infoTree);
            rightInfoPanel.Controls.Add(searchPanel);
            rightInfoPanel.Controls.Add(rightHeader);

            this.Controls.Add(listView);
            this.Controls.Add(rightInfoPanel);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PANEL DERECHO — TREEVIEW EXPANDIBLE
        // ════════════════════════════════════════════════════════════════════

        // Categorías de archivos para clasificar el contenido del directorio. La última categoría "Otros" es un comodín para archivos que no encajen en las anteriores.
        private static readonly (string Label, string[] Exts, string Emoji)[] FileGroups =
        {
            ("Imágenes",     new[]{".jpg",".jpeg",".png",".gif",".bmp",".ico",".webp",".tiff"}, "🖼️"),
            ("Audio",        new[]{".mp3",".wav",".wma",".m4a",".flac",".aac",".ogg"},          "🎵"),
            ("Video",        new[]{".mp4",".avi",".mkv",".mov",".wmv",".flv",".webm"},          "🎬"),
            ("Texto/Código", new[]{".txt",".json",".xml",".csv",".log",".ini",".md",
                                   ".cs",".py",".js",".ts",".html",".css",".config"},           "📝"),
            ("Documentos",   new[]{".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf"},       "📄"),
            ("Otros",        Array.Empty<string>(),                                             "📦"),
        };

        //            Actualizar panel con contenido del directorio actual 
        private void UpdateRightPanel(string path)
        {
            if (infoTree == null) return;
            infoTree.BeginUpdate();
            infoTree.Nodes.Clear();
            folderNameLabel.Text = new DirectoryInfo(path).Name;
            try
            {
                var di = new DirectoryInfo(path);
                var subdirs = di.GetDirectories()
                                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                .OrderBy(d => d.Name).ToArray();
                var files = di.GetFiles()
                                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                .ToArray();

                // Nodo carpetas
                if (subdirs.Length > 0)
                {
                    var foldersNode = MakeGroupNode("📁 Carpetas", subdirs.Length, NodeKind.Header);
                    foreach (var d in subdirs)
                    {
                        var dn = MakeFolderNode(d.Name, d.FullName);
                        PopulateFolderNodeDummy(dn, d.FullName);
                        foldersNode.Nodes.Add(dn);
                    }
                    foldersNode.Expand();
                    infoTree.Nodes.Add(foldersNode);
                }

                // Nodos por categoría
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (label, exts, emoji) in FileGroups)
                {
                    FileInfo[] matched = exts.Length == 0
                        ? files.Where(f => !used.Contains(f.FullName)).ToArray()
                        : files.Where(f => exts.Contains(f.Extension.ToLower()) && !used.Contains(f.FullName)).ToArray();

                    if (matched.Length == 0) continue;
                    foreach (var f in matched) used.Add(f.FullName);

                    var grp = MakeGroupNode(emoji + " " + label, matched.Length, NodeKind.Category);
                    foreach (var f in matched.OrderBy(x => x.Name))
                        grp.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                    infoTree.Nodes.Add(grp);
                }

                if (infoTree.Nodes.Count == 0)
                    infoTree.Nodes.Add(MakeDimNode("(carpeta vacía)"));
            }
            catch
            {
                infoTree.Nodes.Add(MakeDimNode("Sin acceso"));
            }
            infoTree.EndUpdate();
        }

        //  Búsqueda expandible 
        private void SearchInPanel(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { UpdateRightPanel(currentPath); return; }
            infoTree.BeginUpdate();
            infoTree.Nodes.Clear();
            query = query.Trim();
            folderNameLabel.Text = "\"" + query + "\"";
            try
            {
                var di = new DirectoryInfo(currentPath);
                var dirs = di.GetDirectories("*", SearchOption.AllDirectories)
                             .Where(d => (d.Attributes & FileAttributes.Hidden) == 0 &&
                                    d.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                             .OrderBy(d => d.Name).ToArray();
                var files = di.GetFiles("*", SearchOption.AllDirectories)
                              .Where(f => (f.Attributes & FileAttributes.Hidden) == 0 &&
                                     f.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                              .OrderBy(f => f.Name).ToArray();

                if (dirs.Length == 0 && files.Length == 0)
                {
                    infoTree.Nodes.Add(MakeDimNode("Sin resultados"));
                    infoTree.EndUpdate();
                    return;
                }

                // Carpetas encontradas — cada una es expandible para ver su contenido
                if (dirs.Length > 0)
                {
                    var rootNode = MakeGroupNode("📁 Carpetas encontradas", dirs.Length, NodeKind.Header);
                    foreach (var d in dirs)
                    {
                        string rel = d.FullName.Length > currentPath.Length
                            ? d.FullName.Substring(currentPath.Length).TrimStart(Path.DirectorySeparatorChar)
                            : d.Name;
                        var dn = MakeFolderNode(rel, d.FullName);
                        PopulateFolderNodeDummy(dn, d.FullName);
                        rootNode.Nodes.Add(dn);
                    }
                    rootNode.Expand();
                    infoTree.Nodes.Add(rootNode);
                }

                // Archivos encontrados
                if (files.Length > 0)
                {
                    var rootNode = MakeGroupNode("📄 Archivos encontrados", files.Length, NodeKind.Header);
                    foreach (var f in files)
                    {
                        string rel = f.FullName.Length > currentPath.Length
                            ? f.FullName.Substring(currentPath.Length).TrimStart(Path.DirectorySeparatorChar)
                            : f.Name;
                        rootNode.Nodes.Add(MakeFileNode(rel, f.FullName));
                    }
                    rootNode.Expand();
                    infoTree.Nodes.Add(rootNode);
                }
            }
            catch (Exception ex)
            {
                infoTree.Nodes.Add(MakeDimNode("Error: " + ex.Message));
            }
            infoTree.EndUpdate();
        }

        //  agregar dummy para que aparezca el [+] 
        private void PopulateFolderNodeDummy(TreeNode node, string folderPath)
        {
            try
            {
                var di = new DirectoryInfo(folderPath);
                bool hasChildren =
                    di.GetDirectories().Any(d => (d.Attributes & FileAttributes.Hidden) == 0) ||
                    di.GetFiles().Any(f => (f.Attributes & FileAttributes.Hidden) == 0);
                if (hasChildren)
                    node.Nodes.Add(new TreeNode("__dummy__") { Tag = new NodeTag(NodeKind.Dim, "__dummy__") });
            }
            catch { }
        }

        //           Expandir nodo carpeta con contenido real 
        private void InfoTree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node?.Tag is not NodeTag nt || nt.Kind != NodeKind.Folder || nt.Path == null) return;
            string folderPath = nt.Path;
            if (node.Nodes.Count == 1 && node.Nodes[0].Tag is NodeTag dt && dt.Path == "__dummy__")
            {
                infoTree.BeginUpdate();
                node.Nodes.Clear();
                try
                {
                    var di = new DirectoryInfo(folderPath);
                    var subdirs = di.GetDirectories()
                                    .Where(x => (x.Attributes & FileAttributes.Hidden) == 0)
                                    .OrderBy(x => x.Name).ToArray();
                    var files = di.GetFiles()
                                    .Where(x => (x.Attributes & FileAttributes.Hidden) == 0)
                                    .OrderBy(x => x.Name).ToArray();

                    // Subcarpetas
                    foreach (var sub in subdirs)
                    {
                        var sn = MakeFolderNode(sub.Name, sub.FullName);
                        PopulateFolderNodeDummy(sn, sub.FullName);
                        node.Nodes.Add(sn);
                    }

                    // Archivos por categoría
                    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (label, exts, emoji) in FileGroups)
                    {
                        FileInfo[] matched = exts.Length == 0
                            ? files.Where(f => !used.Contains(f.FullName)).ToArray()
                            : files.Where(f => exts.Contains(f.Extension.ToLower()) && !used.Contains(f.FullName)).ToArray();
                        if (matched.Length == 0) continue;
                        foreach (var f in matched) used.Add(f.FullName);
                        var grp = MakeGroupNode(emoji + " " + label, matched.Length, NodeKind.Category);
                        foreach (var f in matched)
                            grp.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                        node.Nodes.Add(grp);
                    }

                    if (node.Nodes.Count == 0)
                        node.Nodes.Add(MakeDimNode("(vacía)"));
                }
                catch { node.Nodes.Add(MakeDimNode("Sin acceso")); }
                infoTree.EndUpdate();
            }
        }

        //      Doble clic: navegar a carpeta o abrir archivo 
        private void InfoTree_NodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is NodeTag nt && nt.Path != null && nt.Path != "__dummy__")
            {
                if (Directory.Exists(nt.Path)) NavigateToPath(nt.Path);
                else if (File.Exists(nt.Path)) OpenEntry(nt.Path);
            }
        }

        //     Tag compuesto: tipo + ruta 
        private enum NodeKind { Header, Category, Folder, File, Dim }

        private record NodeTag(NodeKind Kind, string? Path = null);

        //       OwnerDraw: colorear nodos según tipo 
        private void InfoTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null) return;

            NodeKind kind = e.Node.Tag is NodeTag nt ? nt.Kind : NodeKind.Dim;

            Color fg = kind switch
            {
                NodeKind.Header => Color.FromArgb(56, 139, 253),   // azul acento
                NodeKind.Category => Color.FromArgb(140, 180, 255),   // azul claro
                NodeKind.Folder => Color.FromArgb(255, 210, 80),    // amarillo
                NodeKind.File => Color.FromArgb(220, 232, 248),   // blanco suave
                _ => Color.FromArgb(110, 140, 180)    // gris
            };

            bool selected = (e.State & TreeNodeStates.Selected) != 0;

            // Fondo de toda la fila
            Rectangle rowRect = new Rectangle(0, e.Bounds.Top, infoTree.Width, e.Bounds.Height);
            using var bgBrush = new SolidBrush(selected ? Color.FromArgb(31, 90, 180) : Color.FromArgb(17, 23, 33));
            e.Graphics.FillRectangle(bgBrush, rowRect);

            // Dibujar líneas de conexión del TreeView
            e.DrawDefault = false;

            // Calcular indent
            int indent = (e.Node.Level + 1) * infoTree.Indent;
            int textX = indent + 4;
            int textY = e.Bounds.Top + (e.Bounds.Height - 15) / 2;

            // Botón +/- si tiene hijos
            if (e.Node.Nodes.Count > 0 || (e.Node.Tag is NodeTag nt2 && nt2.Kind == NodeKind.Folder))
            {
                int btnX = indent - 14;
                int btnY = e.Bounds.Top + (e.Bounds.Height - 10) / 2;
                Rectangle btnRect = new Rectangle(btnX, btnY, 10, 10);
                using var btnPen = new Pen(Color.FromArgb(56, 139, 253));
                e.Graphics.DrawRectangle(btnPen, btnRect);
                using var signBrush = new SolidBrush(Color.FromArgb(56, 139, 253));
                // línea horizontal siempre
                e.Graphics.FillRectangle(signBrush, btnX + 2, btnY + 4, 6, 1);
                // línea vertical solo si collapsed
                if (!e.Node.IsExpanded)
                    e.Graphics.FillRectangle(signBrush, btnX + 4, btnY + 2, 1, 6);
            }

            FontStyle fs = kind == NodeKind.Header ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font("Segoe UI", 8.5F, fs);
            using var brush = new SolidBrush(fg);
            e.Graphics.DrawString(e.Node.Text, font, brush, textX, textY);
        }

        //       Helpers para crear nodos 
        private static TreeNode MakeGroupNode(string label, int count, NodeKind kind)
        {
            string txt = count > 0 ? $"{label}  ({count})" : label;
            return new TreeNode(txt) { Tag = new NodeTag(kind) };
        }

        private static TreeNode MakeFolderNode(string name, string fullPath) =>
            new TreeNode("📁 " + name) { Tag = new NodeTag(NodeKind.Folder, fullPath) };

        private static TreeNode MakeFileNode(string name, string fullPath) =>
            new TreeNode("  * " + name) { Tag = new NodeTag(NodeKind.File, fullPath) };

        private static TreeNode MakeDimNode(string text) =>
            new TreeNode("  " + text) { Tag = new NodeTag(NodeKind.Dim) };










        // ════════════════════════════════════════════════════════════════════
        //  REFRESH
        // ════════════════════════════════════════════════════════════════════
        private void RefreshView()
        {
            if (!string.IsNullOrEmpty(currentPath))
                LoadDirectory(currentPath);
        }






        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR CSV
        // ════════════════════════════════════════════════════════════════════
        private async Task ExportCsvAsync()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Guardar índice CSV",
                Filter = "Archivo CSV (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"indice_{Path.GetFileName(currentPath)}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                InitialDirectory = currentPath
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // Bloquear botón y mostrar progreso
            exportCsvButton.Enabled = false;
            exportCsvButton.Text = "⏳  Generando…";
            string savedPath = dlg.FileName;

            var progress = new Progress<string>(folder =>
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)(() =>
                        statusLabel.Text = $"  Indexando: {folder}"));
            });

            try
            {
                string csv = await CsvIndexer.GenerateAsync(currentPath, progress);
                await File.WriteAllTextAsync(savedPath, csv, System.Text.Encoding.UTF8);

                statusLabel.Text = $"  ✔  Índice exportado → {Path.GetFileName(savedPath)}";

                // Preguntar si abrir el archivo
                if (MessageBox.Show(
                        $"Índice CSV generado correctamente:\n{savedPath}\n\n¿Abrir el archivo ahora?",
                        "Exportación completada",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo { FileName = savedPath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                exportCsvButton.Enabled = true;
                exportCsvButton.Text = "📊  Exportar CSV";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MENÚ CONTEXTUAL
        // ════════════════════════════════════════════════════════════════════
        private void BuildContextMenu()
        {
            contextMenu = new ContextMenuStrip { Font = new Font("Segoe UI", 9F) };
            contextMenu.BackColor = Theme.BgRaised;
            contextMenu.ForeColor = Theme.TextPrimary;
            contextMenu.Renderer = new DarkMenuRenderer();

            var miOpen = new ToolStripMenuItem("📂  Abrir") { ForeColor = Theme.TextPrimary };
            var miSep1 = new ToolStripSeparator();
            var miNewFolder = new ToolStripMenuItem("📁  Nueva carpeta") { ForeColor = Theme.TextPrimary };
            var miSep2 = new ToolStripSeparator();
            var miRename = new ToolStripMenuItem("✏️  Renombrar") { ForeColor = Theme.TextPrimary };
            var miDelete = new ToolStripMenuItem("🗑️  Enviar a la papelera") { ForeColor = Color.FromArgb(255, 100, 100) };
            var miSep3 = new ToolStripSeparator();
            var miRefresh = new ToolStripMenuItem("⟳  Actualizar (F5)") { ForeColor = Theme.AccentBlue };

            miOpen.Click += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag!.ToString()!); };
            miNewFolder.Click += (s, e) => CreateFolder();
            miRename.Click += (s, e) => RenameSelected();
            miDelete.Click += (s, e) => DeleteSelected();
            miRefresh.Click += (s, e) => RefreshView();

            contextMenu.Items.AddRange(new ToolStripItem[]
                { miOpen, miSep1, miNewFolder, miSep2, miRename, miDelete, miSep3, miRefresh });
        }

        private void ListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            bool sel = listView.SelectedItems.Count > 0;
            contextMenu.Items[0].Visible = sel;   // Abrir
            contextMenu.Items[1].Visible = sel;   // sep1
            contextMenu.Items[3].Visible = sel;   // sep2
            contextMenu.Items[4].Visible = sel;   // Renombrar
            contextMenu.Items[5].Visible = sel;   // Eliminar
        }

        // ════════════════════════════════════════════════════════════════════
        //  CREAR CARPETA
        // ════════════════════════════════════════════════════════════════════
        private void CreateFolder()
        {
            string? name = InputDialog("Nueva carpeta", "Nombre:", "Nueva carpeta");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("El nombre contiene caracteres no válidos.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newDir = Path.Combine(currentPath, name);
            if (Directory.Exists(newDir))
            {
                MessageBox.Show("Ya existe una carpeta con ese nombre.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(newDir);
                LoadDirectory(currentPath);
                BeginInvoke((Action)(() =>
                {
                    foreach (ListViewItem item in listView.Items)
                        if (item.Tag!.ToString() == newDir)
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                            break;
                        }
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RENOMBRAR
        // ════════════════════════════════════════════════════════════════════
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error al renombrar: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ELIMINAR
        // ════════════════════════════════════════════════════════════════════
        private void DeleteSelected()
        {
            if (listView.SelectedItems.Count == 0) return;

            string[] paths = listView.SelectedItems.Cast<ListViewItem>()
                             .Select(i => i.Tag!.ToString()!).ToArray();

            string msg = paths.Length == 1
                ? $"¿Enviar \"{Path.GetFileName(paths[0])}\" a la papelera?"
                : $"¿Enviar {paths.Length} elementos a la papelera?";

            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (string p in paths) SendToRecycleBin(p);
            LoadDirectory(currentPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DRAG SOURCE
        // ════════════════════════════════════════════════════════════════════
        private void ListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            string[] paths = listView.SelectedItems.Cast<ListViewItem>()
                             .Select(i => i.Tag!.ToString()!).ToArray();
            if (paths.Length == 0) return;
            listView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths),
                DragDropEffects.Move | DragDropEffects.Copy);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DROP EN CARPETA
        // ════════════════════════════════════════════════════════════════════
        private void ListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
                       ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.None; return; }

            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            ListViewItem? hovered = listView.GetItemAt(pt.X, pt.Y);
            string[] dragged = (string[])e.Data.GetData(DataFormats.FileDrop)!;

            if (dragHighlightedItem != null && dragHighlightedItem != hovered)
            {
                dragHighlightedItem.BackColor = Theme.BgDeep;
                dragHighlightedItem.ForeColor = Theme.TextPrimary;
                dragHighlightedItem = null;
            }

            bool valid = hovered != null
                      && Directory.Exists(hovered.Tag!.ToString())
                      && !dragged.Contains(hovered.Tag!.ToString());

            if (valid)
            {
                e.Effect = DragDropEffects.Move;
                hovered!.BackColor = Theme.DragFolder;
                hovered.ForeColor = Theme.AccentBlue;
                dragHighlightedItem = hovered;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ListView_DragDrop(object sender, DragEventArgs e)
        {
            ClearDragHighlight();
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;

            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            ListViewItem? target = listView.GetItemAt(pt.X, pt.Y);
            if (target == null || !Directory.Exists(target.Tag!.ToString())) return;

            MoveItems((string[])e.Data.GetData(DataFormats.FileDrop)!, target.Tag!.ToString()!);
        }

        private void ClearDragHighlight()
        {
            if (dragHighlightedItem == null) return;
            dragHighlightedItem.BackColor = Theme.BgDeep;
            dragHighlightedItem.ForeColor = Theme.TextPrimary;
            dragHighlightedItem = null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  DROP EN PAPELERA
        // ════════════════════════════════════════════════════════════════════
        private void RecycleDragEnter(DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effect = DragDropEffects.Move;
            recycleDropPanel.BackColor = Theme.RecycleHot;
            recycleIconBox.Image = GetRecycleBinIcon(true).ToBitmap();
            recyclePanelLabel.ForeColor = Color.White;
            recyclePanelLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            recyclePanelLabel.Text = "Soltar para eliminar";
        }

        private void RecycleDragOver(DragEventArgs e)
        {
            e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
                       ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void RecycleDragLeave()
        {
            recycleDropPanel.BackColor = Theme.RecycleBg;
            recycleIconBox.Image = GetRecycleBinIcon(false).ToBitmap();
            recyclePanelLabel.ForeColor = Theme.TextSecondary;
            recyclePanelLabel.Font = new Font("Segoe UI", 8.5F);
            recyclePanelLabel.Text = "Arrastrar para eliminar";
        }

        private void RecycleDragDrop(DragEventArgs e)
        {
            RecycleDragLeave();
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;

            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (paths.Length == 0) return;

            string msg = paths.Length == 1
                ? $"¿Enviar \"{Path.GetFileName(paths[0])}\" a la papelera?"
                : $"¿Enviar {paths.Length} elementos a la papelera?";

            if (MessageBox.Show(msg, "Confirmar eliminación",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            int err = 0;
            foreach (string p in paths) if (!SendToRecycleBin(p)) err++;
            if (err > 0) MessageBox.Show($"{err} elemento(s) no pudieron eliminarse.",
                "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            LoadDirectory(currentPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOVER ELEMENTOS
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
                            var r = MessageBox.Show($"Ya existe \"{name}\". ¿Sobreescribir?",
                                "Conflicto", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (r == DialogResult.Cancel) return;
                            if (r == DialogResult.No) continue;
                            File.Delete(dest);
                        }
                        File.Move(src, dest);
                    }
                    else if (Directory.Exists(src))
                    {
                        if (targetDir.StartsWith(src + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show($"No se puede mover \"{name}\" dentro de sí misma.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                        Directory.Move(src, dest);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al mover \"{Path.GetFileName(src)}\": {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            LoadDirectory(currentPath);
            statusLabel.Text = $"  ✔  {sources.Length} elemento(s) movido(s).";
        }

        // ════════════════════════════════════════════════════════════════════
        //  NAVEGACIÓN
        // ════════════════════════════════════════════════════════════════════
        private void NavigateToPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("La ruta no existe.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!string.IsNullOrEmpty(currentPath) && currentPath != path)
                    navigationHistory.Push(currentPath);

                currentPath = path;
                addressBar.Text = currentPath;
                LoadDirectory(currentPath);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Sin permisos para acceder a esta ubicación.",
                    "Error de acceso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al navegar: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //            Carga del directorio con estadísticas por tipo 
        private async void LoadDirectory(string path)
        {
            listView.Items.Clear();
            statusLabel.Text = "  Cargando…";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                var di = new DirectoryInfo(path);

                var dirs = await Task.Run(() =>
                    di.GetDirectories()
                      .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                      .OrderBy(d => d.Name).ToList());

                var files = await Task.Run(() =>
                    di.GetFiles()
                      .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                      .OrderBy(f => f.Name).ToList());

                //         Carpetas 
                foreach (var d in dirs)
                {
                    // Info de carpeta con conteo por tipo (async para no bloquear)
                    string info = await Task.Run(() => DirInfoDetailed(d.FullName));

                    var item = new ListViewItem(d.Name)
                    {
                        ImageKey = "folder",
                        Tag = d.FullName
                    };
                    item.SubItems.Add("Carpeta");
                    item.SubItems.Add("");
                    item.SubItems.Add(info);
                    item.SubItems.Add(d.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }

                //                  Archivos 
                foreach (var f in files)
                {
                    var item = new ListViewItem(f.Name)
                    {
                        ImageKey = IconKey(f.Extension),
                        Tag = f.FullName
                    };
                    item.SubItems.Add(FileTypeName(f.Extension));
                    item.SubItems.Add(FormatSize(f.Length));
                    item.SubItems.Add(f.Extension.ToUpper().TrimStart('.'));
                    item.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }

                //                   Barra de estado con desglose por tipo 
                var stats = CsvIndexer.ClassifyFiles(files.ToArray());
                statusLabel.Text = "  " + stats.ToStatusString(dirs.Count);
                UpdateRightPanel(path);
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"  Error: {ex.Message}";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // Info detallada de carpeta (conteo por tipo)  
        private string DirInfoDetailed(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                var files = di.GetFiles()
                                 .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                                 .ToArray();
                var subdirs = di.GetDirectories()
                                 .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                                 .ToArray();

                var stats = CsvIndexer.ClassifyFiles(files);
                return stats.ToInfoColumn(subdirs.Length);
            }
            catch { return "Sin acceso"; }
        }

        private void GoBack()   // metodo para navegar hacia atrás usando la pila de historial
        {
            if (navigationHistory.Count == 0) return;
            currentPath = navigationHistory.Pop();
            addressBar.Text = currentPath;
            LoadDirectory(currentPath);
        }

        private void GoUp()   // método para navegar hacia arriba al directorio padre
        {
            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null) NavigateToPath(parent.FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)  // Permitir navegar a la ruta escrita al presionar Enter
        {
            if (e.KeyCode != Keys.Enter) return;
            NavigateToPath(addressBar.Text);
            e.Handled = e.SuppressKeyPress = true;
        }

        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)  // Ordenar por columna al hacer clic en el encabezado
        {
            if (e.Column != sortColumn) { sortColumn = e.Column; listView.Sorting = SortOrder.Ascending; }
            else listView.Sorting = listView.Sorting == SortOrder.Ascending
                                    ? SortOrder.Descending : SortOrder.Ascending;
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
                if (new[] { ".txt", ".csv", ".json", ".xml", ".log" }.Contains(ext))
                    new FileViewerForm(path).Show();
                else if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(ext))
                    new ImageViewerForm(path).Show();
                else
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DIÁLOGO DE TEXTO
        // ════════════════════════════════════════════════════════════════════
        private string? InputDialog(string title, string prompt, string def = "")
        {
            using Form dlg = new Form
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

            var lbl = new Label
            {
                Text = prompt,
                Left = 14,
                Top = 18,
                Width = 400,
                ForeColor = Theme.TextSecondary,
                Font = new Font("Segoe UI", 9.5F)
            };

            var txt = new TextBox
            {
                Text = def,
                Left = 14,
                Top = 46,
                Width = 404,
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            txt.SelectAll();

            var ok = new Button
            {
                Text = "Aceptar",
                Left = 224,
                Top = 90,
                Width = 95,
                Height = 34,
                DialogResult = DialogResult.OK,
                BackColor = Theme.AccentBlueDark,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            ok.FlatAppearance.BorderColor = Theme.AccentBlue;

            var cancel = new Button
            {
                Text = "Cancelar",
                Left = 324,
                Top = 90,
                Width = 95,
                Height = 34,
                DialogResult = DialogResult.Cancel,
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            cancel.FlatAppearance.BorderColor = Theme.Border;

            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private string IconKey(string ext)
        {
            ext = ext.ToLower();
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".webp" }.Contains(ext)) return "image";
            if (new[] { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac", ".ogg" }.Contains(ext)) return "audio";
            if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" }.Contains(ext)) return "video";
            if (new[] { ".txt", ".csv", ".json", ".xml", ".log", ".ini",
                        ".config", ".md", ".cs", ".py", ".js", ".html"            }.Contains(ext)) return "text";
            return "file";
        }

        private string FileTypeName(string ext)
        {
            ext = ext.ToLower();
            var map = new Dictionary<string, string>
            {
                {".txt","Texto"},{".csv","CSV"},{".json","JSON"},{".xml","XML"},
                {".md","Markdown"},{".log","Log"},{".ini","Config"},
                {".cs","C#"},{".py","Python"},{".js","JavaScript"},
                {".jpg","JPG"},{".jpeg","JPEG"},{".png","PNG"},{".gif","GIF"},{".bmp","BMP"},
                {".mp3","MP3"},{".wav","WAV"},{".flac","FLAC"},{".aac","AAC"},
                {".mp4","MP4"},{".avi","AVI"},{".mkv","MKV"},{".mov","MOV"}
            };
            return map.TryGetValue(ext, out var t) ? t : "Archivo";
        }

        private string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        // ════════════════════════════════════════════════════════════════════
        //  ICONOS
        // ════════════════════════════════════════════════════════════════════
        private Icon MakeFolderIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(56, 139, 253));
            g.FillRectangle(b, 4, 12, 24, 16);
            g.FillPolygon(b, new[] { new Point(4, 12), new Point(10, 8), new Point(16, 12) });
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon MakeFileIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var body = new SolidBrush(Color.FromArgb(100, 130, 170));
            using var fold = new SolidBrush(Color.FromArgb(56, 139, 253));
            g.FillRectangle(body, 8, 4, 16, 24);
            g.FillPolygon(fold, new[] { new Point(24, 4), new Point(24, 10), new Point(18, 4) });
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon MakeImageIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(30, 80, 140));
            using var sun = new SolidBrush(Color.FromArgb(255, 210, 60));
            using var mnt = new SolidBrush(Color.FromArgb(56, 139, 253));
            g.FillRectangle(bg, 6, 6, 20, 20);
            g.FillEllipse(sun, 10, 9, 6, 6);
            g.FillPolygon(mnt, new[] { new Point(6, 26), new Point(12, 17), new Point(18, 22), new Point(26, 26) });
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon MakeAudioIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(80, 160, 255));
            g.FillEllipse(b, 8, 18, 8, 8);
            g.FillRectangle(b, 14, 8, 2, 14);
            g.FillEllipse(b, 16, 8, 6, 6);
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon MakeVideoIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(30, 60, 110));
            using var play = new SolidBrush(Color.FromArgb(56, 139, 253));
            g.FillRectangle(bg, 6, 10, 14, 12);
            g.FillPolygon(play, new[] { new Point(20, 13), new Point(26, 16), new Point(20, 19) });
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon MakeTextIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var page = new SolidBrush(Color.FromArgb(80, 110, 150));
            g.FillRectangle(page, 8, 4, 16, 24);
            using var pen = new Pen(Color.FromArgb(56, 139, 253), 2);
            g.DrawLine(pen, 11, 10, 21, 10);
            g.DrawLine(pen, 11, 14, 21, 14);
            g.DrawLine(pen, 11, 18, 21, 18);
            g.DrawLine(pen, 11, 22, 18, 22);
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RENDERER OSCURO
    // ════════════════════════════════════════════════════════════════════════
    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.FillRectangle(
                new SolidBrush(e.Item.Selected
                    ? Color.FromArgb(31, 90, 180)
                    : Color.FromArgb(24, 32, 46)),
                new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(24, 32, 46)), e.AffectedBounds);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(38, 50, 70));
            e.Graphics.DrawRectangle(pen,
                new Rectangle(e.AffectedBounds.X, e.AffectedBounds.Y,
                              e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1));
        }
    }

    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(31, 90, 180);
        public override Color MenuItemBorder => Color.FromArgb(56, 139, 253);
        public override Color MenuBorder => Color.FromArgb(38, 50, 70);
        public override Color ToolStripDropDownBackground => Color.FromArgb(24, 32, 46);
        public override Color ImageMarginGradientBegin => Color.FromArgb(17, 23, 33);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(17, 23, 33);
        public override Color ImageMarginGradientEnd => Color.FromArgb(17, 23, 33);
    }






    //  LISTVIEW CON HEADER OSCURO
    // ════════════════════════════════════════════════════════════════════════
    internal class DarkListView : ListView
    {
        private HeaderNativeWindow? _header;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int LVM_GETHEADER = 0x101F;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            IntPtr hHeader = SendMessage(this.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (hHeader != IntPtr.Zero)
            {
                _header?.ReleaseHandle();
                _header = new HeaderNativeWindow(this);
                _header.AssignHandle(hHeader);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _header?.ReleaseHandle();
            base.OnHandleDestroyed(e);
        }

        private class HeaderNativeWindow : NativeWindow
        {
            private const int WM_PAINT = 0x000F;
            private readonly DarkListView _owner;

            [DllImport("user32.dll")]
            private static extern bool GetClientRect(IntPtr hWnd, out RECT r);
            [DllImport("user32.dll")]
            private static extern IntPtr GetDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT { public int Left, Top, Right, Bottom; }

            public HeaderNativeWindow(DarkListView owner) { _owner = owner; }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != WM_PAINT) return;

                int colsWidth = 0;
                foreach (ColumnHeader col in _owner.Columns) colsWidth += col.Width;

                GetClientRect(this.Handle, out RECT rc);
                int clientWidth = rc.Right - rc.Left;
                if (colsWidth >= clientWidth) return;

                IntPtr hdc = GetDC(this.Handle);
                if (hdc == IntPtr.Zero) return;
                try
                {
                    using var g = Graphics.FromHdc(hdc);
                    int h = rc.Bottom - rc.Top;
                    using var bg = new SolidBrush(Color.FromArgb(17, 23, 33));
                    g.FillRectangle(bg, new Rectangle(colsWidth, 0, clientWidth - colsWidth, h));
                    using var accent = new Pen(Color.FromArgb(56, 139, 253), 1);
                    g.DrawLine(accent, colsWidth, h - 1, clientWidth, h - 1);
                }
                finally { ReleaseDC(this.Handle, hdc); }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COMPARADOR DE COLUMNAS
    // ════════════════════════════════════════════════════════════════════════
    internal class LvComparer : System.Collections.IComparer
    {
        private readonly int col;
        private readonly SortOrder order;
        public LvComparer(int col, SortOrder order) { this.col = col; this.order = order; }

        public int Compare(object? x, object? y)
        {
            int r = string.Compare(
                ((ListViewItem)x!).SubItems[col].Text,
                ((ListViewItem)y!).SubItems[col].Text,
                StringComparison.CurrentCulture);
            return order == SortOrder.Descending ? r * -1 : r;
        }
    }
}