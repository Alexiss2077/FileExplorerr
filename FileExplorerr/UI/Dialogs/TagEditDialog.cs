using System;
using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO DE EDICIÓN DE TAGS — Título, Artista, Álbum, Año, Pista, Género
    //  Tema oscuro consistente con FileExplorerr
    // ════════════════════════════════════════════════════════════════════════
    public class TagEditDialog : Form
    {
        // ── Resultados ───────────────────────────────────────────────────────
        public string Titulo { get; private set; } = "";
        public string Artista { get; private set; } = "";
        public string Album { get; private set; } = "";
        public uint Anio { get; private set; }
        public uint NumPista { get; private set; }
        public string Genero { get; private set; } = "";

        // ── Controles ────────────────────────────────────────────────────────
        private TextBox txtTitulo = null!;
        private TextBox txtArtista = null!;
        private TextBox txtAlbum = null!;
        private TextBox txtAnio = null!;
        private TextBox txtPista = null!;
        private ComboBox cmbGenero = null!;

        // ── Colores ──────────────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(14, 20, 30);
        private static readonly Color BgField = Color.FromArgb(24, 32, 46);
        private static readonly Color BgHeader = Color.FromArgb(17, 23, 33);
        private static readonly Color Border = Color.FromArgb(38, 50, 70);
        private static readonly Color Accent = Color.FromArgb(56, 139, 253);
        private static readonly Color TextPri = Color.FromArgb(220, 232, 248);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);

        // ── Géneros comunes ──────────────────────────────────────────────────
        private static readonly string[] Generos =
        {
            "", "Pop", "Rock", "Hip-Hop", "Rap", "R&B", "Reggaeton",
            "Electrónica", "EDM", "House", "Techno", "Trap",
            "Jazz", "Blues", "Soul", "Funk",
            "Country", "Folk", "Indie",
            "Metal", "Punk", "Grunge", "Alternative",
            "Clásica", "Ópera", "Soundtrack",
            "Reggae", "Ska", "Latin", "Salsa", "Bachata", "Cumbia",
            "K-Pop", "J-Pop", "Anime",
            "Lo-Fi", "Ambient", "Chillout",
            "Gospel", "Corridos", "Norteña", "Banda", "Ranchera",
            "Otro"
        };

        public TagEditDialog(string titulo, string artista, string album,
                             uint anio, uint pista, string genero)
        {
            BuildUI(titulo, artista, album, anio, pista, genero);
        }

        private void BuildUI(string titulo, string artista, string album,
                             uint anio, uint pista, string genero)
        {
            Text = "Editar Tags";
            Size = new Size(460, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            // ── Header ──────────────────────────────────────────────────────
            var header = new Panel
            {
                Height = 44,
                Dock = DockStyle.Top,
                BackColor = BgHeader
            };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border),
                    0, header.Height - 1, header.Width, header.Height - 1);

            var headerLabel = new Label
            {
                Text = "✏️  Editar metadatos",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Accent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            header.Controls.Add(headerLabel);

            // ── Campos ──────────────────────────────────────────────────────
            int y = 58;

            AddLabel("Título:", 14, y);
            txtTitulo = AddTextBox(110, y, 320, titulo);

            y += 36;
            AddLabel("Artista:", 14, y);
            txtArtista = AddTextBox(110, y, 320, artista);

            y += 36;
            AddLabel("Álbum:", 14, y);
            txtAlbum = AddTextBox(110, y, 320, album);

            y += 36;
            AddLabel("Año:", 14, y);
            txtAnio = AddTextBox(110, y, 80, anio > 0 ? anio.ToString() : "");

            AddLabel("Pista #:", 210, y);
            txtPista = AddTextBox(280, y, 60, pista > 0 ? pista.ToString() : "");

            y += 36;
            AddLabel("Género:", 14, y);
            cmbGenero = new ComboBox
            {
                Location = new Point(110, y),
                Size = new Size(200, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            cmbGenero.Items.AddRange(Generos);
            cmbGenero.Text = genero;
            Controls.Add(cmbGenero);

            // ── Botones ─────────────────────────────────────────────────────
            y += 48;

            var btnGuardar = new Button
            {
                Text = "💾  Guardar",
                Location = new Point(200, y),
                Size = new Size(120, 36),
                BackColor = Color.FromArgb(22, 100, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnGuardar.FlatAppearance.BorderColor = Color.FromArgb(35, 134, 54);
            btnGuardar.Click += BtnGuardar_Click;

            var btnCancelar = new Button
            {
                Text = "Cancelar",
                Location = new Point(330, y),
                Size = new Size(100, 36),
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderColor = Border;

            Controls.Add(header);
            Controls.Add(btnGuardar);
            Controls.Add(btnCancelar);
            AcceptButton = btnGuardar;
            CancelButton = btnCancelar;
        }

        private void BtnGuardar_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitulo.Text))
            {
                MessageBox.Show("El título no puede estar vacío.", "Validación",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Titulo = txtTitulo.Text.Trim();
            Artista = txtArtista.Text.Trim();
            Album = txtAlbum.Text.Trim();
            Genero = cmbGenero.Text.Trim();

            if (uint.TryParse(txtAnio.Text.Trim(), out uint a) && a >= 1900 && a <= 2100)
                Anio = a;
            else
                Anio = 0;

            if (uint.TryParse(txtPista.Text.Trim(), out uint p))
                NumPista = p;
            else
                NumPista = 0;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 3),
                AutoSize = true,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F)
            });
        }

        private TextBox AddTextBox(int x, int y, int width, string value)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                Text = value
            };
            Controls.Add(txt);
            return txt;
        }
    }
}