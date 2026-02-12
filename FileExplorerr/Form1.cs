using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public partial class Form1 : Form
    {
        private ListView listView;
        private TextBox addressBar;
        private Label statusLabel;
        private Button backButton;
        private Button upButton;
        private string currentPath;
        private Stack<string> navigationHistory;
        private ImageList imageList;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            navigationHistory = new Stack<string>();

            // Iniciar en la carpeta de usuario
            string startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            NavigateToPath(startPath);
        }


        private Icon CreateFolderIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                {
                    g.FillRectangle(brush, 4, 12, 24, 16);
                    g.FillPolygon(brush, new Point[] {
                        new Point(4, 12),
                        new Point(10, 8),
                        new Point(16, 12)
                    });
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon CreateFileIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                {
                    g.FillRectangle(brush, 8, 4, 16, 24);
                    g.FillPolygon(brush, new Point[] {
                        new Point(24, 4),
                        new Point(24, 10),
                        new Point(18, 4)
                    });
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon CreateImageIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(100, 150, 200)))
                {
                    g.FillRectangle(brush, 6, 6, 20, 20);
                    g.FillEllipse(Brushes.Yellow, 10, 10, 6, 6);
                    g.FillPolygon(Brushes.Green, new Point[] {
                        new Point(6, 26),
                        new Point(12, 18),
                        new Point(18, 22),
                        new Point(26, 26)
                    });
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon CreateAudioIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(200, 100, 50)))
                {
                    g.FillEllipse(brush, 8, 18, 8, 8);
                    g.FillRectangle(brush, 14, 8, 2, 14);
                    g.FillEllipse(brush, 16, 8, 6, 6);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon CreateVideoIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(150, 50, 150)))
                {
                    g.FillRectangle(brush, 6, 10, 14, 12);
                    g.FillPolygon(brush, new Point[] {
                        new Point(20, 13),
                        new Point(26, 16),
                        new Point(20, 19)
                    });
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private Icon CreateTextIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                {
                    g.FillRectangle(brush, 8, 4, 16, 24);
                    using (Pen pen = new Pen(Color.White, 2))
                    {
                        g.DrawLine(pen, 11, 10, 21, 10);
                        g.DrawLine(pen, 11, 14, 21, 14);
                        g.DrawLine(pen, 11, 18, 21, 18);
                        g.DrawLine(pen, 11, 22, 18, 22);
                    }
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void NavigateToPath(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("La ruta no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (currentPath != null && currentPath != path)
                {
                    navigationHistory.Push(currentPath);
                }

                currentPath = path;
                addressBar.Text = currentPath;
                LoadDirectory(currentPath);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("No tiene permisos para acceder a esta ubicación.", "Error de acceso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al navegar: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void LoadDirectory(string path)
        {
            listView.Items.Clear();
            statusLabel.Text = "Cargando...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(path);

                // Obtener directorios
                var directories = await Task.Run(() => dirInfo.GetDirectories()
                    .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                    .OrderBy(d => d.Name)
                    .ToList());

                // Obtener archivos
                var files = await Task.Run(() => dirInfo.GetFiles()
                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                    .OrderBy(f => f.Name)
                    .ToList());

                // Agregar directorios al ListView
                foreach (var dir in directories)
                {
                    var info = await Task.Run(() => GetDirectoryInfo(dir.FullName));
                    ListViewItem item = new ListViewItem(dir.Name)
                    {
                        ImageKey = "folder",
                        Tag = dir.FullName
                    };
                    item.SubItems.Add("Carpeta");
                    item.SubItems.Add("");
                    item.SubItems.Add(info);
                    item.SubItems.Add(dir.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }

                // Agregar archivos al ListView
                foreach (var file in files)
                {
                    string iconKey = GetIconKeyForFile(file.Extension);
                    ListViewItem item = new ListViewItem(file.Name)
                    {
                        ImageKey = iconKey,
                        Tag = file.FullName
                    };
                    item.SubItems.Add(GetFileType(file.Extension));
                    item.SubItems.Add(FormatFileSize(file.Length));
                    item.SubItems.Add(file.Extension.ToUpper());
                    item.SubItems.Add(file.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                    listView.Items.Add(item);
                }

                // Actualizar barra de estado
                statusLabel.Text = $"{directories.Count} carpetas, {files.Count} archivos";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private string GetDirectoryInfo(string path)
        {
            try
            {
                int fileCount = 0;
                int dirCount = 0;
                CountFilesAndDirectories(path, ref fileCount, ref dirCount);
                return $"{dirCount} carpetas, {fileCount} archivos";
            }
            catch
            {
                return "Sin acceso";
            }
        }

        private void CountFilesAndDirectories(string path, ref int fileCount, ref int dirCount, int depth = 0)
        {
            if (depth > 10) return; // Límite de profundidad para evitar bucles infinitos

            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(path);

                // Contar archivos en el directorio actual
                fileCount += dirInfo.GetFiles().Length;

                // Obtener subdirectorios
                var subDirs = dirInfo.GetDirectories();
                dirCount += subDirs.Length;

                // Recursivamente contar en subdirectorios
                foreach (var subDir in subDirs)
                {
                    try
                    {
                        CountFilesAndDirectories(subDir.FullName, ref fileCount, ref dirCount, depth + 1);
                    }
                    catch
                    {
                        // Ignorar directorios sin acceso
                    }
                }
            }
            catch
            {
                // Ignorar errores de acceso
            }
        }

        private string GetIconKeyForFile(string extension)
        {
            extension = extension.ToLower();

            // Imágenes
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico" }.Contains(extension))
                return "image";

            // Audio
            if (new[] { ".mp3", ".wav", ".wma", ".m4a", ".flac", ".aac" }.Contains(extension))
                return "audio";

            // Video
            if (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" }.Contains(extension))
                return "video";

            // Texto
            if (new[] { ".txt", ".csv", ".json", ".xml", ".log", ".ini", ".config" }.Contains(extension))
                return "text";

            return "file";
        }

        private string GetFileType(string extension)
        {
            extension = extension.ToLower();

            var types = new Dictionary<string, string>
            {
                { ".txt", "Archivo de texto" },
                { ".csv", "CSV" },
                { ".json", "JSON" },
                { ".xml", "XML" },
                { ".jpg", "Imagen JPG" },
                { ".jpeg", "Imagen JPEG" },
                { ".png", "Imagen PNG" },
                { ".gif", "Imagen GIF" },
                { ".mp3", "Audio MP3" },
                { ".wav", "Audio WAV" },
                { ".wma", "Audio WMA" },
                { ".m4a", "Audio M4A" },
                { ".mp4", "Video MP4" },
                { ".avi", "Video AVI" },
                { ".mkv", "Video MKV" }
            };

            return types.ContainsKey(extension) ? types[extension] : "Archivo";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void ListView_DoubleClick(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                string path = listView.SelectedItems[0].Tag.ToString();

                if (Directory.Exists(path))
                {
                    NavigateToPath(path);
                }
                else if (File.Exists(path))
                {
                    OpenFile(path);
                }
            }
        }

        private void OpenFile(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();

                // Archivos de texto
                if (new[] { ".txt", ".csv", ".json", ".xml", ".log" }.Contains(extension))
                {
                    FileViewerForm viewer = new FileViewerForm(filePath);
                    viewer.Show();
                }
                // Imágenes
                else if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" }.Contains(extension))
                {
                    ImageViewerForm viewer = new ImageViewerForm(filePath);
                    viewer.Show();
                }
                // Audio y Video - usar reproductor predeterminado
                else if (new[] { ".mp3", ".wav", ".wma", ".m4a", ".mp4", ".avi", ".mkv" }.Contains(extension))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Otros archivos - intentar abrir con aplicación predeterminada
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir el archivo: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            if (navigationHistory.Count > 0)
            {
                string previousPath = navigationHistory.Pop();
                currentPath = previousPath;
                addressBar.Text = currentPath;
                LoadDirectory(currentPath);
            }
        }

        private void UpButton_Click(object sender, EventArgs e)
        {
            try
            {
                DirectoryInfo parentDir = Directory.GetParent(currentPath);
                if (parentDir != null)
                {
                    NavigateToPath(parentDir.FullName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                NavigateToPath(addressBar.Text);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private int sortColumn = -1;
        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn)
            {
                sortColumn = e.Column;
                listView.Sorting = SortOrder.Ascending;
            }
            else
            {
                listView.Sorting = listView.Sorting == SortOrder.Ascending ?
                    SortOrder.Descending : SortOrder.Ascending;
            }

            listView.Sort();
            listView.ListViewItemSorter = new ListViewItemComparer(e.Column, listView.Sorting);
        }
    }

    class ListViewItemComparer : System.Collections.IComparer
    {
        private int col;
        private SortOrder order;

        public ListViewItemComparer(int column, SortOrder order)
        {
            col = column;
            this.order = order;
        }

        public int Compare(object x, object y)
        {
            int returnVal = String.Compare(((ListViewItem)x).SubItems[col].Text,
                                          ((ListViewItem)y).SubItems[col].Text);

            if (order == SortOrder.Descending)
                returnVal *= -1;

            return returnVal;
        }
    }
}