using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public sealed class AccountButton : Button
    {
        private UserProfile? _profile;
        private Bitmap? _avatar;
        private bool _avatarLoaded;
        private AccountPanel? _openPanel;

        private static readonly Color BgHover = Color.FromArgb(42, 46, 68);
        private static readonly Color BgNormal = Color.FromArgb(28, 32, 48);
        private static readonly Color Accent = Color.FromArgb(124, 111, 247);
        private static readonly Color AccentDim = Color.FromArgb(36, 27, 82);
        private static readonly Color BorderNorm = Color.FromArgb(60, 124, 111, 247);   // acento tenue en reposo
        private static readonly Color BorderHov = Color.FromArgb(150, 124, 111, 247);  // acento visible al hover
        private static readonly Color Teal = Color.FromArgb(52, 211, 153);
        private static readonly Color EmailColor = Color.FromArgb(200, 52, 211, 153);   // teal sólido para el correo
        private static readonly Color TextPri = Color.FromArgb(240, 242, 255);
        private static readonly Color TextSec = Color.FromArgb(157, 163, 191);
        private static readonly Color TextMuted = Color.FromArgb(90, 96, 128);
        private static readonly Color GoogleRed = Color.FromArgb(234, 67, 53);
        private static readonly Color GhColor = Color.FromArgb(110, 120, 140);

        public event EventHandler? SignOutRequested;
        public event EventHandler? SwitchAccountRequested;

        public AccountButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = BgHover;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            Size = new Size(310, 62);

            Text = string.Empty;
            Click += OnClick;
        }

        public void SetProfile(UserProfile? profile)
        {
            _profile = profile;
            _avatar = null;
            _avatarLoaded = false;
            if (profile != null && !profile.IsGuest)
                _ = LoadAvatarAsync(profile);
            Invalidate();
        }

        private async Task LoadAvatarAsync(UserProfile profile)
        {
            _avatar = await SessionManager.LoadAvatarAsync(profile);
            _avatarLoaded = _avatar != null;
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(Invalidate);
        }

        private void OnClick(object? sender, EventArgs e)
        {
            if (_openPanel != null && !_openPanel.IsDisposed)
            {
                _openPanel.Close();
                _openPanel = null;
                return;
            }

            if (_profile == null) return;

            var btnBottomRight = PointToScreen(new Point(Width, Height));
            int panelW = 300;
            int panelH = 380;

            int x = btnBottomRight.X - panelW;
            int y = btnBottomRight.Y + 4;

            var screen = Screen.FromControl(this).WorkingArea;
            if (x < screen.Left) x = screen.Left + 4;
            if (x + panelW > screen.Right) x = screen.Right - panelW - 4;
            if (y + panelH > screen.Bottom) y = PointToScreen(new Point(0, 0)).Y - panelH - 4;

            _openPanel = new AccountPanel(_profile, new Point(x, y));
            _openPanel.SignOutRequested += (s, ev) => SignOutRequested?.Invoke(this, ev);
            _openPanel.SwitchAccountRequested += (s, ev) => SwitchAccountRequested?.Invoke(this, ev);
            _openPanel.FormClosed += (s, ev) => _openPanel = null;

            _openPanel.Show(FindForm());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var bg = Parent?.BackColor ?? Color.FromArgb(20, 23, 32);
            g.Clear(bg);

            if (_profile == null)
            {
                DrawSignInPrompt(g);
                return;
            }

            bool hover = ClientRectangle.Contains(PointToClient(MousePosition));

            // ── Fondo redondeado con borde ────────────────────────────────────
            var btnRect = new Rectangle(1, 1, Width - 2, Height - 2);
            using var btnPath = RoundedRect(btnRect, 8);

            // Fondo: más oscuro en reposo, hover más claro
            using var bgBrush = new SolidBrush(hover ? BgHover : BgNormal);
            g.FillPath(bgBrush, btnPath);

            // Borde: acento violeta tenue (se intensifica al hover)
            using var borderPen = new Pen(hover ? BorderHov : BorderNorm, 1.2f);
            g.DrawPath(borderPen, btnPath);

            // ── Avatar ───────────────────────────────────────────────────────
            int sz = 42;
            int ax = 10;
            int ay = (Height - sz) / 2;
            var aRect = new Rectangle(ax, ay, sz, sz);

            if (_avatar != null && _avatarLoaded)
            {
                using var cp = new GraphicsPath();
                cp.AddEllipse(aRect);
                g.SetClip(cp);
                g.DrawImage(_avatar, aRect);
                g.ResetClip();
            }
            else
            {
                using var gradBr = new LinearGradientBrush(aRect, AccentDim,
                    Color.FromArgb(50, Teal), 135F);
                using var cp = new GraphicsPath();
                cp.AddEllipse(aRect);
                g.FillPath(gradBr, cp);

                using var iBr = new SolidBrush(TextPri);
                // CAMBIO 3: inicial más grande en el avatar
                using var iF = new Font("Segoe UI", 14F, FontStyle.Bold);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(_profile.Initials, iF, iBr,
                    new RectangleF(ax, ay, sz, sz), sf);
            }

            // Borde de proveedor
            using var pPen = new Pen(GetProviderColor(_profile.Provider), 2f);
            g.DrawEllipse(pPen, ax + 1, ay + 1, sz - 3, sz - 3);

            // Punto verde de estado
            if (!_profile.IsGuest)
            {
                using var dBg = new SolidBrush(bg);
                using var dBr = new SolidBrush(Teal);
                g.FillEllipse(dBg, ax + sz - 10, ay + sz - 10, 11, 11);
                g.FillEllipse(dBr, ax + sz - 9, ay + sz - 9, 9, 9);
            }

            // ── Texto ────────────────────────────────────────────────────────
            int tx = ax + sz + 10;
            int tw = Width - tx - 18;

            if (tw > 20)
            {
                string l1 = _profile.IsGuest ? "Invitado" : _profile.DisplayName;
                string l2 = _profile.IsGuest ? "Sin sesión"
                          : !string.IsNullOrEmpty(_profile.Email)
                              ? _profile.Email
                              : _profile.Provider;

                using var nBr = new SolidBrush(TextPri);
                using var sBr = new SolidBrush(EmailColor);
                using var nF = new Font("Segoe UI", 12F, FontStyle.Bold);
                using var sF = new Font("Segoe UI", 9.5F);

                // Centrar verticalmente el bloque nombre+correo
                int blockH = 19 + 5 + 15;
                int startY = (Height - blockH) / 2;

                g.DrawString(Trunc(g, l1, nF, tw), nF, nBr, tx, startY);
                g.DrawString(Trunc(g, l2, sF, tw), sF, sBr, tx, startY + 22);
            }

            // Chevron
            using var cBr = new SolidBrush(TextMuted);
            using var cF = new Font("Segoe UI", 8F);
            g.DrawString("▾", cF, cBr, Width - 16, (Height - 14) / 2);
        }

        private void DrawSignInPrompt(Graphics g)
        {
            using var br = new SolidBrush(TextSec);
            using var f = new Font("Segoe UI", 9F);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("🔐  Iniciar sesión", f, br,
                new RectangleF(0, 0, Width, Height), sf);
        }

        private static Color GetProviderColor(string p) => p switch
        {
            "Google" => GoogleRed,
            "GitHub" => GhColor,
            _ => Color.FromArgb(90, 96, 128)
        };

        private static string Trunc(Graphics g, string t, Font f, float max)
        {
            if (g.MeasureString(t, f).Width <= max) return t;
            while (t.Length > 1 && g.MeasureString(t + "…", f).Width > max)
                t = t[..^1];
            return t + "…";
        }

        private static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _avatar?.Dispose();
            base.Dispose(disposing);
        }
    }
}