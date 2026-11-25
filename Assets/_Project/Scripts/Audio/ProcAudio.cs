using UnityEngine;

/// <summary>
/// All game sounds are generated in code (no audio files needed): each clip is built once from
/// simple waveforms (sawtooth, sine, noise) and cached.
/// </summary>
public static class ProcAudio
{
    private const int Rate = 44100;
    private static AudioClip engine, siren, crash, pickup, click, nitro;

    /// <summary>Looping engine hum. Pitch it up with speed.</summary>
    public static AudioClip Engine => engine != null ? engine : engine = Make("Engine", 1f, (t, i) =>
    {
        // Every frequency here is a whole number of cycles per second → the 1 s clip loops seamlessly.
        float f = 56f;
        float saw = Saw(t * f) * 0.5f + Saw(t * f * 2f) * 0.25f + Mathf.Sin(t * f * 0.5f * Mathf.PI * 2f) * 0.35f;
        float rumble = Mathf.Sin(t * 27f * Mathf.PI * 2f) * 0.5f + 0.5f; // pulsing like cylinders
        return Soft(saw * (0.6f + 0.4f * rumble) * 0.45f);
    });

    /// <summary>Looping two-tone police wail (rises and falls once per second).</summary>
    public static AudioClip Siren => siren != null ? siren : siren = MakePhase("Siren", 2f, 0.28f, t =>
    {
        float sweep = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI); // 0→1→0 over 2 s
        return Mathf.Lerp(650f, 1350f, sweep);
    });

    /// <summary>Metal crunch: decaying, low-passed noise with a thump.</summary>
    public static AudioClip Crash => crash != null ? crash : crash = MakeFiltered("Crash", 0.7f, t =>
    {
        float env = Mathf.Exp(-t * 7f);
        float thump = Mathf.Sin(t * 70f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f);
        return (Random.value * 2f - 1f) * env * 0.8f + thump * 0.9f;
    }, 0.35f);

    /// <summary>Bright two-note chime for collecting a pickup.</summary>
    public static AudioClip Pickup => pickup != null ? pickup : pickup = Make("Pickup", 0.4f, (t, i) =>
    {
        float f = t < 0.12f ? 880f : 1320f;
        float env = t < 0.12f ? 1f : Mathf.Exp(-(t - 0.12f) * 9f);
        return Mathf.Sin(t * f * Mathf.PI * 2f) * env * 0.5f;
    });

    /// <summary>Whoosh for nitro: rising filtered noise.</summary>
    public static AudioClip Nitro => nitro != null ? nitro : nitro = MakeFiltered("Nitro", 0.9f, t =>
    {
        float env = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
        return (Random.value * 2f - 1f) * env * 0.7f;
    }, 0.6f);

    /// <summary>Short UI tick.</summary>
    public static AudioClip Click => click != null ? click : click = Make("Click", 0.06f, (t, i) =>
        Mathf.Sin(t * 1800f * Mathf.PI * 2f) * Mathf.Exp(-t * 80f) * 0.5f);

    // ---------- helpers ----------

    private static AudioClip Make(string name, float seconds, System.Func<float, int, float> sample)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = sample((float)i / Rate, i);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Sine with a changing frequency (phase accumulated so it doesn't click).</summary>
    private static AudioClip MakePhase(string name, float seconds, float amp, System.Func<float, float> freq)
    {
        double phase = 0;
        return Make(name, seconds, (t, i) =>
        {
            phase += freq(t) / Rate;
            float s = Mathf.Sin((float)(phase * Mathf.PI * 2.0));
            s += 0.3f * Mathf.Sin((float)(phase * Mathf.PI * 4.0)); // a bit of edge
            return s * amp;
        });
    }

    /// <summary>One-pole low-pass on top of a raw generator (smaller alpha = darker).</summary>
    private static AudioClip MakeFiltered(string name, float seconds, System.Func<float, float> raw, float alpha)
    {
        float y = 0f;
        return Make(name, seconds, (t, i) => y += alpha * (raw(t) - y));
    }

    private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
    private static float Soft(float x) => x / (1f + Mathf.Abs(x)); // gentle saturation
}
