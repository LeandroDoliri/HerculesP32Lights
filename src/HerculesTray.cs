using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HerculesP32
{
    public class HerculesTrayApp : ApplicationContext
    {
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

        // Se leen desde el hilo MIDI, por eso son volatile
        private volatile AppMode currentMode = AppMode.Normal;
        private volatile string demoColor = "dj";

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

        public HerculesTrayApp(AppMode? forcedMode)
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

            commandEvents = new EventWaitHandle[]
            {
                new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventToggle),
                new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventOff),
                new EventWaitHandle(false, EventResetMode.AutoReset, HerculesShared.EventNormal),
            };

            listenerThread = new Thread(ListenForCommands);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            InitializeTray();

            if (forcedMode.HasValue)
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

        private static string ColorDisplayName(string color)
        {
            switch (color)
            {
                case "purple": return "Púrpura";
                case "blue": return "Azul";
                case "red": return "Rojo";
                default: return "DJ";
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (currentMode == AppMode.Normal)
                {
                    notifyIcon.Icon = iconOn;
                    notifyIcon.Text = "Hercules P32: Luces NORMALES (Programa)";
                }
                else if (currentMode == AppMode.Off)
                {
                    notifyIcon.Icon = iconOff;
                    notifyIcon.Text = "Hercules P32: Luces APAGADAS";
                }
                else
                {
                    notifyIcon.Icon = iconOn;
                    notifyIcon.Text = "Hercules P32: Modo Lámpara (" + ColorDisplayName(demoColor) + ")";
                }

                itemModeNormal.Checked = currentMode == AppMode.Normal;
                itemModeOff.Checked = currentMode == AppMode.Off;
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

        private static AppMode? ParseModeArg(string[] args)
        {
            if (args.Length == 0) return null;
            string a = args[0].ToLower();
            if (a == "/off") return AppMode.Off;
            if (a == "/normal" || a == "/on") return AppMode.Normal;
            return null;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, HerculesShared.TrayMutexName, out createdNew);

            string cmd = args.Length > 0 ? args[0].ToLower() : "";
            AppMode? forcedMode = ParseModeArg(args);

            if (!createdNew)
            {
                // Ya hay una bandeja corriendo: le reenvía la orden
                if (cmd == "/toggle") HerculesShared.SignalTray(HerculesShared.EventToggle);
                else if (forcedMode == AppMode.Off) HerculesShared.SignalTray(HerculesShared.EventOff);
                else if (forcedMode == AppMode.Normal) HerculesShared.SignalTray(HerculesShared.EventNormal);
                return;
            }

            if (cmd == "/toggle")
            {
                AppMode saved;
                string color;
                HerculesShared.LoadState(out saved, out color);
                forcedMode = saved == AppMode.Off ? AppMode.Normal : AppMode.Off;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HerculesTrayApp(forcedMode));
            GC.KeepAlive(singleInstanceMutex);
        }
    }
}
