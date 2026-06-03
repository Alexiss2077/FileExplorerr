using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE OPENER
    //  Routes a file or directory path to the correct viewer or action.
    //
    //  Phase 5A: extracted from Form1.cs.
    //  Original method removed from Form1: private void OpenEntry(string path).
    //
    //  Directory navigation is delegated back to the Form1 caller via the
    //  NavigateAction callback, preserving the original behaviour without
    //  creating a circular dependency.
    //
    //  All viewer windows are opened with Show() (non-modal), identical to
    //  the original implementation.
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileOpener
    {
        // ── Supported video extensions (identical to Form1.OpenEntry) ─────
        private static readonly string[] VideoExtensions =
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
            ".m4v", ".ts", ".3gp", ".mpg", ".mpeg", ".vob", ".divx"
        };

        // ── Callback type ─────────────────────────────────────────────────

        /// <summary>
        /// Invoked when the opened path is a directory.
        /// Form1 passes its <c>NavigateToPath</c> method here.
        /// </summary>
        public delegate void NavigateAction(string directoryPath);

        // ════════════════════════════════════════════════════════════════════
        //  OPEN
        //  Direct translation of Form1.OpenEntry(string path).
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens <paramref name="path"/> in the appropriate viewer or application.
        /// <para>
        /// Call-sites in Form1: replace
        ///   <c>OpenEntry(path)</c>
        /// with
        ///   <c>FileOpener.Open(path, this, NavigateToPath)</c>
        /// </para>
        /// </summary>
        public static void Open(string path, IWin32Window? owner, NavigateAction? navigate)
        {
            // Directory: delegate navigation to Form1.
            if (Directory.Exists(path))
            {
                navigate?.Invoke(path);
                return;
            }

            if (!File.Exists(path)) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                // .txt has its own mini-dialog (table viewer vs notepad).
                if (ext == ".txt")
                {
                    OpenTextFile(path, owner);
                    return;
                }

                // Structured data files.
                if (ext is ".csv" or ".json" or ".xml" or ".log")
                {
                    new FileViewerForm(path).Show();
                    return;
                }

                // Image files.
                if (ImageViewerForm.SupportedExtensions.Contains(ext))
                {
                    new ImageViewerForm(path).Show();
                    return;
                }

                // Audio files.
                if (MusicPlayerForm.SupportedExtensions.Contains(ext))
                {
                    new MusicPlayerForm(path).Show();
                    return;
                }

                // Video files.
                if (VideoExtensions.Contains(ext))
                {
                    new VideoPlayerForm(path).Show();
                    return;
                }

                // Anything else: hand off to the OS default application.
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE — .txt choice dialog
        //  Identical to the inline Form creation from Form1.OpenEntry.
        // ════════════════════════════════════════════════════════════════════

        private static void OpenTextFile(string path, IWin32Window? owner)
        {
            using var dlg = new Form
            {
                Text = "Abrir como...",
                Width = 360,
                Height = 170,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.BgSurface,
                ForeColor = Theme.TextPrimary
            };

            var lbl = new Label
            {
                Text = $"\u00BFC\u00F3mo deseas abrir \"{Path.GetFileName(path)}\"?",
                Left = 14,
                Top = 20,
                Width = 320,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontBody
            };

            var btnTable = Theme.MakeButton("Visor de tabla", 140);
            btnTable.Left = 14;
            btnTable.Top = 70;
            btnTable.Click += (_, _) => { dlg.Tag = "table"; dlg.DialogResult = DialogResult.OK; };

            var btnNote = Theme.MakeButton("Bloc de notas", 140, Theme.ButtonKind.Primary);
            btnNote.Left = 164;
            btnNote.Top = 70;
            btnNote.Click += (_, _) => { dlg.Tag = "notepad"; dlg.DialogResult = DialogResult.OK; };

            dlg.Controls.AddRange(new Control[] { lbl, btnTable, btnNote });

            if (dlg.ShowDialog(owner) == DialogResult.OK)
            {
                if (dlg.Tag?.ToString() == "notepad")
                    new NotepadForm(path).Show();
                else
                    new FileViewerForm(path).Show();
            }
        }
    }
}