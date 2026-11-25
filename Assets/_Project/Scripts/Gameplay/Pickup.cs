using UnityEngine;

/// <summary>
/// A glowing power-up floating above the road. Driving the player car through it applies the effect:
///   NITRO  (orange) — speed boost for a few seconds
///   REPAIR (green)  — empties the BUSTING meter
///   TIME   (cyan)   — +5 s escape time
/// Created and placed by PickupSpawner.
/// </summary>
public class Pickup : MonoBehaviour
{
    public enum Kind { Nitro, Repair, Time }

    public Kind Type { get; private set; }
    public bool Collected { get; private set; }

    private Transform visual;
    private float baseY;

    public static Color ColorOf(Kind k) => k switch
    {
        Kind.Nitro => new Color(1f, 0.5f, 0.05f),
        Kind.Repair => new Color(0.2f, 1f, 0.3f),
        _ => new Color(0.2f, 0.85f, 1f),
    };

    public static string LabelOf(Kind k) => k switch
    {
        Kind.Nitro => "NITRO!",
        Kind.Repair => "REPAIRED!",
        _ => "+5 SECONDS!",
    };

    public static Pickup Create(Kind kind, Vector3 groundPos)
    {
        var root = new GameObject("Pickup_" + kind);
        root.layer = 2; // Ignore Raycast: AI rays, ground checks and the camera look straight through it
        root.transform.position = groundPos + Vector3.up * 1.2f;

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2f;

        PrimitiveType shape = kind == Kind.Nitro ? PrimitiveType.Capsule
                           : kind == Kind.Repair ? PrimitiveType.Cube : PrimitiveType.Sphere;
        GameObject v = GameObject.CreatePrimitive(shape);
        Object.Destroy(v.GetComponent<Collider>());
        v.layer = 2;
        v.transform.SetParent(root.transform, false);
        v.transform.localScale = kind == Kind.Nitro ? new Vector3(0.6f, 0.6f, 0.6f) : Vector3.one * 0.8f;
        v.transform.localRotation = Quaternion.Euler(kind == Kind.Repair ? 45f : 0f, 0f, kind == Kind.Repair ? 45f : 0f);

        Color c = ColorOf(kind);
        Material m = v.GetComponent<Renderer>().material;
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * 3f);

        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.type = LightType.Point;
        light.color = c;
        light.range = 7f;
        light.intensity = 3f;

        Pickup p = root.AddComponent<Pickup>();
        p.Type = kind;
        p.visual = v.transform;
        p.baseY = root.transform.position.y;
        return p;
    }

    private void Update()
    {
        visual.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * 3f) * 0.25f;
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Collected || !GameState.IsPlaying) return;
        SimpleCarController car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<SimpleCarController>() : null;
        if (car == null || !car.PlayerControlled || car.GetComponent<PoliceChaseAI>() != null) return;

        Collected = true;
        switch (Type)
        {
            case Kind.Nitro:
                car.Boost(3f);
                AudioManager.PlayUI(ProcAudio.Nitro, 0.8f);
                break;
            case Kind.Repair:
                PoliceChaseAI.ClearAllBustProgress();
                break;
            case Kind.Time:
                RaceHUD hud = FindAnyObjectByType<RaceHUD>();
                if (hud != null) hud.AddEscapeTime(5f);
                break;
        }
        AudioManager.PlayUI(ProcAudio.Pickup);
        PickupSpawner.ShowToast(LabelOf(Type), ColorOf(Type));
        Sparks.Spawn(transform.position, Vector3.up, 0.6f);
        Destroy(gameObject);
    }
}
