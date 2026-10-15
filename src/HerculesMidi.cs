using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HerculesP32
{
    public static class HerculesMidi
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern uint midiOutGetNumDevs();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MIDIOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public ushort wTechnology;
            public ushort wVoices;
            public ushort wNotes;
            public ushort wChannelMask;
            public uint dwSupport;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern uint midiOutGetDevCaps(IntPtr uDeviceID, ref MIDIOUTCAPS lpMidiOutCaps, uint cbMidiOutCaps);

        [DllImport("winmm.dll")]
        private static extern uint midiOutOpen(out IntPtr lphmo, uint uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

        [DllImport("winmm.dll")]
        private static extern uint midiOutShortMsg(IntPtr hmo, uint dwMsg);

        [DllImport("winmm.dll")]
        private static extern uint midiOutClose(IntPtr hmo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const byte VK_F12 = 0x7B;
        private const byte SCAN_F12 = 0x58;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint MMSYSERR_ALLOCATED = 4;

        // Rango de notas con LED según el mapeo oficial de Mixxx (P32.shutdown)
        private const int FIRST_LED_NOTE = 0x01;
        private const int LAST_LED_NOTE = 0x63;
        private const int CC_LOOP_DISPLAY = 0x1B;

        private static readonly object midiLock = new object();

        /// <summary>Descripción del último error MIDI, para mostrar al usuario.</summary>
        public static string LastError { get; private set; }

        public static int FindDevice()
        {
            uint count = midiOutGetNumDevs();
            MIDIOUTCAPS caps = new MIDIOUTCAPS();
            uint capsSize = (uint)Marshal.SizeOf(typeof(MIDIOUTCAPS));

            for (uint i = 0; i < count; i++)
            {
                if (midiOutGetDevCaps((IntPtr)i, ref caps, capsSize) == 0)
                {
                    string name = caps.szPname.ToLower();
                    if (name.Contains("p32") || name.Contains("hercules"))
                    {
                        return (int)i;
                    }
                }
            }
            return -1;
        }

        public static bool IsConnected()
        {
            return FindDevice() >= 0;
        }

        /// <summary>
        /// Abre el puerto, ejecuta los envíos y lo cierra enseguida para no bloquearlo
        /// al software de DJ. Devuelve false (y deja LastError) si algo falla.
        /// </summary>
        private static bool WithDevice(Func<IntPtr, bool> send)
        {
            lock (midiLock)
            {
                int devId = FindDevice();
                if (devId < 0)
                {
                    LastError = "No se encontró la controladora Hercules P32 DJ. ¿Está conectada?";
                    return false;
                }

                IntPtr hmo;
                uint res = midiOutOpen(out hmo, (uint)devId, IntPtr.Zero, IntPtr.Zero, 0);
                if (res != 0)
                {
                    LastError = res == MMSYSERR_ALLOCATED
                        ? "El puerto MIDI de la controladora está ocupado por otro programa."
                        : "No se pudo abrir el puerto MIDI (error " + res + ").";
                    return false;
                }

                try
                {
                    if (!send(hmo))
                    {
                        LastError = "Falló el envío de mensajes MIDI a la controladora.";
                        return false;
                    }
                    LastError = null;
                    return true;
                }
                finally
                {
                    midiOutClose(hmo);
                }
            }
        }

        private static bool SendShortMsg(IntPtr hmo, int status, int data1, int data2)
        {
            uint msg = (uint)(status | (data1 << 8) | (data2 << 16));
            return midiOutShortMsg(hmo, msg) == 0;
        }

        /// <summary>
        /// Apaga los LEDs. Con allChannels=false solo barre los canales que usa el
        /// software de DJ (central y decks 1/2), suficiente para el guardián periódico.
        /// </summary>
        public static bool ClearAllLights(bool allChannels = true)
        {
            int lastChannel = allChannels ? 5 : 2;
            return WithDevice(hmo =>
            {
                for (int ch = 0; ch <= lastChannel; ch++)
                {
                    if (!SendShortMsg(hmo, 0xB0 + ch, CC_LOOP_DISPLAY, 0)) return false;
                    for (int note = FIRST_LED_NOTE; note <= LAST_LED_NOTE; note++)
                    {
                        if (!SendShortMsg(hmo, 0x90 + ch, note, 0)) return false;
                    }
                }
                return true;
            });
        }

        /// <summary>
        /// Pide a Traktor Pro 4 que reenvíe el estado de las luces (Send Monitor State,
        /// asignado a F12). Solo envía la tecla a Traktor: si está en primer plano se
        /// simula la pulsación, si no se le postea el mensaje a su ventana.
        /// Devuelve false si Traktor no está abierto.
        /// </summary>
        public static bool TriggerTraktorRefresh()
        {
            bool found = false;
            uint foregroundPid = 0;
            IntPtr foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero) GetWindowThreadProcessId(foreground, out foregroundPid);

            foreach (var proc in Process.GetProcesses())
            {
                using (proc)
                {
                    try
                    {
                        if (!proc.ProcessName.ToLower().Contains("traktor")) continue;
                        IntPtr hwnd = proc.MainWindowHandle;
                        if (hwnd == IntPtr.Zero) continue;
                        found = true;

                        if (proc.Id == foregroundPid)
                        {
                            keybd_event(VK_F12, SCAN_F12, 0, UIntPtr.Zero);
                            Thread.Sleep(40);
                            keybd_event(VK_F12, SCAN_F12, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                        else
                        {
                            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_F12, IntPtr.Zero);
                            Thread.Sleep(30);
                            PostMessage(hwnd, WM_KEYUP, (IntPtr)VK_F12, IntPtr.Zero);
                        }
                    }
                    catch { }
                }
            }
            return found;
        }

        public static bool SetStaticDemoLights(string colorMode = "dj")
        {
            return WithDevice(hmo =>
            {
                bool ok = true;
                ok &= SendShortMsg(hmo, 0xB1, CC_LOOP_DISPLAY, 8);
                ok &= SendShortMsg(hmo, 0xB2, CC_LOOP_DISPLAY, 8);

                ok &= SendShortMsg(hmo, 0x90, 0x01, 127);
                ok &= SendShortMsg(hmo, 0x90, 0x02, 127);
                ok &= SendShortMsg(hmo, 0x90, 0x03, 127);
                if (!ok) return false;

                int[] palette = new int[] { 125, 126, 127 };
                int[] buttons = new int[] { 0x01, 0x03, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0F, 0x10, 0x14, 0x15, 0x16, 0x17 };

                for (int ch = 1; ch <= 2; ch++)
                {
                    foreach (int b in buttons)
                    {
                        if (!SendShortMsg(hmo, 0x90 + ch, b, 127)) return false;
                    }

                    for (int layer = 0; layer < 4; layer++)
                    {
                        for (int pad = 0; pad < 16; pad++)
                        {
                            int row = pad / 4;
                            int col = pad % 4;
                            int midiRow = 3 - row;
                            int note = 0x24 + 16 * layer + midiRow * 4 + col;

                            int color = 127;
                            if (colorMode == "dj") color = palette[(row + col + (ch - 1)) % 3];
                            else if (colorMode == "purple") color = 127;
                            else if (colorMode == "blue") color = 126;
                            else if (colorMode == "red") color = 125;

                            if (!SendShortMsg(hmo, 0x90 + ch, note, color)) return false;
                        }
                    }
                }
                return true;
            });
        }
    }
}
