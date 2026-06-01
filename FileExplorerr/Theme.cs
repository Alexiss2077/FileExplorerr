using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  TEMA GLOBAL — "Arctic Night"
    //  Paleta oscura profunda + acento violeta + semánticos vivos
    //  Reemplaza completamente al antiguo "Arctic Frost"
    // ════════════════════════════════════════════════════════════════════════
    internal static class Theme
    {
        // ── Fondos (4 capas de profundidad) ─────────────────────────────────
        public static readonly Color BgBase = Color.FromArgb(13, 15, 20);   // #0d0f14
        public static readonly Color BgSurface = Color.FromArgb(20, 23, 32);   // #141720
        public static readonly Color BgElevated = Color.FromArgb(28, 32, 48);   // #1c2030
        public static readonly Color BgCard = Color.FromArgb(26, 30, 46);   // #1a1e2e
        public static readonly Color BgHover = Color.FromArgb(33, 37, 56);   // #212538
        public static readonly Color BgActive = Color.FromArgb(41, 45, 70);   // #292d46
        public static readonly Color BgSelected = Color.FromArgb(36, 40, 64);   // #242840

        // ── Acento principal — Violeta/Púrpura ──────────────────────────────
        public static readonly Color Accent = Color.FromArgb(124, 111, 247);  // #7c6ff7
        public static readonly Color Accent2 = Color.FromArgb(167, 139, 250);  // #a78bfa
        public static readonly Color AccentDim = Color.FromArgb(124, 111, 247, 38); // 15% alpha
        public static readonly Color AccentBg = Color.FromArgb(36, 27, 82);   // #241b52

        // ── Teal — SQL / confirmaciones ─────────────────────────────────────
        public static readonly Color Teal = Color.FromArgb(52, 211, 153);  // #34d399
        public static readonly Color TealDim = Color.FromArgb(20, 60, 46);   // #143c2e

        // ── Coral — Video / peligro ─────────────────────────────────────────
        public static readonly Color Coral = Color.FromArgb(248, 113, 113);  // #f87171
        public static readonly Color CoralDim = Color.FromArgb(80, 30, 30);   // #501e1e

        // ── Ámbar — Advertencias / carpetas ─────────────────────────────────
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);  // #fbbf24
        public static readonly Color AmberDim = Color.FromArgb(80, 56, 8);   // #503808

        // ── Azul cielo — Info / imágenes ────────────────────────────────────
        public static readonly Color Sky = Color.FromArgb(96, 165, 250);  // #60a5fa
        public static readonly Color SkyDim = Color.FromArgb(12, 44, 78);   // #0c2c4e

        // ── Rosa — Audio / música ────────────────────────────────────────────
        public static readonly Color Pink = Color.FromArgb(244, 114, 182);  // #f472b6
        public static readonly Color PinkDim = Color.FromArgb(72, 24, 60);   // #48183c

        // ── Verde — Éxito / audio ────────────────────────────────────────────
        public static readonly Color Success = Color.FromArgb(52, 211, 153);  // igual a Teal
        public static readonly Color SuccessDim = Color.FromArgb(20, 60, 46);

        // ── Semánticos heredados (compatibilidad) ────────────────────────────
        public static readonly Color Danger = Coral;
        public static readonly Color DangerDim = CoralDim;
        public static readonly Color Warning = Amber;
        public static readonly Color WarningDim = AmberDim;

        // ── Texto (4 niveles) ────────────────────────────────────────────────
        public static readonly Color TextPrimary = Color.FromArgb(240, 242, 255); // #f0f2ff
        public static readonly Color TextSecondary = Color.FromArgb(157, 163, 191); // #9da3bf
        public static readonly Color TextMuted = Color.FromArgb(90, 96, 128); // #5a6080
        public static readonly Color TextOnAccent = Color.FromArgb(13, 15, 20);  // sobre fondo de acento

        // ── Bordes ───────────────────────────────────────────────────────────
        public static readonly Color Border = Color.FromArgb(255, 255, 255, 15);   // ~6% alpha
        public static readonly Color Border2 = Color.FromArgb(255, 255, 255, 26);   // ~10% alpha

        // ── Drag & Papelera ──────────────────────────────────────────────────
        public static readonly Color DragTarget = Color.FromArgb(30, 56, 70);
        public static readonly Color RecycleBg = Color.FromArgb(32, 20, 20);
        public static readonly Color RecycleHot = Color.FromArgb(80, 30, 30);

        // ── Fuentes ──────────────────────────────────────────────────────────
        public static readonly Font FontBody = new("Segoe UI", 9.5F);
        public static readonly Font FontBodyBold = new("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font FontSmall = new("Segoe UI", 8.5F);
        public static readonly Font FontSmallBold = new("Segoe UI", 8.5F, FontStyle.Bold);
        public static readonly Font FontTitle = new("Segoe UI", 12.0F, FontStyle.Bold);
        public static readonly Font FontMono = new("Cascadia Code", 9.5F);
        public static readonly Font FontMonoSmall = new("Cascadia Code", 8.5F);
        public static readonly Font FontIcon = new("Segoe UI", 14.0F);
        public static readonly Font FontIconBig = new("Segoe UI", 16.0F, FontStyle.Bold);
        public static readonly Font FontNavTab = new("Segoe UI", 9.0F, FontStyle.Bold);

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY METHODS
        // ════════════════════════════════════════════════════════════════════

        public static Button MakeButton(string text, int width = 0, ButtonKind kind = ButtonKind.Default)
        {
            var (bg, border, fg) = kind switch
            {
                ButtonKind.Primary => (AccentBg, Accent, TextPrimary),
                ButtonKind.Danger => (CoralDim, Coral, Coral),
                ButtonKind.Success => (TealDim, Teal, TextPrimary),
                ButtonKind.Ghost => (Color.Transparent, Border, TextSecondary),
                ButtonKind.Teal => (TealDim, Teal, Teal),
                ButtonKind.Amber => (AmberDim, Amber, Amber),
                ButtonKind.Sky => (SkyDim, Sky, Sky),
                _ => (BgElevated, Border, TextPrimary)
            };

            var btn = new Button
            {
                Text = text,
                Height = 34,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = FontBody,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = BgHover;
            btn.FlatAppearance.MouseDownBackColor = BgActive;
            if (width > 0) btn.Width = width;
            else btn.AutoSize = true;
            btn.Padding = new Padding(10, 0, 10, 0);
            btn.MinimumSize = new System.Drawing.Size(32, 34);
            return btn;
        }

        public static Button MakeIconButton(string icon, ButtonKind kind = ButtonKind.Default)
        {
            var btn = MakeButton(icon, 36, kind);
            btn.Font = FontIcon;
            btn.Padding = Padding.Empty;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.Height = 34;
            btn.Width = 34;
            return btn;
        }

        /// <summary>Crea un botón de pestaña de navegación superior.</summary>
        public static Button MakeNavTab(string icon, string label, Color accentColor)
        {
            var btn = new Button
            {
                Text = $"  {icon}  {label}",
                Height = 38,
                AutoSize = true,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font = FontNavTab,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = BgHover;
            // Al activarse, el formulario principal cambia BackColor y ForeColor.
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
                LabelKind.Title => (FontTitle, Accent2),
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

        /// <summary>Aplica el estilo Arctic Night a un DataGridView.</summary>
        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = BgBase;
            grid.GridColor = Color.FromArgb(255, 255, 255, 10);
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 32;
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
                Padding = new Padding(10, 0, 0, 0)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BgBase,
                ForeColor = TextPrimary,
                Font = FontBody,
                SelectionBackColor = BgSelected,
                SelectionForeColor = TextPrimary,
                Padding = new Padding(10, 0, 0, 0)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(18, 21, 32),
                ForeColor = TextPrimary,
                SelectionBackColor = BgSelected,
                SelectionForeColor = TextPrimary
            };
        }

        // ── Enums ────────────────────────────────────────────────────────────
        public enum ButtonKind { Default, Primary, Danger, Success, Ghost, Teal, Amber, Sky }
        public enum LabelKind { Body, Title, Subtitle, Caption, Mono, MonoSmall }
    }
}