import ctypes
from ctypes import wintypes
import time

winmm = ctypes.windll.winmm

class MIDIOUTCAPSW(ctypes.Structure):
    _fields_ = [
        ('wMid', wintypes.WORD),
        ('wPid', wintypes.WORD),
        ('vDriverVersion', wintypes.DWORD),
        ('szPname', wintypes.WCHAR * 32),
        ('wTechnology', wintypes.WORD),
        ('wVoices', wintypes.WORD),
        ('wNotes', wintypes.WORD),
        ('wChannelMask', wintypes.WORD),
        ('dwSupport', wintypes.DWORD),
    ]

def get_hercules_device_id():
    num_devs = winmm.midiOutGetNumDevs()
    caps = MIDIOUTCAPSW()
    for i in range(num_devs):
        winmm.midiOutGetDevCapsW(i, ctypes.byref(caps), ctypes.sizeof(caps))
        if "p32" in caps.szPname.lower() or "hercules" in caps.szPname.lower():
            return i, caps.szPname
    return None, None

def send_msg(hmidi, status, data1, data2):
    msg = status | (data1 << 8) | (data2 << 16)
    winmm.midiOutShortMsg(hmidi, msg)

def set_lights(on=True, color_mode="dj"):
    dev_id, name = get_hercules_device_id()
    if dev_id is None:
        print("Device not found")
        return False

    hmidi = wintypes.HANDLE()
    res = winmm.midiOutOpen(ctypes.byref(hmidi), dev_id, 0, 0, 0)
    if res != 0:
        print(f"Error opening device: {res}")
        return False

    try:
        if not on:
            # Turn everything off
            for ch in range(6):
                send_msg(hmidi, 0xB0 + ch, 0x1B, 0)
                for note in range(128):
                    send_msg(hmidi, 0x90 + ch, note, 0)
            print("All lights OFF")
            return True

        # Turn ON
        # Display 7-segment
        send_msg(hmidi, 0xB1, 0x1B, 8)
        send_msg(hmidi, 0xB2, 0x1B, 8)

        # Master / Center buttons (ch 0)
        for b in [0x01, 0x02, 0x03]:
            send_msg(hmidi, 0x90, b, 127)

        # Decks 1 & 2
        colors_palette = [125, 126, 127] # Red, Blue, Purple
        for ch in [1, 2]:
            # Buttons: Transport, FX, Modes, Headphone, Load
            for b in [0x01, 0x03, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0F, 0x10, 0x14, 0x15, 0x16, 0x17]:
                send_msg(hmidi, 0x90 + ch, b, 127)

            # Pads across all 4 layers (0x24 to 0x63)
            for layer in range(4):
                for pad in range(16):
                    row = pad // 4
                    col = pad % 4
                    midi_row = 3 - row
                    note = 0x24 + 16 * layer + midi_row * 4 + col

                    if color_mode == "dj":
                        # Deck 1 mostly cyan/blue & purple, Deck 2 red & purple
                        c = colors_palette[(row + col + (ch - 1)) % 3]
                    elif color_mode == "purple":
                        c = 127
                    elif color_mode == "blue":
                        c = 126
                    elif color_mode == "red":
                        c = 125
                    else:
                        c = 127
                    send_msg(hmidi, 0x90 + ch, note, c)

        print(f"All lights ON (mode: {color_mode})")
        return True
    finally:
        winmm.midiOutClose(hmidi)

if __name__ == "__main__":
    import sys
    arg = sys.argv[1] if len(sys.argv) > 1 else "on"
    if arg == "off":
        set_lights(False)
    else:
        set_lights(True, "dj")
