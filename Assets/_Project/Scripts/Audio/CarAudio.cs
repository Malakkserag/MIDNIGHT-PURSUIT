using UnityEngine;

/// <summary>
/// Per-car sound and crash effects, added automatically by AudioManager:
/// - engine hum whose pitch follows the speed,
/// - police siren loop (police cars only),
/// - crash sound + sparks when the car hits something hard.
/// </summary>
[RequireComponent(typeof(SimpleCarController))]
public class CarAudio : MonoBehaviour
{
    [Tooltip("Impact speed (m/s, along the hit direction) needed for a crash sound + sparks.")]
    [SerializeField] private float crashThreshold = 3.5f;

    private SimpleCarController car;
    private AudioSource engine, siren, fx;
    private bool isPlayer, isPolice;
    private float nextCrash;

    private void Start()
    {
        car = GetComponent<SimpleCarController>();
        isPlayer = car.PlayerControlled && GetComponent<PoliceChaseAI>() == null;
        isPolice = GetComponent<PoliceChaseAI>() != null;

        engine = NewSource(ProcAudio.Engine, true);
        engine.Play();
        if (isPolice)
        {
            siren = NewSource(ProcAudio.Siren, true);
            siren.minDistance = 12f;
            siren.maxDistance = 150f;
            siren.Play();
        }
        fx = NewSource(null, false);
    }

    private AudioSource NewSource(AudioClip clip, bool loop)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = isPlayer ? 0f : 1f; // your own car is "in your ears", others come from their position
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 5f;
        s.maxDistance = 80f;
        s.dopplerLevel = 0.3f;
        return s;
    }

    private void Update()
    {
        if (engine == null) return;
        float speed01 = Mathf.Clamp01(Mathf.Abs(car.ForwardSpeed) / 35f);
        engine.pitch = Mathf.Lerp(0.55f, 2.1f, speed01) * (car.Boosting ? 1.15f : 1f);
        float baseVol = isPlayer ? 0.55f : (isPolice ? 0.5f : 0.35f);
        engine.volume = GameSettings.Engine * baseVol * Mathf.Lerp(0.5f, 1f, speed01);

        if (siren != null)
        {
            bool active = GameState.Current != GameState.State.Menu;
            siren.volume = active ? GameSettings.Siren * 0.7f : 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < nextCrash || collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        if (contact.normal.y > 0.7f) return; // landing on the road isn't a crash

        float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        if (impact < crashThreshold) return;
        nextCrash = Time.time + 0.2f;

        float strength = Mathf.InverseLerp(crashThreshold, 20f, impact);
        // Car-vs-car: only one of the two cars makes the sound/sparks, so it isn't doubled.
        var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<CarAudio>() : null;
        if (other != null && other.GetInstanceID() < GetInstanceID()) return;

        if (fx != null)
        {
            fx.pitch = Random.Range(0.85f, 1.15f);
            fx.PlayOneShot(ProcAudio.Crash, GameSettings.Effects * Mathf.Lerp(0.4f, 1f, strength));
        }
        Sparks.Spawn(contact.point, contact.normal, strength);
    }
}
