# Especificación MIDI - Hercules DJControl P32 DJ

Documento de referencia técnica sobre los mensajes MIDI para el control de luces (LEDs), pantallas de 7 segmentos y botones de la **Hercules DJControl P32 DJ**.

---

## 1. Identificación del Dispositivo en Windows
- **Nombre MIDI Out:** `Hercules P32 DJ`
- **Protocolo:** MIDI estándar USB Class-Compliant a través de la API WinMM (`winmm.dll`).

---

## 2. Canales MIDI
| Canal MIDI | Hex Status | Función |
| :--- | :--- | :--- |
| **Canal 1 (Index 0)** | `0x90` / `0xB0` | Sección Central / Master / Global |
| **Canal 2 (Index 1)** | `0x91` / `0xB1` | Deck 1 (Izquierdo, Decks 1/3) |
| **Canal 3 (Index 2)** | `0x92` / `0xB2` | Deck 2 (Derecho, Decks 2/4) |
| **Canal 4 (Index 3)** | `0x93` / `0xB3` | Deck 3 (Shift Izquierdo) |
| **Canal 5 (Index 4)** | `0x94` / `0xB4` | Deck 4 (Shift Derecho) |
| **Canal 6 (Index 5)** | `0x95` / `0xB5` | Capas adicionales de Sampler |

---

## 3. Botones y Controles Principales (Note-On: 127 = On, 0 = Off)

### Sección Central (Canal 1 / `0x90`)
- `0x01`: Botón de codificador Browse (Explorador)
- `0x02`: Botón Record (Grabación)
- `0x03`: Botón Slip

### Decks 1 y 2 (Canales 2 y 3 / `0x91` y `0x92`)
- **Transporte:**
  - `0x08`: Botón Sync
  - `0x09`: Botón Cue
  - `0x0A`: Botón Play / Pause
  - `0x0F`: Botón Load Track
  - `0x10`: Botón PFL (Auriculares / Cue)
- **Efectos (FX Rack):**
  - `0x03`: FX 1
  - `0x04`: FX 2
  - `0x05`: FX 3
  - `0x06`: FX Focus / Macro
- **Modos de Pads:**
  - `0x14`: Modo Hotcue
  - `0x15`: Modo Loop
  - `0x16`: Modo Slicer
  - `0x17`: Modo Sampler

---

## 4. Pantalla de 7 Segmentos (Loop Size / Beatjump)
- **Tipo de mensaje:** Control Change (CC)
- **Controlador:** `0x1B` en Canales 2 y 3 (`0xB1` y `0xB2`).
- **Valores:**
  - `0`: Pantalla apagada.
  - `1` a `13`: Muestra tamaño del loop / beatjump (números enteros o fracciones con punto).
  - `14`: Muestra dos puntos (`..`).

---

## 5. Matriz de 32 Pads (16 Pads por Deck)
Los pads están organizados en una matriz de 4 filas x 4 columnas numeradas de arriba a abajo.
El hardware mapea internamente 4 capas por deck:

| Capa | Rango de Notas MIDI | Nombre en el Controlador |
| :--- | :--- | :--- |
| **Capa 0** | `0x24` a `0x33` (36 - 51) | Sampler |
| **Capa 1** | `0x34` a `0x43` (52 - 67) | Slicer |
| **Capa 2** | `0x44` a `0x53` (68 - 83) | Loop |
| **Capa 3** | `0x54` a `0x63` (84 - 99) | Hotcue |

### Colores de los Pads (Velocidad MIDI)
- `0`: **Apagado (Off)**
- `125`: **Rojo (Red)**
- `126`: **Azul (Blue)**
- `127`: **Púrpura / Violeta (Red + Blue)**

---

## 6. Comportamiento del Firmware (Vegas Mode / LightShow)
- Tras 5 minutos de inactividad en la conexión USB, el firmware del controlador inicia automáticamente el **Vegas Mode** (LightShow animado).
- Para evitar que este modo se active cuando las luces están en modo "Apagado", la aplicación envía una señal periódica de apagado que mantiene el hardware en reposo total sin interferir con las aplicaciones de música.
