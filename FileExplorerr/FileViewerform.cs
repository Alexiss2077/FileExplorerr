using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  VISOR DE ARCHIVOS — CSV / TXT / JSON / XML
    //  Con tabla, duplicados, normalización de fechas, campos vacíos y filtrado
    // ════════════════════════════════════════════════════════════════════════
    public class FileViewerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Panel topPanel = null!;
        private Label fileInfoLabel = null!;
        private Panel filterPanel = null!;
        private TextBox filterBox = null!;
        private ComboBox filterColumnCombo = null!;
        private Button filterBtn = null!;
        private Button clearFilterBtn = null!;
        private Label filterLabel = null!;
        private DataGridView grid = null!;
        private Panel bottomPanel = null!;
        private Label statusLabel = null!;
        private Button saveFixedBtn = null!;
        private Button exportCsvBtn = null!;

        // ── Estado ───────────────────────────────────────────────────────────
        private readonly string filePath;
        private readonly string ext;
        private DataTable masterTable = new();   // datos originales parseados
        private DataTable displayTable = new();  // tabla filtrada/mostrada
        private List<int> duplicateRows = new(); // índices de filas duplicadas
        private List<(int Row, int Col, string Original, string Fixed)> dateIssues = new();
        private List<(int Row, int Col)> emptyFields = new();

        // Regex para detectar fechas comunes distintas a yyyy-MM-dd
        private static readonly Regex[] DatePatterns = {
            new(@"\b(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})\b"),   // dd/mm/yyyy  dd-mm-yyyy
            new(@"\b(\d{2,4})[/\-\.](\d{1,2})[/\-\.](\d{1,2})\b"),   // yyyy/mm/dd con separador /
            new(@"\b(\d{1,2})\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(\d{2,4})\b", RegexOptions.IgnoreCase),
        };

        // ════════════════════════════════════════════════════════════════════
        public FileViewerForm(string path)
        {
            filePath = path;
            ext = Path.GetExtension(path).ToLower();
            BuildUI();
            LoadFile();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = $"Visor — {Path.GetFileName(filePath)}";
            Size = new Size(1100, 720);
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 14, 20);
            ForeColor = Color.FromArgb(220, 232, 248);

            // ── Top panel ───────────────────────────────────────────────────
            topPanel = new Panel { Height = 48, Dock = DockStyle.Top, BackColor = Color.FromArgb(17, 23, 33) };
            topPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);

            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(110, 140, 180),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            topPanel.Controls.Add(fileInfoLabel);

            // ── Filter panel ────────────────────────────────────────────────
            filterPanel = new Panel { Height = 46, Dock = DockStyle.Top, BackColor = Color.FromArgb(14, 20, 30), Padding = new Padding(8, 7, 8, 7) };
            filterPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, filterPanel.Height - 1, filterPanel.Width, filterPanel.Height - 1);

            filterLabel = new Label
            {
                Text = "Filtrar:",
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 9F),
                Location = new Point(8, 14)
            };

            filterColumnCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(24, 32, 46),
                ForeColor = Color.FromArgb(220, 232, 248),
                Font = new Font("Segoe UI", 9F),
                Width = 160,
                Location = new Point(58, 10),
                FlatStyle = FlatStyle.Flat
            };

            filterBox = new TextBox
            {
                BackColor = Color.FromArgb(24, 32, 46),
                ForeColor = Color.FromArgb(220, 232, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                Width = 260,
                Location = new Point(228, 11),
                PlaceholderText = "Texto a buscar…"
            };
            filterBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };

            filterBtn = MakeBtn("Buscar", 80, Color.FromArgb(31, 90, 180), Color.FromArgb(56, 139, 253));
            filterBtn.Location = new Point(498, 10);
            filterBtn.Click += (s, e) => ApplyFilter();

            clearFilterBtn = MakeBtn("Limpiar", 70, Color.FromArgb(24, 32, 46), Color.FromArgb(38, 50, 70));
            clearFilterBtn.Location = new Point(582, 10);
            clearFilterBtn.Click += (s, e) => ClearFilter();

            filterPanel.Controls.AddRange(new Control[] { filterLabel, filterColumnCombo, filterBox, filterBtn, clearFilterBtn });

            // ── Grid ────────────────────────────────────────────────────────
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(10, 14, 20),
                GridColor = Color.FromArgb(28, 38, 54),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = true,
                RowHeadersWidth = 48,
                RowHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(17, 23, 33),
                    ForeColor = Color.FromArgb(80, 110, 150),
                    Font = new Font("Segoe UI", 8F)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(17, 23, 33),
                    ForeColor = Color.FromArgb(110, 160, 210),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Padding = new Padding(4, 0, 0, 0)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(13, 18, 26),
                    ForeColor = Color.FromArgb(200, 220, 245),
                    Font = new Font("Cascadia Code", 9F),
                    SelectionBackColor = Color.FromArgb(31, 70, 140),
                    SelectionForeColor = Color.White,
                    NullValue = ""
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(15, 21, 31),
                    ForeColor = Color.FromArgb(200, 220, 245)
                },
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 26 },
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both
            };
            grid.CellFormatting += Grid_CellFormatting;
            grid.ColumnHeaderMouseClick += (s, e) => SortByColumn(e.ColumnIndex);

            // ── Bottom panel ────────────────────────────────────────────────
            bottomPanel = new Panel { Height = 84, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(17, 23, 33) };
            bottomPanel.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, 0, bottomPanel.Width, 0);
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)), 0, 42, bottomPanel.Width, 42);
            };

            statusLabel = new Label
            {
                Left = 12,
                Top = 12,
                Height = 22,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            bottomPanel.Resize += (s, e) => statusLabel.Width = bottomPanel.Width - 24;

            // Fila 1: guardar copia corregida (alineada derecha)
            saveFixedBtn = MakeBtn("💾  Guardar copia corregida", 200, Color.FromArgb(22, 80, 40), Color.FromArgb(35, 134, 54));
            saveFixedBtn.Top = 8;
            saveFixedBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saveFixedBtn.Click += (s, e) => SaveFixedCopy();
            bottomPanel.Resize += (s, e) => saveFixedBtn.Left = bottomPanel.Width - saveFixedBtn.Width - 8;

            // Fila 2: botones exportar por formato
            var exportLabel = new Label
            {
                Text = "Exportar como:",
                Top = 52,
                Left = 12,
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 140, 180),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            exportCsvBtn = MakeBtn("CSV", 62, Color.FromArgb(20, 55, 100), Color.FromArgb(56, 139, 253));
            exportCsvBtn.Top = 48; exportCsvBtn.Left = 114;
            exportCsvBtn.Click += (s, e) => ExportAs(".csv");

            var exportJsonBtn = MakeBtn("JSON", 62, Color.FromArgb(80, 50, 10), Color.FromArgb(210, 140, 30));
            exportJsonBtn.Top = 48; exportJsonBtn.Left = 182;
            exportJsonBtn.Click += (s, e) => ExportAs(".json");

            var exportTxtBtn = MakeBtn("TXT", 62, Color.FromArgb(30, 60, 30), Color.FromArgb(60, 160, 60));
            exportTxtBtn.Top = 48; exportTxtBtn.Left = 250;
            exportTxtBtn.Click += (s, e) => ExportAs(".txt");

            var exportXmlBtn = MakeBtn("XML", 62, Color.FromArgb(70, 20, 70), Color.FromArgb(180, 60, 200));
            exportXmlBtn.Top = 48; exportXmlBtn.Left = 318;
            exportXmlBtn.Click += (s, e) => ExportAs(".xml");

            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(saveFixedBtn);
            bottomPanel.Controls.Add(exportLabel);
            bottomPanel.Controls.Add(exportCsvBtn);
            bottomPanel.Controls.Add(exportJsonBtn);
            bottomPanel.Controls.Add(exportTxtBtn);
            bottomPanel.Controls.Add(exportXmlBtn);

            Controls.Add(grid);
            Controls.Add(filterPanel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
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
        //  CARGA
        // ════════════════════════════════════════════════════════════════════
        private void LoadFile()
        {
            try
            {
                var fi = new FileInfo(filePath);
                fileInfoLabel.Text = $"  {fi.Name}   ·   {FormatSize(fi.Length)}   ·   {fi.LastWriteTime:dd/MM/yyyy HH:mm}";

                masterTable = ext switch
                {
                    ".csv" => ParseCsv(File.ReadAllText(filePath)),
                    ".txt" => ParseTxt(File.ReadAllText(filePath)),
                    ".json" => ParseJson(File.ReadAllText(filePath)),
                    ".xml" => ParseXml(File.ReadAllText(filePath)),
                    _ => ParseTxt(File.ReadAllText(filePath))
                };

                AnalyzeTable();
                PopulateFilterCombo();
                ApplyDisplayTable(masterTable);
                UpdateStatus();
                ShowAnalysisPopup();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PARSERS
        // ════════════════════════════════════════════════════════════════════

        // ── CSV ─────────────────────────────────────────────────────────────
        private static DataTable ParseCsv(string content)
        {
            var dt = new DataTable();
            var lines = SplitLines(content);
            if (lines.Count == 0) return dt;

            var headers = SplitCsvLine(lines[0]);
            foreach (var h in headers)
                dt.Columns.Add(h.Trim('"', ' ').Length > 0 ? h.Trim('"', ' ') : $"Col{dt.Columns.Count + 1}");

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = SplitCsvLine(lines[i]);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Count ? cells[c].Trim('"') : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ── TXT (delimitado por tabs, pipes, punto y coma, o líneas planas) ─
        private static DataTable ParseTxt(string content)
        {
            var lines = SplitLines(content);
            if (lines.Count == 0) return SingleColumnTable(content);

            // Detectar delimitador
            char delim = '\0';
            string first = lines[0];
            if (first.Contains('\t')) delim = '\t';
            else if (first.Contains('|')) delim = '|';
            else if (first.Contains(';')) delim = ';';
            else if (first.Contains(',') && lines.Count > 1) delim = ',';

            if (delim == '\0') return SingleColumnTable(content);

            var dt = new DataTable();
            var headers = first.Split(delim);
            foreach (var h in headers)
                dt.Columns.Add(h.Trim().Length > 0 ? h.Trim() : $"Col{dt.Columns.Count + 1}");

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = lines[i].Split(delim);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Length ? cells[c].Trim() : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static DataTable SingleColumnTable(string content)
        {
            var dt = new DataTable();
            dt.Columns.Add("Línea");
            foreach (var line in SplitLines(content))
                if (!string.IsNullOrWhiteSpace(line))
                    dt.Rows.Add(line);
            return dt;
        }

        // ── JSON ─────────────────────────────────────────────────────────────
        private static DataTable ParseJson(string content)
        {
            var dt = new DataTable();
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Array de objetos → tabla
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in root.EnumerateArray())
                    {
                        if (elem.ValueKind != JsonValueKind.Object) continue;
                        foreach (var prop in elem.EnumerateObject())
                            if (!dt.Columns.Contains(prop.Name))
                                dt.Columns.Add(prop.Name);
                    }
                    foreach (var elem in root.EnumerateArray())
                    {
                        if (elem.ValueKind != JsonValueKind.Object) continue;
                        var row = dt.NewRow();
                        foreach (var prop in elem.EnumerateObject())
                            if (dt.Columns.Contains(prop.Name))
                                row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.ToString();
                        dt.Rows.Add(row);
                    }
                }
                // Objeto único → pares clave-valor
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    dt.Columns.Add("Clave");
                    dt.Columns.Add("Valor");
                    foreach (var prop in root.EnumerateObject())
                        dt.Rows.Add(prop.Name, prop.Value.ToString());
                }
            }
            catch
            {
                dt = SingleColumnTable(content);
            }
            return dt;
        }

        // ── XML ──────────────────────────────────────────────────────────────
        private static DataTable ParseXml(string content)
        {
            var dt = new DataTable();
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(content);

                // Buscar el primer nivel de nodos repetidos (los "registros")
                XmlNodeList? records = null;
                if (doc.DocumentElement != null)
                {
                    // Intenta primer hijo repetido
                    var firstChild = doc.DocumentElement.FirstChild;
                    if (firstChild != null)
                        records = doc.DocumentElement.SelectNodes(firstChild.Name);
                }

                if (records == null || records.Count == 0)
                {
                    // Fallback: mostrar como árbol plano
                    dt.Columns.Add("Nodo");
                    dt.Columns.Add("Atributos");
                    dt.Columns.Add("Valor");
                    FlattenXml(doc.DocumentElement, dt, "");
                    return dt;
                }

                // Recopilar todas las columnas (atributos + hijos de texto)
                var colSet = new LinkedList<string>();
                foreach (XmlNode rec in records)
                {
                    foreach (XmlAttribute attr in rec.Attributes!)
                        if (!colSet.Contains("@" + attr.Name)) colSet.AddLast("@" + attr.Name);
                    foreach (XmlNode child in rec.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && !colSet.Contains(child.Name))
                            colSet.AddLast(child.Name);
                }

                foreach (var col in colSet) dt.Columns.Add(col);

                foreach (XmlNode rec in records)
                {
                    var row = dt.NewRow();
                    foreach (XmlAttribute attr in rec.Attributes!)
                        if (dt.Columns.Contains("@" + attr.Name))
                            row["@" + attr.Name] = attr.Value;
                    foreach (XmlNode child in rec.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && dt.Columns.Contains(child.Name))
                            row[child.Name] = child.InnerText;
                    dt.Rows.Add(row);
                }
            }
            catch
            {
                dt = SingleColumnTable(content);
            }
            return dt;
        }

        private static void FlattenXml(XmlNode? node, DataTable dt, string prefix)
        {
            if (node == null) return;
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                string name = string.IsNullOrEmpty(prefix) ? child.Name : prefix + "/" + child.Name;
                string attrs = string.Join(", ", child.Attributes!.Cast<XmlAttribute>().Select(a => $"{a.Name}={a.Value}"));
                string val = child.HasChildNodes && child.FirstChild!.NodeType == XmlNodeType.Text ? child.InnerText : "";
                dt.Rows.Add(name, attrs, val);
                if (child.HasChildNodes && !(child.FirstChild!.NodeType == XmlNodeType.Text && child.ChildNodes.Count == 1))
                    FlattenXml(child, dt, name);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ANÁLISIS — duplicados, fechas, vacíos
        // ════════════════════════════════════════════════════════════════════
        private void AnalyzeTable()
        {
            duplicateRows.Clear();
            dateIssues.Clear();
            emptyFields.Clear();

            // Duplicados: comparar todas las filas entre sí (por contenido)
            var seen = new Dictionary<string, int>(); // hash → primer índice
            for (int r = 0; r < masterTable.Rows.Count; r++)
            {
                string key = string.Join("│", masterTable.Rows[r].ItemArray.Select(x => x?.ToString() ?? ""));
                if (seen.TryGetValue(key, out int orig))
                {
                    if (!duplicateRows.Contains(orig)) duplicateRows.Add(orig);
                    duplicateRows.Add(r);
                }
                else seen[key] = r;
            }

            // Fechas y vacíos
            for (int r = 0; r < masterTable.Rows.Count; r++)
            {
                for (int c = 0; c < masterTable.Columns.Count; c++)
                {
                    string val = masterTable.Rows[r][c]?.ToString() ?? "";

                    // Vacío
                    if (string.IsNullOrWhiteSpace(val))
                    { emptyFields.Add((r, c)); continue; }

                    // Fecha mal formateada
                    string? fixedDate = DetectAndFixDate(val);
                    if (fixedDate != null && fixedDate != val)
                        dateIssues.Add((r, c, val, fixedDate));
                }
            }
        }

        // Detecta si un string es una fecha en formato distinto a yyyy-MM-dd
        // Devuelve la fecha en formato correcto o null si no aplica.
        private static string? DetectAndFixDate(string val)
        {
            // ¿Ya está en formato correcto yyyy-MM-dd?
            if (Regex.IsMatch(val, @"^\d{4}-\d{2}-\d{2}$")) return null;

            // Patrón dd/mm/yyyy o mm/dd/yyyy etc.
            var m1 = Regex.Match(val, @"^(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})$");
            if (m1.Success)
            {
                int a = int.Parse(m1.Groups[1].Value);
                int b = int.Parse(m1.Groups[2].Value);
                int y = int.Parse(m1.Groups[3].Value);
                if (y < 100) y += 2000;
                // Heurística: si primer número > 12 es día
                if (a > 12 && b <= 12 && b >= 1)
                    return TryDate(y, b, a);
                // Si segundo número > 12 es día
                if (b > 12 && a <= 12 && a >= 1)
                    return TryDate(y, a, b);
                // Asumir dd/mm/yyyy
                return TryDate(y, b, a);
            }

            // Patrón yyyy/mm/dd con separador no guión
            var m2 = Regex.Match(val, @"^(\d{4})[/\.](\d{1,2})[/\.](\d{1,2})$");
            if (m2.Success)
                return TryDate(int.Parse(m2.Groups[1].Value), int.Parse(m2.Groups[2].Value), int.Parse(m2.Groups[3].Value));

            // Patrón textual: 15 Jan 2024
            var months = new[] { "", "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
            var m3 = Regex.Match(val, @"^(\d{1,2})\s+([A-Za-z]+)\s+(\d{2,4})$");
            if (m3.Success)
            {
                int day = int.Parse(m3.Groups[1].Value);
                int mo = Array.FindIndex(months, x => x == m3.Groups[2].Value.ToLower().Substring(0, 3));
                int yr = int.Parse(m3.Groups[3].Value);
                if (yr < 100) yr += 2000;
                if (mo > 0) return TryDate(yr, mo, day);
            }

            return null;
        }

        private static string? TryDate(int y, int m, int d)
        {
            try { return new DateTime(y, m, d).ToString("yyyy-MM-dd"); }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOSTRAR TABLA
        // ════════════════════════════════════════════════════════════════════
        private void ApplyDisplayTable(DataTable source)
        {
            displayTable = source;
            grid.DataSource = null;
            grid.DataSource = displayTable;

            // Ajustar anchos
            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.Programmatic;
                col.Width = Math.Min(300, Math.Max(80, col.Width));
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
        }

        // Colorear celdas según análisis
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Encontrar índice original si es vista filtrada
            int origRow = e.RowIndex; // en displayTable mismo orden por ahora

            bool isDup = duplicateRows.Contains(origRow);
            bool isEmpty = emptyFields.Any(x => x.Row == origRow && x.Col == e.ColumnIndex);
            bool isDate = dateIssues.Any(x => x.Row == origRow && x.Col == e.ColumnIndex);

            if (isDup)
            {
                e.CellStyle.BackColor = Color.FromArgb(60, 20, 20);
                e.CellStyle.ForeColor = Color.FromArgb(255, 140, 140);
            }
            if (isEmpty)
            {
                e.CellStyle.BackColor = Color.FromArgb(40, 30, 10);
                e.CellStyle.ForeColor = Color.FromArgb(200, 160, 60);
                e.Value = "(vacío)";
                e.FormattingApplied = true;
            }
            if (isDate)
            {
                e.CellStyle.BackColor = Color.FromArgb(20, 40, 60);
                e.CellStyle.ForeColor = Color.FromArgb(80, 180, 255);
            }

            // Header de fila: número
            if (grid.RowHeadersVisible)
                grid.Rows[e.RowIndex].HeaderCell.Value = (e.RowIndex + 1).ToString();
        }

        private void UpdateStatus()
        {
            int total = masterTable.Rows.Count;
            int dups = duplicateRows.Count;
            int dates = dateIssues.Count;
            int empties = emptyFields.Count;

            var parts = new List<string> { $"📄 {total} filas" };
            if (dups > 0) parts.Add($"🔴 {dups} duplicados");
            if (dates > 0) parts.Add($"🔵 {dates} fechas a normalizar");
            if (empties > 0) parts.Add($"🟡 {empties} campos vacíos");
            if (dups == 0 && dates == 0 && empties == 0) parts.Add("✅ Sin problemas detectados");

            statusLabel.Text = "  " + string.Join("   ·   ", parts);
        }

        private void ShowAnalysisPopup()
        {
            int dups = duplicateRows.Count;
            int dates = dateIssues.Count;
            int empties = emptyFields.Count;

            if (dups == 0 && dates == 0 && empties == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Se detectaron problemas en \"{Path.GetFileName(filePath)}\":");
            sb.AppendLine();

            if (dups > 0)
            {
                sb.AppendLine($"🔴  DUPLICADOS — {dups} fila(s) repetida(s)");
                foreach (int r in duplicateRows.Take(5))
                {
                    string preview = string.Join(" | ", masterTable.Rows[r].ItemArray
                        .Take(3).Select(x => x?.ToString()?.Trim() ?? ""));
                    sb.AppendLine($"     Fila {r + 1}: {preview}…");
                }
                if (dups > 5) sb.AppendLine($"     … y {dups - 5} más");
                sb.AppendLine();
            }

            if (dates > 0)
            {
                sb.AppendLine($"🔵  FECHAS MAL FORMATEADAS — {dates} celda(s)");
                foreach (var (row, col, orig, fixd) in dateIssues.Take(5))
                    sb.AppendLine($"     Fila {row + 1}, col \"{masterTable.Columns[col].ColumnName}\": \"{orig}\" → \"{fixd}\"");
                if (dates > 5) sb.AppendLine($"     … y {dates - 5} más");
                sb.AppendLine();
            }

            if (empties > 0)
            {
                sb.AppendLine($"🟡  CAMPOS VACÍOS — {empties} celda(s) sin valor");
                var byCol = emptyFields.GroupBy(x => x.Col)
                    .Select(g => $"\"{masterTable.Columns[g.Key].ColumnName}\" ({g.Count()})");
                sb.AppendLine($"     Columnas afectadas: {string.Join(", ", byCol)}");
                sb.AppendLine();
            }

            sb.AppendLine("Las celdas afectadas están resaltadas en la tabla.");
            sb.AppendLine("Usa \"Guardar copia corregida\" para aplicar todas las correcciones.");

            MessageBox.Show(sb.ToString(), "Análisis del archivo",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTRADO
        // ════════════════════════════════════════════════════════════════════
        private void PopulateFilterCombo()
        {
            filterColumnCombo.Items.Clear();
            filterColumnCombo.Items.Add("— Todas las columnas —");
            foreach (DataColumn col in masterTable.Columns)
                filterColumnCombo.Items.Add(col.ColumnName);
            filterColumnCombo.SelectedIndex = 0;
        }

        private void ApplyFilter()
        {
            string query = filterBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) { ClearFilter(); return; }

            string colSel = filterColumnCombo.SelectedIndex > 0
                ? filterColumnCombo.SelectedItem!.ToString()!
                : "";

            var filtered = masterTable.Clone(); // mismo esquema
            foreach (DataRow row in masterTable.Rows)
            {
                bool match = false;
                if (string.IsNullOrEmpty(colSel))
                {
                    // Buscar en todas las columnas
                    foreach (var item in row.ItemArray)
                        if (item?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        { match = true; break; }
                }
                else
                {
                    match = row[colSel]?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (match) filtered.ImportRow(row);
            }

            ApplyDisplayTable(filtered);
            statusLabel.Text = $"  🔍 Mostrando {filtered.Rows.Count} de {masterTable.Rows.Count} filas  ·  filtro: \"{query}\"";
        }

        private void ClearFilter()
        {
            filterBox.Text = "";
            filterColumnCombo.SelectedIndex = 0;
            ApplyDisplayTable(masterTable);
            UpdateStatus();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ORDENAR
        // ════════════════════════════════════════════════════════════════════
        private int lastSortCol = -1;
        private bool sortAsc = true;

        private void SortByColumn(int colIdx)
        {
            if (colIdx < 0 || colIdx >= displayTable.Columns.Count) return;
            sortAsc = (colIdx == lastSortCol) ? !sortAsc : true;
            lastSortCol = colIdx;

            string colName = displayTable.Columns[colIdx].ColumnName;
            var view = displayTable.DefaultView;
            view.Sort = $"[{colName}] {(sortAsc ? "ASC" : "DESC")}";
            grid.DataSource = null;
            grid.DataSource = view.ToTable();
        }

        // ════════════════════════════════════════════════════════════════════
        //  GUARDAR COPIA CORREGIDA
        // ════════════════════════════════════════════════════════════════════
        private void SaveFixedCopy()
        {
            // Construir tabla corregida
            var fixed_table = masterTable.Copy();

            // 1. Normalizar fechas
            foreach (var (row, col, orig, fixd) in dateIssues)
                fixed_table.Rows[row][col] = fixd;

            // 2. Marcar vacíos como "(vacío)"
            foreach (var (row, col) in emptyFields)
                fixed_table.Rows[row][col] = "(vacío)";

            // 3. Eliminar duplicados (mantener primera ocurrencia)
            var toRemove = duplicateRows
                .GroupBy(r => string.Join("│", masterTable.Rows[r].ItemArray.Select(x => x?.ToString() ?? "")))
                .SelectMany(g => g.Skip(1))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            foreach (int r in toRemove)
                if (r < fixed_table.Rows.Count)
                    fixed_table.Rows[r].Delete();
            fixed_table.AcceptChanges();

            // Generar nombre
            string dir = Path.GetDirectoryName(filePath)!;
            string name = Path.GetFileNameWithoutExtension(filePath);
            string origExt = Path.GetExtension(filePath);
            string destPath = Path.Combine(dir, $"{name}_corregido{origExt}");

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar copia corregida",
                InitialDirectory = dir,
                FileName = Path.GetFileName(destPath),
                Filter = GetSaveFilter()
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string content = origExt switch
                {
                    ".csv" => TableToCsv(fixed_table),
                    ".txt" => TableToTsv(fixed_table),
                    ".json" => TableToJson(fixed_table),
                    ".xml" => TableToXml(fixed_table),
                    _ => TableToCsv(fixed_table)
                };
                File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
                MessageBox.Show(
                    $"Copia corregida guardada:\n{dlg.FileName}\n\n" +
                    $"• {dateIssues.Count} fechas normalizadas\n" +
                    $"• {emptyFields.Count} campos vacíos marcados\n" +
                    $"• {toRemove.Count} duplicados eliminados",
                    "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetSaveFilter() => ext switch
        {
            ".csv" => "CSV (*.csv)|*.csv|Todos|*.*",
            ".txt" => "Texto (*.txt)|*.txt|Todos|*.*",
            ".json" => "JSON (*.json)|*.json|Todos|*.*",
            ".xml" => "XML (*.xml)|*.xml|Todos|*.*",
            _ => "Todos|*.*"
        };

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR A FORMATO ELEGIDO
        // ════════════════════════════════════════════════════════════════════
        private void ExportAs(string targetExt)
        {
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string filter = targetExt switch
            {
                ".csv" => "CSV (*.csv)|*.csv|Todos|*.*",
                ".json" => "JSON (*.json)|*.json|Todos|*.*",
                ".txt" => "Texto separado por tabuladores (*.txt)|*.txt|Todos|*.*",
                ".xml" => "XML (*.xml)|*.xml|Todos|*.*",
                _ => "Todos|*.*"
            };

            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar como {targetExt.TrimStart('.').ToUpper()}",
                Filter = filter,
                FileName = $"{baseName}_exportado{targetExt}",
                InitialDirectory = Path.GetDirectoryName(filePath)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string content = targetExt switch
                {
                    ".csv" => TableToCsv(displayTable),
                    ".json" => TableToJson(displayTable),
                    ".txt" => TableToTsv(displayTable),
                    ".xml" => TableToXml(displayTable),
                    _ => TableToCsv(displayTable)
                };
                File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
                MessageBox.Show(
                    $"Exportado correctamente:\n{dlg.FileName}\n\n{displayTable.Rows.Count} filas · {displayTable.Columns.Count} columnas",
                    $"Exportado como {targetExt.TrimStart('.').ToUpper()}",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  SERIALIZACIÓN
        // ════════════════════════════════════════════════════════════════════
        private static string TableToCsv(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => $"\"{Esc(c.ColumnName)}\"")));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join(",", row.ItemArray.Select(x => $"\"{Esc(x?.ToString() ?? "")}\"")));
            return sb.ToString();
        }

        private static string TableToTsv(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join("\t", row.ItemArray.Select(x => x?.ToString() ?? "")));
            return sb.ToString();
        }

        private static string TableToJson(DataTable dt)
        {
            var rows = new List<Dictionary<string, string?>>();
            foreach (DataRow row in dt.Rows)
            {
                var dict = new Dictionary<string, string?>();
                foreach (DataColumn col in dt.Columns)
                    dict[col.ColumnName] = row[col]?.ToString();
                rows.Add(dict);
            }
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string TableToXml(DataTable dt)
        {
            dt.TableName = "Records";
            foreach (DataColumn col in dt.Columns)
                col.ColumnMapping = MappingType.Element;
            using var sw = new StringWriter();
            dt.WriteXml(sw, XmlWriteMode.WriteSchema);
            // Simplify: without schema
            using var sw2 = new StringWriter();
            dt.WriteXml(sw2);
            return sw2.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static List<string> SplitLines(string content) =>
            content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                   .Where(l => !string.IsNullOrEmpty(l)).ToList();

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuote = false;
            var cur = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    { cur.Append('"'); i++; }
                    else inQuote = !inQuote;
                }
                else if (c == ',' && !inQuote)
                { result.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            result.Add(cur.ToString());
            return result;
        }

        private static string Esc(string s) => s.Replace("\"", "\"\"");

        private static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }
    }
}