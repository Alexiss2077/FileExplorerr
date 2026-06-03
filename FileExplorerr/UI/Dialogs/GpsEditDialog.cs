using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  GPS EDIT DIALOG
    //  Allows the user to enter or edit GPS coordinates for an image.
    //  Previously defined at the end of ImagevIewerform.cs.
    //  Depends on: GpsData (GpsData.cs)
    // ════════════════════════════════════════════════════════════════════════
    internal class GpsEditDialog : Form
    {
        // ── Results ───────────────────────────────────────────────────────
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double? Altitude { get; private set; }

        // ── Controls ──────────────────────────────────────────────────────
        private readonly TextBox _txtLat;
        private readonly TextBox _txtLon;
        private readonly TextBox _txtAlt;

        // ── Colours ───────────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color BgHeader = Color.FromArgb(26, 26, 32);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        private static readonly Color GreenAcc = Color.FromArgb(82, 196, 120);
        private static readonly Color GreenBtn = Color.FromArgb(22, 100, 40);
        private static readonly Color GreenBrd = Color.FromArgb(35, 134, 54);
        private static readonly Color NeutralBg = Color.FromArgb(34, 34, 42);
        private static readonly Color NeutralBrd = Color.FromArgb(44, 44, 54);

        public GpsEditDialog(GpsData? existing)
        {
            Text = existing?.HasGps == true
                ? "Editar coordenadas GPS"
                : "Agregar coordenadas GPS";
            Size = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            // ── Header ────────────────────────────────────────────────────
            var header = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = BgHeader };
            header.Controls.Add(new Label
            {
                Text = "📍  Coordenadas GPS",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = GreenAcc,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            // ── Info label ────────────────────────────────────────────────
            var lblInfo = new Label
            {
                Text = "Ingresa las coordenadas en formato decimal.\n" +
                             "Ejemplo: Lat 27.057918, Lon -101.543602",
                Location = new Point(14, 56),
                Size = new Size(380, 36),
                ForeColor = Color.FromArgb(140, 140, 156),
                Font = new Font("Segoe UI", 8.5F)
            };

            // ── Fields ────────────────────────────────────────────────────
            AddFieldLabel("Latitud:", 14, 100);
            _txtLat = AddField(120, 98, 260,
                existing?.HasGps == true
                    ? existing.Latitude.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);

            AddFieldLabel("Longitud:", 14, 136);
            _txtLon = AddField(120, 134, 260,
                existing?.HasGps == true
                    ? existing.Longitude.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);

            AddFieldLabel("Altitud (m):", 14, 172);
            _txtAlt = AddField(120, 170, 120,
                existing?.Altitude.HasValue == true
                    ? existing.Altitude.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);

            // ── Buttons ───────────────────────────────────────────────────
            var btnSave = new Button
            {
                Text = "Guardar GPS",
                Location = new Point(170, 210),
                Size = new Size(120, 36),
                BackColor = GreenBtn,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderColor = GreenBrd;
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(300, 210),
                Size = new Size(100, 36),
                BackColor = NeutralBg,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = NeutralBrd;

            Controls.AddRange(new Control[] { header, lblInfo, btnSave, btnCancel });
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(_txtLat.Text.Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double lat)
                || Math.Abs(lat) > 90)
            {
                MessageBox.Show(
                    "Latitud inválida. Debe ser un número entre -90 y 90.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (!double.TryParse(_txtLon.Text.Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double lon)
                || Math.Abs(lon) > 180)
            {
                MessageBox.Show(
                    "Longitud inválida. Debe ser un número entre -180 y 180.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Latitude = lat;
            Longitude = lon;
            Altitude = double.TryParse(_txtAlt.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double alt)
                ? alt
                : null;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void AddFieldLabel(string text, int x, int y) =>
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F)
            });

        private TextBox AddField(int x, int y, int width, string value)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Cascadia Code", 10F),
                Text = value
            };
            Controls.Add(txt);
            return txt;
        }
    }
}