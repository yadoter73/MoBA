using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Создаёт эффект капающей воды из душевой лейки (PFB_Shower) с брызгами на полу.
/// </summary>
[ExecuteAlways]
public class ShowerDripEffect : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Showerhead Bounds (Local Space of PFB_Shower)")]
    [SerializeField] private Vector3 _nozzleCenter = new Vector3(-0.2526f, 2.222f, -0.4277f);
    [SerializeField] private Vector2 _nozzleSizeXZ = new Vector2(0.32f, 0.36f);
    [SerializeField] private float _floorLocalY = 0.015f;
    [SerializeField] private float _dripRate = 7.5f;
    [SerializeField] private Color _waterColor = new Color(0.72f, 0.85f, 0.90f, 0.72f);

    private GameObject _dripRoot;
    private Texture2D _dropTex;
    private Texture2D _rippleTex;
    private Material _dropMat;
    private Material _rippleMat;

    private void OnEnable()
    {
        BuildDripSystems();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void BuildDripSystems()
    {
        Cleanup();

        _dripRoot = new GameObject("ShowerDripVFX")
        {
            hideFlags = HideFlags.DontSave
        };
        _dripRoot.transform.SetParent(transform, false);

        _dropTex = CreateDropletTexture(64);
        _rippleTex = CreateRippleTexture(64);

        _dropMat = CreateWaterParticleMaterial(_dropTex, _waterColor);
        Color rippleColor = new Color(_waterColor.r, _waterColor.g, _waterColor.b, _waterColor.a * 0.45f);
        _rippleMat = CreateWaterParticleMaterial(_rippleTex, rippleColor);

        float fallDistance = Mathf.Max(0.5f, _nozzleCenter.y - _floorLocalY);
        float gravityScale = 0.85f;
        float gravityAccel = 9.81f * gravityScale;
        float fallTime = Mathf.Sqrt(2f * fallDistance / gravityAccel);

        // 1. Падающие капли воды из перфорированной лейки
        var dropsGo = new GameObject("Drops") { hideFlags = HideFlags.DontSave };
        dropsGo.transform.SetParent(_dripRoot.transform, false);
        dropsGo.transform.localPosition = _nozzleCenter;

        var dropsPs = dropsGo.AddComponent<ParticleSystem>();
        var main = dropsPs.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(fallTime * 0.94f, fallTime * 1.03f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
        main.gravityModifier = gravityScale;
        main.maxParticles = 64;
        main.startColor = Color.white;

        var emission = dropsPs.emission;
        emission.enabled = true;
        emission.rateOverTime = _dripRate;

        var shape = dropsPs.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Направляем локальную ось эмиссии вниз (-Y)
        dropsGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shape.scale = new Vector3(_nozzleSizeXZ.x, _nozzleSizeXZ.y, 0.005f);

        var col = dropsPs.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.85f, 0.92f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var dropsRenderer = dropsGo.GetComponent<ParticleSystemRenderer>();
        dropsRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        dropsRenderer.lengthScale = 2.8f;
        dropsRenderer.velocityScale = 0.018f;
        dropsRenderer.sharedMaterial = _dropMat;
        dropsRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // 2. Брызги/круги на полу душевой под струйками
        var splashGo = new GameObject("FloorSplashes") { hideFlags = HideFlags.DontSave };
        splashGo.transform.SetParent(_dripRoot.transform, false);
        splashGo.transform.localPosition = new Vector3(_nozzleCenter.x, _floorLocalY, _nozzleCenter.z);
        splashGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var splashPs = splashGo.AddComponent<ParticleSystem>();
        var sMain = splashPs.main;
        sMain.loop = true;
        sMain.playOnAwake = true;
        sMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
        sMain.startSpeed = 0f;
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        sMain.maxParticles = 48;
        sMain.startColor = Color.white;

        var sEmission = splashPs.emission;
        sEmission.enabled = true;
        sEmission.rateOverTime = _dripRate * 0.9f;

        var sShape = splashPs.shape;
        sShape.enabled = true;
        sShape.shapeType = ParticleSystemShapeType.Box;
        sShape.scale = new Vector3(_nozzleSizeXZ.x * 1.05f, _nozzleSizeXZ.y * 1.05f, 0.002f);

        var sSize = splashPs.sizeOverLifetime;
        sSize.enabled = true;
        sSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f)));

        var sCol = splashPs.colorOverLifetime;
        sCol.enabled = true;
        var sGrad = new Gradient();
        sGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.35f, 0.55f), new GradientAlphaKey(0f, 1f) }
        );
        sCol.color = sGrad;

        var splashRenderer = splashGo.GetComponent<ParticleSystemRenderer>();
        splashRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        splashRenderer.sharedMaterial = _rippleMat;
        splashRenderer.shadowCastingMode = ShadowCastingMode.Off;

        dropsPs.Play();
        splashPs.Play();
    }

    private void Cleanup()
    {
        if (_dripRoot != null)
        {
            DestroySafe(_dripRoot);
            _dripRoot = null;
        }
        if (_dropMat != null) { DestroySafe(_dropMat); _dropMat = null; }
        if (_rippleMat != null) { DestroySafe(_rippleMat); _rippleMat = null; }
        if (_dropTex != null) { DestroySafe(_dropTex); _dropTex = null; }
        if (_rippleTex != null) { DestroySafe(_rippleTex); _rippleTex = null; }
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private static Material CreateWaterParticleMaterial(Texture2D tex, Color tint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        mat.SetTexture(BaseMapId, tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor(BaseColorId, tint);
        mat.SetColor("_Color", tint);

        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        return mat;
    }

    private static Texture2D CreateDropletTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ShowerDropTex",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float ny = (y - half) / half;
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float r2 = nx * nx + ny * ny;
                float a = Mathf.Clamp01(1f - r2);
                a = a * a;
                float spec = Mathf.Clamp01(1f - ((nx + 0.25f) * (nx + 0.25f) + (ny - 0.25f) * (ny - 0.25f)) * 4f);
                byte lum = (byte)Mathf.Clamp(Mathf.RoundToInt((0.78f + 0.22f * spec) * 255f), 0, 255);
                byte ab = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                pixels[y * size + x] = new Color32(lum, lum, 255, ab);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateRippleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ShowerRippleTex",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float ny = (y - half) / half;
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.14f, 2f));
                float fadeOut = Mathf.Clamp01((1f - r) * 4f);
                float a = Mathf.Clamp01(ring * fadeOut);
                byte ab = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                pixels[y * size + x] = new Color32(235, 248, 255, ab);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
