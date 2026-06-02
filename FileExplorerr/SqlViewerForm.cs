using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace FileExplorerr
{
    public class SqlViewerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Button btnPostgres = null!, btnMaria = null!, btnSqlServer = null!;
        private Button btnActualizar = null!, btnDesconectar = null!;
        private Button btnEjecutar = null!, btnLimpiar = null!;
        private Button btnExportarCsv = null!, btnExportarJson = null!;
        private Button btnExportarTxt = null!, btnExportarXml = null!;
        private Button btnExportarXlsx = null!, btnExportarDocx = null!;
        private Button btnExportarPptx = null!, btnExportarPdf = null!;
        private Button btnImportar = null!;
        private Label lblConexion = null!, lblHint = null!;
        private Panel tablaPanel = null!;
        private ListBox listaTablas = null!;
        private TextBox editorSql = null!;
        private DataGridView grid = null!;
        private Label lblStatus = null!;
        private Panel loadingPanel = null!;
        private Label loadingLabel = null!;

        // ── Estado ───────────────────────────────────────────────────────────
        internal enum DbTipo { Ninguno, Postgres, Maria, SqlServer }
        private DbTipo tipoConexion = DbTipo.Ninguno;
        private string connectionString = "";
        private DataTable? resultadoActual;
        private IDbConnector? _connector;

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════
        public SqlViewerForm()
        {
            BuildUI();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "SQL — PostgreSQL · MariaDB · SQL Server";
            Size = new Size(1200, 720);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;

            // ── Loading overlay ──────────────────────────────────────────────
            loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(200, 18, 18, 22), Visible = false };
            loadingLabel = new Label { Text = "Ejecutando...", Font = Theme.FontBodyBold, ForeColor = Theme.Accent, BackColor = Color.Transparent, AutoSize = true };
            loadingPanel.Controls.Add(loadingLabel);
            loadingPanel.Resize += (s, e) => CenterLoading();

            // ── Top bar ──────────────────────────────────────────────────────
            var topBar = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = Theme.BgSurface, Padding = new Padding(8, 8, 8, 8) };

            btnPostgres = MakeConnBtn("🐘 PostgreSQL", Color.FromArgb(0, 82, 130), Color.FromArgb(95, 189, 235));
            btnMaria = MakeConnBtn("🌿 MariaDB", Color.FromArgb(0, 72, 50), Color.FromArgb(60, 180, 120));
            btnSqlServer = MakeConnBtn("🗄 SQL Server", Color.FromArgb(60, 20, 0), Color.FromArgb(230, 140, 60));
            btnActualizar = MakeActionBtn("↻ Actualizar", Color.FromArgb(34, 34, 42), Theme.Accent);
            btnDesconectar = MakeActionBtn("✕ Desconectar", Color.FromArgb(34, 34, 42), Theme.Danger);
            lblConexion = new Label { Text = "● Sin conexión", Font = new Font("Segoe UI", 8.5F), ForeColor = Theme.TextMuted, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

            btnPostgres.Click += async (s, e) => await ConectarAsync(DbTipo.Postgres);
            btnMaria.Click += async (s, e) => await ConectarAsync(DbTipo.Maria);
            btnSqlServer.Click += async (s, e) => await ConectarAsync(DbTipo.SqlServer);
            btnActualizar.Click += async (s, e) => await CargarTablasAsync();
            btnDesconectar.Click += (s, e) => Desconectar();

            var connFlow = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };
            connFlow.Controls.AddRange(new Control[] { btnPostgres, btnMaria, btnSqlServer, btnActualizar, btnDesconectar });

            var lblConnPanel = new Panel { Dock = DockStyle.Right, AutoSize = true, BackColor = Color.Transparent, Padding = new Padding(0, 16, 12, 0) };
            lblConexion.Dock = DockStyle.Fill;
            lblConnPanel.Controls.Add(lblConexion);

            topBar.Controls.Add(connFlow);
            topBar.Controls.Add(lblConnPanel);

            // ── Toolbar SQL ──────────────────────────────────────────────────
            var sqlBar = new Panel { Height = 48, Dock = DockStyle.Top, BackColor = Theme.BgElevated, Padding = new Padding(8, 6, 8, 6) };

            var btnEjecutarBtn = new Button
            {
                Text = "▶ Ejecutar (F5)",
                Height = 32,
                AutoSize = true,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.FromArgb(22, 100, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEjecutarBtn.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnEjecutarBtn.Click += async (s, e) => await EjecutarConsultaAsync();

            btnLimpiar = MakeActionBtn("⊗ Limpiar", Color.FromArgb(44, 44, 54), Color.FromArgb(200, 100, 80));
            btnExportarCsv = MakeExportBtn("↓ Exportar CSV", Color.FromArgb(20, 90, 70), Color.FromArgb(56, 210, 170));
            btnExportarJson = MakeExportBtn("↓ Exportar JSON", Color.FromArgb(80, 55, 10), Color.FromArgb(230, 160, 40));
            btnExportarTxt = MakeExportBtn("↓ Exportar TXT", Color.FromArgb(25, 40, 90), Color.FromArgb(90, 140, 240));
            btnExportarXml = MakeExportBtn("↓ Exportar XML", Color.FromArgb(55, 20, 80), Color.FromArgb(180, 80, 230));
            btnExportarXlsx = MakeExportBtn("📊 Excel", Color.FromArgb(16, 72, 32), Color.FromArgb(80, 200, 100));
            btnExportarDocx = MakeExportBtn("📝 Word", Color.FromArgb(12, 48, 96), Color.FromArgb(80, 150, 240));
            btnExportarPptx = MakeExportBtn("📋 PowerPoint", Color.FromArgb(80, 30, 10), Color.FromArgb(230, 100, 60));
            btnExportarPdf = MakeExportBtn("🗒 PDF", Color.FromArgb(70, 10, 10), Color.FromArgb(220, 70, 70));
            btnImportar = MakeActionBtn("↑ Importar archivo→BD", Color.FromArgb(10, 32, 58), Color.FromArgb(125, 211, 252));

            lblHint = new Label { Text = "Ctrl+Enter o F5 para ejecutar", Dock = DockStyle.Right, AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 7.5F), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 8, 4, 0) };

            var sqlFlow = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };

            btnLimpiar.Click += (s, e) => { editorSql.Clear(); editorSql.Focus(); };
            btnExportarCsv.Click += (s, e) => ExportarResultado(".csv");
            btnExportarJson.Click += (s, e) => ExportarResultado(".json");
            btnExportarTxt.Click += (s, e) => ExportarResultado(".txt");
            btnExportarXml.Click += (s, e) => ExportarResultado(".xml");
            btnImportar.Click += async (s, e) => await ImportarArchivoAsync();
            btnExportarXlsx.Click += (s, e) => ExportadorOffice.ExportarConDialogo(resultadoActual!, ObtenerTituloQuery(), ".xlsx", this);
            btnExportarDocx.Click += (s, e) => ExportadorOffice.ExportarConDialogo(resultadoActual!, ObtenerTituloQuery(), ".docx", this);
            btnExportarPptx.Click += (s, e) => ExportadorOffice.ExportarConDialogo(resultadoActual!, ObtenerTituloQuery(), ".pptx", this);
            btnExportarPdf.Click += (s, e) => ExportadorOffice.ExportarConDialogo(resultadoActual!, ObtenerTituloQuery(), ".pdf", this);

            sqlFlow.Controls.AddRange(new Control[] { btnEjecutarBtn, btnLimpiar, btnExportarCsv, btnExportarJson, btnExportarTxt, btnExportarXml, btnExportarXlsx, btnExportarDocx, btnExportarPptx, btnExportarPdf, btnImportar });

            sqlBar.Controls.Add(sqlFlow);
            sqlBar.Controls.Add(lblHint);

            // ── Panel izquierdo — Tablas ─────────────────────────────────────
            var leftPanel = new Panel { Width = 200, Dock = DockStyle.Left, BackColor = Theme.BgSurface };
            var lblTablas = new Label { Text = "Tablas", Height = 32, Dock = DockStyle.Top, Font = Theme.FontBodyBold, ForeColor = Theme.Accent, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), BackColor = Theme.BgElevated };
            listaTablas = new ListBox { Dock = DockStyle.Fill, BackColor = Theme.BgSurface, ForeColor = Theme.TextPrimary, Font = Theme.FontBody, BorderStyle = BorderStyle.None, IntegralHeight = false };
            listaTablas.DoubleClick += ListaTablas_DoubleClick;
            leftPanel.Controls.Add(listaTablas);
            leftPanel.Controls.Add(lblTablas);

            // ── Editor SQL ───────────────────────────────────────────────────
            editorSql = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, Font = new Font("Cascadia Code", 10F), BackColor = Color.FromArgb(14, 14, 20), ForeColor = Color.FromArgb(220, 220, 230), BorderStyle = BorderStyle.None, AcceptsReturn = true, AcceptsTab = true, WordWrap = false };
            editorSql.KeyDown += (s, e) =>
            {
                if ((e.KeyCode == Keys.F5) || (e.KeyCode == Keys.Enter && e.Control))
                {
                    e.Handled = e.SuppressKeyPress = true;
                    _ = EjecutarConsultaAsync();
                }
            };

            var editorWrapper = new Panel { Height = 130, Dock = DockStyle.Top, BackColor = Color.FromArgb(14, 14, 20), Padding = new Padding(8, 6, 8, 6) };
            editorWrapper.Controls.Add(editorSql);

            // ── Grid resultados ──────────────────────────────────────────────
            grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = true, RowHeadersWidth = 44, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None };
            Theme.StyleGrid(grid);
            grid.RowHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.BgSurface, ForeColor = Theme.TextMuted, Font = Theme.FontSmall };
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellFormatting += (s, e) => { if (grid.RowHeadersVisible && e.RowIndex >= 0) grid.Rows[e.RowIndex].HeaderCell.Value = (e.RowIndex + 1).ToString(); };

            // ── Barra inferior ───────────────────────────────────────────────
            var bottomBar = new Panel { Height = 28, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface, Padding = new Padding(10, 4, 10, 4) };
            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall };
            bottomBar.Controls.Add(lblStatus);

            var lblResultados = new Label { Text = "Resultados", Height = 26, Dock = DockStyle.Top, Font = Theme.FontSmall, ForeColor = Theme.TextMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), BackColor = Theme.BgElevated };

            var centerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgBase };
            centerPanel.Controls.Add(loadingPanel);
            centerPanel.Controls.Add(grid);
            centerPanel.Controls.Add(lblResultados);
            centerPanel.Controls.Add(editorWrapper);

            loadingPanel.BringToFront();

            Controls.Add(centerPanel);
            Controls.Add(leftPanel);
            Controls.Add(sqlBar);
            Controls.Add(topBar);
            Controls.Add(bottomBar);

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F5) { e.Handled = true; _ = EjecutarConsultaAsync(); } };

            SetExportButtonsEnabled(false);
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONEXIÓN
        // ════════════════════════════════════════════════════════════════════
        private async Task ConectarAsync(DbTipo tipo)
        {
            using var dlg = new ConexionDialog(tipo);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            connectionString = dlg.ConnectionString;
            tipoConexion = tipo;

            ShowLoading("Conectando...");
            try
            {
                _connector = tipo switch
                {
                    DbTipo.Postgres => new PostgreSqlConnector(connectionString),
                    DbTipo.Maria => new MariaDbConnector(connectionString),
                    DbTipo.SqlServer => new SqlServerConnector(connectionString),
                    _ => throw new InvalidOperationException("Tipo desconocido.")
                };

                string mensaje = await _connector.TestConnectionAsync();

                string nombreTipo = tipo switch { DbTipo.Postgres => "PostgreSQL", DbTipo.Maria => "MariaDB", DbTipo.SqlServer => "SQL Server", _ => "BD" };
                lblConexion.Text = $"● {nombreTipo} conectado";
                lblConexion.ForeColor = Theme.Success;
                Text = $"SQL — {nombreTipo} · datos";
                await CargarTablasAsync();
            }
            catch (Exception ex)
            {
                tipoConexion = DbTipo.Ninguno;
                connectionString = "";
                _connector = null;
                MessageBox.Show($"Error de conexión:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { HideLoading(); }
        }

        private void Desconectar()
        {
            tipoConexion = DbTipo.Ninguno;
            connectionString = "";
            _connector = null;
            listaTablas.Items.Clear();
            grid.DataSource = null;
            resultadoActual = null;
            lblConexion.Text = "● Sin conexión";
            lblConexion.ForeColor = Theme.TextMuted;
            lblStatus.Text = "";
            SetExportButtonsEnabled(false);
            Text = "SQL — visor";
        }

        private async Task CargarTablasAsync()
        {
            if (_connector is null) return;
            ShowLoading("Cargando tablas...");
            try
            {
                var tables = await _connector.GetTablesAsync();
                listaTablas.Items.Clear();
                foreach (var t in tables) listaTablas.Items.Add(t);
                lblStatus.Text = $"  {tables.Count} tabla(s)";
            }
            catch (Exception ex) { lblStatus.Text = $"  Error: {ex.Message}"; }
            finally { HideLoading(); }
        }

        private void ListaTablas_DoubleClick(object? sender, EventArgs e)
        {
            if (listaTablas.SelectedItem == null) return;
            string tabla = listaTablas.SelectedItem.ToString()!;
            string quoted = tipoConexion switch
            {
                DbTipo.Postgres => $"\"{tabla}\"",
                DbTipo.SqlServer => $"[{tabla}]",
                _ => $"`{tabla}`"
            };
            string limit = tipoConexion == DbTipo.SqlServer
                ? $"SELECT TOP 10 * FROM {quoted};"
                : $"SELECT * FROM {quoted} LIMIT 10;";
            editorSql.Text = limit;
            _ = EjecutarConsultaAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  EJECUTAR QUERY
        // ════════════════════════════════════════════════════════════════════
        private async Task EjecutarConsultaAsync()
        {
            if (_connector is null)
            {
                MessageBox.Show("No hay conexión activa.", "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string sql = editorSql.SelectedText.Length > 0 ? editorSql.SelectedText : editorSql.Text.Trim();
            if (string.IsNullOrWhiteSpace(sql)) return;

            ShowLoading("Ejecutando consulta...");
            SetExportButtonsEnabled(false);
            grid.DataSource = null;
            resultadoActual = null;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var (dt, rowsAffected) = await _connector.ExecuteAsync(sql);
                sw.Stop();

                if (dt is not null)
                {
                    resultadoActual = dt;
                    grid.DataSource = resultadoActual;
                    foreach (DataGridViewColumn col in grid.Columns)
                        col.Width = Math.Min(260, Math.Max(60, col.Width));
                    lblStatus.Text = $"  {dt.Rows.Count} fila(s)  ·  {sw.ElapsedMilliseconds} ms";
                    SetExportButtonsEnabled(dt.Rows.Count > 0);
                }
                else
                {
                    lblStatus.Text = $"  {rowsAffected} fila(s) afectada(s)  ·  {sw.ElapsedMilliseconds} ms";
                    await CargarTablasAsync();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"  Error: {ex.Message.Split('\n')[0]}";
                MessageBox.Show(ex.Message, "Error SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { HideLoading(); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR RESULTADO
        // ════════════════════════════════════════════════════════════════════
        private void ExportarResultado(string ext)
        {
            if (resultadoActual == null || resultadoActual.Rows.Count == 0) { MessageBox.Show("No hay resultados para exportar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string tipoStr = ext.TrimStart('.').ToUpper();
            using var dlg = new SaveFileDialog { Title = $"Exportar resultado como {tipoStr}", Filter = $"{tipoStr} (*{ext})|*{ext}|Todos los archivos (*.*)|*.*", FileName = $"resultado_{DateTime.Now:yyyyMMdd_HHmm}{ext}" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dlg.FileName, SerializarTabla(resultadoActual, ext), Encoding.UTF8);
                if (MessageBox.Show($"Exportado correctamente:\n{dlg.FileName}\n\n¿Abrir?", "Exportación completa", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static string SerializarTabla(DataTable dt, string ext) => ext switch { ".csv" => TablaCsv(dt), ".json" => TablaJson(dt), ".xml" => TablaXml(dt), _ => TablaTxt(dt) };

        private static string TablaCsv(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => $"\"{CsvHelper.EscapeField(c.ColumnName)}\"")));
            foreach (DataRow row in dt.Rows) sb.AppendLine(string.Join(",", row.ItemArray.Select(x => $"\"{CsvHelper.EscapeField(x?.ToString() ?? "")}\"")));
            return sb.ToString();
        }

        private static string TablaTxt(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            foreach (DataRow row in dt.Rows) sb.AppendLine(string.Join("\t", row.ItemArray.Select(x => x?.ToString() ?? "")));
            return sb.ToString();
        }

        private static string TablaJson(DataTable dt)
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (DataRow row in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns)
                {
                    string? val = row[col]?.ToString();
                    if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal)) d[col.ColumnName] = numVal;
                    else if (val == "" || val == null) d[col.ColumnName] = null;
                    else d[col.ColumnName] = val;
                }
                rows.Add(d);
            }
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        private static string TablaXml(DataTable dt) { dt.TableName = "Resultados"; using var sw = new StringWriter(); dt.WriteXml(sw); return sw.ToString(); }

        // ════════════════════════════════════════════════════════════════════
        //  IMPORTAR ARCHIVO → BD
        // ════════════════════════════════════════════════════════════════════
        private async Task ImportarArchivoAsync()
        {
            if (tipoConexion == DbTipo.Ninguno) { MessageBox.Show("Conéctate primero a una base de datos.", "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            using var dlg = new OpenFileDialog { Title = "Seleccionar archivo para importar", Filter = "Archivos de datos (*.csv;*.txt;*.json;*.xml)|*.csv;*.txt;*.json;*.xml|Todos|*.*", Multiselect = false };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            ShowLoading("Leyendo archivo...");
            DataTable? dt = null;
            try { string ext = Path.GetExtension(dlg.FileName).ToLower(); string contenido = await Task.Run(() => File.ReadAllText(dlg.FileName)); dt = await Task.Run(() => ParsearArchivo(contenido, ext)); }
            catch (Exception ex) { HideLoading(); MessageBox.Show($"Error leyendo archivo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            if (dt == null || dt.Rows.Count == 0) { HideLoading(); MessageBox.Show("El archivo no contiene datos.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            HideLoading();
            string nombreSugerido = Path.GetFileNameWithoutExtension(dlg.FileName).Replace(" ", "_").ToLowerInvariant();
            await ImportarDataTableABD(dt, nombreSugerido);
        }

        public async Task ImportarDataTableABD(DataTable dt, string nombreTabla)
        {
            using var dlg = new NombreTablaDialog(nombreTabla);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            nombreTabla = dlg.NombreTabla;

            ShowLoading($"Creando tabla '{nombreTabla}'...");
            try
            {
                if (_connector is null) { HideLoading(); return; }

                var writeResult = await _connector.InsertDataTableAsync(dt, nombreTabla);
                if (!writeResult.Success)
                    throw new Exception(writeResult.Message);

                HideLoading();
                await CargarTablasAsync();

                string quoted = tipoConexion == DbTipo.SqlServer ? $"[{nombreTabla}]"
                    : tipoConexion == DbTipo.Postgres ? $"\"{nombreTabla}\""
                    : $"`{nombreTabla}`";
                string limit = tipoConexion == DbTipo.SqlServer
                    ? $"SELECT TOP 10 * FROM {quoted};"
                    : $"SELECT * FROM {quoted} LIMIT 10;";
                editorSql.Text = limit;

                MessageBox.Show($"✅ Tabla '{nombreTabla}' creada con {dt.Rows.Count} fila(s).", "Importación exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _ = EjecutarConsultaAsync();
            }
            catch (Exception ex) { HideLoading(); MessageBox.Show($"Error al importar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private static DataTable ParsearArchivo(string contenido, string ext) => ext switch { ".csv" => ParseCsv(contenido), ".json" => ParseJson(contenido), ".xml" => ParseXml(contenido), _ => ParseTxt(contenido) };

        private static DataTable ParseCsv(string contenido)
        {
            var dt = new DataTable();
            var lineas = contenido.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0) return dt;
            var headers = CsvHelper.SplitLine(lineas[0]);
            foreach (var h in headers) dt.Columns.Add(h.Trim('"', ' ').Length > 0 ? h.Trim('"', ' ') : $"Col{dt.Columns.Count + 1}");
            for (int i = 1; i < lineas.Length; i++) { var cells = CsvHelper.SplitLine(lineas[i]); var row = dt.NewRow(); for (int c = 0; c < dt.Columns.Count; c++) row[c] = c < cells.Count ? cells[c].Trim('"') : ""; dt.Rows.Add(row); }
            return dt;
        }

        private static DataTable ParseTxt(string contenido)
        {
            var dt = new DataTable();
            var lineas = contenido.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0) return dt;
            char delim = lineas[0].Contains('\t') ? '\t' : lineas[0].Contains('|') ? '|' : ';';
            var headers = lineas[0].Split(delim);
            foreach (var h in headers) dt.Columns.Add(h.Trim().Length > 0 ? h.Trim() : $"Col{dt.Columns.Count + 1}");
            for (int i = 1; i < lineas.Length; i++) { var cells = lineas[i].Split(delim); var row = dt.NewRow(); for (int c = 0; c < dt.Columns.Count; c++) row[c] = c < cells.Length ? cells[c].Trim() : ""; dt.Rows.Add(row); }
            return dt;
        }

        private static DataTable ParseJson(string contenido)
        {
            var dt = new DataTable();
            try
            {
                using var doc = JsonDocument.Parse(contenido);
                var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement : doc.RootElement.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array).Value;
                foreach (var elem in arr.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; foreach (var prop in elem.EnumerateObject()) if (!dt.Columns.Contains(prop.Name)) dt.Columns.Add(prop.Name); }
                foreach (var elem in arr.EnumerateArray()) { if (elem.ValueKind != JsonValueKind.Object) continue; var row = dt.NewRow(); foreach (var prop in elem.EnumerateObject()) if (dt.Columns.Contains(prop.Name)) row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.ToString(); dt.Rows.Add(row); }
            }
            catch { }
            return dt;
        }

        private static DataTable ParseXml(string contenido)
        {
            var dt = new DataTable();
            try
            {
                var doc = new System.Xml.XmlDocument(); doc.LoadXml(contenido);
                if (doc.DocumentElement == null) return dt;
                var primer = doc.DocumentElement.FirstChild?.Name; if (primer == null) return dt;
                var nodos = doc.DocumentElement.SelectNodes(primer); if (nodos == null) return dt;
                foreach (System.Xml.XmlNode n in nodos) foreach (System.Xml.XmlNode child in n.ChildNodes) if (!dt.Columns.Contains(child.Name)) dt.Columns.Add(child.Name);
                foreach (System.Xml.XmlNode n in nodos) { var row = dt.NewRow(); foreach (System.Xml.XmlNode child in n.ChildNodes) if (dt.Columns.Contains(child.Name)) row[child.Name] = child.InnerText; dt.Rows.Add(row); }
            }
            catch { }
            return dt;
        }

        private static string SanitizarColumna(string nombre) =>
            string.IsNullOrWhiteSpace(nombre) ? "col" :
            new string(nombre.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray()).TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS UI
        // ════════════════════════════════════════════════════════════════════
        private void ShowLoading(string texto = "Procesando...") { loadingLabel.Text = texto; loadingPanel.Visible = true; CenterLoading(); loadingPanel.BringToFront(); }
        private void HideLoading() => loadingPanel.Visible = false;
        private void CenterLoading() { if (loadingLabel == null || loadingPanel == null) return; loadingLabel.Location = new Point((loadingPanel.Width - loadingLabel.Width) / 2, (loadingPanel.Height - loadingLabel.Height) / 2); }

        private void SetExportButtonsEnabled(bool enabled)
        {
            btnExportarCsv.Enabled = enabled; btnExportarJson.Enabled = enabled;
            btnExportarTxt.Enabled = enabled; btnExportarXml.Enabled = enabled;
            btnExportarXlsx.Enabled = enabled; btnExportarDocx.Enabled = enabled;
            btnExportarPptx.Enabled = enabled; btnExportarPdf.Enabled = enabled;
        }

        private string ObtenerTituloQuery()
        {
            string sql = editorSql.Text.Trim();
            var match = System.Text.RegularExpressions.Regex.Match(sql, @"FROM\s+[`""\[\[]?(\w+)[`""\]\]]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "resultado_sql";
        }

        private static Button MakeConnBtn(string text, Color bg, Color fg)
        {
            var btn = new Button { Text = text, Height = 32, AutoSize = true, Padding = new Padding(10, 0, 10, 0), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = fg; btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakeActionBtn(string text, Color bg, Color fg)
        {
            var btn = new Button { Text = text, Height = 32, AutoSize = true, Padding = new Padding(8, 0, 8, 0), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = fg; btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakeExportBtn(string text, Color bg, Color fg)
        {
            var btn = new Button { Text = text, Height = 32, AutoSize = true, Padding = new Padding(8, 0, 8, 0), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = fg; btn.FlatAppearance.BorderSize = 1;
            return btn;
        }
    }
}