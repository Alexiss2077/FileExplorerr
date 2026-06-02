using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  EXPORT PROGRESS FORM
    //  Non-modal progress window shown during Office/PDF export.
    //  Displays an animated progress bar and a Cancel button.
    //  Previously defined at the end of ExportadorOffice.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class ExportProgressForm : Form
    {
        // ── Controls ──────────────────────────────────────────────────────
        private readonly Label _lblTitulo;
        private readonly Label _lblStatus;
        private readonly ProgressBar _bar;
        private readonly Button _btnCancel;
        private readonly System.Windows.Forms.Timer _timer;

        // ── State ─────────────────────────────────────────────────────────
        private readonly CancellationTokenSource _cts;
        private int _animPct;

        // ── Colours ───────────────────────────────────────────────────────
        private static readonly Color BgForm = Color.FromArgb(26, 26, 32);
        private static readonly Color TextMain = Color.FromArgb(220, 220, 236);
        private static readonly Color AccentTeal = Color.FromArgb(72, 202, 188);
        private static readonly Color CancelBg = Color.FromArgb(80, 30, 30);
        private static readonly Color CancelFg = Color.FromArgb(220, 95, 85);
        private static readonly Color CancelBrd = Color.FromArgb(220, 95, 85);

        public ExportProgressForm(
            string titulo,
            string fmt,
            CancellationTokenSource cts)
        {
            _cts = cts;

            Text = $"Exportando {fmt}...";
            Size = new Size(440, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            TopMost = true;
            BackColor = Color.FromArgb(26, 26, 32);

            _lblTitulo = new Label
            {
                Text = $"Exportando: {titulo}",
                Left = 16,
                Top = 14,
                Width = 400,
                Height = 20,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoEllipsis = true
            };

            _lblStatus = new Label
            {
                Text = "Iniciando...",
                Left = 16,
                Top = 38,
                Width = 400,
                Height = 18,
                ForeColor = AccentTeal,
                Font = new Font("Segoe UI", 8.5F)
            };

            _bar = new ProgressBar
            {
                Left = 16,
                Top = 62,
                Width = 400,
                Height = 22,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            _btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 160,
                Top = 96,
                Width = 110,
                Height = 32,
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
                { _lblTitulo, _lblStatus, _bar, _btnCancel });

            // Smooth progress animation: the bar catches up to _animPct gradually.
            _timer = new System.Windows.Forms.Timer { Interval = 40 };
            _timer.Tick += (_, _) =>
            {
                if (_bar.Value < _animPct)
                    _bar.Value = Math.Min(_bar.Value + 2, _animPct);
            };
            _timer.Start();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Updates the status label and/or advances the target progress percentage.
        /// Safe to call from any thread via Invoke.
        /// </summary>
        /// <param name="msg">Status text, or <c>null</c> to leave unchanged.</param>
        /// <param name="pct">Target progress (0–100), or -1 to leave unchanged.</param>
        public void SetStatus(string? msg, int pct = -1)
        {
            if (msg is not null) _lblStatus.Text = msg;
            if (pct >= 0) _animPct = Math.Min(pct, 100);
        }

        // ── Dispose ───────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer?.Dispose();
            base.Dispose(disposing);
        }
    }
}