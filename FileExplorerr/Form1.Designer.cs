namespace FileExplorerr
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // ── Controles de navegación ──────────────────────────────────────────
        private System.Windows.Forms.Button backButton = null!;
        private System.Windows.Forms.Button upButton = null!;
        private System.Windows.Forms.Button newFolderButton = null!;
        private System.Windows.Forms.Button refreshButton = null!;
        private System.Windows.Forms.Button exportCsvButton = null!;
        private System.Windows.Forms.TextBox addressBar = null!;

        // ── Lista principal ──────────────────────────────────────────────────
        private System.Windows.Forms.ListView listView = null!;
        private System.Windows.Forms.ImageList imageList = null!;
        private System.Windows.Forms.Label statusLabel = null!;

        // ── Panel papelera ────────────────────────────────────────────────────
        private System.Windows.Forms.Panel recycleDropPanel = null!;
        private System.Windows.Forms.Label recyclePanelLabel = null!;

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
            this.ClientSize = new System.Drawing.Size(1100, 700);
            this.MinimumSize = new System.Drawing.Size(900, 500);
            this.Name = "Form1";
            this.Text = "Explorador de Archivos";
            this.BackColor = System.Drawing.Color.FromArgb(10, 14, 20);
            this.ForeColor = System.Drawing.Color.FromArgb(220, 232, 248);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }
    }
}