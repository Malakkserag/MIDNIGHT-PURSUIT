using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps a handful of pickups on the roads around the player (on the NavMesh = streets only).
/// Pickups that get left far behind are moved to new spots ahead. Also shows the "NITRO!" toast.
/// RaceHUD adds this component automatically.
/// </summary>
public class PickupSpawner : MonoBehaviour
{
    [SerializeField] private int count = 10;
    [SerializeField] private float minDistance = 40f;
    [SerializeField] private float maxDistance = 180f;
    [SerializeField] private float despawnDistance = 240f;

    private readonly List<Pickup> pickups = new List<Pickup>();
    private Transform player;
    private float nextCheck;

    private static string toast;
    private static Color toastColor;
    private static float toastUntil;
    private GUIStyle toastStyle;

    public static void ShowToast(string text, Color color)
    {
        toast = text;
        toastColor = color;
        toastUntil = Time.time + 1.5f;
    }

    private void Update()
    {
        if (!GameState.IsPlaying || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;

        if (player == null)
        {
            foreach (SimpleCarController c in FindObjectsByType<SimpleCarController>(FindObjectsSortMode.None))
                if (c.PlayerControlled && c.GetComponent<PoliceChaseAI>() == null) player = c.transform;
            if (player == null) return;
        }

        // Remove collected and far-away pickups, then top up.
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            Pickup p = pickups[i];
            if (p == null || p.Collected) { pickups.RemoveAt(i); continue; }
            if (Vector3.Distance(p.transform.position, player.position) > despawnDistance)
            {
                Destroy(p.gameObject);
                pickups.RemoveAt(i);
            }
        }

        int tries = 0;
        while (pickups.Count < count && tries++ < 10)
        {
            if (!TryFindSpot(out Vector3 pos)) continue;
            // Nitro most common, Repair rarer (it's powerful).
            float r = Random.value;
            Pickup.Kind kind = r < 0.5f ? Pickup.Kind.Nitro : (r < 0.8f ? Pickup.Kind.Time : Pickup.Kind.Repair);
            pickups.Add(Pickup.Create(kind, pos));
        }
    }

    private bool TryFindSpot(out Vector3 pos)
    {
        pos = default;
        // Bias spots toward the front of the player so you actually drive into them.
        Vector2 rnd = Random.insideUnitCircle.normalized;
        Vector3 dir = new Vector3(rnd.x, 0f, rnd.y);
        Vector3 fwd = player.forward; fwd.y = 0f;
        dir = (dir + fwd.normalized * 0.8f).normalized;
        Vector3 guess = player.position + dir * Random.Range(minDistance, maxDistance);

        if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 12f, NavMesh.AllAreas)) return false;
        if (Mathf.Abs(hit.position.y - player.position.y) > 6f) return false; // same street level
        foreach (Pickup p in pickups)
            if (p != null && Vector3.Distance(p.transform.position, hit.position) < 25f) return false;
        pos = hit.position;
        return true;
    }

    private void OnGUI()
    {
        if (toast == null || Time.time > toastUntil || GameState.Current != GameState.State.Playing) return;
        float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
        if (toastStyle == null)
        {
            toastStyle = new GUIStyle(GUI.skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Arial Black", "Arial" }, 48),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
            };
        }
        toastStyle.fontSize = Mathf.RoundToInt(48 * s);
        float t = 1f - (toastUntil - Time.time) / 1.5f;      // 0→1
        float pop = 1f + 0.3f * Mathf.Max(0f, 1f - t * 6f);  // quick "pop" at the start
        float alpha = Mathf.Clamp01((toastUntil - Time.time) / 0.4f);
        Rect r = new Rect(0, Screen.height * 0.28f, Screen.width, 70 * s);

        Matrix4x4 old = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * pop, r.center);
        toastStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
        GUI.Label(new Rect(r.x + 3 * s, r.y + 3 * s, r.width, r.height), toast, toastStyle);
        Color c = toastColor; c.a = alpha;
        toastStyle.normal.textColor = c;
        GUI.Label(r, toast, toastStyle);
        GUI.matrix = old;
    }
}
