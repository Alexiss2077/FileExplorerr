using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  INPUT DIALOG
    //  Simple single-line text-input dialog.
    //  Previously implemented as a private method on Form1.
    // ════════════════════════════════════════════════════════════════════════
    internal static class InputDialog
    {
        /// <summary>
        /// Shows a modal dialog with a single text field.
        /// Returns the trimmed text the user entered, or <c>null</c> if cancelled.
        /// </summary>
        /// <param name="owner">Parent window (used for centering).</param>
        /// <param name="title">Dialog window title.</param>
        /// <param name="prompt">Label text shown above the input field.</param>
        /// <param name="defaultValue">Pre-filled value in the text field.</param>
        public static string? Show(
            IWin32Window? owner,
            string title,
            string prompt,
            string defaultValue = "")
        {
            using var dlg = new Form
            {
                Text = title,
                Width = 440,
                Height = 170,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary
            };

            var lblPrompt = new Label
            {
                Text = prompt,
                Left = 14,
                Top = 20,
                Width = 400,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontBody
            };

            var txtInput = Theme.MakeTextBox();
            txtInput.Text = defaultValue;
            txtInput.Left = 14;
            txtInput.Top = 48;
            txtInput.Width = 400;
            txtInput.SelectAll();

            var btnOk = Theme.MakeButton("Aceptar", 90, Theme.ButtonKind.Primary);
            btnOk.Left = 218;
            btnOk.Top = 92;
            btnOk.DialogResult = DialogResult.OK;

            var btnCancel = Theme.MakeButton("Cancelar", 90);
            btnCancel.Left = 318;
            btnCancel.Top = 92;
            btnCancel.DialogResult = DialogResult.Cancel;

            dlg.Controls.AddRange(new Control[] { lblPrompt, txtInput, btnOk, btnCancel });
            dlg.AcceptButton = (IButtonControl)btnOk;
            dlg.CancelButton = (IButtonControl)btnCancel;

            return dlg.ShowDialog(owner) == DialogResult.OK
                ? txtInput.Text.Trim()
                : null;
        }
    }
}