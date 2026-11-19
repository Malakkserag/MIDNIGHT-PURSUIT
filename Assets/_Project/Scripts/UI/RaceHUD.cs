using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Prototype HUD: speedometer, escape timer, BUSTED screen. Press R to restart.
/// Uses OnGUI so it needs no Canvas setup — replace with real UI later.
/// </summary>
public class RaceHUD : MonoBehaviour
{
    [SerializeField] private SimpleCarController playerCar;

    private float escapeTime;
    private GUIStyle speedStyle;
    private GUIStyle labelStyle;
    private GUIStyle bigStyle;

    public SimpleCarController PlayerCar
    {
        get => playerCar;
        set => playerCar = value;
    }

    private void Awake()
    {
        if (GetComponent<Minimap>() == null) gameObject.AddComponent<Minimap>();
    }

    private void Update()
    {
        if (!PoliceChaseAI.IsBusted) escapeTime += Time.deltaTime;

        if (RestartPressed())
        {
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Works even if the scene isn't in Build Settings yet.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }
    }

    private static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private void OnGUI()
    {
        if (speedStyle == null)
        {
            speedStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.UpperLeft };
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 80, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            speedStyle.normal.textColor = Color.white;
            labelStyle.normal.textColor = Color.white;
            bigStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
        }

        float kmh = playerCar != null ? Mathf.Abs(playerCar.ForwardSpeed) * 3.6f : 0f;
        GUI.Label(new Rect(Screen.width - 320, Screen.height - 90, 300, 70), $"{kmh:0} km/h", speedStyle);
        GUI.Label(new Rect(20, 20, 500, 40), $"Escape time: {escapeTime:0.0}s", labelStyle);
        GUI.Label(new Rect(20, 50, 500, 40), "WASD drive · Space drift · R restart", labelStyle);

        if (PoliceChaseAI.IsBusted)
        {
            GUI.Label(new Rect(0, Screen.height / 2f - 80, Screen.width, 100), "BUSTED!", bigStyle);
            GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f + 20, 300, 40),
                $"You lasted {escapeTime:0.0}s — press R", labelStyle);
        }
    }
}
