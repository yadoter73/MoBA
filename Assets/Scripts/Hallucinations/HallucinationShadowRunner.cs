using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoBA.Hallucinations
{
    /// <summary>
    /// Спавнит и прогоняет очень тёмный силуэт монстра ростом с игрока:
    /// либо прямо перед игроком (при резком развороте), либо чуть-чуть за его спиной (в отражении зеркала).
    /// </summary>
    public class HallucinationShadowRunner : MonoBehaviour
    {
        private static readonly int AnimIsMoving = Animator.StringToHash("isMoving");
        private static readonly int AnimIsChasing = Animator.StringToHash("isChasing");
        private static readonly int StateChaseHash = Animator.StringToHash("Chase");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        [Header("Visual")]
        [SerializeField] private GameObject _shadowMonsterPrefab;
        [SerializeField] private Material _darkShadowMaterial;
        [SerializeField] private float _targetHeight = 7.2f;

        [Header("Front Run (Sharp Turn)")]
        [SerializeField] private float _frontDistance = 9.0f;
        [SerializeField] private float _frontMinDistance = 4.5f;
        [SerializeField] private float _frontLateralSpan = 22.0f;
        [SerializeField] private float _frontRunDuration = 0.65f;

        [Header("Mirror Run (Behind Player)")]
        [SerializeField] private float _mirrorBackDistance = 4.8f;
        [SerializeField] private float _mirrorMinBackDistance = 2.5f;
        [SerializeField] private float _mirrorLateralSpan = 14.0f;
        [SerializeField] private float _mirrorRunDuration = 0.60f;

        [Header("Optional Audio")]
        [SerializeField] private AudioClip _hallucinationClip;
        [SerializeField] [Range(0f, 1f)] private float _audioVolume = 0.55f;

        private GameObject _activeInstance;
        private Material _runtimeFallbackMaterial;
        private Tween _activeMoveTween;

        public bool IsRunning => _activeInstance != null;

        private void Awake()
        {
            // Защита от старых сериализованных значений под 1.75м-масштаб (игрок в Level1 имеет рост 7.2 юнита)
            if (_targetHeight < 3.5f)
            {
                _targetHeight = 7.2f;
                _frontDistance = 9.0f;
                _frontMinDistance = 4.5f;
                _frontLateralSpan = 22.0f;
                _frontRunDuration = 0.65f;
                _mirrorBackDistance = 4.8f;
                _mirrorMinBackDistance = 2.5f;
                _mirrorLateralSpan = 14.0f;
                _mirrorRunDuration = 0.60f;
            }
        }

        private void OnDestroy()
        {
            StopAndCleanup();
            if (_runtimeFallbackMaterial != null)
            {
                Destroy(_runtimeFallbackMaterial);
                _runtimeFallbackMaterial = null;
            }
        }

        public async UniTask RunInFrontOfPlayerAsync(
            Transform playerHead,
            Transform playerTransform,
            LayerMask groundMask,
            CancellationToken token)
        {
            if (IsRunning || playerHead == null) return;

            Vector3 flatForward = new Vector3(playerHead.forward.x, 0f, playerHead.forward.z);
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
            float safeForwardDist = ResolveSafeDistance(playerHead.position, flatForward, _frontDistance, _frontMinDistance);
            Vector3 center = playerHead.position + flatForward * safeForwardDist;

            float baseY = playerTransform != null ? playerTransform.position.y : playerHead.position.y - _targetHeight;
            float centerGroundY = SampleGroundY(center, baseY, groundMask);
            float sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float halfSpan = _frontLateralSpan * 0.5f;

            Vector3 startPos = center - right * (halfSpan * sideSign);
            Vector3 endPos = center + right * (halfSpan * sideSign);
            startPos.y = SampleGroundY(startPos, centerGroundY, groundMask);
            endPos.y = SampleGroundY(endPos, centerGroundY, groundMask);

            float desiredHeight = ResolveStandingPlayerHeight(playerHead, playerTransform);
            await ExecuteRunAsync(startPos, endPos, _frontRunDuration, desiredHeight, token);
        }

        public async UniTask RunBehindPlayerInMirrorAsync(
            Transform playerHead,
            Transform playerTransform,
            MirrorReflection mirror,
            LayerMask groundMask,
            CancellationToken token)
        {
            if (IsRunning || playerHead == null) return;

            // Направление чуть-чуть за спину игрока вдоль нормали зеркала (чтобы было видно в отражении, но не перед глазами)
            Vector3 backDir = mirror != null
                ? new Vector3(mirror.WorldNormal.x, 0f, mirror.WorldNormal.z)
                : new Vector3(-playerHead.forward.x, 0f, -playerHead.forward.z);

            if (backDir.sqrMagnitude < 0.0001f) backDir = -Vector3.forward;
            backDir.Normalize();

            Vector3 lateral = Vector3.Cross(Vector3.up, backDir).normalized;
            float safeBackDist = ResolveSafeDistance(playerHead.position, backDir, _mirrorBackDistance, _mirrorMinBackDistance);

            // Центрируем траекторию напротив центра зеркала, но чуть позади игрока
            Vector3 anchor = playerHead.position;
            if (mirror != null)
            {
                Vector3 mirrorCenter = mirror.WorldCenter;
                float playerDistFromMirror = Vector3.Dot(playerHead.position - mirrorCenter, backDir);
                float targetDistFromMirror = Mathf.Max(1.5f, playerDistFromMirror) + safeBackDist;
                anchor = new Vector3(mirrorCenter.x, playerHead.position.y, mirrorCenter.z) + backDir * targetDistFromMirror;
            }
            else
            {
                anchor = playerHead.position + backDir * safeBackDist;
            }

            // Внутри комнаты (у зеркала) держим высоту строго по полу игрока, чтобы стены не сбивали SampleGroundY
            float floorY = playerTransform != null ? playerTransform.position.y : playerHead.position.y - _targetHeight;
            float sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float halfSpan = _mirrorLateralSpan * 0.5f;

            Vector3 startPos = anchor - lateral * (halfSpan * sideSign);
            Vector3 endPos = anchor + lateral * (halfSpan * sideSign);
            startPos.y = floorY;
            endPos.y = floorY;

            float desiredHeight = ResolveStandingPlayerHeight(playerHead, playerTransform);
            await ExecuteRunAsync(startPos, endPos, _mirrorRunDuration, desiredHeight, token);
        }

        private float ResolveStandingPlayerHeight(Transform playerHead, Transform playerTransform)
        {
            float height = _targetHeight > 0.5f ? _targetHeight : 7.2f;
            if (playerTransform != null)
            {
                CapsuleCollider cap = playerTransform.GetComponent<CapsuleCollider>();
                if (cap != null && cap.height > height * 0.8f)
                {
                    height = Mathf.Max(height, cap.height);
                }
            }
            if (playerHead != null && playerTransform != null)
            {
                float eyeHeight = playerHead.position.y - playerTransform.position.y;
                if (eyeHeight > 2.5f)
                {
                    height = Mathf.Max(height, eyeHeight + 0.6f);
                }
            }
            return height;
        }

        private async UniTask ExecuteRunAsync(Vector3 startPos, Vector3 endPos, float duration, float desiredHeight, CancellationToken token)
        {
            Vector3 runDir = endPos - startPos;
            runDir.y = 0f;
            Quaternion facing = runDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(runDir.normalized, Vector3.up)
                : Quaternion.identity;

            _activeInstance = CreateShadowVisual(startPos, facing, desiredHeight);
            PlayOptionalSfx(startPos);

            try
            {
                _activeMoveTween = Tween.Position(
                    _activeInstance.transform,
                    startValue: startPos,
                    endValue: endPos,
                    duration: Mathf.Max(0.25f, duration),
                    ease: Ease.Linear,
                    useUnscaledTime: false);

                await _activeMoveTween.ToUniTask(cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена при выгрузке сцены или отключении
            }
            finally
            {
                StopAndCleanup();
            }
        }

        private GameObject CreateShadowVisual(Vector3 feetPosition, Quaternion rotation, float desiredHeight)
        {
            GameObject runnerRoot = new GameObject("HallucinationShadow_Runtime");
            runnerRoot.transform.SetPositionAndRotation(feetPosition, rotation);

            GameObject visual;
            if (_shadowMonsterPrefab != null)
            {
                visual = Instantiate(_shadowMonsterPrefab, feetPosition, rotation);
                StripGameplayComponents(visual);
            }
            else
            {
                visual = BuildProceduralShadowSilhouette(feetPosition, rotation);
            }

            visual.transform.SetParent(runnerRoot.transform, true);
            SetLayerRecursive(runnerRoot, 0); // Default — рендерится и основной камерой, и MirrorCamera

            Material mat = GetOrCreateDarkMaterial();
            ApplyDarkMaterialFitHeightAndGroundPivot(runnerRoot.transform, visual.transform, mat, desiredHeight);
            StartRunAnimation(visual);
            return runnerRoot;
        }

        private void ApplyDarkMaterialFitHeightAndGroundPivot(Transform runnerRoot, Transform visual, Material mat, float desiredHeight)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null) continue;

                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;

                if (rend is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }

                if (mat != null)
                {
                    Material[] slots = new Material[Mathf.Max(1, rend.sharedMaterials.Length)];
                    for (int s = 0; s < slots.Length; s++) slots[s] = mat;
                    rend.sharedMaterials = slots;
                }
            }

            if (TryComputeBounds(renderers, out Bounds beforeBounds) && beforeBounds.size.y > 0.05f && desiredHeight > 0.2f)
            {
                float scaleFactor = Mathf.Clamp(desiredHeight / beforeBounds.size.y, 0.05f, 30f);
                visual.localScale *= scaleFactor;
            }

            // Выравниваем ноги модели (min.y) и горизонтальный центр строго по runnerRoot.position,
            // чтобы смещённый pivot префаба (например Body.y = 12.9 в Agent.prefab) не уводил монстра в потолок.
            if (TryComputeBounds(renderers, out Bounds afterBounds))
            {
                Vector3 correction = new Vector3(
                    runnerRoot.position.x - afterBounds.center.x,
                    runnerRoot.position.y - afterBounds.min.y,
                    runnerRoot.position.z - afterBounds.center.z);
                visual.position += correction;
            }
        }

        private static bool TryComputeBounds(Renderer[] renderers, out Bounds combined)
        {
            combined = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null) continue;
                if (!hasBounds)
                {
                    combined = rend.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(rend.bounds);
                }
            }
            return hasBounds;
        }

        private static void StartRunAnimation(GameObject instance)
        {
            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator anim = animators[i];
                if (anim == null || anim.runtimeAnimatorController == null) continue;

                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.speed = 2.0f;

                foreach (AnimatorControllerParameter param in anim.parameters)
                {
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        if (param.nameHash == AnimIsMoving) anim.SetBool(AnimIsMoving, true);
                        else if (param.nameHash == AnimIsChasing) anim.SetBool(AnimIsChasing, true);
                    }
                }

                if (anim.HasState(0, StateChaseHash))
                {
                    anim.Play(StateChaseHash, 0, 0f);
                }
            }
        }

        private static void StripGameplayComponents(GameObject root)
        {
            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(rb);
            }
            foreach (UnityEngine.AI.NavMeshAgent agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            {
                Destroy(agent);
            }
            foreach (EnemyAI ai in root.GetComponentsInChildren<EnemyAI>(true))
            {
                Destroy(ai);
            }
            foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
            {
                Destroy(audio);
            }
        }

        private static GameObject BuildProceduralShadowSilhouette(Vector3 position, Quaternion rotation)
        {
            GameObject root = new GameObject("ProceduralShadowSilhouette");
            root.transform.SetPositionAndRotation(position, rotation);

            CreatePart(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.92f, 0f), new Vector3(0.42f, 0.58f, 0.30f));
            CreatePart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.60f, 0.05f), new Vector3(0.28f, 0.32f, 0.28f));
            CreatePart(root.transform, PrimitiveType.Capsule, new Vector3(-0.14f, 0.36f, 0.08f), new Vector3(0.15f, 0.40f, 0.15f));
            CreatePart(root.transform, PrimitiveType.Capsule, new Vector3(0.14f, 0.36f, -0.08f), new Vector3(0.15f, 0.40f, 0.15f));
            CreatePart(root.transform, PrimitiveType.Capsule, new Vector3(-0.30f, 1.02f, -0.06f), new Vector3(0.12f, 0.38f, 0.12f));
            CreatePart(root.transform, PrimitiveType.Capsule, new Vector3(0.30f, 1.02f, 0.06f), new Vector3(0.12f, 0.38f, 0.12f));
            return root;
        }

        private static void CreatePart(Transform parent, PrimitiveType primitive, Vector3 localPos, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
        }

        private Material GetOrCreateDarkMaterial()
        {
            if (_darkShadowMaterial != null) return _darkShadowMaterial;

            if (_runtimeFallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                _runtimeFallbackMaterial = new Material(shader) { name = "HallucinationShadow_RuntimeMat" };
                Color veryDark = new Color(0.08f, 0.08f, 0.11f, 1f);
                if (_runtimeFallbackMaterial.HasProperty(BaseColorId)) _runtimeFallbackMaterial.SetColor(BaseColorId, veryDark);
                if (_runtimeFallbackMaterial.HasProperty(ColorId)) _runtimeFallbackMaterial.SetColor(ColorId, veryDark);
                if (_runtimeFallbackMaterial.HasProperty(SmoothnessId)) _runtimeFallbackMaterial.SetFloat(SmoothnessId, 0.08f);
                if (_runtimeFallbackMaterial.HasProperty(MetallicId)) _runtimeFallbackMaterial.SetFloat(MetallicId, 0f);
            }

            return _runtimeFallbackMaterial;
        }

        private void PlayOptionalSfx(Vector3 worldPos)
        {
            if (_hallucinationClip != null)
            {
                AudioSource.PlayClipAtPoint(_hallucinationClip, worldPos, _audioVolume);
            }
        }

        private float ResolveSafeDistance(Vector3 origin, Vector3 dir, float desiredDist, float minDist)
        {
            const int mask = ~(1 << 2 | 1 << 3); // Исключаем Ignore Raycast и Player
            if (Physics.Raycast(origin, dir, out RaycastHit hit, desiredDist, mask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(minDist, hit.distance - 1.5f);
            }
            return desiredDist;
        }

        private static float SampleGroundY(Vector3 pos, float fallbackY, LayerMask groundMask)
        {
            int mask = groundMask.value != 0 ? groundMask.value : ~(1 << 2 | 1 << 3);
            Vector3 rayOrigin = new Vector3(pos.x, fallbackY + 8f, pos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 24f, mask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return fallbackY;
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            Transform tf = obj.transform;
            for (int i = 0; i < tf.childCount; i++)
            {
                SetLayerRecursive(tf.GetChild(i).gameObject, layer);
            }
        }

        private void StopAndCleanup()
        {
            _activeMoveTween.Stop();
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
            }
        }
    }
}
