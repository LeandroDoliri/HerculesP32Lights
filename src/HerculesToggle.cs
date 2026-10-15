using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace HerculesP32
{
    public class SwitchForm : Form
    {
        private string appDir;
        private string stateFile;
        private bool isOff = false;

        private Label lblTitle;
        private Label lblStatus;
        private Label lblSubtitle;
        private Button btnToggle;
        private Label lblHint;

        public SwitchForm()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            stateFile = Path.Combine(appDir, "state.ini");

            LoadState();

            this.Text = "Hercules P32 - Interruptor de Luces";
            this.Size = new Size(380, 240);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(22, 26, 35);
            this.ForeColor = Color.White;

            string iconPath = Path.Combine(appDir, "app.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            lblTitle = new Label();
            lblTitle.Text = "🎛️ Hercules DJControl P32 DJ";
            lblTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(220, 230, 245);
            lblTitle.Location = new Point(20, 16);
            lblTitle.AutoSize = true;
            this.Controls.Add(lblTitle);

            lblStatus = new Label();
            lblStatus.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            lblStatus.Location = new Point(20, 50);
            lblStatus.AutoSize = true;
            this.Controls.Add(lblStatus);

            lblSubtitle = new Label();
            lblSubtitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(160, 170, 190);
            lblSubtitle.Location = new Point(20, 80);
            lblSubtitle.Size = new Size(330, 36);
            this.Controls.Add(lblSubtitle);

            btnToggle = new Button();
            btnToggle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnToggle.Location = new Point(20, 122);
            btnToggle.Size = new Size(325, 45);
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Cursor = Cursors.Hand;
            btnToggle.Click += (s, e) => ToggleState();
            this.Controls.Add(btnToggle);

            lblHint = new Label();
            lblHint.Text = "Tip: Clic derecho en este icono en la barra de tareas -> 'Anclar'";
            lblHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            lblHint.ForeColor = Color.FromArgb(120, 130, 150);
            lblHint.Location = new Point(20, 175);
            lblHint.AutoSize = true;
            this.Controls.Add(lblHint);

            UpdateUI();
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(stateFile))
                {
                    string txt = File.ReadAllText(stateFile);
                    isOff = txt.Contains("Mode=Off");
                }
            }
            catch { }
        }

        private void ToggleState()
        {
            isOff = !isOff;
            if (isOff)
            {
                HerculesMidi.ClearAllLights();
                File.WriteAllText(stateFile, "Mode=Off\nDemoColor=dj\n");
            }
            else
            {
                File.WriteAllText(stateFile, "Mode=Normal\nDemoColor=dj\n");
                // Dispara refresco en Traktor Pro 4
                new Thread(() =>
                {
                    Thread.Sleep(80);
                    HerculesMidi.TriggerTraktorRefresh();
                }).Start();
            }

            try
            {
                EventWaitHandle evt;
                if (EventWaitHandle.TryOpenExisting(@"Global\HerculesP32ToggleEvent", out evt))
                {
                    evt.Set();
                }
            }
            catch { }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (isOff)
            {
                lblStatus.Text = "🌑 LUCES: APAGADAS";
                lblStatus.ForeColor = Color.FromArgb(240, 100, 100);
                lblSubtitle.Text = "El controlador está a oscuras. No se activará el modo demo.";
                btnToggle.Text = "💡 ENCENDER LUCES (Modo Programa)";
                btnToggle.BackColor = Color.FromArgb(0, 160, 220);
                btnToggle.ForeColor = Color.White;
            }
            else
            {
                lblStatus.Text = "🟢 LUCES: ENCENDIDAS (MODO NORMAL)";
                lblStatus.ForeColor = Color.FromArgb(0, 230, 140);
                lblSubtitle.Text = "Las luces responden 100% a Traktor en tiempo real.";
                btnToggle.Text = "🌑 APAGAR LUCES";
                btnToggle.BackColor = Color.FromArgb(200, 60, 75);
                btnToggle.ForeColor = Color.White;
            }
        }
    }

    public class HerculesToggle
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SwitchForm());
        }
    }
}
