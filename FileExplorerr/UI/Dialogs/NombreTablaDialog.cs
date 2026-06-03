using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  NOMBRE TABLA DIALOG
    //  Asks the user for the name of a table to create during CSV/JSON import.
    //  Previously defined at the end of SqlViewerForm.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class NombreTablaDialog : Form
    {
        // ── Result ────────────────────────────────────────────────────────
        /// <summary>
        /// The table name entered by the user.
        /// Only valid when DialogResult == OK.
        /// </summary>
        public string NombreTabla { get; private set; } = string.Empty;

        // ── Controls ──────────────────────────────────────────────────────
        private readonly TextBox _txtNombre;

        // ── Colours ───────────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        private static readonly Color GreenBtn = Color.FromArgb(22, 100, 40);
        private static readonly Color GreenBrd = Color.FromArgb(35, 134, 54);
        private static readonly Color NeutralBg = Color.FromArgb(34, 34, 42);
        private static readonly Color NeutralBrd = Color.FromArgb(44, 44, 54);

        public NombreTablaDialog(string suggested)
        {
            Text = "Nombre de tabla";
            Size = new Size(380, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            Controls.Add(new Label
            {
                Text = "Nombre para la nueva tabla:",
                Location = new Point(14, 20),
                AutoSize = true,
                ForeColor = TextSec
            });

            _txtNombre = new TextBox
            {
                Location = new Point(14, 46),
                Size = new Size(344, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Text = suggested
            };
            _txtNombre.SelectAll();
            Controls.Add(_txtNombre);

            var btnOk = new Button
            {
                Text = "Crear",
                Location = new Point(160, 90),
                Size = new Size(90, 34),
                BackColor = GreenBtn,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnOk.FlatAppearance.BorderColor = GreenBrd;
            btnOk.Click += (_, _) =>
            {
                NombreTabla = _txtNombre.Text.Trim();
                if (string.IsNullOrWhiteSpace(NombreTabla))
                {
                    MessageBox.Show(
                        "Ingresa un nombre.",
                        "Requerido",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(260, 90),
                Size = new Size(90, 34),
                BackColor = NeutralBg,
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = NeutralBrd;

            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}