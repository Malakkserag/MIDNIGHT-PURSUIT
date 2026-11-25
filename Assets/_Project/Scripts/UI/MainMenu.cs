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
        else if (GameState.Current == GameState.State.Paused)
        {
            if (showPauseHowTo || showOptions) { showPauseHowTo = false; showOptions = false; } // Esc backs out first
            else GameState.Resume();
        }
        else if (GameState.Current == GameState.State.Menu) { showHowTo = false; showOptions = false; }
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
        if (showOptions)
        {
            Rect panel = new Rect(cx - 320, y + 150, 640, Mathf.Min(560, Screen.height - y - 170));
            Box(panel);
            DrawOptions(panel, 1f, button);
            return;
        }

        float by = y + 170;
        if (Btn(new Rect(cx - 140, by, 280, 60), "PLAY", button)) GameState.StartGame();
        if (Btn(new Rect(cx - 140, by + 75, 280, 60), "HOW TO PLAY", button)) showHowTo = true;
        if (Btn(new Rect(cx - 140, by + 150, 280, 60), "OPTIONS", button)) showOptions = true;
        if (Btn(new Rect(cx - 140, by + 225, 280, 60), "QUIT", button)) GameState.Quit();

        float best = GameState.BestEscapeTime;
        GUI.Label(new Rect(0, by + 305, Screen.width, 30),
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
        if (Btn(new Rect(cx - 100, panel.yMax - 65, 200, 50), "BACK", button)) showHowTo = false;
    }

    // ---------- Options (sound volumes + controls) ----------
    private bool showOptions;
    private GUIStyle optLabel, optHeader, optValue;

    /// <summary>Button that also plays the click sound.</summary>
    private static bool Btn(Rect r, string text, GUIStyle style)
    {
        if (!GUI.Button(r, text, style)) return false;
        AudioManager.PlayUI(ProcAudio.Click, 0.8f);
        return true;
    }

    /// <summary>Volume sliders, mute toggle and the controls list, drawn inside 'area'.</summary>
    private void DrawOptions(Rect area, float s, GUIStyle backStyle)
    {
        if (optLabel == null)
        {
            optLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            optLabel.normal.textColor = Color.white;
            optHeader = new GUIStyle(optLabel) { fontStyle = FontStyle.BoldAndItalic };
            optHeader.normal.textColor = new Color(0.25f, 0.85f, 1f);
            optValue = new GUIStyle(optLabel) { alignment = TextAnchor.MiddleRight };
        }
        optLabel.fontSize = optValue.fontSize = Mathf.RoundToInt(19 * s);
        optHeader.fontSize = Mathf.RoundToInt(22 * s);

        float x = area.x + 30 * s, w = area.width - 60 * s, y = area.y + 16 * s, row = 46 * s;

        // ----- Tabs: SOUND | CONTROLS -----
        float tabW = w / 2f, tabH = 44 * s;
        for (int t = 0; t < 2; t++)
        {
            Rect tab = new Rect(x + t * tabW, y, tabW, tabH);
            bool selected = optionsTab == t;
            bool hover = tab.Contains(Event.current.mousePosition);
            Fill(tab, selected ? new Color(0.25f, 0.85f, 1f, 0.18f) : new Color(1f, 1f, 1f, hover ? 0.08f : 0.03f));
            Fill(new Rect(tab.x, tab.yMax - 3 * s, tab.width, 3 * s),
                 selected ? new Color(0.25f, 0.85f, 1f) : new Color(1f, 1f, 1f, 0.15f));
            Color keep = optHeader.normal.textColor;
            optHeader.normal.textColor = selected ? new Color(0.25f, 0.85f, 1f) : new Color(1f, 1f, 1f, 0.6f);
            optHeader.alignment = TextAnchor.MiddleCenter;
            GUI.Label(tab, t == 0 ? "SOUND" : "CONTROLS", optHeader);
            optHeader.alignment = TextAnchor.MiddleLeft;
            optHeader.normal.textColor = keep;
            if (!selected && GUI.Button(tab, GUIContent.none, GUIStyle.none))
            {
                optionsTab = t;
                AudioManager.PlayUI(ProcAudio.Click, 0.8f);
            }
        }
        y += tabH + 18 * s;

        if (optionsTab == 0)
        {
            GameSettings.Master = VolumeRow(ref y, x, w, row, s, "Master volume", GameSettings.Master);
            GameSettings.Engine = VolumeRow(ref y, x, w, row, s, "Engine", GameSettings.Engine);
            GameSettings.Effects = VolumeRow(ref y, x, w, row, s, "Crashes & pickups", GameSettings.Effects);
            GameSettings.Siren = VolumeRow(ref y, x, w, row, s, "Police siren", GameSettings.Siren);

            // Mute checkbox
            Rect boxR = new Rect(x, y + row * 0.25f, row * 0.5f, row * 0.5f);
            Fill(boxR, new Color(1f, 1f, 1f, 0.15f));
            if (GameSettings.Muted)
                Fill(new Rect(boxR.x + 4 * s, boxR.y + 4 * s, boxR.width - 8 * s, boxR.height - 8 * s), new Color(0.25f, 0.85f, 1f));
            GUI.Label(new Rect(boxR.xMax + 12 * s, y, w, row), "Mute all sound", optLabel);
            if (GUI.Button(new Rect(x, y, w * 0.5f, row), GUIContent.none, GUIStyle.none))
            {
                GameSettings.Muted = !GameSettings.Muted;
                AudioManager.PlayUI(ProcAudio.Click, 0.8f);
            }
        }
        else
        {
            string[,] keys =
            {
                { "W  /  ↑", "Accelerate" }, { "S  /  ↓", "Brake / reverse" }, { "A D  /  ← →", "Steer" },
                { "SPACE", "Handbrake drift" }, { "C", "Change camera" }, { "R", "Restart" }, { "ESC", "Pause / back" },
            };
            float keyW = w * 0.38f, small = row * 0.85f;
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                float cy = y + i * small;
                if (i % 2 == 0) Fill(new Rect(x, cy, w, small), new Color(1f, 1f, 1f, 0.04f));
                GUI.Label(new Rect(x + 12 * s, cy, keyW, small), "<b>" + keys[i, 0] + "</b>", RichLabel);
                GUI.Label(new Rect(x + keyW, cy, w - keyW, small), keys[i, 1], optLabel);
            }
        }

        float bw = Mathf.Min(220 * s, area.width * 0.6f), bh = 54 * s;
        if (Btn(new Rect(area.center.x - bw / 2, area.yMax - bh - 16 * s, bw, bh), "BACK", backStyle)) showOptions = false;
    }

    private GUIStyle richLabel;
    private GUIStyle RichLabel
    {
        get
        {
            if (richLabel == null) richLabel = new GUIStyle(optLabel) { richText = true };
            richLabel.fontSize = optLabel.fontSize;
            return richLabel;
        }
    }

    private int optionsTab; // 0 = Sound, 1 = Controls

    private float VolumeRow(ref float y, float x, float w, float row, float s, string label, float value)
    {
        GUI.Label(new Rect(x, y, w * 0.4f, row), label, optLabel);
        float v = VolumeSlider(new Rect(x + w * 0.42f, y, w * 0.44f, row), value, s);
        GUI.Label(new Rect(x + w * 0.86f, y, w * 0.14f, row), $"{Mathf.RoundToInt(v * 100)}%", optValue);
        y += row;
        return v;
    }

    /// <summary>
    /// Custom volume bar: dark track, cyan fill up to the value, round-ish white knob.
    /// Click or drag anywhere on the row to set it; plays a tick on release so you hear the new level.
    /// </summary>
    private static float VolumeSlider(Rect row, float value, float s)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        float trackH = 8f * s;
        Rect track = new Rect(row.x, row.center.y - trackH / 2f, row.width, trackH);
        Event e = Event.current;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (row.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    value = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    AudioManager.PlayUI(ProcAudio.Click, 0.8f);
                    e.Use();
                }
                break;
            case EventType.Repaint:
                bool active = GUIUtility.hotControl == id || row.Contains(e.mousePosition);
                Fill(track, new Color(1f, 1f, 1f, 0.12f));
                Fill(new Rect(track.x, track.y, track.width * value, track.height),
                     Color.Lerp(new Color(0.1f, 0.55f, 0.9f), new Color(0.25f, 0.9f, 1f), value));
                float k = (active ? 22f : 18f) * s;
                Rect knob = new Rect(track.x + track.width * value - k / 2f, row.center.y - k / 2f, k, k);
                Fill(new Rect(knob.x - 2 * s, knob.y - 2 * s, knob.width + 4 * s, knob.height + 4 * s), new Color(0f, 0f, 0f, 0.5f));
                Fill(knob, Color.white);
                break;
        }
        return value;
    }

    private static void Fill(Rect r, Color c)
    {
        Color keep = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = keep;
    }

    // ---------- Pause menu: dark side panel on the right, game still visible on the left ----------
    private bool showPauseHowTo;
    private GUIStyle pauseTitle, pauseSub, primaryBtn, secondaryBtn, howToBody;
    private static readonly Color Accent = new Color(0.25f, 0.85f, 1f);       // neon cyan
    private static readonly Color PrimaryFill = new Color(1f, 0.82f, 0.05f);  // yellow

    private void DrawPauseMenu()
    {
        BuildPauseStyles();
        float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f); // UI scale with window height

        // Light dim over the game, darker panel on the right with a cyan edge line.
        Dim(0.25f);
        float panelW = Mathf.Max(Screen.width * 0.30f, 360f * s);
        Rect panel = new Rect(Screen.width - panelW, 0, panelW, Screen.height);
        GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Accent;
        GUI.DrawTexture(new Rect(panel.x, 0, 3f * s, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = panel.center.x;
        pauseTitle.fontSize = Mathf.RoundToInt(64 * s);
        pauseSub.fontSize = Mathf.RoundToInt(34 * s);
        primaryBtn.fontSize = secondaryBtn.fontSize = Mathf.RoundToInt(28 * s);
        howToBody.fontSize = Mathf.RoundToInt(19 * s);

        float y = Screen.height * 0.10f;
        GUI.Label(new Rect(panel.x, y, panelW, 70 * s), "PAUSE", pauseTitle);
        GUI.Label(new Rect(panel.x, y + 62 * s, panelW, 45 * s),
            showPauseHowTo ? "HOW TO PLAY" : (showOptions ? "OPTIONS" : "MENU"), pauseSub);

        float bw = Mathf.Min(panelW * 0.72f, 320f * s), bh = 72f * s;
        float bx = cx - bw / 2f;

        if (showOptions)
        {
            DrawOptions(new Rect(panel.x + 10 * s, y + 120 * s, panelW - 20 * s, Screen.height - y - 140 * s), s, primaryBtn);
            return;
        }

        if (showPauseHowTo)
        {
            Rect text = new Rect(panel.x + 30 * s, y + 140 * s, panelW - 60 * s, Screen.height - y - 300 * s);
            GUI.Label(text,
                "<b>W / S</b>   accelerate / brake & reverse\n" +
                "<b>A / D</b>   steer\n" +
                "<b>SPACE</b>   handbrake drift\n" +
                "<b>C</b>   change camera\n" +
                "<b>R</b>   restart\n" +
                "<b>ESC</b>   pause / back\n\n" +
                "The police are hunting you. Every time they ram you the <b>BUSTING</b> bar fills up. " +
                "When it's full, it's game over.\n\nUse the radar (top-left) to see where they're coming from. " +
                "Grab pickups: <b>orange</b> = nitro, <b>green</b> = repair (empties BUSTING), <b>blue</b> = +5 s. Dodge the traffic, " +
                "and escape for as long as you can!",
                howToBody);
            if (Btn(new Rect(bx, Screen.height - 150 * s, bw, bh), "BACK", primaryBtn)) showPauseHowTo = false;
            return;
        }

        float by = Screen.height * 0.34f, gap = 18f * s;
        if (Btn(new Rect(bx, by, bw, bh), "RESUME", primaryBtn)) GameState.Resume();
        if (Btn(new Rect(bx, by + (bh + gap), bw, bh), "RESTART", secondaryBtn)) GameState.Retry();
        if (Btn(new Rect(bx, by + 2 * (bh + gap), bw, bh), "HOW TO PLAY", secondaryBtn)) showPauseHowTo = true;
        if (Btn(new Rect(bx, by + 3 * (bh + gap), bw, bh), "OPTIONS", secondaryBtn)) showOptions = true;
        // Extra space before the "leave" button, like a separate group.
        if (Btn(new Rect(bx, by + 4 * (bh + gap) + 36 * s, bw, bh), "QUIT RACE", secondaryBtn))
        {
            showPauseHowTo = false;
            showOptions = false;
            GameState.GoToMenu();
        }
    }

    private void BuildPauseStyles()
    {
        if (pauseTitle != null) return;
        pauseTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
        pauseTitle.normal.textColor = Accent;
        pauseSub = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
        pauseSub.normal.textColor = Color.white;
        howToBody = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
        howToBody.normal.textColor = Color.white;

        primaryBtn = MakeButton(PrimaryFill, new Color(1f, 0.92f, 0.35f), Color.black);
        secondaryBtn = MakeButton(new Color(0.9f, 0.9f, 0.92f), Accent, new Color(0.08f, 0.08f, 0.1f));
    }

    private static GUIStyle MakeButton(Color fill, Color hoverFill, Color text)
    {
        var st = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
        st.normal.background = Solid(fill);
        st.hover.background = Solid(hoverFill);
        st.active.background = Solid(hoverFill * 0.85f);
        st.focused.background = st.normal.background;
        st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = text;
        return st;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        c.a = 1f;
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
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

        if (Btn(new Rect(cx - 140, y + 200, 280, 60), "TRY AGAIN", button)) GameState.Retry();
        if (Btn(new Rect(cx - 140, y + 275, 280, 60), "MAIN MENU", button)) GameState.GoToMenu();
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
