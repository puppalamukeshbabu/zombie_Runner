# ZOMBIE RUNNER — RAIN MODE ENVIRONMENT VARIANT IMPLEMENTATION REPORT

**Project Name**: Zombie Runner (3D Survival-Horror Endless Runner)  
**Assigned Variant**: RAIN MODE  
**Engine Version**: Unity 6.5 (6000.5.9f1) URP (Universal Render Pipeline)  
**Platform**: macOS / Windows PC Compatible  
**Date**: November 6, 2026  
**Status**: FULLY IMPLEMENTED & OPERATIONAL (0 Compiler Errors, 0 Runtime Exceptions)

---

## 1. Executive Summary & Variant Concept

In accordance with team requirements, this customized version of **Zombie Runner** transforms the default clear night city into a cinematic, atmospheric **RAIN MODE** survival-horror environment. 

The environment features:
- Realistic, high-density rainfall with stretched billboard rain streaks
- Wet, reflective asphalt and road markings with accurate micro-roughness (not mirror-like chrome)
- Subtle, reflective standing water puddles along the running lanes
- Dark, stormy overcast lighting and dense atmospheric mist/fog
- Periodic atmospheric lightning discharges with multi-channel surges
- Dedicated 44.1kHz 16-bit stereo seamless rain storm audio bed
- Dynamic tunnel occlusion: rain particles and ground puddles smoothly deactivate when entering the subterranean subway tunnel, and rain audio smoothly muffles to a distant rumble
- **100% Gameplay Preservation**: Player controls, lane switching, jumping, sliding, shooting, zombie combat/AI, scoring, collectibles, and endless city–tunnel segment recycling remain completely untouched.

---

## 2. Environment Changes & Technical Implementation

### A. Realistic Rainfall Particles (`RainModeManager.cs`)
- **Particle Architecture**: Implemented via a persistent `ParticleSystem` dynamically positioned above and ahead of the runner $(0, \text{player.y} + 24\text{m}, \text{player.z} + 18\text{m})$ with `SimulationSpace.World`.
- **Emitter Volume**: Flat box shape $22\text{m} \times 1\text{m} \times 44\text{m}$ spanning all 3 lanes, curbs, and upcoming road.
- **Particle Physics**:
  - Downward velocity: $16 - 22\text{ m/s}$ with realistic wind slant ($\pm 0.6\text{ m/s}$ X, $-1.0\text{ m/s}$ Z).
  - Gravity modifier: $1.1$.
  - Lifetime: $1.2 - 1.8\text{ s}$.
  - Emission rate: $850\text{ particles/sec}$ (budget-friendly for laptops, high visual density).
- **Streak Rendering**: Configured with `ParticleSystemRenderMode.Stretch` (`velocityScale: 0.055`, `lengthScale: 1.45`), producing fine, needle-like rain streaks.
- **Collision & Cleanup**: Ground collision enabled; particles instantly destroy on contact with the road layer (`lifetimeLoss: 1.0`).

### B. Wet Road Materials & Realistic Reflections
All key environment materials were upgraded to simulate rain-drenched surfaces:
1. **`WR_Asphalt.mat` (Road Surface)**:
   - Smoothness: Increased from $0.38 \to 0.72$ (slick wet sheen with sharp streetlight & muzzle flash highlights).
   - Base Color: Darkened from $(0.48, 0.50, 0.54) \to (0.35, 0.37, 0.41)$ to represent light absorption of saturated asphalt.
   - Specular Color: Boosted to $(0.45, 0.50, 0.58)$ for realistic Fresnel reflections.
   - Normal Map Bump: Preserved at $1.25$ so asphalt texture remains visible under the wet gloss.
2. **`WR_LanePaint.mat` (Road Markings)**:
   - Smoothness: Increased from $0.36 \to 0.68$ (wet slick paint).
   - Base Color: Tuned to $(0.88, 0.86, 0.78)$ to ensure lane boundaries remain crisp and visible through rain.
3. **`WR_Concrete.mat` (Curbs & Sidewalks)**:
   - Smoothness: Increased from $0.30 \to 0.56$; base color darkened for wet porous concrete.
4. **`WR_SubwayConcrete.mat` & `WR_SubwayTile.mat` (Tunnel Interior)**:
   - Smoothness adjusted to $0.52$ and $0.60$ for damp subterranean tunnel walls.
5. **`WR_Ruins.mat` (Building Facades)**:
   - Smoothness increased from $0.28 \to 0.48$ for rain-washed concrete and brick facades.
6. **`WR_CarPaint.mat` (Abandoned Obstacle Vehicles)**:
   - Smoothness increased from $0.65 \to 0.82$ for rain-soaked glossy automotive paint.

### C. Subtle Reflective Puddles (`WR_Puddle.mat`)
- **Puddle Pool**: `RainModeManager` maintains a recycled pool of 8 subtle puddle quads positioned at $Y = 0.018\text{m}$ (immediately above the road mesh).
- **Material Properties**: Transparent URP Lit material (`WR_Puddle.mat`) with `_Smoothness: 0.94`, `_Metallic: 0.05`, and dark tinted water base color $(0.12, 0.15, 0.20, 0.70)$, providing realistic mirror-like specular puddle reflections from the stormy sky and streetlights.
- **Safety**: Colliders are destroyed on creation, guaranteeing zero interference with player movement, raycasts, or zombie pathing.

### D. Stormy Lighting & Atmospheric Mist Profile (`EnvironmentThemeManager.cs`)
A dedicated **Rain City Lighting Profile** was added to `EnvironmentThemeManager`:
- **Directional Key Light (Moonlight)**:
  - Color: Cold desaturated blue $(0.55, 0.65, 0.82)$.
  - Intensity: Reduced from $1.75 \to 0.85$ to evoke heavy cloud cover.
- **Ambient Lighting (Trilight Mode)**:
  - Sky: Deep indigo $(0.20, 0.24, 0.32)$.
  - Equator: Urban mist $(0.15, 0.18, 0.25)$.
  - Ground: Wet pavement bounce $(0.10, 0.12, 0.16)$.
- **Atmospheric Mist (Fog)**:
  - Fog Color: Storm mist $(0.09, 0.11, 0.16)$.
  - Fog Start: $28\text{m}$; Fog End: $98\text{m}$ (tightened from $165\text{m}$ to obscure spawn horizons while leaving gameplay obstacles and zombies at $15 - 30\text{m}$ fully visible).

### E. Atmospheric Lightning Flashes
- Periodic randomized lightning discharge routine ($10 - 28\text{s}$ interval) executing a realistic double-strike burst:
  - Strike 1: Leader flash ($3.8\times$ intensity surge, duration $45\text{ms}$).
  - Intermediate dip ($35\text{ms}$).
  - Strike 2: Main channel return stroke ($4.6\times$ intensity surge, duration $80\text{ms}$).
  - Smooth quadratic decay ($350\text{ms}$) back to the stormy moonlight level.

### F. Seamless City–Tunnel Transition & Occlusion
- `RainModeManager` continuously reads `EnvironmentThemeManager.BlendFactor`:
  - **Open City**: Full rainfall ($850\text{ p/s}$), active puddles, stormy lighting.
  - **Tunnel Approach / Interior**: As the runner enters the subterranean tunnel (`BlendFactor > 0.2`), rain particle emission smoothly scales to $0$, and puddles are hidden, ensuring no rain falls through the concrete tunnel roof.
  - **Tunnel Exit**: Rain emission smoothly ramps back to full as the player returns to the ruined city.

---

## 3. Audio Architecture

### A. Dedicated Rain Ambience Loop (`Ambience_Rain_Storm_Loop.wav`)
- **Format**: 44,100 Hz, 16-bit stereo PCM WAV, $24.0\text{s}$ duration.
- **Sound Design**:
  - High-frequency rainfall drizzle & hiss ($3,200\text{Hz}$ bandpass).
  - Mid-frequency droplet body & pavement wash ($1,400\text{Hz}$ bandpass).
  - Low-frequency subterranean storm turbulence ($75\text{Hz}$ lowpass).
  - Subtle distant thunder rolls at $4.5\text{s}$ and $15.0\text{s}$.
  - Seamless $1.2\text{s}$ cross-fade at loop boundaries for click-free infinite looping.

### B. Segment-Recycle Immunity & Tunnel Ducking (`AmbientAudioController.cs`)
- The rain audio source is instantiated on a persistent GameObject and routed to the project's `Ambient` AudioMixerGroup.
- **Endless Immunity**: Rain audio never restarts or clips when road segments or environment chunks recycle.
- **Tunnel Muffling**: Dynamically modulates volume based on `BlendFactor`:
  $$\text{Volume} = \text{rainBaseVolume} \times \text{masterVolume} \times \text{Lerp}(1.0, 0.15, \text{blend})$$
  Inside the tunnel, the rain sound smoothly drops to $15\%$ volume, simulating the muffled sound of a storm raging outside the concrete subway portal.

---

## 4. Gameplay Preservation Audit

Every core gameplay system was strictly preserved:

| System | State | Verification |
|---|---|---|
| **Player Movement** | 100% Preserved | 3-lane switching, lane speed ($11\text{m/s}$), forward speed ($8 - 16\text{m/s}$), jump ($2.1\text{m}$), and slide ($0.8\text{s}$) fully intact. |
| **Weapons & Combat** | 100% Preserved | Tommy gun auto-aim, firing rate, bullet raycasts, damage ($1\text{ dmg/shot}$), and blood hit impacts unaffected. |
| **Zombie AI & Spawning** | 100% Preserved | Lane tracking, forward approach, attack triggers, death animations, health pools (Normal, Fast, Big) unchanged. |
| **Pickups & Economy** | 100% Preserved | Gold coins, ammo boxes, health kits, power-ups spawn and collect normally. |
| **Scoring & UI** | 100% Preserved | Distance counter, score multiplier, ammo counter, health bar, Game Over screen, and Revive button functional. |
| **Endless Sequencing** | 100% Preserved | City (3 segments) $\to$ Tunnel Entrance $\to$ Tunnel (2 segments) $\to$ Tunnel Exit cycle uninterrupted. |

---

## 5. Summary of Files Created and Modified

### Newly Created Files:
1. `Assets/ZombieRunner/Rain/Scripts/RainModeManager.cs` — Master Rain Mode manager (particles, player tracking, tunnel occlusion, puddles, lightning).
2. `Assets/ZombieRunner/Rain/Scripts/RainModeManager.cs.meta` — Unity asset metadata.
3. `Assets/ZombieRunner/Rain/Scripts/Editor/RainModeEditorSetup.cs` — Editor menu commands for 1-click configuration and toggling.
4. `Assets/ZombieRunner/Materials/WR_Puddle.mat` — Transparent, high-gloss reflective puddle material.
5. `Assets/ZombieRunner/Materials/WR_Puddle.mat.meta` — Material metadata.
6. `Assets/ZombieRunner/Audio/Clips/Ambience_Rain_Storm_Loop.wav` — Studio-grade 44.1kHz 16-bit stereo seamless rain storm ambience.
7. `Assets/ZombieRunner/Audio/Clips/Ambience_Rain_Storm_Loop.wav.meta` — Audio importer metadata (2D ambient bed).
8. `RAIN_MODE_IMPLEMENTATION_REPORT.md` — Complete engineering documentation.

### Modified Existing Files:
1. `Assets/ZombieRunner/Scripts/World/EnvironmentThemeManager.cs` — Added dedicated `rainMode` flag and `rainCityProfile` with stormy lighting, atmospheric mist, and wet bounce.
2. `Assets/ZombieRunner/Scripts/Audio/AmbientAudioController.cs` — Integrated `rainSource` audio loop with seamless tunnel transition ducking.
3. `Assets/ZombieRunner/Materials/WR_Asphalt.mat` — Updated to wet asphalt ($0.72$ smoothness, darkened base color).
4. `Assets/ZombieRunner/Materials/WR_LanePaint.mat` — Updated to wet road paint ($0.68$ smoothness).
5. `Assets/ZombieRunner/Materials/WR_Concrete.mat` — Updated to wet concrete ($0.56$ smoothness).
6. `Assets/ZombieRunner/Materials/WR_SubwayConcrete.mat` — Updated to damp concrete ($0.52$ smoothness).
7. `Assets/ZombieRunner/Materials/WR_SubwayTile.mat` — Updated to wet glossy tiles ($0.60$ smoothness).
8. `Assets/ZombieRunner/Materials/WR_Ruins.mat` — Updated to wet building facades ($0.48$ smoothness).
9. `Assets/ZombieRunner/Materials/WR_CarPaint.mat` — Updated to wet car paint ($0.82$ smoothness).
10. `PLAYER_AND_ZOMBIE_MOVEMENT_FIX_REPORT.md` — Updated with root cause 10 and final animation resolution.

---

## 6. How to Test and Verify in Unity

1. Open the project in Unity Editor.
2. Open the scene `Assets/ZombieRunner/Scenes/ZombieRunnerGameplay.unity`.
3. Press **▶ Play**:
   - `RainModeManager` auto-initializes via `[RuntimeInitializeOnLoadMethod]` if not already present.
   - You will see visible, realistic rain falling from above the runner.
   - The road surface displays wet specular sheen and reflective puddles in the lanes.
   - The sky and mist reflect cold, stormy blue-grey overcast tones.
   - Continuous rain storm ambience plays smoothly in the background.
   - When running into the subterranean tunnel, the rain smoothly stops and the rain sound ducks to a muffled distant tone.
   - Exiting the tunnel smoothly restores the full storm atmosphere.
   - All player movement (WASD/arrows, space jump, slide) and zombie combat work with zero errors.
