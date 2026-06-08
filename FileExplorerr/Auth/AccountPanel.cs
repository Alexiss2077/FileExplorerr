using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  ACCOUNT PANEL
    //  Panel flotante de cuenta de usuario, estilo Visual Studio.
    //  Se abre desde el botón de cuenta en la barra de navegación superior.
    //
    //  Muestra:
    //    - Avatar del usuario (descargado o inicial de fallback)
    //    - Nombre, email, proveedor
    //    - Botón de cerrar sesión
    //    - Botón de cambiar cuenta
    //    - Estadísticas rápidas de la sesión
    // ════════════════════════════════════════════════════════════════════════
    public sealed class AccountPanel : Form
    {
        // ── Eventos ───────────────────────────────────────────────────────
        public event EventHandler? SignOutRequested;
        public event EventHandler? SwitchAccountRequested;

        // ── Estado ────────────────────────────────────────────────────────
        private readonly UserProfile _profile;
        private Bitmap? _avatar;
        private bool _avatarLoaded;

        // ── Controles ─────────────────────────────────────────────────────
        private PictureBox _avatarBox = null!;
        private Label _nameLabel = null!;
        private Label _emailLabel = null!;
        private Label _providerBadge = null!;
        private Panel _avatarPanel = null!;

        // ── Paleta Arctic Night ───────────────────────────────────────────
        private static readonly Color BgPanel = Color.FromArgb(24, 27, 38);
        private static readonly Color BgCard = Color.FromArgb(30, 34, 50);
        private static readonly Color BgElevated = Color.FromArgb(36, 40, 58);
        private static readonly Color BgHover = Color.FromArgb(42, 46, 68);
        private static readonly Color Accent = Color.FromArgb(124, 111, 247);
        private static readonly Color AccentDim = Color.FromArgb(36, 27, 82);
        private static readonly Color Teal = Color.FromArgb(52, 211, 153);
        private static readonly Color TealDim = Color.FromArgb(10, 48, 36);
        private static readonly Color Coral = Color.FromArgb(248, 113, 113);
        private static readonly Color CoralDim = Color.FromArgb(80, 30, 30);
        private static readonly Color TextPri = Color.FromArgb(240, 242, 255);
        private static readonly Color TextSec = Color.FromArgb(157, 163, 191);
        private static readonly Color TextMuted = Color.FromArgb(90, 96, 128);
        private static readonly Color Border = Color.FromArgb(42, 46, 68);
        private static readonly Color GoogleRed = Color.FromArgb(234, 67, 53);
        private static readonly Color GhColor = Color.FromArgb(110, 120, 140);

        // ════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════
        /// <param name="position">Coordenadas de pantalla donde aparecerá la esquina superior izquierda del panel.</param>
        public AccountPanel(UserProfile profile, Point position)
        {
            _profile = profile;
            BuildUI(position);
            _ = LoadAvatarAsync();
        }

        // ════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN
        // ════════════════════════════════════════════════════════════════
        private void BuildUI(Point position)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(300, 400); // <-- CAMBIO: Más alto para que quepa la letra grande (era 380)
            BackColor = BgPanel;
            ForeColor = TextPri;
            Font = new Font("Segoe UI", 10F); // <-- CAMBIO: Base de fuente más grande

            // La posición ya viene calculada desde AccountButton
            Location = position;

            // Cerrar al perder el foco
            Deactivate += (s, e) => Close();

            Paint += OnPanelPaint;

            BuildContent();
        }

        private void OnPanelPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fondo con borde redondeado
            using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10);
            using var bgBr = new SolidBrush(BgPanel);
            g.FillPath(bgBr, path);
            using var borderPen = new Pen(Color.FromArgb(60, Accent), 1.5f);
            g.DrawPath(borderPen, path);

            // Barra superior con gradiente
            using var topPath = RoundedRectTop(new Rectangle(0, 0, Width - 1, 110), 10); // Ajustado a 110
            using var topBr = new LinearGradientBrush(
                new Point(0, 0), new Point(0, 110),
                Color.FromArgb(40, Accent), Color.Transparent);
            g.FillPath(topBr, topPath);
        }

        private void BuildContent()
        {
            // ── Avatar ─────────────────────────────────────────────────
            _avatarPanel = new Panel
            {
                Left = 22,
                Top = 24, // Ajustado
                Size = new Size(64, 64),
                BackColor = Color.Transparent
            };
            _avatarPanel.Paint += AvatarPanel_Paint;

            // ── Info de usuario ────────────────────────────────────────
            _nameLabel = new Label
            {
                Text = _profile.DisplayName,
                Left = 98,
                Top = 26,
                Width = 188,
                Height = 26, // <-- CAMBIO: Más alto
                Font = new Font("Segoe UI", 12.5F, FontStyle.Bold), // <-- CAMBIO: Letra mucho más grande (era 10.5F)
                ForeColor = TextPri,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            _emailLabel = new Label
            {
                Text = _profile.IsGuest ? "No autenticado" : _profile.Email,
                Left = 98,
                Top = 52,
                Width = 188,
                Height = 20, // <-- CAMBIO: Más alto
                Font = new Font("Segoe UI", 10F), // <-- CAMBIO: Letra más grande (era 8.5F)
                ForeColor = TextSec,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            _providerBadge = new Label
            {
                Text = GetProviderLabel(),
                Left = 98,
                Top = 75,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), // <-- CAMBIO: Letra más grande (era 7.5F)
                ForeColor = GetProviderColor(),
                BackColor = Color.FromArgb(30, GetProviderColor()),
                Padding = new Padding(6, 2, 6, 2)
            };

            // ── Divisor ────────────────────────────────────────────────
            var divider1 = new Panel
            {
                Left = 0,
                Top = 114, // <-- CAMBIO: Bajó un poco por las letras grandes
                Width = 300,
                Height = 1,
                BackColor = Border
            };

            // ── Acciones de cuenta ─────────────────────────────────────
            int y = 124; // <-- CAMBIO: Bajó un poco

            var btnManage = MakeMenuItem("⚙  Configuración de cuenta", y, TextPri);
            btnManage.Click += (s, e) =>
            {
                Close();
                if (!_profile.IsGuest && !string.IsNullOrEmpty(_profile.AvatarUrl))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _profile.Provider == "Google"
                            ? "https://myaccount.google.com"
                            : "https://github.com/settings/profile",
                        UseShellExecute = true
                    });
                }
            };
            y += 44; // <-- CAMBIO: Espaciado más grande (era 40)

            var btnSwitch = MakeMenuItem("🔄  Cambiar cuenta", y, Accent);
            btnSwitch.Click += (s, e) =>
            {
                Close();
                SwitchAccountRequested?.Invoke(this, EventArgs.Empty);
            };
            y += 44;

            var divider2 = new Panel
            {
                Left = 0,
                Top = y,
                Width = 300,
                Height = 1,
                BackColor = Border
            };
            y += 10;

            // ── Info de sesión ─────────────────────────────────────────
            var sessionSection = BuildSessionInfo(y);
            y += 100; // <-- CAMBIO: Sección de sesión más alta (era 90)

            var divider3 = new Panel
            {
                Left = 0,
                Top = y,
                Width = 300,
                Height = 1,
                BackColor = Border
            };
            y += 10;

            // ── Cerrar sesión ──────────────────────────────────────────
            var btnSignOut = MakeMenuItem(
                _profile.IsGuest ? "🔐  Iniciar sesión" : "🚪  Cerrar sesión",
                y,
                _profile.IsGuest ? Teal : Coral);
            btnSignOut.Click += (s, e) =>
            {
                Close();
                SignOutRequested?.Invoke(this, EventArgs.Empty);
            };

            Controls.AddRange(new Control[]
            {
                _avatarPanel,
                _nameLabel, _emailLabel, _providerBadge,
                divider1,
                btnManage, btnSwitch,
                divider2, sessionSection, divider3,
                btnSignOut
            });
        }

        // ── Ítem de menú ──────────────────────────────────────────────
        private static Button MakeMenuItem(string text, int top, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Left = 0,
                Top = top,
                Width = 300,
                Height = 42, // <-- CAMBIO: Botones más altos (era 38)
                BackColor = Color.Transparent,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5F), // <-- CAMBIO: Letra del menú más grande (era 9F)
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, fg);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, fg);
            return btn;
        }

        // ── Info de sesión ─────────────────────────────────────────────
        private Panel BuildSessionInfo(int top)
        {
            var panel = new Panel
            {
                Left = 0,
                Top = top,
                Width = 300,
                Height = 96, // <-- CAMBIO: Panel más alto (era 86)
                BackColor = Color.Transparent,
                Padding = new Padding(18, 8, 18, 8)
            };

            var lblSessionTitle = new Label
            {
                Text = "Esta sesión",
                Left = 18,
                Top = 8,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), // <-- CAMBIO: Letra más grande (era 7.5F)
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            };

            // Estadísticas
            var stats = new[]
            {
                ("Proveedor",  _profile.IsGuest ? "—" : _profile.Provider),
                ("Estado",     _profile.IsGuest ? "No autenticado" : "Autenticado ✓"),
            };

            int sy = 30; // <-- CAMBIO: Ajuste vertical
            panel.Controls.Add(lblSessionTitle);

            foreach (var (key, val) in stats)
            {
                panel.Controls.Add(new Label
                {
                    Text = key + ":",
                    Left = 18,
                    Top = sy,
                    Width = 90,
                    Height = 22, // <-- CAMBIO: Más alto
                    Font = new Font("Segoe UI", 9.5F), // <-- CAMBIO: Letra más grande (era 8F)
                    ForeColor = TextMuted,
                    BackColor = Color.Transparent
                });
                panel.Controls.Add(new Label
                {
                    Text = val,
                    Left = 110,
                    Top = sy,
                    Width = 170,
                    Height = 22, // <-- CAMBIO: Más alto
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), // <-- CAMBIO: Letra más grande (era 8F)
                    ForeColor = val.Contains("✓") ? Teal : TextSec,
                    BackColor = Color.Transparent
                });
                sy += 26; // <-- CAMBIO: Renglones más separados (era 22)
            }

            return panel;
        }

        // ════════════════════════════════════════════════════════════════
        //  AVATAR
        // ════════════════════════════════════════════════════════════════
        private void AvatarPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = 64;
            var rect = new Rectangle(0, 0, size, size);

            if (_avatar != null && _avatarLoaded)
            {
                // Avatar circular
                using var path = new GraphicsPath();
                path.AddEllipse(rect);
                g.SetClip(path);
                g.DrawImage(_avatar, rect);
                g.ResetClip();

                // Borde del proveedor
                using var borderPen = new Pen(GetProviderColor(), 2.5f);
                g.DrawEllipse(borderPen, 1, 1, size - 3, size - 3);
            }
            else
            {
                // Avatar de fallback con iniciales
                using var bgBr = new LinearGradientBrush(
                    new Point(0, 0), new Point(size, size),
                    AccentDim, Color.FromArgb(60, Teal));
                using var path = new GraphicsPath();
                path.AddEllipse(rect);
                g.FillPath(bgBr, path);

                using var borderPen = new Pen(Color.FromArgb(80, Accent), 2f);
                g.DrawEllipse(borderPen, 1, 1, size - 3, size - 3);

                using var textBr = new SolidBrush(TextPri);
                using var font = new Font("Segoe UI", 24F, FontStyle.Bold); // <-- CAMBIO: Inicial más grande (era 20F)
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(_profile.Initials, font, textBr,
                    new RectangleF(0, 0, size, size), sf);
            }

            // Indicador de estado (punto verde)
            if (!_profile.IsGuest)
            {
                using var dotBr = new SolidBrush(Teal);
                using var dotBg = new SolidBrush(BgPanel);
                g.FillEllipse(dotBg, size - 16, size - 16, 15, 15);
                g.FillEllipse(dotBr, size - 14, size - 14, 11, 11);
            }
        }

        private async Task LoadAvatarAsync()
        {
            if (_profile.IsGuest) return;
            _avatar = await SessionManager.LoadAvatarAsync(_profile);
            _avatarLoaded = _avatar != null;
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => _avatarPanel?.Invalidate());
        }

        // ════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════
        private string GetProviderLabel() => _profile.Provider switch
        {
            "Google" => "  Google",
            "GitHub" => "  GitHub",
            "Guest" => "  Sin cuenta",
            _ => $"  {_profile.Provider}"
        };

        private Color GetProviderColor() => _profile.Provider switch
        {
            "Google" => GoogleRed,
            "GitHub" => GhColor,
            _ => TextMuted
        };

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundedRectTop(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Y + radius, r.Right, r.Bottom);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.AddLine(r.X, r.Bottom, r.X, r.Y + radius);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _avatar?.Dispose();
            base.Dispose(disposing);
        }
    }
}