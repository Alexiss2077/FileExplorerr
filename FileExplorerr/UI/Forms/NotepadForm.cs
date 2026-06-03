using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public class NotepadForm : Form
    {
        private RichTextBox editor = null!;
        private Panel topBar = null!;
        private Panel bottomBar = null!;
        private Panel searchPanel = null!;
        private Panel lineNumberPanel = null!;
        private Label statusLabel = null!;
        private Label zoomLabel = null!;
        private TextBox searchBox = null!;
        private TextBox replaceBox = null!;

        private readonly string filePath;
        private bool hasChanges;
        private float fontSize = 11F;
        private Encoding fileEncoding = Encoding.UTF8;

        // Debounce para evitar recalcular estado en cada keystroke
        private System.Windows.Forms.Timer _statusDebounceTimer = null!;

        public NotepadForm(string path)
        {
            filePath = path;
            BuildUI();
            _ = LoadFileAsync();
        }

        private void BuildUI()
        {
            Text = $"Bloc de notas — {Path.GetFileName(filePath)}";
            Size = new Size(900, 640);
            MinimumSize = new Size(500, 350);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgBase;
            ForeColor = Theme.TextPrimary;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            _statusDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _statusDebounceTimer.Tick += (s, e) => { _statusDebounceTimer.Stop(); UpdateStatus(); };

            // ═══ TOP BAR ═════════════════════════════════════════════════════
            topBar = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = Theme.BgSurface };

            var btnSave = Theme.MakeButton("Guardar", 90, Theme.ButtonKind.Success);
            btnSave.Dock = DockStyle.Left;
            btnSave.Click += async (s, e) => await SaveAsync();

            var btnSaveAs = Theme.MakeButton("Guardar como...", 120);
            btnSaveAs.Dock = DockStyle.Left;
            btnSaveAs.Click += async (s, e) => await SaveAsAsync();

            var sep1 = new Panel { Width = 1, Dock = DockStyle.Left, BackColor = Theme.Border };

            var btnUndo = Theme.MakeButton("Deshacer", 80);
            btnUndo.Dock = DockStyle.Left;
            btnUndo.Click += (s, e) => { if (editor.CanUndo) editor.Undo(); };

            var btnRedo = Theme.MakeButton("Rehacer", 80);
            btnRedo.Dock = DockStyle.Left;
            btnRedo.Click += (s, e) => { if (editor.CanRedo) editor.Redo(); };

            var sep2 = new Panel { Width = 1, Dock = DockStyle.Left, BackColor = Theme.Border };

            var btnSearch = Theme.MakeButton("Buscar / Reemplazar", 150, Theme.ButtonKind.Primary);
            btnSearch.Dock = DockStyle.Left;
            btnSearch.Click += (s, e) => ToggleSearch();

            var btnWordWrap = Theme.MakeButton("Ajustar líneas", 110);
            btnWordWrap.Dock = DockStyle.Right;
            btnWordWrap.Click += (s, e) =>
            {
                editor.WordWrap = !editor.WordWrap;
                btnWordWrap.BackColor = editor.WordWrap ? Theme.AccentBg : Theme.BgElevated;
                btnWordWrap.ForeColor = editor.WordWrap ? Theme.Accent : Theme.TextPrimary;
                lineNumberPanel.Visible = !editor.WordWrap;
            };

            topBar.Controls.Add(btnWordWrap);
            topBar.Controls.Add(btnSearch);
            topBar.Controls.Add(sep2);
            topBar.Controls.Add(btnRedo);
            topBar.Controls.Add(btnUndo);
            topBar.Controls.Add(sep1);
            topBar.Controls.Add(btnSaveAs);
            topBar.Controls.Add(btnSave);

            // ═══ SEARCH PANEL ═══════════════════════════════════════════════
            searchPanel = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = Theme.BgElevated, Visible = false, Padding = new Padding(8, 5, 8, 5) };

            searchBox = Theme.MakeTextBox("Buscar...");
            searchBox.Width = 200; searchBox.Location = new Point(8, 7);
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) FindNext(); };

            replaceBox = Theme.MakeTextBox("Reemplazar con...");
            replaceBox.Width = 200; replaceBox.Location = new Point(218, 7);

            var btnFind = Theme.MakeButton("Siguiente", 80, Theme.ButtonKind.Primary);
            btnFind.Location = new Point(428, 5);
            btnFind.Click += (s, e) => FindNext();

            var btnReplaceOne = Theme.MakeButton("Reemplazar", 90);
            btnReplaceOne.Location = new Point(514, 5);
            btnReplaceOne.Click += (s, e) => ReplaceNext();

            var btnReplaceAll = Theme.MakeButton("Reemplazar todo", 120);
            btnReplaceAll.Location = new Point(610, 5);
            btnReplaceAll.Click += async (s, e) => await ReplaceAllAsync();

            var btnCloseSearch = Theme.MakeButton("✕", 32);
            btnCloseSearch.Dock = DockStyle.Right;
            btnCloseSearch.Click += (s, e) => { searchPanel.Visible = false; editor.Focus(); };

            searchPanel.Controls.AddRange(new Control[] { searchBox, replaceBox, btnFind, btnReplaceOne, btnReplaceAll, btnCloseSearch });

            // ═══ LINE NUMBERS ════════════════════════════════════════════════
            lineNumberPanel = new Panel { Width = 50, Dock = DockStyle.Left, BackColor = Theme.BgSurface };
            lineNumberPanel.Paint += LineNumbers_Paint;

            // ═══ EDITOR ══════════════════════════════════════════════════════
            editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgBase,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Cascadia Code", fontSize),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false
            };
            editor.TextChanged += Editor_TextChanged;
            editor.VScroll += (s, e) => lineNumberPanel.Invalidate();
            editor.Resize += (s, e) => lineNumberPanel.Invalidate();
            editor.SelectionChanged += (s, e) => ScheduleStatusUpdate();

            // ═══ BOTTOM BAR ══════════════════════════════════════════════════
            bottomBar = new Panel { Height = 28, Dock = DockStyle.Bottom, BackColor = Theme.BgSurface };

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            zoomLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 100,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
                Text = "100%"
            };

            var btnZoomIn = Theme.MakeButton("+", 28); btnZoomIn.Dock = DockStyle.Right; btnZoomIn.Height = 28; btnZoomIn.Click += (s, e) => Zoom(1);
            var btnZoomOut = Theme.MakeButton("−", 28); btnZoomOut.Dock = DockStyle.Right; btnZoomOut.Height = 28; btnZoomOut.Click += (s, e) => Zoom(-1);

            bottomBar.Controls.Add(statusLabel);
            bottomBar.Controls.Add(btnZoomIn);
            bottomBar.Controls.Add(btnZoomOut);
            bottomBar.Controls.Add(zoomLabel);

            Controls.Add(editor);
            Controls.Add(lineNumberPanel);
            Controls.Add(searchPanel);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            hasChanges = true;
            lineNumberPanel.Invalidate();
            UpdateTitle();
            ScheduleStatusUpdate();
        }

        private void ScheduleStatusUpdate()
        {
            _statusDebounceTimer.Stop();
            _statusDebounceTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARGA ASÍNCRONA
        // ════════════════════════════════════════════════════════════════════
        private async Task LoadFileAsync()
        {
            try
            {
                byte[] raw = await Task.Run(() => File.ReadAllBytes(filePath));

                if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
                    fileEncoding = Encoding.UTF8;
                else if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
                    fileEncoding = Encoding.Unicode;
                else if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
                    fileEncoding = Encoding.BigEndianUnicode;
                else
                    fileEncoding = Encoding.UTF8;

                string content = await Task.Run(() => fileEncoding.GetString(raw));

                editor.Text = content;
                hasChanges = false;
                UpdateTitle();
                UpdateStatus();
                editor.SelectionStart = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  NÚMEROS DE LÍNEA
        // ════════════════════════════════════════════════════════════════════
        private void LineNumbers_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.BgSurface);
            int firstCharIndex = editor.GetCharIndexFromPosition(new Point(0, 0));
            int firstLine = editor.GetLineFromCharIndex(firstCharIndex);
            int lastCharIndex = editor.GetCharIndexFromPosition(new Point(0, editor.ClientSize.Height));
            int lastLine = editor.GetLineFromCharIndex(lastCharIndex);

            using var brush = new SolidBrush(Theme.TextMuted);
            using var font = new Font("Cascadia Code", fontSize * 0.8f);
            using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            for (int i = firstLine; i <= lastLine; i++)
            {
                int charIdx = editor.GetFirstCharIndexFromLine(i);
                if (charIdx < 0) break;
                Point pos = editor.GetPositionFromCharIndex(charIdx);
                float lineHeight = editor.Font.GetHeight(e.Graphics);
                e.Graphics.DrawString((i + 1).ToString(), font, brush,
                    new System.Drawing.RectangleF(0, pos.Y, lineNumberPanel.Width - 6, lineHeight), sf);
            }

            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, lineNumberPanel.Width - 1, 0, lineNumberPanel.Width - 1, lineNumberPanel.Height);
        }

        // ════════════════════════════════════════════════════════════════════
        //  GUARDAR ASÍNCRONO
        // ════════════════════════════════════════════════════════════════════
        private async Task SaveAsync()
        {
            try
            {
                string content = editor.Text;
                await Task.Run(() => File.WriteAllText(filePath, content, fileEncoding));
                hasChanges = false;
                UpdateTitle();
                statusLabel.Text = "  Guardado.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveAsAsync()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Guardar como",
                Filter = "Texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = Path.GetFileName(filePath),
                InitialDirectory = Path.GetDirectoryName(filePath)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string content = editor.Text;
                await Task.Run(() => File.WriteAllText(dlg.FileName, content, fileEncoding));
                hasChanges = false;
                Text = $"Bloc de notas — {Path.GetFileName(dlg.FileName)}";
                statusLabel.Text = $"  Guardado como {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUSCAR / REEMPLAZAR
        // ════════════════════════════════════════════════════════════════════
        private void ToggleSearch()
        {
            searchPanel.Visible = !searchPanel.Visible;
            if (searchPanel.Visible)
            {
                searchBox.Focus();
                if (editor.SelectionLength > 0) searchBox.Text = editor.SelectedText;
            }
        }

        private void FindNext()
        {
            string query = searchBox.Text;
            if (string.IsNullOrEmpty(query)) return;
            int startAt = editor.SelectionStart + editor.SelectionLength;
            if (startAt >= editor.Text.Length) startAt = 0;
            int index = editor.Text.IndexOf(query, startAt, StringComparison.OrdinalIgnoreCase);
            if (index < 0) index = editor.Text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            if (index < 0) { MessageBox.Show($"No se encontró \"{query}\".", "Buscar", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            editor.SelectionStart = index;
            editor.SelectionLength = query.Length;
            editor.SelectionBackColor = Color.FromArgb(80, 140, 60);
            editor.SelectionColor = Color.White;
            editor.ScrollToCaret();
            editor.Focus();
        }

        private void ReplaceNext()
        {
            string query = searchBox.Text;
            string replacement = replaceBox.Text;
            if (string.IsNullOrEmpty(query)) return;
            if (editor.SelectedText.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                editor.SelectedText = replacement;
                hasChanges = true;
            }
            FindNext();
        }

        /// <summary>
        /// Reemplaza todas las ocurrencias en un hilo de fondo para no bloquear la UI en archivos grandes.
        /// </summary>
        private async Task ReplaceAllAsync()
        {
            string query = searchBox.Text;
            string replacement = replaceBox.Text;
            if (string.IsNullOrEmpty(query)) return;

            statusLabel.Text = "  Reemplazando...";
            string originalText = editor.Text;

            var (newText, count) = await Task.Run(() =>
            {
                int c = 0;
                int pos = 0;
                string text = originalText;
                var sb = new StringBuilder();
                while (pos < text.Length)
                {
                    int idx = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) { sb.Append(text, pos, text.Length - pos); break; }
                    sb.Append(text, pos, idx - pos);
                    sb.Append(replacement);
                    pos = idx + query.Length;
                    c++;
                }
                return (sb.ToString(), c);
            });

            if (count > 0)
            {
                editor.Text = newText;
                hasChanges = true;
                MessageBox.Show($"Se reemplazaron {count} ocurrencia(s).", "Reemplazar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"No se encontró \"{query}\".", "Reemplazar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateStatus();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ZOOM
        // ════════════════════════════════════════════════════════════════════
        private void Zoom(int direction)
        {
            float newSize = fontSize + direction;
            if (newSize < 6 || newSize > 40) return;
            fontSize = newSize;
            editor.Font = new Font("Cascadia Code", fontSize);
            int pct = (int)(fontSize / 11f * 100);
            zoomLabel.Text = $"{pct}%";
            lineNumberPanel.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ESTADO — conteo de palabras liviano
        // ════════════════════════════════════════════════════════════════════
        private void UpdateStatus()
        {
            // Trabajar sobre una copia del texto para no bloquear
            string text = editor.Text;
            int lines = editor.Lines.Length;
            int chars = text.Length;

            // Conteo de palabras sencillo sin foreach char a char
            int words = 0;
            bool inWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i])) { if (!inWord) { words++; inWord = true; } }
                else inWord = false;
            }

            int currentLine = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
            int firstChar = editor.GetFirstCharIndexOfCurrentLine();
            int currentCol = editor.SelectionStart - firstChar + 1;

            statusLabel.Text = $"  Lín {currentLine}, Col {currentCol}   ·   " +
                               $"{lines} líneas   ·   {words} palabras   ·   {chars} caracteres   ·   " +
                               $"{fileEncoding.WebName.ToUpper()}";
        }

        private void UpdateTitle()
        {
            string marker = hasChanges ? " ●" : "";
            Text = $"Bloc de notas — {Path.GetFileName(filePath)}{marker}";
        }

        // ════════════════════════════════════════════════════════════════════
        //  TECLADO
        // ════════════════════════════════════════════════════════════════════
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.S:
                        if (e.Shift) _ = SaveAsAsync(); else _ = SaveAsync();
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                    case Keys.F:
                        ToggleSearch();
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                    case Keys.H:
                        ToggleSearch();
                        if (searchPanel.Visible) replaceBox.Focus();
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                    case Keys.G:
                        GoToLine();
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                    case Keys.Add or Keys.Oemplus:
                        Zoom(1);
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                    case Keys.Subtract or Keys.OemMinus:
                        Zoom(-1);
                        e.Handled = e.SuppressKeyPress = true;
                        break;
                }
            }
            if (e.KeyCode == Keys.F3) { FindNext(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape && searchPanel.Visible) { searchPanel.Visible = false; editor.Focus(); e.Handled = true; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  IR A LÍNEA
        // ════════════════════════════════════════════════════════════════════
        private void GoToLine()
        {
            using var dlg = new Form { Text = "Ir a línea", Width = 300, Height = 130, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Theme.BgSurface, ForeColor = Theme.TextPrimary };
            var lbl = new Label { Text = $"Número de línea (1 — {editor.Lines.Length}):", Left = 12, Top = 12, Width = 270, ForeColor = Theme.TextSecondary, Font = Theme.FontBody };
            var txt = Theme.MakeTextBox(); txt.Left = 12; txt.Top = 38; txt.Width = 270;
            var ok = Theme.MakeButton("Ir", 80, Theme.ButtonKind.Primary); ok.Left = 200; ok.Top = 74; ok.DialogResult = DialogResult.OK;
            dlg.Controls.AddRange(new Control[] { lbl, txt, ok });
            dlg.AcceptButton = (IButtonControl)ok;
            if (dlg.ShowDialog(this) == DialogResult.OK && int.TryParse(txt.Text.Trim(), out int line) && line >= 1 && line <= editor.Lines.Length)
            {
                int idx = editor.GetFirstCharIndexFromLine(line - 1);
                editor.SelectionStart = idx; editor.SelectionLength = 0;
                editor.ScrollToCaret(); editor.Focus();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CERRAR CON CAMBIOS SIN GUARDAR
        // ════════════════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (hasChanges)
            {
                var result = MessageBox.Show("Hay cambios sin guardar. ¿Deseas guardar antes de cerrar?", "Cambios sin guardar", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                    _ = SaveAsync();
                else if (result == DialogResult.Cancel)
                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _statusDebounceTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}