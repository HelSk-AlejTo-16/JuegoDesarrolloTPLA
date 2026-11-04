using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// IA de compañero aliado para el Investigador Privado (Héctor Vargas) en el Nivel 2.
/// Espera a Enrique al inicio del callejón y, tras el diálogo de reclutamiento (o al acercarse),
/// lo sigue a pie manteniendo una distancia natural, animando sus pasos y ofreciendo pistas con [E].
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CompanionFollowAI : MonoBehaviour
{
    [Header("1. Estado de Compañero")]
    [Tooltip("Nombre visible del compañero.")]
    public string companionName = "INVESTIGADOR VARGAS";

    [Tooltip("Si está activo, el investigador ya se unió a Enrique y lo sigue por el mapa.")]
    public bool isFollowingPlayer = false;

    [Tooltip("Si está activo, inicia la conversación de unión automáticamente cuando Enrique se acerca por primera vez.")]
    public bool autoTriggerJoinDialogueOnApproach = true;

    [Tooltip("Distancia a la que se dispara automáticamente el diálogo de encuentro si aún no se ha unido.")]
    public float autoJoinTriggerDistance = 3.2f;

    [Header("2. Parámetros de Seguimiento a Pie")]
    [Tooltip("Distancia ideal a mantener detrás o al lado de Enrique.")]
    [Range(1.1f, 3.5f)]
    public float followDistance = 1.65f;

    [Tooltip("Distancia a partir de la cual empieza a correr para alcanzar a Enrique.")]
    [Range(2.2f, 6.0f)]
    public float catchUpDistance = 3.1f;

    [Tooltip("Distancia máxima antes de reposicionarse cerca de Enrique si quedó atrapado tras una esquina.")]
    public float teleportSnapDistance = 11.0f;

    [Tooltip("Velocidad al caminar siguiendo a Enrique.")]
    public float walkSpeed = 5.2f;

    [Tooltip("Velocidad al correr para alcanzar a Enrique.")]
    public float runSpeed = 7.6f;

    [Header("3. Sprites Direccionales / Animación")]
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public Sprite idleDownSprite;
    public Sprite idleUpSprite;
    public Sprite idleSideSprite;
    public Sprite[] walkDownFrames;
    public Sprite[] walkUpFrames;
    public Sprite[] walkSideFrames;
    public float animationFPS = 9f;

    [Header("4. Diálogos del Investigador Privado")]
    [TextArea(2, 4)]
    public string[] recruitmentDialogueLines = new string[]
    {
        "NARRADOR: \"El paso vehicular termina en una barricada de autos calcinados y láminas oxidadas. Entre los edificios altos de Santa Felipina solo queda un estrecho callejón donde únicamente se puede avanzar a pie.\"",
        "VOZ [MUNDO INTERNO]: \"El humo de una colilla recién apagada flota junto al farol roto. No estamos solos en este callejón, Enrique.\"",
        "INVESTIGADOR PRIVADO: \"Baja los puños, repartidor. Vi cómo dejaste a los matones de la avenida... Me llamo Héctor Vargas, investigador privado.\"",
        "ENRIQUE BOSQUES: \"¿Un detective en este basurero? Si vienes por el paquete, llegas tarde. Ni siquiera sé qué demonios llevo dentro.\"",
        "INVESTIGADOR PRIVADO: \"No busco tu caja, Bosques. Busco al hombre que ordenó la emboscada: 'El Calacas'. Llevo tres meses siguiéndole el rastro por toda Santa Felipina.\"",
        "VOZ [INTELECTO]: \"Un investigador privado... Sus expedientes y su conocimiento del terreno nos ahorran caminar a ciegas. Escucha lo que sabe.\"",
        "INVESTIGADOR PRIVADO: \"Este callejón entre los edificios es el único acceso a su refugio. Bloquearon las calles con chatarra para que ningún auto ni patrulla pueda entrar; aquí solo se pasa a pie.\"",
        "VOZ [CORPOREO]: \"Más vale que sepa repartir golpes si piensa caminar a nuestro lado. El callejón apesta a emboscada.\"",
        "VOZ [MOTRICIDAD]: \"Se mueve con sigilo y mantiene el paso firme. Nos vendrá bien alguien que vigile los ángulos muertos.\"",
        "INVESTIGADOR PRIVADO: \"Más adelante hay varios de sus sicarios custodiando el paso y su mano derecha protege el fondo del callejón. Voy contigo, Enrique: te sigo de cerca hasta dar con El Calacas.\"",
        "NARRADOR: \"[ EL INVESTIGADOR PRIVADO SE HA UNIDO A TI Y TE SEGUIRÁ POR EL CALLEJÓN ]\""
    };

    [TextArea(2, 4)]
    public string[] followUpDialogueLines = new string[]
    {
        "INVESTIGADOR PRIVADO: \"No bajes la guardia, Bosques. Los techos y las ventanas tapiadas de estos edificios son el nido de los vigías de El Calacas.\"",
        "VOZ [INTELECTO]: \"Tiene razón. El callejón se estrecha más adelante entre los contenedores de basura; revisa cada rincón antes de avanzar al fondo.\""
    };

    [Header("5. Indicador Flotante [E]")]
    public GameObject promptUI;
    public TextMeshPro promptText;

    private Rigidbody2D rb;
    private Transform playerTransform;
    private PlayerController2D playerController;
    private ExplorationDialogueManager dialogueManager;
    private BattleManager battleManager;

    private bool hasTriggeredJoinDialogue = false;
    private Vector2 currentVelocity = Vector2.zero;
    private Vector2 lastLookDirection = Vector2.down;
    private float animTimer = 0f;
    private int currentFrameIndex = 0;
    private float baseBobPhase = 0f;
    private readonly Queue<Vector2> breadcrumbTrail = new Queue<Vector2>();
    private Vector2 lastRecordedPlayerPos;
    private bool hasRecordedPlayerPos = false;

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

        if (spriteRenderer == null)
        {
            Transform vis = transform.Find("Visual");
            if (vis != null) spriteRenderer = vis.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Evitar que el collider físico del compañero empuje o bloquee a Enrique en pasillos estrechos
        Collider2D[] myCols = GetComponentsInChildren<Collider2D>();
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
        {
            PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
            if (pc != null) playerGO = pc.gameObject;
        }
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerController = playerGO.GetComponent<PlayerController2D>();
            Collider2D[] playerCols = playerGO.GetComponentsInChildren<Collider2D>();
            foreach (var c1 in myCols)
            {
                if (c1 == null || c1.isTrigger) continue;
                foreach (var c2 in playerCols)
                {
                    if (c2 == null || c2.isTrigger) continue;
                    Physics2D.IgnoreCollision(c1, c2, true);
                }
            }
        }
    }

    private void Start()
    {
        LocateReferences();
        UpdatePromptVisibility(false);
    }

    private void LocateReferences()
    {
        if (playerTransform == null)
        {
            PlayerController2D pc = FindAnyObjectByType<PlayerController2D>();
            if (pc != null)
            {
                playerController = pc;
                playerTransform = pc.transform;
            }
        }

        if (dialogueManager == null)
        {
            dialogueManager = ExplorationDialogueManager.Instance;
        }

        if (battleManager == null)
        {
            battleManager = FindAnyObjectByType<BattleManager>();
        }
    }

    /// <summary>
    /// Activa inmediatamente el modo de seguimiento del Investigador Privado.
    /// </summary>
    public void ActivateCompanionFollow()
    {
        hasTriggeredJoinDialogue = true;
        isFollowingPlayer = true;
        if (promptText != null)
        {
            promptText.text = "[E] HABLAR CON VARGAS";
        }
    }

    private void Update()
    {
        LocateReferences();

        if (playerTransform == null)
            return;

        if (ShouldFreeze())
        {
            currentVelocity = Vector2.zero;
            UpdatePromptVisibility(false);
            UpdateVisualAnimation(Vector2.zero);
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // 1. Si aún no se ha unido y Enrique se acerca en el callejón, iniciar el diálogo de unión
        if (!hasTriggeredJoinDialogue && autoTriggerJoinDialogueOnApproach && dist <= autoJoinTriggerDistance)
        {
            TriggerJoinDialogue();
            return;
        }

        // 2. Mostrar indicador [E] cuando Enrique está cerca y puede hablar con él
        bool canTalkNow = dist <= 2.3f && (playerController == null || playerController.canMove);
        UpdatePromptVisibility(canTalkNow);

        if (canTalkNow && WasInteractKeyPressed())
        {
            if (!hasTriggeredJoinDialogue)
            {
                TriggerJoinDialogue();
            }
            else
            {
                TriggerFollowUpDialogue();
            }
            return;
        }

        // 3. Lógica de seguimiento a pie por el callejón
        if (!isFollowingPlayer)
        {
            currentVelocity = Vector2.zero;
            // Mirar hacia Enrique mientras espera
            Vector2 toPlayer = (Vector2)(playerTransform.position - transform.position);
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                lastLookDirection = toPlayer.normalized;
            }
            UpdateVisualAnimation(Vector2.zero);
            return;
        }

        if (dist > teleportSnapDistance)
        {
            breadcrumbTrail.Clear();
            hasRecordedPlayerPos = false;
            Vector3 snapOffset = new Vector3(-1.1f, 0.1f, 0f);
            transform.position = playerTransform.position + snapOffset;
            currentVelocity = Vector2.zero;
            UpdateVisualAnimation(Vector2.zero);
            return;
        }

        // Registrar rastro de migas de pan (breadcrumbs) de Enrique para doblar esquinas estrechas del callejón sin atascarse
        Vector2 playerPos2D = playerTransform.position;
        if (!hasRecordedPlayerPos)
        {
            lastRecordedPlayerPos = playerPos2D;
            hasRecordedPlayerPos = true;
        }
        else if (Vector2.Distance(playerPos2D, lastRecordedPlayerPos) >= 0.35f)
        {
            breadcrumbTrail.Enqueue(playerPos2D);
            lastRecordedPlayerPos = playerPos2D;
            while (breadcrumbTrail.Count > 45)
            {
                breadcrumbTrail.Dequeue();
            }
        }

        if (dist > followDistance)
        {
            Vector2 myPos2D = transform.position;
            while (breadcrumbTrail.Count > 0 && Vector2.Distance(myPos2D, breadcrumbTrail.Peek()) < 0.42f)
            {
                breadcrumbTrail.Dequeue();
            }

            Vector2 moveTarget = (breadcrumbTrail.Count > 0) ? breadcrumbTrail.Peek() : playerPos2D;
            Vector2 dir = (moveTarget - myPos2D).normalized;
            float speed = (dist > catchUpDistance) ? runSpeed : walkSpeed;
            float arrivalFactor = Mathf.Clamp01((dist - followDistance) / 0.85f);
            currentVelocity = dir * (speed * Mathf.Max(0.40f, arrivalFactor));
            if (dir.sqrMagnitude > 0.001f)
            {
                lastLookDirection = dir;
            }
        }
        else
        {
            breadcrumbTrail.Clear();
            lastRecordedPlayerPos = playerPos2D;
            currentVelocity = Vector2.zero;
        }

        UpdateVisualAnimation(currentVelocity);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (ShouldFreeze())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = currentVelocity;
    }

    public void TriggerJoinDialogue(System.Action onFinished = null)
    {
        if (hasTriggeredJoinDialogue)
        {
            onFinished?.Invoke();
            return;
        }

        hasTriggeredJoinDialogue = true;
        currentVelocity = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        UpdatePromptVisibility(false);

        if (dialogueManager == null)
        {
            dialogueManager = FindAnyObjectByType<ExplorationDialogueManager>();
        }

        if (dialogueManager != null && recruitmentDialogueLines != null && recruitmentDialogueLines.Length > 0)
        {
            dialogueManager.StartDialogue(recruitmentDialogueLines, () =>
            {
                ActivateCompanionFollow();
                onFinished?.Invoke();
            });
        }
        else
        {
            ActivateCompanionFollow();
            onFinished?.Invoke();
        }
    }

    private void TriggerFollowUpDialogue()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindAnyObjectByType<ExplorationDialogueManager>();
        }

        if (dialogueManager != null && followUpDialogueLines != null && followUpDialogueLines.Length > 0)
        {
            currentVelocity = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            UpdatePromptVisibility(false);

            string[] contextualLines = followUpDialogueLines;
            if (playerTransform != null && playerTransform.position.x > 8f)
            {
                contextualLines = new string[]
                {
                    "INVESTIGADOR PRIVADO: \"Estamos llegando al fondo del callejón, Enrique. Detrás de esa reja blindada está el acceso al refugio de El Calacas.\"",
                    "VOZ [CORPOREO]: \"Prepara los puños y revisa nuestras armas en el inventario [TAB]. Aquí es donde se pone serio.\""
                };
            }

            dialogueManager.StartDialogue(contextualLines);
        }
    }

    private void UpdateVisualAnimation(Vector2 velocity)
    {
        if (spriteRenderer == null) return;

        bool isMoving = velocity.sqrMagnitude > 0.08f;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == "IsMoving") animator.SetBool("IsMoving", isMoving);
                else if (p.name == "IsRunning") animator.SetBool("IsRunning", velocity.magnitude > walkSpeed + 0.5f);
                else if (p.name == "MoveX") animator.SetFloat("MoveX", lastLookDirection.x);
                else if (p.name == "MoveY") animator.SetFloat("MoveY", lastLookDirection.y);
            }
            if (Mathf.Abs(lastLookDirection.x) > 0.15f)
            {
                spriteRenderer.flipX = lastLookDirection.x < 0f;
            }
            return;
        }

        // Animación por frames si no usa AnimatorController
        if (Mathf.Abs(lastLookDirection.x) > 0.2f)
        {
            spriteRenderer.flipX = (lastLookDirection.x < 0f);
        }

        if (!isMoving)
        {
            animTimer = 0f;
            currentFrameIndex = 0;

            if (Mathf.Abs(lastLookDirection.y) > Mathf.Abs(lastLookDirection.x))
            {
                if (lastLookDirection.y > 0f && idleUpSprite != null)
                    spriteRenderer.sprite = idleUpSprite;
                else if (idleDownSprite != null)
                    spriteRenderer.sprite = idleDownSprite;
            }
            else if (idleSideSprite != null)
            {
                spriteRenderer.sprite = idleSideSprite;
            }
            else if (idleDownSprite != null)
            {
                spriteRenderer.sprite = idleDownSprite;
            }

            if (spriteRenderer.transform != transform)
            {
                baseBobPhase += Time.deltaTime * 2.4f;
                float bob = Mathf.Sin(baseBobPhase) * 0.02f;
                spriteRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }
            return;
        }

        Sprite[] activeFrames = walkSideFrames;
        if (Mathf.Abs(lastLookDirection.y) > Mathf.Abs(lastLookDirection.x))
        {
            activeFrames = (lastLookDirection.y > 0f && walkUpFrames != null && walkUpFrames.Length > 0)
                ? walkUpFrames
                : walkDownFrames;
        }

        if (activeFrames != null && activeFrames.Length > 0)
        {
            animTimer += Time.deltaTime * animationFPS;
            if (animTimer >= 1f)
            {
                animTimer -= 1f;
                currentFrameIndex = (currentFrameIndex + 1) % activeFrames.Length;
            }
            Sprite frame = activeFrames[currentFrameIndex % activeFrames.Length];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }
    }

    private void UpdatePromptVisibility(bool visible)
    {
        if (promptUI != null && promptUI.activeSelf != visible)
        {
            promptUI.SetActive(visible);
        }
    }

    private bool ShouldFreeze()
    {
        if (dialogueManager != null && dialogueManager.IsDialogueActive)
            return true;

        if (battleManager != null && battleManager.state != BattleState.INACTIVE)
            return true;

        if (PauseMenuManager.Instance != null && PauseMenuManager.Instance.IsPaused)
            return true;

        return false;
    }

    private bool WasInteractKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
        {
            return true;
        }
#endif
        return false;
    }
}
