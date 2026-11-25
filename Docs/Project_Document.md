# MIDNIGHT PURSUIT — Project Document

*A night-time police chase driving game made in Unity 6 (URP).*

---

## 1. Overview

**Midnight Pursuit** is a 3D arcade driving game. The player drives a sports car through a city at night while police cars chase them. Every time a police car rams the player, the **BUSTING** meter fills up; when it is full the player is busted. The goal is to escape for as long as possible — the **escape time** is the score, and the best time is saved.

| | |
|---|---|
| **Engine** | Unity 6, Universal Render Pipeline (URP) |
| **Language** | C# |
| **Input** | Unity Input System (keyboard) |
| **Version control** | Git + GitHub (main + feature branches, Git LFS for assets) |
| **Repository** | github.com/Malakkserag/MIDNIGHT-PURSUIT |

### Controls

| Key | Action |
|---|---|
| W / ↑ | Accelerate |
| S / ↓ | Brake / reverse |
| A D / ← → | Steer |
| SPACE | Handbrake drift |
| C | Change camera view |
| R | Restart |
| ESC | Pause / back |

---

## 2. Development story

### 2.1 Prototype — a cube that drives

We started in a sandbox scene with a placeholder cube car driven by our own `SimpleCarController` (Rigidbody + forces), a chase camera, a first police AI and a simple HUD.

![Prototype cube car](Screenshots/01_prototype_cube_car.png)
![First busted screen](Screenshots/02_prototype_busted.png)

### 2.2 Real car models

The cubes were replaced by Asset Store models (ICONIC Sports Car for the player, Police Interceptor for the police). An editor tool (`CarModelSwapper`) swaps the models in and fits the physics collider exactly to the mesh.

![Real car models](Screenshots/03_real_car_models_scene.png)
![Police chase close-up](Screenshots/04_police_chase_closeup.png)
![Busted with real models](Screenshots/05_busted_real_models.png)

### 2.3 The night city

The chase moved into a full city map (Demo City by Versatile Studio) lit for night, with a whole-map **minimap** in the top-right corner. Busting was changed to require real physical contact between the cars.

![City overview](Screenshots/06_demo_city_overview.webp)
![Driving in the city](Screenshots/07_city_driving_108kmh.webp)
![Police with sirens](Screenshots/08_city_police_sirens_scene.webp)
![Busted in the city](Screenshots/09_city_busted.webp)
![Street driving](Screenshots/10_city_driving_street.webp)
![Minimap](Screenshots/12_city_driving_minimap.webp)

### 2.4 Police that follow the streets

A **NavMesh** was baked on the roads only, so the police drive along streets instead of through buildings. They use a "carrot on a stick" look-ahead point on the path for smooth steering, slow down for corners, back up when stuck, and respawn behind the player (out of sight) if they fall hopelessly behind.

![Police on the road only](Screenshots/20_police_on_road_only.webp)

**Bugs we hit along the way** (and fixed):

![Police circling the player](Screenshots/bugs/bug_police_circling_player.png)
![Police flying](Screenshots/bugs/bug_police_flying.png)
![Police stuck behind a wall](Screenshots/bugs/bug_police_stuck_behind_wall.webp)

### 2.5 Camera views

A clickable camera icon (or the **C** key) switches between **Close, Wide, Overhead** and **Driver** views.

![Close](Screenshots/14_camera_close.webp)
![Wide](Screenshots/15_camera_wide.webp)
![Overhead](Screenshots/16_camera_overhead.webp)
![Driver seat](Screenshots/17_camera_driver_seat.webp)

### 2.6 Menus

A start menu (with the city flying past in the background), How to Play, pause and busted screens, and a saved best escape time.

![Start menu](Screenshots/21_start_menu.webp)
![How to play](Screenshots/22_how_to_play.webp)
![Busted — new record](Screenshots/24_busted_new_record.webp)

---

## 3. OpenGL in the project

Our course requires OpenGL. Unity normally hides the graphics API, but it exposes a low-level **`GL` class** that works like classic OpenGL immediate mode. We used it to draw the **police radar** (top-left of the HUD) entirely by hand — no textures, no UI system:

| OpenGL-style call | Used for |
|---|---|
| `GL.PushMatrix()` / `GL.LoadPixelMatrix()` / `GL.PopMatrix()` | 2D pixel-space projection for the HUD |
| `GL.Begin(GL.TRIANGLES)` | Background disc (triangle fan) and the rotating **sweep beam** with fading alpha |
| `GL.Begin(GL.LINES)` | Range rings and cross-hair |
| `GL.Begin(GL.QUADS)` | Police blips (flashing red/blue) |
| `GL.Color` / `GL.Vertex3` | Per-vertex colour and positions |

The radar is **player-centred and rotating**: "up" is always the direction the car is facing. Each police car's position is transformed into the player's local space (`InverseTransformPoint`), scaled to the radar radius, and clamped to the edge when out of range. A built-in vertex-coloured, alpha-blended shader (`Hidden/Internal-Colored`) is used as the material.

Source: `Assets/_Project/Scripts/UI/GLRadar.cs`

The project can also be switched to render through the **OpenGLCore** backend (Project Settings → Player → Graphics APIs).

![GL radar](Screenshots/26_gl_radar_hud.webp)

---

## 4. HUD and menu redesign

- **Escape time** became a centred headline (Impact font, neon cyan with a black outline). It turns red and pulses while the police are busting you, and switches to `m:ss.s` after one minute.
- The **BUSTING** bar sits centred under the timer, with its label underneath.
- The controls text was removed from the screen during play (it lives in How to Play / Options now).

![Escape timer headline](Screenshots/29_escape_timer_headline.webp)
![Busting bar](Screenshots/33_busting_label_centered.webp)

**Pause menu**: a dark side panel on the right with the game still visible on the left — Resume, Restart, How to Play, Options and Quit Race.

![Pause menu](Screenshots/27_pause_menu.webp)
![Pause — how to play](Screenshots/30_pause_how_to_play_v2.webp)

**Options**: two tabs — **Sound** (custom volume sliders for Master, Engine, Crashes & pickups, Police siren, plus Mute) and **Controls**. Settings are saved with `PlayerPrefs`.

![Options — sound](Screenshots/38_options_sound_tab.webp)
![Options — controls](Screenshots/39_options_controls_tab.webp)
![Start menu with options](Screenshots/40_start_menu_options.webp)

---

## 5. Physics and AI bug fixes

| Problem | Cause | Fix |
|---|---|---|
| Cars spinning like tops after a hit | Car bodies have zero friction; nothing slowed collision spin | Damp yaw/roll/pitch spin every physics step while grounded; angular damping + max angular velocity |
| Cars passing through buildings | At 30+ m/s a car moves further than a wall's thickness in one physics step | `CollisionDetectionMode.ContinuousDynamic` |
| Car on another car's roof could still drive | Any upward contact counted as "ground" | Only static colliders count as ground |
| Police driving into walls | Only followed the path, never looked ahead | Three ray "whiskers" ahead that steer away from walls and slow down |
| Police wiggling back and forth | Turn-around manoeuvre restarted forever | Stops when facing the player / blocked / timed out, then a 3 s cooldown |

---

## 6. Gameplay features

### 6.1 Sound (generated in code)

The project has **no audio files** — every sound is synthesised at runtime from waveforms (`ProcAudio.cs`) and cached as `AudioClip`s:

- **Engine** — smooth harmonic tone; pitch follows speed with a fake gearbox.
- **Police siren** — horn-speaker style tone; slow **wail** when far, fast **yelp** within 45 m, cross-faded.
- **Crash** — filtered noise burst with a thump, volume based on impact speed.
- **Pickup chime, nitro whoosh, menu click.**

Other cars' engines are quiet and short-range so they don't stack into a drone; Doppler is disabled to avoid warbling at 250 km/h.

### 6.2 Crash sparks

Hard impacts spawn a burst of stretched, fading spark particles plus a quick orange light flash (`Sparks.cs`), all built in code.

![Nitro and sparks](Screenshots/36_nitro_sparks.webp)

### 6.3 Road pickups

Glowing, bobbing power-ups are placed on the roads (on the NavMesh) ahead of the player:

| Pickup | Effect |
|---|---|
| 🟠 **NITRO** | Higher top speed and double acceleration for 3 s |
| 🟢 **REPAIR** | Empties the BUSTING meter |
| 🔵 **TIME** | +5 seconds escape time |

![Repair pickup](Screenshots/35_pickup_repair.webp)
![Pickups on the highway](Screenshots/37_highway_pickups.webp)

### 6.4 Speed

Player top speed is **180 km/h**, and **~260 km/h with nitro**. Steering is softened at very high speed to stay controllable.

![262 km/h with nitro](Screenshots/44_nitro_262kmh.webp)

### 6.5 Traffic

About 8 civilian cars drive around the city at ~100 km/h in **real colour variants** of the sports car (red, white, black, yellow, green, grey…). They follow NavMesh routes in the **right-hand lane**, brake only for other cars, back up and re-route when stuck, and appear/disappear only off-screen.

![Traffic cars](Screenshots/43_traffic_coloured_cars.webp)

### 6.6 Three police cars

Two **interceptors** (top speed ~288 km/h) start in formation beside the original police car, so all three set off together. A **"POLICE IS COMING!!"** warning pops up every time a police car closes in.

![Three police cars](Screenshots/49_three_police_start_together.webp)
![Police is coming](Screenshots/50_police_is_coming_toast.webp)
![Three police busting the player](Screenshots/47_three_police_busting.webp)
![Busted after 202 seconds](Screenshots/46_busted_202s.webp)

---

## 7. Code structure

All game code is in `Assets/_Project/Scripts/`:

| Folder | Scripts | Purpose |
|---|---|---|
| `Car/` | `SimpleCarController`, `ChaseCamera` | Car physics (throttle, steering, grip, downforce, nitro, auto-recover), camera views |
| `AI/` | `PoliceChaseAI`, `PoliceSpawner`, `TrafficCar`, `TrafficManager` | Police pathfinding/ramming/busting, interceptors + warning, civilian traffic |
| `Audio/` | `ProcAudio`, `AudioManager`, `CarAudio` | Generated sounds, listener + volume, per-car engine/siren/crash |
| `VFX/` | `Sparks` | Crash sparks |
| `Gameplay/` | `Pickup`, `PickupSpawner` | Power-ups and on-screen messages |
| `UI/` | `RaceHUD`, `MainMenu`, `GLRadar`, `Minimap` | HUD, menus/options, OpenGL radar, minimap |
| `Core/` | `GameState`, `GameSettings` | Game flow (Menu → Playing ⇄ Paused → Busted), saved settings |
| `Editor/` | `CarModelSwapper`, `CityChaseSetup`, `PoliceNavMeshBaker`, `TestTrackBuilder` | Editor tools for building the scenes |

`RaceHUD` adds all runtime systems automatically (minimap, radar, audio, pickups, traffic, police spawner, menus), so the scene needs no extra setup.

---

## 8. Third-party assets

- ICONIC – Sports Car FREE Vol.02 (player and traffic cars)
- Police Car & Helicopter (police interceptor)
- Demo City by Versatile Studio (city map)
- Low Poly Street Pack

All code, sounds and effects were written for this project.
