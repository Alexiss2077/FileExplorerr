using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  DIÁLOGO DE EDICIÓN DE TAGS — Título, Artista, Álbum, Año, Pista, Género, Carátula
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

        // Propiedades para la carátula
        public byte[]? NewCoverData { get; private set; } = null;
        public bool CoverRemoved { get; private set; } = false;

        // ── Controles ────────────────────────────────────────────────────────
        private TextBox txtTitulo = null!;
        private TextBox txtArtista = null!;
        private TextBox txtAlbum = null!;
        private TextBox txtAnio = null!;
        private TextBox txtPista = null!;
        private ComboBox cmbGenero = null!;
        private PictureBox pbCover = null!;

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
                             uint anio, uint pista, string genero, Image? currentCover = null)
        {
            BuildUI(titulo, artista, album, anio, pista, genero, currentCover);
        }

        private void BuildUI(string titulo, string artista, string album,
                             uint anio, uint pista, string genero, Image? currentCover)
        {
            Text = "Editar Tags";
            Size = new Size(680, 390);
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
            Controls.Add(header);

            // ── Medidas de layout ────────────────────────────────────────────
            int leftX = 14;
            int fieldX = 110;
            int fieldW = 290;
            int coverX = 430;
            int coverW = 210;
            int coverH = 210;
            int y = 58;

            // ── Campos de texto ──────────────────────────────────────────────
            AddLabel("Título:", leftX, y);
            txtTitulo = AddTextBox(fieldX, y, fieldW, titulo);

            y += 36;
            AddLabel("Artista:", leftX, y);
            txtArtista = AddTextBox(fieldX, y, fieldW, artista);

            y += 36;
            AddLabel("Álbum:", leftX, y);
            txtAlbum = AddTextBox(fieldX, y, fieldW, album);

            y += 36;
            AddLabel("Año:", leftX, y);
            txtAnio = AddTextBox(fieldX, y, 80, anio > 0 ? anio.ToString() : "");
            AddLabel("Pista #:", fieldX + 95, y);
            txtPista = AddTextBox(fieldX + 165, y, 60, pista > 0 ? pista.ToString() : "");

            y += 36;
            AddLabel("Género:", leftX, y);
            cmbGenero = new ComboBox
            {
                Location = new Point(fieldX, y),
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

            // ── Carátula (columna derecha) ───────────────────────────────────
            int coverY = 58;

            pbCover = new PictureBox
            {
                Location = new Point(coverX, coverY),
                Size = new Size(coverW, coverH),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = BgField,
                BorderStyle = BorderStyle.FixedSingle,
                Image = currentCover
            };
            Controls.Add(pbCover);

            // Label "Sin carátula" visible cuando no hay imagen
            var lblNoCover = new Label
            {
                Text = "Sin carátula",
                Location = new Point(coverX, coverY),
                Size = new Size(coverW, coverH),
                ForeColor = TextSec,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F),
                Visible = currentCover == null
            };
            Controls.Add(lblNoCover);

            // Sincronizar visibilidad del label con el PictureBox
            pbCover.Paint += (s, e) => lblNoCover.Visible = pbCover.Image == null;

            // ── Botones Cambiar / Quitar — justo debajo del PictureBox ───────
            int btnCoverY = coverY + coverH + 6;

            var btnChangeCover = new Button
            {
                Text = "🖼 Cambiar",
                Location = new Point(coverX, btnCoverY),
                Size = new Size(100, 28),
                BackColor = Color.FromArgb(30, 215, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnChangeCover.FlatAppearance.BorderSize = 0;
            btnChangeCover.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Seleccionar nueva carátula",
                    Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp",
                    Multiselect = false
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    NewCoverData = File.ReadAllBytes(ofd.FileName);
                    CoverRemoved = false;
                    pbCover.Image?.Dispose();
                    pbCover.Image = Image.FromFile(ofd.FileName);
                    lblNoCover.Visible = false;
                }
            };

            var btnRemoveCover = new Button
            {
                Text = "✕ Quitar",
                Location = new Point(coverX + 108, btnCoverY),
                Size = new Size(100, 28),
                BackColor = Color.FromArgb(248, 113, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveCover.FlatAppearance.BorderSize = 0;
            btnRemoveCover.Click += (s, e) =>
            {
                NewCoverData = null;
                CoverRemoved = true;
                pbCover.Image?.Dispose();
                pbCover.Image = null;
                lblNoCover.Visible = true;
            };

            Controls.Add(btnChangeCover);
            Controls.Add(btnRemoveCover);

            // ── Botones Guardar / Cancelar — posición fija en la parte baja ──
            int bottomY = 320;

            var btnGuardar = new Button
            {
                Text = "💾  Guardar",
                Location = new Point(430, bottomY),
                Size = new Size(110, 36),
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
                Location = new Point(548, bottomY),
                Size = new Size(100, 36),
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancelar.FlatAppearance.BorderColor = Border;

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