using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace FileExplorerr
{
    public class FileViewerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Label fileInfoLabel = null!;
        private Panel filterPanel = null!;
        private TextBox filterBox = null!;
        private ComboBox filterColumnCombo = null!;
        private DataGridView grid = null!;
        private Label statusLabel = null!;
        private Panel loadingPanel = null!;
        private Label loadingLabel = null!;
        private Button btnShareEmail = null!;

        // ── Estado ───────────────────────────────────────────────────────────
        private readonly string filePath;
        private readonly string ext;
        private DataTable masterTable = new();
        private DataTable displayTable = new();
        private List<int> duplicateRows = new();
        private List<(int Row, int Col, string Original, string Fixed)> dateIssues = new();
        private List<(int Row, int Col)> emptyFields = new();
        private List<(int Row, int Col, string Original, string Fixed)> phoneIssues = new();
        private List<(int Row, int Col, string Original)> emailIssues = new();
        private List<int> columnMismatchRows = new();
        private int expectedColumnCount = 0;

        private static readonly Regex[] DatePatterns = {
            new(@"\b(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})\b"),
            new(@"\b(\d{2,4})[/\-\.](\d{1,2})[/\-\.](\d{1,2})\b"),
            new(@"\b(\d{1,2})\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(\d{2,4})\b", RegexOptions.IgnoreCase),
        };

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

        private static readonly string[] PhoneKeywords =
            { "phone", "telefono", "teléfono", "tel", "celular", "mobile", "cell",
              "fono", "movil", "móvil", "whatsapp", "contacto", "numero", "número" };
        private static readonly string[] EmailKeywords =
            { "email", "correo", "mail", "e-mail", "correo electronico",
              "correo electrónico", "electronic", "address" };

        private Dictionary<int, (bool IsNumeric, bool IsCurrency, bool IsPhone)> columnNumericInfo = new();
        private static readonly string[] CurrencyKeywords =
            { "price", "precio", "cost", "costo", "amount", "monto", "total", "salary", "salario",
              "revenue", "ingreso", "venta", "sale", "fee", "value", "valor", "budget", "expense", "gasto" };

        private List<(int Row, int ExpectedCols, int ActualCols)> columnMismatchDetails = new();

        public FileViewerForm(string path) { filePath = path; ext = Path.GetExtension(path).ToLower(); BuildUI(); _ = LoadFileAsync(); }

        private void BuildUI()
        {
            Text = $"Visor — {Path.GetFileName(filePath)}";
            Size = new Size(1000, 660);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;

            // ── Loading overlay ───────────────────────────────────────────────
            loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(200, 18, 18, 22), Visible = false };
            loadingLabel = new Label { Text = "Cargando...", Font = Theme.FontBodyBold, ForeColor = Theme.Accent, BackColor = Color.Transparent, AutoSize = true };
            loadingPanel.Controls.Add(loadingLabel);
            loadingPanel.Resize += (s, e) => CenterLabel();

            // ── Top ──────────────────────────────────────────────────────────
            var topPanel = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(12, 0, 0, 0) };
            fileInfoLabel = new Label { Dock = DockStyle.Fill, Font = Theme.FontBody, ForeColor = Theme.TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            topPanel.Controls.Add(fileInfoLabel);

            // ── Filter ───────────────────────────────────────────────────────
            filterPanel = new Panel { Height = 38, Dock = DockStyle.Top, BackColor = Theme.BgBase, Padding = new Padding(8, 4, 8, 4) };
            filterColumnCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgElevated, ForeColor = Theme.TextPrimary, Font = Theme.FontBody, Width = 150, Location = new Point(8, 7), FlatStyle = FlatStyle.Flat };
            filterBox = Theme.MakeTextBox("Buscar..."); filterBox.Width = 240; filterBox.Location = new Point(168, 7);
            filterBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };
            var filterBtn = Theme.MakeButton("Buscar", 70, Theme.ButtonKind.Primary); filterBtn.Location = new Point(418, 5); filterBtn.Click += (s, e) => ApplyFilter();
            var clearBtn = Theme.MakeButton("Limpiar", 70); clearBtn.Location = new Point(494, 5); clearBtn.Click += (s, e) => ClearFilter();
            filterPanel.Controls.AddRange(new Control[] { filterColumnCombo, filterBox, filterBtn, clearBtn });

            // ── Grid ─────────────────────────────────────────────────────────
            grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = true, RowHeadersWidth = 44, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ScrollBars = ScrollBars.Both };
            Theme.StyleGrid(grid);
            grid.RowHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.BgSurface, ForeColor = Theme.TextMuted, Font = Theme.FontSmall };
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellFormatting += Grid_CellFormatting;
            grid.ColumnHeaderMouseClick += (s, e) => SortByColumn(e.ColumnIndex);

            // ── Bottom ───────────────────────────────────────────────────────
            var bottomPanel = new Panel { Height = 108, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };
            var rowTop = new Panel { Height = 34, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(10, 4, 8, 0) };
            statusLabel = new Label { Dock = DockStyle.Fill, ForeColor = Theme.TextSecondary, Font = Theme.FontBody, TextAlign = ContentAlignment.MiddleLeft };
            var saveBtn = Theme.MakeButton("Guardar copia corregida", 160, Theme.ButtonKind.Success); saveBtn.Dock = DockStyle.Right; saveBtn.Click += (s, e) => SaveFixedCopy();
            rowTop.Controls.Add(statusLabel); rowTop.Controls.Add(saveBtn);

            // ── Fila exportar ─────────────────────────────────────────────────
            var rowBot = new Panel { Height = 36, Dock = DockStyle.Bottom, BackColor = Theme.BgElevated, Padding = new Padding(10, 4, 8, 4) };
            var expLabel = new Label { Text = "Exportar:", Dock = DockStyle.Left, Width = 64, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, TextAlign = ContentAlignment.MiddleLeft };
            var expCsv = MakeExportButton("CSV", Color.FromArgb(20, 90, 70), Color.FromArgb(56, 210, 170)); expCsv.Dock = DockStyle.Left; expCsv.Click += (s, e) => ExportAs(".csv");
            var expJson = MakeExportButton("JSON", Color.FromArgb(80, 55, 10), Color.FromArgb(230, 160, 40)); expJson.Dock = DockStyle.Left; expJson.Click += (s, e) => ExportAs(".json");
            var expTxt = MakeExportButton("TXT", Color.FromArgb(25, 40, 90), Color.FromArgb(90, 140, 240)); expTxt.Dock = DockStyle.Left; expTxt.Click += (s, e) => ExportAs(".txt");
            var expXml = MakeExportButton("XML", Color.FromArgb(55, 20, 80), Color.FromArgb(180, 80, 230)); expXml.Dock = DockStyle.Left; expXml.Click += (s, e) => ExportAs(".xml");
            var expBD = MakeExportButton("→ BD SQL", Color.FromArgb(10, 32, 58), Color.FromArgb(125, 211, 252)); expBD.Width = 80; expBD.Dock = DockStyle.Left; expBD.Click += async (s, e) => await ExportarABD();

            btnShareEmail = MakeExportButton("✉ Email", Color.FromArgb(20, 60, 90), Color.FromArgb(100, 180, 255));
            btnShareEmail.Width = 75;
            btnShareEmail.Dock = DockStyle.Left;
            btnShareEmail.Click += BtnShareEmail_Click;

            rowBot.Controls.Add(expXml);
            rowBot.Controls.Add(expTxt);
            rowBot.Controls.Add(expJson);
            rowBot.Controls.Add(expCsv);
            rowBot.Controls.Add(expBD);
            rowBot.Controls.Add(btnShareEmail);
            rowBot.Controls.Add(expLabel);

            // ── Segunda fila: exportación Office ─────────────────────────────
            var rowOffice = new Panel { Height = 36, Dock = DockStyle.Bottom, BackColor = Theme.BgElevated, Padding = new Padding(10, 4, 8, 4) };
            var offLabel = new Label { Text = "Office/PDF:", Dock = DockStyle.Left, Width = 72, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, TextAlign = ContentAlignment.MiddleLeft };
            var expXlsx = MakeExportButton("📊 Excel", Color.FromArgb(16, 72, 32), Color.FromArgb(80, 200, 100)); expXlsx.Dock = DockStyle.Left; expXlsx.Click += (s, e) => ExportarOffice(".xlsx");
            var expDocx = MakeExportButton("📝 Word", Color.FromArgb(12, 48, 96), Color.FromArgb(80, 150, 240)); expDocx.Dock = DockStyle.Left; expDocx.Click += (s, e) => ExportarOffice(".docx");
            var expPptx = MakeExportButton("📋 PowerPoint", Color.FromArgb(80, 30, 10), Color.FromArgb(230, 100, 60)); expPptx.Dock = DockStyle.Left; expPptx.Click += (s, e) => ExportarOffice(".pptx");
            var expPdf = MakeExportButton("🗒 PDF", Color.FromArgb(70, 10, 10), Color.FromArgb(220, 70, 70)); expPdf.Dock = DockStyle.Left; expPdf.Click += (s, e) => ExportarOffice(".pdf");
            rowOffice.Controls.Add(expPdf); rowOffice.Controls.Add(expPptx); rowOffice.Controls.Add(expDocx); rowOffice.Controls.Add(expXlsx); rowOffice.Controls.Add(offLabel);

            bottomPanel.Controls.Add(rowOffice);
            bottomPanel.Controls.Add(rowBot);
            bottomPanel.Controls.Add(rowTop);

            Controls.Add(loadingPanel);
            Controls.Add(grid);
            Controls.Add(filterPanel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
            loadingPanel.BringToFront();
        }

        // ── Un solo btnShareEmail_Click ───────────────────────────────────────
        private void BtnShareEmail_Click(object? sender, EventArgs e)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show("El archivo no existe.", "Archivo no encontrado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var emailForm = new EmailForm(filePath);
            emailForm.ShowDialog(this);
        }

        private void CenterLabel()
        {
            if (loadingLabel == null || loadingPanel == null) return;
            loadingLabel.Location = new Point(
                (loadingPanel.Width - loadingLabel.Width) / 2,
                (loadingPanel.Height - loadingLabel.Height) / 2);
        }

        private void ShowLoading(string text = "Cargando...")
        {
            loadingLabel.Text = text;
            loadingPanel.Visible = true;
            CenterLabel();
            loadingPanel.BringToFront();
        }

        private void HideLoading() => loadingPanel.Visible = false;

        private static Button MakeExportButton(string text, Color bgColor, Color accentColor)
        {
            var btn = new Button { Text = text, Width = 62, Height = 28, BackColor = bgColor, ForeColor = accentColor, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = accentColor; btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR A BASE DE DATOS
        // ════════════════════════════════════════════════════════════════════
        private async Task ExportarABD()
        {
            if (masterTable == null || masterTable.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos cargados para exportar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SqlViewerForm? sqlViewer = null;
            foreach (Form f in Application.OpenForms)
                if (f is SqlViewerForm sv) { sqlViewer = sv; break; }
            if (sqlViewer == null)
            {
                var resultado = MessageBox.Show("No hay una ventana SQL abierta.\n¿Abrir el visor SQL para conectarte a una base de datos?", "Exportar a BD", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (resultado != DialogResult.Yes) return;
                sqlViewer = new SqlViewerForm();
                sqlViewer.Show();
                MessageBox.Show("Conéctate a PostgreSQL o MariaDB en la ventana SQL y luego cierra este mensaje para continuar.", "Paso 1 — Conectar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            string nombreSugerido = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_").ToLowerInvariant();
            await sqlViewer.ImportarDataTableABD(masterTable, nombreSugerido);
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGA ASÍNCRONA
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadFileAsync()
        {
            ShowLoading("Leyendo archivo...");
            try
            {
                var fi = new FileInfo(filePath);
                fileInfoLabel.Text = $"  {fi.Name}  ·  {FormatSize(fi.Length)}  ·  {fi.LastWriteTime:dd/MM/yyyy HH:mm}";

                string content = await Task.Run(() => File.ReadAllText(filePath));

                ShowLoading("Procesando datos...");
                masterTable = await Task.Run(() =>
                {
                    if (ext == ".csv") return ParseCsvWithMismatch(content);
                    return ext switch
                    {
                        ".json" => ParseJson(content),
                        ".xml" => ParseXml(content),
                        _ => ParseTxt(content)
                    };
                });

                ShowLoading("Analizando calidad...");
                await Task.Run(() => AnalyzeTable());

                PopulateFilterCombo();
                ApplyDisplayTable(masterTable);
                UpdateStatus();

                if (duplicateRows.Count > 0 || dateIssues.Count > 0 || emptyFields.Count > 0 ||
                    phoneIssues.Count > 0 || emailIssues.Count > 0 || columnMismatchRows.Count > 0)
                    ShowAnalysisPopup();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            finally
            {
                HideLoading();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PARSERS
        // ════════════════════════════════════════════════════════════════════
        private DataTable ParseCsvWithMismatch(string content)
        {
            var dt = new DataTable();
            var lines = SplitLines(content);
            if (lines.Count == 0) return dt;
            var headers = SplitCsvLine(lines[0]);
            expectedColumnCount = headers.Count;
            foreach (var h in headers)
                dt.Columns.Add(h.Trim('"', ' ').Length > 0 ? h.Trim('"', ' ') : $"Col{dt.Columns.Count + 1}");
            columnMismatchRows.Clear(); columnMismatchDetails.Clear();
            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = SplitCsvLine(lines[i]);
                if (cells.Count != expectedColumnCount)
                {
                    columnMismatchRows.Add(i - 1);
                    columnMismatchDetails.Add((i - 1, expectedColumnCount, cells.Count));
                }
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Count ? cells[c].Trim('"') : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static DataTable ParseTxt(string content)
        {
            var lines = SplitLines(content); if (lines.Count == 0) return SingleColumnTable(content);
            char delim = '\0'; string first = lines[0];
            if (first.Contains('\t')) delim = '\t'; else if (first.Contains('|')) delim = '|'; else if (first.Contains(';')) delim = ';'; else if (first.Contains(',') && lines.Count > 1) delim = ',';
            if (delim == '\0') return SingleColumnTable(content);
            var dt = new DataTable(); var headers = first.Split(delim);
            foreach (var h in headers) dt.Columns.Add(h.Trim().Length > 0 ? h.Trim() : $"Col{dt.Columns.Count + 1}");
            for (int i = 1; i < lines.Count; i++) { if (string.IsNullOrWhiteSpace(lines[i])) continue; var cells = lines[i].Split(delim); var row = dt.NewRow(); for (int c = 0; c < dt.Columns.Count; c++) row[c] = c < cells.Length ? cells[c].Trim() : ""; dt.Rows.Add(row); }
            return dt;
        }

        private static DataTable SingleColumnTable(string content) { var dt = new DataTable(); dt.Columns.Add("Línea"); foreach (var line in SplitLines(content)) if (!string.IsNullOrWhiteSpace(line)) dt.Rows.Add(line); return dt; }

        private static DataTable ParseJson(string content)
        {
            var dt = new DataTable();
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; foreach (var prop in elem.EnumerateObject()) if (!dt.Columns.Contains(prop.Name)) dt.Columns.Add(prop.Name); }
                    foreach (var elem in doc.RootElement.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; var row = dt.NewRow(); foreach (var prop in elem.EnumerateObject()) { if (!dt.Columns.Contains(prop.Name)) continue; row[prop.Name] = prop.Value.ValueKind switch { JsonValueKind.Null => "", JsonValueKind.Object => prop.Value.GetRawText(), JsonValueKind.Array => prop.Value.GetRawText(), _ => prop.Value.ToString() }; } dt.Rows.Add(row); }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    bool foundArray = false;
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var elem in prop.Value.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; foreach (var p in elem.EnumerateObject()) if (!dt.Columns.Contains(p.Name)) dt.Columns.Add(p.Name); }
                            foreach (var elem in prop.Value.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; var row = dt.NewRow(); foreach (var p in elem.EnumerateObject()) { if (!dt.Columns.Contains(p.Name)) continue; row[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? "" : p.Value.ToString(); } dt.Rows.Add(row); }
                            foundArray = true; break;
                        }
                    }
                    if (!foundArray) { dt.Columns.Add("Clave"); dt.Columns.Add("Valor"); foreach (var prop in doc.RootElement.EnumerateObject()) dt.Rows.Add(prop.Name, prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.GetRawText()); }
                }
            }
            catch { dt = SingleColumnTable(content); }
            return dt;
        }

        private static DataTable ParseXml(string content)
        {
            var dt = new DataTable();
            try
            {
                var doc = new XmlDocument(); doc.LoadXml(content);
                XmlNodeList? records = null;
                if (doc.DocumentElement != null) { var fc = doc.DocumentElement.FirstChild; if (fc != null) records = doc.DocumentElement.SelectNodes(fc.Name); }
                if (records == null || records.Count == 0) { dt.Columns.Add("Nodo"); dt.Columns.Add("Valor"); FlattenXml(doc.DocumentElement, dt, ""); return dt; }
                var colSet = new LinkedList<string>();
                foreach (XmlNode rec in records) { foreach (XmlAttribute attr in rec.Attributes!) if (!colSet.Contains("@" + attr.Name)) colSet.AddLast("@" + attr.Name); foreach (XmlNode child in rec.ChildNodes) if (child.NodeType == XmlNodeType.Element && !colSet.Contains(child.Name)) colSet.AddLast(child.Name); }
                foreach (var col in colSet) dt.Columns.Add(col);
                foreach (XmlNode rec in records) { var row = dt.NewRow(); foreach (XmlAttribute attr in rec.Attributes!) if (dt.Columns.Contains("@" + attr.Name)) row["@" + attr.Name] = attr.Value; foreach (XmlNode child in rec.ChildNodes) if (child.NodeType == XmlNodeType.Element && dt.Columns.Contains(child.Name)) row[child.Name] = child.InnerText; dt.Rows.Add(row); }
            }
            catch { dt = SingleColumnTable(content); }
            return dt;
        }

        private static void FlattenXml(XmlNode? node, DataTable dt, string prefix)
        {
            if (node == null) return;
            foreach (XmlNode child in node.ChildNodes) { if (child.NodeType != XmlNodeType.Element) continue; string name = string.IsNullOrEmpty(prefix) ? child.Name : prefix + "/" + child.Name; string val = child.HasChildNodes && child.FirstChild!.NodeType == XmlNodeType.Text ? child.InnerText : ""; dt.Rows.Add(name, val); if (child.HasChildNodes && !(child.FirstChild!.NodeType == XmlNodeType.Text && child.ChildNodes.Count == 1)) FlattenXml(child, dt, name); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DETECCIÓN DE TELÉFONO
        // ════════════════════════════════════════════════════════════════════
        private static bool IsPhoneColumn(DataTable dt, int colIndex)
        {
            string colName = dt.Columns[colIndex].ColumnName.ToLower();
            if (PhoneKeywords.Any(k => colName.Contains(k))) return true;
            var nonEmpty = dt.Rows.Cast<DataRow>().Select(r => r[colIndex]?.ToString()?.Trim() ?? "").Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (nonEmpty.Count == 0) return false;
            int phoneCount = nonEmpty.Count(v => LooksLikePhone(v));
            return (double)phoneCount / nonEmpty.Count >= 0.6;
        }

        private static bool LooksLikePhone(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!Regex.IsMatch(value, @"^[\d\s\+\-\(\)\.ext]{7,20}$", RegexOptions.IgnoreCase)) return false;
            string digitsOnly = new string(value.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 7 || digitsOnly.Length > 15) return false;
            var dotMatch = Regex.Match(value, @"\.(\d+)");
            if (dotMatch.Success && dotMatch.Groups[1].Value.TrimEnd('0').Length > 0) return false;
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ANÁLISIS
        // ════════════════════════════════════════════════════════════════════
        private void AnalyzeTable()
        {
            duplicateRows.Clear(); dateIssues.Clear(); emptyFields.Clear(); phoneIssues.Clear(); emailIssues.Clear();
            var phoneColumns = new HashSet<int>();
            var emailColumns = new HashSet<int>();
            for (int c = 0; c < masterTable.Columns.Count; c++)
            {
                if (IsPhoneColumn(masterTable, c)) phoneColumns.Add(c);
                string colName = masterTable.Columns[c].ColumnName.ToLower();
                if (EmailKeywords.Any(k => colName.Contains(k))) emailColumns.Add(c);
            }
            var seen = new Dictionary<string, int>();
            for (int r = 0; r < masterTable.Rows.Count; r++)
            {
                string key = string.Join("│", masterTable.Rows[r].ItemArray.Select(x => x?.ToString() ?? ""));
                if (seen.TryGetValue(key, out int orig)) { if (!duplicateRows.Contains(orig)) duplicateRows.Add(orig); duplicateRows.Add(r); } else seen[key] = r;
            }
            for (int r = 0; r < masterTable.Rows.Count; r++)
                for (int c = 0; c < masterTable.Columns.Count; c++)
                {
                    string val = masterTable.Rows[r][c]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(val)) { emptyFields.Add((r, c)); continue; }
                    if (phoneColumns.Contains(c)) { string? fp = ValidateAndFixPhone(val); if (fp != null) phoneIssues.Add((r, c, val, fp)); }
                    if (emailColumns.Contains(c)) { if (!IsValidEmail(val)) emailIssues.Add((r, c, val)); }
                    if (!phoneColumns.Contains(c)) { string? fd = DetectAndFixDate(val); if (fd != null && fd != val) dateIssues.Add((r, c, val, fd)); }
                }
        }

        private static string? ValidateAndFixPhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length == 0) return "⚠(sin dígitos)";
            if (digitsOnly.Length == 10) { if (raw.Trim() != digitsOnly) return digitsOnly; return null; }
            if (digitsOnly.Length > 10 && digitsOnly.Length <= 15) return digitsOnly.Substring(digitsOnly.Length - 10);
            if (digitsOnly.Length < 10 && digitsOnly.Length >= 7) return $"⚠{digitsOnly}({digitsOnly.Length}d)";
            return $"⚠{digitsOnly}(inválido)";
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            email = email.Trim();
            if (!email.Contains('@')) return false;
            var parts = email.Split('@');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1])) return false;
            if (!parts[1].Contains('.') || parts[1].StartsWith('.') || parts[1].EndsWith('.')) return false;
            string tld = parts[1].Substring(parts[1].LastIndexOf('.') + 1);
            return tld.Length >= 2;
        }

        private static string? DetectAndFixDate(string val)
        {
            if (Regex.IsMatch(val, @"^\d{4}-\d{2}-\d{2}$")) return null;
            var m1 = Regex.Match(val, @"^(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})$");
            if (m1.Success) { int a = int.Parse(m1.Groups[1].Value), b = int.Parse(m1.Groups[2].Value), y = int.Parse(m1.Groups[3].Value); if (y < 100) y += 2000; if (a > 12 && b <= 12) return TryDate(y, b, a); if (b > 12 && a <= 12) return TryDate(y, a, b); return TryDate(y, b, a); }
            var m2 = Regex.Match(val, @"^(\d{4})[/\.](\d{1,2})[/\.](\d{1,2})$");
            if (m2.Success) return TryDate(int.Parse(m2.Groups[1].Value), int.Parse(m2.Groups[2].Value), int.Parse(m2.Groups[3].Value));
            return null;
        }

        private static string? TryDate(int y, int m, int d) { try { return new DateTime(y, m, d).ToString("yyyy-MM-dd"); } catch { return null; } }

        // ════════════════════════════════════════════════════════════════════
        //  DISPLAY
        // ════════════════════════════════════════════════════════════════════
        private void ApplyDisplayTable(DataTable source)
        {
            displayTable = source;
            columnNumericInfo.Clear();
            grid.DataSource = null;
            grid.DataSource = displayTable;

            for (int c = 0; c < displayTable.Columns.Count; c++)
            {
                bool isPhoneCol = IsPhoneColumn(displayTable, c);
                if (isPhoneCol) { columnNumericInfo[c] = (false, false, true); continue; }
                string colName = displayTable.Columns[c].ColumnName.ToLower();
                bool isCurrencyCol = CurrencyKeywords.Any(k => colName.Contains(k));
                int numericCount = 0; bool hasCurrencySymbol = false;
                foreach (DataRow row in displayTable.Rows)
                {
                    string raw = row[c]?.ToString()?.Trim() ?? ""; if (string.IsNullOrWhiteSpace(raw)) continue;
                    string clean = raw.TrimStart('$', '€', '£', '¥', ' '); if (clean != raw) hasCurrencySymbol = true;
                    if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) numericCount++;
                }
                int nonEmpty = displayTable.Rows.Cast<DataRow>().Count(r => !string.IsNullOrWhiteSpace(r[c]?.ToString()));
                bool isNumeric = nonEmpty > 0 && (double)numericCount / nonEmpty >= 0.8;
                columnNumericInfo[c] = (isNumeric, isNumeric && (isCurrencyCol || hasCurrencySymbol), false);
            }
            foreach (DataGridViewColumn col in grid.Columns) { col.SortMode = DataGridViewColumnSortMode.Programmatic; col.Width = Math.Min(280, Math.Max(70, col.Width)); }
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            bool isDup = duplicateRows.Contains(e.RowIndex);
            bool isEmpty = emptyFields.Any(x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isDate = dateIssues.Any(x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isPhone = phoneIssues.Any(x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isEmail = emailIssues.Any(x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isColMismatch = columnMismatchRows.Contains(e.RowIndex);

            if (columnNumericInfo.TryGetValue(e.ColumnIndex, out var ni) && ni.IsNumeric && !ni.IsPhone && !isEmpty)
            {
                string raw = e.Value?.ToString()?.Trim() ?? "";
                string clean = raw.TrimStart('$', '€', '£', '¥', ' ').Replace(",", "");
                if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                {
                    numVal = Math.Round(numVal, 2);
                    e.Value = ni.IsCurrency ? $"${numVal:N2}" : (numVal == Math.Floor(numVal) ? numVal.ToString("N0") : numVal.ToString("N2"));
                    e.FormattingApplied = true;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    e.CellStyle.ForeColor = ni.IsCurrency ? Theme.Success : Color.FromArgb(160, 190, 230);
                }
            }

            if (isDup) { e.CellStyle.BackColor = Color.FromArgb(50, 25, 25); e.CellStyle.ForeColor = Theme.Danger; }
            if (isEmpty) { e.CellStyle.BackColor = Theme.WarningDim; e.CellStyle.ForeColor = Theme.Warning; e.Value = "(vacío)"; e.FormattingApplied = true; }
            if (isDate) { e.CellStyle.BackColor = Color.FromArgb(20, 35, 50); e.CellStyle.ForeColor = Theme.Accent; }
            if (isPhone) { e.CellStyle.BackColor = Color.FromArgb(50, 20, 50); e.CellStyle.ForeColor = Color.FromArgb(230, 130, 200); }
            if (isEmail) { e.CellStyle.BackColor = Color.FromArgb(50, 35, 15); e.CellStyle.ForeColor = Color.FromArgb(255, 170, 60); }
            if (isColMismatch && !isDup && !isEmpty && !isDate && !isPhone && !isEmail)
            { e.CellStyle.BackColor = Color.FromArgb(60, 20, 20); e.CellStyle.ForeColor = Color.FromArgb(255, 120, 100); }

            if (grid.RowHeadersVisible) grid.Rows[e.RowIndex].HeaderCell.Value = (e.RowIndex + 1).ToString();
        }

        private void UpdateStatus()
        {
            int total = masterTable.Rows.Count, dups = duplicateRows.Count, dates = dateIssues.Count,
                empties = emptyFields.Count, phones = phoneIssues.Count, emails = emailIssues.Count,
                colMis = columnMismatchRows.Count;
            var parts = new List<string> { $"{total} filas" };
            if (dups > 0) parts.Add($"{dups} duplicados");
            if (dates > 0) parts.Add($"{dates} fechas");
            if (empties > 0) parts.Add($"{empties} vacíos");
            if (phones > 0) parts.Add($"{phones} teléfonos");
            if (emails > 0) parts.Add($"{emails} emails");
            if (colMis > 0) parts.Add($"{colMis} col.desajustadas");
            if (dups == 0 && dates == 0 && empties == 0 && phones == 0 && emails == 0 && colMis == 0)
                parts.Add("Sin problemas");
            statusLabel.Text = "  " + string.Join("  ·  ", parts);
        }

        private void ShowAnalysisPopup()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Análisis de \"{Path.GetFileName(filePath)}\":\n");
            if (duplicateRows.Count > 0) sb.AppendLine($"• {duplicateRows.Count} fila(s) duplicada(s)");
            if (dateIssues.Count > 0) sb.AppendLine($"• {dateIssues.Count} fecha(s) a normalizar");
            if (emptyFields.Count > 0) sb.AppendLine($"• {emptyFields.Count} campo(s) vacío(s)");
            if (phoneIssues.Count > 0) { sb.AppendLine($"• {phoneIssues.Count} teléfono(s) con problemas:"); int shown = 0; foreach (var (r, c, orig, fix) in phoneIssues) { if (shown++ >= 5) { sb.AppendLine($"    ... y {phoneIssues.Count - 5} más"); break; } sb.AppendLine($"    Fila {r + 1}: \"{orig}\" → \"{fix}\""); } }
            if (emailIssues.Count > 0) { sb.AppendLine($"• {emailIssues.Count} email(s) inválido(s):"); int shown = 0; foreach (var (r, c, orig) in emailIssues) { if (shown++ >= 5) { sb.AppendLine($"    ... y {emailIssues.Count - 5} más"); break; } sb.AppendLine($"    Fila {r + 1}: \"{orig}\""); } }
            if (columnMismatchRows.Count > 0) { sb.AppendLine($"• {columnMismatchRows.Count} fila(s) con columnas incorrectas:"); int shown = 0; foreach (var (row, exp, act) in columnMismatchDetails) { if (shown++ >= 5) { sb.AppendLine($"    ... y {columnMismatchDetails.Count - 5} más"); break; } sb.AppendLine($"    Fila {row + 1}: esperadas {exp}, tiene {act}"); } }
            sb.AppendLine("\nLas celdas afectadas están resaltadas.");
            MessageBox.Show(sb.ToString(), "Análisis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTRADO / ORDEN
        // ════════════════════════════════════════════════════════════════════
        private void PopulateFilterCombo() { filterColumnCombo.Items.Clear(); filterColumnCombo.Items.Add("Todas"); foreach (DataColumn col in masterTable.Columns) filterColumnCombo.Items.Add(col.ColumnName); filterColumnCombo.SelectedIndex = 0; }

        private void ApplyFilter()
        {
            string query = filterBox.Text.Trim(); if (string.IsNullOrEmpty(query)) { ClearFilter(); return; }
            string colSel = filterColumnCombo.SelectedIndex > 0 ? filterColumnCombo.SelectedItem!.ToString()! : "";
            var filtered = masterTable.Clone();
            foreach (DataRow row in masterTable.Rows)
            {
                bool match = false;
                if (string.IsNullOrEmpty(colSel)) { foreach (var item in row.ItemArray) if (item?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; } }
                else match = row[colSel]?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                if (match) filtered.ImportRow(row);
            }
            ApplyDisplayTable(filtered);
            statusLabel.Text = $"  {filtered.Rows.Count} de {masterTable.Rows.Count} filas  ·  \"{query}\"";
        }

        private void ClearFilter() { filterBox.Text = ""; filterColumnCombo.SelectedIndex = 0; ApplyDisplayTable(masterTable); UpdateStatus(); }

        private int lastSortCol = -1; private bool sortAsc = true;
        private void SortByColumn(int colIdx)
        {
            if (colIdx < 0 || colIdx >= displayTable.Columns.Count) return;
            sortAsc = colIdx == lastSortCol ? !sortAsc : true; lastSortCol = colIdx;
            var view = displayTable.DefaultView;
            view.Sort = $"[{displayTable.Columns[colIdx].ColumnName}] {(sortAsc ? "ASC" : "DESC")}";
            grid.DataSource = null; grid.DataSource = view.ToTable();
        }

        // ════════════════════════════════════════════════════════════════════
        //  GUARDAR / EXPORTAR
        // ════════════════════════════════════════════════════════════════════
        private void SaveFixedCopy()
        {
            var ft = masterTable.Copy();
            foreach (var (r, c, _, f) in dateIssues) ft.Rows[r][c] = f;
            foreach (var (r, c, orig, f) in phoneIssues) { string digitsInFix = new string(f.Where(char.IsDigit).ToArray()); if (digitsInFix.Length == 10 && !f.StartsWith("⚠")) ft.Rows[r][c] = digitsInFix; else ft.Rows[r][c] = f; }
            foreach (var (r, c, orig) in emailIssues) ft.Rows[r][c] = $"⚠{orig}";
            foreach (var (r, c) in emptyFields) ft.Rows[r][c] = "(vacío)";
            var toRemove = duplicateRows.GroupBy(r => string.Join("│", masterTable.Rows[r].ItemArray.Select(x => x?.ToString() ?? ""))).SelectMany(g => g.Skip(1)).Distinct().OrderByDescending(x => x).ToList();
            foreach (int r in toRemove) if (r < ft.Rows.Count) ft.Rows[r].Delete();
            ft.AcceptChanges();

            string dir = Path.GetDirectoryName(filePath)!;
            using var dlg = new SaveFileDialog { Title = "Guardar corregida", InitialDirectory = dir, FileName = Path.GetFileNameWithoutExtension(filePath) + "_corregido" + ext, Filter = GetSaveFilter() };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(dlg.FileName, SerializeTable(ft, ext), Encoding.UTF8); MessageBox.Show($"Guardado: {dlg.FileName}", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ExportAs(string targetExt)
        {
            using var dlg = new SaveFileDialog { Title = $"Exportar como {targetExt.TrimStart('.').ToUpper()}", Filter = $"{targetExt.TrimStart('.').ToUpper()} (*{targetExt})|*{targetExt}|Todos|*.*", FileName = Path.GetFileNameWithoutExtension(filePath) + "_exportado" + targetExt };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(dlg.FileName, SerializeTable(displayTable, targetExt), Encoding.UTF8); MessageBox.Show($"Exportado: {dlg.FileName}", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ExportarOffice(string ext)
        {
            string titulo = Path.GetFileNameWithoutExtension(filePath);
            ExportadorOffice.ExportarConDialogo(displayTable, titulo, ext, this);
        }

        private string GetSaveFilter() => ext switch { ".csv" => "CSV|*.csv|Todos|*.*", ".txt" => "Texto|*.txt|Todos|*.*", ".json" => "JSON|*.json|Todos|*.*", ".xml" => "XML|*.xml|Todos|*.*", _ => "Todos|*.*" };

        private static string SerializeTable(DataTable dt, string ext) => ext switch
        {
            ".csv" => TableToCsv(dt),
            ".json" => TableToJson(dt),
            ".xml" => TableToXml(dt),
            _ => TableToTsv(dt)
        };

        private static string TableToCsv(DataTable dt) { var sb = new StringBuilder(); sb.AppendLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => $"\"{Esc(c.ColumnName)}\""))); foreach (DataRow row in dt.Rows) sb.AppendLine(string.Join(",", row.ItemArray.Select(x => $"\"{Esc(x?.ToString() ?? "")}\""))); return sb.ToString(); }
        private static string TableToTsv(DataTable dt) { var sb = new StringBuilder(); sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName))); foreach (DataRow row in dt.Rows) sb.AppendLine(string.Join("\t", row.ItemArray.Select(x => x?.ToString() ?? ""))); return sb.ToString(); }

        private static string TableToJson(DataTable dt)
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (DataRow row in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns) { string? val = row[col]?.ToString(); if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal)) d[col.ColumnName] = numVal; else if (val == "" || val == null) d[col.ColumnName] = null; else d[col.ColumnName] = val; }
                rows.Add(d);
            }
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        private static string TableToXml(DataTable dt) { dt.TableName = "Records"; using var sw = new StringWriter(); dt.WriteXml(sw); return sw.ToString(); }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static List<string> SplitLines(string c) => c.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Where(l => !string.IsNullOrEmpty(l)).ToList();

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>(); bool inQuote = false; var cur = new StringBuilder();
            for (int i = 0; i < line.Length; i++) { char c = line[i]; if (c == '"') { if (inQuote && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else inQuote = !inQuote; } else if (c == ',' && !inQuote) { result.Add(cur.ToString()); cur.Clear(); } else cur.Append(c); }
            result.Add(cur.ToString()); return result;
        }

        private static string Esc(string s) => s.Replace("\"", "\"\"");
        private static string FormatSize(long bytes) { string[] u = { "B", "KB", "MB", "GB" }; double v = bytes; int i = 0; while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; } return $"{v:0.##} {u[i]}"; }
    }
}