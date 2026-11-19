using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Placeholder arcade car controller for prototyping.
/// W/S = throttle/reverse, A/D = steer, Space = handbrake.
/// Attach to a GameObject with a Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : MonoBehaviour
{
    [Header("Driving")]
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float turnSpeed = 120f;
    [SerializeField] private float brakeDrag = 3f;

    [Header("Control")]
    [Tooltip("On = keyboard drives this car. Off = another script (e.g. AI) calls SetInput.")]
    [SerializeField] private bool playerControlled = true;

    [Header("Grip")]
    [Tooltip("How strongly sideways sliding is cancelled (0 = ice, 1 = full grip).")]
    [Range(0f, 1f)][SerializeField] private float grip = 0.9f;

    private Rigidbody rb;
    private float throttleInput;
    private float steerInput;
    private bool handbrake;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f); // lower = less flipping
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth camera follow
    }

    /// <summary>Current forward speed in m/s (negative when reversing).</summary>
    public float ForwardSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;

    public bool PlayerControlled
    {
        get => playerControlled;
        set => playerControlled = value;
    }

    /// <summary>Drive the car from code. throttle/steer in -1..1.</summary>
    public void SetInput(float throttle, float steer, bool brake)
    {
        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
        handbrake = brake;
    }

    public void SetMaxSpeed(float value) => maxSpeed = value;

    private void Update()
    {
        if (playerControlled) ReadInput();
    }

    private void FixedUpdate()
    {
        Vector3 velocity = rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(velocity, transform.forward);

        // Throttle
        if (Mathf.Abs(forwardSpeed) < maxSpeed || Mathf.Sign(throttleInput) != Mathf.Sign(forwardSpeed))
        {
            rb.AddForce(transform.forward * throttleInput * acceleration, ForceMode.Acceleration);
        }

        // Steering (scaled by speed so the car can't spin in place)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
        float turn = steerInput * turnSpeed * speedFactor * Mathf.Sign(forwardSpeed) * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

        // Grip: remove part of the sideways velocity
        Vector3 sideways = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
        float currentGrip = handbrake ? grip * 0.3f : grip;
        rb.AddForce(-sideways * currentGrip, ForceMode.VelocityChange);

        // Handbrake / natural slowdown
        rb.linearDamping = handbrake ? brakeDrag : 0.2f;
    }

    private void ReadInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        throttleInput = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                      - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        steerInput = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                   - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        handbrake = kb.spaceKey.isPressed;
#else
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        handbrake = Input.GetKey(KeyCode.Space);
#endif
    }
}
