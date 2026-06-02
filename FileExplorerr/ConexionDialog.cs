using System;
using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  CONEXION DIALOG
    //  Collects host, port, database, username and password for a database
    //  connection. Supports PostgreSQL, MariaDB and SQL Server.
    //  Previously defined at the end of SqlViewerForm.cs.
    //  Depends on: SqlViewerForm.DbTipo (for the switch expression in BuildUI)
    // ════════════════════════════════════════════════════════════════════════
    internal class ConexionDialog : Form
    {
        // ── Result ────────────────────────────────────────────────────────
        /// <summary>
        /// The fully-formed connection string built from the fields the user
        /// filled in. Only valid when DialogResult == OK.
        /// </summary>
        public string ConnectionString { get; private set; } = string.Empty;

        // ── State ─────────────────────────────────────────────────────────
        private readonly SqlViewerForm.DbTipo _tipo;

        // ── Controls ──────────────────────────────────────────────────────
        private TextBox _txtHost = null!;
        private TextBox _txtPuerto = null!;
        private TextBox _txtBd = null!;
        private TextBox _txtUser = null!;
        private TextBox _txtPass = null!;
        private CheckBox? _chkTrusted;

        // ── Colours ───────────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        private static readonly Color GreenBtn = Color.FromArgb(22, 100, 40);
        private static readonly Color GreenBrd = Color.FromArgb(35, 134, 54);
        private static readonly Color NeutralBg = Color.FromArgb(34, 34, 42);
        private static readonly Color NeutralBrd = Color.FromArgb(44, 44, 54);

        public ConexionDialog(SqlViewerForm.DbTipo tipo)
        {
            _tipo = tipo;
            BuildUI();
        }

        // ── UI construction ───────────────────────────────────────────────

        private void BuildUI()
        {
            bool esSqlServer = _tipo == SqlViewerForm.DbTipo.SqlServer;

            Text = _tipo switch
            {
                SqlViewerForm.DbTipo.Postgres => "Conectar a PostgreSQL",
                SqlViewerForm.DbTipo.Maria => "Conectar a MariaDB",
                SqlViewerForm.DbTipo.SqlServer => "Conectar a SQL Server",
                _ => "Conectar"
            };

            Size = new Size(420, esSqlServer ? 380 : 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            var (accentColor, headerText) = _tipo switch
            {
                SqlViewerForm.DbTipo.Postgres =>
                    (Color.FromArgb(95, 189, 235), "🐘  Conexión PostgreSQL"),
                SqlViewerForm.DbTipo.Maria =>
                    (Color.FromArgb(60, 180, 120), "🌿  Conexión MariaDB"),
                SqlViewerForm.DbTipo.SqlServer =>
                    (Color.FromArgb(230, 140, 60), "🗄  Conexión SQL Server"),
                _ =>
                    (Color.FromArgb(72, 202, 188), "Conexión")
            };

            var header = new Panel
            { Height = 44, Dock = DockStyle.Top, BackColor = Color.FromArgb(26, 26, 32) };
            header.Controls.Add(new Label
            {
                Text = headerText,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = accentColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            int y = 56;

            void AddRow(string label, ref TextBox tb, string def, bool pass = false)
            {
                Controls.Add(new Label
                {
                    Text = label,
                    Location = new Point(14, y + 4),
                    AutoSize = true,
                    ForeColor = TextSec
                });
                tb = new TextBox
                {
                    Location = new Point(120, y),
                    Size = new Size(268, 28),
                    BackColor = BgField,
                    ForeColor = TextPri,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = def
                };
                if (pass) tb.PasswordChar = '●';
                Controls.Add(tb);
                y += 36;
            }

            bool esPostgres = _tipo == SqlViewerForm.DbTipo.Postgres;
            AddRow("Host:", ref _txtHost, "localhost");
            AddRow("Puerto:", ref _txtPuerto, esPostgres ? "5432" : esSqlServer ? "1433" : "3306");
            AddRow("Base de datos:", ref _txtBd, string.Empty);
            AddRow("Usuario:", ref _txtUser, esPostgres ? "postgres" : esSqlServer ? "sa" : "root");
            AddRow("Contraseña:", ref _txtPass, string.Empty, pass: true);

            if (esSqlServer)
            {
                _chkTrusted = new CheckBox
                {
                    Text = "Autenticación de Windows (Trusted Connection)",
                    Location = new Point(14, y),
                    Size = new Size(374, 24),
                    ForeColor = TextSec,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9F)
                };
                _chkTrusted.CheckedChanged += (_, _) =>
                {
                    _txtUser.Enabled = !_chkTrusted.Checked;
                    _txtPass.Enabled = !_chkTrusted.Checked;
                };
                Controls.Add(_chkTrusted);
                y += 32;
            }

            var btnOk = new Button
            {
                Text = "Conectar",
                Location = new Point(160, y + 8),
                Size = new Size(110, 36),
                BackColor = GreenBtn,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnOk.FlatAppearance.BorderColor = GreenBrd;
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(280, y + 8),
                Size = new Size(90, 36),
                BackColor = NeutralBg,
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = NeutralBrd;

            Controls.Add(header);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        // ── Validation + connection string builder ────────────────────────

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            string host = _txtHost.Text.Trim();
            string port = _txtPuerto.Text.Trim();
            string db = _txtBd.Text.Trim();
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;

            if (string.IsNullOrWhiteSpace(db))
            {
                MessageBox.Show(
                    "Especifica el nombre de la base de datos.",
                    "Campo requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            ConnectionString = _tipo switch
            {
                SqlViewerForm.DbTipo.Postgres =>
                    $"Host={host};Port={port};Database={db};" +
                    $"Username={user};Password={pass};" +
                    "Timeout=10;CommandTimeout=60",

                SqlViewerForm.DbTipo.Maria =>
                    $"Server={host};Port={port};Database={db};" +
                    $"User={user};Password={pass};" +
                    "ConnectionTimeout=10;DefaultCommandTimeout=60",

                SqlViewerForm.DbTipo.SqlServer =>
                    _chkTrusted?.Checked == true
                        ? $"Server={host},{port};Database={db};" +
                          "Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;"
                        : $"Server={host},{port};Database={db};" +
                          $"User Id={user};Password={pass};" +
                          "TrustServerCertificate=True;Connect Timeout=10;",

                _ => string.Empty
            };
        }
    }
}