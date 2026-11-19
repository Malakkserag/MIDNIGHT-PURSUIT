using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Start menu, pause menu and busted screen, drawn over the live city.
/// While in the menu the camera slowly flies around the city (see ChaseCamera).
/// RaceHUD adds this component automatically — no scene setup needed.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private bool showHowTo;
    private bool newRecord;
    private float lastEscapeTime;
    private bool resultSaved;

    private GUIStyle title, subtitle, button, body, small;

    /// <summary>Called by RaceHUD when the player gets busted.</summary>
    public void OnBusted(float escapeTime)
    {
        if (resultSaved) return;
        resultSaved = true;
        lastEscapeTime = escapeTime;
        newRecord = GameState.SubmitEscapeTime(escapeTime);
    }

    private void Update()
    {
        if (!EscapePressed()) return;
        if (GameState.Current == GameState.State.Playing) GameState.Pause();
        else if (GameState.Current == GameState.State.Paused) GameState.Resume();
        else if (GameState.Current == GameState.State.Menu && showHowTo) showHowTo = false;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void OnGUI()
    {
        GUI.depth = -10; // draw on top of the HUD
        BuildStyles();
        // Scale the big title with the window so "MIDNIGHT PURSUIT" always fits on one line.
        title.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.width / 11f), 28, 72);
        switch (GameState.Current)
        {
            case GameState.State.Menu: DrawStartMenu(); break;
            case GameState.State.Paused: DrawPauseMenu(); break;
            case GameState.State.Busted: DrawBusted(); break;
        }
    }

    private void DrawStartMenu()
    {
        Dim(0.45f);
        float cx = Screen.width / 2f;
        float y = Screen.height * 0.18f;

        GUI.Label(new Rect(0, y, Screen.width, 100), "MIDNIGHT PURSUIT", title);
        GUI.Label(new Rect(0, y + 95, Screen.width, 40), "Outrun the police. Survive the night.", subtitle);

        if (showHowTo)
        {
            DrawHowTo(cx, y + 160);
            return;
        }

        float by = y + 170;
        if (GUI.Button(new Rect(cx - 140, by, 280, 60), "PLAY", button)) GameState.StartGame();
        if (GUI.Button(new Rect(cx - 140, by + 75, 280, 60), "HOW TO PLAY", button)) showHowTo = true;
        if (GUI.Button(new Rect(cx - 140, by + 150, 280, 60), "QUIT", button)) GameState.Quit();

        float best = GameState.BestEscapeTime;
        GUI.Label(new Rect(0, by + 230, Screen.width, 30),
            best > 0f ? $"Best escape: {best:0.0}s" : "No record yet — set one!", subtitle);
    }

    private void DrawHowTo(float cx, float y)
    {
        Rect panel = new Rect(cx - 300, y, 600, 380);
        Box(panel);
        GUI.Label(new Rect(panel.x + 30, panel.y + 20, panel.width - 60, panel.height - 90),
            "W / S  —  accelerate / brake & reverse\n" +
            "A / D  —  steer\n" +
            "SPACE  —  handbrake drift\n" +
            "C  —  change camera (or click the camera icon)\n" +
            "R  —  restart\n" +
            "ESC  —  pause\n\n" +
            "The police are hunting you. Every time they ram you the BUSTING bar fills up. " +
            "When it's full, it's game over. Escape for as long as you can!",
            body);
        if (GUI.Button(new Rect(cx - 100, panel.yMax - 65, 200, 50), "BACK", button)) showHowTo = false;
    }

    private void DrawPauseMenu()
    {
        Dim(0.55f);
        float cx = Screen.width / 2f;
        float y = Screen.height * 0.3f;
        GUI.Label(new Rect(0, y, Screen.width, 80), "PAUSED", title);
        if (GUI.Button(new Rect(cx - 140, y + 110, 280, 60), "RESUME", button)) GameState.Resume();
        if (GUI.Button(new Rect(cx - 140, y + 185, 280, 60), "RESTART", button)) GameState.Retry();
        if (GUI.Button(new Rect(cx - 140, y + 260, 280, 60), "MAIN MENU", button)) GameState.GoToMenu();
    }

    private void DrawBusted()
    {
        Dim(0.35f);
        float cx = Screen.width / 2f;
        float y = Screen.height * 0.22f;

        Color old = title.normal.textColor;
        title.normal.textColor = new Color(1f, 0.2f, 0.2f);
        GUI.Label(new Rect(0, y, Screen.width, 100), "BUSTED!", title);
        title.normal.textColor = old;

        GUI.Label(new Rect(0, y + 100, Screen.width, 40), $"You escaped for {lastEscapeTime:0.0}s", subtitle);
        GUI.Label(new Rect(0, y + 135, Screen.width, 40),
            newRecord ? "NEW RECORD!" : $"Best: {GameState.BestEscapeTime:0.0}s", subtitle);

        if (GUI.Button(new Rect(cx - 140, y + 200, 280, 60), "TRY AGAIN", button)) GameState.Retry();
        if (GUI.Button(new Rect(cx - 140, y + 275, 280, 60), "MAIN MENU", button)) GameState.GoToMenu();
    }

    private static void Dim(float alpha)
    {
        GUI.color = new Color(0f, 0f, 0.02f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static void Box(Rect r)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void BuildStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
        title.normal.textColor = Color.white;
        subtitle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        subtitle.normal.textColor = new Color(0.75f, 0.85f, 1f);
        body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        body.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 14 };

        button = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
        button.normal.textColor = Color.white;
        button.hover.textColor = new Color(0.4f, 0.8f, 1f);
    }
}
