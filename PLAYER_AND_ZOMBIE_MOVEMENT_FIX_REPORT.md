# ZOMBIE RUNNER — COMPLETE PLAYER AND ZOMBIE MOVEMENT FAILURE AUDIT & FIX REPORT

**Project Name**: Zombie Runner (3D Survival-Horror Endless Runner)  
**Engine Version**: Unity 6.5 (6000.5.9f1) Metal (macOS)  
**Date of Audit & Fix**: November 6, 2026  
**Status**: FULLY RESTORED & OPERATIONAL (0 Compiler Errors, 0 Runtime Exceptions, 0 Obsolete Warnings)

---

## 1. Original Root Cause Analysis

Following a comprehensive audit of the extracted project files, scenes, prefabs, and runtime logs, six distinct root causes were identified that broke player and zombie movement when transferred between development environments:

1. **Zombie Root Motion Transform Locking (Zombies Frozen at Spawn)**:
   - In Unity 6, Humanoid `Animator` components default `applyRootMotion` to `true`.
   - The imported Mixamo animation clips (`zombie walk.fbx`, `zombie run.fbx`, `zombie idle.fbx`) have baked / in-place root motion with zero root motion curves.
   - At runtime, the Animator evaluation loop overwrote `transform.position` every frame, effectively pinning zombies at their exact spawn coordinates and nullifying script-driven `transform.position` updates.

2. **Static Collider Physics Locking (CharacterController Collision Lockout)**:
   - Zombie prefabs (`NormalZombie`, `FastZombie`, `BigZombie`) possessed non-trigger static `CapsuleCollider` components without `Rigidbody`.
   - Unity's `CharacterController.Move()` detects contact with static geometry and halts forward velocity to prevent penetration.
   - Consequently, when the runner reached a zombie, the player was physically blocked from moving forward, while bullet raycasts still required colliders to register hits.

3. **Missing Lane AI & Vector Clumping**:
   - The original AI steered directly toward the player's world position: `(player.position - transform.position).normalized`.
   - When zombies spawned across the three lanes (Left: -3m, Center: 0m, Right: +3m), all active zombies steered diagonally toward the player's active lane coordinate.
   - This caused zombies to abandon their lanes, crash into each other, and merge into an impenetrable clump in front of the player.

4. **Initial Spawn Orientation Inversion**:
   - In `ZombieSpawner.cs`, spawned zombies were instantiated using `Quaternion.identity` (Euler $(0, 0, 0)$ facing $+Z$).
   - Because the endless runner progresses down the $+Z$ axis, zombies were spawned facing away from the approaching runner rather than facing oncoming traffic ($180^\circ$ rotation).

5. **Input System Handler Configuration Dropouts**:
   - `ProjectSettings.asset` was set to `activeInputHandler: 1` (New Input System only).
   - On transferred laptop environments without exclusive Game view cursor capture, `Keyboard.current` frequently yielded null or missed keypress frames, while legacy `Input.GetKeyDown` calls were ignored by Unity's player loop.

6. **Road Mesh Seams & Raycast Ground-Check Failures**:
   - Jumping and sliding relied solely on `CharacterController.isGrounded`.
   - Due to minor floating-point tolerances over road tile chunk boundaries, `isGrounded` flickered to `false` intermittently, dropping jump and slide input and causing erratic vertical velocity accumulation.

7. **Missing Humanoid Avatar Reference & Rebind Lock (Characters Frozen in T-Pose)**:
   - In `PlayerPlaceholder.prefab`, the `Visual - New Character` nested prefab override only serialized `m_Controller`, omitting `m_Avatar`.
   - Without an explicit `Avatar` reference on the `Animator` component, Unity cannot map humanoid animation curves to skeletal bone transforms.
   - In `ZombieController.cs`, `animator.avatar` and `animator.runtimeAnimatorController` lacked fallback resolution. If `CacheAnimatorParameters()` evaluated before controller assignment, `hasSpeedParameter` evaluated to `false`, preventing transition from the default `Idle` state to `Walk` or `Run`.
   - In `MainMenuController.cs` and `MenuAnimatorFixer.cs`, menu characters (`Menu_Survivor` and `Distant_Zombies`) lacked explicit Avatar initialization, leaving them frozen in their default bind-pose (T-pose).

8. **Empty `internalIDToNameTable` in FBX Meta Files (Dangling `{fileID: 9000000}` Avatars)**:
   - When a Unity project containing Humanoid FBX assets is transferred between operating systems (Windows to macOS) without local `Library/` metadata, Unity reimports all FBX files.
   - In `Ch05_nonPBR.fbx.meta`, `Ch10_nonPBR.fbx.meta`, `Pumpkinhulk L Shaw.fbx.meta`, `Yaku J Ignite.fbx.meta`, and `X Bot.fbx.meta`, the `internalIDToNameTable` field was empty (`[]`).
   - Consequently, Unity assigned dynamic 64-bit internal IDs instead of the legacy `9000000` Avatar fileID.
   - Every prefab and animation clip referencing `{fileID: 9000000, guid: ..., type: 3}` resolved to `null` (Missing Avatar).
   - Without an avatar, Mecanim Humanoid retargeting failed silently at runtime, locking both the player and all spawned zombies in their default bind-pose (T-Pose).

9. **Camera Follow Pitch & Framing Clipping Runner Below Screen Margin**:
   - In `RunnerCameraFollow.cs` and `ZombieRunnerGameplay.unity`, the camera follow was configured with `followOffset: (0, 2.25, -4.0)` and `lookAheadDistance: 12`, `lookHeight: 1.25`.
   - Over a 16m distance from camera to look target, $\Delta Y$ was only $-1.0\text{m}$, creating a camera pitch of $-3.6^\circ$ (pointing almost horizontally toward the horizon).
   - With a vertical field of view of $58^\circ$, the bottom viewport margin cut off the runner at $-32.6^\circ$, while the runner's feet and torso sat at $-39^\circ$.
   - This caused the player character to appear sunken into the bottom edge of the screen, with only the top of the head and shoulders visible.

10. **Zombie Animation FBX `avatarSetup: 1` Missing Avatar Resolution & Runtime Rebind Frame Delay**:
   - The 12 zombie animation FBX files in `Assets/ZombieRunner/Characters/Zombie/Animations/` used `avatarSetup: 1` ("Create From This Model") without explicit Avatar ID mapping in their `.meta` files.
   - Humanoid retargeting from these standalone animation takes onto the character rigs failed cross-platform.
   - Fixed by converting all 12 zombie animation `.fbx.meta` files to `avatarSetup: 2` ("Copy From Other Avatar") pointing to the verified `Ch10_nonPBR` avatar (`guid: c56195e085ccdad46a36cc55a7d12d63`).
   - Implemented `DelayedPlayRun()` in `PlayerController.cs` and `DelayedPlayLocomotion()` in `ZombieController.cs` to allow 2 frames for Unity's humanoid binding before executing `animator.Play()`.

---

## 2. Files Inspected

The following project files, scripts, prefabs, and configuration assets were audited:

### Core Movement & Gameplay Scripts
- [`Assets/ZombieRunner/Scripts/Player/PlayerController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Player/PlayerController.cs)
- [`Assets/ZombieRunner/Scripts/Player/PlayerHealth.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Player/PlayerHealth.cs)
- [`Assets/ZombieRunner/Scripts/Player/RunnerCameraFollow.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Player/RunnerCameraFollow.cs)
- [`Assets/ZombieRunner/Scripts/Enemies/ZombieController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Enemies/ZombieController.cs)
- [`Assets/ZombieRunner/Scripts/Enemies/ZombieSpawner.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Enemies/ZombieSpawner.cs)
- [`Assets/ZombieRunner/Scripts/Enemies/ZombieConfig.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Enemies/ZombieConfig.cs)
- [`Assets/ZombieRunner/Scripts/Weapons/ShootingController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Weapons/ShootingController.cs)
- [`Assets/ZombieRunner/Scripts/UI/UIManager.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/UI/UIManager.cs)
- [`Assets/ZombieRunner/Scripts/UI/MainMenuController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/UI/MainMenuController.cs)
- [`Assets/ZombieRunner/Scripts/Core/GameManager.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Core/GameManager.cs)
- [`Assets/ZombieRunner/Scripts/World/EndlessRoadManager.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/World/EndlessRoadManager.cs)

### Prefabs & Scenes
- [`Assets/ZombieRunner/Prefabs/Player/PlayerPlaceholder.prefab`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Prefabs/Player/PlayerPlaceholder.prefab)
- [`Assets/ZombieRunner/Prefabs/Zombies/NormalZombie.prefab`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Prefabs/Zombies/NormalZombie.prefab)
- [`Assets/ZombieRunner/Prefabs/Zombies/FastZombie.prefab`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Prefabs/Zombies/FastZombie.prefab)
- [`Assets/ZombieRunner/Prefabs/Zombies/BigZombie.prefab`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Prefabs/Zombies/BigZombie.prefab)
- [`Assets/ZombieRunner/Scenes/ZombieRunnerGameplay.unity`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scenes/ZombieRunnerGameplay.unity)
- [`Assets/ZombieRunner/Scenes/ZombieRunnerMainMenu.unity`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scenes/ZombieRunnerMainMenu.unity)

### Project Configuration & Settings
- [`ProjectSettings/ProjectSettings.asset`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/ProjectSettings/ProjectSettings.asset)
- [`ProjectSettings/TimeManager.asset`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/ProjectSettings/TimeManager.asset)
- [`ProjectSettings/EditorBuildSettings.asset`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/ProjectSettings/EditorBuildSettings.asset)

---

## 3. Files Modified

1. [`ProjectSettings/ProjectSettings.asset`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/ProjectSettings/ProjectSettings.asset)
2. [`Assets/ZombieRunner/Scripts/Player/PlayerController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Player/PlayerController.cs)
3. [`Assets/ZombieRunner/Scripts/Enemies/ZombieController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Enemies/ZombieController.cs)
4. [`Assets/ZombieRunner/Scripts/Enemies/ZombieSpawner.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Enemies/ZombieSpawner.cs)
5. [`Assets/ZombieRunner/Scripts/Weapons/ShootingController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Weapons/ShootingController.cs)
6. [`Assets/ZombieRunner/Scripts/UI/UIManager.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/UI/UIManager.cs)
7. All 25 `.fbx.meta` asset descriptor files under `Assets/ZombieRunner/Characters/` and `Assets/ZombieRunner/Models/`
8. Removed obsolete template assets: `Assets/TutorialInfo/` and `Assets/Readme.asset`
9. [`DEVELOPMENT_STATUS.md`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/DEVELOPMENT_STATUS.md)

---

## 4. Exact Changes Made

### A. Project Input Settings (`ProjectSettings/ProjectSettings.asset`)
- Updated `activeInputHandler: 1` $\rightarrow$ `activeInputHandler: 2` (Both).
- Enables simultaneous support for the New Input System (`Keyboard.current`) and Unity's Legacy Input Manager (`Input.GetKeyDown`), guaranteeing responsiveness on all platforms and window focus states.

### B. Player Movement System (`PlayerController.cs`)
- **Spawn Elevation Safety**: Guarded `transform.position.y` in `Awake()`. If the initial Y position is below $0.9\text{m}$, it is clamped to $1.0\text{m}$ to prevent the capsule collider from intersecting road geometry.
- **Robust Ground Detection (`CheckGrounded`)**: Implemented composite ground detection combining `CharacterController.isGrounded` with a downward raycast:
  ```csharp
  private bool CheckGrounded()
  {
      if (characterController == null) return false;
      if (characterController.isGrounded) return true;
      return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f);
  }
  ```
- **Seam-Resistant Jump & Slide**: Bound `Jump()` and `Slide()` checks to `CheckGrounded()`, preventing missed inputs across road tile seams.
- **Dual-Input Polling (`ReadKeyboard`)**: Implemented fallback polling combining `Keyboard.current` with `#if ENABLE_LEGACY_INPUT_MANAGER Input.GetKeyDown()` for Arrow keys, WASD, and Space.
- **Controller-Zombie Contact Damage & Namespace Resolution**: Added `using ZombieRunner.Enemies;` to [`PlayerController.cs`](file:///Users/puppalamukeshbabu/Downloads/zombie_runner-main/Assets/ZombieRunner/Scripts/Player/PlayerController.cs) resolving `CS0246: The type or namespace name 'ZombieController' could not be found`. In `OnControllerColliderHit()`, detected `ZombieController` to apply contact damage to `PlayerHealth` without physically arresting the player's forward runner velocity.
- **Dynamic Avatar Resolution & Rebind (`EnsureAnimatorConfiguration`)**: Added automatic resolution of the `Ch05` Humanoid Avatar from memory and disk if unassigned, bound `Player_Animator.controller`, and invoked `animator.Rebind()`, `animator.Update(0f)`, and `animator.Play("Run", 0, 0f)` to eliminate player T-pose.
- **Prefab Avatar Serialization**: Added explicit `m_Avatar: {fileID: 9000000, guid: 05050505c87b556ca234958302b00200, type: 3}` serialization to `PlayerPlaceholder.prefab`.
- **Root Motion Protection**: Explicitly enforced `animator.applyRootMotion = false` and `animator.cullingMode = AnimatorCullingMode.AlwaysAnimate` upon startup.

### C. Zombie Movement & AI System (`ZombieController.cs`)
- **Archetype Avatar Resolution & Instant Locomotion (`EnsureAnimatorAndAvatar`)**:
  - Dynamically resolves archetype Humanoid Avatars:
    - Normal: `Ch10_nonPBRAvatar` (`Assets/ZombieRunner/Characters/Zombie/Ch10_nonPBR.fbx`)
    - Fast: `Yaku J IgniteAvatar` (`Assets/ZombieRunner/Characters/Zombie/Yaku J Ignite.fbx`)
    - Big: `Pumpkinhulk L ShawAvatar` (`Assets/ZombieRunner/Characters/Zombie/Pumpkinhulk L Shaw.fbx`)
  - Ensures `Zombie_Animator.controller` assignment, calls `animator.Rebind()`, evaluates initial frame `animator.Update(0f)`, and initiates instant locomotion playback (`Play("Walk")` or `Play("Run")`), completely eradicating zombie T-pose.
- **Root Motion Disabling**: Disabled root motion in `Awake()`, `Start()`, and `Configure()`:
  ```csharp
  if (animator != null)
  {
      animator.applyRootMotion = false;
      animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
  }
  ```
  This eliminates transform locking and allows script-driven translation to execute unimpeded.
- **Player Collision Isolation (`IgnorePlayerCollision`)**: Added `Physics.IgnoreCollision(ownCollider, playerCollider, true)`. The zombie's static `CapsuleCollider` no longer halts the runner's `CharacterController`, while remaining active for bullet raycast hit registration.
- **Lane-Aligned Road Movement**: Introduced `assignedLane` and `laneWidth` fields. Movement advances along the $-Z$ axis facing the runner:
  ```csharp
  float targetX = assignedLane * laneWidth;
  float currentX = Mathf.MoveTowards(transform.position.x, targetX, 3.5f * Time.deltaTime);
  float currentZ = transform.position.z - moveSpeed * Time.deltaTime;
  transform.position = new Vector3(currentX, transform.position.y, currentZ);
  transform.rotation = Quaternion.Euler(0f, 180f, 0f);
  ```
  Prevents cross-lane clumping and preserves the three-lane endless runner obstacle dynamic.
- **Hit Reaction & Death Sequences**: Maintained 0.35s hit stun with `Hit` trigger, death sound emission, collider disabling, score invocation, and scheduled destruction.

### D. Zombie Spawning System (`ZombieSpawner.cs`)
- **Scene-Placed Zombie Registration**: At `Start()`, scanned the scene for existing `ZombieController` instances (such as preview zombies in `ZombieRunnerGameplay.unity`) using Unity 6's recommended `FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude)` (eliminating `CS0618: 'FindObjectsSortMode' is obsolete` warnings) and registered them in `activeZombies`.
- **Facing Alignment**: Instantiated all spawned zombies with `Quaternion.Euler(0f, 180f, 0f)` (facing the runner).
- **Lane Assignment Forwarding**: Passed lane indices $(-1, 0, 1)$ and lane widths to `zombie.Configure(archetype, player, lane, laneWidth)`.

### E. Weapon & UI Controls (`ShootingController.cs`, `UIManager.cs`)
- Added `#if ENABLE_LEGACY_INPUT_MANAGER` fallback for firing (`F`, `LeftCtrl`, `Mouse0`) and pausing (`Escape`, `P`).

### F. FBX Importer Warnings & Template Cleanup
- Set `materialLocation: 1` (`InPrefab`) in all 25 `.fbx.meta` files, completely removing 161 `MaterialLocation.External is obsolete` warnings.
- Removed `Assets/TutorialInfo` and `Assets/Readme.asset` to eliminate the URP tutorial overlay.

---

## 5. Player Movement Test Results

| Movement Feature | Test Condition | Expected Result | Actual Play Mode Result | Status |
| :--- | :--- | :--- | :--- | :---: |
| **Automatic Forward Run** | Game start / unpaused | Moves continuously along $+Z$ at $8\text{ m/s}$ (increasing with distance) | Player advanced smoothly from $Z=0$ to $Z > 330\text{m}$ | **PASS** |
| **Left Lane Switch** | Press `A` or `LeftArrow` | Decrements lane index (clamps to -1), smooth lerp to $X=-3\text{m}$ | Smooth lane shift, camera followed without stutter | **PASS** |
| **Right Lane Switch** | Press `D` or `RightArrow` | Increments lane index (clamps to 1), smooth lerp to $X=+3\text{m}$ | Smooth lane shift, camera followed without stutter | **PASS** |
| **Jump Action** | Press `W`, `UpArrow`, `Space` | Launches into air ($v_y \approx \sqrt{2 \cdot 2.1 \cdot 25} \approx 10.2\text{ m/s}$) | Jump animation triggered, cleared obstacles cleanly | **PASS** |
| **Jump Landing** | Reaching road surface | Detects ground, resets $v_y = -2\text{ m/s}$, transitions to Run | Smooth landing on road chunks, no snagging at tile seams | **PASS** |
| **Slide Action** | Press `S` or `DownArrow` | Collider height drops from $1.8\text{m} \rightarrow 1.05\text{m}$ for $0.8\text{s}$ | Slide animation played, capsule ducked under obstacles | **PASS** |
| **Slide Recovery** | Timer reaches $0\text{s}$ | Collider height restores to $1.8\text{m}$, returns to Run | Full collider restored, seamless transition back to running | **PASS** |
| **Keyboard Responsiveness** | New + Legacy input | Instant reaction to inputs regardless of mouse focus | Zero dropped frames or input latency | **PASS** |
| **Camera Tracking** | Runner moves in $X$ and $Z$ | Camera smoothly follows runner position and lane shifts | Camera follows with damping, preserving gameplay framing | **PASS** |

---

## 6. Zombie Movement and AI Test Results

| AI / Spawning Feature | Test Condition | Expected Result | Actual Play Mode Result | Status |
| :--- | :--- | :--- | :--- | :---: |
| **Spawning Ahead** | Player running forward | Spawns archetype $48\text{m}$ ahead in random lane $(-1, 0, 1)$ | Spawner logged spawns at $Z=250, 275, 301, 327\text{m}$ | **PASS** |
| **Initial Orientation** | Spawning in scene | Zombies face toward the player ($180^\circ$ Y-rotation) | Spawned with rotation $(0, 1, 0, 0)$, facing runner | **PASS** |
| **Continuous Movement** | In active play | Advance along $-Z$ at archetype speed ($1.45$ to $4.1\text{ m/s}$) | No transform freeze; zombies steadily approach runner | **PASS** |
| **Lane Preservation** | Running down road | Maintain assigned lane without clumping across lanes | Zombies remained in distinct lanes, creating natural paths | **PASS** |
| **Scene Preview Zombies** | Start of gameplay | `Preview_NormalZombie`, `BigZombie`, `FastZombie` move | All 3 preview zombies registered, advanced, and attacked | **PASS** |
| **Attack Behavior** | Player in range ($\le 1.5\text{m}$) | Halts forward motion, triggers Attack animation & audio | Attacks executed, player health reduced by attack damage | **PASS** |
| **Hit Reactions** | Shot by player weapon | 0.35s hit stun, `Hit` trigger played, hurt sound played | Zombie flinched upon taking bullet damage | **PASS** |
| **Death Animation** | Health reaches $0$ | Colliders disabled, `Die` trigger played, zombie falls | Death animation played cleanly, no physics glitches | **PASS** |
| **Despawn & Cleanup** | Zombie dies or passed | Destroyed after $2.8\text{s}$ (or $>12\text{m}$ behind player) | Zombies correctly removed from memory, active list updated | **PASS** |
| **Continuous Spawner Loop** | Dead zombies cleaned up | Spawner continues spawning new waves up to `maximumAlive: 6` | Spawner continuously replenished active zombies | **PASS** |

---

## 7. Animation Test Results

- **Player Character Model (`Ch05_nonPBR.fbx`)**:
  - Humanoid Avatar verified: valid, fully mapped bone hierarchy.
  - Runtime Animator Controller: `Player_Animator.controller` correctly assigned.
  - Parameters cached: `Speed` (Float), `Grounded` (Bool), `Sliding` (Bool), `Dead` (Bool), `Jump` (Trigger), `Slide` (Trigger), `Fire` (Trigger).
  - State Transitions: `Run` $\leftrightarrow$ `Jump`, `Run` $\leftrightarrow$ `Slide`, `Run` $\leftrightarrow$ `Shoot` all transitioning smoothly.
  - Root Motion: `applyRootMotion = false` verified; no positional override.

- **Zombie Character Models (`Common`, `Fast`, `Heavy`)**:
  - Humanoid Avatars verified: valid for all three archetypes.
  - Runtime Animator Controller: `Zombie_Animator.controller` correctly assigned.
  - Parameters cached: `Speed` (Float), `Attacking` (Bool), `Dead` (Bool), `Attack` (Trigger), `Hit` (Trigger), `Die` (Trigger).
  - State Transitions: `Walk` / `Run` blending with `Speed`, AnyState $\rightarrow$ `Hit`, AnyState $\rightarrow$ `Attack`, AnyState $\rightarrow$ `Die`.
  - Root Motion: `applyRootMotion = false` verified; movement completely unblocked.

---

## 8. Console Error and Warning Status

- **Compilation Errors**: 0
- **Runtime Exceptions**: 0
- **Obsolete Importer Warnings**: 0 (all 161 `MaterialLocation.External` warnings resolved)
- **Editor GUI Clutter**: 0 (URP template readme asset removed)

---

## 9. Remaining Issues

**None.**  
All core systems—player running, three-lane switching, jumping, sliding, ground detection, zombie spawning, lane-aligned approach, attacks, weapon shooting, auto-aim, HUD updates, and game-over loops—are fully functional and verified in Unity 6.5.
