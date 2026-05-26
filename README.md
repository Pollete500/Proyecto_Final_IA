# Proyecto Final IA — Kart AI Racing

Juego de karting con modelos de IA desarrollado en Unity 6 (URP).

---

## Rama: `menu-and-cuentaatras`

### Nuevos scripts

| Archivo | Descripción |
|---|---|
| `Assets/Scripts/UI/MainMenuUI.cs` | Menú principal generado por código: título 3D con prefabs de letras Synty, botones JUGAR / CONFIGURACION / SALIR, panel de configuración (volumen, vueltas, calidad gráfica) |
| `Assets/Scripts/UI/RaceUI.cs` | HUD de carrera: cuenta atrás animada, vuelta/tiempo/posición, pausa con ESC, modo espectador con Tab |
| `Assets/Scripts/UI/ResultsScreen.cs` | Pantalla de clasificación final tras la carrera |
| `Assets/Scripts/UI/UIIconRenderer.cs` | Utilidad estática que renderiza prefabs 3D a `RenderTexture` para usarlos como iconos en la UI |

### Mejoras en scripts existentes

**`KartController.cs`**
- `SetControlEnabled(bool, bool freezePhysics = true)` — al terminar la carrera el kart frena de forma natural por arrastre en lugar de detenerse de golpe

**`RaceManager.cs`**
- Cuando el jugador cruza la meta, los bots siguen corriendo hasta agotar el tiempo o terminar; el jugador pasa a modo espectador

### Funcionalidades de UI implementadas

**Menú principal**
- Título "KART AI RACING" con prefabs de letras 3D (Synty PolygonIcons), con fallback a texto amarillo en builds sin editor
- Panel de configuración: volumen, número de vueltas (1/3/5), calidad gráfica (Baja/Media/Alta) con persistencia en `PlayerPrefs`
- Icono de engranaje junto al título del panel

**HUD de carrera**
- Icono de reloj 3D junto al temporizador (solo visible durante la carrera)
- Cuenta atrás 3 → 2 → 1 → GO! con animación de escala y fade
- Vuelta actual, tiempo de carrera y posición en tiempo real
- Pausa con **ESC**: overlay oscuro con icono de pausa centrado (sin botones, ESC para reanudar)

**Modo espectador** (tras cruzar la meta)
- La cámara sigue automáticamente al bot mejor posicionado que aún no ha terminado
- **Tab** para cambiar de objetivo; el HUD muestra la vuelta y posición del bot observado
- Indicador `ESPECTANDO: [nombre] [2/5] Tab→siguiente`

**Pantalla de resultados**
- Aparece 2.5 s después de que termine la carrera
- Clasificación con posición, nombre y tiempo; `DNF` para los que no terminaron
- Icono de trofeo 3D, botones VOLVER A JUGAR y MENU PRINCIPAL
- El cursor se desbloquea automáticamente al mostrar la pantalla

### Nuevos assets

- **`Assets/Prefabs/Karts/`** — 15 modelos de karts (Modular Cyber Racing Cars · ithappy)
- **`Assets/Prefabs/PowerUps/`** — prefabs de power-ups (Synty PolygonIcons)
- **`Assets/Prefabs/Track/`** — props de circuito: barreras, conos, líneas de meta, trampolines, flechas
- **`Assets/Prefabs/UI/`** — iconos 3D para UI: reloj, engranaje, pausa, trofeo, letras A–Z

### Nuevas escenas

| Escena | Descripción |
|---|---|
| `Assets/Scenes/MainMenu.unity` | Escena de menú principal |
| `Assets/Scenes/XJJ TrainingScene.unity` | Escena de entrenamiento IA (XJJ) |
| `Assets/Scenes/Demonstration.unity` | Demo del asset de coches (ithappy) |

---

## Configuración en Inspector (escena de carrera)

Para que los iconos 3D funcionen hay que asignar en el Inspector del GameObject que tenga **RaceUI**:
- `Clock Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Clock_01.prefab`
- `Pause Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Pause_01.prefab`

En el GameObject con **MainMenuUI**:
- `Gear Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Gear_01.prefab`

En el GameObject con **ResultsScreen**:
- `Trophy Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Trophy_01.prefab`

---

## Assets de terceros utilizados

- **POLYGON - Icons Pack** (Synty Studios) — iconos 3D y letras para la UI
- **Modular Cyber Racing Cars** (ithappy) — modelos de karts low-poly
