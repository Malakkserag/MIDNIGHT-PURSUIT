using UnityEngine;

/// <summary>
/// Player options saved between sessions (PlayerPrefs). Shown in the OPTIONS menu.
/// </summary>
public static class GameSettings
{
    private const string Prefix = "MidnightPursuit.";

    public static float Master { get => Get("Master", 0.8f); set => Set("Master", value); }
    public static float Engine { get => Get("Engine", 0.7f); set => Set("Engine", value); }
    public static float Effects { get => Get("Effects", 0.8f); set => Set("Effects", value); }
    public static float Siren { get => Get("Siren", 0.6f); set => Set("Siren", value); }
    public static bool Muted
    {
        get => PlayerPrefs.GetInt(Prefix + "Muted", 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "Muted", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Final volume for the whole game (what the AudioListener uses).</summary>
    public static float ListenerVolume => Muted ? 0f : Master;

    private static float Get(string key, float def) => PlayerPrefs.GetFloat(Prefix + key, def);

    private static void Set(string key, float v)
    {
        if (Mathf.Approximately(Get(key, -1f), Mathf.Clamp01(v))) return; // sliders call this every frame
        PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(v));
        PlayerPrefs.Save();
    }
}
