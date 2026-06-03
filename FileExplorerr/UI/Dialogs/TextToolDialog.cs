using System;
using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  TEXT TOOL DIALOG
    //  Lets the user choose font, size, style, colour and content before
    //  stamping text onto an image in ImageViewerForm.
    //  Previously defined at the end of ImagevIewerform.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class TextToolDialog : Form
    {
        // ── Results ───────────────────────────────────────────────────────
        public string TextContent { get; private set; } = string.Empty;
        public string SelectedFontFamily { get; private set; }
        public float SelectedFontSize { get; private set; }
        public FontStyle SelectedFontStyle { get; private set; }
        public Color SelectedColor { get; private set; }

        // ── Controls ──────────────────────────────────────────────────────
        private readonly TextBox _txtContent;
        private readonly ComboBox _cmbFont;
        private readonly NumericUpDown _nudSize;
        private readonly CheckBox _chkBold;
        private readonly CheckBox _chkItalic;
        private readonly CheckBox _chkUnderline;
        private readonly Panel _colorSwatch;
        private readonly Label _previewLabel;
        private Color _currentColor;

        // ── Colour palette ────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(18, 18, 22);
        private static readonly Color BgField = Color.FromArgb(34, 34, 42);
        private static readonly Color BgHeader = Color.FromArgb(26, 26, 32);
        private static readonly Color BorderCol = Color.FromArgb(44, 44, 54);
        private static readonly Color AccentCol = Color.FromArgb(72, 202, 188);
        private static readonly Color TextPri = Color.FromArgb(230, 230, 236);
        private static readonly Color TextSec = Color.FromArgb(110, 140, 180);
        private static readonly Color GreenBtn = Color.FromArgb(22, 100, 40);
        private static readonly Color GreenBrd = Color.FromArgb(35, 134, 54);

        // ── Common font families ──────────────────────────────────────────
        private static readonly string[] FontFamilies =
        {
            "Segoe UI", "Arial", "Times New Roman", "Courier New", "Verdana",
            "Georgia", "Trebuchet MS", "Impact", "Comic Sans MS", "Tahoma",
            "Calibri", "Cambria", "Consolas", "Cascadia Code", "Palatino Linotype"
        };

        public TextToolDialog(
            string fontFamily,
            float fontSize,
            FontStyle fontStyle,
            Color color)
        {
            SelectedFontFamily = fontFamily;
            SelectedFontSize = fontSize;
            SelectedFontStyle = fontStyle;
            SelectedColor = color;
            _currentColor = color;

            Text = "Insertar texto";
            Size = new Size(520, 420);
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
                Text = "✏  Insertar texto en imagen",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentCol,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });

            // ── Text content ──────────────────────────────────────────────
            AddLabel("Texto:", 14, 58);
            _txtContent = new TextBox
            {
                Location = new Point(110, 56),
                Size = new Size(384, 28),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            _txtContent.TextChanged += (_, _) => UpdatePreview();

            // ── Font family ───────────────────────────────────────────────
            AddLabel("Fuente:", 14, 100);
            _cmbFont = new ComboBox
            {
                Location = new Point(110, 98),
                Size = new Size(230, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };
            foreach (string fam in FontFamilies) _cmbFont.Items.Add(fam);
            int fontIdx = _cmbFont.Items.IndexOf(SelectedFontFamily);
            _cmbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;
            _cmbFont.SelectedIndexChanged += (_, _) => UpdatePreview();

            // ── Font size ─────────────────────────────────────────────────
            AddLabel("Tamaño:", 354, 100);
            _nudSize = new NumericUpDown
            {
                Location = new Point(420, 98),
                Size = new Size(74, 28),
                Minimum = 6,
                Maximum = 200,
                Value = (decimal)Math.Clamp(SelectedFontSize, 6, 200),
                BackColor = BgField,
                ForeColor = TextPri,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F)
            };
            _nudSize.ValueChanged += (_, _) => UpdatePreview();

            // ── Style checkboxes ──────────────────────────────────────────
            AddLabel("Estilo:", 14, 142);
            _chkBold = MakeCheckBox("Negrita", 110, 140);
            _chkItalic = MakeCheckBox("Cursiva", 188, 140);
            _chkUnderline = MakeCheckBox("Subrayado", 266, 140);
            _chkBold.Checked = (SelectedFontStyle & FontStyle.Bold) != 0;
            _chkItalic.Checked = (SelectedFontStyle & FontStyle.Italic) != 0;
            _chkUnderline.Checked = (SelectedFontStyle & FontStyle.Underline) != 0;
            _chkBold.CheckedChanged += (_, _) => UpdatePreview();
            _chkItalic.CheckedChanged += (_, _) => UpdatePreview();
            _chkUnderline.CheckedChanged += (_, _) => UpdatePreview();

            // ── Colour picker ─────────────────────────────────────────────
            AddLabel("Color:", 14, 184);
            _colorSwatch = new Panel
            {
                Location = new Point(110, 182),
                Size = new Size(44, 26),
                BackColor = _currentColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            _colorSwatch.Click += PickColor;

            var btnColorPick = new Button
            {
                Text = "Elegir color...",
                Location = new Point(162, 181),
                Size = new Size(110, 28),
                BackColor = BgField,
                ForeColor = TextSec,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btnColorPick.FlatAppearance.BorderColor = BorderCol;
            btnColorPick.Click += PickColor;

            // ── Preview area ──────────────────────────────────────────────
            var previewBox = new Panel
            {
                Location = new Point(14, 222),
                Size = new Size(480, 90),
                BackColor = Color.FromArgb(10, 10, 14),
                BorderStyle = BorderStyle.FixedSingle
            };
            _previewLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Text = "Vista previa"
            };
            previewBox.Controls.Add(_previewLabel);

            var lblPreviewHint = new Label
            {
                Text = "Vista previa",
                Location = new Point(14, 208),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = TextSec
            };

            // ── Buttons ───────────────────────────────────────────────────
            var btnOk = new Button
            {
                Text = "Insertar",
                Location = new Point(280, 326),
                Size = new Size(100, 36),
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
                Location = new Point(390, 326),
                Size = new Size(100, 36),
                BackColor = BgField,
                ForeColor = TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = BorderCol;

            Controls.AddRange(new Control[]
            {
                header, _txtContent, _cmbFont, _nudSize,
                _chkBold, _chkItalic, _chkUnderline,
                _colorSwatch, btnColorPick,
                lblPreviewHint, previewBox,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            UpdatePreview();
            _txtContent.Focus();
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void PickColor(object? sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _currentColor, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _currentColor = dlg.Color;
                _colorSwatch.BackColor = _currentColor;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            try
            {
                FontStyle style = FontStyle.Regular;
                if (_chkBold?.Checked == true) style |= FontStyle.Bold;
                if (_chkItalic?.Checked == true) style |= FontStyle.Italic;
                if (_chkUnderline?.Checked == true) style |= FontStyle.Underline;

                string family = _cmbFont?.SelectedItem?.ToString() ?? "Segoe UI";
                float size = (float)(_nudSize?.Value ?? 14);

                _previewLabel.Font?.Dispose();
                _previewLabel.Font = new Font(family, Math.Min(size, 40), style);
                _previewLabel.ForeColor = _currentColor;
                _previewLabel.Text = string.IsNullOrWhiteSpace(_txtContent?.Text)
                    ? "Vista previa"
                    : _txtContent.Text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TextToolDialog.UpdatePreview] {ex.Message}");
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            TextContent = _txtContent.Text;
            SelectedFontFamily = _cmbFont.SelectedItem?.ToString() ?? "Segoe UI";
            SelectedFontSize = (float)_nudSize.Value;
            SelectedColor = _currentColor;
            SelectedFontStyle = FontStyle.Regular;
            if (_chkBold.Checked) SelectedFontStyle |= FontStyle.Bold;
            if (_chkItalic.Checked) SelectedFontStyle |= FontStyle.Italic;
            if (_chkUnderline.Checked) SelectedFontStyle |= FontStyle.Underline;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void AddLabel(string text, int x, int y) =>
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y + 4),
                AutoSize = true,
                ForeColor = TextSec,
                Font = new Font("Segoe UI", 9F)
            });

        private CheckBox MakeCheckBox(string text, int x, int y)
        {
            var chk = new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                ForeColor = TextPri,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            Controls.Add(chk);
            return chk;
        }
    }
}