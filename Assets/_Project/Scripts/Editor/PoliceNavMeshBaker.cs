using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu: Midnight Pursuit > Bake Police NavMesh.
/// Builds a NavMesh over the open scene's colliders so police cars can path along streets
/// instead of driving into buildings. Re-run after changing the map.
/// </summary>
public static class PoliceNavMeshBaker
{
    [MenuItem("Midnight Pursuit/Bake Police NavMesh")]
    public static void Bake()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Bake Police NavMesh", "Save the scene first.", "OK");
            return;
        }

        GameObject go = GameObject.Find("PoliceNavMesh");
        if (go == null)
        {
            go = new GameObject("PoliceNavMesh");
            Undo.RegisterCreatedObjectUndo(go, "Bake Police NavMesh");
        }
        NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(go);

        // The default agent is a 0.5 m-wide human: paths hug walls and bollards and a 2.5 m car gets stuck.
        // Make the agent car-sized so paths keep to the middle of the streets.
        SetCarAgentSettings();
        surface.agentTypeID = 0;

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // the city's MeshColliders

        // Cars must not punch holes in the road NavMesh: hide them during the bake.
        GameObject[] cars = { GameObject.Find("PlayerCar"), GameObject.Find("PoliceCar") };
        foreach (GameObject c in cars) if (c != null) c.SetActive(false);

        try
        {
            EditorUtility.DisplayProgressBar("Bake Police NavMesh", "Building NavMesh over the city...", 0.5f);
            surface.BuildNavMesh();
        }
        finally
        {
            foreach (GameObject c in cars) if (c != null) c.SetActive(true);
            EditorUtility.ClearProgressBar();
        }

        if (surface.navMeshData == null)
        {
            Debug.LogError("Midnight Pursuit: NavMesh bake produced nothing. Did Setup City Chase add colliders?");
            return;
        }

        // Save the baked data as an asset next to the scene so it survives reloads and Git.
        string assetPath = scene.path.Replace(".unity", "_NavMesh.asset");
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        Debug.Log($"Midnight Pursuit: police NavMesh baked ({tri.indices.Length / 3} triangles) → {assetPath}. Press Ctrl+S.");
    }

    /// <summary>Sets the default NavMesh agent (ID 0) to car dimensions in Project Settings.</summary>
    private static void SetCarAgentSettings()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (assets == null || assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        SerializedProperty agent = so.FindProperty("m_Settings.Array.data[0]");
        if (agent == null) return;

        agent.FindPropertyRelative("agentRadius").floatValue = 1.6f; // half a car width + margin
        agent.FindPropertyRelative("agentHeight").floatValue = 2f;
        agent.FindPropertyRelative("agentClimb").floatValue = 0.3f;  // no driving up kerbs/steps
        agent.FindPropertyRelative("agentSlope").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
