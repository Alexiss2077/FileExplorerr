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

namespace FileExplorerr
{
    public partial class Form1 : Form
    {
        // ── Estado ───────────────────────────────────────────────────────────
        private string currentPath = "";
        private Stack<string> navigationHistory = new();
        private ListViewItem? dragHighlightedItem;
        private int sortColumn = -1;
        private PictureBox recycleIconBox = null!;

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private static int ToBgr(Color c) => c.B << 16 | c.G << 8 | c.R;

        private void ApplyDarkTitleBar()
        {
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int bg = ToBgr(Theme.BgSurface);
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref bg, sizeof(int));
                int tx = ToBgr(Theme.Accent);
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref tx, sizeof(int));
            }
            catch { }
        }

        private Icon GetRecycleBinIcon(bool full = false)
        {
            try
            {
                string shell32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
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
        //  UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            BackColor = Theme.BgBase;
            Text = "Explorador";

            // ── ImageList ────────────────────────────────────────────────────
            imageList = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            imageList.Images.Add("folder", MakeFolderIcon());
            imageList.Images.Add("file", MakeFileIcon());
            imageList.Images.Add("image", MakeImageIcon());
            imageList.Images.Add("audio", MakeAudioIcon());
            imageList.Images.Add("video", MakeVideoIcon());
            imageList.Images.Add("text", MakeTextIcon());

            // ═══ TOP BAR ═════════════════════════════════════════════════════
            var topPanel = new Panel
            {
                Height = 52,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(8, 0, 8, 0)
            };

            backButton = Theme.MakeIconButton("←");
            backButton.Click += (s, e) => GoBack();

            upButton = Theme.MakeIconButton("↑");
            upButton.Click += (s, e) => GoUp();

            addressBar = Theme.MakeTextBox();
            addressBar.Font = Theme.FontBody;
            addressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            addressBar.KeyDown += AddressBar_KeyDown;

            refreshButton = Theme.MakeIconButton("↻");
            refreshButton.Click += (s, e) => RefreshView();
            new ToolTip().SetToolTip(refreshButton, "Actualizar (F5)");

            newFolderButton = Theme.MakeButton("+ Carpeta", 100, Theme.ButtonKind.Default);
            newFolderButton.Click += (s, e) => CreateFolder();

            exportCsvButton = Theme.MakeButton("Exportar CSV", 110, Theme.ButtonKind.Primary);
            exportCsvButton.Click += async (s, e) => await ExportCsvAsync();

            // Layout del top bar
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };
            rightFlow.Controls.Add(refreshButton);
            rightFlow.Controls.Add(newFolderButton);
            rightFlow.Controls.Add(exportCsvButton);

            var navPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            backButton.Location = new Point(4, 10);
            upButton.Location = new Point(44, 10);
            addressBar.Location = new Point(88, 12);
            addressBar.Height = 28;

            navPanel.Controls.AddRange(new Control[] { backButton, upButton, addressBar });
            navPanel.Resize += (s, e) => addressBar.Width = Math.Max(100, navPanel.Width - 96);

            topPanel.Controls.Add(navPanel);
            topPanel.Controls.Add(rightFlow);

            // ═══ LISTVIEW ════════════════════════════════════════════════════
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
            listView.Columns.Add("Nombre", 320);
            listView.Columns.Add("Tipo", 100);
            listView.Columns.Add("Tamaño", 90);
            listView.Columns.Add("Info", 220);
            listView.Columns.Add("Modificado", 140);

            listView.DrawColumnHeader += ListView_DrawColumnHeader;
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
            listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };

            // ═══ CONTEXT MENU ════════════════════════════════════════════════
            BuildContextMenu();
            listView.ContextMenuStrip = contextMenu;

            // ═══ BOTTOM BAR ══════════════════════════════════════════════════
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextSecondary,
                Padding = new Padding(16, 0, 0, 0),
                Font = Theme.FontBody
            };

            recycleIconBox = new PictureBox
            {
                Size = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Dock = DockStyle.Left,
                Padding = new Padding(8),
                AllowDrop = true,
                Image = GetRecycleBinIcon(false).ToBitmap()
            };
            recycleIconBox.DragEnter += (s, e) => RecycleDragEnter(e);
            recycleIconBox.DragOver += (s, e) => RecycleDragOver(e);
            recycleIconBox.DragLeave += (s, e) => RecycleDragLeave();
            recycleIconBox.DragDrop += (s, e) => RecycleDragDrop(e);

            recyclePanelLabel = new Label
            {
                Text = "Papelera",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall,
                AllowDrop = true
            };
            recyclePanelLabel.DragEnter += (s, e) => RecycleDragEnter(e);
            recyclePanelLabel.DragOver += (s, e) => RecycleDragOver(e);
            recyclePanelLabel.DragLeave += (s, e) => RecycleDragLeave();
            recyclePanelLabel.DragDrop += (s, e) => RecycleDragDrop(e);

            recycleDropPanel = new Panel
            {
                Width = 160,
                Dock = DockStyle.Right,
                BackColor = Theme.RecycleBg,
                AllowDrop = true
            };
            recycleDropPanel.Controls.Add(recyclePanelLabel);
            recycleDropPanel.Controls.Add(recycleIconBox);
            recycleDropPanel.DragEnter += (s, e) => RecycleDragEnter(e);
            recycleDropPanel.DragOver += (s, e) => RecycleDragOver(e);
            recycleDropPanel.DragLeave += (s, e) => RecycleDragLeave();
            recycleDropPanel.DragDrop += (s, e) => RecycleDragDrop(e);

            var bottomPanel = new Panel
            {
                Height = 44,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface
            };
            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(recycleDropPanel);

            // ═══ RIGHT PANEL ═════════════════════════════════════════════════
            rightInfoPanel = new Panel
            {
                Width = 280,
                Dock = DockStyle.Right,
                BackColor = Theme.BgSurface,
            };

            var rightHeader = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = Theme.BgElevated,
                Padding = new Padding(12, 0, 0, 0)
            };

            folderNameLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Buscar",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            rightHeader.Controls.Add(folderNameLabel);

            var searchPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(8, 5, 8, 5)
            };

            searchBox = Theme.MakeTextBox("Buscar archivos...");
            searchBox.Dock = DockStyle.Fill;

            var searchBtn = Theme.MakeButton("Ir", 50, Theme.ButtonKind.Primary);
            searchBtn.Dock = DockStyle.Right;
            searchBtn.Click += (s, e) => SearchInPanel(searchBox.Text);
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SearchInPanel(searchBox.Text); };

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(searchBtn);

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
                ItemHeight = 24,
                DrawMode = TreeViewDrawMode.OwnerDrawAll
            };
            infoTree.BeforeExpand += InfoTree_BeforeExpand;
            infoTree.NodeMouseDoubleClick += InfoTree_NodeDoubleClick;
            infoTree.DrawNode += InfoTree_DrawNode;

            rightInfoPanel.Controls.Add(infoTree);
            rightInfoPanel.Controls.Add(searchPanel);
            rightInfoPanel.Controls.Add(rightHeader);

            // ═══ ASSEMBLY ════════════════════════════════════════════════════
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) RefreshView(); };

            Controls.Add(listView);
            Controls.Add(rightInfoPanel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
        }

        // ════════════════════════════════════════════════════════════════════
        //  LISTVIEW HEADER DRAW
        // ════════════════════════════════════════════════════════════════════
        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using var bg = new SolidBrush(Theme.BgSurface);
            e.Graphics.FillRectangle(bg, e.Bounds);

            // Bottom accent line
            using var line = new Pen(Theme.Border);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            var textRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
            using var brush = new SolidBrush(Theme.TextMuted);
            using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(e.Header!.Text, Theme.FontSmallBold, brush, textRect, sf);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PANEL DERECHO — TREEVIEW
        // ════════════════════════════════════════════════════════════════════
        private static readonly (string Label, string[] Exts, string Emoji)[] FileGroups =
        {
            ("Imágenes",     new[]{".jpg",".jpeg",".png",".gif",".bmp",".ico",".webp",".tiff"}, "●"),
            ("Audio",        new[]{".mp3",".wav",".wma",".m4a",".flac",".aac",".ogg"},          "●"),
            ("Video",        new[]{".mp4",".avi",".mkv",".mov",".wmv",".flv",".webm"},          "●"),
            ("Texto",        new[]{".txt",".json",".xml",".csv",".log",".ini",".md",
                                   ".cs",".py",".js",".ts",".html",".css",".config"},           "●"),
            ("Documentos",   new[]{".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf"},       "●"),
            ("Otros",        Array.Empty<string>(),                                             "●"),
        };

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

                if (subdirs.Length > 0)
                {
                    var foldersNode = MakeGroupNode("Carpetas", subdirs.Length, NodeKind.Header);
                    foreach (var d in subdirs)
                    {
                        var dn = MakeFolderNode(d.Name, d.FullName);
                        PopulateFolderNodeDummy(dn, d.FullName);
                        foldersNode.Nodes.Add(dn);
                    }
                    foldersNode.Expand();
                    infoTree.Nodes.Add(foldersNode);
                }

                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (label, exts, emoji) in FileGroups)
                {
                    FileInfo[] matched = exts.Length == 0
                        ? files.Where(f => !used.Contains(f.FullName)).ToArray()
                        : files.Where(f => exts.Contains(f.Extension.ToLower()) && !used.Contains(f.FullName)).ToArray();
                    if (matched.Length == 0) continue;
                    foreach (var f in matched) used.Add(f.FullName);
                    var grp = MakeGroupNode(label, matched.Length, NodeKind.Category);
                    foreach (var f in matched.OrderBy(x => x.Name))
                        grp.Nodes.Add(MakeFileNode(f.Name, f.FullName));
                    infoTree.Nodes.Add(grp);
                }

                if (infoTree.Nodes.Count == 0)
                    infoTree.Nodes.Add(MakeDimNode("Vacía"));
            }
            catch { infoTree.Nodes.Add(MakeDimNode("Sin acceso")); }
            infoTree.EndUpdate();
        }

        private void SearchInPanel(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { UpdateRightPanel(currentPath); return; }
            infoTree.BeginUpdate();
            infoTree.Nodes.Clear();
            query = query.Trim();
            folderNameLabel.Text = $"\"{query}\"";
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
                { infoTree.Nodes.Add(MakeDimNode("Sin resultados")); infoTree.EndUpdate(); return; }

                if (dirs.Length > 0)
                {
                    var rootNode = MakeGroupNode("Carpetas", dirs.Length, NodeKind.Header);
                    foreach (var d in dirs)
                    {
                        string rel = d.FullName.Length > currentPath.Length
                            ? d.FullName.Substring(currentPath.Length).TrimStart(Path.DirectorySeparatorChar) : d.Name;
                        var dn = MakeFolderNode(rel, d.FullName);
                        PopulateFolderNodeDummy(dn, d.FullName);
                        rootNode.Nodes.Add(dn);
                    }
                    rootNode.Expand();
                    infoTree.Nodes.Add(rootNode);
                }
                if (files.Length > 0)
                {
                    var rootNode = MakeGroupNode("Archivos", files.Length, NodeKind.Header);
                    foreach (var f in files)
                    {
                        string rel = f.FullName.Length > currentPath.Length
                            ? f.FullName.Substring(currentPath.Length).TrimStart(Path.DirectorySeparatorChar) : f.Name;
                        rootNode.Nodes.Add(MakeFileNode(rel, f.FullName));
                    }
                    rootNode.Expand();
                    infoTree.Nodes.Add(rootNode);
                }
            }
            catch (Exception ex) { infoTree.Nodes.Add(MakeDimNode("Error: " + ex.Message)); }
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
                infoTree.BeginUpdate();
                node.Nodes.Clear();
                try
                {
                    var di = new DirectoryInfo(nt.Path);
                    var subdirs = di.GetDirectories().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToArray();
                    var files = di.GetFiles().Where(x => (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).ToArray();
                    foreach (var sub in subdirs) { var sn = MakeFolderNode(sub.Name, sub.FullName); PopulateFolderNodeDummy(sn, sub.FullName); node.Nodes.Add(sn); }
                    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (label, exts, emoji) in FileGroups)
                    {
                        FileInfo[] matched = exts.Length == 0 ? files.Where(f => !used.Contains(f.FullName)).ToArray()
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

        private void InfoTree_NodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is NodeTag nt && nt.Path != null && nt.Path != "__dummy__")
            {
                if (Directory.Exists(nt.Path)) NavigateToPath(nt.Path);
                else if (File.Exists(nt.Path)) OpenEntry(nt.Path);
            }
        }

        private enum NodeKind { Header, Category, Folder, File, Dim }
        private record NodeTag(NodeKind Kind, string? Path = null);

        private void InfoTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null) return;
            NodeKind kind = e.Node.Tag is NodeTag nt ? nt.Kind : NodeKind.Dim;

            Color fg = kind switch
            {
                NodeKind.Header => Theme.Accent,
                NodeKind.Category => Theme.TextSecondary,
                NodeKind.Folder => Color.FromArgb(210, 200, 140),
                NodeKind.File => Theme.TextPrimary,
                _ => Theme.TextMuted
            };

            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Rectangle rowRect = new(0, e.Bounds.Top, infoTree.Width, e.Bounds.Height);
            using var bgBrush = new SolidBrush(selected ? Theme.BgSelected : Theme.BgSurface);
            e.Graphics.FillRectangle(bgBrush, rowRect);

            int indent = (e.Node.Level + 1) * infoTree.Indent;
            int textX = indent + 4;
            int textY = e.Bounds.Top + (e.Bounds.Height - 14) / 2;

            if (e.Node.Nodes.Count > 0 || (e.Node.Tag is NodeTag nt2 && nt2.Kind == NodeKind.Folder))
            {
                int btnX = indent - 14, btnY = e.Bounds.Top + (e.Bounds.Height - 8) / 2;
                using var signBrush = new SolidBrush(Theme.TextMuted);
                string sign = e.Node.IsExpanded ? "−" : "+";
                e.Graphics.DrawString(sign, Theme.FontSmallBold, signBrush, btnX - 2, btnY - 2);
            }

            FontStyle fs = kind == NodeKind.Header ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font("Segoe UI", 8.5F, fs);
            using var brush = new SolidBrush(fg);
            e.Graphics.DrawString(e.Node.Text, font, brush, textX, textY);
        }

        private static TreeNode MakeGroupNode(string label, int count, NodeKind kind) =>
            new($"{label}  ({count})") { Tag = new NodeTag(kind) };
        private static TreeNode MakeFolderNode(string name, string path) =>
            new("▸ " + name) { Tag = new NodeTag(NodeKind.Folder, path) };
        private static TreeNode MakeFileNode(string name, string path) =>
            new("  " + name) { Tag = new NodeTag(NodeKind.File, path) };
        private static TreeNode MakeDimNode(string text) =>
            new("  " + text) { Tag = new NodeTag(NodeKind.Dim) };

        // ════════════════════════════════════════════════════════════════════
        //  REFRESH / EXPORT
        // ════════════════════════════════════════════════════════════════════
        private void RefreshView()
        {
            if (!string.IsNullOrEmpty(currentPath)) LoadDirectory(currentPath);
        }

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
                if (MessageBox.Show($"CSV generado:\n{dlg.FileName}\n\n¿Abrir?", "Exportación",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { exportCsvButton.Enabled = true; exportCsvButton.Text = "Exportar CSV"; }
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
            var miSep1 = new ToolStripSeparator();
            var miNewFolder = new ToolStripMenuItem("Nueva carpeta") { ForeColor = Theme.TextPrimary };
            var miSep2 = new ToolStripSeparator();
            var miRename = new ToolStripMenuItem("Renombrar") { ForeColor = Theme.TextPrimary };
            var miDelete = new ToolStripMenuItem("Eliminar") { ForeColor = Theme.Danger };
            var miSep3 = new ToolStripSeparator();
            var miRefresh = new ToolStripMenuItem("Actualizar") { ForeColor = Theme.Accent };

            miOpen.Click += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag!.ToString()!); };
            miNewFolder.Click += (s, e) => CreateFolder();
            miRename.Click += (s, e) => RenameSelected();
            miDelete.Click += (s, e) => DeleteSelected();
            miRefresh.Click += (s, e) => RefreshView();

            contextMenu.Items.AddRange(new ToolStripItem[] { miOpen, miSep1, miNewFolder, miSep2, miRename, miDelete, miSep3, miRefresh });
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
        }

        // ════════════════════════════════════════════════════════════════════
        //  CREAR CARPETA / RENOMBRAR / ELIMINAR
        // ════════════════════════════════════════════════════════════════════
        private void CreateFolder()
        {
            string? name = InputDialog("Nueva carpeta", "Nombre:");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { MessageBox.Show("Nombre no válido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
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
            string msg = paths.Length == 1 ? $"¿Eliminar \"{Path.GetFileName(paths[0])}\"?" : $"¿Eliminar {paths.Length} elementos?";
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
            if (paths.Length > 0) listView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Move | DragDropEffects.Copy);
        }

        private void ListView_DragEnter(object sender, DragEventArgs e) =>
            e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.None; return; }
            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            ListViewItem? hovered = listView.GetItemAt(pt.X, pt.Y);
            string[] dragged = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (dragHighlightedItem != null && dragHighlightedItem != hovered)
            { dragHighlightedItem.BackColor = Theme.BgBase; dragHighlightedItem.ForeColor = Theme.TextPrimary; dragHighlightedItem = null; }
            bool valid = hovered != null && Directory.Exists(hovered.Tag!.ToString()) && !dragged.Contains(hovered.Tag!.ToString());
            if (valid) { e.Effect = DragDropEffects.Move; hovered!.BackColor = Theme.DragTarget; hovered.ForeColor = Theme.Accent; dragHighlightedItem = hovered; }
            else e.Effect = DragDropEffects.None;
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
            dragHighlightedItem.BackColor = Theme.BgBase;
            dragHighlightedItem.ForeColor = Theme.TextPrimary;
            dragHighlightedItem = null;
        }

        // ── Papelera D&D ─────────────────────────────────────────────────────
        private void RecycleDragEnter(DragEventArgs e)
        {
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effect = DragDropEffects.Move;
            recycleDropPanel.BackColor = Theme.RecycleHot;
            recycleIconBox.Image = GetRecycleBinIcon(true).ToBitmap();
            recyclePanelLabel.ForeColor = Theme.Danger;
            recyclePanelLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            recyclePanelLabel.Text = "Soltar para eliminar";
        }
        private void RecycleDragOver(DragEventArgs e) =>
            e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;
        private void RecycleDragLeave()
        {
            recycleDropPanel.BackColor = Theme.RecycleBg;
            recycleIconBox.Image = GetRecycleBinIcon(false).ToBitmap();
            recyclePanelLabel.ForeColor = Theme.TextMuted;
            recyclePanelLabel.Font = Theme.FontSmall;
            recyclePanelLabel.Text = "Papelera";
        }
        private void RecycleDragDrop(DragEventArgs e)
        {
            RecycleDragLeave();
            if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (paths.Length == 0) return;
            string msg = paths.Length == 1 ? $"¿Eliminar \"{Path.GetFileName(paths[0])}\"?" : $"¿Eliminar {paths.Length} elementos?";
            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (string p in paths) SendToRecycleBin(p);
            LoadDirectory(currentPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOVER
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
                            var r = MessageBox.Show($"Ya existe \"{name}\". ¿Sobreescribir?", "Conflicto", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                            if (r == DialogResult.Cancel) return;
                            if (r == DialogResult.No) continue;
                            File.Delete(dest);
                        }
                        File.Move(src, dest);
                    }
                    else if (Directory.Exists(src))
                    {
                        if (targetDir.StartsWith(src + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        { MessageBox.Show($"No se puede mover dentro de sí misma.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); continue; }
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
                if (!Directory.Exists(path)) { MessageBox.Show("La ruta no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                if (!string.IsNullOrEmpty(currentPath) && currentPath != path) navigationHistory.Push(currentPath);
                currentPath = path;
                addressBar.Text = currentPath;
                LoadDirectory(currentPath);
            }
            catch (UnauthorizedAccessException) { MessageBox.Show("Sin permisos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private async void LoadDirectory(string path)
        {
            listView.Items.Clear();
            statusLabel.Text = "  Cargando...";
            Cursor = Cursors.WaitCursor;
            try
            {
                var di = new DirectoryInfo(path);
                var dirs = await Task.Run(() => di.GetDirectories().Where(d => (d.Attributes & FileAttributes.Hidden) == 0).OrderBy(d => d.Name).ToList());
                var files = await Task.Run(() => di.GetFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).OrderBy(f => f.Name).ToList());

                foreach (var d in dirs)
                {
                    string info = await Task.Run(() => DirInfoDetailed(d.FullName));
                    var item = new ListViewItem(d.Name) { ImageKey = "folder", Tag = d.FullName };
                    item.SubItems.Add("Carpeta");
                    item.SubItems.Add("");
                    item.SubItems.Add(info);
                    item.SubItems.Add(d.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }
                foreach (var f in files)
                {
                    var item = new ListViewItem(f.Name) { ImageKey = IconKey(f.Extension), Tag = f.FullName };
                    item.SubItems.Add(FileTypeName(f.Extension));
                    item.SubItems.Add(FormatSize(f.Length));
                    item.SubItems.Add(f.Extension.ToUpper().TrimStart('.'));
                    item.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }

                var stats = CsvIndexer.ClassifyFiles(files.ToArray());
                statusLabel.Text = "  " + stats.ToStatusString(dirs.Count);
                UpdateRightPanel(path);
            }
            catch (Exception ex) { statusLabel.Text = $"  Error: {ex.Message}"; }
            finally { Cursor = Cursors.Default; }
        }

        private string DirInfoDetailed(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                var files = di.GetFiles().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).ToArray();
                var subdirs = di.GetDirectories().Where(d => (d.Attributes & FileAttributes.Hidden) == 0).ToArray();
                return CsvIndexer.ClassifyFiles(files).ToInfoColumn(subdirs.Length);
            }
            catch { return "Sin acceso"; }
        }

        private void GoBack() { if (navigationHistory.Count == 0) return; currentPath = navigationHistory.Pop(); addressBar.Text = currentPath; LoadDirectory(currentPath); }
        private void GoUp() { try { var p = Directory.GetParent(currentPath); if (p != null) NavigateToPath(p.FullName); } catch { } }
        private void AddressBar_KeyDown(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { NavigateToPath(addressBar.Text); e.Handled = e.SuppressKeyPress = true; } }
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
                if (new[] { ".txt", ".csv", ".json", ".xml", ".log" }.Contains(ext))
                    new FileViewerForm(path).Show();
                else if (ImageViewerForm.SupportedExtensions.Contains(ext))
                    new ImageViewerForm(path).Show();
                else if (MusicPlayerForm.SupportedExtensions.Contains(ext))
                    new MusicPlayerForm(path).Show();
                else if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".3gp", ".mpg", ".mpeg", ".vob", ".divx" }.Contains(ext))
                    new VideoPlayerForm(path).Show();
                else
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DIÁLOGO
        // ════════════════════════════════════════════════════════════════════
        private string? InputDialog(string title, string prompt, string def = "")
        {
            using Form dlg = new()
            {
                Text = title,
                Width = 420,
                Height = 160,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary
            };
            var lbl = new Label { Text = prompt, Left = 14, Top = 18, Width = 390, ForeColor = Theme.TextSecondary, Font = Theme.FontBody };
            var txt = Theme.MakeTextBox(); txt.Text = def; txt.Left = 14; txt.Top = 44; txt.Width = 386; txt.SelectAll();
            var ok = Theme.MakeButton("Aceptar", 90, Theme.ButtonKind.Primary); ok.Left = 210; ok.Top = 86; ok.DialogResult = DialogResult.OK;
            var cancel = Theme.MakeButton("Cancelar", 90); cancel.Left = 310; cancel.Top = 86; cancel.DialogResult = DialogResult.Cancel;
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = (IButtonControl)ok; dlg.CancelButton = (IButtonControl)cancel;
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
                {".jpg","JPEG"},{".jpeg","JPEG"},{".png","PNG"},{".gif","GIF"},{".bmp","BMP"},{".svg","SVG"},
                {".webp","WebP"},{".ico","Icono"},{".tiff","TIFF"},
                {".mp3","MP3"},{".wav","WAV"},{".flac","FLAC"},{".aac","AAC"},{".m4a","M4A"},{".ogg","OGG"},
                {".mp4","MP4"},{".avi","AVI"},{".mkv","MKV"},{".mov","MOV"},{".wmv","WMV"},{".webm","WebM"},
                {".pdf","PDF"},{".doc","Word"},{".docx","Word"},{".xls","Excel"},{".xlsx","Excel"},
                {".ppt","PowerPoint"},{".pptx","PowerPoint"},
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

        // ── Iconos minimalistas: círculos de color ───────────────────────────
        // ── Iconos detallados por tipo de archivo (32×32) ─────────────────
        private static Icon MakeFolderIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Theme.Accent);
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
            using var body = new SolidBrush(Theme.TextMuted);
            using var fold = new SolidBrush(Theme.Accent);
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
            using var bg = new SolidBrush(Color.FromArgb(50, 35, 70));
            using var sun = new SolidBrush(Color.FromArgb(230, 200, 100));
            using var mnt = new SolidBrush(Color.FromArgb(160, 120, 200));
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
            using var b = new SolidBrush(Color.FromArgb(100, 200, 150));
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
            using var bg = new SolidBrush(Color.FromArgb(70, 45, 25));
            using var play = new SolidBrush(Color.FromArgb(220, 160, 100));
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
            using var page = new SolidBrush(Color.FromArgb(70, 90, 130));
            g.FillRectangle(page, 8, 4, 16, 24);
            using var pen = new Pen(Color.FromArgb(140, 180, 230), 2);
            g.DrawLine(pen, 11, 10, 21, 10);
            g.DrawLine(pen, 11, 14, 21, 14);
            g.DrawLine(pen, 11, 18, 21, 18);
            g.DrawLine(pen, 11, 22, 18, 22);
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MENU RENDERER MINIMALISTA
    // ════════════════════════════════════════════════════════════════════════
    internal class MinimalMenuRenderer : ToolStripProfessionalRenderer
    {
        public MinimalMenuRenderer() : base(new MinimalColorTable()) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) =>
            e.Graphics.FillRectangle(new SolidBrush(e.Item.Selected ? Theme.BgHover : Theme.BgElevated), new Rectangle(Point.Empty, e.Item.Size));
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        { int y = e.Item.Height / 2; e.Graphics.DrawLine(new Pen(Theme.Border), 8, y, e.Item.Width - 8, y); }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
            e.Graphics.FillRectangle(new SolidBrush(Theme.BgElevated), e.AffectedBounds);
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) =>
            e.Graphics.DrawRectangle(new Pen(Theme.Border), new Rectangle(e.AffectedBounds.X, e.AffectedBounds.Y, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1));
    }

    internal class MinimalColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.BgHover;
        public override Color MenuItemBorder => Theme.Border;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.BgElevated;
        public override Color ImageMarginGradientBegin => Theme.BgSurface;
        public override Color ImageMarginGradientMiddle => Theme.BgSurface;
        public override Color ImageMarginGradientEnd => Theme.BgSurface;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COMPARADOR
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