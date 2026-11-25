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
    private AudioSource engine, siren, yelp, fx;
    private PoliceChaseAI police;
    private float yelpMix;
    private bool isPlayer, isPolice;
    private float nextCrash;

    private void Start()
    {
        car = GetComponent<SimpleCarController>();
        isPlayer = car.PlayerControlled && GetComponent<PoliceChaseAI>() == null;
        isPolice = GetComponent<PoliceChaseAI>() != null;

        engine = NewSource(ProcAudio.Engine, true);
        if (!isPlayer) engine.maxDistance = isPolice ? 60f : 35f; // only hear other engines up close
        engine.Play();
        if (isPolice)
        {
            siren = NewSource(ProcAudio.Siren, true);
            siren.minDistance = 12f;
            siren.maxDistance = 150f;
            siren.Play();
            yelp = NewSource(ProcAudio.SirenYelp, true);
            yelp.minDistance = 12f;
            yelp.maxDistance = 150f;
            yelp.volume = 0f;
            yelp.Play();
            police = GetComponent<PoliceChaseAI>();
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
        s.dopplerLevel = 0f; // doppler at 200+ km/h made engines warble/vibrate
        return s;
    }

    private void Update()
    {
        if (engine == null) return;
        // Speed → engine pitch, with a fake gearbox: the pitch climbs through each "gear" then drops a bit.
        float speed = Mathf.Abs(car.ForwardSpeed);
        float speed01 = Mathf.Clamp01(speed / 55f);
        const float gearSize = 14f; // m/s per gear (~50 km/h)
        float inGear = (speed % gearSize) / gearSize;
        int gear = Mathf.Min((int)(speed / gearSize), 4);
        float pitch = 0.7f + gear * 0.18f + inGear * 0.55f;
        engine.pitch = Mathf.Lerp(engine.pitch, pitch * (car.Boosting ? 1.12f : 1f), Time.deltaTime * 8f);
        // Other cars are quiet so a dozen engines don't stack into a droning buzz.
        float baseVol = isPlayer ? 0.5f : (isPolice ? 0.25f : 0.12f);
        engine.volume = GameSettings.Engine * baseVol * Mathf.Lerp(0.5f, 1f, speed01);

        if (siren != null)
        {
            bool active = GameState.Current != GameState.State.Menu;
            // Far away: slow wail. Closing in (< 45 m): fast yelp. Crossfade between them.
            float dist = police != null && police.Target != null
                ? Vector3.Distance(transform.position, police.Target.position) : 999f;
            float target = dist < 45f ? 1f : 0f;
            yelpMix = Mathf.MoveTowards(yelpMix, target, Time.deltaTime * 1.5f);
            float vol = active ? GameSettings.Siren * 0.7f : 0f;
            siren.volume = vol * (1f - yelpMix);
            yelp.volume = vol * yelpMix;
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
