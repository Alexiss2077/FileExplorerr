using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public sealed class LoginForm : Form
    {
        public UserProfile? LoggedInUser { get; private set; }

        private static string GoogleClientId => OAuthConfig.GoogleClientId;
        private static string GoogleClientSecret => OAuthConfig.GoogleClientSecret;
        private static string GitHubClientId => OAuthConfig.GitHubClientId;
        private static string GitHubClientSecret => OAuthConfig.GitHubClientSecret;
        private const string RedirectUri = "http://localhost:5200/callback";

        private static readonly HttpClient Http = new HttpClient();

        private HttpListener? _listener;
        private string _oauthState = string.Empty;
        private OAuthProvider _pendingProvider;
        private enum OAuthProvider { None, Google, GitHub }

        private Panel _leftPanel = null!;
        private Panel _rightPanel = null!;
        private Panel _loadingOverlay = null!;
        private Label _loadingLabel = null!;
        private Label _errorLabel = null!;

        private static readonly Color BgDeep = Color.FromArgb(10, 12, 18);
        private static readonly Color BgPanel = Color.FromArgb(16, 18, 26);
        private static readonly Color BgCard = Color.FromArgb(22, 26, 38);
        private static readonly Color BgElevated = Color.FromArgb(28, 32, 48);
        private static readonly Color Accent = Color.FromArgb(124, 111, 247);
        private static readonly Color AccentDim = Color.FromArgb(36, 27, 82);
        private static readonly Color Teal = Color.FromArgb(52, 211, 153);
        private static readonly Color TealDim = Color.FromArgb(10, 48, 36);
        private static readonly Color TextPri = Color.FromArgb(240, 242, 255);
        private static readonly Color TextSec = Color.FromArgb(157, 163, 191);
        private static readonly Color TextMuted = Color.FromArgb(90, 96, 128);
        private static readonly Color Border = Color.FromArgb(40, 44, 65);
        private static readonly Color GoogleRed = Color.FromArgb(234, 67, 53);
        private static readonly Color GhBlack = Color.FromArgb(36, 41, 47);
        private static readonly Color Coral = Color.FromArgb(248, 113, 113);

        private static readonly Font FontDisplay = new Font("Segoe UI", 22F, FontStyle.Bold);
        private static readonly Font FontTitle = new Font("Segoe UI", 11F, FontStyle.Bold);
        private static readonly Font FontBody = new Font("Segoe UI", 9.5F);
        private static readonly Font FontSmall = new Font("Segoe UI", 8.5F);
        private static readonly Font FontMono = new Font("Cascadia Code", 8F);

        public LoginForm()
        {
            Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "FileExplorerr/1.0");
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "FileExplorerr — Iniciar sesión";
            Size = new Size(900, 660);
            MinimumSize = new Size(800, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BackColor = BgDeep;
            ForeColor = TextPri;
            Font = FontBody;

            _leftPanel = new Panel
            {
                Width = 340,
                Dock = DockStyle.Left,
                BackColor = BgPanel
            };
            _leftPanel.Paint += LeftPanel_Paint;
            BuildLeftPanel();

            _rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgDeep,
                Padding = new Padding(48, 40, 48, 32)
            };
            BuildRightPanel();

            _loadingOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(220, 10, 12, 18),
                Visible = false
            };
            _loadingLabel = new Label
            {
                AutoSize = true,
                Font = FontTitle,
                ForeColor = Accent,
                BackColor = Color.Transparent,
                Text = "Conectando..."
            };
            _loadingOverlay.Controls.Add(_loadingLabel);
            _loadingOverlay.Resize += (s, e) => CenterLoading();
            _loadingOverlay.Click += (s, e) => { };

            Controls.Add(_rightPanel);
            Controls.Add(_leftPanel);
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();
        }

        private void BuildLeftPanel()
        {
            var logoArea = new Panel
            {
                Left = 0,
                Top = 0,
                Width = 340,
                Height = 200,
                BackColor = Color.Transparent
            };
            logoArea.Paint += LogoArea_Paint;
            _leftPanel.Controls.Add(logoArea);

            int y = 230;
            var features = new[]
            {
                ("🗂", "Explorador de archivos avanzado"),
                ("🎵", "Reproductor de música y video"),
                ("🖼", "Visor y editor de imágenes con GPS"),
                ("📊", "Análisis de datos CSV/JSON/XML"),
                ("🗄", "Cliente SQL integrado"),
                ("📦", "Compresión y extracción de archivos"),
            };

            foreach (var (icon, text) in features)
            {
                var row = new Panel
                {
                    Left = 28,
                    Top = y,
                    Width = 284,
                    Height = 30,
                    BackColor = Color.Transparent
                };
                row.Controls.Add(new Label
                {
                    Text = icon,
                    Left = 0,
                    Top = 5,
                    Width = 24,
                    Height = 22,
                    Font = new Font("Segoe UI", 11F),
                    ForeColor = Teal,
                    BackColor = Color.Transparent
                });
                row.Controls.Add(new Label
                {
                    Text = text,
                    Left = 30,
                    Top = 7,
                    Width = 250,
                    Height = 18,
                    Font = FontSmall,
                    ForeColor = TextSec,
                    BackColor = Color.Transparent
                });
                _leftPanel.Controls.Add(row);
                y += 34;
            }

            _leftPanel.Controls.Add(new Label
            {
                Text = "v1.0  / .NET 8",
                Left = 28,
                Top = 560,
                Width = 284,
                Height = 18,
                Font = FontSmall,
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            });
        }


        private void LeftPanel_Paint(object? sender, PaintEventArgs e)
        {
            // Evitar crash de GDI+ cuando se minimiza la ventana (Height o Width en 0)
            if (_leftPanel.Width <= 0 || _leftPanel.Height <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var br = new LinearGradientBrush(
                new Point(0, 0), new Point(0, _leftPanel.Height),
                Color.FromArgb(20, 22, 34), Color.FromArgb(14, 16, 24));
            g.FillRectangle(br, 0, 0, _leftPanel.Width, _leftPanel.Height);

            using var pen = new Pen(Border, 1);
            g.DrawLine(pen, _leftPanel.Width - 1, 0, _leftPanel.Width - 1, _leftPanel.Height);

            using var radBr = new PathGradientBrush(new PointF[]
            {
                new(0, 0), new(340, 0), new(340, 200), new(0, 200)
            })
            {
                CenterPoint = new PointF(170, 100),
                CenterColor = Color.FromArgb(18, Accent),
                SurroundColors = new[] { Color.Transparent }
            };
            g.FillEllipse(radBr, 20, 20, 300, 160);
        }

        private void LogoArea_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using var accentBrush = new SolidBrush(Accent);
            using var tealBrush = new SolidBrush(Teal);
            float cx = 170, cy = 64;

            using var glowBr = new PathGradientBrush(new PointF[]
            {
                new(cx - 36, cy - 24), new(cx + 36, cy - 24),
                new(cx + 36, cy + 32), new(cx - 36, cy + 32)
            })
            {
                CenterPoint = new PointF(cx, cy),
                CenterColor = Color.FromArgb(60, Accent),
                SurroundColors = new[] { Color.Transparent }
            };
            g.FillEllipse(glowBr, cx - 52, cy - 40, 104, 80);

            g.FillRectangle(accentBrush, cx - 30, cy - 12, 60, 34);
            g.FillPolygon(accentBrush, new PointF[]
            {
                new(cx - 30, cy - 12), new(cx - 14, cy - 22), new(cx - 4, cy - 12)
            });

            using var whitePen = new Pen(Color.FromArgb(40, 255, 255, 255), 1.5f);
            g.DrawLine(whitePen, cx - 20, cy - 2, cx + 20, cy - 2);
            g.DrawLine(whitePen, cx - 20, cy + 7, cx + 20, cy + 7);
            g.DrawLine(whitePen, cx - 20, cy + 16, cx + 10, cy + 16);

            using var titleFont = new Font("Segoe UI", 18F, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("FileExplorerr", titleFont, accentBrush,
                new RectangleF(20, cy + 38, 300, 36), sf);

            using var subFont = new Font("Segoe UI", 8.5F);
            using var subBr = new SolidBrush(TextSec);
            g.DrawString("Explorador de archivos avanzado", subFont, subBr,
                new RectangleF(20, cy + 74, 300, 20), sf);
        }

        // ════════════════════════════════════════════════════════════════
        //  BUILD RIGHT PANEL  — layout con posiciones fijas bien calculadas
        //
        //  Top  40  → Título "Bienvenido de vuelta"       (36px)
        //  Top  84  → Subtítulo                           (20px)
        //  Top 120  → Botón Google                        (52px)
        //  Top 190  → Divisor "o"                         (22px)
        //  Top 220  → Botón GitHub                        (52px)
        //  Top 290  → Label error (oculto por defecto)    (20px)
        //  Top 320  → ── separador ──                     (1px)
        //  Top 340  → "¿Prefiero no iniciar sesión?"      (18px)
        //  Top 366  → Botón "Continuar como invitado"     (30px)
        //  Top 414  → ── separador ──                     (1px)
        //  Top 430  → Nota legal                          (30px)
        // ════════════════════════════════════════════════════════════════
        private void BuildRightPanel()
        {
            // ── Título ────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text = "Bienvenido de vuelta",
                Left = 48,
                Top = 40,
                Width = 400,
                Height = 36,
                Font = FontDisplay,
                ForeColor = TextPri,
                BackColor = Color.Transparent
            };

            // ── Subtítulo ─────────────────────────────────────────────
            var lblSub = new Label
            {
                Text = "Inicia sesión para sincronizar tu perfil y preferencias.",
                Left = 48,
                Top = 84,
                Width = 420,
                Height = 20,
                Font = FontBody,
                ForeColor = TextSec,
                BackColor = Color.Transparent
            };

            // ── Botón Google ──────────────────────────────────────────
            var btnGoogle = BuildOAuthButton(
                "Continuar con Google",
                DrawGoogleIcon,
                Color.FromArgb(234, 67, 53),
                Color.FromArgb(80, 20, 14));
            btnGoogle.Top = 120;
            btnGoogle.Click += async (s, e) => await StartOAuth(OAuthProvider.Google);

            // ── Divisor "o" ───────────────────────────────────────────
            var divPanel = new Panel
            {
                Left = 48,
                Top = 190,
                Width = 420,
                Height = 22,
                BackColor = Color.Transparent
            };
            divPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using var pen = new Pen(Border, 1);
                g.DrawLine(pen, 0, 10, 170, 10);
                g.DrawLine(pen, 252, 10, 420, 10);
                using var br = new SolidBrush(TextMuted);
                using var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("o", FontSmall, br, new RectangleF(170, 0, 82, 22), sf);
            };

            // ── Botón GitHub ──────────────────────────────────────────
            var btnGitHub = BuildOAuthButton(
                "Continuar con GitHub",
                DrawGitHubIcon,
                Color.FromArgb(200, 210, 230),
                Color.FromArgb(25, 28, 38));
            btnGitHub.Top = 220;
            btnGitHub.Click += async (s, e) => await StartOAuth(OAuthProvider.GitHub);

            // ── Label de error ────────────────────────────────────────
            _errorLabel = new Label
            {
                Left = 48,
                Top = 290,
                Width = 420,
                Height = 20,
                Font = FontSmall,
                ForeColor = Coral,
                BackColor = Color.Transparent,
                Text = "",
                Visible = false
            };

            // ── Separador superior ────────────────────────────────────
            var sepLine1 = new Panel
            {
                Left = 48,
                Top = 320,
                Width = 420,
                Height = 1,
                BackColor = Border
            };

            // ── "¿Prefiero no iniciar sesión?" ────────────────────────
            var lblGuest = new Label
            {
                Text = "¿Prefiero no iniciar sesión?",
                Left = 48,
                Top = 340,
                Width = 420,
                Height = 18,
                Font = FontSmall,
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            };

            // ── Botón invitado ────────────────────────────────────────
            var btnGuest = new Button
            {
                Text = "Continuar como invitado  →",
                Left = 48,
                Top = 366,
                Width = 220,
                Height = 32,
                BackColor = Color.Transparent,
                ForeColor = TextSec,
                FlatStyle = FlatStyle.Flat,
                Font = FontSmall,
                Cursor = Cursors.Hand
            };
            btnGuest.FlatAppearance.BorderSize = 0;
            btnGuest.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnGuest.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnGuest.Click += (s, e) =>
            {
                LoggedInUser = UserProfile.Guest();
                DialogResult = DialogResult.OK;
                Close();
            };

            // Cambiamos el ForeColor con los eventos para evitar 
            // el error de texto duplicado/encimado del evento Paint
            btnGuest.MouseEnter += (s, e) => btnGuest.ForeColor = Accent;
            btnGuest.MouseLeave += (s, e) => btnGuest.ForeColor = TextSec;

            // ── Separador inferior ────────────────────────────────────
            var sepLine2 = new Panel
            {
                Left = 48,
                Top = 414,
                Width = 420,
                Height = 1,
                BackColor = Border
            };

            // ── Nota legal ────────────────────────────────────────────
            var lblLegal = new Label
            {
                Text = "Al continuar, aceptas los Términos de uso y la Política de privacidad.",
                Left = 48,
                Top = 430,
                Width = 430,
                Height = 30,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            };

            _rightPanel.Controls.AddRange(new Control[]
            {
                lblTitle, lblSub,
                btnGoogle, divPanel, btnGitHub,
                _errorLabel,
                sepLine1, lblGuest, btnGuest,
                sepLine2, lblLegal
            });
        }

        private Button BuildOAuthButton(
            string text,
            Action<Graphics, Rectangle> iconDrawer,
            Color fg, Color bg)
        {
            var btn = new Button
            {
                Left = 48,
                Width = 420,
                Height = 52,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Text = text
            };
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(
                    Math.Min(bg.R + 18, 255),
                    Math.Min(bg.G + 18, 255),
                    Math.Min(bg.B + 18, 255));
            btn.FlatAppearance.MouseDownBackColor = bg;
            btn.TextAlign = ContentAlignment.MiddleCenter;

            btn.Paint += (s, e) =>
            {
                var b = (Button)s!;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var path = RoundedRect(b.ClientRectangle, 8);
                using var br = new SolidBrush(b.BackColor);
                g.FillPath(br, path);
                using var pen = new Pen(Border, 1);
                g.DrawPath(pen, path);

                iconDrawer(g, new Rectangle(20, 14, 24, 24));

                using var textBr = new SolidBrush(fg);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, b.Font, textBr,
                    new RectangleF(44, 0, b.Width - 44, b.Height), sf);
            };

            return btn;
        }

        private static void DrawGoogleIcon(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen1 = new Pen(Color.FromArgb(234, 67, 53), 2.5f);
            using var pen2 = new Pen(Color.FromArgb(52, 168, 83), 2.5f);
            using var pen3 = new Pen(Color.FromArgb(251, 188, 5), 2.5f);
            using var pen4 = new Pen(Color.FromArgb(66, 133, 244), 2.5f);
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rad = r.Width / 2f - 1;
            g.DrawArc(pen1, cx - rad, cy - rad, rad * 2, rad * 2, -10, 90);
            g.DrawArc(pen3, cx - rad, cy - rad, rad * 2, rad * 2, 80, 90);
            g.DrawArc(pen2, cx - rad, cy - rad, rad * 2, rad * 2, 170, 90);
            g.DrawArc(pen4, cx - rad, cy - rad, rad * 2, rad * 2, 260, 100);
            g.DrawLine(pen4, cx, cy, cx + rad, cy);
        }

        private static void DrawGitHubIcon(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(Color.FromArgb(200, 210, 230));
            g.FillEllipse(br, r);
            var earL = new RectangleF(r.X + 2, r.Y + 2, 7, 7);
            var earR = new RectangleF(r.Right - 9, r.Y + 2, 7, 7);
            g.FillEllipse(br, earL);
            g.FillEllipse(br, earR);
            g.FillEllipse(br, r.X + 3, r.Y + 4, r.Width - 6, r.Height - 6);
            using var innerBr = new SolidBrush(Color.FromArgb(25, 28, 38));
            g.FillEllipse(innerBr, r.X + 7, r.Y + 7, r.Width - 14, r.Height - 10);
            using var tentBr = new SolidBrush(Color.FromArgb(200, 210, 230));
            g.FillRectangle(tentBr, r.X + 7, r.Bottom - 8, 3, 5);
            g.FillRectangle(tentBr, r.X + 11, r.Bottom - 6, 2, 3);
            g.FillRectangle(tentBr, r.Right - 10, r.Bottom - 8, 3, 5);
            g.FillRectangle(tentBr, r.Right - 13, r.Bottom - 6, 2, 3);
        }

        // ════════════════════════════════════════════════════════════════
        //  OAUTH FLOW
        // ════════════════════════════════════════════════════════════════
        private async Task StartOAuth(OAuthProvider provider)
        {
            _pendingProvider = provider;
            _oauthState = Guid.NewGuid().ToString("N");
            ShowError(string.Empty);

            string url = provider == OAuthProvider.Google
                ? BuildGoogleAuthUrl()
                : BuildGitHubAuthUrl();

            try
            {
                _listener?.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:5200/");
                _listener.Start();
            }
            catch (Exception ex)
            {
                ShowError($"No se pudo iniciar el servidor local: {ex.Message}");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError($"No se pudo abrir el navegador: {ex.Message}");
                _listener.Close();
                return;
            }

            ShowLoading($"Esperando autorización de " +
                (provider == OAuthProvider.Google ? "Google" : "GitHub") + "...");

            try
            {
                var code = await Task.Run(async () =>
                {
                    var ctx = await _listener.GetContextAsync();
                    var query = ctx.Request.Url?.Query ?? "";
                    var @params = ParseQueryString(query);

                    string html = BuildCallbackHtml(@params.ContainsKey("error") ? "error" : "ok");
                    byte[] bytes = Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                    ctx.Response.Close();
                    _listener.Close();

                    if (@params.ContainsKey("error"))
                        throw new Exception(@params.GetValueOrDefault("error_description", "Acceso denegado"));

                    if (@params.GetValueOrDefault("state") != _oauthState)
                        throw new Exception("Estado OAuth inválido (posible CSRF)");

                    return @params.GetValueOrDefault("code", "");
                });

                if (string.IsNullOrEmpty(code))
                    throw new Exception("No se recibió el código de autorización");

                ShowLoading("Obteniendo token...");
                var token = provider == OAuthProvider.Google
                    ? await ExchangeGoogleCode(code)
                    : await ExchangeGitHubCode(code);

                ShowLoading("Obteniendo perfil...");
                var profile = provider == OAuthProvider.Google
                    ? await GetGoogleProfile(token)
                    : await GetGitHubProfile(token);

                SessionManager.Save(profile);
                LoggedInUser = profile;
                HideLoading();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                ShowError($"Error: {ex.Message}");
                try { _listener?.Close(); } catch { }
            }
        }

        private string BuildGoogleAuthUrl() =>
            "https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={Uri.EscapeDataString(GoogleClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            "&response_type=code" +
            "&scope=openid%20email%20profile" +
            $"&state={_oauthState}" +
            "&prompt=select_account";

        private string BuildGitHubAuthUrl() =>
            "https://github.com/login/oauth/authorize?" +
            $"client_id={Uri.EscapeDataString(GitHubClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            "&scope=user:email%20read:user" +
            $"&state={_oauthState}";

        private async Task<string> ExchangeGoogleCode(string code)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = GoogleClientId,
                ["client_secret"] = GoogleClientSecret,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code"
            });
            var resp = await Http.PostAsync("https://oauth2.googleapis.com/token", body);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString() ?? "";
        }

        private async Task<string> ExchangeGitHubCode(string code)
        {
            var req = new HttpRequestMessage(HttpMethod.Post,
                "https://github.com/login/oauth/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = GitHubClientId,
                    ["client_secret"] = GitHubClientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri
                })
            };
            req.Headers.Add("Accept", "application/json");
            var resp = await Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString() ?? "";
        }

        private async Task<UserProfile> GetGoogleProfile(string token)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/oauth2/v2/userinfo");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var resp = await Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new UserProfile
            {
                Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
                AvatarUrl = root.TryGetProperty("picture", out var pic) ? pic.GetString() ?? "" : "",
                Provider = "Google",
                AccessToken = token
            };
        }

        private async Task<UserProfile> GetGitHubProfile(string token)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var resp = await Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string email = "";
            if (!root.TryGetProperty("email", out var emailProp) ||
                emailProp.ValueKind == JsonValueKind.Null)
            {
                try
                {
                    var req2 = new HttpRequestMessage(HttpMethod.Get,
                        "https://api.github.com/user/emails");
                    req2.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    var resp2 = await Http.SendAsync(req2);
                    if (resp2.IsSuccessStatusCode)
                    {
                        var j2 = await resp2.Content.ReadAsStringAsync();
                        using var d2 = JsonDocument.Parse(j2);
                        foreach (var e in d2.RootElement.EnumerateArray())
                        {
                            if (e.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                            {
                                email = e.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                email = emailProp.GetString() ?? "";
            }
            return new UserProfile
            {
                Id = root.TryGetProperty("id", out var idp) ? idp.GetInt32().ToString() : "",
                Name = root.TryGetProperty("name", out var namep) ? namep.GetString() ?? "" : "",
                Email = email,
                AvatarUrl = root.TryGetProperty("avatar_url", out var av) ? av.GetString() ?? "" : "",
                Username = root.TryGetProperty("login", out var login) ? login.GetString() ?? "" : "",
                Provider = "GitHub",
                AccessToken = token
            };
        }

        private static string BuildCallbackHtml(string status) => status == "ok"
            ? @"<!DOCTYPE html><html><head><meta charset='utf-8'>
<style>body{background:#0a0c12;color:#f0f2ff;font-family:'Segoe UI',sans-serif;
display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;margin:0}
.icon{font-size:48px;margin-bottom:16px}.title{font-size:22px;font-weight:700;color:#34d399}
.sub{color:#9da3bf;margin-top:8px}</style></head><body>
<div class='icon'>✅</div>
<div class='title'>¡Autenticado correctamente!</div>
<div class='sub'>Puedes cerrar esta ventana y volver a FileExplorerr.</div>
<script>setTimeout(()=>window.close(),2500);</script></body></html>"
            : @"<!DOCTYPE html><html><head><meta charset='utf-8'>
<style>body{background:#0a0c12;color:#f0f2ff;font-family:'Segoe UI',sans-serif;
display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;margin:0}
.icon{font-size:48px;margin-bottom:16px}.title{font-size:22px;font-weight:700;color:#f87171}
.sub{color:#9da3bf;margin-top:8px}</style></head><body>
<div class='icon'>❌</div>
<div class='title'>Acceso cancelado</div>
<div class='sub'>Puedes cerrar esta ventana.</div>
<script>setTimeout(()=>window.close(),2500);</script></body></html>";

        private void ShowLoading(string msg)
        {
            if (InvokeRequired) { Invoke(() => ShowLoading(msg)); return; }
            _loadingLabel.Text = msg;
            _loadingOverlay.Visible = true;
            CenterLoading();
            _loadingOverlay.BringToFront();
        }

        private void HideLoading()
        {
            if (InvokeRequired) { Invoke(HideLoading); return; }
            _loadingOverlay.Visible = false;
        }

        private void CenterLoading()
        {
            if (_loadingLabel is null || _loadingOverlay is null) return;
            _loadingLabel.Location = new Point(
                (_loadingOverlay.Width - _loadingLabel.Width) / 2,
                (_loadingOverlay.Height - _loadingLabel.Height) / 2);
        }

        private void ShowError(string msg)
        {
            if (InvokeRequired) { Invoke(() => ShowError(msg)); return; }
            _errorLabel.Text = msg;
            _errorLabel.Visible = !string.IsNullOrEmpty(msg);
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in query.TrimStart('?').Split('&'))
            {
                var idx = part.IndexOf('=');
                if (idx < 0) continue;
                dict[Uri.UnescapeDataString(part[..idx])] =
                    Uri.UnescapeDataString(part[(idx + 1)..]);
            }
            return dict;
        }

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

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _listener?.Close(); } catch { } }
            base.Dispose(disposing);
        }
    }
}