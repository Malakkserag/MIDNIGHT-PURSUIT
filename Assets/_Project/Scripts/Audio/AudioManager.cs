using UnityEngine;

/// <summary>
/// Sets up game audio at runtime: makes sure the camera has an AudioListener, gives every car a
/// CarAudio (engine / siren / crash + sparks), applies the volume settings and pauses sounds
/// while the game is paused. Also plays UI sounds (menu clicks, pickups).
/// RaceHUD adds this component automatically.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioSource ui;
    private float nextScan;

    private void Awake()
    {
        ui = gameObject.AddComponent<AudioSource>();
        ui.playOnAwake = false;
        ui.spatialBlend = 0f;
        ui.ignoreListenerPause = true; // menu clicks still work while paused
    }

    private void Update()
    {
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();

        AudioListener.volume = GameSettings.ListenerVolume;
        AudioListener.pause = GameState.Current == GameState.State.Paused;

        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        foreach (SimpleCarController car in FindObjectsByType<SimpleCarController>(FindObjectsSortMode.None))
            if (car.GetComponent<CarAudio>() == null) car.gameObject.AddComponent<CarAudio>();
    }

    /// <summary>Plays a 2D sound (scaled by the Effects volume).</summary>
    public static void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (ui != null && clip != null) ui.PlayOneShot(clip, volume * GameSettings.Effects);
    }
}
