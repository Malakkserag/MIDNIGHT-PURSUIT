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
    [SerializeField] private float startDelay = 2f;

    [Header("Catching")]
    [SerializeField] private float catchDistance = 5f;
    [SerializeField] private float catchTime = 1.5f;

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

        // Stuck against a wall? Back up for a moment.
        if (Mathf.Abs(car.ForwardSpeed) < 1f) stuckTimer += Time.fixedDeltaTime;
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

        // Catch check
        if (toTarget.magnitude < catchDistance)
        {
            closeTimer += Time.fixedDeltaTime;
            if (closeTimer >= catchTime)
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

    private void FlashSiren()
    {
        if (redLight == null || blueLight == null) return;
        bool red = Mathf.Repeat(Time.time * flashRate, 2f) < 1f;
        redLight.enabled = red;
        blueLight.enabled = !red;
    }
}
