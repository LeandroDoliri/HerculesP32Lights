using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HerculesP32
{
    public class HerculesTrayApp : ApplicationContext
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem itemHeader;
        private ToolStripMenuItem itemDevice;
        private ToolStripMenuItem itemToggle;
        private ToolStripMenuItem itemModeNormal;
        private ToolStripMenuItem itemModeOff;
        private ToolStripMenuItem itemModeDemo;
        private ToolStripMenuItem[] itemColors;
        private ToolStripMenuItem itemAutoStart;

        private Icon iconOn;
        private Icon iconOff;

        // Se leen desde el hilo MIDI, por eso son volatile
        private volatile AppMode currentMode = AppMode.Normal;
        private volatile string demoColor = "dj";
        private volatile bool deviceConnected;

        private EventWaitHandle[] commandEvents;
        private Thread listenerThread;
        private volatile bool running = true;
        private SynchronizationContext uiContext;

        // Todo el MIDI corre en un único hilo, en orden, para no trabar la interfaz
        private readonly BlockingCollection<Action> midiQueue = new BlockingCollection<Action>();
        private Thread midiThread;
        private System.Threading.Timer watchdogTimer;
        private int watchdogPending;
        private bool deviceWasConnected;

        private static Mutex singleInstanceMutex;

        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "HerculesP32Tray";
        private const int WATCHDOG_INTERVAL_MS = 3500;

        public HerculesTrayApp(AppMode? forcedMode, string forcedColor)
        {
            uiContext = WindowsFormsSynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            LoadIcons();
            AppMode mode;
            string color;
            HerculesShared.LoadState(out mode, out color);
            currentMode = mode;
            demoColor = color;

            midiThread = new Thread(MidiWorker);
            midiThread.IsBackground = true;
            midiThread.Start();

            // Índices 0-2: alternar / apagar / normal; desde el 3: un evento por color de lámpara
            commandEvents = new EventWaitHandle[3 + HerculesShared.DemoColors.Length];
            commandEvents[0] = new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventToggle);
            commandEvents[1] = new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventOff);
            commandEvents[2] = new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventNormal);
            for (int i = 0; i < HerculesShared.DemoColors.Length; i++)
            {
                commandEvents[3 + i] = new EventWaitHandle(false, EventResetMode.AutoReset,
                    HerculesShared.LampEventName(HerculesShared.DemoColors[i]));
            }

            listenerThread = new Thread(ListenForCommands);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            deviceConnected = HerculesMidi.IsConnected();
            InitializeTray();

            if (forcedMode == AppMode.Demo)
            {
                SetDemoMode(forcedColor);
            }
            else if (forcedMode.HasValue)
            {
                // Iniciada por el interruptor: aplica la orden como si la hubiera pedido el usuario
                SetMode(forcedMode.Value);
            }
            else
            {
                // Al arrancar no se pide refresco a Traktor (evita mandar F12 al iniciar Windows)
                ApplyMode(currentMode, currentMode, false);
            }

            watchdogTimer = new System.Threading.Timer(s => QueueWatchdog(), null, WATCHDOG_INTERVAL_MS, WATCHDOG_INTERVAL_MS);
        }

        private static Icon LoadEmbeddedIcon(string name)
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("HerculesP32." + name))
                {
                    if (s != null) return new Icon(s);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        private void LoadIcons()
        {
            iconOn = LoadEmbeddedIcon("icon_on.ico");
            iconOff = LoadEmbeddedIcon("icon_off.ico");
        }

        private void InitializeTray()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.ShowImageMargin = true;
            int img;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) img = (int)Math.Round(16 * g.DpiX / 96f);
            contextMenu.ImageScalingSize = new Size(img, img);

            // Encabezado informativo: estado actual y conexión (no clickeable)
            itemHeader = new ToolStripMenuItem("Hercules P32");
            itemHeader.Font = new Font(contextMenu.Font, FontStyle.Bold);
            itemHeader.Enabled = false;
            itemDevice = new ToolStripMenuItem("");
            itemDevice.Enabled = false;

            // Acción principal (equivale al clic izquierdo en el ícono)
            itemToggle = new ToolStripMenuItem("", null, (s, e) => ToggleLights());
            itemToggle.Font = new Font(contextMenu.Font, FontStyle.Bold);

            itemModeNormal = new ToolStripMenuItem("Programa de DJ", HerculesUi.CreateDotBitmap(img, HerculesUi.Green),
                (s, e) => SetMode(AppMode.Normal));
            itemModeOff = new ToolStripMenuItem("Apagado", HerculesUi.CreateDotBitmap(img, HerculesUi.OffGray),
                (s, e) => SetMode(AppMode.Off));
            itemModeDemo = new ToolStripMenuItem("Modo lámpara");

            itemColors = new ToolStripMenuItem[HerculesShared.DemoColors.Length];
            for (int i = 0; i < itemColors.Length; i++)
            {
                string color = HerculesShared.DemoColors[i];
                itemColors[i] = new ToolStripMenuItem(HerculesUi.ColorName(color), HerculesUi.CreateSwatchBitmap(img, color),
                    (s, e) => SetDemoMode(color));
                itemModeDemo.DropDownItems.Add(itemColors[i]);
            }
            itemModeDemo.DropDown.ImageScalingSize = new Size(img, img);

            var itemOpenSwitch = new ToolStripMenuItem("Abrir interruptor…", null, (s, e) => OpenSwitch());

            itemAutoStart = new ToolStripMenuItem("Iniciar con Windows", null, (s, e) => ToggleAutoStart());
            itemAutoStart.Checked = IsAutoStartEnabled();

            var itemExit = new ToolStripMenuItem("Salir", null, (s, e) => ExitApp());

            contextMenu.Items.Add(itemHeader);
            contextMenu.Items.Add(itemDevice);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemToggle);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemModeNormal);
            contextMenu.Items.Add(itemModeOff);
            contextMenu.Items.Add(itemModeDemo);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemOpenSwitch);
            contextMenu.Items.Add(itemAutoStart);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(itemExit);

            // El estado de conexión se refresca justo antes de mostrar el menú
            contextMenu.Opening += (s, e) =>
            {
                deviceConnected = HerculesMidi.IsConnected();
                UpdateUI();
            };

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

        private void ListenForCommands()
        {
            while (running)
            {
                try
                {
                    int idx = WaitHandle.WaitAny(commandEvents, 400);
                    if (!running) break;
                    if (idx == 0) uiContext.Post(state => ToggleLights(), null);
                    else if (idx == 1) uiContext.Post(state => SetMode(AppMode.Off), null);
                    else if (idx == 2) uiContext.Post(state => SetMode(AppMode.Normal), null);
                    else if (idx >= 3 && idx < commandEvents.Length)
                    {
                        string color = HerculesShared.DemoColors[idx - 3];
                        uiContext.Post(state => SetDemoMode(color), null);
                    }
                }
                catch (ThreadAbortException) { break; }
                catch (ObjectDisposedException) { break; }
                catch { }
            }
        }

        private void MidiWorker()
        {
            foreach (Action action in midiQueue.GetConsumingEnumerable())
            {
                try { action(); }
                catch { }
            }
        }

        private void EnqueueMidi(Action action)
        {
            try { midiQueue.Add(action); }
            catch (InvalidOperationException) { } // la cola ya se cerró al salir
        }

        /// <summary>
        /// Guardián periódico: en modo Apagado mantiene las luces apagadas (y evita el
        /// Vegas Mode); al reconectar la controladora vuelve a aplicar el modo actual.
        /// </summary>
        private void QueueWatchdog()
        {
            if (!running) return;
            if (Interlocked.CompareExchange(ref watchdogPending, 1, 0) != 0) return;

            EnqueueMidi(() =>
            {
                try
                {
                    bool connected = HerculesMidi.IsConnected();
                    bool reconnected = connected && !deviceWasConnected;
                    deviceWasConnected = connected;
                    if (connected != deviceConnected)
                    {
                        deviceConnected = connected;
                        uiContext.Post(state => UpdateUI(), null);
                    }
                    if (!connected) return;

                    AppMode mode = currentMode;
                    if (mode == AppMode.Off) HerculesMidi.ClearAllLights(reconnected);
                    else if (mode == AppMode.Demo && reconnected) HerculesMidi.SetStaticDemoLights(demoColor);
                }
                finally
                {
                    Interlocked.Exchange(ref watchdogPending, 0);
                }
            });
        }

        public void ToggleLights()
        {
            SetMode(currentMode == AppMode.Off ? AppMode.Normal : AppMode.Off);
        }

        public void SetMode(AppMode mode)
        {
            AppMode previous = currentMode;
            currentMode = mode;
            ApplyMode(mode, previous, true);
            HerculesShared.SaveState(currentMode, demoColor);
            UpdateUI();
        }

        public void SetDemoMode(string color)
        {
            if (!HerculesShared.IsValidDemoColor(color)) return;
            demoColor = color;
            SetMode(AppMode.Demo);
        }

        private void ApplyMode(AppMode mode, AppMode previous, bool userInitiated)
        {
            string color = demoColor;
            EnqueueMidi(() =>
            {
                bool ok = true;
                if (mode == AppMode.Off)
                {
                    ok = HerculesMidi.ClearAllLights();
                }
                else if (mode == AppMode.Normal)
                {
                    // Si venía del modo lámpara, borra esas luces antes de devolverle el control al programa
                    if (previous == AppMode.Demo) HerculesMidi.ClearAllLights();
                    if (userInitiated)
                    {
                        Thread.Sleep(80);
                        HerculesMidi.TriggerTraktorRefresh();
                    }
                }
                else if (mode == AppMode.Demo)
                {
                    ok = HerculesMidi.SetStaticDemoLights(color);
                }

                deviceWasConnected = HerculesMidi.IsConnected();

                if (!ok && userInitiated)
                {
                    string error = HerculesMidi.LastError;
                    uiContext.Post(state => ShowWarning(error), null);
                }
            });
        }

        private void ShowWarning(string message)
        {
            if (notifyIcon == null || string.IsNullOrEmpty(message)) return;
            try { notifyIcon.ShowBalloonTip(4000, "Hercules P32", message, ToolTipIcon.Warning); }
            catch { }
        }

        private void UpdateUI()
        {
            try
            {
                AppMode mode = currentMode;
                string color = demoColor;

                string status = mode == AppMode.Demo
                    ? "Modo lámpara (" + HerculesUi.ColorName(color).ToLower() + ")"
                    : HerculesUi.ModeTitle(mode);

                notifyIcon.Icon = mode == AppMode.Off ? iconOff : iconOn;
                string tip = "Hercules P32: " + status;
                if (!deviceConnected) tip += " · sin controladora";
                // NotifyIcon.Text admite como máximo 63 caracteres
                notifyIcon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

                itemHeader.Text = status;
                Image oldHeaderImage = itemHeader.Image;
                itemHeader.Image = mode == AppMode.Demo
                    ? HerculesUi.CreateSwatchBitmap(contextMenu.ImageScalingSize.Width, color)
                    : HerculesUi.CreateDotBitmap(contextMenu.ImageScalingSize.Width, HerculesUi.ModeColor(mode, color));
                if (oldHeaderImage != null) oldHeaderImage.Dispose();
                itemDevice.Text = deviceConnected ? "Controladora conectada" : "Controladora no detectada";

                itemToggle.Text = mode == AppMode.Off ? "Encender luces" : "Apagar luces";

                itemModeNormal.Checked = mode == AppMode.Normal;
                itemModeOff.Checked = mode == AppMode.Off;
                itemModeDemo.Checked = mode == AppMode.Demo;
                for (int i = 0; i < itemColors.Length; i++)
                {
                    itemColors[i].Checked = mode == AppMode.Demo && HerculesShared.DemoColors[i] == color;
                }
            }
            catch { }
        }

        private void OpenSwitch()
        {
            string exe = Path.Combine(HerculesShared.AppDir, "HerculesToggle.exe");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                {
                    WorkingDirectory = HerculesShared.AppDir,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                ShowWarning("No se pudo abrir el interruptor: " + ex.Message);
            }
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
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RUN_KEY))
                {
                    if (current)
                    {
                        key.DeleteValue(APP_NAME, false);
                    }
                    else
                    {
                        key.SetValue(APP_NAME, "\"" + Application.ExecutablePath + "\"");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowWarning("No se pudo cambiar el inicio con Windows: " + ex.Message);
            }
            itemAutoStart.Checked = IsAutoStartEnabled();
        }

        private void ExitApp()
        {
            running = false;

            if (watchdogTimer != null) watchdogTimer.Dispose();
            midiQueue.CompleteAdding();
            // Deja terminar el último envío MIDI pendiente (con un tope)
            if (midiThread != null) midiThread.Join(2000);

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            if (contextMenu != null) contextMenu.Dispose();
            if (iconOn != null && iconOn != SystemIcons.Application) iconOn.Dispose();
            if (iconOff != null && iconOff != SystemIcons.Application) iconOff.Dispose();

            ExitThread();
        }

        /// <summary>Interpreta /off, /normal (/on) y /lamp:&lt;color&gt;.</summary>
        private static AppMode? ParseModeArg(string[] args, out string color)
        {
            color = "dj";
            if (args.Length == 0) return null;
            string a = args[0].ToLower();
            if (a == "/off") return AppMode.Off;
            if (a == "/normal" || a == "/on") return AppMode.Normal;
            if (a.StartsWith("/lamp"))
            {
                int colon = a.IndexOf(':');
                if (colon > 0 && HerculesShared.IsValidDemoColor(a.Substring(colon + 1))) color = a.Substring(colon + 1);
                return AppMode.Demo;
            }
            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, HerculesShared.TrayMutexName, out createdNew);

            string cmd = args.Length > 0 ? args[0].ToLower() : "";
            string forcedColor;
            AppMode? forcedMode = ParseModeArg(args, out forcedColor);

            if (!createdNew)
            {
                // Ya hay una bandeja corriendo: le reenvía la orden
                if (cmd == "/toggle") HerculesShared.SignalTray(HerculesShared.EventToggle);
                else if (forcedMode.HasValue)
                    HerculesShared.SignalTray(HerculesShared.CommandEventName(forcedMode.Value, forcedColor));
                return;
            }

            // Menú nítido en pantallas con escalado
            try { SetProcessDPIAware(); }
            catch { }

            if (cmd == "/toggle")
            {
                AppMode saved;
                string color;
                HerculesShared.LoadState(out saved, out color);
                forcedMode = saved == AppMode.Off ? AppMode.Normal : AppMode.Off;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HerculesTrayApp(forcedMode, forcedColor));
            GC.KeepAlive(singleInstanceMutex);
        }
    }
}
