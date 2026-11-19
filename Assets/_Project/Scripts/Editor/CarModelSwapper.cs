using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Midnight Pursuit > Swap Car Models.
/// Replaces the placeholder cubes on PlayerCar / PoliceCar with the imported Asset Store models,
/// fits the collider to the model and repositions headlights and sirens.
/// Gameplay scripts on the cars are kept as-is.
/// </summary>
public static class CarModelSwapper
{
    private const string PlayerModelPath =
        "Assets/ThirdParty/ICONIC - Sports Car FREE Vol.02/Prefabs/NoColliders/Samurai_Blue_A.prefab";
    private const string PoliceModelPath =
        "Assets/ThirdParty/Police Car & Helicopter/Prefabs/Interceptor.prefab";

    [MenuItem("Midnight Pursuit/Swap Car Models")]
    public static void SwapAll()
    {
        int swapped = 0;
        swapped += Swap("PlayerCar", PlayerModelPath) ? 1 : 0;
        swapped += Swap("PoliceCar", PoliceModelPath) ? 1 : 0;
        if (swapped > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Midnight Pursuit: swapped {swapped} car model(s). If a car faces backwards, " +
                      "select its 'Model' child and set Rotation Y to 180.");
        }
    }

    private static bool Swap(string carName, string prefabPath)
    {
        GameObject car = GameObject.Find(carName);
        if (car == null)
        {
            Debug.LogWarning($"Midnight Pursuit: '{carName}' not found. Run Build Test Track first.");
            return false;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Midnight Pursuit: model not found at {prefabPath}");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(car, "Swap Car Model");

        // Remove placeholder visuals and any previous model.
        foreach (string childName in new[] { "Cabin", "Model" })
        {
            Transform old = car.transform.Find(childName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        MeshRenderer cubeRenderer = car.GetComponent<MeshRenderer>();
        if (cubeRenderer != null) Undo.DestroyObjectImmediate(cubeRenderer);
        MeshFilter cubeFilter = car.GetComponent<MeshFilter>();
        if (cubeFilter != null) Undo.DestroyObjectImmediate(cubeFilter);

        // Un-scale the root (the cube was scaled 2x1x4) so the model keeps its real size.
        car.transform.localScale = Vector3.one;

        // Add the model as a child.
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(model, "Swap Car Model");

        // The model is visual only: physics uses the root's BoxCollider + Rigidbody.
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (Animator a in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
        foreach (Rigidbody r in model.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(r);

        // Fit collider to the model's bounds (in the car's local space).
        Bounds b = LocalBounds(car.transform, model);
        BoxCollider box = car.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(car);
        box.center = b.center;
        box.size = b.size;

        // Sit the car on whatever ground is below it (works on flat tracks and in the city).
        Vector3 p = car.transform.position;
        float groundY = 0f;
        float closest = float.MaxValue;
        Physics.SyncTransforms();
        // RaycastAll is unordered: take the HIGHEST surface below (the road), not something underneath it.
        foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 50f))
        {
            if (hit.collider.transform.IsChildOf(car.transform)) continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                groundY = hit.point.y;
            }
        }
        p.y = groundY - b.min.y + 0.05f;
        car.transform.position = p;

        // Move lights to the new body.
        int headlight = 0;
        foreach (Transform child in car.transform)
        {
            if (child.name == "Headlight")
            {
                float side = headlight++ % 2 == 0 ? -1f : 1f;
                child.localPosition = new Vector3(b.center.x + side * b.extents.x * 0.6f,
                                                  b.min.y + b.size.y * 0.35f,
                                                  b.max.z + 0.05f);
            }
            else if (child.name == "Siren_Red" || child.name == "Siren_Blue")
            {
                float side = child.name == "Siren_Red" ? -1f : 1f;
                child.localPosition = new Vector3(b.center.x + side * 0.35f, b.max.y + 0.2f, b.center.z);
            }
        }

        return true;
    }

    [MenuItem("Midnight Pursuit/Fit Car Colliders (keep positions)")]
    public static void FitCollidersOnly()
    {
        foreach (string carName in new[] { "PlayerCar", "PoliceCar" })
        {
            GameObject car = GameObject.Find(carName);
            Transform model = car != null ? car.transform.Find("Model") : null;
            BoxCollider box = car != null ? car.GetComponent<BoxCollider>() : null;
            if (model == null || box == null) continue;

            Undo.RecordObject(box, "Fit Car Collider");
            Bounds b = LocalBounds(car.transform, model.gameObject);
            box.center = b.center;
            box.size = b.size;
            Debug.Log($"Midnight Pursuit: {carName} collider = {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// Exact model bounds in the car's local space, computed from the meshes themselves
    /// (not Renderer.bounds, which is a world-axis box and balloons when the car is turned).
    /// </summary>
    private static Bounds LocalBounds(Transform root, GameObject model)
    {
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>();
        bool first = true;
        Bounds result = new Bounds(Vector3.zero, new Vector3(2f, 1f, 4f));
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = m.MultiplyPoint3x4(corner);
                if (first) { result = new Bounds(local, Vector3.zero); first = false; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }
}
