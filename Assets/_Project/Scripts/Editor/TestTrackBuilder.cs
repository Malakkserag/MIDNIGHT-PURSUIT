using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Midnight Pursuit > Build Test Track.
/// Builds a night-time prototype arena in the open scene: ground, walls, ramps, buildings,
/// player car, police car, chase camera and HUD. Running it again rebuilds from scratch.
/// </summary>
public static class TestTrackBuilder
{
    private const string RootName = "TestTrack";
    private const string MaterialFolder = "Assets/_Project/Materials";

    [MenuItem("Midnight Pursuit/Build Test Track")]
    public static void Build()
    {
        // Rescue the camera first in case it was parented to the old PlayerCar.
        if (Camera.main != null && Camera.main.transform.parent != null)
            Undo.SetTransformParent(Camera.main.transform, null, "Build Test Track");

        // Remove a previous build and the old hand-made placeholders.
        foreach (string old in new[] { RootName, "PlayerCar", "PoliceCar", "Plane", "HUD" })
        {
            GameObject existing = GameObject.Find(old);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        Material asphalt = GetMaterial("Asphalt", new Color(0.12f, 0.12f, 0.14f));
        Material wall = GetMaterial("Wall", new Color(0.35f, 0.35f, 0.4f));
        Material ramp = GetMaterial("Ramp", new Color(0.9f, 0.6f, 0.1f));
        Material building = GetMaterial("Building", new Color(0.2f, 0.22f, 0.3f));
        Material playerPaint = GetMaterial("PlayerPaint", new Color(0.1f, 0.5f, 1f));
        Material policePaint = GetMaterial("PolicePaint", new Color(0.95f, 0.95f, 0.95f));
        Material glass = GetMaterial("Glass", new Color(0.05f, 0.05f, 0.08f));

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Test Track");

        // Ground 200 x 200
        Primitive(PrimitiveType.Plane, "Ground", root.transform, Vector3.zero, new Vector3(20f, 1f, 20f), asphalt);

        // Boundary walls
        const float half = 100f;
        Primitive(PrimitiveType.Cube, "Wall_N", root.transform, new Vector3(0, 2, half), new Vector3(200, 4, 2), wall);
        Primitive(PrimitiveType.Cube, "Wall_S", root.transform, new Vector3(0, 2, -half), new Vector3(200, 4, 2), wall);
        Primitive(PrimitiveType.Cube, "Wall_E", root.transform, new Vector3(half, 2, 0), new Vector3(2, 4, 200), wall);
        Primitive(PrimitiveType.Cube, "Wall_W", root.transform, new Vector3(-half, 2, 0), new Vector3(2, 4, 200), wall);

        // City blocks with streetlights, leaving streets between them
        Transform city = new GameObject("City").transform;
        city.SetParent(root.transform);
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                if (x == 0 && z == 0) continue; // open plaza in the middle
                if ((x == 0 && z == -1)) continue; // keep the start street clear
                float h = 8f + Mathf.Abs((x * 7 + z * 13) % 5) * 5f;
                Vector3 pos = new Vector3(x * 36f, h / 2f, z * 36f);
                Primitive(PrimitiveType.Cube, $"Building_{x}_{z}", city, pos, new Vector3(20, h, 20), building);
                StreetLight(city, new Vector3(x * 36f + 12f, 0, z * 36f + 12f));
            }
        }

        // Ramps in the plaza and on the streets
        Transform ramps = new GameObject("Ramps").transform;
        ramps.SetParent(root.transform);
        RampAt(ramps, new Vector3(0, 0.8f, 8), 0f, ramp);
        RampAt(ramps, new Vector3(18, 0.8f, 0), 90f, ramp);
        RampAt(ramps, new Vector3(-18, 0.8f, 54), 180f, ramp);
        RampAt(ramps, new Vector3(54, 0.8f, -18), -90f, ramp);

        GameObject player = SpawnChase(new Vector3(0, 0.6f, -40), 0f, out Camera cam);

        // Night lighting
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "Night");
            Undo.RecordObject(l.transform, "Night");
            l.intensity = 0.15f;
            l.color = new Color(0.55f, 0.65f, 1f);
            l.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.12f);
        RenderSettings.skybox = null;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("Midnight Pursuit: test track built. Press Ctrl+S to save, then Play.");
    }

    /// <summary>
    /// Creates PlayerCar, PoliceCar (16 m behind), chase camera and HUD.
    /// Removes any existing PlayerCar / PoliceCar / HUD first. Returns the player.
    /// </summary>
    public static GameObject SpawnChase(Vector3 playerPos, float yaw, out Camera cam)
    {
        if (Camera.main != null && Camera.main.transform.parent != null)
            Undo.SetTransformParent(Camera.main.transform, null, "Spawn Chase");
        foreach (string old in new[] { "PlayerCar", "PoliceCar", "HUD" })
        {
            GameObject existing = GameObject.Find(old);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        Material playerPaint = GetMaterial("PlayerPaint", new Color(0.1f, 0.5f, 1f));
        Material policePaint = GetMaterial("PolicePaint", new Color(0.95f, 0.95f, 0.95f));
        Material glass = GetMaterial("Glass", new Color(0.05f, 0.05f, 0.08f));
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        // Player car
        GameObject player = BuildCar("PlayerCar", playerPos, playerPaint, glass);
        player.transform.rotation = rot;
        player.AddComponent<SimpleCarController>();
        Headlights(player.transform);

        // Police car, starting behind the player
        GameObject police = BuildCar("PoliceCar", playerPos - rot * Vector3.forward * 16f, policePaint, glass);
        police.transform.rotation = rot;
        police.AddComponent<SimpleCarController>();
        PoliceChaseAI ai = police.AddComponent<PoliceChaseAI>();
        ai.Target = player.transform;
        Light red = SirenLight(police.transform, "Siren_Red", new Vector3(-0.4f, 1.6f, 0), Color.red);
        Light blue = SirenLight(police.transform, "Siren_Blue", new Vector3(0.4f, 1.6f, 0), new Color(0.2f, 0.4f, 1f));
        ai.SetSirenLights(red, blue);
        Headlights(police.transform);

        // Camera
        cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            Undo.RegisterCreatedObjectUndo(camGo, "Spawn Chase");
        }
        cam.transform.position = player.transform.position + rot * new Vector3(0, 3, -8);
        ChaseCamera follow = cam.GetComponent<ChaseCamera>();
        if (follow == null) follow = Undo.AddComponent<ChaseCamera>(cam.gameObject);
        follow.Target = player.transform;
        cam.farClipPlane = 400f;

        // HUD
        GameObject hud = new GameObject("HUD");
        Undo.RegisterCreatedObjectUndo(hud, "Build Test Track");
        hud.AddComponent<RaceHUD>().PlayerCar = player.GetComponent<SimpleCarController>();

        return player;
    }

    private static GameObject BuildCar(string name, Vector3 pos, Material paint, Material glass)
    {
        GameObject car = GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.name = name;
        car.transform.position = pos;
        car.transform.localScale = new Vector3(2f, 1f, 4f);
        car.GetComponent<Renderer>().sharedMaterial = paint;
        Undo.RegisterCreatedObjectUndo(car, "Build Test Track");

        // Cabin (visual only). Child of a scaled parent, so use local values relative to 2x1x4.
        GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        Object.DestroyImmediate(cabin.GetComponent<Collider>());
        cabin.transform.SetParent(car.transform, false);
        cabin.transform.localPosition = new Vector3(0f, 0.8f, -0.05f);
        cabin.transform.localScale = new Vector3(0.85f, 0.7f, 0.5f);
        cabin.GetComponent<Renderer>().sharedMaterial = glass;

        Rigidbody rb = car.AddComponent<Rigidbody>();
        rb.mass = 1200f;
        return car;
    }

    private static void Headlights(Transform car)
    {
        foreach (float x in new[] { -0.35f, 0.35f })
        {
            GameObject go = new GameObject("Headlight");
            go.transform.SetParent(car, false);
            go.transform.localPosition = new Vector3(x, 0.1f, 0.52f);
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 35f;
            l.spotAngle = 55f;
            l.intensity = 25f;
            l.color = new Color(1f, 0.95f, 0.8f);
        }
    }

    private static Light SirenLight(Transform car, string name, Vector3 worldOffset, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(car, false);
        go.transform.localPosition = new Vector3(worldOffset.x / 2f, worldOffset.y, worldOffset.z / 4f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 15f;
        l.intensity = 20f;
        l.color = color;
        return l;
    }

    private static void StreetLight(Transform parent, Vector3 basePos)
    {
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "StreetLight";
        pole.transform.SetParent(parent, false);
        pole.transform.position = basePos + Vector3.up * 3f;
        pole.transform.localScale = new Vector3(0.25f, 3f, 0.25f);

        GameObject lamp = new GameObject("Lamp");
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = basePos + Vector3.up * 6.2f;
        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 18f;
        l.intensity = 15f;
        l.color = new Color(1f, 0.7f, 0.35f);
    }

    private static void RampAt(Transform parent, Vector3 pos, float yaw, Material mat)
    {
        GameObject r = Primitive(PrimitiveType.Cube, "Ramp", parent, pos, new Vector3(6f, 0.5f, 8f), mat);
        r.transform.rotation = Quaternion.Euler(-12f, yaw, 0f);
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static Material GetMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/Proto_{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
