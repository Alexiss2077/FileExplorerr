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
using Npgsql;
using MySqlConnector;

namespace FileExplorerr
{
    public class SqlViewerForm : Form
    {
        // ── Controles ────────────────────────────────────────────────────────
        private Button btnPostgres = null!, btnMaria = null!;
        private Button btnActualizar = null!, btnDesconectar = null!;
        private Button btnEjecutar = null!, btnLimpiar = null!;
        private Button btnExportarCsv = null!, btnExportarJson = null!;
        private Button btnExportarTxt = null!, btnExportarXml = null!;
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
        internal enum DbTipo { Ninguno, Postgres, Maria }
        private DbTipo tipoConexion = DbTipo.Ninguno;
        private string connectionString = "";
        private DataTable? resultadoActual;

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
            Text = "SQL — PostgreSQL · datos";
            Size = new Size(1200, 720);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;

            // ── Loading overlay ──────────────────────────────────────────────
            loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 18, 18, 22),
                Visible = false
            };
            loadingLabel = new Label
            {
                Text = "Ejecutando...",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            loadingPanel.Controls.Add(loadingLabel);
            loadingPanel.Resize += (s, e) => CenterLoading();

            // ── Top bar ──────────────────────────────────────────────────────
            var topBar = new Panel
            {
                Height = 52,
                Dock = DockStyle.Top,
                BackColor = Theme.BgSurface,
                Padding = new Padding(8, 8, 8, 8)
            };

            // Botones de conexión
            btnPostgres = MakeConnBtn("🐘 PostgreSQL", Color.FromArgb(0, 82, 130), Color.FromArgb(95, 189, 235));
            btnMaria = MakeConnBtn("🌿 MariaDB", Color.FromArgb(0, 72, 50), Color.FromArgb(60, 180, 120));
            btnActualizar = MakeActionBtn("↻ Actualizar", Color.FromArgb(34, 34, 42), Theme.Accent);
            btnDesconectar = MakeActionBtn("✕ Desconectar", Color.FromArgb(34, 34, 42), Theme.Danger);
            lblConexion = new Label
            {
                Text = "● Sin conexión",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnPostgres.Click += async (s, e) => await ConectarAsync(DbTipo.Postgres);
            btnMaria.Click += async (s, e) => await ConectarAsync(DbTipo.Maria);
            btnActualizar.Click += async (s, e) => await CargarTablasAsync();
            btnDesconectar.Click += (s, e) => Desconectar();

            var connFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            connFlow.Controls.AddRange(new Control[] { btnPostgres, btnMaria, btnActualizar, btnDesconectar });

            var lblConnPanel = new Panel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 16, 12, 0)
            };
            lblConexion.Dock = DockStyle.Fill;
            lblConnPanel.Controls.Add(lblConexion);

            topBar.Controls.Add(connFlow);
            topBar.Controls.Add(lblConnPanel);

            // ── Toolbar SQL ──────────────────────────────────────────────────
            var sqlBar = new Panel
            {
                Height = 48,
                Dock = DockStyle.Top,
                BackColor = Theme.BgElevated,
                Padding = new Padding(8, 6, 8, 6)
            };

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

            // ── Botones de exportación ────────────────────────────────────────
            btnExportarCsv = MakeExportBtn("↓ Exportar CSV",
                Color.FromArgb(20, 90, 70), Color.FromArgb(56, 210, 170));
            btnExportarJson = MakeExportBtn("↓ Exportar JSON",
                Color.FromArgb(80, 55, 10), Color.FromArgb(230, 160, 40));
            btnExportarTxt = MakeExportBtn("↓ Exportar TXT",
                Color.FromArgb(25, 40, 90), Color.FromArgb(90, 140, 240));
            btnExportarXml = MakeExportBtn("↓ Exportar XML",
                Color.FromArgb(55, 20, 80), Color.FromArgb(180, 80, 230));

            btnImportar = MakeActionBtn("↑ Importar archivo→BD",
                Color.FromArgb(10, 32, 58), Color.FromArgb(125, 211, 252));

            lblHint = new Label
            {
                Text = "Ctrl+Enter o F5 para ejecutar",
                Dock = DockStyle.Right,
                AutoSize = true,
                ForeColor = Theme.TextMuted,
                Font = new Font("Segoe UI", 7.5F),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 8, 4, 0)
            };

            var sqlFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 0)
            };

            btnLimpiar.Click += (s, e) => { editorSql.Clear(); editorSql.Focus(); };
            btnExportarCsv.Click += (s, e) => ExportarResultado(".csv");
            btnExportarJson.Click += (s, e) => ExportarResultado(".json");
            btnExportarTxt.Click += (s, e) => ExportarResultado(".txt");
            btnExportarXml.Click += (s, e) => ExportarResultado(".xml");
            btnImportar.Click += async (s, e) => await ImportarArchivoAsync();

            sqlFlow.Controls.AddRange(new Control[]
            {
                btnEjecutarBtn, btnLimpiar,
                btnExportarCsv, btnExportarJson, btnExportarTxt, btnExportarXml,
                btnImportar
            });

            sqlBar.Controls.Add(sqlFlow);
            sqlBar.Controls.Add(lblHint);

            // ── Panel izquierdo — Tablas ─────────────────────────────────────
            var leftPanel = new Panel
            {
                Width = 200,
                Dock = DockStyle.Left,
                BackColor = Theme.BgSurface
            };
            var lblTablas = new Label
            {
                Text = "Tablas",
                Height = 32,
                Dock = DockStyle.Top,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Accent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = Theme.BgElevated
            };
            listaTablas = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            listaTablas.DoubleClick += ListaTablas_DoubleClick;
            leftPanel.Controls.Add(listaTablas);
            leftPanel.Controls.Add(lblTablas);

            // ── Editor SQL ───────────────────────────────────────────────────
            editorSql = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Cascadia Code", 10F),
                BackColor = Color.FromArgb(14, 14, 20),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.None,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = false
            };
            editorSql.KeyDown += (s, e) =>
            {
                if ((e.KeyCode == Keys.F5) || (e.KeyCode == Keys.Enter && e.Control))
                {
                    e.Handled = e.SuppressKeyPress = true;
                    _ = EjecutarConsultaAsync();
                }
            };

            var editorWrapper = new Panel
            {
                Height = 130,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(14, 14, 20),
                Padding = new Padding(8, 6, 8, 6)
            };
            editorWrapper.Controls.Add(editorSql);

            // ── Grid resultados ──────────────────────────────────────────────
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = true,
                RowHeadersWidth = 44,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both,
                BorderStyle = BorderStyle.None
            };
            Theme.StyleGrid(grid);
            grid.RowHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.CellFormatting += (s, e) =>
            {
                if (grid.RowHeadersVisible && e.RowIndex >= 0)
                    grid.Rows[e.RowIndex].HeaderCell.Value = (e.RowIndex + 1).ToString();
            };

            // ── Barra inferior ───────────────────────────────────────────────
            var bottomBar = new Panel
            {
                Height = 28,
                Dock = DockStyle.Bottom,
                BackColor = Theme.BgSurface,
                Padding = new Padding(10, 4, 10, 4)
            };
            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontSmall
            };
            bottomBar.Controls.Add(lblStatus);

            // ── Marcador de "Resultados" ─────────────────────────────────────
            var lblResultados = new Label
            {
                Text = "Resultados",
                Height = 26,
                Dock = DockStyle.Top,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.BgElevated
            };

            // ── Panel central (editor + grid) ────────────────────────────────
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
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    _ = EjecutarConsultaAsync();
                }
            };

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
                await Task.Run(() => TestConexion());
                lblConexion.Text = $"● {(tipo == DbTipo.Postgres ? "PostgreSQL" : "MariaDB")} conectado";
                lblConexion.ForeColor = Theme.Success;
                Text = $"SQL — {(tipo == DbTipo.Postgres ? "PostgreSQL" : "MariaDB")} · datos";
                await CargarTablasAsync();
            }
            catch (Exception ex)
            {
                tipoConexion = DbTipo.Ninguno;
                connectionString = "";
                MessageBox.Show($"Error de conexión:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { HideLoading(); }
        }

        private void TestConexion()
        {
            if (tipoConexion == DbTipo.Postgres)
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();
            }
            else
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
            }
        }

        private void Desconectar()
        {
            tipoConexion = DbTipo.Ninguno;
            connectionString = "";
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
            if (tipoConexion == DbTipo.Ninguno) return;
            ShowLoading("Cargando tablas...");
            try
            {
                var tablas = await Task.Run(() => ObtenerTablas());
                listaTablas.Items.Clear();
                foreach (var t in tablas) listaTablas.Items.Add(t);
                lblStatus.Text = $"  {tablas.Count} tabla(s)";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"  Error: {ex.Message}";
            }
            finally { HideLoading(); }
        }

        private List<string> ObtenerTablas()
        {
            var lista = new List<string>();
            string sql = tipoConexion == DbTipo.Postgres
                ? "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename"
                : "SHOW TABLES";

            if (tipoConexion == DbTipo.Postgres)
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(reader.GetString(0));
            }
            else
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
                using var cmd = new MySqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(reader.GetString(0));
            }
            return lista;
        }

        private void ListaTablas_DoubleClick(object? sender, EventArgs e)
        {
            if (listaTablas.SelectedItem == null) return;
            string tabla = listaTablas.SelectedItem.ToString()!;
            // Wrap in quotes for safety
            string quoted = tipoConexion == DbTipo.Postgres
                ? $"\"{tabla}\""
                : $"`{tabla}`";
            editorSql.Text = $"SELECT * FROM {quoted} LIMIT 10;";
            _ = EjecutarConsultaAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  EJECUTAR QUERY
        // ════════════════════════════════════════════════════════════════════
        private async Task EjecutarConsultaAsync()
        {
            if (tipoConexion == DbTipo.Ninguno)
            {
                MessageBox.Show("No hay conexión activa.", "Sin conexión",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string sql = editorSql.SelectedText.Length > 0
                ? editorSql.SelectedText
                : editorSql.Text.Trim();

            if (string.IsNullOrWhiteSpace(sql)) return;

            ShowLoading("Ejecutando consulta...");
            SetExportButtonsEnabled(false);
            grid.DataSource = null;
            resultadoActual = null;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await Task.Run(() => EjecutarSql(sql));
                sw.Stop();

                if (result.dt != null)
                {
                    resultadoActual = result.dt;
                    grid.DataSource = resultadoActual;
                    foreach (DataGridViewColumn col in grid.Columns)
                        col.Width = Math.Min(260, Math.Max(60, col.Width));
                    lblStatus.Text = $"  {result.dt.Rows.Count} fila(s)  ·  {sw.ElapsedMilliseconds} ms";
                    SetExportButtonsEnabled(result.dt.Rows.Count > 0);
                }
                else
                {
                    lblStatus.Text = $"  {result.filas} fila(s) afectada(s)  ·  {sw.ElapsedMilliseconds} ms";
                    await CargarTablasAsync(); // Refrescar si fue DDL/DML
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"  Error: {ex.Message.Split('\n')[0]}";
                MessageBox.Show(ex.Message, "Error SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { HideLoading(); }
        }

        private (DataTable? dt, int filas) EjecutarSql(string sql)
        {
            if (tipoConexion == DbTipo.Postgres)
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };

                if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                    sql.TrimStart().StartsWith("WITH", StringComparison.OrdinalIgnoreCase) ||
                    sql.TrimStart().StartsWith("SHOW", StringComparison.OrdinalIgnoreCase))
                {
                    var adapter = new Npgsql.NpgsqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return (dt, 0);
                }
                else
                {
                    return (null, cmd.ExecuteNonQuery());
                }
            }
            else
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
                using var cmd = new MySqlCommand(sql, conn) { CommandTimeout = 60 };

                if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                    sql.TrimStart().StartsWith("SHOW", StringComparison.OrdinalIgnoreCase) ||
                    sql.TrimStart().StartsWith("DESCRIBE", StringComparison.OrdinalIgnoreCase))
                {
                    var adapter = new MySqlConnector.MySqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return (dt, 0);
                }
                else
                {
                    return (null, cmd.ExecuteNonQuery());
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR RESULTADO
        // ════════════════════════════════════════════════════════════════════
        private void ExportarResultado(string ext)
        {
            if (resultadoActual == null || resultadoActual.Rows.Count == 0)
            {
                MessageBox.Show("No hay resultados para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string tipoStr = ext.TrimStart('.').ToUpper();
            using var dlg = new SaveFileDialog
            {
                Title = $"Exportar resultado como {tipoStr}",
                Filter = $"{tipoStr} (*{ext})|*{ext}|Todos los archivos (*.*)|*.*",
                FileName = $"resultado_{DateTime.Now:yyyyMMdd_HHmm}{ext}"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string contenido = SerializarTabla(resultadoActual, ext);
                File.WriteAllText(dlg.FileName, contenido, Encoding.UTF8);
                if (MessageBox.Show(
                    $"Exportado correctamente:\n{dlg.FileName}\n\n¿Abrir?",
                    "Exportación completa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = dlg.FileName, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string SerializarTabla(DataTable dt, string ext) => ext switch
        {
            ".csv" => TablaCsv(dt),
            ".json" => TablaJson(dt),
            ".xml" => TablaXml(dt),
            _ => TablaTxt(dt)
        };

        private static string TablaCsv(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",",
                dt.Columns.Cast<DataColumn>().Select(c => $"\"{Esc(c.ColumnName)}\"")));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join(",",
                    row.ItemArray.Select(x => $"\"{Esc(x?.ToString() ?? "")}\"")));
            return sb.ToString();
        }

        private static string TablaTxt(DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t",
                dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join("\t",
                    row.ItemArray.Select(x => x?.ToString() ?? "")));
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
                    if (double.TryParse(val,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double numVal))
                        d[col.ColumnName] = numVal;
                    else if (val == "" || val == null)
                        d[col.ColumnName] = null;
                    else
                        d[col.ColumnName] = val;
                }
                rows.Add(d);
            }
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private static string TablaXml(DataTable dt)
        {
            dt.TableName = "Resultados";
            using var sw = new StringWriter();
            dt.WriteXml(sw);
            return sw.ToString();
        }

        private static string Esc(string s) => s.Replace("\"", "\"\"");

        // ════════════════════════════════════════════════════════════════════
        //  IMPORTAR ARCHIVO → BD
        // ════════════════════════════════════════════════════════════════════
        private async Task ImportarArchivoAsync()
        {
            if (tipoConexion == DbTipo.Ninguno)
            {
                MessageBox.Show("Conéctate primero a una base de datos.", "Sin conexión",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new OpenFileDialog
            {
                Title = "Seleccionar archivo para importar",
                Filter = "Archivos de datos (*.csv;*.txt;*.json;*.xml)|*.csv;*.txt;*.json;*.xml|Todos|*.*",
                Multiselect = false
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // Leer y parsear el archivo
            ShowLoading("Leyendo archivo...");
            DataTable? dt = null;
            try
            {
                string ext = Path.GetExtension(dlg.FileName).ToLower();
                string contenido = await Task.Run(() => File.ReadAllText(dlg.FileName));
                dt = await Task.Run(() => ParsearArchivo(contenido, ext));
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"Error leyendo archivo:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (dt == null || dt.Rows.Count == 0)
            {
                HideLoading();
                MessageBox.Show("El archivo no contiene datos.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            HideLoading();
            string nombreSugerido = Path.GetFileNameWithoutExtension(dlg.FileName)
                .Replace(" ", "_").ToLowerInvariant();
            await ImportarDataTableABD(dt, nombreSugerido);
        }

        /// <summary>Importa un DataTable a la BD conectada. Llamable desde FileViewerForm.</summary>
        public async Task ImportarDataTableABD(DataTable dt, string nombreTabla)
        {
            using var dlg = new NombreTablaDialog(nombreTabla);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            nombreTabla = dlg.NombreTabla;

            ShowLoading($"Creando tabla '{nombreTabla}'...");
            try
            {
                await Task.Run(() => CrearEInsertarTabla(dt, nombreTabla));
                HideLoading();
                await CargarTablasAsync();
                editorSql.Text = $"SELECT * FROM \"{nombreTabla}\" LIMIT 10;";
                MessageBox.Show(
                    $"✅ Tabla '{nombreTabla}' creada con {dt.Rows.Count} fila(s).",
                    "Importación exitosa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _ = EjecutarConsultaAsync();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"Error al importar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CrearEInsertarTabla(DataTable dt, string nombreTabla)
        {
            if (tipoConexion == DbTipo.Postgres)
                CrearEInsertarPostgres(dt, nombreTabla);
            else
                CrearEInsertarMaria(dt, nombreTabla);
        }

        private void CrearEInsertarPostgres(DataTable dt, string nombreTabla)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            // Crear tabla
            var colDefs = dt.Columns.Cast<DataColumn>()
                .Select(c => $"\"{SanitizarColumna(c.ColumnName)}\" TEXT");
            string createSql = $"CREATE TABLE IF NOT EXISTS \"{nombreTabla}\" ({string.Join(", ", colDefs)})";
            using (var cmd = new NpgsqlCommand(createSql, conn)) cmd.ExecuteNonQuery();

            // Insertar filas en lotes
            using var tx = conn.BeginTransaction();
            foreach (DataRow row in dt.Rows)
            {
                var cols = dt.Columns.Cast<DataColumn>()
                    .Select(c => $"\"{SanitizarColumna(c.ColumnName)}\"");
                var parms = Enumerable.Range(1, dt.Columns.Count).Select(i => $"@p{i}");
                string insertSql = $"INSERT INTO \"{nombreTabla}\" ({string.Join(",", cols)}) VALUES ({string.Join(",", parms)})";
                using var cmd = new NpgsqlCommand(insertSql, conn, tx);
                for (int i = 0; i < dt.Columns.Count; i++)
                    cmd.Parameters.AddWithValue($"@p{i + 1}", row[i]?.ToString() ?? "");
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        private void CrearEInsertarMaria(DataTable dt, string nombreTabla)
        {
            using var conn = new MySqlConnection(connectionString);
            conn.Open();

            var colDefs = dt.Columns.Cast<DataColumn>()
                .Select(c => $"`{SanitizarColumna(c.ColumnName)}` TEXT");
            string createSql = $"CREATE TABLE IF NOT EXISTS `{nombreTabla}` ({string.Join(", ", colDefs)})";
            using (var cmd = new MySqlCommand(createSql, conn)) cmd.ExecuteNonQuery();

            using var tx = conn.BeginTransaction();
            foreach (DataRow row in dt.Rows)
            {
                var cols = dt.Columns.Cast<DataColumn>()
                    .Select(c => $"`{SanitizarColumna(c.ColumnName)}`");
                var parms = Enumerable.Range(1, dt.Columns.Count).Select(i => $"@p{i}");
                string insertSql = $"INSERT INTO `{nombreTabla}` ({string.Join(",", cols)}) VALUES ({string.Join(",", parms)})";
                using var cmd = new MySqlCommand(insertSql, conn, tx);
                for (int i = 0; i < dt.Columns.Count; i++)
                    cmd.Parameters.AddWithValue($"@p{i + 1}", row[i]?.ToString() ?? "");
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        private static DataTable ParsearArchivo(string contenido, string ext)
        {
            return ext switch
            {
                ".csv" => ParseCsv(contenido),
                ".json" => ParseJson(contenido),
                ".xml" => ParseXml(contenido),
                _ => ParseTxt(contenido)
            };
        }

        private static DataTable ParseCsv(string contenido)
        {
            var dt = new DataTable();
            var lineas = contenido.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0) return dt;
            var headers = SplitCsvLine(lineas[0]);
            foreach (var h in headers)
                dt.Columns.Add(h.Trim('"', ' ').Length > 0 ? h.Trim('"', ' ') : $"Col{dt.Columns.Count + 1}");
            for (int i = 1; i < lineas.Length; i++)
            {
                var cells = SplitCsvLine(lineas[i]);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Count ? cells[c].Trim('"') : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuote = false;
            var cur = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') inQuote = !inQuote;
                else if (c == ',' && !inQuote) { result.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            result.Add(cur.ToString());
            return result;
        }

        private static DataTable ParseTxt(string contenido)
        {
            var dt = new DataTable();
            var lineas = contenido.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0) return dt;
            char delim = lineas[0].Contains('\t') ? '\t' : lineas[0].Contains('|') ? '|' : ';';
            var headers = lineas[0].Split(delim);
            foreach (var h in headers) dt.Columns.Add(h.Trim().Length > 0 ? h.Trim() : $"Col{dt.Columns.Count + 1}");
            for (int i = 1; i < lineas.Length; i++)
            {
                var cells = lineas[i].Split(delim);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Length ? cells[c].Trim() : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static DataTable ParseJson(string contenido)
        {
            var dt = new DataTable();
            try
            {
                using var doc = JsonDocument.Parse(contenido);
                var arr = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement
                    : doc.RootElement.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array).Value;

                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind != JsonValueKind.Object) continue;
                    foreach (var prop in elem.EnumerateObject())
                        if (!dt.Columns.Contains(prop.Name)) dt.Columns.Add(prop.Name);
                }
                foreach (var elem in arr.EnumerateArray())
                {
                    if (elem.ValueKind != JsonValueKind.Object) continue;
                    var row = dt.NewRow();
                    foreach (var prop in elem.EnumerateObject())
                        if (dt.Columns.Contains(prop.Name))
                            row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.ToString();
                    dt.Rows.Add(row);
                }
            }
            catch { }
            return dt;
        }

        private static DataTable ParseXml(string contenido)
        {
            var dt = new DataTable();
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(contenido);
                if (doc.DocumentElement == null) return dt;
                var primer = doc.DocumentElement.FirstChild?.Name;
                if (primer == null) return dt;
                var nodos = doc.DocumentElement.SelectNodes(primer);
                if (nodos == null) return dt;
                foreach (System.Xml.XmlNode n in nodos)
                    foreach (System.Xml.XmlNode child in n.ChildNodes)
                        if (!dt.Columns.Contains(child.Name)) dt.Columns.Add(child.Name);
                foreach (System.Xml.XmlNode n in nodos)
                {
                    var row = dt.NewRow();
                    foreach (System.Xml.XmlNode child in n.ChildNodes)
                        if (dt.Columns.Contains(child.Name)) row[child.Name] = child.InnerText;
                    dt.Rows.Add(row);
                }
            }
            catch { }
            return dt;
        }

        private static string SanitizarColumna(string nombre)
        {
            return string.IsNullOrWhiteSpace(nombre) ? "col" :
                new string(nombre.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray())
                    .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS UI
        // ════════════════════════════════════════════════════════════════════
        private void ShowLoading(string texto = "Procesando...")
        {
            loadingLabel.Text = texto;
            loadingPanel.Visible = true;
            CenterLoading();
            loadingPanel.BringToFront();
        }

        private void HideLoading() => loadingPanel.Visible = false;

        private void CenterLoading()
        {
            if (loadingLabel == null || loadingPanel == null) return;
            loadingLabel.Location = new Point(
                (loadingPanel.Width - loadingLabel.Width) / 2,
                (loadingPanel.Height - loadingLabel.Height) / 2);
        }

        private void SetExportButtonsEnabled(bool enabled)
        {
            btnExportarCsv.Enabled = enabled;
            btnExportarJson.Enabled = enabled;
            btnExportarTxt.Enabled = enabled;
            btnExportarXml.Enabled = enabled;
        }

        private static Button MakeConnBtn(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Height = 32,
                AutoSize = true,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = fg;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakeActionBtn(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Height = 32,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = fg;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static Button MakeExportBtn(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Height = 32,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = fg;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO: Conexión
    // ════════════════════════════════════════════════════════════════════════
    internal class ConexionDialog : Form
    {
        public string ConnectionString { get; private set; } = "";
        private readonly bool esPostgres;
        private TextBox txtHost = null!, txtPuerto = null!, txtBd = null!,
                         txtUser = null!, txtPass = null!;

        public ConexionDialog(SqlViewerForm.DbTipo tipo)
        {
            esPostgres = tipo == SqlViewerForm.DbTipo.Postgres;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = esPostgres ? "Conectar a PostgreSQL" : "Conectar a MariaDB";
            Size = new Size(420, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(18, 18, 22);
            ForeColor = Color.FromArgb(230, 230, 236);
            Font = new Font("Segoe UI", 9.5F);

            int y = 56;
            void AddRow(string label, ref TextBox tb, string def, bool pass = false)
            {
                Controls.Add(new Label { Text = label, Location = new Point(14, y + 4), AutoSize = true, ForeColor = Color.FromArgb(110, 140, 180) });
                tb = new TextBox { Location = new Point(120, y), Size = new Size(268, 28), BackColor = Color.FromArgb(34, 34, 42), ForeColor = Color.FromArgb(230, 230, 236), BorderStyle = BorderStyle.FixedSingle, Text = def };
                if (pass) tb.PasswordChar = '●';
                Controls.Add(tb);
                y += 36;
            }

            var header = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Color.FromArgb(26, 26, 32) };
            header.Controls.Add(new Label { Text = esPostgres ? "🐘  Conexión PostgreSQL" : "🌿  Conexión MariaDB", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = esPostgres ? Color.FromArgb(95, 189, 235) : Color.FromArgb(60, 180, 120), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0) });
            Controls.Add(header);

            AddRow("Host:", ref txtHost, "localhost");
            AddRow("Puerto:", ref txtPuerto, esPostgres ? "5432" : "3306");
            AddRow("Base de datos:", ref txtBd, "");
            AddRow("Usuario:", ref txtUser, esPostgres ? "postgres" : "root");
            AddRow("Contraseña:", ref txtPass, "", pass: true);

            var btnOk = new Button { Text = "Conectar", Location = new Point(160, y + 8), Size = new Size(110, 36), BackColor = Color.FromArgb(22, 100, 40), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand, DialogResult = DialogResult.OK };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button { Text = "Cancelar", Location = new Point(280, y + 8), Size = new Size(90, 36), BackColor = Color.FromArgb(34, 34, 42), ForeColor = Color.FromArgb(220, 220, 230), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F), Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(44, 44, 54);

            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            string host = txtHost.Text.Trim();
            string puerto = txtPuerto.Text.Trim();
            string bd = txtBd.Text.Trim();
            string user = txtUser.Text.Trim();
            string pass = txtPass.Text;

            if (string.IsNullOrWhiteSpace(bd))
            { MessageBox.Show("Especifica el nombre de la base de datos.", "Campo requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; return; }

            ConnectionString = esPostgres
                ? $"Host={host};Port={puerto};Database={bd};Username={user};Password={pass};Timeout=10;CommandTimeout=60"
                : $"Server={host};Port={puerto};Database={bd};User={user};Password={pass};ConnectionTimeout=10;DefaultCommandTimeout=60";
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO: Nombre de tabla
    // ════════════════════════════════════════════════════════════════════════
    internal class NombreTablaDialog : Form
    {
        public string NombreTabla { get; private set; } = "";
        private TextBox txtNombre = null!;

        public NombreTablaDialog(string sugerido)
        {
            Text = "Nombre de tabla";
            Size = new Size(380, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(18, 18, 22);
            ForeColor = Color.FromArgb(230, 230, 236);
            Font = new Font("Segoe UI", 9.5F);

            Controls.Add(new Label { Text = "Nombre para la nueva tabla:", Location = new Point(14, 20), AutoSize = true, ForeColor = Color.FromArgb(110, 140, 180) });
            txtNombre = new TextBox { Location = new Point(14, 46), Size = new Size(344, 28), BackColor = Color.FromArgb(34, 34, 42), ForeColor = Color.FromArgb(230, 230, 236), BorderStyle = BorderStyle.FixedSingle, Text = sugerido };
            txtNombre.SelectAll();

            var btnOk = new Button { Text = "Crear", Location = new Point(160, 90), Size = new Size(90, 34), BackColor = Color.FromArgb(22, 100, 40), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand, DialogResult = DialogResult.OK };
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnOk.Click += (s, e) =>
            {
                NombreTabla = txtNombre.Text.Trim();
                if (string.IsNullOrWhiteSpace(NombreTabla)) { MessageBox.Show("Ingresa un nombre.", "Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; }
            };

            var btnCancel = new Button { Text = "Cancelar", Location = new Point(260, 90), Size = new Size(90, 34), BackColor = Color.FromArgb(34, 34, 42), ForeColor = Color.FromArgb(220, 220, 230), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F), Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(44, 44, 54);

            Controls.Add(txtNombre); Controls.Add(btnOk); Controls.Add(btnCancel);
            AcceptButton = btnOk; CancelButton = btnCancel;
        }
    }
}