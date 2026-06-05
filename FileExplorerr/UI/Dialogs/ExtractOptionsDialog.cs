using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  EXTRACT OPTIONS DIALOG
    //  Presenta tres destinos de extracción mutuamente excluyentes:
    //
    //    1. "Extraer aquí"
    //       Los archivos van directamente a la carpeta que contiene el ZIP.
    //       E.g.  C:\Downloads\proyecto.zip  →  C:\Downloads\
    //
    //    2. "Extraer en subcarpeta"  (default, recomendado)
    //       Se crea una carpeta con el nombre del ZIP junto a él.
    //       E.g.  C:\Downloads\proyecto.zip  →  C:\Downloads\proyecto\
    //
    //    3. "Elegir carpeta..."
    //       Abre un FolderBrowserDialog para que el usuario elija cualquier destino.
    //
    //  Retorna la ruta elegida en SelectedDestination,
    //  o null si el usuario canceló.
    //  También expone OverwriteExisting (checkbox).
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class ExtractOptionsDialog : Form
    {
        // ── Resultados ────────────────────────────────────────────────────

        /// <summary>
        /// Ruta completa de la carpeta de destino elegida por el usuario.
        /// null cuando el usuario canceló.
        /// Solo válido cuando DialogResult == OK.
        /// </summary>
        public string? SelectedDestination { get; private set; }

        /// <summary>
        /// Cuando true, los archivos existentes en el destino se sobreescriben.
        /// Refleja el estado del checkbox "Sobreescribir".
        /// </summary>
        public bool OverwriteExisting { get; private set; }

        // ── Estado ────────────────────────────────────────────────────────

        private readonly string _archivePath;
        private readonly string _archiveFolder;
        private readonly string _archiveNameNoExt;

        // ── Controles ─────────────────────────────────────────────────────

        private RadioButton _rdoHere = null!;
        private RadioButton _rdoSubfolder = null!;
        private RadioButton _rdoChoose = null!;
        private Label _lblPreview = null!;
        private TextBox _txtCustom = null!;
        private Button _btnBrowse = null!;
        private CheckBox _chkOverwrite = null!;
        private Button _btnExtract = null!;
        private Button _btnCancel = null!;

        // ── Paleta Arctic Night ───────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgHeader = Color.FromArgb(26, 26, 32);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        private static readonly Color TextMuted = Color.FromArgb(70, 80, 110);
        private static readonly Color AccentVio = Color.FromArgb(124, 111, 247);
        private static readonly Color GreenBtn = Color.FromArgb(22, 100, 40);
        private static readonly Color GreenBrd = Color.FromArgb(35, 134, 54);
        private static readonly Color NeutralBg = Color.FromArgb(34, 34, 42);
        private static readonly Color NeutralBrd = Color.FromArgb(44, 44, 54);

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        public ExtractOptionsDialog(string archivePath)
        {
            _archivePath = archivePath;
            _archiveFolder = Path.GetDirectoryName(archivePath) ?? string.Empty;
            _archiveNameNoExt = Path.GetFileNameWithoutExtension(archivePath);

            BuildUI();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN DE UI
        // ════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Text = "Extraer archivo ZIP";
            Size = new Size(500, 490);   // altura aumentada para mostrar botones
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = BgForm;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 9.5F);

            // ── Header ────────────────────────────────────────────────────
            var header = new Panel { Height = 46, Dock = DockStyle.Top, BackColor = BgHeader };
            header.Controls.Add(new Label
            {
                Text = "📦  Extraer archivo ZIP",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentVio,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            // ── Nombre del archivo ────────────────────────────────────────
            var lblArchive = new Label
            {
                Text = $"Archivo: {Path.GetFileName(_archivePath)}",
                Left = 14,
                Top = 56,
                Width = 462,
                Height = 18,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 8.5F),
                AutoEllipsis = true
            };

            // ── Radio 1: Extraer aquí ─────────────────────────────────────
            _rdoHere = MakeRadio(
                "Extraer aquí",
                $"→ {_archiveFolder}",
                14, 84);

            // ── Radio 2: Extraer en subcarpeta ────────────────────────────
            _rdoSubfolder = MakeRadio(
                "Extraer en subcarpeta  (recomendado)",
                $"→ {Path.Combine(_archiveFolder, _archiveNameNoExt)}",
                14, 136);

            // ── Radio 3: Elegir carpeta ───────────────────────────────────
            _rdoChoose = MakeRadio(
                "Elegir carpeta...",
                string.Empty,
                14, 188);

            // Fila de carpeta personalizada
            _txtCustom = new TextBox
            {
                Left = 32,
                Top = 218,
                Width = 360,
                Height = 28,
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                ReadOnly = true,
                Enabled = false,
                Text = string.Empty
            };

            _btnBrowse = new Button
            {
                Text = "...",
                Left = 398,
                Top = 218,
                Width = 44,
                Height = 28,
                BackColor = NeutralBg,
                ForeColor = TextSec,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnBrowse.FlatAppearance.BorderColor = NeutralBrd;
            _btnBrowse.Click += BtnBrowse_Click;

            // ── Preview de destino ────────────────────────────────────────
            _lblPreview = new Label
            {
                Left = 14,
                Top = 254,
                Width = 462,
                Height = 18,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                AutoEllipsis = true
            };

            // ── Checkbox sobreescribir ────────────────────────────────────
            _chkOverwrite = new CheckBox
            {
                Text = "Sobreescribir archivos existentes",
                Left = 14,
                Top = 282,
                Width = 320,
                Height = 22,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F),
                Checked = false,
                BackColor = Color.Transparent
            };

            // ── Botones ───────────────────────────────────────────────────
            _btnExtract = new Button
            {
                Text = "Extraer",
                Left = 274,
                Top = 396,
                Width = 100,
                Height = 36,
                BackColor = GreenBtn,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            _btnExtract.FlatAppearance.BorderColor = GreenBrd;
            _btnExtract.Click += BtnExtract_Click;

            _btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 384,
                Top = 396,
                Width = 96,
                Height = 36,
                BackColor = NeutralBg,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            _btnCancel.FlatAppearance.BorderColor = NeutralBrd;

            // ── Suscribir eventos DESPUÉS de crear _btnExtract ────────────
            // (evita NullReferenceException en UpdatePreview durante construcción)
            _rdoHere.CheckedChanged += (_, _) => UpdatePreview();
            _rdoSubfolder.CheckedChanged += (_, _) => UpdatePreview();
            _rdoChoose.CheckedChanged += (_, _) => UpdatePreview();

            // Selección por defecto
            _rdoSubfolder.Checked = true;

            // ── Ensamblar controles ───────────────────────────────────────
            Controls.Add(header);
            Controls.Add(lblArchive);
            Controls.Add(_rdoHere);
            Controls.Add(_rdoSubfolder);
            Controls.Add(_rdoChoose);
            Controls.Add(_txtCustom);
            Controls.Add(_btnBrowse);
            Controls.Add(_lblPreview);
            Controls.Add(_chkOverwrite);
            Controls.Add(_btnExtract);
            Controls.Add(_btnCancel);

            AcceptButton = _btnExtract;
            CancelButton = _btnCancel;

            // Estado inicial
            UpdatePreview();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY: radio button con subtítulo
        // ════════════════════════════════════════════════════════════════════

        private RadioButton MakeRadio(string title, string subtitle, int left, int top)
        {
            var rdo = new RadioButton
            {
                Text = title,
                Left = left,
                Top = top,
                Width = 462,
                Height = 22,
                ForeColor = Color.FromArgb(220, 220, 236),
                Font = new Font("Segoe UI", 9.5F),
                BackColor = Color.Transparent
            };

            if (!string.IsNullOrEmpty(subtitle))
            {
                Controls.Add(new Label
                {
                    Text = subtitle,
                    Left = left + 20,
                    Top = top + 22,
                    Width = 440,
                    Height = 16,
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 8F),
                    AutoEllipsis = true
                });
            }

            return rdo;
        }

        // ════════════════════════════════════════════════════════════════════
        //  EVENTOS
        // ════════════════════════════════════════════════════════════════════

        private void UpdatePreview()
        {
            // Guard: evita NullReferenceException si se llama antes de que
            // _btnExtract esté creado (no debería ocurrir con el orden actual).
            if (_btnExtract is null) return;

            bool chooseMode = _rdoChoose.Checked;
            _txtCustom.Enabled = chooseMode;
            _btnBrowse.Enabled = chooseMode;

            string destination = ComputeDestination();
            _lblPreview.Text = string.IsNullOrEmpty(destination)
                ? "Selecciona una carpeta de destino."
                : $"Destino: {destination}";

            _btnExtract.Enabled = !string.IsNullOrEmpty(destination);
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Selecciona la carpeta de destino",
                UseDescriptionForTitle = true,
                SelectedPath = _archiveFolder
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtCustom.Text = dlg.SelectedPath;
                UpdatePreview();
            }
        }

        private void BtnExtract_Click(object? sender, EventArgs e)
        {
            string dest = ComputeDestination();

            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show(
                    "Selecciona una carpeta de destino para continuar.",
                    "Destino requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            SelectedDestination = dest;
            OverwriteExisting = _chkOverwrite.Checked;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CÁLCULO DE DESTINO
        // ════════════════════════════════════════════════════════════════════

        private string ComputeDestination()
        {
            if (_rdoHere.Checked)
                return _archiveFolder;

            if (_rdoSubfolder.Checked)
                return Path.Combine(_archiveFolder, _archiveNameNoExt);

            // _rdoChoose
            return _txtCustom.Text.Trim();
        }
    }
}