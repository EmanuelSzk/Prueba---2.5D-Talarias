using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove25D : MonoBehaviour
{
    [Header("Movimiento")]
    public float runSpeed = 5f;
    public float turnSpeed = 15f;
    public float idleYRotation = 180f;

    [Header("Carrera acelerada")]
    [Tooltip("Segundos de carrera continua antes de activar la carrera rápida.")]
    [SerializeField] private float timeToStartFastRun = 3f;

    [Tooltip("Velocidad al entrar en la carrera rápida.")]
    [SerializeField] private float fastRunStartSpeed = 7f;

    [Tooltip("Velocidad máxima que puede alcanzar la carrera rápida.")]
    [SerializeField] private float maxFastRunSpeed = 10f;

    [Tooltip("Segundos que tarda en pasar de fastRunStartSpeed a maxFastRunSpeed.")]
    [SerializeField] private float timeToMaxFastSpeed = 2.5f;

    [Tooltip("Segundos que tarda en agotarse una reserva completa al dejar de correr.")]
    [SerializeField] private float speedReserveDrainTime = 5f;

    private float continuousRunTimer;
    private float speedReserve; // De 0 a 1.
    private int lastRunDirection;
    private bool isFastRunning;

    // Datos públicos para que el HUD pueda leer el sistema de velocidad.
    public float SpeedReserve => speedReserve;
    public bool IsFastRunning => isFastRunning;
    public float SpeedReserveSeconds => speedReserve * speedReserveDrainTime;

    [Header("Salto")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float airSpeedMultiplier = 0.6f;

    [Header("Wall Slide")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckRadius = 1f;
    [SerializeField] private float wallSlidingSpeed = 2f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpingTime = 0.2f;
    [SerializeField] private float wallJumpingDuration = 0.4f;
    [SerializeField] private Vector2 wallJumpingPower = new Vector2(8f, 16f);

    [Header("Dash")]
    [SerializeField] private float dashingVelocity = 14f;
    [SerializeField] private float dashingTime = 0.5f;
    [SerializeField] private float dashCooldown = 0.3f;

    private Rigidbody rb;
    private Animator animator;
    private TrailRenderer trailRenderer;

    private float input;
    private float nextDashTime;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isWallSliding;
    private bool isWallJumping;
    private bool isDashing;
    private bool canDash = true;
    private bool jumpPressed;
    private bool dashPressed;

    private int jumpsRemaining = 2;
    private float wallJumpingDirection;
    private float wallJumpingCounter;
    private Vector2 dashingDir;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        trailRenderer = GetComponent<TrailRenderer>();

        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    private void Update()
    {
        input = Input.GetAxisRaw("Horizontal");

        // Registrar las pulsaciones.
        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (Input.GetButtonDown("Dash"))
            dashPressed = true;

        // Comprobar si el personaje está en el suelo.
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance,
            groundMask
        );

        // Recuperar los saltos y el dash al aterrizar.
        if (isGrounded && !wasGrounded)
        {
            jumpsRemaining = 2;
            canDash = true;
            isWallJumping = false;
        }

        wasGrounded = isGrounded;

        if (isGrounded)
            canDash = true;

        // Iniciar dash únicamente en el aire.
        if (dashPressed &&
            !isGrounded &&
            canDash &&
            !isDashing &&
            Time.time >= nextDashTime &&
            input != 0f)
        {
            isDashing = true;
            canDash = false;
            nextDashTime = Time.time + dashCooldown;

            dashingDir = new Vector2(Mathf.Sign(input), 0f);

            if (trailRenderer != null)
                trailRenderer.emitting = true;

            StartCoroutine(StopDashing());
        }

        dashPressed = false;

        if (animator != null)
        {
            animator.SetBool("isDashing", isDashing);
            animator.SetBool("IsRunning", input != 0f);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("VerticalSpeed", rb.linearVelocity.y);
            animator.SetBool("IsFastRunning", isFastRunning);
        }

        if (isDashing)
            return;

        // Girar según la dirección del movimiento,
        // excepto durante el impulso del Wall Jump.
        if (!isWallJumping)
        {
            float targetY;

            if (input > 0f)
                targetY = 90f;
            else if (input < 0f)
                targetY = -90f;
            else
                targetY = idleYRotation;

            Quaternion targetRotation = Quaternion.Euler(0f, targetY, 0f);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
    }

    private void FixedUpdate()
    {
        // El dash tiene prioridad.
        if (isDashing)
        {
            rb.linearVelocity = new Vector3(
                dashingDir.normalized.x * dashingVelocity,
                0f,
                0f
            );

            jumpPressed = false;
            return;
        }

        // Comprobar el Wall Slide.
        WallSlide();

        // Actualizar el sistema de carrera acelerada y reserva.
        UpdateSpeedSystem();

        // Actualizar la ventana de tiempo del Wall Jump.
        if (!isWallSliding)
            wallJumpingCounter -= Time.fixedDeltaTime;

        // Procesar primero el Wall Jump.
        if (jumpPressed && wallJumpingCounter > 0f)
        {
            WallJump();
        }
        else if (jumpPressed &&
                 !isWallSliding &&
                 jumpsRemaining > 0)
        {
            // Salto normal o doble salto.
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                jumpForce,
                0f
            );

            jumpsRemaining--;

            // Activar el trail solamente en el doble salto.
            if (jumpsRemaining == 0 && trailRenderer != null)
            {
                trailRenderer.emitting = true;
                StartCoroutine(StopDoubleJumpTrail());
            }

            isWallJumping = false;

            if (animator != null)
            {
                animator.ResetTrigger("Jump");

                // Mantener el comportamiento anterior:
                // activar el trigger en el primer salto.
                if (jumpsRemaining == 1)
                    animator.SetTrigger("Jump");
            }
        }

        jumpPressed = false;

        // Movimiento horizontal normal.
        // No sobrescribir el impulso del Wall Jump.
        if (!isWallJumping)
        {
            float movementSpeed = GetCurrentRunSpeed();

            if (!isGrounded)
                movementSpeed *= airSpeedMultiplier;

            rb.linearVelocity = new Vector3(
                input * movementSpeed,
                rb.linearVelocity.y,
                0f
            );
        }

        // Gravedad adicional durante la caída.
        if (rb.linearVelocity.y < 0f && !isWallSliding)
        {
            rb.linearVelocity += Vector3.up
                * Physics.gravity.y
                * (fallMultiplier - 1f)
                * Time.fixedDeltaTime;
        }

        if (animator != null)
        {
            animator.SetBool("isWallSliding", isWallSliding);
            animator.SetFloat("VerticalSpeed", rb.linearVelocity.y);
            animator.SetBool("IsFastRunning", isFastRunning);
        }
    }

    private void UpdateSpeedSystem()
    {
        float deltaTime = Time.fixedDeltaTime;

        bool canBuildSpeed =
            isGrounded &&
            input != 0f &&
            !isDashing &&
            !isWallSliding &&
            !isWallJumping;

        if (canBuildSpeed)
        {
            int currentDirection = input > 0f ? 1 : -1;

            // Cambiar de dirección reinicia el tiempo continuo,
            // pero conserva la reserva acumulada.
            if (lastRunDirection != 0 && currentDirection != lastRunDirection)
                continuousRunTimer = 0f;

            lastRunDirection = currentDirection;
            continuousRunTimer += deltaTime;

            // La reserva empieza a crecer tras el tiempo inicial.
            // Si ya había reserva, se puede recuperar antes de llegar
            // otra vez al tiempo inicial.
            if (continuousRunTimer >= timeToStartFastRun || speedReserve > 0f)
            {
                float accelerationDuration = Mathf.Max(0.01f, timeToMaxFastSpeed);
                speedReserve += deltaTime / accelerationDuration;
            }
        }
        else
        {
            continuousRunTimer = 0f;

            // La reserva se drena gradualmente mientras no corre
            // de forma continua por el suelo.
            float drainDuration = Mathf.Max(0.01f, speedReserveDrainTime);
            speedReserve -= deltaTime / drainDuration;

            if (input == 0f)
                lastRunDirection = 0;
        }

        speedReserve = Mathf.Clamp01(speedReserve);

        // La animación rápida solo se muestra mientras corre por el suelo
        // y ya tiene impulso acumulado.
        isFastRunning =
            canBuildSpeed &&
            speedReserve > 0.01f &&
            (continuousRunTimer >= timeToStartFastRun || speedReserve > 0.01f);
    }

    private float GetCurrentRunSpeed()
    {
        if (!isFastRunning && speedReserve <= 0f)
            return runSpeed;

        // La reserva determina la velocidad entre el inicio rápido y el máximo.
        float boostedSpeed = Mathf.Lerp(
            fastRunStartSpeed,
            maxFastRunSpeed,
            speedReserve
        );

        // Antes de activar la carrera rápida, mantener la velocidad normal.
        if (continuousRunTimer < timeToStartFastRun && speedReserve <= 0f)
            return runSpeed;

        return Mathf.Max(runSpeed, boostedSpeed);
    }

    private bool IsWalled()
    {
        if (wallCheck == null)
            return false;

        return Physics.CheckSphere(
            wallCheck.position,
            wallCheckRadius,
            wallLayer
        );
    }

    private void WallSlide()
    {
        bool touchingWall = IsWalled();

        if (touchingWall &&
            !isGrounded &&
            input != 0f &&
            !isDashing &&
            !isWallJumping)
        {
            isWallSliding = true;

            if (rb.linearVelocity.y < -wallSlidingSpeed)
            {
                rb.linearVelocity = new Vector3(
                    rb.linearVelocity.x,
                    -wallSlidingSpeed,
                    0f
                );
            }

            // Renovar la ventana para saltar desde la pared.
            wallJumpingCounter = wallJumpingTime;

            // Determinar hacia qué lado está la pared.
            Collider[] walls = Physics.OverlapSphere(
                wallCheck.position,
                wallCheckRadius,
                wallLayer
            );

            if (walls.Length > 0)
            {
                float wallX = walls[0].bounds.center.x;
                wallJumpingDirection = transform.position.x < wallX ? -1f : 1f;
            }
        }
        else
        {
            isWallSliding = false;
        }

        if (animator != null)
            animator.SetBool("isWallSliding", isWallSliding);
    }

    private void WallJump()
    {
        isWallJumping = true;

        // Impulso hacia arriba y alejándose de la pared.
        rb.linearVelocity = new Vector3(
            wallJumpingDirection * wallJumpingPower.x,
            wallJumpingPower.y,
            0f
        );

        wallJumpingCounter = 0f;
        jumpsRemaining = 1;

        // Girar hacia el sentido del impulso.
        float targetY = wallJumpingDirection > 0f ? 90f : -90f;
        transform.rotation = Quaternion.Euler(0f, targetY, 0f);

        CancelInvoke(nameof(StopWallJumping));
        Invoke(nameof(StopWallJumping), wallJumpingDuration);

        if (animator != null)
        {
            animator.SetBool("isWallSliding", false);
            animator.ResetTrigger("Jump");
            animator.SetTrigger("WallJump");
        }
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    private IEnumerator StopDashing()
    {
        yield return new WaitForSeconds(dashingTime);

        if (trailRenderer != null)
            trailRenderer.emitting = false;

        isDashing = false;
    }

    private IEnumerator StopDoubleJumpTrail()
    {
        yield return new WaitForSeconds(0.25f);

        if (trailRenderer != null && !isDashing)
            trailRenderer.emitting = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (wallCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
        }
    }
}
