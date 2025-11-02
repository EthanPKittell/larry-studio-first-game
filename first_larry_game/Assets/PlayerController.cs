using UnityEngine;
using UnityEngine.InputSystem; // new Input System

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Jump Tuning")]
    public float fallMultiplier = 2.5f;       // Faster fall
    public float lowJumpMultiplier = 2f;      // Short hop control

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 2f;       // Sprint speed multiplier
    public float maxGroundSpeed = 8f;
    public float maxSprintSpeed = 16f;        // Max speed when sprinting
    public float groundDrag = 5f;

    [Header("Jump")]
    public float jumpForce = 6f;
    public int maxJumps = 1;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.1f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer = ~0;

    private Rigidbody rb;
    private Vector3 inputDirection;
    private int jumpsLeft;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private bool isSprinting = false;
    private bool wasSprintingOnGround = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        jumpsLeft = maxJumps;
    }

    void Update()
    {
        // NEW INPUT SYSTEM: read keyboard state safely (works in Editor and builds)
        Vector2 move = Vector2.zero;
        if (Keyboard.current != null)
        {
            // WASD
            float right = Keyboard.current.dKey.isPressed ? 1f : 0f;
            float left  = Keyboard.current.aKey.isPressed ? 1f : 0f;
            float up    = Keyboard.current.wKey.isPressed ? 1f : 0f;
            float down  = Keyboard.current.sKey.isPressed ? 1f : 0f;
            move = new Vector2(right - left, up - down);

            // Sprint input (Left Shift) - only allow sprint to START on ground
            bool sprintInput = Keyboard.current.leftShiftKey.isPressed;
            
            if (IsGrounded())
            {
                // On ground: allow sprint state to change freely
                isSprinting = sprintInput;
                wasSprintingOnGround = isSprinting;
            }
            else
            {
                // In air: can only continue sprinting if we started on ground
                isSprinting = sprintInput && wasSprintingOnGround;
            }
        }
        else
        {
            // fallback (rare) if Keyboard.current is null; won't throw old-API exception
            move = Vector2.zero;
            isSprinting = false;
        }

        // --- Camera-relative movement ---
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;

        // flatten so camera pitch doesn't tilt movement
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        inputDirection = (camRight * move.x + camForward * move.y).normalized;


        // Jump input detection (buffer) – spacebar
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            lastJumpPressedTime = Time.time;
        }

        if (IsGrounded())
            lastGroundedTime = Time.time;
    }

    void FixedUpdate()
    {
        // Calculate current move speed based on sprint state
        float currentMoveSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        float currentMaxSpeed = isSprinting ? maxSprintSpeed : maxGroundSpeed;

        // Horizontal movement: preserve vertical velocity
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 desiredVel = inputDirection * currentMoveSpeed;

        Vector3 velChange = desiredVel - horizontalVel;
        rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);

        // Clamp horizontal speed
        Vector3 clampedHorizontal = new Vector3(
            Mathf.Clamp(rb.linearVelocity.x, -currentMaxSpeed, currentMaxSpeed),
            0f,
            Mathf.Clamp(rb.linearVelocity.z, -currentMaxSpeed, currentMaxSpeed)
        );
        rb.linearVelocity = new Vector3(clampedHorizontal.x, rb.linearVelocity.y, clampedHorizontal.z);

        // Ground drag
        if (IsGrounded())
        {
            Vector3 slowed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) * Mathf.Max(0f, 1f - groundDrag * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(slowed.x, rb.linearVelocity.y, slowed.z);
        }

        // Jump handling with buffer + coyote
        if (Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            bool allowedByCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
            if (IsGrounded() || allowedByCoyote && jumpsLeft > 0)
            {
                DoJump();
                lastJumpPressedTime = -10f;
            }
        }

        bool grounded = IsGrounded();

        // Reset jumps only *once* when we newly touch the ground
        if (grounded && jumpsLeft < maxJumps && rb.linearVelocity.y <= 0f)
        {
            jumpsLeft = maxJumps;
        }

        // Update last grounded time (for coyote jump)
        if (grounded)
            lastGroundedTime = Time.time;

        // --- Better gravity & variable jump height ---
        if (rb.linearVelocity.y < 0)
        {
            // Falling: apply stronger gravity for snappier descent
            rb.AddForce(Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * rb.mass);
        }
        else if (rb.linearVelocity.y > 0 && (Keyboard.current == null || !Keyboard.current.spaceKey.isPressed))
        {
            // Jump key released early → shorter hop
            rb.AddForce(Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * rb.mass);
        }

    }

    void DoJump()
    {
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
    }

    bool IsGrounded()
    {
        if (groundCheck == null)
        {
            float rayDist = 1.1f;
            return Physics.Raycast(transform.position, Vector3.down, rayDist, groundLayer);
        }
        else
        {
            return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}