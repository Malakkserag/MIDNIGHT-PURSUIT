using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns civilian traffic on the streets around the player once the game starts.
/// Traffic cars reuse the player car's 3D model in random colours. Cars that fall far behind
/// are removed and new ones appear out of sight, so there is always traffic nearby.
/// RaceHUD adds this component automatically.
/// </summary>
public class TrafficManager : MonoBehaviour
{
    [SerializeField] private int count = 8;
    [SerializeField] private float spawnMin = 70f;
    [SerializeField] private float spawnMax = 170f;
    [SerializeField] private float despawnDistance = 230f;

    private static readonly Color[] Paints =
    {
        new Color(0.8f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.1f, 0.1f),
        new Color(0.95f, 0.75f, 0.1f), new Color(0.2f, 0.6f, 0.25f), new Color(0.55f, 0.55f, 0.6f),
        new Color(0.9f, 0.45f, 0.1f),
    };

    private static GameObject[] models;
    private readonly List<TrafficCar> cars = new List<TrafficCar>();
    private SimpleCarController player;
    private float nextCheck;

    private void Update()
    {
        if (!GameState.IsPlaying || Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;

        if (player == null)
        {
            foreach (SimpleCarController c in FindObjectsByType<SimpleCarController>(FindObjectsSortMode.None))
                if (c.PlayerControlled && c.GetComponent<PoliceChaseAI>() == null) player = c;
            if (player == null) return;
        }

        for (int i = cars.Count - 1; i >= 0; i--)
        {
            TrafficCar t = cars[i];
            if (t == null) { cars.RemoveAt(i); continue; }
            if (Vector3.Distance(t.transform.position, player.transform.position) > despawnDistance && !Visible(t.transform.position))
            {
                Destroy(t.gameObject);
                cars.RemoveAt(i);
            }
        }

        // Spawn at most 2 per second so it never hitches.
        for (int n = 0; n < 2 && cars.Count < count; n++)
        {
            if (TryFindSpot(out Vector3 pos, out Quaternion rot)) cars.Add(Spawn(pos, rot));
        }
    }

    private bool TryFindSpot(out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = Quaternion.identity;
        for (int i = 0; i < 5; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(spawnMin, spawnMax);
            Vector3 guess = player.transform.position + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 10f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - player.transform.position.y) > 6f) continue;
            if (Visible(hit.position)) continue; // never pop in on screen

            // Room to spawn: no other car (or anything solid) in the way.
            bool blocked = false;
            foreach (Collider c in Physics.OverlapSphere(hit.position + Vector3.up * 1.2f, 4f))
                if (!c.isTrigger && c.attachedRigidbody != null) { blocked = true; break; }
            if (blocked) continue;

            // Face along the road: use the direction of a short path from here.
            var p = new NavMeshPath();
            Vector3 ahead = hit.position + Random.insideUnitSphere * 30f;
            Vector3 face = Random.insideUnitSphere;
            if (NavMesh.SamplePosition(ahead, out NavMeshHit a, 10f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(hit.position, a.position, NavMesh.AllAreas, p) && p.corners.Length >= 2)
                face = p.corners[1] - p.corners[0];
            face.y = 0f;
            if (face.sqrMagnitude < 0.01f) face = Vector3.forward;

            pos = hit.position;
            rot = Quaternion.LookRotation(face, Vector3.up);
            return true;
        }
        return false;
    }

    private TrafficCar Spawn(Vector3 pos, Quaternion rot)
    {
        BoxCollider playerBox = player.GetComponent<BoxCollider>();
        Rigidbody playerRb = player.GetComponent<Rigidbody>();

        var go = new GameObject("TrafficCar");
        go.transform.SetPositionAndRotation(pos, rot);

        var box = go.AddComponent<BoxCollider>();
        if (playerBox != null) { box.center = playerBox.center; box.size = playerBox.size; }
        else { box.center = new Vector3(0f, 0.7f, 0f); box.size = new Vector3(1.9f, 1.3f, 4.4f); }
        // Sit on the road.
        go.transform.position = pos + Vector3.up * (box.size.y * 0.5f - box.center.y + 0.15f);

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = playerRb != null ? playerRb.mass : 1200f;

        // Visual: a real colour variant of the sports car (Resources/TrafficCars: red, white, black,
        // yellow, green, grey...). Falls back to a repainted copy of the player's model.
        if (models == null) models = Resources.LoadAll<GameObject>("TrafficCars");
        Transform model = player.transform.Find("Model");
        GameObject source = models.Length > 0 ? models[Random.Range(0, models.Length)] : (model != null ? model.gameObject : null);
        if (source != null)
        {
            GameObject m = Instantiate(source, go.transform, false);
            m.name = "Model";
            if (model != null) // same placement as the player's model (some models are turned 180°)
            {
                m.transform.localPosition = model.localPosition;
                m.transform.localRotation = model.localRotation;
                m.transform.localScale = model.localScale;
            }
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Rigidbody r in m.GetComponentsInChildren<Rigidbody>()) Destroy(r);
            foreach (Animator a in m.GetComponentsInChildren<Animator>()) Destroy(a);
            if (models.Length == 0) Repaint(m, Paints[Random.Range(0, Paints.Length)]);
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(go.transform, false);
            cube.transform.localPosition = box.center;
            cube.transform.localScale = box.size;
            cube.GetComponent<Renderer>().material.color = Paints[Random.Range(0, Paints.Length)];
        }

        go.AddComponent<SimpleCarController>(); // TrafficCar.Awake switches it to AI control
        return go.AddComponent<TrafficCar>();
    }

    /// <summary>Recolours the car paint: any clearly coloured (saturated) material gets the new colour.</summary>
    private static void Repaint(GameObject model, Color paint)
    {
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.materials; // per-instance copies
            foreach (Material mat in mats)
            {
                string prop = mat.HasProperty("_BaseColor") ? "_BaseColor" : (mat.HasProperty("_Color") ? "_Color" : null);
                if (prop == null) continue;
                Color c = mat.GetColor(prop);
                Color.RGBToHSV(c, out _, out float s, out float v);
                if (s > 0.35f && v > 0.15f) mat.SetColor(prop, new Color(paint.r, paint.g, paint.b, c.a));
            }
            r.materials = mats;
        }
    }

    private static bool Visible(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.z < 120f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
    }
}
