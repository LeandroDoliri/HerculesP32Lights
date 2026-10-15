"""Uso: python set_lights.py [on|off] [dj|purple|blue|red]"""
import sys

from hercules_midi import (CC_LOOP_DISPLAY, PAD_BLUE, PAD_PURPLE, PAD_RED,
                           clear_all, open_hercules, send_msg)

COLORS = {"purple": PAD_PURPLE, "blue": PAD_BLUE, "red": PAD_RED}


def set_lights(on=True, color_mode="dj"):
    try:
        with open_hercules() as (hmidi, _name):
            if not on:
                clear_all(hmidi)
                print("All lights OFF")
                return True

            # Display 7-segment
            send_msg(hmidi, 0xB1, CC_LOOP_DISPLAY, 8)
            send_msg(hmidi, 0xB2, CC_LOOP_DISPLAY, 8)

            # Master / Center buttons (ch 0)
            for b in [0x01, 0x02, 0x03]:
                send_msg(hmidi, 0x90, b, 127)

            # Decks 1 & 2
            colors_palette = [PAD_RED, PAD_BLUE, PAD_PURPLE]
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
                            # Diagonales alternando rojo, azul y púrpura, desfasadas entre decks
                            c = colors_palette[(row + col + (ch - 1)) % 3]
                        else:
                            c = COLORS.get(color_mode, PAD_PURPLE)
                        send_msg(hmidi, 0x90 + ch, note, c)

            print(f"All lights ON (mode: {color_mode})")
            return True
    except RuntimeError as e:
        print(e)
        return False


if __name__ == "__main__":
    state = sys.argv[1] if len(sys.argv) > 1 else "on"
    color = sys.argv[2] if len(sys.argv) > 2 else "dj"
    if color != "dj" and color not in COLORS:
        print(__doc__)
        sys.exit(2)
    ok = set_lights(state != "off", color)
    sys.exit(0 if ok else 1)
