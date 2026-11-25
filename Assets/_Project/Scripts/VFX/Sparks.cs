using UnityEngine;

/// <summary>
/// Crash sparks: a short burst of bright, stretched particles flying out of the contact point,
/// plus a quick orange light flash. Built entirely in code (no prefab needed).
/// </summary>
public static class Sparks
{
    private static Material material;

    public static void Spawn(Vector3 point, Vector3 normal, float strength)
    {
        var go = new GameObject("Sparks");
        go.transform.position = point;
        go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.1f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Lerp(15, 60, strength)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.1f;

        // Fade from white-hot to dark orange.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch; // streaks, not dots
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 1f;
        renderer.material = Material;

        ps.Play();

        // Flash
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.2f);
        light.range = 6f;
        light.intensity = Mathf.Lerp(2f, 6f, strength);
        go.AddComponent<SparkLightFade>();

        Object.Destroy(go, 1.2f);
    }

    private static Material Material
    {
        get
        {
            if (material != null) return material;
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(s) { name = "SparkMat" };
            return material;
        }
    }

    /// <summary>Fades the flash light out over 0.15 s.</summary>
    private class SparkLightFade : MonoBehaviour
    {
        private Light l;
        private float start;
        private void Start() { l = GetComponent<Light>(); start = l.intensity; }
        private void Update()
        {
            l.intensity = Mathf.MoveTowards(l.intensity, 0f, start / 0.15f * Time.deltaTime);
        }
    }
}
