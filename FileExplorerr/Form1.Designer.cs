namespace FileExplorerr
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 700);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "Form1";
            this.Text = "Explorador de Archivos";
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
        }

        private void InitializeCustomComponents()
        {
            // Panel superior para controles de navegación
            Panel topPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(10)
            };

            // Botón Atrás
            backButton = new Button
            {
                Text = "◄",
                Location = new Point(10, 10),
                Size = new Size(40, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            backButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            backButton.Click += BackButton_Click;

            // Botón Arriba
            upButton = new Button
            {
                Text = "▲",
                Location = new Point(55, 10),
                Size = new Size(40, 30),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            upButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            upButton.Click += UpButton_Click;

            // Barra de dirección
            addressBar = new TextBox
            {
                Location = new Point(100, 10),
                Size = new Size(topPanel.Width - 120, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F)
            };
            addressBar.KeyDown += AddressBar_KeyDown;

            topPanel.Controls.Add(backButton);
            topPanel.Controls.Add(upButton);
            topPanel.Controls.Add(addressBar);

            // ListView para archivos y carpetas
            imageList = new ImageList
            {
                ImageSize = new Size(32, 32),
                ColorDepth = ColorDepth.Depth32Bit
            };
            imageList.Images.Add("folder", CreateFolderIcon());
            imageList.Images.Add("file", CreateFileIcon());
            imageList.Images.Add("image", CreateImageIcon());
            imageList.Images.Add("audio", CreateAudioIcon());
            imageList.Images.Add("video", CreateVideoIcon());
            imageList.Images.Add("text", CreateTextIcon());

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
                SmallImageList = imageList,
                LargeImageList = imageList
            };

            // Columnas del ListView
            listView.Columns.Add("Nombre", 350);
            listView.Columns.Add("Tipo", 120);
            listView.Columns.Add("Tamaño", 100);
            listView.Columns.Add("Información", 250);
            listView.Columns.Add("Fecha modificación", 150);

            listView.DoubleClick += ListView_DoubleClick;
            listView.ColumnClick += ListView_ColumnClick;

            // Panel inferior para barra de estado
            Panel bottomPanel = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(45, 45, 45)
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 9F)
            };

            bottomPanel.Controls.Add(statusLabel);

            // Agregar controles al formulario
            this.Controls.Add(listView);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
        }

        #endregion
    }
}