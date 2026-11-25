using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Adds extra police "interceptors" during a run: copies of the scene's police car with a much
/// higher top speed (default 80 m/s ≈ 288 km/h). They join the chase one after another, appearing
/// on the road behind the player, out of sight.
/// RaceHUD adds this component automatically.
/// </summary>
public class PoliceSpawner : MonoBehaviour
{
    [SerializeField] private int extraPolice = 2;
    [Tooltip("Escape-time seconds at which each extra police car joins.")]
    [SerializeField] private float[] joinTimes = { 15f, 35f };
    [Tooltip("Interceptor top speed in m/s (80 = 288 km/h).")]
    [SerializeField] private float interceptorSpeed = 80f;
    [SerializeField] private float interceptorAcceleration = 40f;

    private int spawned;
    private float playTime;
    private float nextTry;

    private void Update()
    {
        if (!GameState.IsPlaying || spawned >= extraPolice) return;
        playTime += Time.deltaTime;
        float joinAt = spawned < joinTimes.Length ? joinTimes[spawned] : joinTimes[joinTimes.Length - 1] + 20f * spawned;
        if (playTime < joinAt || Time.time < nextTry) return;
        nextTry = Time.time + 0.5f;

        PoliceChaseAI original = FindAnyObjectByType<PoliceChaseAI>();
        if (original == null || original.Target == null) return;
        if (!FindSpotBehind(original.Target, out Vector3 pos, out Quaternion rot)) return;

        // Lift by the collider's half height so it sits on the road.
        BoxCollider box = original.GetComponent<BoxCollider>();
        float lift = box != null ? box.size.y * 0.5f - box.center.y + 0.2f : 1f;
        GameObject clone = Instantiate(original.gameObject, pos + Vector3.up * lift, rot);
        clone.name = "PoliceInterceptor_" + (spawned + 1);

        // Runtime-added audio was copied too: remove it, AudioManager gives the clone fresh audio.
        foreach (CarAudio a in clone.GetComponents<CarAudio>()) DestroyImmediate(a);
        foreach (AudioSource s in clone.GetComponents<AudioSource>()) DestroyImmediate(s);

        clone.GetComponent<PoliceChaseAI>().MakeInterceptor(interceptorSpeed, interceptorAcceleration);
        spawned++;
        PickupSpawner.ShowToast("POLICE BACKUP INCOMING!", new Color(1f, 0.25f, 0.2f));
    }

    private static bool FindSpotBehind(Transform player, out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = Quaternion.identity;
        foreach (float back in new[] { 90f, 70f, 120f, 55f })
        {
            Vector3 guess = player.position - player.forward * back;
            if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 20f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - player.position.y) > 3f) continue;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(hit.position);
                if (vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f) continue;
            }
            bool blocked = false;
            foreach (Collider c in Physics.OverlapSphere(hit.position + Vector3.up, 4f))
                if (!c.isTrigger && c.attachedRigidbody != null) { blocked = true; break; }
            if (blocked) continue;

            Vector3 face = player.position - hit.position;
            face.y = 0f;
            pos = hit.position;
            rot = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : player.forward, Vector3.up);
            return true;
        }
        return false;
    }
}
