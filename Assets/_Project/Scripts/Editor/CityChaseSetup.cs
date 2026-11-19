using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Midnight Pursuit > Setup City Chase.
/// 1. If the third-party demo city scene is open, saves a copy into our project and opens that
///    (the original pack stays untouched).
/// 2. Adds MeshColliders to every city mesh (the pack ships without colliders).
/// 3. Spawns player, police, camera and HUD on the road under the Scene view camera,
///    facing the way the Scene view looks, then swaps in the real car models.
/// </summary>
public static class CityChaseSetup
{
    private const string GameScenePath = "Assets/_Project/Scenes/City_Chase.unity";

    [MenuItem("Midnight Pursuit/Setup City Chase")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Work on our own copy, never the Asset Store scene.
        if (scene.path.StartsWith("Assets/ThirdParty"))
        {
            if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene })) return;
            if (!File.Exists(GameScenePath))
            {
                AssetDatabase.CopyAsset(scene.path, GameScenePath);
                Debug.Log($"Midnight Pursuit: copied city scene to {GameScenePath}");
            }
            scene = EditorSceneManager.OpenScene(GameScenePath);
        }

        int added = AddCityColliders();

        // Where to spawn: straight down from the Scene view camera's focus point.
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        float yaw = view != null ? view.rotation.eulerAngles.y : 0f;

        Physics.SyncTransforms();
        Vector3 spawn = pivot + Vector3.up * 0.6f;
        if (Physics.Raycast(pivot + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 500f))
            spawn = hit.point + Vector3.up * 0.6f;

        yaw = StreetDirection(spawn + Vector3.up * 0.6f, yaw);

        GameObject player = TestTrackBuilder.SpawnChase(spawn, yaw, out _);
        CarModelSwapper.SwapAll();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Midnight Pursuit: city chase ready ({added} colliders added). " +
                  "Press Ctrl+S, then Play. Run this menu again to move the start point.");
    }

    /// <summary>
    /// Finds which way the street runs: casts rays all around at car height and picks the
    /// direction with the longest clear line (a street is long and open, buildings are close).
    /// Of the two ends of the street, picks the one closer to where the Scene view is looking.
    /// </summary>
    private static float StreetDirection(Vector3 origin, float preferredYaw)
    {
        const float maxDist = 300f;
        float bestYaw = preferredYaw;
        float bestScore = -1f;

        for (int angle = 0; angle < 180; angle += 2)
        {
            // A street is open in BOTH directions, so score the line through the point.
            float score = ClearDistance(origin, angle, maxDist) + ClearDistance(origin, angle + 180, maxDist);
            if (score > bestScore)
            {
                bestScore = score;
                bestYaw = angle;
            }
        }

        // Face the end of the street that's closer to the Scene view direction.
        if (Mathf.Abs(Mathf.DeltaAngle(bestYaw, preferredYaw)) > 90f) bestYaw += 180f;
        return Mathf.Repeat(bestYaw, 360f);
    }

    private static float ClearDistance(Vector3 origin, float yaw, float maxDist)
    {
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float closest = maxDist; // RaycastAll results are unordered, so take the minimum
        foreach (RaycastHit h in Physics.RaycastAll(origin, dir, maxDist))
        {
            Transform root = h.collider.transform.root;
            if (root.name == "PlayerCar" || root.name == "PoliceCar") continue;
            closest = Mathf.Min(closest, h.distance);
        }
        return closest;
    }

    /// <summary>Adds a MeshCollider to every mesh that has none, skipping our cars.</summary>
    private static int AddCityColliders()
    {
        int count = 0;
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            Transform root = mf.transform.root;
            if (root.name == "PlayerCar" || root.name == "PoliceCar" || root.GetComponent<Camera>() != null) continue;

            Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
            count++;
        }
        return count;
    }
}
