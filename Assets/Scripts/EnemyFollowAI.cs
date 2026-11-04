using UnityEngine;

/// <summary>
/// Controla la IA de movimiento y persecución en el mapa para los enemigos de Santa Felipina.
/// Permite patrullaje/movimiento autónomo sutil y persecución activa hacia Enrique
/// hasta posicionarse a una distancia muy cercana (intimidación/contacto para combate).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyFollowAI : MonoBehaviour
{
    [Header("Rangos y Distancias de Persecución")]
    [Tooltip("Distancia a la que el enemigo detecta a Enrique y comienza a perseguirlo.")]
    [Range(2f, 15f)]
    public float detectionRadius = 6.0f;

    [Tooltip("Distancia muy cercana a la que el enemigo se detiene frente al jugador.")]
    [Range(0.6f, 3f)]
    public float stoppingDistance = 1.25f;

    [Tooltip("Distancia a partir de la cual el enemigo pierde de vista a Enrique y regresa a su puesto.")]
    [Range(5f, 25f)]
    public float leashRadius = 13.0f;

    [Tooltip("Radio del área de patrullaje/movimiento errante alrededor de su punto de spawn.")]
    [Range(0.5f, 5f)]
    public float wanderRadius = 1.8f;

    [Header("Velocidades de Desplazamiento")]
    [Tooltip("Velocidad de caminata al patrullar o regresar a su posición.")]
    [Range(1f, 5f)]
    public float walkSpeed = 2.0f;

    [Tooltip("Velocidad al perseguir activamente a Enrique.")]
    [Range(1.5f, 7f)]
    public float chaseSpeed = 3.4f;

    [Header("Comportamiento")]
    [Tooltip("Si está activo, el enemigo realiza pequeños paseos autónomos en su zona cuando Enrique está lejos.")]
    public bool enableWander = true;

    [Header("Referencias Visuales")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private Transform playerTransform;
    private PlayerController2D playerController;
    private BattleManager battleManager;

    private Vector2 spawnPosition;
    private Vector2 currentMoveVelocity;
    private Vector2 wanderTarget;
    private float wanderTimer = 0f;
    private float wanderWaitTimer = 0f;
    private bool isChasing = false;

    // Hashes de parámetros de Animator
    private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int AnimIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");

    private bool hasIsWalking = false;
    private bool hasIsRunning = false;
    private bool hasIsMoving = false;
    private bool hasSpeed = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();

        spawnPosition = transform.position;
        wanderTarget = spawnPosition;
    }

    private void Start()
    {
        CacheAnimatorParameters();
        LocatePlayerAndManagers();
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == AnimIsWalking) hasIsWalking = true;
            else if (p.nameHash == AnimIsRunning) hasIsRunning = true;
            else if (p.nameHash == AnimIsMoving) hasIsMoving = true;
            else if (p.nameHash == AnimSpeed) hasSpeed = true;
        }
    }

    private void LocatePlayerAndManagers()
    {
        if (playerTransform == null)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                playerTransform = playerGO.transform;
                playerController = playerGO.GetComponent<PlayerController2D>();
            }
        }

        if (battleManager == null)
        {
            battleManager = FindAnyObjectByType<BattleManager>();
        }
    }

    private void Update()
    {
        // 1. Congelar movimiento durante combate o si el jugador está en pausa/diálogo
        if (ShouldFreezeMovement())
        {
            currentMoveVelocity = Vector2.zero;
            UpdateVisualsAndAnimations(Vector2.zero, isWalking: false, isRunning: false);
            return;
        }

        // 2. Si no tenemos la referencia de Enrique, buscarla
        if (playerTransform == null)
        {
            LocatePlayerAndManagers();
            if (playerTransform == null) return;
        }

        Vector2 currentPos = transform.position;
        Vector2 playerPos = playerTransform.position;
        float distToPlayer = Vector2.Distance(currentPos, playerPos);
        float distToSpawn = Vector2.Distance(currentPos, spawnPosition);

        Vector2 desiredVelocity = Vector2.zero;
        bool isWalking = false;
        bool isRunning = false;

        // 3. Lógica de persecución
        if (distToPlayer <= detectionRadius)
        {
            // Enrique está dentro del radio de detección
            isChasing = true;
        }
        else if (isChasing && distToPlayer <= leashRadius && distToSpawn <= leashRadius)
        {
            // Continúa persiguiendo mientras no exceda el radio de escape
            isChasing = true;
        }
        else
        {
            // Enrique se alejó lo suficiente: perder rastro
            isChasing = false;
        }

        if (isChasing)
        {
            // Perseguir a Enrique
            if (distToPlayer > stoppingDistance)
            {
                Vector2 dir = (playerPos - currentPos).normalized;
                desiredVelocity = dir * chaseSpeed;
                isRunning = true;
                isWalking = false;
            }
            else
            {
                // Llegó a la distancia muy cercana (se planta frente a Enrique)
                desiredVelocity = Vector2.zero;
                isRunning = false;
                isWalking = false;

                // Mirar fijamente a Enrique
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = (playerPos.x < currentPos.x);
                }
            }
        }
        else
        {
            // 4. Lógica de patrulla / retorno a su puesto cuando no persigue
            if (distToSpawn > wanderRadius + 0.5f)
            {
                // Regresar a su zona de spawn
                Vector2 dir = (spawnPosition - currentPos).normalized;
                desiredVelocity = dir * walkSpeed;
                isWalking = true;
                isRunning = false;
            }
            else if (enableWander)
            {
                // Pequeños movimientos errantes en su área
                desiredVelocity = HandleWanderBehavior(currentPos, out isWalking);
                isRunning = false;
            }
            else
            {
                desiredVelocity = Vector2.zero;
            }
        }

        currentMoveVelocity = desiredVelocity;
        UpdateVisualsAndAnimations(currentMoveVelocity, isWalking, isRunning);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (ShouldFreezeMovement())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = currentMoveVelocity;
    }

    private Vector2 HandleWanderBehavior(Vector2 currentPos, out bool isWalking)
    {
        isWalking = false;

        if (wanderWaitTimer > 0f)
        {
            wanderWaitTimer -= Time.deltaTime;
            return Vector2.zero;
        }

        float distToTarget = Vector2.Distance(currentPos, wanderTarget);
        if (distToTarget <= 0.25f || wanderTimer <= 0f)
        {
            // Llegó al destino o expiró el temporizador: esperar en reposo
            wanderWaitTimer = Random.Range(2.5f, 5.0f);
            wanderTimer = Random.Range(3.0f, 6.0f);

            // Escoger nuevo punto en el radio de spawn
            Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPosition + randomOffset;
            return Vector2.zero;
        }

        wanderTimer -= Time.deltaTime;
        Vector2 dir = (wanderTarget - currentPos).normalized;
        isWalking = true;
        return dir * (walkSpeed * 0.75f);
    }

    private bool ShouldFreezeMovement()
    {
        // Detenerse durante el combate
        if (battleManager != null && battleManager.battlePanel != null && battleManager.battlePanel.activeSelf)
        {
            return true;
        }

        // Detenerse si Enrique está bloqueado por cinemática o diálogo
        if (playerController != null && !playerController.canMove)
        {
            return true;
        }

        return false;
    }

    private void UpdateVisualsAndAnimations(Vector2 velocity, bool isWalking, bool isRunning)
    {
        // Volteo horizontal según la dirección del movimiento
        if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
        {
            spriteRenderer.flipX = (velocity.x < 0f);
        }

        // Actualizar parámetros del Animator
        if (animator != null)
        {
            bool isMoving = isWalking || isRunning;

            if (hasIsWalking) animator.SetBool(AnimIsWalking, isWalking);
            if (hasIsRunning) animator.SetBool(AnimIsRunning, isRunning);
            if (hasIsMoving) animator.SetBool(AnimIsMoving, isMoving);
            if (hasSpeed) animator.SetFloat(AnimSpeed, velocity.magnitude);
        }
    }

    private void OnDisable()
    {
        currentMoveVelocity = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        UpdateVisualsAndAnimations(Vector2.zero, isWalking: false, isRunning: false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;

        // Círculo de detección (Amarillo)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Círculo de parada muy cerca (Rojo)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        // Área de patrulla errante (Verde)
        Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
#endif
}
