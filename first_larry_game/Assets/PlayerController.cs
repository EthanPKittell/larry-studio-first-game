using UnityEngine;
using UnityEngine.InputSystem; // new Input System

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float maxGroundSpeed = 8f;
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
        }
        else
        {
            // fallback (rare) if Keyboard.current is null; won't throw old-API exception
            move = Vector2.zero;
        }

        // --- Camera-relative movement ---
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;

        // flatten so camera pitch doesn’t tilt movement
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        inputDirection = (camRight * move.x + camForward * move.y).normalized;


        // Jump input detection (buffer) — spacebar
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            lastJumpPressedTime = Time.time;
        }

        if (IsGrounded())
            lastGroundedTime = Time.time;
    }

    void FixedUpdate()
    {
        // Horizontal movement: preserve vertical velocity
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 desiredVel = inputDirection * moveSpeed;

        Vector3 velChange = desiredVel - horizontalVel;
        rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);

        // Clamp horizontal speed
        Vector3 clampedHorizontal = new Vector3(
            Mathf.Clamp(rb.velocity.x, -maxGroundSpeed, maxGroundSpeed),
            0f,
            Mathf.Clamp(rb.velocity.z, -maxGroundSpeed, maxGroundSpeed)
        );
        rb.velocity = new Vector3(clampedHorizontal.x, rb.velocity.y, clampedHorizontal.z);

        // Ground drag
        if (IsGrounded())
        {
            Vector3 slowed = new Vector3(rb.velocity.x, 0, rb.velocity.z) * Mathf.Max(0f, 1f - groundDrag * Time.fixedDeltaTime);
            rb.velocity = new Vector3(slowed.x, rb.velocity.y, slowed.z);
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
        if (grounded && jumpsLeft < maxJumps && rb.velocity.y <= 0f)
        {
            jumpsLeft = maxJumps;
        }

        // Update last grounded time (for coyote jump)
        if (grounded)
            lastGroundedTime = Time.time;

    }

    void DoJump()
    {
        Vector3 vel = rb.velocity;
        vel.y = 0f;
        rb.velocity = vel;
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
