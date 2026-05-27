# Kart AI Racing — Proyecto Final IA

Videojuego de carreras arcade desarrollado en Unity 6 (URP) como proyecto final del Máster de Inteligencia Artificial y Big Data.

El objetivo del proyecto es crear un prototipo funcional inspirado en juegos tipo kart racing, integrando sistemas de inteligencia artificial aplicados al comportamiento de bots, clasificación del jugador y balance dinámico mediante power-ups.

---

## Descripción del proyecto

Kart AI Racing es un prototipo 3D low-poly de carreras arcade en el que un jugador compite contra varios bots en un circuito cerrado.

El proyecto combina mecánicas de videojuego con sistemas de inteligencia artificial:

1. Bots capaces de seguir el circuito mediante checkpoints y una base preparada para aprendizaje por refuerzo con Unity ML-Agents.
2. Sistema de clasificación del comportamiento del jugador a partir de estadísticas de carrera.
3. Sistema de power-ups con asignación dinámica según la posición del corredor.

La prioridad del proyecto es entregar un MVP funcional, demostrable y bien documentado.

---

## Tecnologías utilizadas

- Unity 6
- C#
- Universal Render Pipeline (URP)
- Unity Input System
- Unity ML-Agents
- Rigidbody Physics
- Python para entrenamiento y scripts auxiliares
- GitHub para control de versiones

---

## Estructura del proyecto

```text
Assets/
├── Scenes/
├── Scripts/
│   ├── Core/
│   ├── Kart/
│   ├── UI/
│   ├── AI/
│   │   ├── Reinforcement/
│   │   ├── Classification/
│   │   └── PowerUps/
│   └── Editor/
├── MLAgents/
├── Data/
├── Python/
├── Prefabs/
│   ├── Karts/
│   ├── PowerUps/
│   ├── Track/
│   └── UI/
└── Documentation/
```

---

## Sistemas principales

### 1. Sistema de kart

El movimiento de los karts está basado en Rigidbody y conducción arcade.

Scripts principales:

- `KartController.cs`
- `PlayerKartInput.cs`
- `AIKartInput.cs`
- `CameraFollow.cs`

Funciones principales:

- Aceleración y frenado.
- Giro arcade.
- Límite de velocidad.
- Boost temporal.
- Stun.
- Invencibilidad.
- Reset del kart.
- `SetControlEnabled(bool, bool freezePhysics = true)`: al terminar la carrera el kart frena de forma natural por arrastre en lugar de detenerse de golpe.

---

### 2. Sistema de carrera

El sistema de carrera controla el flujo completo de la partida.

Scripts principales:

- `RaceManager.cs`
- `LapManager.cs`
- `PositionManager.cs`
- `TrackData.cs`
- `Checkpoint.cs`
- `CheckpointTracker.cs`

Funciones principales:

- Cuenta atrás inicial.
- Registro de corredores.
- Gestión de vueltas.
- Sistema de checkpoints.
- Cálculo de posiciones en tiempo real.
- Finalización de carrera.
- Cuando el jugador cruza la meta, los bots siguen corriendo hasta agotar el tiempo o terminar; el jugador pasa a modo espectador.

---

### 3. Bots e inteligencia artificial

Actualmente el proyecto incluye dos enfoques:

- Bots funcionales mediante seguimiento de checkpoints.
- Base de entrenamiento con Unity ML-Agents para aprendizaje por refuerzo.

Scripts principales:

- `AIKartInput.cs`
- `KartAgent.cs`
- `AgentRewardManager.cs`
- `TrainingSceneManager.cs`

El agente de ML-Agents utiliza observaciones estructuradas como velocidad, dirección hacia el siguiente checkpoint, distancia al objetivo, progreso de checkpoints y sensores de raycast.

---

### 4. Sistema de power-ups

El sistema de power-ups forma parte del alcance del MVP.

Power-ups previstos:

- Banana
- Concha
- Seta boost
- Estrella

La idea principal es asignar power-ups según la posición del corredor, de forma que los jugadores en posiciones bajas tengan más probabilidad de recibir objetos fuertes.

---

### 5. Clasificación del comportamiento del jugador

El proyecto contempla un sistema de clasificación del jugador al finalizar la carrera.

Métricas previstas:

- Velocidad media.
- Velocidad máxima.
- Número de choques.
- Salidas de pista.
- Power-ups usados.
- Posición final.
- Tiempo total de carrera.

Modelo previsto:

- Árbol de decisión exportado a reglas C#.

Etiquetas previstas:

- Patoso
- Agresivo
- Conservador
- Pro
- Caótico
- Lento

---

### 6. Sistema de UI

Scripts principales:

| Archivo | Descripción |
| --- | --- |
| `Assets/Scripts/UI/MainMenuUI.cs` | Menú principal generado por código: título 3D con prefabs de letras Synty, botones JUGAR / CONFIGURACION / SALIR, panel de configuración (volumen, vueltas, calidad gráfica). |
| `Assets/Scripts/UI/RaceUI.cs` | HUD de carrera: cuenta atrás animada, vuelta/tiempo/posición, pausa con ESC, modo espectador con Tab. |
| `Assets/Scripts/UI/ResultsScreen.cs` | Pantalla de clasificación final tras la carrera. |
| `Assets/Scripts/UI/UIIconRenderer.cs` | Utilidad estática que renderiza prefabs 3D a `RenderTexture` para usarlos como iconos en la UI. |

**Menú principal**

- Título "KART AI RACING" con prefabs de letras 3D (Synty PolygonIcons), con fallback a texto amarillo en builds sin editor.
- Panel de configuración: volumen, número de vueltas (1/3/5), calidad gráfica (Baja/Media/Alta) con persistencia en `PlayerPrefs`.
- Icono de engranaje junto al título del panel.

**HUD de carrera**

- Icono de reloj 3D junto al temporizador (solo visible durante la carrera).
- Cuenta atrás 3 → 2 → 1 → GO! con animación de escala y fade.
- Vuelta actual, tiempo de carrera y posición en tiempo real.
- Pausa con **ESC**: overlay oscuro con icono de pausa centrado (sin botones, ESC para reanudar).

**Modo espectador** (tras cruzar la meta)

- La cámara sigue automáticamente al bot mejor posicionado que aún no ha terminado.
- **Tab** para cambiar de objetivo; el HUD muestra la vuelta y posición del bot observado.
- Indicador `ESPECTANDO: [nombre] [2/5] Tab→siguiente`.

**Pantalla de resultados**

- Aparece 2.5 s después de que termine la carrera.
- Clasificación con posición, nombre y tiempo; `DNF` para los que no terminaron.
- Icono de trofeo 3D, botones VOLVER A JUGAR y MENU PRINCIPAL.
- El cursor se desbloquea automáticamente al mostrar la pantalla.

---

## Configuración en Inspector (escena de carrera)

Para que los iconos 3D funcionen hay que asignar en el Inspector los siguientes prefabs:

GameObject con **RaceUI**:

- `Clock Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Clock_01.prefab`
- `Pause Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Pause_01.prefab`

GameObject con **MainMenuUI**:

- `Gear Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Gear_01.prefab`

GameObject con **ResultsScreen**:

- `Trophy Icon Prefab` → `Assets/Prefabs/UI/SM_Icon_Trophy_01.prefab`

---

## Escenas

Las escenas del proyecto se encuentran en `Assets/Scenes/`. La selección final de escenas que formarán parte del MVP está pendiente de definirse.

---

## Prefabs

- `Assets/Prefabs/Karts/`: 15 modelos de karts (Modular Cyber Racing Cars · ithappy).
- `Assets/Prefabs/PowerUps/`: prefabs de power-ups (Synty PolygonIcons).
- `Assets/Prefabs/Track/`: props de circuito: barreras, conos, líneas de meta, trampolines, flechas.
- `Assets/Prefabs/UI/`: iconos 3D para UI: reloj, engranaje, pausa, trofeo, letras A–Z.

---

## Instalación y ejecución

1. Clonar el repositorio:

```bash
git clone https://github.com/Pollete500/Proyecto_Final_IA.git
```

2. Abrir el proyecto con Unity 6.

3. Abrir una escena del proyecto desde:

```text
Assets/Scenes/
```

4. Pulsar Play para probar el prototipo.

---

## Entrenamiento con ML-Agents

Para entrenar el agente con Unity ML-Agents:

1. Crear y activar un entorno virtual de Python.

2. Instalar dependencias:

```bash
pip install -r Assets/Python/requirements-mlagents.txt
```

3. Ejecutar entrenamiento:

```bash
mlagents-learn Assets/MLAgents/Config/kart_agent_config.yaml --run-id kart_agent_test --base-port 5004 --results-dir Assets/MLAgents/TrainingLogs
```

4. Cuando el terminal indique que espera conexión, pulsar Play en Unity.

---

## Documentación

La documentación técnica del proyecto se encuentra en:

```text
Assets/Documentation/
```

Incluye información sobre:

- Descripción general del proyecto.
- Configuración de Unity.
- Sistema de kart.
- Sistema de carrera.
- Checkpoints, vueltas y posiciones.
- ML-Agents.
- Dataset.
- Clasificador de comportamiento.
- Resultados de entrenamiento.
- Problemas conocidos y trabajo futuro.

---

## Estado actual del proyecto

Estado general:

- Base jugable del kart implementada.
- Sistema de checkpoints, vueltas y posiciones implementado.
- Bots con seguimiento básico de checkpoints.
- Escena de entrenamiento ML-Agents preparada.
- Menú principal, HUD de carrera, modo espectador y pantalla de resultados implementados.
- Documentación técnica en desarrollo.

Sistemas pendientes o en desarrollo:

- Power-ups completos.
- Dataset final.
- Clasificador de comportamiento final.
- Resultados definitivos de entrenamiento.
- Selección final de escenas de carrera.
- Manual de usuario final.
- Vídeo de demostración.

---

## Ramas de trabajo

El desarrollo puede contener ramas específicas para funcionalidades concretas.

- `main`: rama principal del proyecto.
- `menu-and-cuentaatras`: rama centrada en menú, HUD, cuenta atrás y pantalla de resultados (ya fusionada en `main`).

Cuando una funcionalidad de una rama esté comprobada y estable, debe fusionarse en `main`.

---

## Autores

Proyecto desarrollado como trabajo final del Máster de Inteligencia Artificial y Big Data.

---

## Licencia y assets externos

Este proyecto utiliza assets externos únicamente con fines académicos.

**Assets ya integrados en el proyecto:**

- [**POLYGON - Icons Pack**](https://assetstore.unity.com/packages/3d/environments/fantastic-seaside-town-323176) (Synty Studios).
- [**Modular Cyber Racing Cars**](https://assetstore.unity.com/packages/3d/environments/urban/polygon-battle-royale-pack-art-by-synty-128513) (ithappy).

**Assets previstos para los escenarios de carrera** (uso condicionado al tiempo disponible de desarrollo; pueden no llegar a incorporarse al MVP final):

- [**FANTASTIC - Seaside Town**](https://assetstore.unity.com/packages/3d/environments/fantastic-seaside-town-323176) (Tidal Flask Studios).
- [**POLYGON Battle Royale Pack**](https://assetstore.unity.com/packages/3d/environments/urban/polygon-battle-royale-pack-art-by-synty-128513) (Synty Studios).
- [**POLYGON Western Pack**](https://assetstore.unity.com/packages/3d/environments/historic/polygon-western-pack-art-by-synty-112212) (Synty Studios).
