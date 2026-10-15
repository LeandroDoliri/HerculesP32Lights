# Hercules P32 DJ - Controlador de Luces para Barra de Tareas y Bandeja del Sistema 🎛️💡

Aplicación nativa para Windows (C# .NET) diseñada para encender, apagar y sincronizar las luces y pads del controlador MIDI **Hercules DJControl P32 DJ** desde la barra de tareas de Windows, respetando y restaurando el estado dinámico en programas de DJ como **Traktor Pro 4** y **DJUCED**.

---

## 📁 Estructura del Proyecto

```text
HerculesP32Lights\
│
├── HerculesTray.exe            # (generado) App residente en la bandeja del sistema
├── HerculesToggle.exe          # (generado) Ventana del interruptor visual (anclable)
├── state.ini                   # (generado) Modo actual; solo lo escribe la bandeja
├── build.cmd                   # Doble clic para recompilar el proyecto
├── README.md                   # Esta documentación
│
├── src\                        # Código fuente C# (.NET 4.8 / csc.exe)
│   ├── HerculesMidi.cs         # Comunicación WinMM MIDI y refresco F12 de Traktor
│   ├── HerculesShared.cs       # Estado (state.ini) y órdenes entre interruptor y bandeja
│   ├── HerculesTray.cs         # Bandeja, menú contextual y guardián de blackout
│   ├── HerculesToggle.cs       # Ventana del interruptor (selector de modo y colores)
│   └── HerculesUi.cs           # Paleta, textos y dibujos compartidos de la interfaz
│
├── assets\                     # Iconos (se embeben en los .exe al compilar)
│   ├── app.ico                 # Icono principal multi-resolución
│   ├── icon_on.ico             # Icono encendido (colores neón)
│   ├── icon_off.ico            # Icono apagado (escala de grises)
│   └── generate_icons.py       # Script en Python (Pillow) para generar los iconos ICO
│
├── docs\                       # Documentación técnica y especificaciones
│   ├── midi_spec.md            # Mapeo completo de canales, notas, pads y colores
│   ├── Hercules-P32-scripts.js # Mapeo de referencia oficial de Mixxx (JavaScript)
│   └── Hercules-P32-DJ.midi.xml# Mapeo XML de referencia de Mixxx
│
└── scripts\                    # Scripts de utilidad y herramientas de diagnóstico
    ├── build.ps1               # Script de compilación usando csc.exe
    ├── install_shortcuts.ps1   # Generador de accesos directos (Escritorio y Menú Inicio)
    ├── hercules_midi.py        # Módulo común de acceso MIDI para los scripts
    ├── list_midi.py            # Diagnóstico: enumera dispositivos MIDI en Windows
    ├── test_lights.py          # Diagnóstico: prueba de ráfaga ON/OFF en hardware
    └── set_lights.py           # CLI: set_lights.py [on|off] [dj|purple|blue|red]
```

---

## ⚡ ¿Cómo funciona?

### 1. Dos Formas de Control

- **Bandeja del Sistema (`HerculesTray.exe`):**  
  Permanece activo junto al reloj. Con **un solo clic izquierdo** conmuta entre:
  - 🟢 **Programa de DJ (Normal):** El controlador queda libre para tu software de DJ.
  - 🌑 **Apagado:** Apaga todas las luces del controlador al instante.
  - Clic derecho: abre un menú que muestra el modo actual y si la controladora está conectada, con la acción principal (Encender / Apagar luces), los tres modos, los colores del modo lámpara, *Abrir interruptor…*, inicio con Windows y salida. El tooltip del ícono también avisa si no se detecta la controladora.

- **Interruptor Visual de Barra de Tareas (`HerculesToggle.exe`):**
  Una ventana oscura con el estado actual, un indicador de conexión, un selector de modo (**Programa / Apagado / Lámpara**) y los colores del modo lámpara. Se mantiene sincronizada con la bandeja: no toca el MIDI, le envía la orden a `HerculesTray.exe` y, si la bandeja no está abierta, la inicia.
  - Atajos: `1` Programa, `2` Apagado, `3` Lámpara, `Esc` cierra; con `Tab` y las flechas se recorren los controles.
  - Si ya está abierta, volver a ejecutarla la trae al frente.
  - Para anclarla: *clic derecho en su icono en la barra de tareas ➡️ "Anclar a la barra de tareas"*.

- **Línea de comandos:** `HerculesTray.exe /off`, `/normal`, `/toggle` o `/lamp:<color>` (`dj`, `purple`, `blue`, `red`). Si la bandeja ya está corriendo, le reenvía la orden.

---

### 2. Sincronización Total con Traktor Pro 4 (`F12`)

El problema habitual al apagar las luces de un controlador es que el software de DJ no sabe que el hardware se apagó, por lo que al volver a encenderlo no reenvía las luces de las pistas que ya estaban cargadas.

Este proyecto lo resuelve así:
1. Al pulsar **Apagar**: se apagan todos los LEDs y un guardián los vuelve a apagar cada 3,5 s. Eso evita que el hardware entre en su "Vegas Mode" tras 5 minutos de inactividad y tapa las luces que el programa de DJ siga enviando (por ejemplo, el parpadeo de Play puede verse un instante).
2. Al pulsar **Encender**:
   - Se desactiva el guardián de apagado (y se borran las luces del modo lámpara, si venías de ahí).
   - Si **Traktor Pro 4** está abierto, la aplicación le envía la tecla **`F12`** solo a Traktor (nunca a otra ventana).
   - Con `F12` asignado en Traktor a **`Send Monitor State`**, Traktor reenvía su estado (Play, Cue, Hotcues en sus colores, loops y vúmetros) y las luces vuelven a reflejar las pistas cargadas.

> **Requisito:** en Traktor, *Preferences ➡️ Controller Manager*, asigná `F12` al comando **Send Monitor State**. Con otros programas (DJUCED, Mixxx) las luces vuelven cuando el programa las reenvía.

Otros detalles:
- Si la controladora se desconecta y se vuelve a conectar, la bandeja reaplica el modo Apagado o Lámpara automáticamente.
- Si no encuentra la controladora o el puerto MIDI está ocupado, la bandeja muestra un aviso.
- Al salir de la bandeja estando en Apagado, el guardián deja de correr: a los 5 minutos el hardware puede volver al Vegas Mode. Al volver a abrirla se retoma el modo guardado.

---

## 🛠️ Cómo Recompilar el Proyecto

El proyecto no requiere Visual Studio ni gestores de paquetes pesados. Utiliza el compilador nativo de Windows `csc.exe` (.NET Framework 4.8):

1. Abre la carpeta del proyecto.
2. Haz doble clic en **`build.cmd`** (o ejecuta en PowerShell `scripts\build.ps1`).
3. Los ejecutables `HerculesTray.exe` y `HerculesToggle.exe` se generan en la raíz del proyecto, con los iconos embebidos. Si alguna de las dos apps está abierta, el script la cierra antes de compilar.

---

## 🚀 Reinstalar Accesos Directos

Si necesitas restaurar los accesos directos en el Escritorio o Menú Inicio:
Ejecuta en PowerShell:
```powershell
.\scripts\install_shortcuts.ps1
```
