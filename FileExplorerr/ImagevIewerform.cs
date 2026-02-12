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
            InitializeComponents();
            LoadImage();
        }

        private void InitializeComponents()
        {
            this.Text = $"Visor de imágenes - {Path.GetFileName(imagePath)}";
            this.Size = new Size(1000, 800);
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(600, 400);
            this.KeyPreview = true;
            this.KeyDown += ImageViewerForm_KeyDown;

            // Panel superior con información
            topPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(15)
            };

            imageInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(60, 60, 60),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Botones de zoom
            Button zoomInButton = new Button
            {
                Text = "+",
                Size = new Size(40, 30),
                Location = new Point(topPanel.Width - 100, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            zoomInButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            zoomInButton.Click += (s, e) => Zoom(1.2f);

            Button zoomOutButton = new Button
            {
                Text = "-",
                Size = new Size(40, 30),
                Location = new Point(topPanel.Width - 55, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            zoomOutButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            zoomOutButton.Click += (s, e) => Zoom(0.8f);

            Button resetButton = new Button
            {
                Text = "1:1",
                Size = new Size(40, 30),
                Location = new Point(topPanel.Width - 10, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F),
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            resetButton.Click += (s, e) => ResetZoom();

            topPanel.Controls.Add(imageInfoLabel);
            topPanel.Controls.Add(zoomInButton);
            topPanel.Controls.Add(zoomOutButton);
            topPanel.Controls.Add(resetButton);

            // Panel para la imagen con scroll
            Panel imagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(45, 45, 45)
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

        private void LoadImage()
        {
            try
            {
                currentImage = Image.FromFile(imagePath);
                pictureBox.Image = currentImage;

                FileInfo fileInfo = new FileInfo(imagePath);
                imageInfoLabel.Text = $"Archivo: {fileInfo.Name}\n" +
                                    $"Dimensiones: {currentImage.Width} x {currentImage.Height} px | " +
                                    $"Tamaño: {FormatFileSize(fileInfo.Length)} | " +
                                    $"Formato: {currentImage.RawFormat} | " +
                                    $"Zoom: {zoomFactor:P0}";
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
            zoomFactor *= factor;

            if (zoomFactor < 0.1f) zoomFactor = 0.1f;
            if (zoomFactor > 10f) zoomFactor = 10f;

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
            int newWidth = (int)(currentImage.Width * zoomFactor);
            int newHeight = (int)(currentImage.Height * zoomFactor);

            if (zoomFactor != 1.0f)
            {
                pictureBox.Dock = DockStyle.None;
                pictureBox.Size = new Size(newWidth, newHeight);
            }
            else
            {
                pictureBox.Dock = DockStyle.Fill;
            }
        }

        private void UpdateInfo()
        {
            if (currentImage != null)
            {
                FileInfo fileInfo = new FileInfo(imagePath);
                imageInfoLabel.Text = $"Archivo: {fileInfo.Name}\n" +
                                    $"Dimensiones: {currentImage.Width} x {currentImage.Height} px | " +
                                    $"Tamaño: {FormatFileSize(fileInfo.Length)} | " +
                                    $"Formato: {currentImage.RawFormat} | " +
                                    $"Zoom: {zoomFactor:P0}";
            }
        }

        private void ImageViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Add:
                case Keys.Oemplus:
                    Zoom(1.2f);
                    break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    Zoom(0.8f);
                    break;
                case Keys.D0:
                case Keys.NumPad0:
                    if (e.Control)
                        ResetZoom();
                    break;
                case Keys.Escape:
                    this.Close();
                    break;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                currentImage?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}