namespace FileExplorerr
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Button backButton;
        private System.Windows.Forms.Button upButton;
        private System.Windows.Forms.Button newFolderButton;
        private System.Windows.Forms.TextBox addressBar;
        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Panel recycleDropPanel;
        private System.Windows.Forms.Label recyclePanelLabel;
        private System.Windows.Forms.ContextMenuStrip contextMenu;

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
            this.MinimumSize = new System.Drawing.Size(800, 500);
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