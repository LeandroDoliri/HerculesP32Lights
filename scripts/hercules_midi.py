"""Acceso compartido a la Hercules P32 DJ por WinMM, usado por los scripts de diagnóstico."""
import ctypes
from contextlib import contextmanager
from ctypes import wintypes

winmm = ctypes.windll.winmm

# Rango de notas con LED según el mapeo oficial de Mixxx (P32.shutdown)
FIRST_LED_NOTE = 0x01
LAST_LED_NOTE = 0x63
CC_LOOP_DISPLAY = 0x1B

# Colores de los pads (velocidad MIDI)
PAD_RED = 125
PAD_BLUE = 126
PAD_PURPLE = 127


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


def list_devices():
    """Devuelve [(id, nombre, resultado)] de todas las salidas MIDI."""
    caps = MIDIOUTCAPSW()
    devices = []
    for i in range(winmm.midiOutGetNumDevs()):
        res = winmm.midiOutGetDevCapsW(i, ctypes.byref(caps), ctypes.sizeof(caps))
        devices.append((i, caps.szPname if res == 0 else "", res))
    return devices


def find_hercules():
    for i, name, res in list_devices():
        if res == 0 and ("p32" in name.lower() or "hercules" in name.lower()):
            return i, name
    return None, None


@contextmanager
def open_hercules():
    """Abre la salida MIDI de la P32 y la cierra al terminar. Lanza RuntimeError si falla."""
    dev_id, name = find_hercules()
    if dev_id is None:
        raise RuntimeError("Hercules P32 DJ no encontrada")
    hmidi = wintypes.HANDLE()
    res = winmm.midiOutOpen(ctypes.byref(hmidi), dev_id, 0, 0, 0)
    if res != 0:
        raise RuntimeError(f"Error abriendo el dispositivo MIDI: {res}")
    try:
        yield hmidi, name
    finally:
        winmm.midiOutClose(hmidi)


def send_msg(hmidi, status, data1, data2):
    msg = status | (data1 << 8) | (data2 << 16)
    res = winmm.midiOutShortMsg(hmidi, msg)
    if res != 0:
        raise RuntimeError(f"Error enviando mensaje MIDI: {res}")


def clear_all(hmidi):
    for ch in range(6):
        send_msg(hmidi, 0xB0 + ch, CC_LOOP_DISPLAY, 0)
        for note in range(FIRST_LED_NOTE, LAST_LED_NOTE + 1):
            send_msg(hmidi, 0x90 + ch, note, 0)
