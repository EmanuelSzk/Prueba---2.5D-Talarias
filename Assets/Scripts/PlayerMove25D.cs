
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove25D : MonoBehaviour
{
    [Header("Movimiento")]
    public float runSpeed = 5f;
    public float turnSpeed = 15f;
    public float idleYRotation = 180f;

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
    [SerializeField]
    private Vector2 wallJumpingPower =
        new Vector2(8f, 16f);

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

        // Iniciar dash.
        if (dashPressed &&
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

        // Actualizar animaciones.
        if (animator != null)
        {
            animator.SetBool("isDashing", isDashing);
            animator.SetBool("IsRunning", input != 0f);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("VerticalSpeed", rb.linearVelocity.y);
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

            Quaternion targetRotation =
                Quaternion.Euler(0f, targetY, 0f);

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

        // Actualizar la ventana de tiempo del Wall Jump.
        if (!isWallSliding)
        {
            wallJumpingCounter -= Time.fixedDeltaTime;
        }

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
            isWallJumping = false;

            if (animator != null)
            {
                animator.ResetTrigger("Jump");

                // Conserva el comportamiento anterior:
                // no activar el trigger en el segundo salto.
                if (jumpsRemaining == 1)
                    animator.SetTrigger("Jump");
            }
        }

        jumpPressed = false;

        // Movimiento horizontal normal.
        // No sobrescribir el impulso del Wall Jump.
        if (!isWallJumping)
        {
            float speed = isGrounded
                ? runSpeed
                : runSpeed * airSpeedMultiplier;

            rb.linearVelocity = new Vector3(
                input * speed,
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
        }
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

                wallJumpingDirection =
                    transform.position.x < wallX ? -1f : 1f;
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

        // Permitir un salto adicional después del Wall Jump.
        jumpsRemaining = 1;

        // Girar hacia el sentido del impulso.
        float targetY = wallJumpingDirection > 0f ? 90f : -90f;

        transform.rotation = Quaternion.Euler(0f, targetY, 0f);

        CancelInvoke(nameof(StopWallJumping));
        Invoke(nameof(StopWallJumping), wallJumpingDuration);

        if (animator != null)
        {
            // Cortar la animación de deslizamiento.
            animator.SetBool("isWallSliding", false);

            // Evitar que se active el salto normal.
            animator.ResetTrigger("Jump");

            // Activar la animación especial de Wall Jump.
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

    private void OnDrawGizmosSelected()
    {
        if (wallCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                wallCheck.position,
                wallCheckRadius
            );
        }
    }
}
