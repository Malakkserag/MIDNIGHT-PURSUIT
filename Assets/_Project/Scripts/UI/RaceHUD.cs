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
    private GUIStyle buttonStyle;
    private Texture2D cameraIcon;

    /// <summary>Draws a simple white camera symbol (body, viewfinder bump, lens ring) into a texture.</summary>
    private static Texture2D BuildCameraIcon()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        Vector2 lens = new Vector2(32f, 28f);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            // y = 0 is the bottom row of the texture.
            bool body = x >= 6 && x <= 57 && y >= 10 && y <= 46 && !CutCorner(x, y, 6, 57, 10, 46, 5);
            bool bump = x >= 20 && x <= 40 && y > 46 && y <= 53;
            float d = Vector2.Distance(new Vector2(x, y), lens);
            bool lensHole = d < 13f && d > 7.5f;   // ring cut out of the body
            bool lensDot = d <= 4.5f;              // glass highlight
            bool flash = x >= 47 && x <= 52 && y >= 38 && y <= 42;

            bool on = ((body || bump) && !lensHole && !flash) || lensDot;
            px[y * s + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static bool CutCorner(int x, int y, int x0, int x1, int y0, int y1, int r)
    {
        int cx = x < x0 + r ? x0 + r : (x > x1 - r ? x1 - r : x);
        int cy = y < y0 + r ? y0 + r : (y > y1 - r ? y1 - r : y);
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r;
    }
    private ChaseCamera chaseCam;

    private ChaseCamera ChaseCam
    {
        get
        {
            if (chaseCam == null && Camera.main != null) chaseCam = Camera.main.GetComponent<ChaseCamera>();
            return chaseCam;
        }
    }

    public SimpleCarController PlayerCar
    {
        get => playerCar;
        set => playerCar = value;
    }

    private MainMenu menu;

    private void Awake()
    {
        GameState.OnSceneStart();
        if (GetComponent<Minimap>() == null) gameObject.AddComponent<Minimap>();
        if (GetComponent<GLRadar>() == null) gameObject.AddComponent<GLRadar>();
        menu = GetComponent<MainMenu>();
        if (menu == null) menu = gameObject.AddComponent<MainMenu>();
    }

    private void Update()
    {
        if (GameState.IsPlaying) escapeTime += Time.deltaTime;
        if (GameState.Current == GameState.State.Busted) menu.OnBusted(escapeTime);

        if (GameState.IsPlaying && CameraPressed() && ChaseCam != null) ChaseCam.NextView();

        bool inRun = GameState.IsPlaying || GameState.Current == GameState.State.Busted;
        if (inRun && RestartPressed()) GameState.Retry();
    }

    private static bool CameraPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.C);
#endif
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

        // The start menu has the screen to itself.
        if (GameState.Current == GameState.State.Menu) return;

        float kmh = playerCar != null ? Mathf.Abs(playerCar.ForwardSpeed) * 3.6f : 0f;
        GUI.Label(new Rect(Screen.width - 320, Screen.height - 90, 300, 70), $"{kmh:0} km/h", speedStyle);
        GUI.Label(new Rect(20, 20, 500, 40), $"Escape time: {escapeTime:0.0}s", labelStyle);

        // Bust meter: fills while the police is touching you.
        float bust = 0f;
        foreach (PoliceChaseAI p in FindObjectsByType<PoliceChaseAI>(FindObjectsSortMode.None))
            bust = Mathf.Max(bust, p.BustProgress);
        if (bust > 0.01f && !PoliceChaseAI.IsBusted)
        {
            Rect bar = new Rect(20, 58, 260, 14);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(1f, 0.6f, 0f), Color.red, bust);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * bust, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 8, bar.y - 6, 120, 26), "BUSTING!", labelStyle);
        }

        // Camera view button, bottom-right above the speedometer.
        if (ChaseCam != null)
        {
            if (cameraIcon == null) cameraIcon = BuildCameraIcon();
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
                buttonStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            }

            // Clickable camera symbol (no button box), with the current view name underneath.
            const float iconSize = 56f;
            Rect icon = new Rect(Screen.width - iconSize - 28, Screen.height - 165, iconSize, iconSize);
            bool hover = icon.Contains(Event.current.mousePosition);
            GUI.color = hover ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(icon, cameraIcon);
            GUI.color = Color.white;
            if (GUI.Button(icon, GUIContent.none, GUIStyle.none)) ChaseCam.NextView();
            GUI.Label(new Rect(icon.x - 30, icon.yMax + 2, iconSize + 60, 22), ChaseCam.CurrentViewName, buttonStyle);
        }
        // BUSTED / pause screens are drawn by MainMenu.
    }
}
