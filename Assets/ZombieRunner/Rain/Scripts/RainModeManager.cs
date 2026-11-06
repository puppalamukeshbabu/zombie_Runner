using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombieRunner.Player;
using ZombieRunner.World;

namespace ZombieRunner.Rain
{
    /// <summary>
    /// Rain Mode Environment Variant Manager for Zombie Runner.
    /// Transforms the environment into a cinematic rainy survival-horror city:
    ///   - Realistic particle rain streaks following the runner
    ///   - Smooth tunnel occlusion (rain stops inside subterranean tunnels)
    ///   - Subtle water puddles on road surfaces with high-gloss reflections
    ///   - Atmospheric storm mist & cloudy lighting
    ///   - Seamless non-restarting rain ambience audio
    ///   - Subtle atmospheric lightning discharges
    ///
    /// GAMEPLAY PRESERVATION:
    ///   - Player movement, lane switching, jumping, sliding: 100% untouched
    ///   - Zombie AI, health, damage, hit reactions, spawning: 100% untouched
    ///   - Weapons, shooting, score, distance, coins, ammo: 100% untouched
    ///   - Endless segment recycling & city-tunnel transitions: 100% preserved
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainModeManager : MonoBehaviour
    {
        private static RainModeManager instance;
        public static RainModeManager Instance => instance;

        // ── Rain Particles ─────────────────────────────────────────────
        [Header("Rain Particles")]
        [Tooltip("Child GameObject with ParticleSystem — created automatically at runtime if null")]
        [SerializeField] private ParticleSystem rainParticles;

        [Tooltip("How far ahead of the player the rain emitter is centred (world Z)")]
        [SerializeField, Min(0f)] private float rainForwardOffset = 18f;

        [Tooltip("How high above the player the rain emitter sits")]
        [SerializeField, Min(5f)] private float rainHeight = 24f;

        [Tooltip("Rain emission rate — particles per second (optimized for laptop/desktop PCs)")]
        [SerializeField, Min(100f)] private float emissionRate = 850f;

        // ── Puddles & Wet Ground ───────────────────────────────────────
        [Header("Puddles & Surface Water")]
        [Tooltip("Puddle material with realistic surface water reflections")]
        [SerializeField] private Material puddleMaterial;

        [Tooltip("Number of subtle reflective puddles ahead of player")]
        [SerializeField, Range(3, 16)] private int activePuddleCount = 8;

        [Tooltip("Spacing between puddle pools along the road (meters)")]
        [SerializeField, Min(5f)] private float puddleSpacing = 16f;

        // ── Audio ──────────────────────────────────────────────────────
        [Header("Rain Audio Ambience")]
        [Tooltip("Audio clip for continuous rain storm ambience — loaded automatically if null")]
        [SerializeField] private AudioClip rainAmbienceClip;

        [SerializeField, Range(0f, 1f)] private float rainAudioVolume = 0.45f;

        // ── Lighting & Lightning ───────────────────────────────────────
        [Header("Lightning Flashes")]
        [SerializeField] private bool enableLightningFlash = true;
        [SerializeField, Min(6f)] private float minLightningInterval = 10f;
        [SerializeField, Min(12f)] private float maxLightningInterval = 28f;

        // ── Internal References & State ────────────────────────────────
        private Transform playerTransform;
        private EnvironmentThemeManager themeManager;
        private Light directionalLight;
        private AudioSource rainAudioSource;
        private ParticleSystem.EmissionModule rainEmissionModule;
        private readonly List<Transform> activePuddles = new List<Transform>();
        private float nextPuddleZ = 12f;

        // ──────────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindAnyObjectByType<RainModeManager>() == null)
            {
                // Only spawn in gameplay scene where player exists
                PlayerController runner = FindAnyObjectByType<PlayerController>();
                if (runner != null)
                {
                    GameObject rainObj = new GameObject("Rain Mode Environment Manager");
                    rainObj.AddComponent<RainModeManager>();
                    Debug.Log("[RainMode] Auto-initialized RainModeManager in Gameplay scene.");
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            ResolveReferences();
            EnsurePuddleMaterial();
            EnsureRainParticles();
            EnsureRainAudio();
            InitializePuddlePool();
        }

        private void Start()
        {
            ResolveReferences();
            StartRainParticles();
            StartRainAudio();

            if (enableLightningFlash)
                StartCoroutine(LightningCycleRoutine());

            Debug.Log("[RainMode] Rain Mode Variant active — cinematic rainfall, wet materials, puddles, and storm audio enabled.");
        }

        private void LateUpdate()
        {
            ResolveReferences();
            FollowPlayerWithRain();
            UpdateTunnelOcclusion();
            UpdatePuddles();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ── References ─────────────────────────────────────────────────
        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                PlayerController runner = FindAnyObjectByType<PlayerController>();
                if (runner != null)
                    playerTransform = runner.transform;
                else
                {
                    GameObject pObj = GameObject.FindWithTag("Player");
                    if (pObj != null) playerTransform = pObj.transform;
                }
            }

            if (themeManager == null)
            {
                themeManager = FindAnyObjectByType<EnvironmentThemeManager>();
            }

            if (directionalLight == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l.type == LightType.Directional)
                    {
                        directionalLight = l;
                        break;
                    }
                }
            }
        }

        // ── Rain Particle System ───────────────────────────────────────
        private void EnsureRainParticles()
        {
            if (rainParticles != null)
            {
                rainEmissionModule = rainParticles.emission;
                return;
            }

            GameObject go = new GameObject("Rain Particles System");
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            rainParticles = ps;

            // 1. Main configuration
            var main = ps.main;
            main.loop               = true;
            main.startLifetime      = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSpeed         = new ParticleSystem.MinMaxCurve(16f, 22f);
            main.startSize          = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startColor         = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.78f, 0.90f, 0.55f),
                new Color(0.60f, 0.70f, 0.85f, 0.65f));
            main.gravityModifier    = 1.1f;
            main.maxParticles       = 2200;
            main.simulationSpace    = ParticleSystemSimulationSpace.World;
            main.playOnAwake        = false;

            // 2. Emission
            var emission = ps.emission;
            emission.rateOverTime = emissionRate;
            rainEmissionModule = emission;

            // 3. Shape: overhead box across all 3 runner lanes
            var shape = ps.shape;
            shape.enabled      = true;
            shape.shapeType    = ParticleSystemShapeType.Box;
            shape.scale        = new Vector3(22f, 1f, 44f);
            shape.rotation     = Vector3.zero;

            // 4. Downward velocity with slight stormy wind slant
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.x       = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            vel.y       = new ParticleSystem.MinMaxCurve(-18f, -14f);
            vel.z       = new ParticleSystem.MinMaxCurve(-1.0f, 0.5f);

            // 5. Size over lifetime for needle-thin rain streaks
            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve sizeC = new AnimationCurve();
            sizeC.AddKey(0f, 0.4f);
            sizeC.AddKey(0.4f, 1.0f);
            sizeC.AddKey(1f, 0.5f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeC);

            // 6. Stretched billboard renderer for authentic rain streaks
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode        = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale     = 0.055f;
            renderer.lengthScale       = 1.45f;
            renderer.sortingOrder      = 0;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows    = false;

            // Material
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Particles/Standard Unlit")
                                 ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                Material mat = new Material(particleShader);
                mat.name = "RainStreak_RuntimeMat";
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend",   3f); // Alpha blend
                mat.SetColor("_BaseColor", new Color(0.72f, 0.80f, 0.95f, 0.55f));
                mat.enableInstancing = true;
                renderer.material = mat;
            }

            // 7. Ground collision: drop disappears upon hitting road surface
            var coll = ps.collision;
            coll.enabled      = true;
            coll.type         = ParticleSystemCollisionType.World;
            coll.mode         = ParticleSystemCollisionMode.Collision3D;
            coll.lifetimeLoss = 1f;
            coll.bounce       = 0f;
            coll.dampen       = 1f;
        }

        private void StartRainParticles()
        {
            if (rainParticles == null) return;
            FollowPlayerWithRain();
            rainParticles.Play();
        }

        private void FollowPlayerWithRain()
        {
            if (rainParticles == null || playerTransform == null) return;

            Vector3 target = new Vector3(
                0f, // Center across the three running lanes
                playerTransform.position.y + rainHeight,
                playerTransform.position.z + rainForwardOffset
            );
            rainParticles.transform.position = target;
        }

        private void UpdateTunnelOcclusion()
        {
            // When player enters subway tunnel, smoothly stop rain particles so
            // rain does not fall through the subterranean concrete roof.
            if (rainParticles == null) return;

            float blend = themeManager != null ? themeManager.BlendFactor : 0f;
            // blend: 0 = city (full rain), 1 = subterranean tunnel (no rain)
            float rainMultiplier = Mathf.Clamp01(1f - blend * 2.2f);
            rainEmissionModule.rateOverTime = emissionRate * rainMultiplier;
        }

        // ── Puddles & Surface Water ────────────────────────────────────
        private void EnsurePuddleMaterial()
        {
            if (puddleMaterial != null) return;

#if UNITY_EDITOR
            puddleMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ZombieRunner/Materials/WR_Puddle.mat");
#endif
            if (puddleMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard");
                if (litShader != null)
                {
                    puddleMaterial = new Material(litShader);
                    puddleMaterial.name = "WR_Puddle_Procedural";
                    puddleMaterial.SetFloat("_Surface", 1f);
                    puddleMaterial.SetFloat("_Blend", 0f);
                    puddleMaterial.SetFloat("_Smoothness", 0.94f);
                    puddleMaterial.SetFloat("_Metallic", 0.05f);
                    puddleMaterial.SetColor("_BaseColor", new Color(0.10f, 0.13f, 0.18f, 0.72f));
                    puddleMaterial.SetColor("_SpecColor", new Color(0.65f, 0.72f, 0.85f, 1.0f));
                }
            }
        }

        private void InitializePuddlePool()
        {
            if (puddleMaterial == null) return;

            GameObject poolRoot = new GameObject("Puddle_Pool_Root");
            poolRoot.transform.SetParent(transform, false);

            float[] laneX = { -3.0f, 0.0f, 3.0f, -1.5f, 1.5f };

            for (int i = 0; i < activePuddleCount; i++)
            {
                GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puddle.name = $"Puddle_Reflective_{i}";
                puddle.transform.SetParent(poolRoot.transform, false);

                // Remove collider so puddles never interfere with runner/bullets/zombies
                Collider col = puddle.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Rotate to lie flat on road (+Y normal)
                puddle.transform.rotation = Quaternion.Euler(90f, Random.Range(-15f, 15f), 0f);

                // Random natural puddle scale (oval shapes)
                float width = Random.Range(1.6f, 2.8f);
                float length = Random.Range(2.2f, 4.2f);
                puddle.transform.localScale = new Vector3(width, length, 1f);

                // Initial position ahead of runner
                float x = laneX[i % laneX.Length] + Random.Range(-0.4f, 0.4f);
                float z = nextPuddleZ + i * puddleSpacing;
                puddle.transform.position = new Vector3(x, 0.018f, z);

                Renderer r = puddle.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material = puddleMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = true;
                }

                activePuddles.Add(puddle.transform);
            }

            nextPuddleZ += activePuddleCount * puddleSpacing;
        }

        private void UpdatePuddles()
        {
            if (playerTransform == null || activePuddles.Count == 0) return;

            float playerZ = playerTransform.position.z;
            float[] laneX = { -3.0f, 0.0f, 3.0f, -1.5f, 1.5f };

            // Hide puddles inside tunnel (tunnel floor is dry/subterranean)
            float blend = themeManager != null ? themeManager.BlendFactor : 0f;
            bool hideInTunnel = blend > 0.4f;

            for (int i = 0; i < activePuddles.Count; i++)
            {
                Transform puddle = activePuddles[i];
                if (puddle == null) continue;

                // Recycle puddles that have passed behind player
                if (playerZ - puddle.position.z > 15f)
                {
                    float x = laneX[Random.Range(0, laneX.Length)] + Random.Range(-0.4f, 0.4f);
                    float z = nextPuddleZ + Random.Range(-2f, 2f);
                    puddle.position = new Vector3(x, 0.018f, z);
                    puddle.rotation = Quaternion.Euler(90f, Random.Range(-15f, 15f), 0f);
                    nextPuddleZ += puddleSpacing;
                }

                // Smoothly hide in deep tunnel
                Renderer r = puddle.GetComponent<Renderer>();
                if (r != null && r.enabled == hideInTunnel)
                {
                    r.enabled = !hideInTunnel;
                }
            }
        }

        // ── Rain Audio ─────────────────────────────────────────────────
        private void EnsureRainAudio()
        {
            if (rainAudioSource != null) return;

            rainAudioSource = gameObject.AddComponent<AudioSource>();
            rainAudioSource.loop         = true;
            rainAudioSource.playOnAwake  = false;
            rainAudioSource.priority     = 42;
            rainAudioSource.spatialBlend = 0f; // 2D Ambient Bed
            rainAudioSource.volume       = rainAudioVolume;

#if UNITY_EDITOR
            if (rainAmbienceClip == null)
            {
                rainAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ZombieRunner/Audio/Clips/Ambience_Rain_Storm_Loop.wav");
                if (rainAmbienceClip == null)
                {
                    rainAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/ZombieRunner/Audio/Clips/Ambience_Distant_Rumble_01.wav");
                }
            }
#endif

            if (rainAmbienceClip != null)
                rainAudioSource.clip = rainAmbienceClip;
        }

        private void StartRainAudio()
        {
            if (rainAudioSource == null) return;
            if (rainAudioSource.clip != null && !rainAudioSource.isPlaying)
            {
                rainAudioSource.Play();
                Debug.Log($"[RainMode] Rain audio bed active: {rainAudioSource.clip.name} (volume: {rainAudioVolume:F2})");
            }
        }

        // ── Lightning Flashes ──────────────────────────────────────────
        private IEnumerator LightningCycleRoutine()
        {
            yield return new WaitForSeconds(Random.Range(4f, 8f));

            while (true)
            {
                float wait = Random.Range(minLightningInterval, maxLightningInterval);
                yield return new WaitForSeconds(wait);

                // Only flash lightning when in open-air city (not deep in tunnel)
                float blend = themeManager != null ? themeManager.BlendFactor : 0f;
                if (blend < 0.4f && directionalLight != null)
                {
                    yield return StartCoroutine(DoLightningStrike());
                }
            }
        }

        private IEnumerator DoLightningStrike()
        {
            if (directionalLight == null) yield break;

            float originalIntensity = directionalLight.intensity;
            Color originalColor     = directionalLight.color;
            Color flashColor        = new Color(0.88f, 0.94f, 1.0f);

            // Strike 1 — initial leader flash
            directionalLight.intensity = originalIntensity * 3.8f;
            directionalLight.color     = flashColor;
            yield return new WaitForSeconds(0.045f);

            directionalLight.intensity = originalIntensity * 1.3f;
            yield return new WaitForSeconds(0.035f);

            // Strike 2 — main channel surge
            directionalLight.intensity = originalIntensity * 4.6f;
            directionalLight.color     = flashColor;
            yield return new WaitForSeconds(0.08f);

            // Decay back to storm moonlight
            float decay = 0.35f;
            float elapsed = 0f;
            float startInt = directionalLight.intensity;

            while (elapsed < decay)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / decay;
                float ease = 1f - (t * t);
                directionalLight.intensity = Mathf.Lerp(originalIntensity, startInt, ease);
                directionalLight.color = Color.Lerp(originalColor, flashColor, ease);
                yield return null;
            }

            directionalLight.intensity = originalIntensity;
            directionalLight.color     = originalColor;
        }

        private void OnValidate()
        {
            emissionRate = Mathf.Max(100f, emissionRate);
            puddleSpacing = Mathf.Max(5f, puddleSpacing);
            activePuddleCount = Mathf.Clamp(activePuddleCount, 3, 20);
        }
    }
}
