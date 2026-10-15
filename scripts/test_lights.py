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

def turn_all(on=True):
    dev_id, name = get_hercules_device_id()
    if dev_id is None:
        print("Hercules P32 DJ not found!")
        return
    
    print(f"Opening {name} (ID: {dev_id})...")
    hmidi = wintypes.HANDLE()
    res = winmm.midiOutOpen(ctypes.byref(hmidi), dev_id, 0, 0, 0)
    if res != 0:
        print(f"Error opening MIDI device: {res}")
        return

    try:
        # Loop over channels 0 to 5
        # Buttons 1 to 0x7F (127)
        for ch in range(6):
            # For each channel, set loop display
            if on:
                send_msg(hmidi, 0xB0 + ch, 0x1B, 10) # 7-seg display
            else:
                send_msg(hmidi, 0xB0 + ch, 0x1B, 0)

            for btn in range(1, 128):
                if on:
                    # Alternating colors: 125 (red), 126 (blue), 127 (purple) or 127
                    val = 127
                    send_msg(hmidi, 0x90 + ch, btn, val)
                else:
                    send_msg(hmidi, 0x90 + ch, btn, 0)
                    
        print("Done! Set lights to:", "ON" if on else "OFF")
    finally:
        winmm.midiOutClose(hmidi)

if __name__ == "__main__":
    print("Testing ON...")
    turn_all(True)
    time.sleep(2)
    print("Testing OFF...")
    turn_all(False)
    time.sleep(1)
    print("Testing ON again...")
    turn_all(True)
