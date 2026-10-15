import time

from hercules_midi import (CC_LOOP_DISPLAY, FIRST_LED_NOTE, LAST_LED_NOTE,
                           PAD_PURPLE, clear_all, open_hercules, send_msg)


def turn_all(on=True):
    try:
        with open_hercules() as (hmidi, name):
            print(f"Opening {name}...")
            if not on:
                clear_all(hmidi)
            else:
                for ch in range(6):
                    send_msg(hmidi, 0xB0 + ch, CC_LOOP_DISPLAY, 10)  # 7-seg display
                    for btn in range(FIRST_LED_NOTE, LAST_LED_NOTE + 1):
                        send_msg(hmidi, 0x90 + ch, btn, PAD_PURPLE)
            print("Done! Set lights to:", "ON" if on else "OFF")
    except RuntimeError as e:
        print(e)


if __name__ == "__main__":
    print("Testing ON...")
    turn_all(True)
    time.sleep(2)
    print("Testing OFF...")
    turn_all(False)
    time.sleep(1)
    print("Testing ON again...")
    turn_all(True)
