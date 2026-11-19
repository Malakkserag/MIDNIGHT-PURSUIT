using System;
using UnityEngine;

/// <summary>
/// Drives a SimpleCarController toward a target car. Busts the player if it stays close long enough.
/// Optionally flashes red/blue siren lights.
/// </summary>
[RequireComponent(typeof(SimpleCarController))]
public class PoliceChaseAI : MonoBehaviour
{
    /// <summary>Fired once when any police car catches the target.</summary>
    public static event Action Busted;
    public static bool IsBusted { get; private set; }

    [SerializeField] private Transform target;
    [SerializeField] private float maxSpeed = 26f;
    [SerializeField] private float startDelay = 3f;

    [Header("Catching")]
    [Tooltip("Max gap (m) between the two car bodies that counts as touching. Works on any side.")]
    [SerializeField] private float touchGap = 0.1f;
    [Tooltip("Seconds of contact needed to bust the player.")]
    [SerializeField] private float catchContactTime = 1.2f;

    [Header("Siren (optional)")]
    [SerializeField] private Light redLight;
    [SerializeField] private Light blueLight;
    [SerializeField] private float flashRate = 6f;

    private SimpleCarController car;
    private float stuckTimer;
    private float reverseTimer;
    private float closeTimer;
    private float timeAlive;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public void SetSirenLights(Light red, Light blue)
    {
        redLight = red;
        blueLight = blue;
    }

    private void Awake()
    {
        car = GetComponent<SimpleCarController>();
        car.PlayerControlled = false;
        car.SetMaxSpeed(maxSpeed);
        IsBusted = false;
    }

    private void Update()
    {
        FlashSiren();
    }

    private void FixedUpdate()
    {
        timeAlive += Time.fixedDeltaTime;
        if (target == null || IsBusted || timeAlive < startDelay)
        {
            car.SetInput(0f, 0f, true);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
        float steer = Mathf.Clamp(angle / 35f, -1f, 1f);

        float gap = BodyGap();
        bool nearTarget = gap < touchGap + 2f;

        // Stuck against a wall? Back up for a moment.
        // (Not when pushing against the player — that's the police doing its job.)
        if (Mathf.Abs(car.ForwardSpeed) < 1f && !nearTarget) stuckTimer += Time.fixedDeltaTime;
        else stuckTimer = 0f;
        if (stuckTimer > 1.2f)
        {
            reverseTimer = 1f;
            stuckTimer = 0f;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;
            car.SetInput(-1f, -steer, false); // reverse and turn the other way
        }
        else
        {
            bool sharpTurn = Mathf.Abs(angle) > 70f && car.ForwardSpeed > 12f;
            car.SetInput(sharpTurn ? 0.3f : 1f, steer, sharpTurn);
        }

        // Catch check: the physics engine reports the two car bodies actually colliding
        // (any side, any speed), or they are within a hair of each other.
        bool touching = touchingTarget || gap < touchGap;
        touchingTarget = false; // refreshed by OnCollisionStay every physics step
        if (touching)
        {
            closeTimer += Time.fixedDeltaTime;
            if (closeTimer >= catchContactTime)
            {
                IsBusted = true;
                Busted?.Invoke();
            }
        }
        else
        {
            closeTimer = 0f;
        }
    }

    private bool touchingTarget;

    private void OnCollisionStay(Collision collision)
    {
        if (target != null && collision.transform.IsChildOf(target)) touchingTarget = true;
    }

    private void OnCollisionEnter(Collision collision) => OnCollisionStay(collision);

    private Collider myCollider;
    private Collider targetCollider;

    /// <summary>Distance between the surfaces of the two car bodies (0 = touching), on any side.</summary>
    private float BodyGap()
    {
        if (myCollider == null) myCollider = GetComponent<Collider>();
        if (targetCollider == null || targetCollider.transform != target) targetCollider = target.GetComponent<Collider>();
        if (myCollider == null || targetCollider == null)
            return Vector3.Distance(transform.position, target.position); // fallback

        // Bounce between the two bodies a few times to find the true closest pair of points.
        Vector3 onMe = myCollider.bounds.center;
        Vector3 onTarget = targetCollider.ClosestPoint(onMe);
        for (int i = 0; i < 4; i++)
        {
            onMe = myCollider.ClosestPoint(onTarget);
            onTarget = targetCollider.ClosestPoint(onMe);
        }
        return Vector3.Distance(onTarget, onMe);
    }

    private void FlashSiren()
    {
        if (redLight == null || blueLight == null) return;
        bool red = Mathf.Repeat(Time.time * flashRate, 2f) < 1f;
        redLight.enabled = red;
        blueLight.enabled = !red;
    }
}
