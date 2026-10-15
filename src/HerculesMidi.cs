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

        private const byte VK_F12 = 0x7B;
        private const byte SCAN_F12 = 0x58;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

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

        private static void SendShortMsg(IntPtr hmo, int status, int data1, int data2)
        {
            uint msg = (uint)(status | (data1 << 8) | (data2 << 16));
            midiOutShortMsg(hmo, msg);
        }

        public static bool ClearAllLights()
        {
            int devId = FindDevice();
            if (devId < 0) return false;

            IntPtr hmo;
            if (midiOutOpen(out hmo, (uint)devId, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                return false;

            try
            {
                for (int ch = 0; ch <= 5; ch++)
                {
                    SendShortMsg(hmo, 0xB0 + ch, 0x1B, 0);
                    for (int note = 0; note <= 127; note++)
                    {
                        SendShortMsg(hmo, 0x90 + ch, note, 0);
                    }
                }
                return true;
            }
            finally
            {
                midiOutClose(hmo);
            }
        }

        /// <summary>
        /// Dispara el refresco automático de luces en Traktor Pro 4 (Send Monitor State)
        /// </summary>
        public static void TriggerTraktorRefresh()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    string name = proc.ProcessName.ToLower();
                    if (name.Contains("traktor"))
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            PostMessage(proc.MainWindowHandle, WM_KEYDOWN, (IntPtr)VK_F12, IntPtr.Zero);
                            Thread.Sleep(30);
                            PostMessage(proc.MainWindowHandle, WM_KEYUP, (IntPtr)VK_F12, IntPtr.Zero);
                        }
                    }
                }
            }
            catch { }

            try
            {
                keybd_event(VK_F12, SCAN_F12, 0, UIntPtr.Zero);
                Thread.Sleep(40);
                keybd_event(VK_F12, SCAN_F12, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        public static bool SetStaticDemoLights(string colorMode = "dj")
        {
            int devId = FindDevice();
            if (devId < 0) return false;

            IntPtr hmo;
            if (midiOutOpen(out hmo, (uint)devId, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                return false;

            try
            {
                SendShortMsg(hmo, 0xB1, 0x1B, 8);
                SendShortMsg(hmo, 0xB2, 0x1B, 8);

                SendShortMsg(hmo, 0x90, 0x01, 127);
                SendShortMsg(hmo, 0x90, 0x02, 127);
                SendShortMsg(hmo, 0x90, 0x03, 127);

                int[] palette = new int[] { 125, 126, 127 };
                int[] buttons = new int[] { 0x01, 0x03, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0F, 0x10, 0x14, 0x15, 0x16, 0x17 };

                for (int ch = 1; ch <= 2; ch++)
                {
                    foreach (int b in buttons)
                    {
                        SendShortMsg(hmo, 0x90 + ch, b, 127);
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

                            SendShortMsg(hmo, 0x90 + ch, note, color);
                        }
                    }
                }
                return true;
            }
            finally
            {
                midiOutClose(hmo);
            }
        }
    }
}
