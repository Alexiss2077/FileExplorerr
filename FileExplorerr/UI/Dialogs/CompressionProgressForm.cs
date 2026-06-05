using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  COMPRESSION PROGRESS FORM
    //  Non-modal progress window shown during compression and extraction.
    //  Displays an animated progress bar, the current file name, elapsed
    //  time and a Cancel button.
    //
    //  Mirrors the design of ExportProgressForm in UI/Dialogs/.
    //
    //  Usage:
    //      var cts  = new CancellationTokenSource();
    //      var form = new CompressionProgressForm("Comprimiendo", "ZIP", cts);
    //      form.Show(owner);
    //      // … launch async operation …
    //      form.SetStatus("Procesando archivo.txt", 42);
    //      form.Close();
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class CompressionProgressForm : Form
    {
        // ── Controls ──────────────────────────────────────────────────────
        private readonly Label _lblTitle;
        private readonly Label _lblCurrentFile;
        private readonly Label _lblElapsed;
        private readonly ProgressBar _bar;
        private readonly Button _btnCancel;

        // ── Timers ────────────────────────────────────────────────────────
        private readonly System.Windows.Forms.Timer _animTimer;
        private readonly System.Windows.Forms.Timer _elapsedTimer;

        // ── State ─────────────────────────────────────────────────────────
        private readonly CancellationTokenSource _cts;
        private int _targetPct;
        private int _elapsedSeconds;

        // ── Arctic Night palette ──────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(26, 26, 32);
        private static readonly Color TextMain = Color.FromArgb(220, 220, 236);
        private static readonly Color TextMuted = Color.FromArgb(90, 96, 128);
        private static readonly Color AccentVio = Color.FromArgb(124, 111, 247);
        private static readonly Color CancelBg = Color.FromArgb(80, 30, 30);
        private static readonly Color CancelFg = Color.FromArgb(220, 95, 85);
        private static readonly Color CancelBrd = Color.FromArgb(220, 95, 85);

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <param name="operationName">
        /// Short description shown in the title and header label.
        /// E.g. "Comprimiendo" or "Extrayendo".
        /// </param>
        /// <param name="formatName">
        /// Archive format abbreviation for the window title.
        /// E.g. "ZIP".
        /// </param>
        /// <param name="cts">
        /// CancellationTokenSource that is cancelled when the user clicks Cancel.
        /// </param>
        public CompressionProgressForm(
            string operationName,
            string formatName,
            CancellationTokenSource cts)
        {
            _cts = cts;

            // ── Form shell ────────────────────────────────────────────────
            Text = $"{operationName} {formatName}...";
            Size = new Size(460, 196);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;   // remove X button so user must use Cancel
            TopMost = true;
            BackColor = BgForm;

            // ── Title label ───────────────────────────────────────────────
            _lblTitle = new Label
            {
                Text = $"{operationName}...",
                Left = 16,
                Top = 14,
                Width = 420,
                Height = 20,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoEllipsis = true
            };

            // ── Current file label ────────────────────────────────────────
            _lblCurrentFile = new Label
            {
                Text = "Preparando...",
                Left = 16,
                Top = 38,
                Width = 420,
                Height = 18,
                ForeColor = AccentVio,
                Font = new Font("Segoe UI", 8.5F),
                AutoEllipsis = true
            };

            // ── Progress bar ──────────────────────────────────────────────
            _bar = new ProgressBar
            {
                Left = 16,
                Top = 64,
                Width = 420,
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            // ── Elapsed time label ────────────────────────────────────────
            _lblElapsed = new Label
            {
                Text = "0:00",
                Left = 16,
                Top = 92,
                Width = 200,
                Height = 18,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8F)
            };

            // ── Cancel button ─────────────────────────────────────────────
            _btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 174,
                Top = 116,
                Width = 110,
                Height = 34,
                BackColor = CancelBg,
                ForeColor = CancelFg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = CancelBrd;
            _btnCancel.Click += (_, _) =>
            {
                _btnCancel.Enabled = false;
                _btnCancel.Text = "Cancelando...";
                _cts.Cancel();
            };

            Controls.AddRange(new Control[]
                { _lblTitle, _lblCurrentFile, _bar, _lblElapsed, _btnCancel });

            // ── Animation timer: smoothly advances bar toward _targetPct ──
            _animTimer = new System.Windows.Forms.Timer { Interval = 40 };
            _animTimer.Tick += (_, _) =>
            {
                if (_bar.Value < _targetPct)
                    _bar.Value = Math.Min(_bar.Value + 2, _targetPct);
            };
            _animTimer.Start();

            // ── Elapsed time timer ────────────────────────────────────────
            _elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _elapsedTimer.Tick += (_, _) =>
            {
                _elapsedSeconds++;
                int m = _elapsedSeconds / 60;
                int s = _elapsedSeconds % 60;
                _lblElapsed.Text = $"{m}:{s:D2}  transcurridos";
            };
            _elapsedTimer.Start();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the current-file label and/or advances the target progress
        /// percentage.  Thread-safe: can be called from any thread.
        /// </summary>
        /// <param name="currentFile">
        /// Name of the file/entry currently being processed.
        /// Pass <c>null</c> to leave the label unchanged.
        /// </param>
        /// <param name="pct">
        /// Target progress 0–100.  Pass -1 to leave unchanged.
        /// </param>
        public void SetStatus(string? currentFile, int pct = -1)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(() => SetStatus(currentFile, pct));
                return;
            }

            if (currentFile is not null)
                _lblCurrentFile.Text = currentFile;

            if (pct >= 0)
                _targetPct = Math.Min(pct, 100);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DISPOSE
        // ════════════════════════════════════════════════════════════════════

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer?.Stop(); _animTimer?.Dispose();
                _elapsedTimer?.Stop(); _elapsedTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}