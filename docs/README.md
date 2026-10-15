# Hercules P32 DJ - Controlador de Luces para Barra de Tareas y Bandeja del Sistema 🎛️💡

Aplicación nativa para Windows (C# .NET) diseñada para encender, apagar y sincronizar las luces y pads del controlador MIDI **Hercules DJControl P32 DJ** desde la barra de tareas de Windows, respetando y restaurando el estado dinámico en programas de DJ como **Traktor Pro 4** y **DJUCED**.

---

## 📁 Estructura del Proyecto

```text
C:\Users\leand\HerculesP32Lights\
│
├── HerculesTray.exe            # Aplicación residente en la bandeja del sistema (junto al reloj)
├── HerculesToggle.exe          # Ventana del interruptor visual (anclable a la barra de tareas)
├── app.ico                     # Icono de la aplicación
├── icon_on.ico                 # Icono de estado encendido
├── icon_off.ico                # Icono de estado apagado
├── state.ini                   # Archivo de persistencia de estado actual
├── build.cmd                   # Script de doble clic para recompilar el proyecto
├── README.md                   # Esta documentación
│
├── src\                        # Código fuente C# (.NET 4.8 / csc.exe)
│   ├── HerculesMidi.cs         # Comunicación WinMM MIDI y disparo de F12 a Traktor
│   ├── HerculesTray.cs         # Servicio de bandeja, menú contextual y guardián de blackout
│   └── HerculesToggle.cs       # Formulario del interruptor visual con botón ON/OFF
│
├── assets\                     # Recursos gráficos y generador
│   ├── app.ico                 # Icono principal multi-resolución
│   ├── icon_on.ico             # Icono encendido (colores neón)
│   ├── icon_off.ico            # Icono apagado (escala de grises)
│   └── generate_icons.py       # Script en Python (Pillow) para generar los iconos ICO
│
├── docs\                       # Documentación técnica y especificaciones
│   ├── README.md               # Copia de la documentación general
│   ├── midi_spec.md            # Mapeo completo de canales, notas, pads y colores
│   ├── Hercules-P32-scripts.js # Mapeo de referencia oficial de Mixxx (JavaScript)
│   └── Hercules-P32-DJ.midi.xml# Mapeo XML de referencia de Mixxx
│
└── scripts\                    # Scripts de utilidad y herramientas de diagnóstico
    ├── build.ps1               # Script de compilación usando csc.exe
    ├── install_shortcuts.ps1   # Generador de accesos directos (Escritorio y Menú Inicio)
    ├── list_midi.py            # Diagnóstico: enumera dispositivos MIDI en Windows
    ├── test_lights.py          # Diagnóstico: prueba de ráfaga ON/OFF en hardware
    └── set_lights.py           # CLI: permite fijar estados o colores desde consola
```

---

## ⚡ ¿Cómo funciona?

### 1. Dos Formas de Control

- **Bandeja del Sistema (`HerculesTray.exe`):**  
  Permanece activo junto al reloj. Con **un solo clic izquierdo** conmuta entre:
  - 🟢 **Modo Normal:** El controlador queda libre para tu software de DJ.
  - 🌑 **Modo Apagado:** Apaga todas las luces del controlador al instante.
  - Clic derecho: Abre un menú con selector de modos, modo lámpara opcional, inicio con Windows y salida.

- **Interruptor Visual de Barra de Tareas (`HerculesToggle.exe`):**  
  Una ventana compacta con un botón grande interactivo que muestra el estado actual. Al estar abierta, su icono aparece en la barra de tareas y puede anclarse haciendo:  
  *Clic derecho en su icono en la barra de tareas ➡️ "Anclar a la barra de tareas"*.

---

### 2. Sincronización Total con Traktor Pro 4 (`F12`)

El problema habitual al apagar las luces de un controlador es que el software de DJ no sabe que el hardware se apagó, por lo que al volver a encenderlo no reenvía las luces de las pistas que ya estaban cargadas.

Este proyecto lo resuelve con integración nativa:
1. Al pulsar **Apagar**: Se envía un Note-Off masivo y se activa un temporizador silencioso que evita que el hardware entre en su molesto "Vegas Mode" tras 5 minutos de inactividad.
2. Al pulsar **Encender**:
   - Se desactiva el guardián de apagado.
   - La aplicación envía en segundo plano la pulsación de la tecla **`F12`** directamente a **Traktor Pro 4**.
   - Traktor ejecuta su comando nativo **`Send Monitor State`** y reenvía inmediatamente toda su memoria (Play, Cue, Hotcues en sus colores reales, loops y vúmetros).  
   **Las luces regresan exactamente a como estaban antes de apagarlas.**

---

## 🛠️ Cómo Recompilar el Proyecto

El proyecto no requiere Visual Studio ni gestores de paquetes pesados. Utiliza el compilador nativo de Windows `csc.exe` (.NET Framework 4.8):

1. Abre la carpeta `C:\Users\leand\HerculesP32Lights`.
2. Haz doble clic en **`build.cmd`** (o ejecuta en PowerShell `scripts\build.ps1`).
3. Los ejecutables `HerculesTray.exe` y `HerculesToggle.exe` se compilarán en menos de 2 segundos.

---

## 🚀 Reinstalar Accesos Directos

Si necesitas restaurar los accesos directos en el Escritorio o Menú Inicio:
Ejecuta en PowerShell:
```powershell
& "C:\Users\leand\HerculesP32Lights\scripts\install_shortcuts.ps1"
```
