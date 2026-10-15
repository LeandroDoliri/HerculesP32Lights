using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HerculesP32
{
    /// <summary>
    /// Interruptor visual. No toca el MIDI: le manda órdenes explícitas a HerculesTray
    /// (y la inicia si no está corriendo), y muestra el estado que la bandeja guarda.
    /// </summary>
    public class SwitchForm : Form
    {
        public const string WindowTitle = "Hercules P32 - Interruptor de Luces";

        private AppMode mode = AppMode.Normal;
        private string demoColor = "dj";
        private DateTime ignoreFileUntil = DateTime.MinValue;

        private Label lblTitle;
        private Label lblStatus;
        private Label lblSubtitle;
        private Button btnToggle;
        private Label lblHint;
        private System.Windows.Forms.Timer refreshTimer;

        public SwitchForm()
        {
            HerculesShared.LoadState(out mode, out demoColor);

            this.Text = WindowTitle;
            this.Size = new Size(380, 240);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(22, 26, 35);
            this.ForeColor = Color.White;

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

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

            // Mantiene la ventana sincronizada si el modo se cambia desde la bandeja
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += (s, e) => RefreshFromFile();
            refreshTimer.Start();
            this.Activated += (s, e) => RefreshFromFile();

            UpdateUI();
        }

        private void RefreshFromFile()
        {
            // Tras un clic, da tiempo a que la bandeja guarde el estado nuevo
            if (DateTime.Now < ignoreFileUntil) return;

            AppMode newMode;
            string newColor;
            HerculesShared.LoadState(out newMode, out newColor);
            if (newMode != mode || newColor != demoColor)
            {
                mode = newMode;
                demoColor = newColor;
                UpdateUI();
            }
        }

        private void ToggleState()
        {
            RefreshFromFile();
            AppMode target = mode == AppMode.Off ? AppMode.Normal : AppMode.Off;

            bool sent = HerculesShared.SignalTray(target == AppMode.Off ? HerculesShared.EventOff : HerculesShared.EventNormal);
            if (!sent && !StartTray(target)) return;

            mode = target;
            ignoreFileUntil = DateTime.Now.AddMilliseconds(1500);
            UpdateUI();
        }

        private bool StartTray(AppMode target)
        {
            string trayExe = Path.Combine(HerculesShared.AppDir, "HerculesTray.exe");
            if (!File.Exists(trayExe))
            {
                MessageBox.Show(this, "No se encontró HerculesTray.exe en:\n" + HerculesShared.AppDir,
                    WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(trayExe, target == AppMode.Off ? "/off" : "/normal")
                {
                    WorkingDirectory = HerculesShared.AppDir,
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo iniciar HerculesTray.exe:\n" + ex.Message,
                    WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void UpdateUI()
        {
            if (mode == AppMode.Off)
            {
                lblStatus.Text = "🌑 LUCES: APAGADAS";
                lblStatus.ForeColor = Color.FromArgb(240, 100, 100);
                lblSubtitle.Text = "El controlador está a oscuras. No se activará el modo demo.";
                btnToggle.Text = "💡 ENCENDER LUCES (Modo Programa)";
                btnToggle.BackColor = Color.FromArgb(0, 160, 220);
            }
            else if (mode == AppMode.Demo)
            {
                lblStatus.Text = "💡 LUCES: MODO LÁMPARA";
                lblStatus.ForeColor = Color.FromArgb(190, 120, 255);
                lblSubtitle.Text = "Colores fijos decorativos (" + demoColor + "). Se cambia desde la bandeja.";
                btnToggle.Text = "🌑 APAGAR LUCES";
                btnToggle.BackColor = Color.FromArgb(200, 60, 75);
            }
            else
            {
                lblStatus.Text = "🟢 LUCES: ENCENDIDAS (MODO NORMAL)";
                lblStatus.ForeColor = Color.FromArgb(0, 230, 140);
                lblSubtitle.Text = "Las luces las controla el programa de DJ.";
                btnToggle.Text = "🌑 APAGAR LUCES";
                btnToggle.BackColor = Color.FromArgb(200, 60, 75);
            }
            btnToggle.ForeColor = Color.White;
        }
    }

    public class HerculesToggle
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, HerculesShared.ToggleMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Ya hay una ventana abierta: la trae al frente en vez de abrir otra
                    IntPtr hwnd = FindWindow(null, SwitchForm.WindowTitle);
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindow(hwnd, SW_RESTORE);
                        SetForegroundWindow(hwnd);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SwitchForm());
            }
        }
    }
}
