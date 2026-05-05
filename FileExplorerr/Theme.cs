using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  TEMA GLOBAL — "Arctic Frost"
    //  Minimalista, limpio, fácil de leer.
    //  Paleta: negro suave + blanco cálido + acento teal + rojo suave
    // ════════════════════════════════════════════════════════════════════════
    internal static class Theme
    {
        // ── Fondos ───────────────────────────────────────────────────────────
        public static readonly Color BgBase = Color.FromArgb(18, 18, 22);
        public static readonly Color BgSurface = Color.FromArgb(26, 26, 32);
        public static readonly Color BgElevated = Color.FromArgb(34, 34, 42);
        public static readonly Color BgHover = Color.FromArgb(42, 42, 52);
        public static readonly Color BgSelected = Color.FromArgb(30, 70, 70);

        // ── Acento ───────────────────────────────────────────────────────────
        public static readonly Color Accent = Color.FromArgb(72, 202, 188);   // teal
        public static readonly Color AccentDim = Color.FromArgb(40, 120, 112);
        public static readonly Color AccentBg = Color.FromArgb(24, 56, 54);

        // ── Semánticos ───────────────────────────────────────────────────────
        public static readonly Color Danger = Color.FromArgb(220, 95, 85);
        public static readonly Color DangerDim = Color.FromArgb(80, 30, 30);
        public static readonly Color Success = Color.FromArgb(82, 196, 120);
        public static readonly Color SuccessDim = Color.FromArgb(24, 60, 36);
        public static readonly Color Warning = Color.FromArgb(230, 190, 70);
        public static readonly Color WarningDim = Color.FromArgb(60, 50, 20);

        // ── Texto ────────────────────────────────────────────────────────────
        public static readonly Color TextPrimary = Color.FromArgb(230, 230, 236);
        public static readonly Color TextSecondary = Color.FromArgb(140, 140, 156);
        public static readonly Color TextMuted = Color.FromArgb(90, 90, 106);
        public static readonly Color TextOnAccent = Color.FromArgb(18, 18, 22);

        // ── Bordes ───────────────────────────────────────────────────────────
        public static readonly Color Border = Color.FromArgb(44, 44, 54);
        public static readonly Color BorderLight = Color.FromArgb(54, 54, 64);

        // ── Drag ─────────────────────────────────────────────────────────────
        public static readonly Color DragTarget = Color.FromArgb(30, 70, 70);
        public static readonly Color RecycleBg = Color.FromArgb(28, 24, 24);
        public static readonly Color RecycleHot = Color.FromArgb(80, 30, 30);

        // ── Fuentes ──────────────────────────────────────────────────────────
        public static readonly Font FontBody = new("Segoe UI", 9F);
        public static readonly Font FontBodyBold = new("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font FontSmall = new("Segoe UI", 8F);
        public static readonly Font FontSmallBold = new("Segoe UI", 8F, FontStyle.Bold);
        public static readonly Font FontTitle = new("Segoe UI", 11F, FontStyle.Bold);
        public static readonly Font FontMono = new("Cascadia Code", 9F);
        public static readonly Font FontMonoSmall = new("Cascadia Code", 8F);
        public static readonly Font FontIcon = new("Segoe UI", 13F);
        public static readonly Font FontIconBig = new("Segoe UI", 14F, FontStyle.Bold);

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS — crear controles con estilo consistente
        // ════════════════════════════════════════════════════════════════════

        public static Button MakeButton(string text, int width = 0, ButtonKind kind = ButtonKind.Default)
        {
            var (bg, border, fg) = kind switch
            {
                ButtonKind.Primary => (AccentDim, Accent, TextPrimary),
                ButtonKind.Danger => (DangerDim, Danger, Danger),
                ButtonKind.Success => (SuccessDim, Success, TextPrimary),
                ButtonKind.Ghost => (Color.Transparent, Border, TextSecondary),
                _ => (BgElevated, Border, TextPrimary)
            };

            var btn = new Button
            {
                Text = text,
                Height = 32,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = FontBody,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.BorderSize = 1;
            if (width > 0) btn.Width = width;
            else btn.AutoSize = true;
            btn.Padding = new Padding(8, 0, 8, 0);
            btn.MinimumSize = new System.Drawing.Size(32, 32);
            return btn;
        }

        public static Button MakeIconButton(string icon, ButtonKind kind = ButtonKind.Default)
        {
            var btn = MakeButton(icon, 36, kind);
            btn.Font = FontIcon;
            btn.Padding = Padding.Empty;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            return btn;
        }

        public static TextBox MakeTextBox(string placeholder = "")
        {
            return new TextBox
            {
                BackColor = BgElevated,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = FontBody,
                PlaceholderText = placeholder
            };
        }

        public static Label MakeLabel(string text, LabelKind kind = LabelKind.Body)
        {
            var (font, color) = kind switch
            {
                LabelKind.Title => (FontTitle, Accent),
                LabelKind.Subtitle => (FontBodyBold, TextPrimary),
                LabelKind.Caption => (FontSmall, TextMuted),
                LabelKind.Mono => (FontMono, TextPrimary),
                LabelKind.MonoSmall => (FontMonoSmall, TextSecondary),
                _ => (FontBody, TextSecondary)
            };
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = true
            };
        }

        public static Panel MakeDivider(bool horizontal = true)
        {
            return new Panel
            {
                BackColor = Border,
                Height = horizontal ? 1 : 0,
                Width = horizontal ? 0 : 1
            };
        }

        /// Configura un DataGridView con el estilo del tema
        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = BgBase;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 30;
            grid.AllowUserToAddRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BgSurface,
                ForeColor = TextMuted,
                Font = FontSmallBold,
                SelectionBackColor = BgSurface,
                SelectionForeColor = TextMuted,
                Padding = new Padding(8, 0, 0, 0)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BgBase,
                ForeColor = TextPrimary,
                Font = FontBody,
                SelectionBackColor = BgSelected,
                SelectionForeColor = TextPrimary,
                Padding = new Padding(8, 0, 0, 0)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(22, 22, 28),
                ForeColor = TextPrimary,
                SelectionBackColor = BgSelected,
                SelectionForeColor = TextPrimary
            };
        }

        // ── Enums ────────────────────────────────────────────────────────────
        public enum ButtonKind { Default, Primary, Danger, Success, Ghost }
        public enum LabelKind { Body, Title, Subtitle, Caption, Mono, MonoSmall }
    }
}