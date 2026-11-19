using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global game flow: Menu → Playing ⇄ Paused → Busted.
/// Cars, police, camera and HUD read GameState.Current to decide what to do.
/// </summary>
public static class GameState
{
    public enum State { Menu, Playing, Paused, Busted }

    public static State Current { get; private set; } = State.Menu;

    /// <summary>Set before reloading the scene to jump straight into a new run (Retry / R).</summary>
    private static bool skipMenuOnNextLoad;

    private const string BestTimeKey = "MidnightPursuit.BestEscapeTime";

    public static bool IsPlaying => Current == State.Playing;

    /// <summary>Called once when the game scene loads (by RaceHUD).</summary>
    public static void OnSceneStart()
    {
        Current = skipMenuOnNextLoad ? State.Playing : State.Menu;
        skipMenuOnNextLoad = false;
        Time.timeScale = 1f;
    }

    public static void StartGame()
    {
        Current = State.Playing;
        Time.timeScale = 1f;
    }

    public static void Pause()
    {
        if (Current != State.Playing) return;
        Current = State.Paused;
        Time.timeScale = 0f;
    }

    public static void Resume()
    {
        if (Current != State.Paused) return;
        Current = State.Playing;
        Time.timeScale = 1f;
    }

    public static void SetBusted()
    {
        if (Current == State.Playing) Current = State.Busted;
    }

    public static void Retry()
    {
        skipMenuOnNextLoad = true;
        ReloadScene();
    }

    public static void GoToMenu()
    {
        skipMenuOnNextLoad = false;
        ReloadScene();
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static float BestEscapeTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

    /// <summary>Saves the time if it's a new record. Returns true when it is.</summary>
    public static bool SubmitEscapeTime(float seconds)
    {
        if (seconds <= BestEscapeTime) return false;
        PlayerPrefs.SetFloat(BestTimeKey, seconds);
        PlayerPrefs.Save();
        return true;
    }

    private static void ReloadScene()
    {
        Time.timeScale = 1f;
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
