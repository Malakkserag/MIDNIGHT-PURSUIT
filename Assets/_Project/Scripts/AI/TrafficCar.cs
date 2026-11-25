using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Civilian traffic: drives calmly along the streets (NavMesh) to random destinations,
/// slows down for corners and brakes for anything in front of it. Uses the same
/// SimpleCarController physics as the player and police, so crashes are real.
/// Created by TrafficManager.
/// </summary>
[RequireComponent(typeof(SimpleCarController))]
public class TrafficCar : MonoBehaviour
{
    [SerializeField] private float cruiseSpeed = 28f;  // ~100 km/h
    [SerializeField] private float steerRate = 1.6f;
    [Tooltip("How far right of the route's centre line to drive (m). ~half a lane width + a bit.")]
    [SerializeField] private float laneOffset = 3f;

    /// <summary>Moves from 'from' toward 'to' but stops at the road edge (NavMesh boundary).</summary>
    private static Vector3 KeepOnRoad(Vector3 from, Vector3 to)
    {
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 3f, NavMesh.AllAreas)) return from;
        if (NavMesh.Raycast(start.position, to, out NavMeshHit edge, NavMesh.AllAreas))
            return Vector3.Lerp(start.position, edge.position, 0.7f); // stay a little inside the edge
        return to;
    }

    private SimpleCarController car;
    private NavMeshPath path;
    private float smoothSteer;
    private float stuckTimer;
    private float reverseTimer;
    private float nextRepath;
    private int stuckCount;

    private void Awake()
    {
        car = GetComponent<SimpleCarController>();
        car.PlayerControlled = false;
        car.SetMaxSpeed(cruiseSpeed);
        path = new NavMeshPath();
    }

    private void FixedUpdate()
    {
        if (!GameState.IsPlaying)
        {
            car.SetInput(0f, 0f, true);
            return;
        }

        if (!hasDestination || ReachedEnd()) PickDestination();
        else if (path.corners.Length < 2 || Time.time >= nextRepath) Repath();

        float look = Mathf.Clamp(10f + Mathf.Abs(car.ForwardSpeed) * 0.9f, 10f, 35f);
        Vector3 aim = PointAlongPath(look);
        // Drive in the RIGHT-HAND LANE: the NavMesh route hugs the middle / inside of the road, so shift
        // the aim point sideways to the right of the route direction (but never off the road).
        Vector3 routeDir = PointAlongPath(look + 3f) - aim;
        routeDir.y = 0f;
        if (routeDir.sqrMagnitude < 0.01f) routeDir = transform.forward;
        aim = KeepOnRoad(aim, aim + Vector3.Cross(Vector3.up, routeDir.normalized) * laneOffset);

        Vector3 to = aim - transform.position;
        to.y = 0f;
        float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
        // Gentle, damped steering: small corrections on straights, so no swinging left and right.
        float wanted = Mathf.Clamp(angle / 55f, -1f, 1f);
        if (Mathf.Abs(angle) < 2f) wanted = 0f; // dead zone: go straight
        smoothSteer = Mathf.MoveTowards(smoothSteer, wanted, steerRate * Time.fixedDeltaTime);

        // Corner speed + brake only for CARS ahead (walls are handled by following the road path;
        // braking for walls/kerbs is what left traffic frozen in the middle of the road).
        float sharp = Mathf.InverseLerp(10f, 70f, Mathf.Abs(angle));
        float safe = Mathf.Lerp(cruiseSpeed, 8f, sharp);
        float ahead = ObstacleAhead();
        safe = Mathf.Min(safe, Mathf.Lerp(cruiseSpeed, 0f, ahead));

        // Stuck = barely moving for 2 s, for ANY reason (blocked by a car, wedged on a kerb...):
        // back up while turning, pick a brand new destination. Stuck a long time out of view: respawn.
        float speed = car.ForwardSpeed;
        if (Mathf.Abs(speed) < 1f && reverseTimer <= 0f) stuckTimer += Time.fixedDeltaTime; else stuckTimer = 0f;
        if (stuckTimer > 2f)
        {
            reverseTimer = 1.3f;
            stuckTimer = 0f;
            hasDestination = false;
            stuckCount++;
        }
        if (Mathf.Abs(speed) > 5f) stuckCount = 0;
        if (stuckCount >= 3) { Destroy(gameObject); return; } // TrafficManager spawns a fresh one elsewhere

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;
            car.SetInput(-0.7f, -smoothSteer, false);
        }
        else if (ahead > 0.85f)
        {
            car.SetInput(0f, smoothSteer, true); // car right in front: stop
        }
        else
        {
            float throttle = speed < safe ? 1f : (speed > safe + 4f ? -0.6f : 0.15f);
            car.SetInput(throttle, smoothSteer, false);
        }
    }

    private Vector3 destination;
    private bool hasDestination;

    private bool ReachedEnd() => FlatDistance(transform.position, destination) < 10f;

    private void PickDestination()
    {
        nextRepath = Time.time + 1f;
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(60f, 200f);
            Vector3 guess = transform.position + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(guess, out NavMeshHit dest, 15f, NavMesh.AllAreas)) continue;
            destination = dest.position;
            hasDestination = true;
            if (Repath()) return;
        }
        hasDestination = false;
    }

    /// <summary>Recalculates the route from where we are now to the current destination.</summary>
    private bool Repath()
    {
        nextRepath = Time.time + 1f;
        bool ok = NavMesh.SamplePosition(transform.position, out NavMeshHit from, 8f, NavMesh.AllAreas)
               && NavMesh.CalculatePath(from.position, destination, NavMesh.AllAreas, path)
               && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2;
        if (!ok) { path.ClearCorners(); hasDestination = false; }
        return ok;
    }

    /// <summary>0 = clear road, 1 = obstacle touching our bumper. Looks further at higher speed.</summary>
    private float ObstacleAhead()
    {
        float length = 5f + Mathf.Max(car.ForwardSpeed, 0f) * 0.6f;
        Vector3 origin = transform.position + Vector3.up * 0.7f + transform.forward * 2.2f;
        float worst = 0f;
        foreach (float yaw in new[] { 0f, -12f, 12f })
        {
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * transform.forward;
            foreach (RaycastHit h in Physics.RaycastAll(origin, dir, length))
            {
                if (h.collider.isTrigger || h.collider.transform.IsChildOf(transform)) continue;
                if (h.rigidbody == null) continue; // only other cars count
                worst = Mathf.Max(worst, 1f - h.distance / length);
            }
        }
        return worst;
    }

    private Vector3 PointAlongPath(float lookahead)
    {
        Vector3[] c = path.corners;
        if (c.Length < 2) return transform.position + transform.forward * 10f;
        Vector3 a = c[0], b = c[1], ab = b - a;
        float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(transform.position - a, ab) / ab.sqrMagnitude) : 1f;
        Vector3 cur = a + ab * t;
        float rem = lookahead;
        for (int i = 1; i < c.Length; i++)
        {
            float seg = FlatDistance(cur, c[i]);
            if (seg >= rem) return Vector3.Lerp(cur, c[i], rem / Mathf.Max(seg, 0.001f));
            rem -= seg;
            cur = c[i];
        }
        return c[c.Length - 1];
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
