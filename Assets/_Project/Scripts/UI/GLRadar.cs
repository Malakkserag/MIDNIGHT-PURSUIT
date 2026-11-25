using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Police radar in the bottom-left corner, drawn entirely with Unity's low-level GL API
/// (OpenGL-style immediate mode: GL.Begin / GL.Vertex / GL.End).
///
/// - Player-centred and rotating: "up" on the radar is always the direction the player faces.
/// - Rings + cross-hair drawn with GL.LINES, background disc with GL.TRIANGLES.
/// - Rotating sweep beam drawn as a GL.TRIANGLES fan with fading alpha.
/// - Police cars are red/blue flashing blips (GL.QUADS); police outside range stick to the edge.
///
/// Drawing happens in OnGUI's Repaint event with GL.LoadPixelMatrix, which works in every
/// render pipeline and on every graphics API (Direct3D, OpenGLCore, Vulkan, Metal).
/// RaceHUD adds this component automatically.
/// </summary>
public class GLRadar : MonoBehaviour
{
    [Tooltip("Radar radius as a fraction of screen height.")]
    [Range(0.05f, 0.3f)][SerializeField] private float radiusOfScreen = 0.12f;
    [SerializeField] private float margin = 20f;
    [Tooltip("World distance (metres) covered by the radar's outer ring.")]
    [SerializeField] private float range = 150f;
    [Tooltip("Sweep beam speed in degrees per second.")]
    [SerializeField] private float sweepSpeed = 120f;
    [SerializeField] private int circleSegments = 64;

    private static Material lineMaterial;
    private readonly List<SimpleCarController> cars = new List<SimpleCarController>();
    private SimpleCarController player;
    private float nextScan;

    // Unity's built-in shader for vertex-coloured, unlit, alpha-blended GL drawing.
    private static void CreateLineMaterial()
    {
        if (lineMaterial != null) return;
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    private void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 1f;
        cars.Clear();
        player = null;
        foreach (SimpleCarController c in FindObjectsByType<SimpleCarController>(FindObjectsSortMode.None))
        {
            if (c.GetComponent<PoliceChaseAI>() != null) cars.Add(c);
            else if (c.PlayerControlled) player = c;
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!GameState.IsPlaying || player == null) return;

        CreateLineMaterial();

        float radius = Screen.height * radiusOfScreen;
        // GL pixel matrix: origin bottom-left, y up.
        Vector2 centre = new Vector2(margin + radius, margin + radius);

        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();

        // 1) Background disc (triangle fan as GL.TRIANGLES)
        GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < circleSegments; i++)
        {
            GL.Color(new Color(0f, 0.08f, 0.03f, 0.75f));
            GL.Vertex3(centre.x, centre.y, 0);
            GL.Vertex(Point(centre, radius, i * 360f / circleSegments));
            GL.Vertex(Point(centre, radius, (i + 1) * 360f / circleSegments));
        }
        GL.End();

        // 2) Sweep beam: fan whose alpha fades behind the leading edge
        float sweep = Time.time * sweepSpeed;
        const float beamWidth = 50f;
        const int beamSteps = 20;
        GL.Begin(GL.TRIANGLES);
        for (int i = 0; i < beamSteps; i++)
        {
            float a0 = sweep - beamWidth * i / beamSteps;
            float a1 = sweep - beamWidth * (i + 1) / beamSteps;
            float alpha = 0.45f * (1f - (float)i / beamSteps);
            GL.Color(new Color(0.2f, 1f, 0.4f, alpha));
            GL.Vertex3(centre.x, centre.y, 0);
            GL.Vertex(Point(centre, radius, a0));
            GL.Color(new Color(0.2f, 1f, 0.4f, alpha * 0.8f));
            GL.Vertex(Point(centre, radius, a1));
        }
        GL.End();

        // 3) Range rings + cross-hair
        GL.Begin(GL.LINES);
        Color ring = new Color(0.3f, 1f, 0.5f, 0.6f);
        for (int r = 1; r <= 3; r++) Circle(centre, radius * r / 3f, ring);
        GL.Color(new Color(0.3f, 1f, 0.5f, 0.3f));
        GL.Vertex3(centre.x - radius, centre.y, 0); GL.Vertex3(centre.x + radius, centre.y, 0);
        GL.Vertex3(centre.x, centre.y - radius, 0); GL.Vertex3(centre.x, centre.y + radius, 0);
        GL.End();

        // 4) Police blips, in the player's local frame (forward = up)
        Transform pt = player.transform;
        bool flash = Mathf.Repeat(Time.time * 4f, 2f) < 1f;
        GL.Begin(GL.QUADS);
        foreach (SimpleCarController cop in cars)
        {
            if (cop == null) continue;
            Vector3 local = pt.InverseTransformPoint(cop.transform.position);
            Vector2 offset = new Vector2(local.x, local.z) / range * radius;
            bool outside = offset.magnitude > radius;
            if (outside) offset = offset.normalized * radius;

            Color c = flash ? Color.red : new Color(0.2f, 0.5f, 1f);
            if (outside) c.a = 0.6f;
            float s = outside ? 3f : 5f;
            Quad(centre + offset, s + 1.5f, Color.black);
            Quad(centre + offset, s, c);
        }
        GL.End();

        // 5) Player arrow at the centre (always points up)
        GL.Begin(GL.TRIANGLES);
        GL.Color(Color.cyan);
        GL.Vertex3(centre.x, centre.y + 9, 0);
        GL.Vertex3(centre.x - 6, centre.y - 6, 0);
        GL.Vertex3(centre.x + 6, centre.y - 6, 0);
        GL.End();

        GL.PopMatrix();
    }

    // Angle 0 = up, clockwise (like a compass / radar).
    private static Vector3 Point(Vector2 c, float r, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(c.x + Mathf.Sin(rad) * r, c.y + Mathf.Cos(rad) * r, 0);
    }

    private void Circle(Vector2 c, float r, Color col)
    {
        GL.Color(col);
        for (int i = 0; i < circleSegments; i++)
        {
            GL.Vertex(Point(c, r, i * 360f / circleSegments));
            GL.Vertex(Point(c, r, (i + 1) * 360f / circleSegments));
        }
    }

    private static void Quad(Vector2 p, float half, Color col)
    {
        GL.Color(col);
        GL.Vertex3(p.x - half, p.y - half, 0);
        GL.Vertex3(p.x - half, p.y + half, 0);
        GL.Vertex3(p.x + half, p.y + half, 0);
        GL.Vertex3(p.x + half, p.y - half, 0);
    }
}
