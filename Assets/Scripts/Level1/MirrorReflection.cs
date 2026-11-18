using System.Collections.Generic;
using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Создаёт плоское отражение на зеркале через вспомогательную камеру URP
/// с наклонной плоскостью отсечения (Oblique Near Clip) и исключением рендера самого игрока,
/// чтобы в зеркале отражалась комната без отражения игрока.
/// </summary>
[ExecuteAlways]
public class MirrorReflection : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int MirrorVPId = Shader.PropertyToID("_MirrorVP");

    [Header("Mirror Plane (Local Space)")]
    [SerializeField] private Vector3 _localBottomLeft = new Vector3(0.2540f, 0.1434f, 0.2425f);
    [SerializeField] private Vector3 _localTopLeft = new Vector3(0.2540f, 1.6542f, -0.1342f);
    [SerializeField] private Vector3 _localTopRight = new Vector3(-0.2586f, 1.6542f, -0.1342f);
    [SerializeField] private Vector3 _localBottomRight = new Vector3(-0.2586f, 0.1434f, 0.2425f);
    [SerializeField] private Vector3 _localNormal = new Vector3(0f, 0.2419f, 0.9703f);
    [SerializeField] private float _glassOffset = 0.0025f;

    [Header("Rendering")]
    [SerializeField] private Material _mirrorMaterial = null;
    [SerializeField] private int _rtWidth = 512;
    [SerializeField] private int _rtHeight = 512;
    [SerializeField] private float _maxRenderDistance = 28f;
    [SerializeField] private float _clipPlaneOffset = -0.02f;

    private RenderTexture _renderTexture;
    private Camera _mirrorCamera;
    private MeshRenderer _glassRenderer;
    private Mesh _glassMesh;
    private Material _runtimeMaterial;

    private ExampleCharacterController _cachedPlayer;
    private Renderer[] _playerRenderers;
    private bool _playerSearched;
    private readonly List<Renderer> _hiddenThisPass = new List<Renderer>();

    private Vector3 LocalCenter => (_localBottomLeft + _localTopRight) * 0.5f;

    private void OnEnable()
    {
        BuildMirrorObjects();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RestoreHiddenPlayerRenderers();
        CleanupMirrorObjects();
    }

    private void LateUpdate()
    {
        if (_mirrorCamera == null) return;
        Camera viewer = GetViewerCamera();
        if (viewer == null)
        {
            _mirrorCamera.enabled = false;
            return;
        }

        Vector3 centerWS = transform.TransformPoint(LocalCenter);
        Vector3 normalWS = transform.TransformDirection(_localNormal).normalized;
        Vector3 toViewer = viewer.transform.position - centerWS;

        bool visibleSide = Vector3.Dot(toViewer, normalWS) > 0.01f;
        bool withinRange = toViewer.sqrMagnitude <= _maxRenderDistance * _maxRenderDistance;
        _mirrorCamera.enabled = visibleSide && withinRange;

        if (_mirrorCamera.enabled)
            UpdateMirrorTransformAndMatrix(viewer, centerWS, normalWS);
    }

    private void BuildMirrorObjects()
    {
        CleanupMirrorObjects();

        int w = Mathf.Clamp(_rtWidth, 128, 1024);
        int h = Mathf.Clamp(_rtHeight, 128, 1024);
        _renderTexture = new RenderTexture(w, h, 16, RenderTextureFormat.DefaultHDR)
        {
            name = "MirrorReflectionRT",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _renderTexture.Create();

        Material sourceMat = _mirrorMaterial;
        if (sourceMat == null)
        {
            Shader fallback = Shader.Find("Custom/PlanarMirrorURP");
            if (fallback == null) fallback = Shader.Find("Universal Render Pipeline/Unlit");
            _runtimeMaterial = new Material(fallback) { hideFlags = HideFlags.DontSave };
        }
        else
        {
            _runtimeMaterial = new Material(sourceMat) { hideFlags = HideFlags.DontSave };
        }
        _runtimeMaterial.SetTexture(MainTexId, _renderTexture);

        Vector3 nrm = _localNormal.sqrMagnitude > 0.0001f ? _localNormal.normalized : Vector3.forward;
        Vector3 offset = nrm * _glassOffset;
        _glassMesh = new Mesh
        {
            name = "MirrorGlassQuad",
            hideFlags = HideFlags.DontSave,
            vertices = new[]
            {
                _localBottomLeft + offset,
                _localTopLeft + offset,
                _localTopRight + offset,
                _localBottomRight + offset
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        _glassMesh.RecalculateNormals();
        _glassMesh.RecalculateBounds();

        var glassGo = new GameObject("MirrorGlassSurface")
        {
            hideFlags = HideFlags.DontSave
        };
        glassGo.transform.SetParent(transform, false);
        glassGo.layer = gameObject.layer;

        var mf = glassGo.AddComponent<MeshFilter>();
        mf.sharedMesh = _glassMesh;

        _glassRenderer = glassGo.AddComponent<MeshRenderer>();
        _glassRenderer.sharedMaterial = _runtimeMaterial;
        _glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _glassRenderer.receiveShadows = false;

        var camGo = new GameObject("MirrorCamera")
        {
            hideFlags = HideFlags.DontSave
        };
        camGo.transform.SetParent(transform, false);

        _mirrorCamera = camGo.AddComponent<Camera>();
        _mirrorCamera.targetTexture = _renderTexture;
        _mirrorCamera.depth = -10f;
        _mirrorCamera.useOcclusionCulling = false;
        _mirrorCamera.clearFlags = CameraClearFlags.Skybox;
        _mirrorCamera.allowHDR = true;
        _mirrorCamera.nearClipPlane = 0.05f;
        _mirrorCamera.farClipPlane = 60f;

        int excludedMask = LayerMask.GetMask("Player", "UI", "Arrow");
        _mirrorCamera.cullingMask = ~excludedMask;

        var urpData = camGo.GetComponent<UniversalAdditionalCameraData>();
        if (urpData == null) urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderPostProcessing = false;
        urpData.renderShadows = true;
    }

    private void CleanupMirrorObjects()
    {
        if (_mirrorCamera != null)
        {
            DestroySafe(_mirrorCamera.gameObject);
            _mirrorCamera = null;
        }
        if (_glassRenderer != null)
        {
            DestroySafe(_glassRenderer.gameObject);
            _glassRenderer = null;
        }
        if (_glassMesh != null)
        {
            DestroySafe(_glassMesh);
            _glassMesh = null;
        }
        if (_runtimeMaterial != null)
        {
            DestroySafe(_runtimeMaterial);
            _runtimeMaterial = null;
        }
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            DestroySafe(_renderTexture);
            _renderTexture = null;
        }
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (_mirrorCamera == null || cam != _mirrorCamera) return;

        Camera viewer = GetViewerCamera();
        if (viewer != null)
        {
            Vector3 centerWS = transform.TransformPoint(LocalCenter);
            Vector3 normalWS = transform.TransformDirection(_localNormal).normalized;
            UpdateMirrorTransformAndMatrix(viewer, centerWS, normalWS);
        }

        HidePlayerRenderers();
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (_mirrorCamera == null || cam != _mirrorCamera) return;
        RestoreHiddenPlayerRenderers();
    }

    private void UpdateMirrorTransformAndMatrix(Camera viewer, Vector3 centerWS, Vector3 normalWS)
    {
        Vector3 viewerPos = viewer.transform.position;
        float dist = Vector3.Dot(viewerPos - centerWS, normalWS);
        if (dist <= 0.01f) return;

        Vector3 reflectedPos = viewerPos - 2f * dist * normalWS;

        Vector3 lookDir = (centerWS - reflectedPos).normalized;
        Vector3 upHint = transform.TransformDirection(_localTopLeft - _localBottomLeft).normalized;
        if (Mathf.Abs(Vector3.Dot(lookDir, upHint)) > 0.98f)
            upHint = Vector3.up;

        Quaternion lookRot = Quaternion.LookRotation(lookDir, upHint);
        _mirrorCamera.transform.SetPositionAndRotation(reflectedPos, lookRot);

        Quaternion invRot = Quaternion.Inverse(lookRot);
        float maxTanX = 0.2f;
        float maxTanY = 0.2f;
        UpdateCornerTan(invRot, reflectedPos, _localBottomLeft, ref maxTanX, ref maxTanY);
        UpdateCornerTan(invRot, reflectedPos, _localTopLeft, ref maxTanX, ref maxTanY);
        UpdateCornerTan(invRot, reflectedPos, _localTopRight, ref maxTanX, ref maxTanY);
        UpdateCornerTan(invRot, reflectedPos, _localBottomRight, ref maxTanX, ref maxTanY);

        float aspect = Mathf.Clamp(maxTanX / Mathf.Max(0.05f, maxTanY), 0.35f, 3.0f);
        float fov = Mathf.Clamp(2f * Mathf.Atan(maxTanY * 1.06f) * Mathf.Rad2Deg, 18f, 125f);

        _mirrorCamera.ResetProjectionMatrix();
        _mirrorCamera.aspect = aspect;
        _mirrorCamera.fieldOfView = fov;
        _mirrorCamera.nearClipPlane = 0.05f;
        _mirrorCamera.farClipPlane = 60f;

        if (_runtimeMaterial != null)
        {
            Matrix4x4 vp = _mirrorCamera.projectionMatrix * _mirrorCamera.worldToCameraMatrix;
            _runtimeMaterial.SetMatrix(MirrorVPId, vp);
        }

        Vector4 clipPlane = CameraSpacePlane(_mirrorCamera, centerWS, normalWS, 1f, _clipPlaneOffset);
        _mirrorCamera.projectionMatrix = _mirrorCamera.CalculateObliqueMatrix(clipPlane);
    }

    private void UpdateCornerTan(Quaternion invCamRot, Vector3 camPos, Vector3 localCorner, ref float maxTanX, ref float maxTanY)
    {
        Vector3 cornerWS = transform.TransformPoint(localCorner);
        Vector3 localToCam = invCamRot * (cornerWS - camPos);
        float z = Mathf.Max(0.05f, localToCam.z);
        float tx = Mathf.Abs(localToCam.x) / z;
        float ty = Mathf.Abs(localToCam.y) / z;
        if (tx > maxTanX) maxTanX = tx;
        if (ty > maxTanY) maxTanY = ty;
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign, float offset)
    {
        Vector3 offsetPos = pos + normal * offset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    private void HidePlayerRenderers()
    {
        RestoreHiddenPlayerRenderers();

        if (!_playerSearched || _cachedPlayer == null)
        {
            _cachedPlayer = FindAnyObjectByType<ExampleCharacterController>();
            _playerRenderers = _cachedPlayer != null ? _cachedPlayer.GetComponentsInChildren<Renderer>(true) : null;
            _playerSearched = true;
        }

        if (_playerRenderers == null) return;
        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            Renderer r = _playerRenderers[i];
            if (r == null || r.forceRenderingOff) continue;
            r.forceRenderingOff = true;
            _hiddenThisPass.Add(r);
        }
    }

    private void RestoreHiddenPlayerRenderers()
    {
        for (int i = 0; i < _hiddenThisPass.Count; i++)
        {
            Renderer r = _hiddenThisPass[i];
            if (r != null) r.forceRenderingOff = false;
        }
        _hiddenThisPass.Clear();
    }

    private static Camera GetViewerCamera()
    {
        if (Application.isPlaying)
            return Camera.main;

#if UNITY_EDITOR
        if (UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
            return UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
        return Camera.main;
    }
}
