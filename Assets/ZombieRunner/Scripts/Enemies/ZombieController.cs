using System;
using UnityEngine;
using ZombieRunner.Audio;
using ZombieRunner.Player;
using ZombieRunner.Utilities;

namespace ZombieRunner.Enemies
{
    public enum ZombieArchetype
    {
        Normal,
        Fast,
        Big
    }

    /// <summary>Configurable enemy movement, health, attacking, and death behavior.</summary>
    [DisallowMultipleComponent]
    public sealed class ZombieController : MonoBehaviour, IDamageable
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int AttackingParameter = Animator.StringToHash("Attacking");
        private static readonly int DeadParameter = Animator.StringToHash("Dead");
        private static readonly int AttackParameter = Animator.StringToHash("Attack");
        private static readonly int HitParameter = Animator.StringToHash("Hit");
        private static readonly int DieParameter = Animator.StringToHash("Die");

        [SerializeField] private ZombieArchetype archetype;
        [SerializeField] private ZombieConfig config;
        [SerializeField] private Transform player;
        [SerializeField] private Animator animator;
        [SerializeField, Min(1f)] private float maximumHealth = 3f;
        [SerializeField, Min(0f)] private float currentHealth = 3f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0f)] private float destroyDelay = 2.8f;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private int assignedLane = 0;
        [SerializeField] private float laneWidth = 3f;

        private ZombieSoundEmitter soundEmitter;
        private float nextAttackTime;
        private float hitStunTimer;
        private bool dead;
        private bool playerCollisionIgnored;
        private bool hasSpeedParameter;
        private bool hasAttackingParameter;
        private bool hasDeadParameter;
        private bool hasAttackParameter;
        private bool hasHitParameter;
        private bool hasDieParameter;

        public event Action<ZombieController> Died;

        public ZombieConfig Config => config;
        public ZombieArchetype Archetype => archetype;
        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public bool IsDead => dead;
        public int AssignedLane => assignedLane;

        private void Awake()
        {
            EnsurePlayerReference();
            assignedLane = Mathf.Clamp(Mathf.RoundToInt(transform.position.x / Mathf.Max(0.1f, laneWidth)), -1, 1);
            soundEmitter = GetComponent<ZombieSoundEmitter>();
            EnsureVisuals();
            ApplyArchetypeSettings();
            EnsureAnimatorAndAvatar();
            currentHealth = maximumHealth;
            IgnorePlayerCollision();
        }

        private void Start()
        {
            IgnorePlayerCollision();
            EnsureAnimatorAndAvatar();
            StartCoroutine(DelayedPlayLocomotion());
        }

        private System.Collections.IEnumerator DelayedPlayLocomotion()
        {
            yield return null; // wait one frame for Unity to initialise humanoid avatar
            yield return null; // second frame for safety
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                string clip = archetype == ZombieArchetype.Fast ? "Run" : "Walk";
                if (animator.HasState(0, Animator.StringToHash(clip)))
                    animator.Play(clip, 0, UnityEngine.Random.Range(0f, 1f));
                else if (animator.HasState(0, Animator.StringToHash("Idle")))
                    animator.Play("Idle", 0, UnityEngine.Random.Range(0f, 1f));
                UpdateAnimator(moveSpeed, false);
                Debug.Log($"[ZombieRunner] Zombie '{gameObject.name}': DelayedPlayLocomotion -> Play('{(archetype == ZombieArchetype.Fast ? "Run" : "Walk")}') after 2 frames");
            }
        }

        private void EnsureAnimatorAndAvatar()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                if (animator.avatar == null || !animator.avatar.isValid)
                {
                    Animator[] allAnimators = GetComponentsInChildren<Animator>(true);
                    foreach (Animator a in allAnimators)
                    {
                        if (a.avatar != null && a.avatar.isValid)
                        {
                            animator.avatar = a.avatar;
                            break;
                        }
                    }

                    if (animator.avatar == null || !animator.avatar.isValid)
                    {
                        string searchPattern = archetype == ZombieArchetype.Fast ? "Yaku" : (archetype == ZombieArchetype.Big ? "Pumpkin" : "Ch10");
                        foreach (Avatar av in Resources.FindObjectsOfTypeAll<Avatar>())
                        {
                            if (av != null && av.isValid && av.name.Contains(searchPattern))
                            {
                                animator.avatar = av;
                                break;
                            }
                        }
                    }

#if UNITY_EDITOR
                    if (animator.avatar == null || !animator.avatar.isValid)
                    {
                        string fbxPath = archetype == ZombieArchetype.Fast
                            ? "Assets/ZombieRunner/Characters/Zombie/Yaku J Ignite.fbx"
                            : (archetype == ZombieArchetype.Big
                                ? "Assets/ZombieRunner/Characters/Zombie/Pumpkinhulk L Shaw.fbx"
                                : "Assets/ZombieRunner/Characters/Zombie/Ch10_nonPBR.fbx");
                        foreach (UnityEngine.Object obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                        {
                            if (obj is Avatar av && av.isValid)
                            {
                                animator.avatar = av;
                                break;
                            }
                        }
                    }
#endif
                }

                if (animator.runtimeAnimatorController == null)
                {
#if UNITY_EDITOR
                    animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ZombieRunner/Animations/Zombie_Animator.controller");
#endif
                }

                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
            }
            CacheAnimatorParameters();
            // Note: locomotion clip is started via DelayedPlayLocomotion() coroutine in Start()
            // to give Unity one frame to initialise humanoid avatar retargeting
        }

        private void IgnorePlayerCollision()
        {
            if (playerCollisionIgnored) return;
            EnsurePlayerReference();
            if (player == null) return;

            Collider ownCollider = GetComponent<Collider>();
            Collider playerCollider = player.GetComponent<Collider>();
            if (ownCollider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(ownCollider, playerCollider, true);
                playerCollisionIgnored = true;
            }
        }

        private void EnsurePlayerReference()
        {
            if (player != null) return;

            PlayerController runner = FindAnyObjectByType<PlayerController>();
            if (runner != null)
            {
                player = runner.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }
            if (player != null && !playerCollisionIgnored)
                IgnorePlayerCollision();
        }

        private void EnsureVisuals()
        {
            Renderer anyRenderer = GetComponentInChildren<Renderer>(true);
            if (anyRenderer != null)
            {
                if (!anyRenderer.gameObject.activeSelf)
                    anyRenderer.gameObject.SetActive(true);
                if (!anyRenderer.enabled)
                    anyRenderer.enabled = true;
            }
            else
            {
                // Fallback procedural capsule mesh if 3D model renderer is absent
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Visual - Fallback Capsule";
                fallback.transform.SetParent(transform, false);
                fallback.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                fallback.transform.localRotation = Quaternion.identity;
                fallback.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                Collider col = fallback.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        private void Update()
        {
            if (dead)
                return;

            EnsurePlayerReference();
            if (player == null)
                return;

            if (hitStunTimer > 0f)
            {
                hitStunTimer -= Time.deltaTime;
                UpdateAnimator(0f, false);
                return;
            }

            Vector3 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;
            if (distance <= attackRange)
            {
                UpdateAnimator(0f, true);
                TryAttack();
                return;
            }

            // Lane-aligned movement down the road towards the player
            float targetX = assignedLane * laneWidth;
            float currentX = Mathf.MoveTowards(transform.position.x, targetX, 3.5f * Time.deltaTime);
            float currentZ = transform.position.z - moveSpeed * Time.deltaTime;

            transform.position = new Vector3(currentX, transform.position.y, currentZ);
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            UpdateAnimator(moveSpeed, false);
        }

        public void Configure(ZombieArchetype type, Transform target, int lane = 0, float width = 3f)
        {
            archetype = type;
            assignedLane = Mathf.Clamp(lane, -1, 1);
            laneWidth = Mathf.Max(0.1f, width);
            if (target != null)
                player = target;
            else
                EnsurePlayerReference();

            soundEmitter = GetComponent<ZombieSoundEmitter>();
            EnsureVisuals();
            ApplyArchetypeSettings();
            EnsureAnimatorAndAvatar();
            currentHealth = maximumHealth;
            dead = false;
            hitStunTimer = 0f;
            playerCollisionIgnored = false;
            IgnorePlayerCollision();

            // Restart animation after Configure() resets the animator
            StartCoroutine(DelayedPlayLocomotion());
        }

        public void Configure(ZombieArchetype type, Transform target)
        {
            int inferredLane = Mathf.Clamp(Mathf.RoundToInt(transform.position.x / Mathf.Max(0.1f, laneWidth)), -1, 1);
            Configure(type, target, inferredLane, laneWidth);
        }

        public void TakeDamage(float amount)
        {
            if (dead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (enableDebugLogs)
                Debug.Log($"[ZombieRunner] {gameObject.name} ({archetype}) took {amount:F1} damage -> Health: {currentHealth:F1}/{maximumHealth:F1}");

            if (currentHealth <= 0f)
            {
                Die();
            }
            else
            {
                soundEmitter?.PlayHurt();
                hitStunTimer = 0.35f;
                if (animator != null && hasHitParameter)
                {
                    animator.ResetTrigger(HitParameter);
                    animator.SetTrigger(HitParameter);
                    if (enableDebugLogs)
                        Debug.Log($"[ZombieRunner] {gameObject.name} ({archetype}) triggered Hit reaction animation. Pausing movement for 0.35s.");
                }
                UpdateAnimator(0f, false);
            }
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + attackCooldown;
            soundEmitter?.PlayAttack();
            if (animator != null && hasAttackParameter)
                animator.SetTrigger(AttackParameter);
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.TakeDamage(attackDamage);
            else
                player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }

        private void Die()
        {
            if (dead)
                return;

            dead = true;
            hitStunTimer = 0f;
            soundEmitter?.PlayDeath();
            if (enableDebugLogs)
                Debug.Log($"[ZombieRunner] {gameObject.name} ({archetype}) died! Playing death animation (duration: {destroyDelay:F1}s).");

            if (animator != null)
            {
                if (hasSpeedParameter)
                    animator.SetFloat(SpeedParameter, 0f);
                if (hasAttackingParameter)
                    animator.SetBool(AttackingParameter, false);
                if (hasDieParameter)
                {
                    animator.ResetTrigger(DieParameter);
                    animator.SetTrigger(DieParameter);
                }
                if (hasDeadParameter)
                    animator.SetBool(DeadParameter, true);
            }
            AudioManager.Instance?.Play(GameAudioCue.ZombieDeath);
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider item in colliders)
                item.enabled = false;
            Died?.Invoke(this);
            Destroy(gameObject, destroyDelay);
        }

        private void CacheAnimatorParameters()
        {
            hasSpeedParameter = false;
            hasAttackingParameter = false;
            hasDeadParameter = false;
            hasAttackParameter = false;
            hasHitParameter = false;
            hasDieParameter = false;
            if (animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedParameter && parameter.type == AnimatorControllerParameterType.Float) hasSpeedParameter = true;
                else if (parameter.nameHash == AttackingParameter && parameter.type == AnimatorControllerParameterType.Bool) hasAttackingParameter = true;
                else if (parameter.nameHash == DeadParameter && parameter.type == AnimatorControllerParameterType.Bool) hasDeadParameter = true;
                else if (parameter.nameHash == AttackParameter && parameter.type == AnimatorControllerParameterType.Trigger) hasAttackParameter = true;
                else if (parameter.nameHash == HitParameter && parameter.type == AnimatorControllerParameterType.Trigger) hasHitParameter = true;
                else if (parameter.nameHash == DieParameter && parameter.type == AnimatorControllerParameterType.Trigger) hasDieParameter = true;
            }
        }

        private void UpdateAnimator(float speed, bool attacking)
        {
            if (animator == null)
                return;
            if (hasSpeedParameter) animator.SetFloat(SpeedParameter, speed);
            if (hasAttackingParameter) animator.SetBool(AttackingParameter, attacking);
        }

        private void ApplyArchetypeSettings()
        {
            if (config != null)
            {
                archetype = config.Archetype;
                maximumHealth = config.MaximumHealth;
                moveSpeed = config.MoveSpeed;
                attackDamage = config.AttackDamage;
                attackRange = config.AttackRange;
                attackCooldown = config.AttackCooldown;
                return;
            }

            switch (archetype)
            {
                case ZombieArchetype.Fast:
                    maximumHealth = 2f;
                    moveSpeed = 4.1f;
                    attackDamage = 8f;
                    attackCooldown = 1.05f;
                    break;
                case ZombieArchetype.Big:
                    maximumHealth = 8f;
                    moveSpeed = 1.45f;
                    attackDamage = 22f;
                    attackCooldown = 1.7f;
                    break;
                default:
                    maximumHealth = 3f;
                    moveSpeed = 2.5f;
                    attackDamage = 10f;
                    attackCooldown = 1.2f;
                    break;
            }
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
        }
    }
}
