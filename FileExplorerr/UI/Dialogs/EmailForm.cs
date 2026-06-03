using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileExplorerr
{
    public partial class EmailForm : Form
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const int FormWidth = 400;
        private const int FormHeight = 310;
        private const int ConfigFormWidth = 460;
        private const int ConfigFormHeight = 290;

        // ── State ────────────────────────────────────────────────────────────
        private readonly string _filePath;
        private SmtpConfig _smtpConfig;

        // ── Controls ─────────────────────────────────────────────────────────
        private TextBox txtTo = null!;
        private TextBox txtSubject = null!;
        private Button btnSend = null!;
        private Label lblStatus = null!;

        public EmailForm(string filePath)
        {
            _filePath = filePath;
            _smtpConfig = SmtpConfig.Load();
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Enviar Archivo por Correo";
            Size = new Size(FormWidth, FormHeight);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 35);
            ForeColor = Color.White;

            var lblFile = new Label
            {
                Text = $"Archivo adjunto: {Path.GetFileName(_filePath)}",
                Location = new Point(20, 15),
                Size = new Size(340, 20),
                ForeColor = Color.FromArgb(80, 160, 220),
                AutoEllipsis = true
            };

            var lblTo = new Label { Text = "Para (Correo):", Location = new Point(20, 45), AutoSize = true };
            txtTo = new TextBox
            {
                Location = new Point(20, 65),
                Size = new Size(340, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblSubject = new Label { Text = "Asunto:", Location = new Point(20, 100), AutoSize = true };
            txtSubject = new TextBox
            {
                Location = new Point(20, 120),
                Size = new Size(340, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(20, 175),
                Size = new Size(200, 20),
                ForeColor = Color.Yellow
            };

            btnSend = new Button
            {
                Text = "✉ Enviar",
                Location = new Point(200, 165),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.Click += async (_, _) => await SendEmailAsync();

            // Button to configure SMTP credentials without hardcoding them.
            var btnConfigure = new Button
            {
                Text = "⚙ Configurar SMTP",
                Location = new Point(20, 230),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.FromArgb(150, 180, 220),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnConfigure.FlatAppearance.BorderSize = 0;
            btnConfigure.Click += (_, _) => OpenSmtpConfig();

            Controls.AddRange(new Control[]
            {
                lblFile, lblTo, txtTo, lblSubject, txtSubject,
                lblStatus, btnSend, btnConfigure
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  SEND
        // ════════════════════════════════════════════════════════════════════
        private async Task SendEmailAsync()
        {
            // Reload in case the user just configured credentials.
            _smtpConfig = SmtpConfig.Load();

            if (!_smtpConfig.IsConfigured)
            {
                MessageBox.Show(
                    "Primero configura tu cuenta SMTP.\nHaz clic en '⚙ Configurar SMTP'.",
                    "Sin configuración SMTP",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string destination = txtTo.Text.Trim();

            if (!IsValidEmail(destination))
            {
                MessageBox.Show(
                    "Por favor, ingresa una dirección de correo válida.",
                    "Formato Incorrecto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtTo.Focus();
                return;
            }

            string subject = string.IsNullOrWhiteSpace(txtSubject.Text)
                ? $"Archivo compartido: {Path.GetFileName(_filePath)}"
                : txtSubject.Text.Trim();

            SetSendingState(true);

            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_smtpConfig.SenderAddress, "File Explorer App");
                message.To.Add(destination);
                message.Subject = subject;
                message.Body =
                    $"Hola,\n\nSe ha compartido un archivo contigo desde File Explorer." +
                    $"\n\nNombre del archivo: {Path.GetFileName(_filePath)}";
                message.IsBodyHtml = false;

                using var attachment = new Attachment(_filePath);
                message.Attachments.Add(attachment);

                // ── SMTP client ──────────────────────────────────────────────
                // Gmail requires port 587 + STARTTLS (EnableSsl = true) OR
                // port 465 + implicit SSL.  The key fix: set DeliveryMethod
                // explicitly and use a NetworkCredential with the app-password.
                using var smtp = new SmtpClient(_smtpConfig.Host)
                {
                    Port = _smtpConfig.Port,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,           // ← MUST be false before setting Credentials
                    Credentials = new NetworkCredential(
                        _smtpConfig.SenderAddress,
                        _smtpConfig.AppPassword),
                    EnableSsl = true,
                    Timeout = 30_000
                };

                await smtp.SendMailAsync(message);

                lblStatus.ForeColor = Color.LightGreen;
                lblStatus.Text = "¡Enviado con éxito!";

                MessageBox.Show(
                    "El archivo se envió correctamente.",
                    "Éxito",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Close();
            }
            catch (SmtpException ex)
            {
                lblStatus.Text = "Error al enviar.";
                lblStatus.ForeColor = Color.Red;

                // Provide actionable guidance for the most common Gmail errors.
                string extra = ex.StatusCode switch
                {
                    SmtpStatusCode.MustIssueStartTlsFirst =>
                        "\n\nAsegúrate de que el puerto sea 587 y SSL esté activado.",
                    SmtpStatusCode.MailboxUnavailable or
                    SmtpStatusCode.ClientNotPermitted =>
                        "\n\nVerifica que estés usando una Contraseña de Aplicación de Google " +
                        "(no tu contraseña normal) y que el acceso IMAP esté habilitado en Gmail.",
                    _ => string.Empty
                };

                MessageBox.Show(
                    $"Error SMTP al enviar el correo:\n\n{ex.Message}{extra}\n\n" +
                    "Verifica tu configuración SMTP.",
                    "Error de envío",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error al enviar.";
                lblStatus.ForeColor = Color.Red;

                MessageBox.Show(
                    $"Error inesperado:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetSendingState(false);
            }
        }

        private void SetSendingState(bool isSending)
        {
            btnSend.Enabled = !isSending;
            txtTo.Enabled = !isSending;
            txtSubject.Enabled = !isSending;

            if (isSending)
                lblStatus.Text = "Enviando correo...";
            else if (lblStatus.Text == "Enviando correo...")
                lblStatus.Text = string.Empty;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SMTP CONFIGURATION DIALOG
        // ════════════════════════════════════════════════════════════════════
        private void OpenSmtpConfig()
        {
            using var dlg = new Form
            {
                Text = "Configurar SMTP",
                Size = new Size(ConfigFormWidth, ConfigFormHeight),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 35),
                ForeColor = Color.White
            };

            var lblInfo = new Label
            {
                Text = "Para Gmail: usa una Contraseña de Aplicación (16 caracteres).\n" +
                       "Actívala en: cuenta.google.com → Seguridad → Contraseñas de aplicaciones.",
                Location = new Point(14, 14),
                Size = new Size(420, 40),
                ForeColor = Color.FromArgb(140, 160, 180)
            };

            var lblAddr = new Label { Text = "Cuenta Gmail:", Location = new Point(14, 62), AutoSize = true };
            var txtAddr = new TextBox
            {
                Location = new Point(14, 80),
                Size = new Size(420, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _smtpConfig.SenderAddress
            };

            var lblPass = new Label { Text = "Contraseña de aplicación (16 caracteres):", Location = new Point(14, 112), AutoSize = true };
            var txtPass = new TextBox
            {
                Location = new Point(14, 130),
                Size = new Size(420, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '●',
                Text = _smtpConfig.AppPassword
            };

            // Show/hide password toggle
            var chkShow = new CheckBox
            {
                Text = "Mostrar contraseña",
                Location = new Point(14, 160),
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 160, 180)
            };
            chkShow.CheckedChanged += (_, _) =>
                txtPass.PasswordChar = chkShow.Checked ? '\0' : '●';

            var lblHost = new Label { Text = "Servidor SMTP:", Location = new Point(14, 188), AutoSize = true };
            var txtHost = new TextBox
            {
                Location = new Point(150, 185),
                Size = new Size(180, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _smtpConfig.Host
            };
            var lblPort = new Label { Text = "Puerto:", Location = new Point(340, 188), AutoSize = true };
            var txtPort = new TextBox
            {
                Location = new Point(390, 185),
                Size = new Size(50, 25),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _smtpConfig.Port.ToString()
            };

            var btnSave = new Button
            {
                Text = "Guardar",
                Location = new Point(240, 220),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, _) =>
            {
                _smtpConfig.SenderAddress = txtAddr.Text.Trim();
                _smtpConfig.AppPassword = txtPass.Text.Trim();
                _smtpConfig.Host = txtHost.Text.Trim();
                if (int.TryParse(txtPort.Text.Trim(), out int p)) _smtpConfig.Port = p;
                _smtpConfig.Save();
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(338, 220),
                Size = new Size(76, 32),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            dlg.Controls.AddRange(new Control[]
            {
                lblInfo, lblAddr, txtAddr, lblPass, txtPass,
                chkShow, lblHost, txtHost, lblPort, txtPort,
                btnSave, btnCancel
            });

            dlg.ShowDialog(this);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}