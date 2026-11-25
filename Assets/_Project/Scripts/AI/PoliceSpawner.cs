using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// - Adds extra police "interceptors" (copies of the scene's police car with a much higher top speed,
///   default 80 m/s ≈ 288 km/h). They are placed right beside the original police car when the game
///   starts, so all police cars set off together from the same direction.
/// - Shows "POLICE IS COMING!!" every time a police car closes in on the player.
/// RaceHUD adds this component automatically.
/// </summary>
public class PoliceSpawner : MonoBehaviour
{
    [SerializeField] private int extraPolice = 2;
    [Tooltip("Interceptor top speed in m/s (80 = 288 km/h).")]
    [SerializeField] private float interceptorSpeed = 80f;
    [SerializeField] private float interceptorAcceleration = 40f;
    [Tooltip("Sideways gap between police cars in the starting formation (m).")]
    [SerializeField] private float formationSpacing = 4.5f;

    [Header("Warning")]
    [Tooltip("Show the warning when a police car gets closer than this (m).")]
    [SerializeField] private float warnDistance = 45f;
    [Tooltip("The warning can show again once every police car is further than this (m).")]
    [SerializeField] private float rearmDistance = 80f;

    private bool spawned;
    private bool warningArmed = true;
    private float lastWarning = -99f;

    private void Start()
    {
        // Spawn immediately (still in the menu, cars are held still), so everyone starts together.
        TrySpawnFormation();
    }

    private void Update()
    {
        if (!spawned) TrySpawnFormation();
        if (GameState.IsPlaying) UpdateWarning();
    }

    private void TrySpawnFormation()
    {
        PoliceChaseAI original = FindAnyObjectByType<PoliceChaseAI>();
        if (original == null) return;
        spawned = true;

        Transform t = original.transform;
        // Slots: right, left, then further back right/left if more are added.
        var slots = new List<Vector3>();
        for (int i = 0; i < extraPolice; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            int row = i / 2;
            slots.Add(t.position + t.right * side * formationSpacing * (row + 1) - t.forward * row * 6f);
        }

        int n = 0;
        foreach (Vector3 slot in slots)
        {
            Vector3 pos = slot;
            // Keep it on the road (NavMesh); if the side is blocked, line up behind instead.
            if (!NavMesh.SamplePosition(slot, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                Vector3 behind = t.position - t.forward * 7f * (n + 1);
                if (NavMesh.SamplePosition(behind, out hit, 4f, NavMesh.AllAreas)) pos = hit.position;
            }
            else pos = hit.position;
            pos.y = t.position.y; // same height as the original (already sitting on the road)

            GameObject clone = Instantiate(original.gameObject, pos, t.rotation);
            clone.name = "PoliceInterceptor_" + (++n);
            // Runtime-added audio may have been copied too: remove it, AudioManager adds fresh audio.
            foreach (CarAudio a in clone.GetComponents<CarAudio>()) DestroyImmediate(a);
            foreach (AudioSource s in clone.GetComponents<AudioSource>()) DestroyImmediate(s);
            clone.GetComponent<PoliceChaseAI>().MakeInterceptor(interceptorSpeed, interceptorAcceleration);
        }
    }

    private void UpdateWarning()
    {
        float nearest = float.MaxValue;
        foreach (PoliceChaseAI p in FindObjectsByType<PoliceChaseAI>(FindObjectsSortMode.None))
        {
            if (p.Target == null) continue;
            nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, p.Target.position));
        }
        if (nearest == float.MaxValue) return;

        if (nearest > rearmDistance) warningArmed = true;
        if (warningArmed && nearest < warnDistance && Time.time - lastWarning > 6f)
        {
            warningArmed = false;
            lastWarning = Time.time;
            PickupSpawner.ShowToast("POLICE IS COMING!!", new Color(1f, 0.2f, 0.15f));
        }
    }
}
