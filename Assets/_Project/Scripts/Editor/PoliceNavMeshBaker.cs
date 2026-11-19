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

        // ROADS ONLY: build from just the asphalt parts of the city meshes (by material),
        // so sidewalks, plazas, parks and rooftops are not drivable for the police.
        NavMeshData data;
        int roadPieces;
        try
        {
            EditorUtility.DisplayProgressBar("Bake Police NavMesh", "Collecting road surfaces...", 0.3f);
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable && !Application.isEditor) continue;
                Material[] mats = mr.sharedMaterials;
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount && sub < mats.Length; sub++)
                {
                    if (!IsRoadMaterial(mats[sub])) continue;
                    Mesh road = ExtractSubmesh(mf.sharedMesh, sub);
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = road,
                        transform = mr.transform.localToWorldMatrix,
                        area = 0,
                    });
                    Bounds wb = mr.bounds;
                    if (first) { bounds = wb; first = false; } else bounds.Encapsulate(wb);
                }
            }
            roadPieces = sources.Count;
            if (roadPieces == 0)
            {
                Debug.LogError("Midnight Pursuit: no road (asphalt) meshes found in this scene.");
                return;
            }

            EditorUtility.DisplayProgressBar("Bake Police NavMesh", $"Building NavMesh on {roadPieces} road pieces...", 0.6f);
            bounds.Expand(10f);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (data == null)
        {
            Debug.LogError("Midnight Pursuit: NavMesh bake produced nothing.");
            return;
        }

        surface.RemoveData();
        surface.navMeshData = data;
        surface.AddData();

        // Save the baked data as an asset next to the scene so it survives reloads and Git.
        string assetPath = scene.path.Replace(".unity", "_NavMesh.asset");
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        Debug.Log($"Midnight Pursuit: ROADS-ONLY police NavMesh baked from {roadPieces} road pieces " +
                  $"({tri.indices.Length / 3} triangles) → {assetPath}. Press Ctrl+S.");
    }

    /// <summary>Asphalt materials of the Demo City pack (plaza "asphalt_square" excluded).</summary>
    private static bool IsRoadMaterial(Material m)
    {
        if (m == null) return false;
        string n = m.name.ToLowerInvariant();
        return n.StartsWith("asphalt") && !n.Contains("square");
    }

    private static Mesh ExtractSubmesh(Mesh src, int sub)
    {
        var m = new Mesh { indexFormat = src.indexFormat, vertices = src.vertices };
        m.SetTriangles(src.GetTriangles(sub), 0);
        m.RecalculateBounds();
        return m;
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
