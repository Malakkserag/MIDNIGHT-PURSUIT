using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whole-map minimap in the top-right corner with a marker for every car.
/// Renders the map ONCE from a top-down camera at startup (the city doesn't move), so it's cheap.
/// Cars are found automatically: police = flashing red/blue, player = cyan arrow, any other car = yellow.
/// RaceHUD adds this component automatically.
/// </summary>
public class Minimap : MonoBehaviour
{
    [Tooltip("Minimap width/height as a fraction of screen height, so it scales with the window.")]
    [Range(0.1f, 0.5f)][SerializeField] private float sizeOfScreen = 0.22f;
    [SerializeField] private int margin = 16;
    [SerializeField] private int textureSize = 1024;
    [Tooltip("Brightens the dark night map so streets are readable.")]
    [SerializeField] private float mapBrightness = 2.5f;
    [Tooltip("Ignore objects bigger than this (sky domes, giant ground planes) when sizing the map.")]
    [SerializeField] private float maxObjectSize = 400f;

    private Camera mapCamera;
    private RenderTexture mapTexture;
    private int framesRendered;
    private Bounds mapBounds;
    private readonly List<SimpleCarController> cars = new List<SimpleCarController>();
    private float nextCarScan;
    private Texture2D white;

    private void Start()
    {
        white = Texture2D.whiteTexture;
        mapBounds = ComputeMapBounds();

        mapTexture = new RenderTexture(textureSize, textureSize, 24) { name = "MinimapRT" };

        var go = new GameObject("MinimapCamera");
        go.transform.SetParent(transform, false);
        mapCamera = go.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = Mathf.Max(mapBounds.extents.x, mapBounds.extents.z);
        mapCamera.transform.position = new Vector3(mapBounds.center.x, mapBounds.max.y + 50f, mapBounds.center.z);
        mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // north (+Z) is up on the map
        mapCamera.nearClipPlane = 1f;
        mapCamera.farClipPlane = mapBounds.size.y + 200f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
        mapCamera.targetTexture = mapTexture;
        mapCamera.depth = -10;
    }

    private void LateUpdate()
    {
        // Let the camera render a couple of frames, then switch it off: the map is static.
        if (mapCamera != null && mapCamera.enabled && ++framesRendered > 2) mapCamera.enabled = false;

        if (Time.time >= nextCarScan)
        {
            nextCarScan = Time.time + 1f;
            cars.Clear();
            cars.AddRange(FindObjectsByType<SimpleCarController>(FindObjectsSortMode.None));
        }
    }

    private void OnDestroy()
    {
        if (mapTexture != null) mapTexture.Release();
    }

    private void OnGUI()
    {
        if (mapTexture == null || mapCamera == null) return;

        float screenSize = Screen.height * sizeOfScreen;
        Rect r = new Rect(Screen.width - screenSize - margin, margin, screenSize, screenSize);

        // Frame + map
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), white);
        GUI.color = new Color(mapBrightness, mapBrightness, mapBrightness, 1f);
        GUI.DrawTexture(r, mapTexture, ScaleMode.StretchToFill, false);
        GUI.color = Color.white;

        foreach (SimpleCarController car in cars)
        {
            if (car == null) continue;
            Vector3 vp = mapCamera.WorldToViewportPoint(car.transform.position);
            vp.x = Mathf.Clamp01(vp.x);
            vp.y = Mathf.Clamp01(vp.y);
            Vector2 pos = new Vector2(r.x + vp.x * r.width, r.y + (1f - vp.y) * r.height);

            bool police = car.GetComponent<PoliceChaseAI>() != null;
            Color c;
            float size;
            if (police)
            {
                c = Mathf.Repeat(Time.time * 4f, 2f) < 1f ? Color.red : new Color(0.2f, 0.5f, 1f);
                size = 7f;
            }
            else if (car.PlayerControlled)
            {
                c = Color.cyan;
                size = 8f;
            }
            else
            {
                c = Color.yellow;
                size = 6f;
            }

            DrawMarker(pos, car.transform.eulerAngles.y, size, c, car.PlayerControlled && !police);
        }
    }

    private void DrawMarker(Vector2 pos, float yaw, float size, Color color, bool withArrow)
    {
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(yaw, pos);

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(pos.x - size / 2 - 1, pos.y - size / 2 - 1, size + 2, size + 2), white);
        GUI.color = color;
        GUI.DrawTexture(new Rect(pos.x - size / 2, pos.y - size / 2, size, size), white);
        if (withArrow) // nose shows which way you're facing
            GUI.DrawTexture(new Rect(pos.x - 1.5f, pos.y - size / 2 - 5, 3, 5), white);

        GUI.color = Color.white;
        GUI.matrix = old;
    }

    private Bounds ComputeMapBounds()
    {
        bool any = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.one * 100f);
        foreach (Renderer rend in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (rend.GetComponentInParent<SimpleCarController>() != null) continue;
            if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer) continue;
            Bounds rb = rend.bounds;
            if (rb.size.x > maxObjectSize || rb.size.z > maxObjectSize) continue;
            if (!any) { b = rb; any = true; }
            else b.Encapsulate(rb);
        }
        b.Expand(new Vector3(10f, 0f, 10f));
        return b;
    }
}
