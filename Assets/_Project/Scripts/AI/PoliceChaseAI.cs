using System;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Pathfinding (needs a baked NavMesh)")]
    [Tooltip("Closer than this, drive straight at the player instead of following the path.")]
    [SerializeField] private float directChaseDistance = 15f;
    [SerializeField] private float cornerReachDistance = 6f;
    [Tooltip("Top-speed multiplier when far behind the player (rubber-banding).")]
    [SerializeField] private float catchUpBoost = 1.35f;
    [Tooltip("Seconds without getting closer before the police respawns on the road behind the player.")]
    [SerializeField] private float respawnAfter = 8f;

    [Header("Catching")]
    [Tooltip("Max gap (m) between the two car bodies that counts as touching. Works on any side.")]
    [SerializeField] private float touchGap = 0.1f;
    [Tooltip("Seconds of contact needed to bust the player.")]
    [SerializeField] private float bustContactTime = 0.5f;
    [Tooltip("How fast the contact meter empties when not touching (0.35 = takes ~3.4 s to fully empty).")]
    [SerializeField] private float contactDrainRate = 0.35f;

    /// <summary>0..1: how close the player is to being busted (for a HUD bar).</summary>
    public float BustProgress => Mathf.Clamp01(closeTimer / bustContactTime);

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
        if (!GameState.IsPlaying)
        {
            car.SetInput(0f, 0f, true); // wait in the menu; start delay counts from PLAY
            Status = GameState.Current.ToString();
            return;
        }

        timeAlive += Time.fixedDeltaTime;
        if (target == null || IsBusted || timeAlive < startDelay)
        {
            car.SetInput(0f, 0f, true);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Follow the streets: aim at the next corner of the NavMesh path (straight at the player if close
        // or if the scene has no NavMesh baked).
        // Close up: aim where the player is GOING (a little ahead of them), so the police cuts in
        // and rams instead of chasing its tail around them.
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        Vector3 lead = targetRb != null ? targetRb.linearVelocity * Mathf.Clamp(distance / 20f, 0f, 0.6f) : Vector3.zero;
        // Only drive straight at the player when nothing is in between. A wall/hedge between us
        // means: follow the road path around it instead of pushing into the wall forever.
        bool closeRange = distance < directChaseDistance && HasLineOfSight();
        Vector3 aimPoint = closeRange ? target.position + lead : NextPathPoint();
        Vector3 toAim = aimPoint - transform.position;
        toAim.y = 0f;
        float angle = Vector3.SignedAngle(transform.forward, toAim, Vector3.up);
        float steer = Mathf.Clamp(angle / 35f, -1f, 1f);

        // Catch-up: the further behind, the faster (up to catchUpBoost x normal top speed).
        float boost = Mathf.Lerp(1f, catchUpBoost, Mathf.InverseLerp(40f, 150f, distance));
        car.SetMaxSpeed(maxSpeed * boost);

        float gap = BodyGap();
        bool nearTarget = gap < touchGap + 2f;

        // Stuck against a wall? Back up for a moment.
        // (Not when pushing against the player â€” that's the police doing its job.)
        if (Mathf.Abs(car.ForwardSpeed) < 1f && !nearTarget) stuckTimer += Time.fixedDeltaTime;
        else stuckTimer = 0f;
        if (stuckTimer > 1.2f)
        {
            reverseTimer = 1f;
            stuckTimer = 0f;
            nextRepath = 0f; // pick a fresh route after backing off
        }

        // Not getting any closer for a long time (stuck, lost, or hopelessly behind)?
        // Respawn on the road behind the player, out of the player's sight.
        // Progress = getting closer, or actually touching the player. Merely being near
        // (e.g. stuck behind a wall next to them) is NOT progress.
        if (distance < bestDistance - 5f || BustProgress > 0f)
        {
            bestDistance = distance;
            noProgressTimer = 0f;
        }
        else
        {
            noProgressTimer += Time.fixedDeltaTime;
        }
        if (noProgressTimer > respawnAfter && TryRespawnBehindTarget())
        {
            noProgressTimer = 0f;
            bestDistance = float.MaxValue;
            Status = "Respawned behind player";
            return;
        }

        // Player beside/behind us at close range and we're slow? Circling won't work (a car can't turn
        // on the spot). Do a quick reverse-turn to point the nose at the player, like a 3-point turn.
        if (closeRange && reverseTimer <= 0f && !nearTarget && Mathf.Abs(angle) > 75f && car.ForwardSpeed < 8f)
        {
            reverseTimer = 0.8f;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;
            car.SetInput(-1f, -steer, false); // reverse and turn the other way
            Status = closeRange ? "Turning toward player" : "Stuck - reversing";
        }
        else if (closeRange)
        {
            // Ram: full throttle at the player, no corner braking.
            car.SetInput(1f, steer, false);
            Status = "Ramming player";
        }
        else
        {
            // Slow down for corners like a real driver: the sharper the turn ahead and the closer it is,
            // the lower the safe speed. Over it â†’ lift off / brake instead of slamming into buildings.
            float cornerDist = FlatDistance(transform.position, aimPoint);
            float sharpness = Mathf.InverseLerp(15f, 90f, Mathf.Abs(angle));
            float nearness = Mathf.InverseLerp(40f, 8f, cornerDist);
            float safeSpeed = Mathf.Lerp(maxSpeed * boost, 9f, sharpness * Mathf.Max(nearness, sharpness > 0.8f ? 1f : 0f));
            float speed = car.ForwardSpeed;
            float throttle = speed < safeSpeed ? 1f : (speed > safeSpeed + 4f ? -0.6f : 0.2f);
            car.SetInput(throttle, steer, false);
            Status = distance < directChaseDistance ? "Ramming player"
                   : HasPath ? $"Following streets ({distance:0} m)"
                   : $"NO PATH - driving straight ({distance:0} m)";
        }

        // Catch check: the physics engine reports the two car bodies actually colliding
        // (any side, any speed), or they are within a hair of each other.
        bool touching = touchingTarget || gap < touchGap;
        touchingTarget = false; // refreshed by OnCollisionStay every physics step
        // Contact meter: fills while touching and drains slowly between hits, so repeated
        // rams add up (cars bounce apart on every hit, which used to reset the timer).
        if (touching) closeTimer += Time.fixedDeltaTime;
        else closeTimer = Mathf.Max(0f, closeTimer - Time.fixedDeltaTime * contactDrainRate);
        if (closeTimer >= bustContactTime)
        {
            IsBusted = true;
            GameState.SetBusted();
            Busted?.Invoke();
        }
    }

    [Header("Debug (read-only)")]
    [Tooltip("What the police is doing right now. Select PoliceCar during Play to watch it.")]
    [SerializeField] private string status = "Waiting";
    public string Status { get => status; private set => status = value; }

    private float bestDistance = float.MaxValue;
    private float noProgressTimer;

    private bool HasLineOfSight()
    {
        Vector3 from = transform.position + Vector3.up * 0.8f;
        Vector3 to = target.position + Vector3.up * 0.8f;
        foreach (RaycastHit h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to)))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(target)) continue;
            return false;
        }
        return true;
    }

    private bool HasPath => path != null && path.corners.Length >= 2;

    private bool TryRespawnBehindTarget()
    {
        foreach (float back in new[] { 60f, 45f, 80f, 30f })
        {
            Vector3 guess = target.position - target.forward * back;
            if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 15f, NavMesh.AllAreas)) continue;

            // Must be on the same road level as the player (not a rooftop or a plaza above/below)
            // and actually connected to the player by road.
            if (Mathf.Abs(hit.position.y - target.position.y) > 2f) continue;
            if (!NavMesh.SamplePosition(target.position, out NavMeshHit playerOnNav, 8f, NavMesh.AllAreas)) continue;
            var check = new NavMeshPath();
            if (!NavMesh.CalculatePath(hit.position, playerOnNav.position, NavMesh.AllAreas, check)
                || check.status != NavMeshPathStatus.PathComplete) continue;

            // Don't pop into existence where the player can see it.
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(hit.position);
                if (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) continue;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            Vector3 face = target.position - hit.position;
            face.y = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = hit.position + Vector3.up * 1f;
            rb.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : target.forward, Vector3.up);
            nextRepath = 0f;
            return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (path == null || path.corners.Length < 2) return;
        Gizmos.color = Color.cyan;
        Vector3[] c = path.corners;
        for (int i = 0; i < c.Length - 1; i++) Gizmos.DrawLine(c[i] + Vector3.up, c[i + 1] + Vector3.up);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(c[Mathf.Min(cornerIndex, c.Length - 1)] + Vector3.up, 0.6f);
    }

    private NavMeshPath path;
    private float nextRepath;
    private int cornerIndex;

    /// <summary>Next point to drive toward along the street path to the player.</summary>
    private Vector3 NextPathPoint()
    {
        if (path == null) path = new NavMeshPath();

        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + 0.5f;
            cornerIndex = 1;
            bool ok = NavMesh.SamplePosition(transform.position, out NavMeshHit from, 8f, NavMesh.AllAreas)
                   && NavMesh.SamplePosition(target.position, out NavMeshHit to, 8f, NavMesh.AllAreas)
                   && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
            // A partial path ends somewhere unreachable (e.g. player on a sidewalk island): still use it
            // to get as close as possible, but an invalid one is dropped.
            if (!ok || path.status == NavMeshPathStatus.PathInvalid) path.ClearCorners();
        }

        Vector3[] corners = path.corners;
        if (corners.Length < 2) return target.position; // no NavMesh: old straight-line behaviour

        // Skip corners we've already reached.
        while (cornerIndex < corners.Length - 1 && FlatDistance(transform.position, corners[cornerIndex]) < cornerReachDistance)
            cornerIndex++;
        return corners[Mathf.Min(cornerIndex, corners.Length - 1)];
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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
