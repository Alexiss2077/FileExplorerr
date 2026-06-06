using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileExplorerr.Charts;

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

        // Nuevos controles para gráficas
        private DataChartPanel? _chartPanel;
        private ComboBox? _cmbChartGroup;
        private ComboBox? _cmbChartValue;
        private ComboBox? _cmbChartType;
        private ComboBox? _cmbChartMetric;

        // ── Estado ───────────────────────────────────────────────────────────
        private readonly string filePath;
        private readonly string ext;
        private DataTable masterTable = new();
        private DataTable displayTable = new();

        // Phase 5B: the seven list fields and expectedColumnCount have been
        // replaced by a single QualityReport field.
        private QualityReport _report = new();

        // Numeric/currency/phone column metadata used by Grid_CellFormatting
        // and populated by ApplyDisplayTable — NOT migrated.
        private Dictionary<int, (bool IsNumeric, bool IsCurrency, bool IsPhone)>
            columnNumericInfo = new();

        // Currency keyword heuristic — used by ApplyDisplayTable, NOT migrated.
        private static readonly string[] CurrencyKeywords =
        {
            "price", "precio", "cost", "costo", "amount", "monto", "total",
            "salary", "salario", "revenue", "ingreso", "venta", "sale",
            "fee", "value", "valor", "budget", "expense", "gasto"
        };

        public FileViewerForm(string path)
        {
            filePath = path;
            ext = Path.GetExtension(path).ToLower();
            BuildUI();
            _ = LoadFileAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = $"Visor \u2014 {Path.GetFileName(filePath)}";
            Size = new Size(1100, 700);
            MinimumSize = new Size(700, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;

            // Loading overlay
            loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 13, 15, 20),
                Visible = false
            };
            loadingLabel = new Label
            {
                Text = "Cargando...",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent2,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            loadingPanel.Controls.Add(loadingLabel);
            loadingPanel.Resize += (s, e) => CenterLabel();

            // Top info bar
            var topPanel = new Panel
            {
                Height = 42,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(14, 0, 0, 0)
            };
            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            topPanel.Controls.Add(fileInfoLabel);

            // Filter bar
            filterPanel = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = Theme.BgBase,
                Padding = new Padding(10, 6, 10, 6)
            };

            filterColumnCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Width = 150,
                Location = new Point(10, 7),
                FlatStyle = FlatStyle.Flat
            };

            filterBox = new TextBox
            {
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBody,
                PlaceholderText = "Buscar...",
                Width = 250,
                Location = new Point(168, 7),
                Height = 30
            };
            filterBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyFilter(); };

            var filterBtn = new Button
            {
                Text = "Buscar",
                Width = 80,
                Height = 30,
                Location = new Point(426, 7),
                BackColor = Theme.AccentBg,
                ForeColor = Theme.Accent2,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody,
                Cursor = Cursors.Hand
            };
            filterBtn.FlatAppearance.BorderColor = Color.FromArgb(124, 111, 247, 100);
            filterBtn.Click += (s, e) => ApplyFilter();

            var clearBtn = new Button
            {
                Text = "Limpiar",
                Width = 80,
                Height = 30,
                Location = new Point(514, 7),
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody,
                Cursor = Cursors.Hand
            };
            clearBtn.FlatAppearance.BorderColor = Theme.Border;
            clearBtn.Click += (s, e) => ClearFilter();

            filterPanel.Controls.AddRange(new Control[]
                { filterColumnCombo, filterBox, filterBtn, clearBtn });

            // Grid
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                RowHeadersVisible = true,
                RowHeadersWidth = 48,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both
            };
            Theme.StyleGrid(grid);
            grid.RowHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellFormatting += Grid_CellFormatting;
            grid.ColumnHeaderMouseClick += (s, e) => SortByColumn(e.ColumnIndex);

            // Bottom panel
            var bottomPanel = new Panel
            {
                Height = 116,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface
            };

            // Row 1: status + save
            var rowTop = new Panel
            {
                Height = 38,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(12, 4, 10, 0)
            };
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontBody,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var saveBtn = new Button
            {
                Text = "\U0001F4BE Guardar copia corregida",
                Width = 200,
                Height = 30,
                Dock = DockStyle.Right,
                BackColor = Theme.TealDim,
                ForeColor = Theme.Teal,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody,
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderColor = Color.FromArgb(52, 211, 153, 100);
            saveBtn.Click += (s, e) => SaveFixedCopy();
            rowTop.Controls.Add(statusLabel);
            rowTop.Controls.Add(saveBtn);

            // Row 2: basic export formats
            var rowBot = new Panel
            {
                Height = 38,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgElevated,
                Padding = new Padding(10, 4, 8, 4)
            };
            var expLabel = new Label
            {
                Text = "Exportar:",
                Dock = DockStyle.Left,
                Width = 70,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var expCsv = MakeExportButton("CSV", Color.FromArgb(20, 90, 70), Color.FromArgb(52, 211, 153), 80);
            var expJson = MakeExportButton("JSON", Color.FromArgb(80, 55, 10), Color.FromArgb(251, 191, 36), 80);
            var expTxt = MakeExportButton("TXT", Color.FromArgb(25, 40, 90), Color.FromArgb(96, 165, 250), 80);
            var expXml = MakeExportButton("XML", Color.FromArgb(55, 20, 80), Color.FromArgb(167, 139, 250), 80);
            var expBD = MakeExportButton("\u2192 BD SQL", Color.FromArgb(10, 32, 58), Color.FromArgb(125, 211, 252), 90);
            btnShareEmail = MakeExportButton("\u2709 Email", Color.FromArgb(20, 60, 90), Color.FromArgb(96, 165, 250), 90);

            expCsv.Dock = DockStyle.Left; expCsv.Click += (s, e) => ExportAs(".csv");
            expJson.Dock = DockStyle.Left; expJson.Click += (s, e) => ExportAs(".json");
            expTxt.Dock = DockStyle.Left; expTxt.Click += (s, e) => ExportAs(".txt");
            expXml.Dock = DockStyle.Left; expXml.Click += (s, e) => ExportAs(".xml");
            expBD.Dock = DockStyle.Left; expBD.Click += async (s, e) => await ExportarABD();
            btnShareEmail.Dock = DockStyle.Left;
            btnShareEmail.Click += BtnShareEmail_Click;

            rowBot.Controls.Add(expXml);
            rowBot.Controls.Add(expTxt);
            rowBot.Controls.Add(expJson);
            rowBot.Controls.Add(expCsv);
            rowBot.Controls.Add(expBD);
            rowBot.Controls.Add(btnShareEmail);
            rowBot.Controls.Add(expLabel);

            // Row 3: Office / PDF
            var rowOffice = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgElevated,
                Padding = new Padding(10, 5, 8, 5)
            };
            var offLabel = new Label
            {
                Text = "Office/PDF:",
                Dock = DockStyle.Left,
                Width = 78,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var expXlsx = MakeExportButton("\U0001F4CA Excel", Color.FromArgb(16, 72, 32), Color.FromArgb(80, 200, 100), 100);
            var expDocx = MakeExportButton("\U0001F4DD Word", Color.FromArgb(12, 48, 96), Color.FromArgb(80, 150, 240), 90);
            var expPptx = MakeExportButton("\U0001F4CB PowerPoint", Color.FromArgb(80, 30, 10), Color.FromArgb(230, 100, 60), 130);
            var expPdf = MakeExportButton("\U0001F5D2 PDF", Color.FromArgb(70, 10, 10), Color.FromArgb(220, 70, 70), 80);

            expXlsx.Dock = DockStyle.Left; expXlsx.Click += (s, e) => ExportarOffice(".xlsx");
            expDocx.Dock = DockStyle.Left; expDocx.Click += (s, e) => ExportarOffice(".docx");
            expPptx.Dock = DockStyle.Left; expPptx.Click += (s, e) => ExportarOffice(".pptx");
            expPdf.Dock = DockStyle.Left; expPdf.Click += (s, e) => ExportarOffice(".pdf");

            rowOffice.Controls.Add(expPdf);
            rowOffice.Controls.Add(expPptx);
            rowOffice.Controls.Add(expDocx);
            rowOffice.Controls.Add(expXlsx);
            rowOffice.Controls.Add(offLabel);

            bottomPanel.Controls.Add(rowOffice);
            bottomPanel.Controls.Add(rowBot);
            bottomPanel.Controls.Add(rowTop);

            Controls.Add(loadingPanel);

            // Pestañas (Datos y Gráfica)
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontBody
            };
            var tabData = new TabPage("Datos") { BackColor = Theme.BgBase, UseVisualStyleBackColor = false };
            var tabChart = new TabPage("Gráfica") { BackColor = Theme.BgBase, UseVisualStyleBackColor = false };

            grid.Dock = DockStyle.Fill;
            tabData.Controls.Add(grid);

            tabChart.Controls.Add(BuildChartTab());

            tabs.TabPages.AddRange(new[] { tabData, tabChart });
            Controls.Add(tabs);

            Controls.Add(filterPanel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
            loadingPanel.BringToFront();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CHART CONSTRUCTION & REFRESH
        // ════════════════════════════════════════════════════════════════════
        private Panel BuildChartTab()
        {
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgBase };

            // ── Barra de controles ────────────────────────────────────────────
            var toolbar = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(10, 7, 10, 7)
            };

            int x = 10;

            void AddLabel(string text)
            {
                toolbar.Controls.Add(new Label
                {
                    Text = text,
                    Location = new Point(x, 12),
                    AutoSize = true,
                    ForeColor = Theme.TextMuted,
                    Font = Theme.FontSmall,
                    BackColor = Color.Transparent
                });
                x += toolbar.Controls[^1].Width + 6;
            }

            ComboBox AddCombo(int width, string[] items)
            {
                var cmb = new ComboBox
                {
                    Location = new Point(x, 8),
                    Width = width,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Theme.BgElevated,
                    ForeColor = Theme.TextPrimary,
                    FlatStyle = FlatStyle.Flat,
                    Font = Theme.FontSmall
                };
                cmb.Items.AddRange(items);
                toolbar.Controls.Add(cmb);
                x += width + 10;
                return cmb;
            }

            AddLabel("Tipo:");
            _cmbChartType = AddCombo(100, new[] { "Columnas", "Barras", "Pastel" });
            _cmbChartType.SelectedIndex = 0;

            AddLabel("Agrupar por:");
            _cmbChartGroup = AddCombo(160, Array.Empty<string>());

            AddLabel("Métrica:");
            _cmbChartMetric = AddCombo(90, new[] { "Conteo", "Suma", "Promedio" });
            _cmbChartMetric.SelectedIndex = 0;

            AddLabel("Valor:");
            _cmbChartValue = AddCombo(160, Array.Empty<string>());

            var btnRefresh = new Button
            {
                Text = "↺",
                Location = new Point(x, 8),
                Size = new Size(30, 28),
                BackColor = Theme.AccentBg,
                ForeColor = Theme.Accent2,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (_, _) => RefreshChart();
            toolbar.Controls.Add(btnRefresh);

            // Actualizar cuando cambia cualquier combo
            _cmbChartType.SelectedIndexChanged += (_, _) => RefreshChart();
            _cmbChartGroup.SelectedIndexChanged += (_, _) => RefreshChart();
            _cmbChartMetric.SelectedIndexChanged += (_, _) => RefreshChart();
            _cmbChartValue.SelectedIndexChanged += (_, _) => RefreshChart();

            // ── Panel de la gráfica ───────────────────────────────────────────
            _chartPanel = new DataChartPanel { Dock = DockStyle.Fill };

            root.Controls.Add(_chartPanel);
            root.Controls.Add(toolbar);
            return root;
        }

        private void RefreshChart()
        {
            if (_chartPanel is null || _cmbChartGroup is null ||
                _cmbChartMetric is null || _cmbChartType is null)
                return;

            // Actualizar opciones de columnas cuando se tienen datos nuevos
            if (_cmbChartGroup.Items.Count == 0 && displayTable.Columns.Count > 0)
            {
                var cats = ChartDataBuilder.GetCategoricalColumns(displayTable);
                var nums = ChartDataBuilder.GetNumericColumns(displayTable);
                nums.Insert(0, "— (ninguna)");

                _cmbChartGroup.Items.Clear();
                _cmbChartGroup.Items.AddRange(cats.ToArray<object>());
                if (_cmbChartGroup.Items.Count > 0) _cmbChartGroup.SelectedIndex = 0;

                _cmbChartValue!.Items.Clear();
                _cmbChartValue.Items.AddRange(nums.ToArray<object>());
                _cmbChartValue.SelectedIndex = 0;
            }

            string group = _cmbChartGroup.Text;
            if (string.IsNullOrEmpty(group)) return;

            string? value = _cmbChartValue?.Text is "— (ninguna)" or "" ? null : _cmbChartValue?.Text;
            var metric = _cmbChartMetric.SelectedIndex switch
            {
                1 => ChartDataBuilder.ChartMetric.Sum,
                2 => ChartDataBuilder.ChartMetric.Average,
                _ => ChartDataBuilder.ChartMetric.Count
            };
            var chartType = _cmbChartType.SelectedIndex switch
            {
                1 => ChartType.Bars,
                2 => ChartType.Pie,
                _ => ChartType.Columns
            };

            var data = ChartDataBuilder.Build(displayTable, group, value, metric);
            string title = ChartDataBuilder.BuildTitle(group, value, metric);
            _chartPanel.SetData(data, chartType, title);
        }

        // ── Helper: export button factory ─────────────────────────────────────
        private static Button MakeExportButton(
            string text, Color bgColor, Color accentColor, int width = 75)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = bgColor,
                ForeColor = accentColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(2, 0, 2, 0)
            };
            btn.FlatAppearance.BorderColor = accentColor;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

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

        // ════════════════════════════════════════════════════════════════════
        //  EXPORT TO DATABASE
        // ════════════════════════════════════════════════════════════════════
        private async Task ExportarABD()
        {
            if (masterTable == null || masterTable.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos cargados para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SqlViewerForm? sqlViewer = null;
            foreach (Form f in Application.OpenForms)
                if (f is SqlViewerForm sv) { sqlViewer = sv; break; }

            if (sqlViewer == null)
            {
                var res = MessageBox.Show(
                    "No hay una ventana SQL abierta.\n\u00BFAbrir el visor SQL?",
                    "Exportar a BD",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;
                sqlViewer = new SqlViewerForm();
                sqlViewer.Show();
                MessageBox.Show(
                    "Con\u00E9ctate a la base de datos y luego cierra este mensaje.",
                    "Paso 1 \u2014 Conectar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            string nombreSugerido = Path.GetFileNameWithoutExtension(filePath)
                .Replace(" ", "_").ToLowerInvariant();
            await sqlViewer.ImportarDataTableABD(masterTable, nombreSugerido);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASYNC LOAD — Phase 5B: delegates to DataParsers + DataQualityAnalyzer
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadFileAsync()
        {
            ShowLoading("Leyendo archivo...");
            try
            {
                var fi = new FileInfo(filePath);
                fileInfoLabel.Text =
                    $"  {fi.Name}  \u00B7  {FileSize.Format(fi.Length)}" +
                    $"  \u00B7  {fi.LastWriteTime:dd/MM/yyyy HH:mm}";

                string content = await Task.Run(() => File.ReadAllText(filePath));

                ShowLoading("Procesando datos...");

                if (ext == ".csv")
                {
                    var csvResult = await Task.Run(() => DataParsers.ParseCsv(content));
                    masterTable = csvResult.Table;

                    ShowLoading("Analizando calidad...");
                    _report = await Task.Run(() =>
                        DataQualityAnalyzer.Analyze(
                            masterTable,
                            csvResult.MismatchRows,
                            csvResult.MismatchDetails));
                }
                else
                {
                    masterTable = await Task.Run(() => ext switch
                    {
                        ".json" => DataParsers.ParseJson(content),
                        ".xml" => DataParsers.ParseXml(content),
                        _ => DataParsers.ParseTxt(content)
                    });

                    ShowLoading("Analizando calidad...");
                    _report = await Task.Run(() =>
                        DataQualityAnalyzer.Analyze(masterTable));
                }

                PopulateFilterCombo();
                ApplyDisplayTable(masterTable);
                UpdateStatus();

                if (_report.HasIssues)
                    ShowAnalysisPopup();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            finally
            {
                HideLoading();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DISPLAY — unchanged from original (modified for Charts)
        // ════════════════════════════════════════════════════════════════════
        private void ApplyDisplayTable(DataTable source)
        {
            displayTable = source;
            columnNumericInfo.Clear();
            grid.DataSource = null;
            grid.DataSource = displayTable;

            for (int c = 0; c < displayTable.Columns.Count; c++)
            {
                bool isPhoneCol = DataQualityAnalyzer.IsPhoneColumn(displayTable, c);
                if (isPhoneCol) { columnNumericInfo[c] = (false, false, true); continue; }

                string colName = displayTable.Columns[c].ColumnName.ToLower();
                bool isCurrencyCol = CurrencyKeywords.Any(k => colName.Contains(k));

                int numericCount = 0;
                bool hasCurrencySymbol = false;
                foreach (DataRow row in displayTable.Rows)
                {
                    string raw = row[c]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string clean = raw.TrimStart('$', '\u20AC', '\u00A3', '\u00A5', ' ');
                    if (clean != raw) hasCurrencySymbol = true;
                    if (double.TryParse(clean,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out _))
                        numericCount++;
                }

                int nonEmpty = displayTable.Rows.Cast<DataRow>()
                    .Count(r => !string.IsNullOrWhiteSpace(r[c]?.ToString()));
                bool isNumeric = nonEmpty > 0 &&
                                 (double)numericCount / nonEmpty >= 0.8;

                columnNumericInfo[c] = (isNumeric,
                    isNumeric && (isCurrencyCol || hasCurrencySymbol),
                    false);
            }

            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.Programmatic;
                col.Width = Math.Min(280, Math.Max(70, col.Width));
            }

            // Resetear columnas del combo de gráfica para que se recarguen
            // con las columnas del nuevo dataset
            if (_cmbChartGroup is not null)
            {
                _cmbChartGroup.Items.Clear();
                _cmbChartValue?.Items.Clear();
            }
            RefreshChart();
        }

        // Phase 5B: all list references replaced with _report.<Property>
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            bool isDup = _report.DuplicateRows.Contains(e.RowIndex);
            bool isEmpty = _report.EmptyFields.Any(
                x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isDate = _report.DateIssues.Any(
                x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isPhone = _report.PhoneIssues.Any(
                x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isEmail = _report.EmailIssues.Any(
                x => x.Row == e.RowIndex && x.Col == e.ColumnIndex);
            bool isColMismatch = _report.ColumnMismatchRows.Contains(e.RowIndex);

            if (columnNumericInfo.TryGetValue(e.ColumnIndex, out var ni) &&
                ni.IsNumeric && !ni.IsPhone && !isEmpty)
            {
                string raw = e.Value?.ToString()?.Trim() ?? string.Empty;
                string clean = raw.TrimStart('$', '\u20AC', '\u00A3', '\u00A5', ' ')
                                  .Replace(",", string.Empty);
                if (double.TryParse(clean,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double numVal))
                {
                    numVal = Math.Round(numVal, 2);
                    e.Value = ni.IsCurrency
                        ? $"${numVal:N2}"
                        : (numVal == Math.Floor(numVal)
                            ? numVal.ToString("N0")
                            : numVal.ToString("N2"));
                    e.FormattingApplied = true;
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    e.CellStyle.ForeColor = ni.IsCurrency ? Theme.Teal : Theme.Sky;
                }
            }

            if (isDup)
            {
                e.CellStyle.BackColor = Color.FromArgb(50, 25, 25);
                e.CellStyle.ForeColor = Theme.Coral;
            }
            if (isEmpty)
            {
                e.CellStyle.BackColor = Theme.AmberDim;
                e.CellStyle.ForeColor = Theme.Amber;
                e.Value = "(vac\u00EDo)";
                e.FormattingApplied = true;
            }
            if (isDate)
            {
                e.CellStyle.BackColor = Color.FromArgb(20, 35, 50);
                e.CellStyle.ForeColor = Theme.Accent2;
            }
            if (isPhone)
            {
                e.CellStyle.BackColor = Color.FromArgb(50, 20, 50);
                e.CellStyle.ForeColor = Theme.Pink;
            }
            if (isEmail)
            {
                e.CellStyle.BackColor = Color.FromArgb(50, 35, 15);
                e.CellStyle.ForeColor = Theme.Amber;
            }
            if (isColMismatch && !isDup && !isEmpty && !isDate && !isPhone && !isEmail)
            {
                e.CellStyle.BackColor = Color.FromArgb(60, 20, 20);
                e.CellStyle.ForeColor = Color.FromArgb(255, 120, 100);
            }

            if (grid.RowHeadersVisible)
                grid.Rows[e.RowIndex].HeaderCell.Value = (e.RowIndex + 1).ToString();
        }

        // Phase 5B: reads from _report instead of individual list fields
        private void UpdateStatus()
        {
            int total = masterTable.Rows.Count;
            int dups = _report.DuplicateRows.Count;
            int dates = _report.DateIssues.Count;
            int empties = _report.EmptyFields.Count;
            int phones = _report.PhoneIssues.Count;
            int emails = _report.EmailIssues.Count;
            int colMis = _report.ColumnMismatchRows.Count;

            var parts = new List<string> { $"{total} filas" };
            if (dups > 0) parts.Add($"{dups} duplicados");
            if (dates > 0) parts.Add($"{dates} fechas");
            if (empties > 0) parts.Add($"{empties} vac\u00EDos");
            if (phones > 0) parts.Add($"{phones} tel\u00E9fonos");
            if (emails > 0) parts.Add($"{emails} emails");
            if (colMis > 0) parts.Add($"{colMis} col.desajustadas");

            if (!_report.HasIssues)
                parts.Add("Sin problemas \u2713");

            statusLabel.Text = "  " + string.Join("  \u00B7  ", parts);
        }

        // Phase 5B: reads from _report
        private void ShowAnalysisPopup()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"An\u00E1lisis de \"{Path.GetFileName(filePath)}\":\n");

            if (_report.DuplicateRows.Count > 0)
                sb.AppendLine($"\u2022 {_report.DuplicateRows.Count} fila(s) duplicada(s)");

            if (_report.DateIssues.Count > 0)
                sb.AppendLine($"\u2022 {_report.DateIssues.Count} fecha(s) a normalizar");

            if (_report.EmptyFields.Count > 0)
                sb.AppendLine($"\u2022 {_report.EmptyFields.Count} campo(s) vac\u00EDo(s)");

            if (_report.PhoneIssues.Count > 0)
            {
                sb.AppendLine(
                    $"\u2022 {_report.PhoneIssues.Count} tel\u00E9fono(s) con problemas:");
                int shown = 0;
                foreach (var (r, c, orig, fix) in _report.PhoneIssues)
                {
                    if (shown++ >= 5)
                    {
                        sb.AppendLine(
                            $"    ... y {_report.PhoneIssues.Count - 5} m\u00E1s");
                        break;
                    }
                    sb.AppendLine($"    Fila {r + 1}: \"{orig}\" \u2192 \"{fix}\"");
                }
            }

            if (_report.EmailIssues.Count > 0)
            {
                sb.AppendLine(
                    $"\u2022 {_report.EmailIssues.Count} email(s) inv\u00E1lido(s):");
                int shown = 0;
                foreach (var (r, c, orig) in _report.EmailIssues)
                {
                    if (shown++ >= 5)
                    {
                        sb.AppendLine(
                            $"    ... y {_report.EmailIssues.Count - 5} m\u00E1s");
                        break;
                    }
                    sb.AppendLine($"    Fila {r + 1}: \"{orig}\"");
                }
            }

            if (_report.ColumnMismatchRows.Count > 0)
            {
                sb.AppendLine(
                    $"\u2022 {_report.ColumnMismatchRows.Count} fila(s) con columnas incorrectas:");
                int shown = 0;
                foreach (var (row, exp, act) in _report.ColumnMismatchDetails)
                {
                    if (shown++ >= 5)
                    {
                        sb.AppendLine(
                            $"    ... y {_report.ColumnMismatchDetails.Count - 5} m\u00E1s");
                        break;
                    }
                    sb.AppendLine($"    Fila {row + 1}: esperadas {exp}, tiene {act}");
                }
            }

            sb.AppendLine("\nLas celdas afectadas est\u00E1n resaltadas.");
            MessageBox.Show(sb.ToString(), "An\u00E1lisis de datos",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTER / SORT — unchanged
        // ════════════════════════════════════════════════════════════════════
        private void PopulateFilterCombo()
        {
            filterColumnCombo.Items.Clear();
            filterColumnCombo.Items.Add("Todas");
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
                : string.Empty;

            var filtered = masterTable.Clone();
            foreach (DataRow row in masterTable.Rows)
            {
                bool match = false;
                if (string.IsNullOrEmpty(colSel))
                {
                    foreach (var item in row.ItemArray)
                        if (item?.ToString()?.IndexOf(query,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        { match = true; break; }
                }
                else
                {
                    match = row[colSel]?.ToString()?.IndexOf(
                        query, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (match) filtered.ImportRow(row);
            }

            ApplyDisplayTable(filtered);
            statusLabel.Text =
                $"  {filtered.Rows.Count} de {masterTable.Rows.Count} filas  \u00B7  \"{query}\"";
        }

        private void ClearFilter()
        {
            filterBox.Text = string.Empty;
            filterColumnCombo.SelectedIndex = 0;
            ApplyDisplayTable(masterTable);
            UpdateStatus();
        }

        private int lastSortCol = -1;
        private bool sortAsc = true;

        private void SortByColumn(int colIdx)
        {
            if (colIdx < 0 || colIdx >= displayTable.Columns.Count) return;
            sortAsc = colIdx == lastSortCol ? !sortAsc : true;
            lastSortCol = colIdx;
            var view = displayTable.DefaultView;
            view.Sort = $"[{displayTable.Columns[colIdx].ColumnName}] {(sortAsc ? "ASC" : "DESC")}";
            grid.DataSource = null;
            grid.DataSource = view.ToTable();
        }

        // ════════════════════════════════════════════════════════════════════
        //  SAVE / EXPORT
        //  Phase 5B: SerializeTable() calls replaced by DataSerializer.Serialize()
        // ════════════════════════════════════════════════════════════════════
        private void SaveFixedCopy()
        {
            var ft = masterTable.Copy();

            // Apply date fixes
            foreach (var (r, c, _, f) in _report.DateIssues)
                ft.Rows[r][c] = f;

            // Apply phone fixes (only write back clean 10-digit numbers)
            foreach (var (r, c, orig, f) in _report.PhoneIssues)
            {
                string digitsInFix = new string(f.Where(char.IsDigit).ToArray());
                ft.Rows[r][c] = digitsInFix.Length == 10 && !f.StartsWith("\u26A0")
                    ? digitsInFix
                    : f;
            }

            // Mark invalid emails
            foreach (var (r, c, orig) in _report.EmailIssues)
                ft.Rows[r][c] = $"\u26A0{orig}";

            // Mark empty fields
            foreach (var (r, c) in _report.EmptyFields)
                ft.Rows[r][c] = "(vac\u00EDo)";

            // Remove duplicate rows (keep first occurrence)
            var toRemove = _report.DuplicateRows
                .GroupBy(r => string.Join("\u2502",
                    masterTable.Rows[r].ItemArray
                        .Select(x => x?.ToString() ?? string.Empty)))
                .SelectMany(g => g.Skip(1))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
            foreach (int r in toRemove)
                if (r < ft.Rows.Count) ft.Rows[r].Delete();
            ft.AcceptChanges();

            string dir = Path.GetDirectoryName(filePath)!;
            using var dlg = new SaveFileDialog
            {
                Title = "Guardar corregida",
                InitialDirectory = dir,
                FileName = Path.GetFileNameWithoutExtension(filePath) +
                           "_corregido" + ext,
                Filter = GetSaveFilter()
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.WriteAllText(
                    dlg.FileName,
                    DataSerializer.Serialize(ft, ext),
                    Encoding.UTF8);
                MessageBox.Show($"Guardado: {dlg.FileName}", "OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportAs(string targetExt)
        {
            string extU = targetExt.TrimStart('.').ToUpper();
            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar como {extU}",
                Filter = $"{extU} (*{targetExt})|*{targetExt}|Todos|*.*",
                FileName = Path.GetFileNameWithoutExtension(filePath) +
                           "_exportado" + targetExt
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.WriteAllText(
                    dlg.FileName,
                    DataSerializer.Serialize(displayTable, targetExt),
                    Encoding.UTF8);
                MessageBox.Show($"Exportado: {dlg.FileName}", "OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportarOffice(string officeExt)
        {
            string titulo = Path.GetFileNameWithoutExtension(filePath);
            ExportadorOffice.ExportarConDialogo(displayTable, titulo, officeExt, this);
        }

        private string GetSaveFilter() => ext switch
        {
            ".csv" => "CSV|*.csv|Todos|*.*",
            ".txt" => "Texto|*.txt|Todos|*.*",
            ".json" => "JSON|*.json|Todos|*.*",
            ".xml" => "XML|*.xml|Todos|*.*",
            _ => "Todos|*.*"
        };
    }
}