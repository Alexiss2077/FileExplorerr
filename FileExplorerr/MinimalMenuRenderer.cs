using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  MINIMAL MENU RENDERER
    //  Custom ToolStrip renderer that paints context menus with the
    //  Arctic Night dark theme.
    //  Previously defined at the end of Form1.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class MinimalMenuRenderer : ToolStripProfessionalRenderer
    {
        public MinimalMenuRenderer() : base(new MinimalColorTable()) { }

        protected override void OnRenderMenuItemBackground(
            ToolStripItemRenderEventArgs e)
        {
            using var br = new SolidBrush(
                e.Item.Selected ? Theme.BgHover : Theme.BgElevated);
            e.Graphics.FillRectangle(br, new System.Drawing.Rectangle(
                System.Drawing.Point.Empty, e.Item.Size));
        }

        protected override void OnRenderSeparator(
            ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Color.FromArgb(255, 255, 255, 14));
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderToolStripBackground(
            ToolStripRenderEventArgs e)
        {
            using var br = new SolidBrush(Theme.BgElevated);
            e.Graphics.FillRectangle(br, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(
            ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(255, 255, 255, 15));
            e.Graphics.DrawRectangle(pen, new System.Drawing.Rectangle(
                e.AffectedBounds.X,
                e.AffectedBounds.Y,
                e.AffectedBounds.Width - 1,
                e.AffectedBounds.Height - 1));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MINIMAL COLOR TABLE
    //  Professional color table used by MinimalMenuRenderer.
    //  Previously defined at the end of Form1.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class MinimalColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected =>
            Theme.BgHover;

        public override Color MenuItemBorder =>
            Color.FromArgb(255, 255, 255, 15);

        public override Color MenuBorder =>
            Color.FromArgb(255, 255, 255, 15);

        public override Color ToolStripDropDownBackground =>
            Theme.BgElevated;

        public override Color ImageMarginGradientBegin =>
            Theme.BgSurface;

        public override Color ImageMarginGradientMiddle =>
            Theme.BgSurface;

        public override Color ImageMarginGradientEnd =>
            Theme.BgSurface;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LV COMPARER
    //  IComparer used by the main ListView to sort by column on header click.
    //  Previously defined at the end of Form1.cs.
    // ════════════════════════════════════════════════════════════════════════
    internal class LvComparer : IComparer
    {
        private readonly int _column;
        private readonly SortOrder _order;

        public LvComparer(int column, SortOrder order)
        {
            _column = column;
            _order = order;
        }

        public int Compare(object? x, object? y)
        {
            int result = string.Compare(
                ((ListViewItem)x!).SubItems[_column].Text,
                ((ListViewItem)y!).SubItems[_column].Text,
                System.StringComparison.CurrentCulture);

            return _order == SortOrder.Descending ? -result : result;
        }
    }
}