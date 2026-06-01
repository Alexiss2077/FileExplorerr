namespace FileExplorerr
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // ── Barra de navegación superior ─────────────────────────────────────
        private System.Windows.Forms.Panel topNavBar = null!;
        private System.Windows.Forms.Button navExplorer = null!;
        private System.Windows.Forms.Button navMusic = null!;
        private System.Windows.Forms.Button navVideo = null!;
        private System.Windows.Forms.Button navSql = null!;
        private System.Windows.Forms.Label appLogoLabel = null!;

        // ── Contenedor de páginas ─────────────────────────────────────────────
        private System.Windows.Forms.Panel pageContainer = null!;

        // ── Página Explorer ───────────────────────────────────────────────────
        private System.Windows.Forms.Panel explorerPage = null!;

        private System.Windows.Forms.Button backButton = null!;
        private System.Windows.Forms.Button forwardButton = null!;
        private System.Windows.Forms.Button upButton = null!;
        private System.Windows.Forms.Button refreshButton = null!;
        private System.Windows.Forms.TextBox addressBar = null!;
        private System.Windows.Forms.Button newFolderButton = null!;
        private System.Windows.Forms.Button exportCsvButton = null!;

        private System.Windows.Forms.Panel explorerSidebar = null!;
        private System.Windows.Forms.ListView listView = null!;
        private System.Windows.Forms.ImageList imageList = null!;
        private System.Windows.Forms.Label statusLabel = null!;
        private System.Windows.Forms.Panel statusBar = null!;

        // ── Papelera ──────────────────────────────────────────────────────────
        private System.Windows.Forms.Panel recycleDropPanel = null!;
        private System.Windows.Forms.Label recyclePanelLabel = null!;
        private System.Windows.Forms.PictureBox recycleIconBox = null!;

        // ── Panel derecho del Explorer ────────────────────────────────────────
        private System.Windows.Forms.Panel rightInfoPanel = null!;
        private System.Windows.Forms.TextBox searchBox = null!;
        private System.Windows.Forms.TreeView infoTree = null!;
        private System.Windows.Forms.Label folderNameLabel = null!;

        // ── Menú contextual ───────────────────────────────────────────────────
        private System.Windows.Forms.ContextMenuStrip contextMenu = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 760);
            this.MinimumSize = new System.Drawing.Size(960, 560);
            this.Name = "Form1";
            this.Text = "FileExplorerr";
            this.BackColor = System.Drawing.Color.FromArgb(13, 15, 20);
            this.ForeColor = System.Drawing.Color.FromArgb(240, 242, 255);
            this.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }
    }
}