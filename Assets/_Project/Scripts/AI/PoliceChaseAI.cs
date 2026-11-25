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
    [Tooltip("How fast the police turns its steering wheel (full lock per second).")]
    [SerializeField] private float steerRate = 3f;
    private float smoothSteer;
    private bool turningAround;
    private float turnAroundTimer;
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

    /// <summary>Turns this police car into an interceptor (used by PoliceSpawner for the extra cars).</summary>
    public void MakeInterceptor(float topSpeed, float accel)
    {
        maxSpeed = topSpeed;
        catchUpBoost = 1.05f; // already fast enough
        if (car == null) car = GetComponent<SimpleCarController>();
        car.SetMaxSpeed(topSpeed);
        car.SetAcceleration(accel);
    }

    /// <summary>Empties the BUSTING meter of every police car (Repair pickup).</summary>
    public static void ClearAllBustProgress()
    {
        foreach (PoliceChaseAI p in FindObjectsByType<PoliceChaseAI>(FindObjectsSortMode.None)) p.closeTimer = 0f;
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
        // Player now tops out at 180 km/h: police needs ~150 km/h (+ catch-up boost) to stay a threat.
        maxSpeed = Mathf.Max(maxSpeed, 42f);
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
        // Smooth steering: turn the wheel gradually instead of snapping left/right every frame
        // (the snapping caused the zig-zag "weird" driving).
        float targetSteer = Mathf.Clamp(angle / 45f, -1f, 1f);
        smoothSteer = Mathf.MoveTowards(smoothSteer, targetSteer, steerRate * Time.fixedDeltaTime);
        float steer = smoothSteer;

        // BUG FIX (police driving into buildings): "whiskers" look ahead for walls and steer away.
        float wallBrake = AvoidWalls(ref steer);

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
        // HIT-AND-RUN FIX: after ramming, the police used to fly past and keep going (or circle wide).
        // Now, if the player ends up behind us nearby, we brake hard, then reverse-turn until our nose
        // points back at them, then ram again.
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float playerAngle = Vector3.SignedAngle(transform.forward, toPlayer, Vector3.up);
        turnAroundCooldown -= Time.fixedDeltaTime;
        bool playerBehind = distance < 30f && Mathf.Abs(playerAngle) > 100f && turnAroundCooldown <= 0f && HasLineOfSight();

        if (turningAround)
        {
            // Reverse with the wheel turned AWAY from the player: the nose swings toward them.
            car.SetInput(-1f, -Mathf.Sign(playerAngle), false);
            turnAroundTimer -= Time.fixedDeltaTime;
            Status = "Turning around";
            // BUG FIX (police wiggling back and forth forever): stop when facing the player, when time
            // is up, or when backing into something — then wait before trying another turn-around.
            if (Mathf.Abs(playerAngle) < 50f || turnAroundTimer <= 0f || BlockedBehind())
            {
                turningAround = false;
                turnAroundCooldown = 3f;
            }
        }
        else if (playerBehind && !nearTarget)
        {
            if (car.ForwardSpeed > 4f)
            {
                car.SetInput(-1f, 0f, false); // brake first (negative throttle while rolling forward)
                Status = "Braking to turn around";
            }
            else
            {
                turningAround = true;
                turnAroundTimer = 2f;
            }
        }
        else if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;
            car.SetInput(-1f, -steer, false); // reverse and turn the other way
            Status = "Stuck - reversing";
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
            safeSpeed = Mathf.Min(safeSpeed, Mathf.Lerp(maxSpeed * boost, 6f, wallBrake));
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
            if (h.collider.isTrigger || h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(target)) continue;
            if (h.rigidbody != null) continue; // other cars (traffic) don't block the view of the player
            return false;
        }
        return true;
    }

    private float turnAroundCooldown;

    /// <summary>
    /// Three short ray "whiskers" (centre, left, right) in front of the car. A wall ahead nudges the
    /// steering away from it. Returns 0..1: how hard to slow down (1 = wall right in front).
    /// The player car and the ground are ignored.
    /// </summary>
    private float AvoidWalls(ref float steer)
    {
        float speed = Mathf.Max(car.ForwardSpeed, 0f);
        if (speed < 2f) return 0f;
        float length = Mathf.Clamp(6f + speed * 0.7f, 6f, 25f);
        Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 2f;

        float centre = WhiskerHit(origin, transform.forward, length);
        float left = WhiskerHit(origin, Quaternion.Euler(0f, -30f, 0f) * transform.forward, length * 0.7f);
        float right = WhiskerHit(origin, Quaternion.Euler(0f, 30f, 0f) * transform.forward, length * 0.7f);

        // Closer hit on one side → steer to the other side.
        float push = right - left;
        if (centre > 0f && Mathf.Abs(push) < 0.1f) push = steer >= 0f ? 1f : -1f; // dead ahead: keep turning the way we were
        steer = Mathf.Clamp(steer - push * 0.8f * Mathf.Max(centre, Mathf.Max(left, right)), -1f, 1f);
        return centre;
    }

    /// <summary>0 = nothing within length, 1 = obstacle right at the origin.</summary>
    private float WhiskerHit(Vector3 origin, Vector3 dir, float length)
    {
        foreach (RaycastHit h in Physics.RaycastAll(origin, dir, length))
        {
            if (h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
            if (target != null && h.collider.transform.IsChildOf(target)) continue; // ramming the player is fine
            if (h.normal.y > 0.7f) continue; // road / ramp surface, not a wall
            return 1f - h.distance / length;
        }
        return 0f;
    }

    private bool BlockedBehind()
    {
        Vector3 origin = transform.position + Vector3.up * 0.7f - transform.forward * 2f;
        return WhiskerHit(origin, -transform.forward, 2.5f) > 0f;
    }

    private bool HasPath =>path != null && path.corners.Length >= 2;

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

        // "Carrot on a stick": aim at a point a fixed distance AHEAD along the route (further at speed),
        // not at the next corner itself. Gives smooth, wide turns instead of jerky corner-to-corner driving.
        float lookahead = Mathf.Clamp(8f + Mathf.Abs(car.ForwardSpeed) * 0.6f, 8f, 30f);
        return PointAlongPath(corners, lookahead);
    }

    private Vector3 PointAlongPath(Vector3[] corners, float lookahead)
    {
        // Start from the point on the path closest to us (projected onto the first segment).
        Vector3 pos = transform.position;
        Vector3 a = corners[0], b = corners[1];
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude) : 1f;
        Vector3 current = a + ab * t;
        float remaining = lookahead;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 next = corners[i];
            float seg = FlatDistance(current, next);
            if (seg >= remaining) return Vector3.Lerp(current, next, remaining / Mathf.Max(seg, 0.001f));
            remaining -= seg;
            current = next;
        }
        cornerIndex = corners.Length - 1;
        return corners[corners.Length - 1];
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
