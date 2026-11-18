using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds and drives layered atmospheric horror VFX in the Menu scene:
/// cold creeping floor steam, rising hospital bed vapor, dark crimson demon mist,
/// and slowly drifting blood-red embers.
/// </summary>
[ExecuteAlways]
public class MenuAtmosphereVFX : MonoBehaviour
{
    [SerializeField] private Material _steamMaterial = null;
    [SerializeField] private Material _emberMaterial = null;

    private readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
    private Material _runtimeSteamMat;
    private Material _runtimeEmberMat;

    private void OnEnable()
    {
        RebuildSystems();
    }

    private void OnDisable()
    {
        CleanupSystems();
    }

    private void RebuildSystems()
    {
        CleanupSystems();
        // Частицы в меню отключены для спокойной, чистой картинки без визуального шума
    }

    private void CleanupSystems()
    {
        for (int i = _systems.Count - 1; i >= 0; i--)
        {
            if (_systems[i] == null) continue;
            DestroyDynamicObject(_systems[i].gameObject);
        }
        _systems.Clear();

        if (_runtimeSteamMat != null)
        {
            DestroyDynamicObject(_runtimeSteamMat);
            _runtimeSteamMat = null;
        }

        if (_runtimeEmberMat != null)
        {
            DestroyDynamicObject(_runtimeEmberMat);
            _runtimeEmberMat = null;
        }
    }

    private static void DestroyDynamicObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private static Material GetOrCreateFallbackMaterial(ref Material runtimeSlot, Material serializedMat, bool additive)
    {
        if (serializedMat != null)
            return serializedMat;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        runtimeSlot = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        if (additive)
        {
            runtimeSlot.SetFloat("_Surface", 1f);
            runtimeSlot.SetFloat("_Blend", 2f);
            runtimeSlot.SetColor("_BaseColor", new Color(2.4f, 0.32f, 0.22f, 1f));
        }
        else
        {
            runtimeSlot.SetFloat("_Surface", 1f);
            runtimeSlot.SetFloat("_Blend", 0f);
            runtimeSlot.SetColor("_BaseColor", new Color(0.78f, 0.65f, 0.68f, 0.55f));
        }
        return runtimeSlot;
    }

    private ParticleSystem SpawnEmitter(string emitterName, Vector3 worldPos, Material mat, int maxParticles, float maxParticleScreenSize)
    {
        var go = new GameObject(emitterName)
        {
            hideFlags = HideFlags.DontSave
        };
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.prewarm = true;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = mat;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = maxParticleScreenSize;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _systems.Add(ps);
        return ps;
    }

    private void CreateFloorMist(Material steamMat)
    {
        ParticleSystem ps = SpawnEmitter("VFX_FloorMistSteam", new Vector3(-21.2f, -17.0f, 24.5f), steamMat, 60, 2.5f);

        var main = ps.main;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6.5f, 10.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(3.6f, 6.0f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.36f, 0.38f, 0.75f),
            new Color(0.62f, 0.52f, 0.54f, 0.9f)
        );
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 5.5f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(15f, 0.35f, 11f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.05f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.12f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(0.55f, 1.05f),
            new Keyframe(1f, 1.32f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.52f, 0.44f, 0.46f), 0f),
                new GradientColorKey(new Color(0.56f, 0.46f, 0.48f), 0.5f),
                new GradientColorKey(new Color(0.42f, 0.32f, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.16f, 0.24f),
                new GradientAlphaKey(0.19f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    private void CreateHospitalBedSteam(Material steamMat)
    {
        ParticleSystem ps = SpawnEmitter("VFX_HospitalBedSteam", new Vector3(-22.5f, -17.05f, 22.8f), steamMat, 48, 2.0f);

        var main = ps.main;
        main.duration = 7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 6.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.84f, 0.90f, 0.95f),
            new Color(0.84f, 0.91f, 0.95f, 1f)
        );
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 7f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.2f, 0.2f, 1.3f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.14f);
        vel.y = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.42f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.28f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.5f, 1.0f),
            new Keyframe(1f, 1.55f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.72f, 0.84f, 0.90f), 0f),
                new GradientColorKey(new Color(0.76f, 0.86f, 0.92f), 0.5f),
                new GradientColorKey(new Color(0.55f, 0.62f, 0.68f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.24f, 0.2f),
                new GradientAlphaKey(0.18f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    private void CreateDemonDarkSteam(Material steamMat)
    {
        ParticleSystem ps = SpawnEmitter("VFX_DemonDarkSteam", new Vector3(-18.5f, -16.8f, 26.2f), steamMat, 52, 2.2f);

        var main = ps.main;
        main.duration = 7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.6f, 6.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.58f, 0.14f, 0.16f, 0.9f),
            new Color(0.78f, 0.24f, 0.22f, 1f)
        );
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.25f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.14f, 0.08f);
        vel.y = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.06f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.65f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.36f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.5f, 1.05f),
            new Keyframe(1f, 1.48f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.74f, 0.20f, 0.18f), 0f),
                new GradientColorKey(new Color(0.54f, 0.12f, 0.14f), 0.55f),
                new GradientColorKey(new Color(0.20f, 0.06f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.28f, 0.22f),
                new GradientAlphaKey(0.22f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    private void CreateCrimsonEmbers(Material emberMat)
    {
        ParticleSystem ps = SpawnEmitter("VFX_CrimsonEmbers", new Vector3(-20.8f, -15.6f, 24.5f), emberMat, 75, 0.15f);

        var main = ps.main;
        main.duration = 9f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.085f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.30f, 0.18f, 1f),
            new Color(1f, 0.65f, 0.35f, 1f)
        );
        main.gravityModifier = -0.015f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 8.5f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 4.5f, 9f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.10f, 0.08f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.75f, 0.9f),
            new Keyframe(1f, 0.1f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.42f, 0.24f), 0f),
                new GradientColorKey(new Color(0.95f, 0.22f, 0.15f), 0.65f),
                new GradientColorKey(new Color(0.55f, 0.08f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.2f),
                new GradientAlphaKey(0.85f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }
}
