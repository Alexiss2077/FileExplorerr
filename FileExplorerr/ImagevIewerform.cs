using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FileExplorerr
{
    public class ImageViewerForm : Form
    {
        private PictureBox pictureBox;
        private Panel topPanel;
        private Label imageInfoLabel;
        private string imagePath;
        private Image currentImage;
        private float zoomFactor = 1.0f;

        public ImageViewerForm(string path)
        {
            imagePath = path;
            SetupComponents();
            LoadImage();
        }

        private void SetupComponents()
        {
            this.Text = $"Visor de imágenes — {Path.GetFileName(imagePath)}";
            this.Size = new Size(1000, 800);
            this.BackColor = Color.FromArgb(10, 14, 20);
            this.ForeColor = Color.FromArgb(220, 232, 248);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(600, 400);
            this.KeyPreview = true;
            this.KeyDown += OnKeyDown;

            topPanel = new Panel
            {
                Height = 52,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(17, 23, 33),
                Padding = new Padding(14, 0, 0, 0)
            };
            topPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(38, 50, 70)),
                    0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);

            imageInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(110, 140, 180),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Button btnZoomIn = MakeBtn("+", 14F, FontStyle.Bold);
            Button btnZoomOut = MakeBtn("−", 14F, FontStyle.Bold);
            Button btnReset = MakeBtn("1:1", 8F, FontStyle.Regular);

            btnZoomIn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnZoomOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnZoomIn.Location = new Point(topPanel.Width - 138, 11);
            btnZoomOut.Location = new Point(topPanel.Width - 92, 11);
            btnReset.Location = new Point(topPanel.Width - 46, 11);

            btnZoomIn.Click += (s, e) => Zoom(1.2f);
            btnZoomOut.Click += (s, e) => Zoom(0.8f);
            btnReset.Click += (s, e) => ResetZoom();

            topPanel.Controls.Add(imageInfoLabel);
            topPanel.Controls.Add(btnZoomIn);
            topPanel.Controls.Add(btnZoomOut);
            topPanel.Controls.Add(btnReset);

            Panel imagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(8, 11, 16)
            };

            pictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill
            };

            imagePanel.Controls.Add(pictureBox);
            this.Controls.Add(imagePanel);
            this.Controls.Add(topPanel);
        }

        private Button MakeBtn(string text, float size, FontStyle style)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(38, 30),
                BackColor = Color.FromArgb(24, 32, 46),
                ForeColor = Color.FromArgb(220, 232, 248),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", size, style),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(38, 50, 70);
            return btn;
        }

        private void LoadImage()
        {
            try
            {
                currentImage = Image.FromFile(imagePath);
                pictureBox.Image = currentImage;
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la imagen: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void Zoom(float factor)
        {
            zoomFactor = Math.Max(0.1f, Math.Min(10f, zoomFactor * factor));
            UpdateImageSize();
            UpdateInfo();
        }

        private void ResetZoom()
        {
            zoomFactor = 1.0f;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.Dock = DockStyle.Fill;
            UpdateInfo();
        }

        private void UpdateImageSize()
        {
            if (currentImage == null) return;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            if (zoomFactor != 1.0f)
            {
                pictureBox.Dock = DockStyle.None;
                pictureBox.Size = new Size(
                    (int)(currentImage.Width * zoomFactor),
                    (int)(currentImage.Height * zoomFactor));
            }
            else
            {
                pictureBox.Dock = DockStyle.Fill;
            }
        }

        private void UpdateInfo()
        {
            if (currentImage == null) return;
            var fi = new FileInfo(imagePath);
            imageInfoLabel.Text =
                $"  {fi.Name}   ·   {currentImage.Width} × {currentImage.Height} px   ·   " +
                $"{FormatSize(fi.Length)}   ·   Zoom {zoomFactor:P0}";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Add:
                case Keys.Oemplus: Zoom(1.2f); break;
                case Keys.Subtract:
                case Keys.OemMinus: Zoom(0.8f); break;
                case Keys.D0:
                case Keys.NumPad0: if (e.Control) ResetZoom(); break;
                case Keys.Escape: this.Close(); break;
            }
        }

        private string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) currentImage?.Dispose();
            base.Dispose(disposing);
        }
    }
}