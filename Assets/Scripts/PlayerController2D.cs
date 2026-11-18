using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Movimiento 2D de Enrique Bosques en Santa Felipina.
/// Usa Rigidbody2D con WASD o Flechas y soporte para Shift (Correr/Sprint).
/// Se bloquea mediante 'canMove' durante la intro o el combate.
/// Conecta directamente con el Animator para transicionar limpiamente entre Idle (sprites originales 1..8),
/// Walk (marcha con zancadas) y Run (caminata/guardia rápida).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [Tooltip("Velocidad normal de caminata de Enrique en el mapa.")]
    public float moveSpeed = 5.5f;

    [Tooltip("Velocidad de carrera al mantener pulsado Shift.")]
    public float runSpeed = 8.0f;

    [Tooltip("Se pone en false durante la introducción o el combate, y vuelve a true al terminar.")]
    public bool canMove = true;

    [Header("Referencias Visuales y Animación")]
    [Tooltip("Animator del protagonista con los estados Idle, Walk y Run.")]
    public Animator animator;

    [Tooltip("SpriteRenderer para voltear el sprite según la dirección.")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("Transform del contenedor visual (hijo 'Visual').")]
    public Transform visualTransform;

    [Header("Efecto de Sonido de Pasos (Assets/Sound/SonidoDePasos)")]
    [Tooltip("SonidoDePasos: AudioClip de botas sobre concreto.")]
    public AudioClip footstepSfx;

    [Tooltip("Intervalo en segundos entre cada paso al caminar.")]
    public float walkStepInterval = 0.42f;

    [Tooltip("Intervalo en segundos entre cada paso al correr con Shift.")]
    public float runStepInterval = 0.28f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isRunningInput;
    private Vector2 lastFacingDirection = Vector2.down;
    private float stepTimer = 0f;

    private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int AnimIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimMoveX = Animator.StringToHash("MoveX");
    private static readonly int AnimMoveY = Animator.StringToHash("MoveY");
    private static readonly int AnimLookX = Animator.StringToHash("LookX");
    private static readonly int AnimLookY = Animator.StringToHash("LookY");

    private void OnValidate()
    {
        LoadFootstepClipIfMissing();
    }

    public void LoadFootstepClipIfMissing()
    {
        if (footstepSfx == null)
        {
            footstepSfx = RuntimeAssetCatalog.GetAudioClip(MusicManager.PathSfxPasos);
        }
    }

    private void Awake()
    {
        LoadFootstepClipIfMissing();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        if (visualTransform == null)
        {
            Transform child = transform.Find("Visual");
            if (child != null) visualTransform = child;
            else visualTransform = transform;
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ResetVisualTransform();
        HideAllWorldSpaceObjectLabels();
    }

    /// <summary>
    /// Oculta cualquier texto o letrero flotante (TextMeshPro 3D y Canvas WorldSpace) sobre objetos o personajes del mapa.
    /// </summary>
    public static void HideAllWorldSpaceObjectLabels()
    {
        TMPro.TextMeshPro[] worldTexts = FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include);
        foreach (var wt in worldTexts)
        {
            if (wt != null)
            {
                wt.text = string.Empty;
                wt.gameObject.SetActive(false);
            }
        }

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in allCanvases)
        {
            if (c != null && c.renderMode == RenderMode.WorldSpace)
            {
                c.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!canMove)
        {
            moveInput = Vector2.zero;
            isRunningInput = false;
            UpdateAnimation(false, false, 0f);
            return;
        }

        moveInput = ReadMovementInput();
        bool isMoving = moveInput.sqrMagnitude > 0.001f;
        isRunningInput = isMoving && ReadRunInput();

        if (isMoving)
        {
            // Determinar dirección cardinal dominante (prioriza eje con mayor magnitud)
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                lastFacingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
            }
            else if (Mathf.Abs(moveInput.y) > 0.01f)
            {
                lastFacingDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
            }
        }

        float effectiveSpeed = isRunningInput ? runSpeed : moveSpeed;
        UpdateAnimation(isMoving, isRunningInput, isMoving ? effectiveSpeed : 0f);
        UpdateFootstepAudio(isMoving, isRunningInput);

        // Volteo horizontal del sprite según la dirección:
        // Los sprites 'stand_side' y 'walk_side' miran naturalmente a la DERECHA.
        // Si mira/se mueve a la izquierda, flipX = true.
        // Si mira/se mueve a la derecha o verticalmente, flipX = false.
        if (spriteRenderer != null)
        {
            if (lastFacingDirection.x < -0.1f)
            {
                spriteRenderer.flipX = true;
            }
            else
            {
                spriteRenderer.flipX = false;
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (!canMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float speed = isRunningInput ? runSpeed : moveSpeed;
        rb.linearVelocity = moveInput * speed;
    }

    private void OnDisable()
    {
        moveInput = Vector2.zero;
        isRunningInput = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        UpdateAnimation(false, false, 0f);
        ResetVisualTransform();
    }

    /// <summary>
    /// Detiene inmediatamente la velocidad física y actualiza el estado canMove.
    /// </summary>
    public void SetCanMove(bool value)
    {
        canMove = value;
        if (!canMove)
        {
            moveInput = Vector2.zero;
            isRunningInput = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            UpdateAnimation(false, false, 0f);
            ResetVisualTransform();
        }
    }

    private void UpdateAnimation(bool isMoving, bool isRunning, float speed)
    {
        if (animator == null) return;
        animator.SetBool(AnimIsMoving, isMoving);
        animator.SetBool(AnimIsWalking, isMoving && !isRunning);
        animator.SetBool(AnimIsRunning, isRunning);
        animator.SetFloat(AnimSpeed, speed);

        Vector2 animMove = Vector2.zero;
        if (isMoving)
        {
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                animMove = new Vector2(Mathf.Sign(moveInput.x), 0f);
            }
            else if (Mathf.Abs(moveInput.y) > 0.01f)
            {
                animMove = new Vector2(0f, Mathf.Sign(moveInput.y));
            }
        }

        animator.SetFloat(AnimMoveX, animMove.x);
        animator.SetFloat(AnimMoveY, animMove.y);
        animator.SetFloat(AnimLookX, lastFacingDirection.x);
        animator.SetFloat(AnimLookY, lastFacingDirection.y);
    }

    public void ResetVisualTransform()
    {
        if (visualTransform != null && visualTransform != transform)
        {
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }
    }

    private void UpdateFootstepAudio(bool isMoving, bool isRunning)
    {
        if (!isMoving)
        {
            stepTimer = 0.06f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            float interval = isRunning ? Mathf.Max(0.15f, runStepInterval) : Mathf.Max(0.20f, walkStepInterval);
            stepTimer = interval;

            float randomPitch = isRunning
                ? UnityEngine.Random.Range(1.04f, 1.14f)
                : UnityEngine.Random.Range(0.94f, 1.06f);

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayPasosSfx(footstepSfx, randomPitch);
            }
            else if (footstepSfx != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(footstepSfx, Camera.main.transform.position, 0.50f);
            }
        }
    }

    private bool ReadRunInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            return true;
        }
#endif
        return false;
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                input.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                input.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                input.y += 1f;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (input == Vector2.zero)
        {
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
        }
#endif

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        return input;
    }
}
