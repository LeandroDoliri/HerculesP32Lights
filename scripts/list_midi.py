import ctypes
from ctypes import wintypes

winmm = ctypes.windll.winmm
num_devs = winmm.midiOutGetNumDevs()
print(f"Midi Out Devs count: {num_devs}")

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

caps = MIDIOUTCAPSW()
for i in range(num_devs):
    res = winmm.midiOutGetDevCapsW(i, ctypes.byref(caps), ctypes.sizeof(caps))
    print(f"Device {i}: '{caps.szPname}' (result={res})")
