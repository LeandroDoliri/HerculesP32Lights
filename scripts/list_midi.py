from hercules_midi import list_devices

devices = list_devices()
print(f"Midi Out Devs count: {len(devices)}")
for i, name, res in devices:
    print(f"Device {i}: '{name}' (result={res})")
