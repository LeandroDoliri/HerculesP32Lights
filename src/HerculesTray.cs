using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HerculesP32
{
    public class HerculesTrayApp : ApplicationContext
    {
        public enum AppMode
        {
            Normal,     // Program controls lights dynamically
            Off,        // All lights turned off (blackout)
            Demo        // Optional static decorative lamp mode
        }

        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem itemToggle;
        private ToolStripMenuItem itemModeNormal;
        private ToolStripMenuItem itemModeOff;
        private ToolStripMenuItem itemAutoStart;
        private ToolStripMenuItem itemColorDj;
        private ToolStripMenuItem itemColorPurple;
        private ToolStripMenuItem itemColorBlue;
        private ToolStripMenuItem itemColorRed;

        private Icon iconOn;
        private Icon iconOff;

        private AppMode currentMode = AppMode.Normal;
        private string demoColor = "dj";
        private string appDir;
        private string stateFile;

        private EventWaitHandle toggleEvent;
        private Thread listenerThread;
        private System.Windows.Forms.Timer offKeepAliveTimer;
        private volatile bool running = true;
        private SynchronizationContext uiContext;
        private static Mutex singleInstanceMutex;

        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "HerculesP32Tray";

        public HerculesTrayApp()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory;
            stateFile = Path.Combine(appDir, "state.ini");

            uiContext = WindowsFormsSynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            LoadIcons();
            LoadState();

            offKeepAliveTimer = new System.Windows.Forms.Timer();
            offKeepAliveTimer.Interval = 3500;
            offKeepAliveTimer.Tick += (s, e) =>
            {
                if (currentMode == AppMode.Off)
                {
                    HerculesMidi.ClearAllLights();
                }
            };

            bool createdNew;
            toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\HerculesP32ToggleEvent", out createdNew);

            listenerThread = new Thread(ListenForToggleRequests);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            InitializeTray();

            ApplyMode(currentMode);
        }

        private void LoadIcons()
        {
            try
            {
                string onPath = Path.Combine(appDir, "icon_on.ico");
                string offPath = Path.Combine(appDir, "icon_off.ico");

                iconOn = File.Exists(onPath) ? new Icon(onPath) : SystemIcons.Application;
                iconOff = File.Exists(offPath) ? new Icon(offPath) : SystemIcons.Application;
            }
            catch
            {
                iconOn = SystemIcons.Application;
                iconOff = SystemIcons.Application;
            }
        }

        private void InitializeTray()
        {
            contextMenu = new ContextMenuStrip();

            itemToggle = new ToolStripMenuItem("Alternar (Normal / Apagado)", null, (s, e) => ToggleLights());
            itemToggle.Font = new Font(contextMenu.Font, FontStyle.Bold);

            itemModeNormal = new ToolStripMenuItem("🟢 Modo Normal (Programa de DJ)", null, (s, e) => SetMode(AppMode.Normal));
            itemModeOff = new ToolStripMenuItem("🌑 Modo Apagado (Luces Apagadas)", null, (s, e) => SetMode(AppMode.Off));

            var demoMenu = new ToolStripMenuItem("💡 Modo Lámpara (Decorativo Opcional)");
            itemColorDj = new ToolStripMenuItem("DJ Multicolor", null, (s, e) => SetDemoMode("dj"));
            itemColorPurple = new ToolStripMenuItem("Púrpura Neón", null, (s, e) => SetDemoMode("purple"));
            itemColorBlue = new ToolStripMenuItem("Azul Eléctrico", null, (s, e) => SetDemoMode("blue"));
            itemColorRed = new ToolStripMenuItem("Rojo Fuego", null, (s, e) => SetDemoMode("red"));

            demoMenu.DropDownItems.Add(itemColorDj);
            demoMenu.DropDownItems.Add(itemColorPurple);
            demoMenu.DropDownItems.Add(itemColorBlue);
            demoMenu.DropDownItems.Add(itemColorRed);

            itemAutoStart = new ToolStripMenuItem("🚀 Iniciar con Windows", null, (s, e) => ToggleAutoStart());
            itemAutoStart.Checked = IsAutoStartEnabled();

            var itemExit = new ToolStripMenuItem("❌ Salir", null, (s, e) => ExitApp());

            contextMenu.Items.Add(itemToggle);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemModeNormal);
            contextMenu.Items.Add(itemModeOff);
            contextMenu.Items.Add(demoMenu);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemAutoStart);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemExit);

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;

            UpdateUI();
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleLights();
            }
        }

        private void ListenForToggleRequests()
        {
            while (running)
            {
                try
                {
                    if (toggleEvent.WaitOne(400))
                    {
                        if (!running) break;
                        uiContext.Post(state => ToggleLights(), null);
                    }
                }
                catch (ThreadAbortException) { break; }
                catch { }
            }
        }

        public void ToggleLights()
        {
            if (currentMode == AppMode.Off)
            {
                SetMode(AppMode.Normal);
            }
            else
            {
                SetMode(AppMode.Off);
            }
        }

        public void SetMode(AppMode mode)
        {
            currentMode = mode;
            ApplyMode(currentMode);
            SaveState();
            UpdateUI();
        }

        public void SetDemoMode(string color)
        {
            demoColor = color;
            currentMode = AppMode.Demo;
            ApplyMode(currentMode);
            SaveState();
            UpdateUI();
        }

        private void ApplyMode(AppMode mode)
        {
            try
            {
                if (mode == AppMode.Off)
                {
                    HerculesMidi.ClearAllLights();
                    offKeepAliveTimer.Start();
                }
                else if (mode == AppMode.Normal)
                {
                    offKeepAliveTimer.Stop();
                    // Refresca automáticamente las luces de Traktor
                    new Thread(() =>
                    {
                        Thread.Sleep(80);
                        HerculesMidi.TriggerTraktorRefresh();
                    }).Start();
                }
                else if (mode == AppMode.Demo)
                {
                    offKeepAliveTimer.Stop();
                    HerculesMidi.SetStaticDemoLights(demoColor);
                }
            }
            catch { }
        }

        private void UpdateUI()
        {
            try
            {
                if (currentMode == AppMode.Normal)
                {
                    notifyIcon.Icon = iconOn;
                    notifyIcon.Text = "Hercules P32: Luces NORMALES (Programa)";
                    itemModeNormal.Checked = true;
                    itemModeOff.Checked = false;
                }
                else if (currentMode == AppMode.Off)
                {
                    notifyIcon.Icon = iconOff;
                    notifyIcon.Text = "Hercules P32: Luces APAGADAS";
                    itemModeNormal.Checked = false;
                    itemModeOff.Checked = true;
                }
                else
                {
                    notifyIcon.Icon = iconOn;
                    notifyIcon.Text = "Hercules P32: Modo Lámpara (" + demoColor + ")";
                    itemModeNormal.Checked = false;
                    itemModeOff.Checked = false;
                }

                itemColorDj.Checked = (currentMode == AppMode.Demo && demoColor == "dj");
                itemColorPurple.Checked = (currentMode == AppMode.Demo && demoColor == "purple");
                itemColorBlue.Checked = (currentMode == AppMode.Demo && demoColor == "blue");
                itemColorRed.Checked = (currentMode == AppMode.Demo && demoColor == "red");
            }
            catch { }
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_KEY, false))
                {
                    if (key == null) return false;
                    return key.GetValue(APP_NAME) != null;
                }
            }
            catch { return false; }
        }

        private void ToggleAutoStart()
        {
            try
            {
                bool current = IsAutoStartEnabled();
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
                {
                    if (key != null)
                    {
                        if (current)
                        {
                            key.DeleteValue(APP_NAME, false);
                            itemAutoStart.Checked = false;
                        }
                        else
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(APP_NAME, "\"" + exePath + "\"");
                            itemAutoStart.Checked = true;
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(stateFile))
                {
                    string[] lines = File.ReadAllLines(stateFile);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string k = parts[0].Trim();
                            string v = parts[1].Trim();
                            if (k == "Mode")
                            {
                                if (v == "Off") currentMode = AppMode.Off;
                                else if (v == "Demo") currentMode = AppMode.Demo;
                                else currentMode = AppMode.Normal;
                            }
                            if (k == "DemoColor") demoColor = v;
                        }
                    }
                }
                else
                {
                    currentMode = AppMode.Normal;
                }
            }
            catch { currentMode = AppMode.Normal; }
        }

        private void SaveState()
        {
            try
            {
                string content = string.Format("Mode={0}\nDemoColor={1}\n", currentMode, demoColor);
                File.WriteAllText(stateFile, content);
            }
            catch { }
        }

        private void ExitApp()
        {
            running = false;
            try
            {
                if (offKeepAliveTimer != null) offKeepAliveTimer.Stop();
                if (toggleEvent != null) toggleEvent.Set();
            }
            catch { }

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            Application.Exit();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, @"Global\HerculesP32TrayMutex", out createdNew);

            if (!createdNew)
            {
                if (args.Length > 0 && args[0].ToLower() == "/toggle")
                {
                    try
                    {
                        EventWaitHandle evt = EventWaitHandle.OpenExisting(@"Global\HerculesP32ToggleEvent");
                        evt.Set();
                    }
                    catch { }
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HerculesTrayApp());
        }
    }
}
