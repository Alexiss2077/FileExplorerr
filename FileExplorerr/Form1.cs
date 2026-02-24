using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  PALETA DE COLORES — Negro y Azul claro
    // ════════════════════════════════════════════════════════════════════════
    internal static class Theme
    {
        // Fondos
        public static readonly Color BgDeep = Color.FromArgb(10, 14, 20);   // fondo principal (casi negro)
        public static readonly Color BgSurface = Color.FromArgb(17, 23, 33);   // paneles, ListView
        public static readonly Color BgRaised = Color.FromArgb(24, 32, 46);   // controles elevados

        // Acento azul
        public static readonly Color AccentBlue = Color.FromArgb(56, 139, 253); // azul claro principal
        public static readonly Color AccentBlueDark = Color.FromArgb(31, 90, 180); // botón acción
        public static readonly Color AccentBlueHover = Color.FromArgb(80, 160, 255); // hover

        // Bordes
        public static readonly Color Border = Color.FromArgb(38, 50, 70);
        public static readonly Color BorderSoft = Color.FromArgb(30, 42, 60);

        // Texto
        public static readonly Color TextPrimary = Color.FromArgb(220, 232, 248); // texto principal
        public static readonly Color TextSecondary = Color.FromArgb(110, 140, 180); // texto secundario
        public static readonly Color TextOnAccent = Color.White;

        // Estados especiales
        public static readonly Color DragFolder = Color.FromArgb(28, 60, 110);  // carpeta resaltada al drag
        public static readonly Color RecycleBg = Color.FromArgb(20, 28, 42);  // panel papelera normal
        public static readonly Color RecycleHot = Color.FromArgb(120, 20, 20); // papelera al recibir drag
    }

    public partial class Form1 : Form
    {
        // Estado
        private string currentPath;
        private Stack<string> navigationHistory;
        private ListViewItem dragHighlightedItem;
        private int sortColumn = -1;
        private PictureBox recycleIconBox;

        // ── P/Invoke: icono de Shell ─────────────────────────────────────────
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

        // ── Papelera (P/Invoke) ──────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.U4)] public int wFunc;
            public string pFrom;
            public string pTo;
            public short fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
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

            // ── ImageList ────────────────────────────────────────────────────
            imageList = new ImageList { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            imageList.Images.Add("folder", MakeFolderIcon());
            imageList.Images.Add("file", MakeFileIcon());
            imageList.Images.Add("image", MakeImageIcon());
            imageList.Images.Add("audio", MakeAudioIcon());
            imageList.Images.Add("video", MakeVideoIcon());
            imageList.Images.Add("text", MakeTextIcon());

            // ── Botón Atrás ──────────────────────────────────────────────────
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

            // ── Botón Subir ──────────────────────────────────────────────────
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

            // ── Barra de dirección ───────────────────────────────────────────
            addressBar = new TextBox
            {
                Location = new Point(92, 13),
                Size = new Size(740, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.BgRaised,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            addressBar.KeyDown += AddressBar_KeyDown;

            // ── Botón Nueva Carpeta ──────────────────────────────────────────
            newFolderButton = new Button
            {
                Text = "📁  Nueva carpeta",
                Size = new Size(148, 30),
                Location = new Point(843, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Theme.AccentBlueDark,
                ForeColor = Theme.TextOnAccent,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            newFolderButton.FlatAppearance.BorderColor = Theme.AccentBlue;
            newFolderButton.Click += (s, e) => CreateFolder();

            // ── Panel superior ───────────────────────────────────────────────
            Panel topPanel = new Panel
            {
                Height = 55,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(10)
            };
            // Línea inferior del panel
            topPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Theme.Border, 1),
                    0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };
            topPanel.Controls.Add(backButton);
            topPanel.Controls.Add(upButton);
            topPanel.Controls.Add(addressBar);
            topPanel.Controls.Add(newFolderButton);

            // ── ListView ─────────────────────────────────────────────────────
            listView = new ListView
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
            listView.Columns.Add("Información", 250);
            listView.Columns.Add("Fecha modificación", 150);

            listView.DoubleClick += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag.ToString()); };
            listView.ColumnClick += ListView_ColumnClick;
            listView.ItemDrag += ListView_ItemDrag;
            listView.DragEnter += ListView_DragEnter;
            listView.DragOver += ListView_DragOver;
            listView.DragDrop += ListView_DragDrop;
            listView.DragLeave += (s, e) => ClearDragHighlight();
            listView.MouseClick += ListView_MouseClick;

            // ── Menú contextual ───────────────────────────────────────────────
            BuildContextMenu();
            listView.ContextMenuStrip = contextMenu;

            // ── Label estado ─────────────────────────────────────────────────
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextSecondary,
                Padding = new Padding(12, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F)
            };

            // ── Icono de papelera ─────────────────────────────────────────────
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

            // ── Label papelera ────────────────────────────────────────────────
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

            // ── Panel papelera ────────────────────────────────────────────────
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
            // Línea izquierda del panel papelera
            recycleDropPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Theme.Border, 1), 0, 0, 0, recycleDropPanel.Height);
            };

            // ── Panel inferior ────────────────────────────────────────────────
            Panel bottomPanel = new Panel
            {
                Height = 48,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface
            };
            // Línea superior del panel inferior
            bottomPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Theme.Border, 1), 0, 0, bottomPanel.Width, 0);
            };
            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(recycleDropPanel);

            this.Controls.Add(listView);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
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

            miOpen.Click += (s, e) => { if (listView.SelectedItems.Count > 0) OpenEntry(listView.SelectedItems[0].Tag.ToString()); };
            miNewFolder.Click += (s, e) => CreateFolder();
            miRename.Click += (s, e) => RenameSelected();
            miDelete.Click += (s, e) => DeleteSelected();

            contextMenu.Items.AddRange(new ToolStripItem[] { miOpen, miSep1, miNewFolder, miSep2, miRename, miDelete });
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
        //  CREAR CARPETA
        // ════════════════════════════════════════════════════════════════════
        private void CreateFolder()
        {
            string name = InputDialog("Nueva carpeta", "Nombre:", "Nueva carpeta");
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
                        if (item.Tag.ToString() == newDir) { item.Selected = true; item.EnsureVisible(); break; }
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
            string oldPath = listView.SelectedItems[0].Tag.ToString();
            string oldName = Path.GetFileName(oldPath);
            string newName = InputDialog("Renombrar", "Nuevo nombre:", oldName);
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
                             .Select(i => i.Tag.ToString()).ToArray();

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
                             .Select(i => i.Tag.ToString()).ToArray();
            if (paths.Length == 0) return;
            listView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths),
                DragDropEffects.Move | DragDropEffects.Copy);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DROP EN CARPETA
        // ════════════════════════════════════════════════════════════════════
        private void ListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                       ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.None; return; }

            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            ListViewItem hovered = listView.GetItemAt(pt.X, pt.Y);
            string[] dragged = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (dragHighlightedItem != null && dragHighlightedItem != hovered)
            {
                dragHighlightedItem.BackColor = Theme.BgDeep;
                dragHighlightedItem.ForeColor = Theme.TextPrimary;
                dragHighlightedItem = null;
            }

            bool valid = hovered != null
                      && Directory.Exists(hovered.Tag.ToString())
                      && !dragged.Contains(hovered.Tag.ToString());

            if (valid)
            {
                e.Effect = DragDropEffects.Move;
                hovered.BackColor = Theme.DragFolder;
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
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            Point pt = listView.PointToClient(new Point(e.X, e.Y));
            ListViewItem target = listView.GetItemAt(pt.X, pt.Y);
            if (target == null || !Directory.Exists(target.Tag.ToString())) return;

            MoveItems((string[])e.Data.GetData(DataFormats.FileDrop), target.Tag.ToString());
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
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            e.Effect = DragDropEffects.Move;
            recycleDropPanel.BackColor = Theme.RecycleHot;
            recycleIconBox.Image = GetRecycleBinIcon(true).ToBitmap();
            recyclePanelLabel.ForeColor = Color.White;
            recyclePanelLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            recyclePanelLabel.Text = "Soltar para eliminar";
        }

        private void RecycleDragOver(DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
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
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
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
                        if (targetDir.StartsWith(src + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
            statusLabel.Text = $"✔  {sources.Length} elemento(s) movido(s).";
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
                    MessageBox.Show("La ruta no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (currentPath != null && currentPath != path) navigationHistory.Push(currentPath);
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
                MessageBox.Show($"Error al navegar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void LoadDirectory(string path)
        {
            listView.Items.Clear();
            statusLabel.Text = "Cargando...";
            this.Cursor = Cursors.WaitCursor;
            try
            {
                var di = new DirectoryInfo(path);
                var dirs = await Task.Run(() => di.GetDirectories()
                    .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                    .OrderBy(d => d.Name).ToList());
                var files = await Task.Run(() => di.GetFiles()
                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                    .OrderBy(f => f.Name).ToList());

                foreach (var d in dirs)
                {
                    string info = await Task.Run(() => DirInfo(d.FullName));
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
                    item.SubItems.Add(f.Extension.ToUpper());
                    item.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }
                statusLabel.Text = $"  {dirs.Count} carpetas  ·  {files.Count} archivos";
            }
            catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
            finally { this.Cursor = Cursors.Default; }
        }

        private string DirInfo(string path)
        {
            try { int f = 0, d = 0; CountItems(path, ref f, ref d); return $"{d} carpetas, {f} archivos"; }
            catch { return "Sin acceso"; }
        }

        private void CountItems(string path, ref int f, ref int d, int depth = 0)
        {
            if (depth > 5) return;
            try
            {
                var di = new DirectoryInfo(path);
                f += di.GetFiles().Length;
                var subs = di.GetDirectories(); d += subs.Length;
                foreach (var s in subs) try { CountItems(s.FullName, ref f, ref d, depth + 1); } catch { }
            }
            catch { }
        }

        private void GoBack()
        {
            if (navigationHistory.Count == 0) return;
            currentPath = navigationHistory.Pop();
            addressBar.Text = currentPath;
            LoadDirectory(currentPath);
        }

        private void GoUp()
        {
            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null) NavigateToPath(parent.FullName);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            NavigateToPath(addressBar.Text);
            e.Handled = e.SuppressKeyPress = true;
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
                if (new[] { ".txt", ".csv", ".json", ".xml", ".log" }.Contains(ext))
                    new FileViewerForm(path).Show();
                else if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(ext))
                    new ImageViewerForm(path).Show();
                else
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DIÁLOGO DE TEXTO
        // ════════════════════════════════════════════════════════════════════
        private string InputDialog(string title, string prompt, string def = "")
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
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico" }.Contains(ext)) return "image";
            if (new[] { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac" }.Contains(ext)) return "audio";
            if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" }.Contains(ext)) return "video";
            if (new[] { ".txt", ".csv", ".json", ".xml", ".log", ".ini", ".config" }.Contains(ext)) return "text";
            return "file";
        }

        private string FileTypeName(string ext)
        {
            ext = ext.ToLower();
            var m = new Dictionary<string, string>
            {
                {".txt","Texto"},{".csv","CSV"},{".json","JSON"},{".xml","XML"},
                {".jpg","JPG"},{".jpeg","JPEG"},{".png","PNG"},{".gif","GIF"},
                {".mp3","MP3"},{".wav","WAV"},{".mp4","MP4"},{".avi","AVI"},{".mkv","MKV"}
            };
            return m.TryGetValue(ext, out var t) ? t : "Archivo";
        }

        private string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        // ════════════════════════════════════════════════════════════════════
        //  ICONOS — paleta azul/oscuro
        // ════════════════════════════════════════════════════════════════════
        private Icon MakeFolderIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(56, 139, 253));  // azul acento
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
    //  RENDERER OSCURO PARA EL MENÚ CONTEXTUAL
    // ════════════════════════════════════════════════════════════════════════
    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(31, 90, 180)),
                    new Rectangle(Point.Empty, e.Item.Size));
            }
            else
            {
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(24, 32, 46)),
                    new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(24, 32, 46)), e.AffectedBounds);
        }

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

    // ════════════════════════════════════════════════════════════════════════
    //  COMPARADOR DE COLUMNAS
    // ════════════════════════════════════════════════════════════════════════
    internal class LvComparer : System.Collections.IComparer
    {
        private readonly int col; private readonly SortOrder order;
        public LvComparer(int col, SortOrder order) { this.col = col; this.order = order; }
        public int Compare(object x, object y)
        {
            int r = string.Compare(((ListViewItem)x).SubItems[col].Text,
                                   ((ListViewItem)y).SubItems[col].Text,
                                   StringComparison.CurrentCulture);
            return order == SortOrder.Descending ? r * -1 : r;
        }
    }
}