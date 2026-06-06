using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FileExplorerr.Charts
{
    // ════════════════════════════════════════════════════════════════════════
    //  DATA CHART PANEL
    //  Control GDI+ para visualización de datos de DataTable.
    //  Soporta tres tipos: columnas verticales, barras horizontales, pastel.
    //
    //  Diseño: Single Responsibility — solo dibuja. La lógica de qué datos
    //  mostrar vive en ChartDataBuilder.
    //
    //  Uso:
    //      var panel = new DataChartPanel();
    //      panel.SetData(ChartDataBuilder.Build(dataTable, "Categoria", "Valor"), ChartType.Columns, "Ventas por categoría");
    // ════════════════════════════════════════════════════════════════════════
    public enum ChartType { Columns, Bars, Pie }

    public sealed class DataChartPanel : Panel
    {
        // ── Estado ────────────────────────────────────────────────────────
        private List<(string Label, double Value)> _data = new();
        private ChartType _type = ChartType.Columns;
        private string _title = string.Empty;

        // ── Paleta — compatible con tema Arctic Night ─────────────────────
        private static readonly Color[] Palette =
        {
            Color.FromArgb(52,  211, 153),   // teal
            Color.FromArgb(251, 191,  36),   // amber
            Color.FromArgb(125, 211, 252),   // sky
            Color.FromArgb(167, 139, 250),   // purple
            Color.FromArgb(251, 113, 133),   // coral
            Color.FromArgb(110, 231, 183),   // mint
            Color.FromArgb(252, 211,  77),   // yellow
            Color.FromArgb(147, 197, 253),   // light blue
            Color.FromArgb(196, 181, 253),   // lavender
            Color.FromArgb(253, 164, 175),   // pink
        };

        // ── Colores de la UI ──────────────────────────────────────────────
        private static readonly Color BgPanel = Color.FromArgb(13, 13, 18);
        private static readonly Color GridLine = Color.FromArgb(36, 36, 52);
        private static readonly Color AxisColor = Color.FromArgb(55, 55, 75);
        private static readonly Color LabelColor = Color.FromArgb(110, 110, 140);

        public DataChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = BgPanel;
        }

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Actualiza los datos y fuerza un redibujado.
        /// Seguro llamar desde cualquier hilo gracias al Invoke interno.
        /// </summary>
        public void SetData(List<(string Label, double Value)> data, ChartType type, string title)
        {
            void Apply()
            {
                _data = data ?? new();
                _type = type;
                _title = title ?? string.Empty;
                Invalidate();
            }

            if (InvokeRequired)
                Invoke((Action)Apply);
            else
                Apply();
        }

        public void Clear()
        {
            _data.Clear();
            _title = string.Empty;
            Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BgPanel);

            if (_data.Count == 0 || IsAllZero())
            {
                DrawCentered(g, "Sin datos — carga un archivo primero");
                return;
            }

            switch (_type)
            {
                case ChartType.Columns: DrawColumns(g); break;
                case ChartType.Bars: DrawBars(g); break;
                case ChartType.Pie: DrawPie(g); break;
            }
        }

        // ── Gráfica de columnas ───────────────────────────────────────────

        private void DrawColumns(Graphics g)
        {
            float titleH = DrawTitle(g, Color.FromArgb(52, 211, 153));
            const int marginLeft = 68;
            const int marginBottom = 80;
            float marginTop = titleH + 5;

            var area = new RectangleF(marginLeft, marginTop,
                Width - marginLeft - 20, Height - marginTop - marginBottom);
            if (area.Width < 20 || area.Height < 20) return;

            double max = MaxValue();
            if (max <= 0) return;

            DrawGridLinesH(g, area, max);
            DrawAxes(g, area);

            float barW = area.Width / _data.Count;
            float gap = Math.Max(3f, barW * 0.18f);

            for (int i = 0; i < _data.Count; i++)
            {
                var (label, value) = _data[i];
                float h = (float)(value / max * area.Height);
                float x = area.Left + i * barW + gap / 2f;
                float y = area.Bottom - h;
                float w = barW - gap;
                var c = Palette[i % Palette.Length];

                if (h > 0 && w > 0)
                {
                    var rect = new RectangleF(x, y, w, h);
                    using var br = new LinearGradientBrush(
                        new PointF(x, y), new PointF(x, area.Bottom),
                        Color.FromArgb(230, c), Color.FromArgb(130, c));
                    g.FillRectangle(br, rect);
                    using var pen = new Pen(Color.FromArgb(180, c), 1f);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }

                string vs = FormatValue(value);
                using var vf = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                var vsz = g.MeasureString(vs, vf);
                if (vsz.Width < w + 4)
                    g.DrawString(vs, vf, new SolidBrush(Color.FromArgb(200, c)),
                        x + (w - vsz.Width) / 2f, Math.Max(y - vsz.Height - 2f, area.Top + 2));

                DrawXLabel(g, label, x + w / 2f, area.Bottom + 5f, c);
            }
        }

        // ── Gráfica de barras horizontales ────────────────────────────────

        private void DrawBars(Graphics g)
        {
            float titleH = DrawTitle(g, Color.FromArgb(251, 191, 36));

            using var lblFont = new Font("Segoe UI", 8.5f);
            float maxLabelW = 0f;
            foreach (var (lbl, _) in _data)
                maxLabelW = Math.Max(maxLabelW, g.MeasureString(lbl, lblFont).Width);

            float marginLeft = Math.Clamp(maxLabelW + 10f, 90f, Width * 0.40f);
            float marginRight = 64f;
            float marginTop = titleH + 8f;
            float marginBot = 16f;

            var area = new RectangleF(marginLeft, marginTop,
                Width - marginLeft - marginRight, Height - marginTop - marginBot);
            if (area.Width < 20 || area.Height < 20) return;

            double max = MaxValue();
            if (max <= 0) return;

            DrawGridLinesV(g, area, max);
            DrawAxes(g, area);

            float barH = area.Height / _data.Count;
            float gap = Math.Max(2f, barH * 0.20f);

            for (int i = 0; i < _data.Count; i++)
            {
                var (lbl, value) = _data[i];
                float bw = (float)(value / max * area.Width);
                float y = area.Top + i * barH + gap / 2f;
                float h = barH - gap;
                var c = Palette[i % Palette.Length];

                if (bw > 0 && h > 0)
                {
                    var rect = new RectangleF(area.Left, y, bw, h);
                    using var br = new LinearGradientBrush(
                        new PointF(area.Left, y), new PointF(area.Left + bw, y),
                        Color.FromArgb(130, c), Color.FromArgb(220, c));
                    g.FillRectangle(br, rect);
                    using var pen = new Pen(Color.FromArgb(180, c), 1f);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }

                // Valor a la derecha
                string vs = FormatValue(value);
                using var vf = new Font("Segoe UI", 7.5f);
                var vsz = g.MeasureString(vs, vf);
                g.DrawString(vs, vf, new SolidBrush(Color.FromArgb(190, c)),
                    area.Left + bw + 5f, y + (h - vsz.Height) / 2f);

                // Etiqueta a la izquierda (truncar si no cabe)
                float availW = marginLeft - 8f;
                string dispLabel = TruncateLabel(g, lbl, lblFont, availW);
                var lblSz = g.MeasureString(dispLabel, lblFont);
                g.DrawString(dispLabel, lblFont, new SolidBrush(Color.FromArgb(200, c)),
                    area.Left - lblSz.Width - 6f, y + (h - lblSz.Height) / 2f);
            }
        }

        // ── Gráfica de pastel ─────────────────────────────────────────────

        private void DrawPie(Graphics g)
        {
            float titleH = DrawTitle(g, Color.FromArgb(125, 211, 252));
            double total = Sum();
            if (total <= 0) return;

            float legendW = Math.Min(220f, Width * 0.30f);
            float drawW = Width - legendW - 20f;
            float drawH = Height - titleH - 20f;
            float radius = Math.Min(drawW / 2f - 30f, drawH / 2f - 20f);
            if (radius < 15) return;

            float cx = 20f + drawW / 2f;
            float cy = titleH + 20f + drawH / 2f;
            var pieR = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);

            float startAngle = -90f;
            for (int i = 0; i < _data.Count; i++)
            {
                float sweep = (float)(_data[i].Value / total * 360.0);
                var c = Palette[i % Palette.Length];

                using var br = new SolidBrush(c);
                using var sep = new Pen(BgPanel, 2f);
                g.FillPie(br, pieR, startAngle, sweep);
                g.DrawPie(sep, pieR, startAngle, sweep);

                if (sweep > 12f)
                {
                    double midRad = (startAngle + sweep / 2f) * Math.PI / 180.0;
                    float lx = cx + (float)(Math.Cos(midRad) * radius * 0.62f);
                    float ly = cy + (float)(Math.Sin(midRad) * radius * 0.62f);
                    string pct = $"{_data[i].Value / total:P0}";
                    using var pf = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    var ps = g.MeasureString(pct, pf);
                    g.DrawString(pct, pf, Brushes.White, lx - ps.Width / 2f, ly - ps.Height / 2f);
                }
                startAngle += sweep;
            }

            // Leyenda lateral
            float lx2 = Width - legendW + 10f;
            float ly2 = titleH + 30f;
            using var legF = new Font("Segoe UI", 8f);
            for (int i = 0; i < _data.Count && ly2 + 16 < Height - 10; i++)
            {
                var c = Palette[i % Palette.Length];
                string display = _data[i].Label.Length > 22
                    ? _data[i].Label[..20] + "…"
                    : _data[i].Label;
                string pct = $"{_data[i].Value / total:P1}";
                g.FillRectangle(new SolidBrush(c), lx2, ly2 + 1, 11, 11);
                g.DrawString($"{display}  {pct}", legF,
                    new SolidBrush(LabelColor), lx2 + 15f, ly2 - 1f);
                ly2 += 16f;
            }
        }

        // ── Helpers de dibujo ─────────────────────────────────────────────

        private float DrawTitle(Graphics g, Color accent)
        {
            if (string.IsNullOrEmpty(_title)) return 20f;
            using var tf = new Font("Segoe UI", 10f, FontStyle.Bold);
            var ts = g.MeasureString(_title, tf);
            g.DrawString(_title, tf, new SolidBrush(accent), (Width - ts.Width) / 2f, 8f);
            return ts.Height + 14f;
        }

        private void DrawGridLinesH(Graphics g, RectangleF area, double max)
        {
            using var gp = new Pen(GridLine, 1f) { DashStyle = DashStyle.Dash };
            using var lf = new Font("Segoe UI", 7f);
            const int n = 5;
            for (int i = 0; i <= n; i++)
            {
                double val = max * i / n;
                float y = area.Bottom - (float)(i * area.Height / n);
                g.DrawLine(gp, area.Left, y, area.Right, y);
                string vs = FormatValue(val);
                var sz = g.MeasureString(vs, lf);
                g.DrawString(vs, lf, new SolidBrush(LabelColor),
                    area.Left - sz.Width - 3f, y - sz.Height / 2f);
            }
        }

        private void DrawGridLinesV(Graphics g, RectangleF area, double max)
        {
            using var gp = new Pen(GridLine, 1f) { DashStyle = DashStyle.Dash };
            using var lf = new Font("Segoe UI", 7f);
            const int n = 5;
            for (int i = 0; i <= n; i++)
            {
                double val = max * i / n;
                float x = area.Left + (float)(i * area.Width / n);
                g.DrawLine(gp, x, area.Top, x, area.Bottom);
                string vs = FormatValue(val);
                var sz = g.MeasureString(vs, lf);
                g.DrawString(vs, lf, new SolidBrush(LabelColor),
                    x - sz.Width / 2f, area.Bottom + 3f);
            }
        }

        private void DrawAxes(Graphics g, RectangleF area)
        {
            using var pen = new Pen(AxisColor, 1.5f);
            g.DrawLine(pen, area.Left, area.Top, area.Left, area.Bottom);
            g.DrawLine(pen, area.Left, area.Bottom, area.Right, area.Bottom);
        }

        private void DrawXLabel(Graphics g, string label, float cx, float baseY, Color c)
        {
            using var f = new Font("Segoe UI", 7.5f);
            string txt = label;
            while (txt.Length > 1 && g.MeasureString(txt, f).Width > 110)
                txt = txt[..^1];
            if (txt.Length < label.Length) txt = txt.TrimEnd() + "…";
            var sz = g.MeasureString(txt, f);
            var state = g.Save();
            g.TranslateTransform(cx, baseY + 4f);
            g.RotateTransform(-45f);
            g.DrawString(txt, f, new SolidBrush(Color.FromArgb(190, c)), -sz.Width / 2f, 0f);
            g.Restore(state);
        }

        private void DrawCentered(Graphics g, string text)
        {
            using var f = new Font("Segoe UI", 11f);
            var sz = g.MeasureString(text, f);
            g.DrawString(text, f, new SolidBrush(LabelColor),
                (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
        }

        private string TruncateLabel(Graphics g, string label, Font f, float maxW)
        {
            if (g.MeasureString(label, f).Width <= maxW) return label;
            string result = string.Empty;
            foreach (char ch in label)
            {
                if (g.MeasureString(result + ch + "…", f).Width > maxW) break;
                result += ch;
            }
            return result + "…";
        }

        // ── Utilidades ────────────────────────────────────────────────────

        private double MaxValue()
        {
            double max = 0;
            foreach (var (_, v) in _data) if (v > max) max = v;
            return max;
        }

        private double Sum()
        {
            double s = 0;
            foreach (var (_, v) in _data) s += v;
            return s;
        }

        private bool IsAllZero()
        {
            foreach (var (_, v) in _data) if (v != 0) return false;
            return true;
        }

        private static string FormatValue(double v)
        {
            if (v >= 1_000_000) return $"{v / 1_000_000:F1}M";
            if (v >= 1_000) return $"{v / 1_000:F0}K";
            return v == Math.Floor(v) ? $"{(long)v}" : $"{v:F2}";
        }
    }
}