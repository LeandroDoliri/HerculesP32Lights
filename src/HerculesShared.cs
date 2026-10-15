using System;
using System.IO;
using System.Threading;

namespace HerculesP32
{
    public enum AppMode
    {
        Normal,     // El programa de DJ controla las luces
        Off,        // Todas las luces apagadas (blackout)
        Demo        // Modo lámpara decorativo opcional
    }

    /// <summary>
    /// Estado persistido y comunicación entre HerculesToggle y HerculesTray.
    /// Solo la bandeja escribe state.ini; el interruptor le envía órdenes explícitas.
    /// </summary>
    public static class HerculesShared
    {
        public const string TrayMutexName = @"Local\HerculesP32TrayMutex";
        public const string ToggleMutexName = @"Local\HerculesP32ToggleMutex";
        public const string EventToggle = @"Local\HerculesP32Cmd_Toggle";
        public const string EventOff = @"Local\HerculesP32Cmd_Off";
        public const string EventNormal = @"Local\HerculesP32Cmd_Normal";

        public static readonly string[] DemoColors = new string[] { "dj", "purple", "blue", "red" };

        public static string LampEventName(string color)
        {
            return @"Local\HerculesP32Cmd_Lamp_" + color;
        }

        /// <summary>Evento con el que se le pide a la bandeja pasar a un modo.</summary>
        public static string CommandEventName(AppMode mode, string color)
        {
            if (mode == AppMode.Off) return EventOff;
            if (mode == AppMode.Demo) return LampEventName(color);
            return EventNormal;
        }

        /// <summary>Argumento de línea de comandos equivalente, para iniciar la bandeja con esa orden.</summary>
        public static string CommandArg(AppMode mode, string color)
        {
            if (mode == AppMode.Off) return "/off";
            if (mode == AppMode.Demo) return "/lamp:" + color;
            return "/normal";
        }

        public static bool IsTrayRunning()
        {
            try
            {
                Mutex m;
                if (Mutex.TryOpenExisting(TrayMutexName, out m))
                {
                    m.Dispose();
                    return true;
                }
            }
            catch (UnauthorizedAccessException) { return true; }
            catch { }
            return false;
        }

        public static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string StateFile
        {
            get { return Path.Combine(AppDir, "state.ini"); }
        }

        public static bool IsValidDemoColor(string color)
        {
            return Array.IndexOf(DemoColors, color) >= 0;
        }

        public static void LoadState(out AppMode mode, out string demoColor)
        {
            mode = AppMode.Normal;
            demoColor = "dj";
            try
            {
                if (!File.Exists(StateFile)) return;
                foreach (string line in File.ReadAllLines(StateFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "Mode")
                    {
                        if (v == "Off") mode = AppMode.Off;
                        else if (v == "Demo") mode = AppMode.Demo;
                        else mode = AppMode.Normal;
                    }
                    else if (k == "DemoColor" && IsValidDemoColor(v))
                    {
                        demoColor = v;
                    }
                }
            }
            catch { }
        }

        public static void SaveState(AppMode mode, string demoColor)
        {
            try
            {
                string content = string.Format("Mode={0}\r\nDemoColor={1}\r\n", mode, demoColor);
                string tmp = StateFile + ".tmp";
                File.WriteAllText(tmp, content);
                // Reemplazo atómico para que el interruptor nunca lea un archivo a medias
                if (File.Exists(StateFile)) File.Replace(tmp, StateFile, null);
                else File.Move(tmp, StateFile);
            }
            catch { }
        }

        /// <summary>Envía una orden a la bandeja. Devuelve false si no está corriendo.</summary>
        public static bool SignalTray(string eventName)
        {
            try
            {
                EventWaitHandle evt;
                if (EventWaitHandle.TryOpenExisting(eventName, out evt))
                {
                    using (evt) evt.Set();
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
