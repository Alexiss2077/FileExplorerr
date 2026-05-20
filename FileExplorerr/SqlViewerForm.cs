using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  SQL VIEWER FORM
    //  Visor completo de bases de datos PostgreSQL / MariaDB.
    //  Permite navegar tablas, ejecutar consultas, exportar e importar datos.
    // ════════════════════════════════════════════════════════════════════════
    public class SqlViewerForm : Form
    {
        // ── Controles principales ────────────────────────────────────────────
        private Panel topBar = null!;
        private SplitContainer mainSplit = null!;   // izq = árbol, der = contenido
        private SplitContainer rightSplit = null!;  // arriba = editor SQL, abajo = grid
        private TreeView tableTree = null!;
        private RichTextBox sqlEditor = null!;
        private DataGridView grid = null!;
        private StatusStrip statusBar = null!;
        private ToolStripStatusLabel lblStatus = null!;
        private ToolStripStatusLabel lblRows = null!;
        private Panel sqlToolbar = null!;

        // ── Conexión ─────────────────────────────────────────────────────────
        private string _cadena = "";
        private string _motor = "";   // "PostgreSQL" | "MariaDB"
        private string _tablaActual = "";

        // ── Colores tema ─────────────────────────────────────────────────────
        private static readonly Color BgBase = Color.FromArgb(13, 13, 18);
        private static readonly Color BgSurface = Color.FromArgb(20, 20, 28);
        private static readonly Color BgElevated = Color.FromArgb(26, 26, 36);
        private static readonly Color BgHeader = Color.FromArgb(16, 16, 23);
        private static readonly Color BorderClr = Color.FromArgb(36, 36, 52);
        private static readonly Color TextPri = Color.FromArgb(225, 225, 235);
        private static readonly Color TextDim = Color.FromArgb(110, 110, 140);
        private static readonly Color Mint = Color.FromArgb(52, 211, 153);
        private static readonly Color Amber = Color.FromArgb(251, 191, 36);
        private static readonly Color Sky = Color.FromArgb(125, 211, 252);
        private static readonly Color Rose = Color.FromArgb(251, 113, 133);
        private static readonly Color MintDim = Color.FromArgb(13, 61, 40);
        private static readonly Color AmbDim = Color.FromArgb(50, 38, 8);
        private static readonly Color RoseDim = Color.FromArgb(60, 18, 18);
        private static readonly Color SkyDim = Color.FromArgb(10, 32, 58);

        public SqlViewerForm()
        {
            BuildUI();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN UI
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "SQL — Sin conexión";
            Size = new Size(1200, 780);
            MinimumSize = new Size(900, 550);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgBase;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5) _ = EjecutarConsultaAsync();
                if (e.Control && e.KeyCode == Keys.Return) _ = EjecutarConsultaAsync();
            };

            // ── TOP BAR ──────────────────────────────────────────────────────
            topBar = new Panel { Height = 48, Dock = DockStyle.Top, BackColor = BgHeader };
            topBar.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = BorderClr });

            int tx = 10;
            AddTopBtn("🐘 PostgreSQL", Mint, MintDim, ref tx, BtnPostgres_Click);
            AddTopBtn("🐬 MariaDB", Amber, AmbDim, ref tx, BtnMariaDB_Click);
            tx += 8;
            var divTop = new Panel { Location = new Point(tx, 10), Size = new Size(1, 28), BackColor = BorderClr };
            topBar.Controls.Add(divTop); tx += 10;
            AddTopBtn("⟳ Actualizar", Sky, SkyDim, ref tx, async (s, e) => await RefrescarArbolAsync());
            AddTopBtn("✕ Desconectar", Rose, RoseDim, ref tx, (s, e) => Desconectar());

            var lblConexion = new Label
            {
                Text = "Sin conexión",
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 360,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 14, 0),
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 8f),
                Tag = "lblConexion"
            };
            topBar.Controls.Add(lblConexion);

            // ── MAIN SPLIT ───────────────────────────────────────────────────
            mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 1,
                BackColor = BorderClr,
                Panel1MinSize = 50,
                Panel2MinSize = 50
            };
            Shown += (s, e) =>
            {
                try { mainSplit.SplitterDistance = 220; } catch { }
                try { rightSplit.SplitterDistance = 120; } catch { }
            };

            // ── ÁRBOL IZQUIERDO ──────────────────────────────────────────────
            BuildTreePanel();

            // ── LADO DERECHO ─────────────────────────────────────────────────
            rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 1,
                BackColor = BorderClr,
                Panel1MinSize = 80,
                Panel2MinSize = 140
            };


            // ── EDITOR SQL ───────────────────────────────────────────────────
            BuildSqlEditor();

            // ── GRID RESULTADOS ──────────────────────────────────────────────
            BuildResultGrid();

            mainSplit.Panel2.Controls.Add(rightSplit);

            // ── STATUS BAR ───────────────────────────────────────────────────
            statusBar = new StatusStrip { BackColor = BgSurface, SizingGrip = false };
            lblStatus = new ToolStripStatusLabel("Listo") { ForeColor = TextDim, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblRows = new ToolStripStatusLabel("") { ForeColor = Mint, TextAlign = ContentAlignment.MiddleRight };
            statusBar.Items.AddRange(new ToolStripItem[] { lblStatus, lblRows });

            Controls.Add(mainSplit);
            Controls.Add(topBar);
            Controls.Add(statusBar);
        }

        // ── ÁRBOL ────────────────────────────────────────────────────────────
        private void BuildTreePanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = BgSurface };

            var hdr = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = BgHeader };
            hdr.Controls.Add(new Label
            {
                Text = "  Tablas",
                Dock = DockStyle.Fill,
                ForeColor = Mint,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            hdr.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = BorderClr });

            tableTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = BgSurface,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                ShowLines = false,
                ShowPlusMinus = true,
                FullRowSelect = true,
                HotTracking = true,
                ItemHeight = 26,
                DrawMode = TreeViewDrawMode.OwnerDrawAll
            };
            tableTree.DrawNode += TableTree_DrawNode;
            tableTree.NodeMouseDoubleClick += async (s, e) =>
            {
                if (e.Node?.Tag is string tablaName)
                    await CargarTablaAsync(tablaName);
            };
            tableTree.NodeMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.Node?.Tag is string tablaName)
                    MostrarMenuTabla(tablaName, e.Location);
            };

            pnl.Controls.Add(tableTree);
            pnl.Controls.Add(hdr);
            mainSplit.Panel1.Controls.Add(pnl);
        }

        private void TableTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null) return;
            bool sel = (e.State & TreeNodeStates.Selected) != 0;
            var g = e.Graphics;
            using var bg = new SolidBrush(sel ? Color.FromArgb(28, 50, 38) : BgSurface);
            g.FillRectangle(bg, new Rectangle(0, e.Bounds.Top, tableTree.Width, e.Bounds.Height));
            if (sel)
            {
                using var acc = new SolidBrush(Mint);
                g.FillRectangle(acc, 0, e.Bounds.Top, 3, e.Bounds.Height);
            }

            bool isHeader = e.Node.Tag == null;
            Color fg = isHeader ? TextDim : (sel ? Mint : TextPri);
            string icon = isHeader ? "⬡" : "▦";
            FontStyle fs = isHeader ? FontStyle.Bold : FontStyle.Regular;
            int indent = (e.Node.Level + 1) * 16 + 6;

            using var fontIcon = new Font("Segoe UI", 8f);
            using var fontText = new Font("Segoe UI", 9f, fs);
            using var brush = new SolidBrush(isHeader ? TextDim : (sel ? Mint : Sky));
            g.DrawString(icon, fontIcon, brush, indent, e.Bounds.Top + (e.Bounds.Height - 14) / 2);
            using var brushTxt = new SolidBrush(fg);
            g.DrawString(e.Node.Text, fontText, brushTxt, indent + 18, e.Bounds.Top + (e.Bounds.Height - 14) / 2);
        }

        // ── EDITOR SQL ───────────────────────────────────────────────────────
        private void BuildSqlEditor()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = BgBase };

            sqlToolbar = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = BgHeader };
            sqlToolbar.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = BorderClr });

            int sx = 8;
            AddSqlBtn("▶  Ejecutar (F5)", Mint, MintDim, ref sx, async (s, e) => await EjecutarConsultaAsync());
            AddSqlBtn("⊘  Limpiar", Rose, RoseDim, ref sx, (s, e) => { sqlEditor.Clear(); });
            sx += 8;
            AddSqlBtn("⬇  Exportar CSV", Amber, AmbDim, ref sx, ExportarResultadosCSV);
            AddSqlBtn("⬆  Importar CSV→BD", Sky, SkyDim, ref sx, async (s, e) => await ImportarCSVaBD());

            var lblHint = new Label
            {
                Text = "Ctrl+Enter o F5 para ejecutar",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 200,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 8f)
            };
            sqlToolbar.Controls.Add(lblHint);

            sqlEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgElevated,
                ForeColor = Sky,
                Font = new Font("Cascadia Code", 10f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                Text = "-- Escribe tu consulta SQL aquí\nSELECT * FROM "
            };
            sqlEditor.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.Return))
                {
                    _ = EjecutarConsultaAsync();
                    e.Handled = e.SuppressKeyPress = true;
                }
            };

            pnl.Controls.Add(sqlEditor);
            pnl.Controls.Add(sqlToolbar);
            rightSplit.Panel1.Controls.Add(pnl);
        }

        // ── GRID RESULTADOS ──────────────────────────────────────────────────
        private void BuildResultGrid()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = BgBase };
            var hdr = new Panel { Height = 32, Dock = DockStyle.Top, BackColor = BgHeader };
            hdr.Controls.Add(new Label
            {
                Text = "  Resultados",
                Dock = DockStyle.Fill,
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            });
            hdr.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = BorderClr });

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = BgBase,
                GridColor = BorderClr,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                RowTemplate = { Height = 26 },
                ScrollBars = ScrollBars.Both,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
            };
            grid.DefaultCellStyle.BackColor = BgBase;
            grid.DefaultCellStyle.ForeColor = TextPri;
            grid.DefaultCellStyle.Font = new Font("Cascadia Code", 8.5f);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 90, 160);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = BgSurface;
            grid.ColumnHeadersDefaultCellStyle.BackColor = BgSurface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Mint;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 32;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;

            // Double buffer via reflexión
            typeof(DataGridView)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(grid, true);

            pnl.Controls.Add(grid);
            pnl.Controls.Add(hdr);
            rightSplit.Panel2.Controls.Add(pnl);
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONEXIÓN
        // ════════════════════════════════════════════════════════════════════

        private async void BtnPostgres_Click(object? sender, EventArgs e)
        {
            using var dlg = new SqlConexionDialog("PostgreSQL");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            SetStatus("Conectando a PostgreSQL...");
            bool ok = await Task.Run(() => SqlConnector.ProbarPostgreSQL(dlg.CadenaConexion, out _));
            if (!ok)
            {
                SqlConnector.ProbarPostgreSQL(dlg.CadenaConexion, out string err);
                MessageBox.Show(err, "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Error al conectar.");
                return;
            }

            _cadena = dlg.CadenaConexion;
            _motor = "PostgreSQL";
            ActualizarLabelConexion();
            Text = $"SQL — PostgreSQL · {dlg.BaseDatos}";
            await RefrescarArbolAsync();
        }

        private async void BtnMariaDB_Click(object? sender, EventArgs e)
        {
            using var dlg = new SqlConexionDialog("MariaDB");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            SetStatus("Conectando a MariaDB...");
            bool ok = await Task.Run(() => SqlConnector.ProbarMariaDB(dlg.CadenaConexion, out _));
            if (!ok)
            {
                SqlConnector.ProbarMariaDB(dlg.CadenaConexion, out string err);
                MessageBox.Show(err, "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Error al conectar.");
                return;
            }

            _cadena = dlg.CadenaConexion;
            _motor = "MariaDB";
            ActualizarLabelConexion();
            Text = $"SQL — MariaDB · {dlg.BaseDatos}";
            await RefrescarArbolAsync();
        }

        private void Desconectar()
        {
            _cadena = "";
            _motor = "";
            _tablaActual = "";
            tableTree.Nodes.Clear();
            grid.DataSource = null;
            grid.Columns.Clear();
            Text = "SQL — Sin conexión";
            ActualizarLabelConexion();
            SetStatus("Desconectado.");
        }

        private void ActualizarLabelConexion()
        {
            var lbl = topBar.Controls.OfType<Label>().FirstOrDefault(l => l.Tag?.ToString() == "lblConexion");
            if (lbl == null) return;
            if (string.IsNullOrEmpty(_motor))
            {
                lbl.Text = "Sin conexión";
                lbl.ForeColor = TextDim;
            }
            else
            {
                lbl.Text = $"● {_motor} conectado";
                lbl.ForeColor = _motor == "PostgreSQL" ? Sky : Amber;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ÁRBOL DE TABLAS
        // ════════════════════════════════════════════════════════════════════

        private async Task RefrescarArbolAsync()
        {
            if (string.IsNullOrEmpty(_cadena)) return;
            SetStatus("Cargando tablas...");

            var tablas = await Task.Run(() =>
                _motor == "PostgreSQL"
                    ? SqlConnector.ObtenerTablasPostgreSQL(_cadena)
                    : SqlConnector.ObtenerTablasMariaDB(_cadena));

            tableTree.BeginUpdate();
            tableTree.Nodes.Clear();
            var root = new TreeNode($"Tablas ({tablas.Count})") { Tag = null };
            foreach (var t in tablas)
                root.Nodes.Add(new TreeNode(t) { Tag = t });
            tableTree.Nodes.Add(root);
            root.Expand();
            tableTree.EndUpdate();
            SetStatus($"{tablas.Count} tabla(s) disponibles.");
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGAR TABLA
        // ════════════════════════════════════════════════════════════════════

        private async Task CargarTablaAsync(string tabla, int limite = 1000)
        {
            if (string.IsNullOrEmpty(_cadena)) return;
            _tablaActual = tabla;

            string sqlTexto = _motor == "PostgreSQL"
                ? $"SELECT * FROM \"{tabla}\" LIMIT {limite};"
                : $"SELECT * FROM `{tabla}` LIMIT {limite};";

            sqlEditor.Text = sqlTexto;
            await EjecutarConsultaAsync(sqlTexto);
        }

        // ════════════════════════════════════════════════════════════════════
        //  EJECUTAR CONSULTA
        // ════════════════════════════════════════════════════════════════════

        private async Task EjecutarConsultaAsync(string? sqlOverride = null)
        {
            if (string.IsNullOrEmpty(_cadena))
            {
                MessageBox.Show("No hay conexión activa.\nConéctate a PostgreSQL o MariaDB primero.",
                    "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string sql = sqlOverride ?? sqlEditor.Text.Trim();
            if (string.IsNullOrEmpty(sql)) return;

            SetStatus("Ejecutando consulta...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var dt = await Task.Run(() =>
                    _motor == "PostgreSQL"
                        ? SqlConnector.EjecutarConsultaPostgreSQL(_cadena, sql)
                        : SqlConnector.EjecutarConsultaMariaDB(_cadena, sql));

                sw.Stop();
                BindGrid(dt);
                SetStatus($"OK · {dt.Rows.Count} fila(s) · {sw.ElapsedMilliseconds} ms");
                lblRows.Text = $"{dt.Rows.Count} filas";
            }
            catch (Exception ex)
            {
                sw.Stop();
                SetStatus($"Error: {ex.Message}");
                MessageBox.Show(ex.Message, "Error SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  BIND GRID
        // ════════════════════════════════════════════════════════════════════

        private void BindGrid(DataTable dt)
        {
            grid.DataSource = null;
            grid.Columns.Clear();
            grid.AutoGenerateColumns = true;
            grid.DataSource = dt;

            // Auto-ajustar ancho mínimo
            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.ReadOnly = true;
                col.MinimumWidth = 60;
                col.SortMode = DataGridViewColumnSortMode.Automatic;
                if (col.Width < 80) col.Width = 80;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR RESULTADOS
        // ════════════════════════════════════════════════════════════════════

        private void ExportarResultadosCSV(object? sender, EventArgs e)
        {
            if (grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Exportar resultados",
                Filter = "CSV|*.csv|Todos|*.*",
                FileName = $"sql_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string csv = SqlConnector.DataTableACsv(dt);
                File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);
                SetStatus($"Exportado → {Path.GetFileName(dlg.FileName)}  ({dt.Rows.Count} filas)");
                MessageBox.Show($"Exportado correctamente:\n{dlg.FileName}", "Exportar",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMPORTAR CSV → BD
        // ════════════════════════════════════════════════════════════════════

        private async Task ImportarCSVaBD()
        {
            if (string.IsNullOrEmpty(_cadena))
            {
                MessageBox.Show("No hay conexión activa.", "Sin conexión",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. Seleccionar CSV
            using var dlgOpen = new OpenFileDialog
            {
                Title = "Selecciona el CSV a importar",
                Filter = "CSV|*.csv|Todos|*.*"
            };
            if (dlgOpen.ShowDialog(this) != DialogResult.OK) return;

            // 2. Nombre de tabla destino
            string nombreTabla = Path.GetFileNameWithoutExtension(dlgOpen.FileName)
                .Replace(" ", "_").ToLowerInvariant();
            nombreTabla = InputDialog("Nombre de tabla destino", "Tabla:", nombreTabla) ?? nombreTabla;
            if (string.IsNullOrWhiteSpace(nombreTabla)) return;

            SetStatus("Leyendo CSV...");
            DataTable? dt = null;
            try
            {
                dt = await Task.Run(() => LeerCsvADataTable(dlgOpen.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer CSV:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show("El CSV está vacío.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetStatus($"Importando {dt.Rows.Count} filas a '{nombreTabla}'...");
            var progreso = new Progress<int>(pct => SetStatus($"Importando... {pct}%"));

            var result = await Task.Run(() =>
                _motor == "PostgreSQL"
                    ? SqlConnector.InsertarDataTablePostgreSQL(_cadena, nombreTabla, dt, progreso)
                    : SqlConnector.InsertarDataTableMariaDB(_cadena, nombreTabla, dt, progreso));

            SetStatus(result.Mensaje);
            MessageBox.Show(
                $"{result.Mensaje}\n\nTabla: {nombreTabla}\nFilas: {result.Insertados:N0}\nErrores: {result.Errores}",
                "Importar CSV",
                MessageBoxButtons.OK,
                result.Exito ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (result.Exito)
                await RefrescarArbolAsync();
        }

        private static DataTable LeerCsvADataTable(string ruta)
        {
            var dt = new DataTable();
            string[] lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            if (lineas.Length == 0) return dt;

            // Detectar separador
            char sep = ',';
            if (!lineas[0].Contains(',') && lineas[0].Contains(';')) sep = ';';
            else if (!lineas[0].Contains(',') && lineas[0].Contains('\t')) sep = '\t';

            // Cabecera
            var headers = SepararLinea(lineas[0], sep);
            foreach (string h in headers)
                dt.Columns.Add(h.Trim('"', ' ').Length > 0 ? h.Trim('"', ' ') : $"col{dt.Columns.Count + 1}");

            for (int i = 1; i < lineas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lineas[i])) continue;
                var cols = SepararLinea(lineas[i], sep);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cols.Count ? cols[c].Trim('"') : "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static System.Collections.Generic.List<string> SepararLinea(string linea, char sep)
        {
            var campos = new System.Collections.Generic.List<string>();
            var actual = new StringBuilder();
            bool enComillas = false;
            foreach (char c in linea)
            {
                if (c == '"') { enComillas = !enComillas; actual.Append(c); }
                else if (c == sep && !enComillas) { campos.Add(actual.ToString()); actual.Clear(); }
                else actual.Append(c);
            }
            campos.Add(actual.ToString());
            return campos;
        }

        // ════════════════════════════════════════════════════════════════════
        //  MENÚ CONTEXTUAL TABLA
        // ════════════════════════════════════════════════════════════════════

        private void MostrarMenuTabla(string tabla, Point ubicacion)
        {
            var menu = new ContextMenuStrip { BackColor = BgElevated, ForeColor = TextPri, Font = new Font("Segoe UI", 9f) };

            var miVer = new ToolStripMenuItem("Ver datos (top 1000)") { ForeColor = Sky };
            miVer.Click += async (s, e) => await CargarTablaAsync(tabla);

            var miContar = new ToolStripMenuItem("Contar filas") { ForeColor = Mint };
            miContar.Click += async (s, e) =>
            {
                string sql = _motor == "PostgreSQL"
                    ? $"SELECT COUNT(*) FROM \"{tabla}\";"
                    : $"SELECT COUNT(*) FROM `{tabla}`;";
                sqlEditor.Text = sql;
                await EjecutarConsultaAsync(sql);
            };

            var miDescribir = new ToolStripMenuItem("Describir columnas") { ForeColor = Amber };
            miDescribir.Click += async (s, e) =>
            {
                string sql = _motor == "PostgreSQL"
                    ? $"SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name='{tabla}' ORDER BY ordinal_position;"
                    : $"DESCRIBE `{tabla}`;";
                sqlEditor.Text = sql;
                await EjecutarConsultaAsync(sql);
            };

            var miExportar = new ToolStripMenuItem("Exportar tabla completa → CSV") { ForeColor = Mint };
            miExportar.Click += async (s, e) => await ExportarTablaCompletaAsync(tabla);

            var miEliminar = new ToolStripMenuItem("DROP TABLE (eliminar)") { ForeColor = Rose };
            miEliminar.Click += async (s, e) =>
            {
                if (MessageBox.Show($"¿Eliminar la tabla '{tabla}' definitivamente?", "DROP TABLE",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                string sql = _motor == "PostgreSQL"
                    ? $"DROP TABLE IF EXISTS \"{tabla}\";"
                    : $"DROP TABLE IF EXISTS `{tabla}`;";
                sqlEditor.Text = sql;
                await EjecutarConsultaAsync(sql);
                await RefrescarArbolAsync();
            };

            menu.Items.AddRange(new ToolStripItem[]
            {
                miVer, miContar, miDescribir,
                new ToolStripSeparator(),
                miExportar,
                new ToolStripSeparator(),
                miEliminar
            });
            menu.Show(tableTree, ubicacion);
        }

        private async Task ExportarTablaCompletaAsync(string tabla)
        {
            if (string.IsNullOrEmpty(_cadena)) return;

            using var dlg = new SaveFileDialog
            {
                Title = "Exportar tabla completa",
                Filter = "CSV|*.csv|Todos|*.*",
                FileName = $"{tabla}_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            SetStatus($"Exportando tabla '{tabla}'...");
            try
            {
                var dt = await Task.Run(() =>
                    _motor == "PostgreSQL"
                        ? SqlConnector.LeerTablaPostgreSQL(_cadena, tabla)
                        : SqlConnector.LeerTablaMariaDB(_cadena, tabla));

                string csv = SqlConnector.DataTableACsv(dt);
                await File.WriteAllTextAsync(dlg.FileName, csv, Encoding.UTF8);
                SetStatus($"Exportado '{tabla}' → {Path.GetFileName(dlg.FileName)}  ({dt.Rows.Count} filas)");
                MessageBox.Show(
                    $"Tabla '{tabla}' exportada.\nFilas: {dt.Rows.Count:N0}\nArchivo: {dlg.FileName}",
                    "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private void SetStatus(string msg)
        {
            if (statusBar.InvokeRequired)
                statusBar.Invoke(() => lblStatus.Text = msg);
            else
                lblStatus.Text = msg;
        }

        private void AddTopBtn(string text, Color fg, Color bg, ref int x, EventHandler click)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 9),
                Height = 30,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.1f);
            btn.Click += click;
            topBar.Controls.Add(btn);
            btn.PerformLayout();
            x += btn.Width + 4;
        }

        private void AddSqlBtn(string text, Color fg, Color bg, ref int x, EventHandler click)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, 4),
                Height = 28,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.1f);
            btn.Click += click;
            sqlToolbar.Controls.Add(btn);
            btn.PerformLayout();
            x += btn.Width + 4;
        }

        private string? InputDialog(string titulo, string prompt, string def = "")
        {
            using var dlg = new Form
            {
                Text = titulo,
                Width = 380,
                Height = 150,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = BgSurface,
                ForeColor = TextPri
            };
            var lbl = new Label { Text = prompt, Left = 14, Top = 18, Width = 340, ForeColor = TextDim };
            var txt = new TextBox
            {
                Text = def,
                Left = 14,
                Top = 44,
                Width = 340,
                BackColor = BgElevated,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle
            };
            txt.SelectAll();
            var ok = new Button
            {
                Text = "Aceptar",
                Left = 196,
                Top = 82,
                Width = 80,
                BackColor = MintDim,
                ForeColor = Mint,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            ok.FlatAppearance.BorderSize = 0;
            var cancel = new Button
            {
                Text = "Cancelar",
                Left = 286,
                Top = 82,
                Width = 70,
                BackColor = RoseDim,
                ForeColor = Rose,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            cancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO DE CONEXIÓN SQL
    // ════════════════════════════════════════════════════════════════════════
    public class SqlConexionDialog : Form
    {
        public string CadenaConexion { get; private set; } = "";
        public string BaseDatos { get; private set; } = "";

        private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtPass;
        private readonly Label lblEstado;
        private readonly Button btnDetectar;
        private readonly bool _esPg;

        private static readonly Color BgForm = Color.FromArgb(18, 18, 26);
        private static readonly Color BgSurface = Color.FromArgb(26, 26, 36);
        private static readonly Color TextPri = Color.FromArgb(220, 220, 232);
        private static readonly Color TextDim = Color.FromArgb(100, 100, 130);
        private static readonly Color Mint = Color.FromArgb(52, 211, 153);
        private static readonly Color MintDim = Color.FromArgb(13, 50, 35);
        private static readonly Color Rose = Color.FromArgb(200, 100, 100);
        private static readonly Color RoseDim = Color.FromArgb(40, 18, 18);
        private static readonly Color BorderClr = Color.FromArgb(36, 36, 52);

        public SqlConexionDialog(string motor)
        {
            _esPg = motor == "PostgreSQL";
            string pd = _esPg ? "5432" : "3306";
            string ud = _esPg ? "postgres" : "root";

            Text = $"Conexión — {motor}";
            Size = new Size(440, 360);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9f);

            int y = 16;
            const int lx = 14, cx = 130, cw = 280;

            Label Lbl(string t) => new() { Text = t, AutoSize = true, ForeColor = TextDim };
            TextBox Txt(string d, bool p = false) => new()
            {
                Width = cw,
                Text = d,
                BackColor = BgSurface,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = p
            };

            var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
            txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 34;

            var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
            txtPuerto = Txt(pd); txtPuerto.Location = new Point(cx, y); y += 34;

            var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
            txtBD = Txt(""); txtBD.Location = new Point(cx, y); y += 34;

            var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
            txtUsuario = Txt(ud); txtUsuario.Location = new Point(cx, y); y += 34;

            var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
            txtPass = Txt("", true); txtPass.Location = new Point(cx, y); y += 38;

            btnDetectar = new Button
            {
                Text = "Probar conexión",
                Location = new Point(cx, y),
                Width = 130,
                Height = 28,
                BackColor = Color.FromArgb(8, 34, 55),
                ForeColor = Color.FromArgb(125, 211, 252),
                FlatStyle = FlatStyle.Flat
            };
            btnDetectar.FlatAppearance.BorderSize = 0;
            btnDetectar.Click += BtnProbar_Click;
            y += 34;

            lblEstado = new Label
            {
                Location = new Point(cx, y),
                Size = new Size(cw, 18),
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 7.8f)
            };
            y += 28;

            var ok = new Button
            {
                Text = "Conectar",
                Location = new Point(240, y),
                Width = 88,
                Height = 28,
                BackColor = MintDim,
                ForeColor = Mint,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtBD.Text))
                {
                    MessageBox.Show("Escribe el nombre de la base de datos.", "Requerido",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None; return;
                }
                BaseDatos = txtBD.Text.Trim();
                string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
                string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (_esPg ? "5432" : "3306") : txtPuerto.Text.Trim();
                string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (_esPg ? "postgres" : "root") : txtUsuario.Text.Trim();
                CadenaConexion = _esPg
                    ? $"Host={h};Port={p};Database={BaseDatos};Username={u};Password={txtPass.Text};"
                    : $"Server={h};Port={p};Database={BaseDatos};User={u};Password={txtPass.Text};";
            };

            var can = new Button
            {
                Text = "Cancelar",
                Location = new Point(336, y),
                Width = 80,
                Height = 28,
                BackColor = RoseDim,
                ForeColor = Rose,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            can.FlatAppearance.BorderSize = 0;

            ClientSize = new Size(428, y + 48);
            Controls.AddRange(new Control[]
            {
                l1, txtHost, l2, txtPuerto, l3, txtBD,
                l4, txtUsuario, l5, txtPass,
                btnDetectar, lblEstado, ok, can
            });
            AcceptButton = ok;
            CancelButton = can;
        }

        private async void BtnProbar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBD.Text))
            {
                lblEstado.Text = "Escribe el nombre de la BD primero.";
                lblEstado.ForeColor = Color.FromArgb(180, 140, 50);
                return;
            }
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (_esPg ? "5432" : "3306") : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (_esPg ? "postgres" : "root") : txtUsuario.Text.Trim();
            string cadena = _esPg
                ? $"Host={h};Port={p};Database={txtBD.Text.Trim()};Username={u};Password={txtPass.Text};"
                : $"Server={h};Port={p};Database={txtBD.Text.Trim()};User={u};Password={txtPass.Text};";

            btnDetectar.Enabled = false;
            lblEstado.Text = "Probando...";
            lblEstado.ForeColor = Color.FromArgb(180, 140, 30);

            try
            {
                bool ok = await Task.Run(() =>
                    _esPg
                        ? SqlConnector.ProbarPostgreSQL(cadena, out _)
                        : SqlConnector.ProbarMariaDB(cadena, out _));

                if (ok)
                {
                    lblEstado.Text = "✓ Conexión exitosa";
                    lblEstado.ForeColor = Color.FromArgb(52, 180, 120);
                }
                else
                {
                    string msg = "";
                    if (_esPg) SqlConnector.ProbarPostgreSQL(cadena, out msg);
                    else SqlConnector.ProbarMariaDB(cadena, out msg);
                    lblEstado.Text = $"✗ {msg}";
                    lblEstado.ForeColor = Color.FromArgb(200, 80, 80);
                }
            }
            finally { btnDetectar.Enabled = true; }
        }
    }
}