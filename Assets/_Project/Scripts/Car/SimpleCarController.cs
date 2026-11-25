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
    [Tooltip("Slowdown when no throttle is pressed (engine braking / rolling resistance).")]
    [SerializeField] private float coastDrag = 0.8f;

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
        // Put the center of mass near the bottom of the collider = much less flipping.
        BoxCollider box = GetComponent<BoxCollider>();
        rb.centerOfMass = box != null
            ? box.center - new Vector3(0f, box.size.y * 0.45f, 0f)
            : new Vector3(0f, -0.5f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth camera follow
        // BUG FIX (cars passing through buildings): at 30+ m/s a car moves ~0.6 m per physics step,
        // more than a thin wall collider, so discrete collision checks missed walls. Continuous
        // detection sweeps the body between steps.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // BUG FIX (endless spinning / flips): collision spin was never slowed down.
        rb.angularDamping = 2f;
        rb.maxAngularVelocity = 6f;

        // The body is a box sliding on the road: with normal friction (made worse by downforce)
        // it acted like a brake and capped the speed around 53 km/h. This script handles grip
        // and slowdown itself, so the body gets zero friction.
        if (box != null)
        {
            box.material = new PhysicsMaterial("CarBody")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        }
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

    // ---------- Nitro (from pickups) ----------
    private float boostTimer;
    /// <summary>True while a nitro boost is active.</summary>
    public bool Boosting => boostTimer > 0f;
    /// <summary>Higher top speed and much stronger acceleration for a few seconds.</summary>
    public void Boost(float seconds) => boostTimer = Mathf.Max(boostTimer, seconds);

    private void Update()
    {
        if (!playerControlled) return;
        if (GameState.IsPlaying) ReadInput();
        else SetInput(0f, 0f, true); // menu / paused / busted: hold the car still
    }

    private void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        bool upright = transform.up.y > 0.7f;

        // Only drive/steer with wheels on the road. Pushing while airborne or tilted is what
        // launched cars into the air and onto their sides.
        if (IsGrounded && upright)
        {
            Vector3 velocity = rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);

            // Throttle (nitro: +50% top speed, double acceleration, pushes even without W pressed)
            if (boostTimer > 0f) boostTimer -= Time.fixedDeltaTime;
            float topSpeed = Boosting ? maxSpeed * 1.5f : maxSpeed;
            float accel = Boosting ? acceleration * 2f : acceleration;
            float throttle = Boosting && throttleInput >= 0f ? 1f : throttleInput;
            if (Mathf.Abs(forwardSpeed) < topSpeed || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed))
            {
                rb.AddForce(transform.forward * throttle * accel, ForceMode.Acceleration);
            }

            // Steering (scaled by speed so the car can't spin in place)
            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
            float turn = steerInput * turnSpeed * speedFactor * Mathf.Sign(forwardSpeed) * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));

            // Grip: remove part of the sideways velocity
            Vector3 sideways = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
            float currentGrip = handbrake ? grip * 0.3f : grip;
            rb.AddForce(-sideways * currentGrip, ForceMode.VelocityChange);

            // Downforce: faster = pressed harder onto the road, so kerbs don't launch the car.
            rb.AddForce(-transform.up * Mathf.Abs(forwardSpeed) * downforce, ForceMode.Acceleration);

            // BUG FIX (cars spinning like tops after a hit): steering is done with MoveRotation, so any
            // physics yaw spin on the ground comes from collisions. Tyres would stop that quickly — kill
            // most of it each step, and damp roll/pitch wobble so bumps don't tip the car over.
            Vector3 localAv = transform.InverseTransformDirection(rb.angularVelocity);
            localAv.y *= handbrake ? 0.9f : 0.75f;
            localAv.x *= 0.8f;
            localAv.z *= 0.8f;
            rb.angularVelocity = transform.TransformDirection(localAv);
        }

        // Slowdown. The body has no friction, so without this the car would drift/roll forever
        // (and slide down any slope) when no key is pressed.
        bool noThrottle = Mathf.Abs(throttleInput) < 0.05f;
        rb.linearDamping = handbrake ? brakeDrag : (noThrottle ? coastDrag : 0.3f);

        // Parked: no throttle and almost stopped on the ground → hold the car still.
        if (noThrottle && IsGrounded && upright)
        {
            Vector3 v = rb.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.magnitude < 2f) rb.linearVelocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
        }

        AutoRecover();
        groundContact = false; // refreshed by OnCollisionStay each physics step
    }

    private bool groundContact;

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint c in collision.contacts)
        {
            // Only static ground counts: standing on top of another car is not "on the road".
            if (collision.rigidbody == null && Vector3.Dot(c.normal, transform.up) > 0.5f) { groundContact = true; return; }
        }
    }

    [Header("Stability")]
    [SerializeField] private float downforce = 0.25f;
    [Tooltip("Seconds flipped, on its side, or in the air before the car is put back on the road.")]
    [SerializeField] private float recoverAfter = 1.5f;

    public bool IsGrounded { get; private set; }

    private float troubleTimer;

    private bool CheckGrounded()
    {
        if (groundContact) return true; // physically resting on something below

        BoxCollider box = GetComponent<BoxCollider>();
        float halfHeight = box != null ? box.size.y * 0.5f : 0.5f;
        Vector3 origin = transform.TransformPoint(box != null ? box.center : Vector3.zero);
        foreach (RaycastHit hit in Physics.RaycastAll(origin, -transform.up, halfHeight + 0.6f))
        {
            if (hit.rigidbody == rb || hit.collider.isTrigger) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Flipped, on its side, or flying for 1.5 s → put the car back upright on the nearest road.
    /// </summary>
    private void AutoRecover()
    {
        bool tilted = transform.up.y < 0.7f;
        // "Flying" = no ground under the car AND really off the road (rising or falling fast),
        // so small bumps at speed never trigger a reset.
        bool flying = !IsGrounded && Mathf.Abs(rb.linearVelocity.y) > 2f;
        if (tilted || flying) troubleTimer += Time.fixedDeltaTime;
        else troubleTimer = 0f;

        if (troubleTimer < recoverAfter) return;
        troubleTimer = 0f;

        Vector3 pos = rb.position;
        // Prefer the nearest point on the road network (NavMesh), else the ground right below.
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit nav, 15f, UnityEngine.AI.NavMesh.AllAreas))
            pos = nav.position;
        else if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 60f))
            pos = ground.point;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = pos + Vector3.up * 1f;
        rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
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
