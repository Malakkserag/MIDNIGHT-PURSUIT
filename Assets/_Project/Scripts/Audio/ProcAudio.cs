using UnityEngine;

/// <summary>
/// All game sounds are generated in code (no audio files needed): each clip is built once from
/// simple waveforms (sawtooth, sine, noise) and cached.
/// </summary>
public static class ProcAudio
{
    private const int Rate = 44100;
    private static AudioClip engine, siren, crash, pickup, click, nitro;

    /// <summary>
    /// Looping engine: a smooth, clean tone (no buzzing pulses) — a fundamental with a few soft
    /// harmonics, a very gentle slow "breathing" so it sounds alive, and a whisper of filtered air.
    /// Pitch it up with speed. All frequencies are whole cycles per second → seamless 1 s loop.
    /// </summary>
    public static AudioClip Engine => engine != null ? engine : engine = MakeFiltered("Engine", 1f, t =>
    {
        const float f = 60f;
        float w = t * f * Mathf.PI * 2f;
        float tone = Mathf.Sin(w) * 0.55f          // body
                   + Mathf.Sin(2f * w) * 0.28f     // warmth
                   + Mathf.Sin(3f * w) * 0.12f     // a touch of growl
                   + Mathf.Sin(0.5f * w) * 0.25f;  // low sub (30 Hz)
        float breathe = 0.9f + 0.1f * Mathf.Sin(t * 4f * Mathf.PI * 2f); // slow, subtle (4 Hz)
        float air = (Random.value * 2f - 1f) * 0.04f;
        return (tone * breathe + air) * 0.45f;
    }, 0.5f);

    /// <summary>
    /// Police "WAIL" (used when the police is far): a slow climb to a high pitch, then a long
    /// slide back down — 4 s per cycle — through a harsh horn-speaker tone.
    /// </summary>
    public static AudioClip Siren => siren != null ? siren : siren = MakeSiren("SirenWail", 4f, t =>
    {
        float u = t / 4f;
        float k = u < 0.4f ? Mathf.SmoothStep(0f, 1f, u / 0.4f)        // fast rise (1.6 s)
                           : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.6f); // slow fall (2.4 s)
        return Mathf.Lerp(520f, 1450f, k);
    });

    /// <summary>
    /// Police "YELP" (used when the police is close): the same tone sweeping up and down very fast,
    /// the urgent sound real police cars switch to during a chase.
    /// </summary>
    public static AudioClip SirenYelp => sirenYelp != null ? sirenYelp : sirenYelp = MakeSiren("SirenYelp", 1.2f, t =>
    {
        float u = (t / 0.3f) % 1f; // 4 sweeps per loop
        float k = u < 0.7f ? u / 0.7f : 1f - (u - 0.7f) / 0.3f;
        return Mathf.Lerp(650f, 1550f, k);
    });

    private static AudioClip sirenYelp;

    /// <summary>
    /// Siren voice: odd harmonics (square-ish, like a horn speaker), a little clipping for grit,
    /// and a quiet second voice detuned by a few Hz for a thicker "two speakers" sound.
    /// </summary>
    private static AudioClip MakeSiren(string name, float seconds, System.Func<float, float> freq)
    {
        double p1 = 0, p2 = 0;
        return Make(name, seconds, (t, i) =>
        {
            float f = freq(t);
            p1 += f / Rate;
            p2 += (f * 1.006f) / Rate;
            float a = (float)(p1 * Mathf.PI * 2.0), b = (float)(p2 * Mathf.PI * 2.0);
            float voice = Mathf.Sin(a) + Mathf.Sin(3f * a) / 3f + Mathf.Sin(5f * a) / 5f + Mathf.Sin(7f * a) / 9f;
            float second = Mathf.Sin(b) + Mathf.Sin(3f * b) / 3f;
            float s = voice * 0.75f + second * 0.3f;
            s = Mathf.Clamp(s * 1.4f, -1f, 1f);          // horn speaker grit
            // Fade the first/last 10 ms so the loop point never clicks.
            float edge = Mathf.Clamp01(Mathf.Min(t, seconds - t) / 0.01f);
            return s * 0.32f * Mathf.Lerp(0.6f, 1f, edge);
        });
    }

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
