using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Player;
using PrimeTween;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Интерактивный стул для сцены ужина после доставки еды Сае:
    /// отодвигает стул, плавно сажает камеру игрока за стол, проигрывает анимацию поедания яблок и питья водки,
    /// запускает диалог с вопросом, почему Сая не ест, и поднимает игрока из-за стола.
    /// </summary>
    public class DinnerChairInteractable : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
    {
        [SerializeField] private Transform _tableTransform;
        [SerializeField] private Transform _sayaTransform;
        [SerializeField] private GameObject _applePrefab;
        [SerializeField] private GameObject _vodkaPrefab;
        [SerializeField] private GameObject _breadPrefab;
        [SerializeField] private NPCConversation _dinnerConversation;

        [Inject(Optional = true)] private KeyQuestService _questService;
        [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;
        [Inject(Id = "HeadTransform", Optional = true)] private Transform _playerHead;

        private readonly DialogueSessionRunner _runner = new DialogueSessionRunner();
        private bool _isRunning;
        private CancellationTokenSource _cutsceneCts;

        public InteractionPromptType PromptType => InteractionPromptType.Interact;
        public bool CanInteractNow => !_isRunning && _questService != null && _questService.CanSitForDinner;

        public void Configure(
            KeyQuestService questService,
            IPlayerControlLock playerControlLock,
            Transform playerHead,
            Transform tableTransform,
            Transform sayaTransform)
        {
            if (questService != null) _questService = questService;
            if (playerControlLock != null) _playerControlLock = playerControlLock;
            if (playerHead != null) _playerHead = playerHead;
            if (tableTransform != null) _tableTransform = tableTransform;
            if (sayaTransform != null) _sayaTransform = sayaTransform;
        }

        private void OnDisable()
        {
            CancelCutscene();
        }

        private void OnDestroy()
        {
            CancelCutscene();
        }

        public void Interact(int id)
        {
            if (!CanInteractNow) return;

            CancelCutscene();
            _cutsceneCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            PlayDinnerCutsceneAsync(_cutsceneCts.Token).Forget();
        }

        private async UniTaskVoid PlayDinnerCutsceneAsync(CancellationToken token)
        {
            _isRunning = true;
            IDisposable controlLock = _playerControlLock?.AcquireLock(unlockCursor: false);

            Vector3 chairStartPos = transform.position;
            Quaternion chairStartRot = transform.rotation;

            GameObject camGo = null;
            CinemachineCamera dinnerCam = null;
            var spawnedProps = new List<GameObject>();

            try
            {
                Camera mainCam = Camera.main;
                Vector3 camStartPos = mainCam != null
                    ? mainCam.transform.position
                    : (_playerHead != null ? _playerHead.position : chairStartPos + Vector3.up * 5.2f);
                Quaternion camStartRot = mainCam != null
                    ? mainCam.transform.rotation
                    : (_playerHead != null ? _playerHead.rotation : transform.rotation);

                camGo = new GameObject("DinnerCutsceneCamera");
                camGo.transform.SetPositionAndRotation(camStartPos, camStartRot);
                dinnerCam = camGo.AddComponent<CinemachineCamera>();
                dinnerCam.Priority = 120;
                dinnerCam.Lens.FieldOfView = mainCam != null ? mainCam.fieldOfView : 75f;

                Vector3 toTable = _tableTransform != null
                    ? (_tableTransform.position - chairStartPos)
                    : transform.forward;
                toTable.y = 0f;
                if (toTable.sqrMagnitude < 0.001f) toTable = Vector3.forward;
                toTable.Normalize();

                Vector3 pullOutPos = chairStartPos - toTable * 0.65f;

                // 1. Отодвигаем стул
                await Tween.Position(transform, pullOutPos, 0.42f, Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                // 2. Определяем сидячую позицию камеры над стулом (чуть выше и дальше от стола, чтобы видеть Саю целиком и весь стол)
                Bounds chairBounds = GetObjectBounds(gameObject);
                Vector3 tableSurfaceCenter = ResolveTableSurfaceCenter(chairStartPos, toTable);
                float seatHeight = Mathf.Max(
                    chairBounds.size.y > 0.1f ? chairBounds.min.y + chairBounds.size.y * 0.48f + 1.98f : chairStartPos.y + 2.75f,
                    tableSurfaceCenter.y + 0.72f);

                Vector3 tableFocus = _tableTransform != null
                    ? _tableTransform.position + Vector3.up * 0.85f
                    : chairStartPos + toTable * 1.1f + Vector3.up * 0.85f;
                Vector3 sayaFocus = _sayaTransform != null
                    ? _sayaTransform.position + Vector3.up * 1.40f
                    : chairStartPos + toTable * 2.0f + Vector3.up * 1.45f;
                Vector3 lookTarget = Vector3.Lerp(tableFocus, sayaFocus, 0.78f);

                Vector3 seatedCamPos = new Vector3(chairStartPos.x, seatHeight, chairStartPos.z) - toTable * 0.10f;
                Quaternion seatedCamRot = Quaternion.LookRotation((lookTarget - seatedCamPos).normalized, Vector3.up);

                await UniTask.WhenAll(
                    Tween.Position(camGo.transform, seatedCamPos, 0.72f, Ease.InOutSine).ToUniTask(cancellationToken: token),
                    Tween.Rotation(camGo.transform, seatedCamRot, 0.72f, Ease.InOutSine).ToUniTask(cancellationToken: token));

                // 3. Пододвигаем стул обратно к столу
                await Tween.Position(transform, chairStartPos, 0.35f, Ease.InOutQuad)
                    .ToUniTask(cancellationToken: token);

                // 4. Выставляем принесённые продукты из магазина на стол в их реальном размере
                Vector3 right = Vector3.Cross(Vector3.up, toTable).normalized;

                var apples = new List<GameObject>(3);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 applePos = tableSurfaceCenter + right * ((i - 1) * 0.18f) - toTable * 0.14f;
                    apples.Add(SpawnFoodProp(_applePrefab, "DinnerApple_" + i, applePos, Quaternion.Euler(0f, 35f + i * 95f, 0f), new Color(0.75f, 0.12f, 0.10f), new Vector3(0.09f, 0.09f, 0.09f), spawnedProps));
                }

                Vector3 vodkaPos = tableSurfaceCenter + right * 0.36f + toTable * 0.06f;
                Quaternion vodkaUprightRot = Quaternion.LookRotation(-toTable, Vector3.up);
                GameObject vodka = SpawnFoodProp(_vodkaPrefab, "DinnerVodka", vodkaPos, vodkaUprightRot, new Color(0.62f, 0.78f, 0.72f), new Vector3(0.10f, 0.30f, 0.10f), spawnedProps, isCylinder: true);
                if (vodka != null) vodkaPos = vodka.transform.position;

                Vector3 breadPos = tableSurfaceCenter - right * 0.42f - toTable * 0.04f;
                GameObject bread = SpawnFoodProp(_breadPrefab, "DinnerBread", breadPos, Quaternion.Euler(0f, -22f, 0f), new Color(0.72f, 0.50f, 0.24f), new Vector3(0.24f, 0.12f, 0.15f), spawnedProps);

                await UniTask.Delay(TimeSpan.FromSeconds(0.45f), cancellationToken: token);

                // 5. Неспешная анимация поедания яблок: подъём к комфортной дистанции перед камерой и несколько укусов
                for (int i = 0; i < apples.Count; i++)
                {
                    if (apples[i] == null) continue;
                    await EatFoodPropAsync(
                        apples[i],
                        camGo.transform,
                        minDistance: 0.46f,
                        handOffset: new Vector3(0.04f, -0.11f, 0f),
                        biteCount: 2,
                        liftDuration: 0.62f,
                        biteDuration: 0.22f,
                        finalScaleFactor: 0.52f,
                        token);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.18f), cancellationToken: token);
                }

                // 6. Неспешная анимация поедания хлеба
                if (bread != null)
                {
                    await EatFoodPropAsync(
                        bread,
                        camGo.transform,
                        minDistance: 0.56f,
                        handOffset: new Vector3(-0.03f, -0.13f, 0f),
                        biteCount: 3,
                        liftDuration: 0.72f,
                        biteDuration: 0.25f,
                        finalScaleFactor: 0.42f,
                        token);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.22f), cancellationToken: token);
                }

                // 7. Анимация питья водки с учётом реальных Bounds бутылки, чтобы она не клипала в камеру
                if (vodka != null)
                {
                    Bounds vodkaBounds = GetObjectBounds(vodka);
                    float bottleHeight = Mathf.Max(0.28f, vodkaBounds.size.y);
                    float liftDist = Mathf.Max(0.82f, bottleHeight * 1.45f + 0.40f);
                    float tiltDist = Mathf.Max(0.72f, bottleHeight * 1.25f + 0.34f);

                    Vector3 camPos = camGo.transform.position;
                    Vector3 camFwd = camGo.transform.forward;
                    Vector3 camUp = camGo.transform.up;
                    Vector3 camRight = camGo.transform.right;

                    Vector3 drinkLiftPos = camPos + camFwd * liftDist - camUp * (0.14f + bottleHeight * 0.18f) + camRight * 0.06f;
                    Vector3 drinkTiltPos = camPos + camFwd * tiltDist - camUp * (0.06f + bottleHeight * 0.10f);
                    vodka.transform.rotation = vodkaUprightRot;

                    await Tween.Position(vodka.transform, drinkLiftPos, 0.62f, Ease.OutCubic)
                        .ToUniTask(cancellationToken: token);

                    // Естественный наклон для глотка горлышком к игроку без пересечения ближней плоскости камеры
                    Quaternion tiltRot = Quaternion.AngleAxis(72f, -camRight) * vodkaUprightRot;
                    await UniTask.WhenAll(
                        Tween.Position(vodka.transform, drinkTiltPos, 0.50f, Ease.InOutSine).ToUniTask(cancellationToken: token),
                        Tween.Rotation(vodka.transform, tiltRot, 0.50f, Ease.InOutSine).ToUniTask(cancellationToken: token));

                    await UniTask.Delay(TimeSpan.FromSeconds(0.85f), cancellationToken: token);

                    await UniTask.WhenAll(
                        Tween.Position(vodka.transform, drinkLiftPos, 0.45f, Ease.OutQuad).ToUniTask(cancellationToken: token),
                        Tween.Rotation(vodka.transform, vodkaUprightRot, 0.45f, Ease.OutQuad).ToUniTask(cancellationToken: token));
                    await Tween.Position(vodka.transform, vodkaPos, 0.55f, Ease.InOutQuad)
                        .ToUniTask(cancellationToken: token);
                }

                // 8. Диалог строго через DialogueEditor: игрок спрашивает, почему Сая сама не ест
                await PlayDinnerDialogueAsync(token);

                // 8. Встаём из-за стола: стул чуть отодвигается, камера плавно возвращается в стойку
                await Tween.Position(transform, pullOutPos, 0.35f, Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                await UniTask.WhenAll(
                    Tween.Position(camGo.transform, camStartPos, 0.65f, Ease.InOutSine).ToUniTask(cancellationToken: token),
                    Tween.Rotation(camGo.transform, camStartRot, 0.65f, Ease.InOutSine).ToUniTask(cancellationToken: token));

                await Tween.Position(transform, chairStartPos, 0.30f, Ease.InOutQuad)
                    .ToUniTask(cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена при выгрузке сцены
            }
            finally
            {
                for (int i = 0; i < spawnedProps.Count; i++)
                {
                    if (spawnedProps[i] != null) Destroy(spawnedProps[i]);
                }

                transform.SetPositionAndRotation(chairStartPos, chairStartRot);

                if (dinnerCam != null) dinnerCam.Priority = -10;
                if (camGo != null) Destroy(camGo);

                controlLock?.Dispose();
                _isRunning = false;

                if (!token.IsCancellationRequested)
                {
                    _questService?.NotifyDinnerFinished();
                }
            }
        }

        private async UniTask EatFoodPropAsync(
            GameObject prop,
            Transform camTransform,
            float minDistance,
            Vector3 handOffset,
            int biteCount,
            float liftDuration,
            float biteDuration,
            float finalScaleFactor,
            CancellationToken token)
        {
            if (prop == null || camTransform == null) return;

            Bounds bounds = GetObjectBounds(prop);
            float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            float dist = Mathf.Max(minDistance, radius + 0.36f);

            Vector3 camPos = camTransform.position;
            Vector3 camFwd = camTransform.forward;
            Vector3 camUp = camTransform.up;
            Vector3 camRight = camTransform.right;

            Vector3 holdPos = camPos + camFwd * dist + camRight * handOffset.x + camUp * handOffset.y;
            Quaternion holdRot = Quaternion.LookRotation(-camFwd, Vector3.up);
            Vector3 startScale = prop.transform.localScale;

            await UniTask.WhenAll(
                Tween.Position(prop.transform, holdPos, liftDuration, Ease.OutCubic).ToUniTask(cancellationToken: token),
                Tween.Rotation(prop.transform, holdRot, liftDuration, Ease.OutCubic).ToUniTask(cancellationToken: token));

            Vector3 bitePos = holdPos - camFwd * 0.06f + camUp * 0.03f;
            Quaternion biteRot = Quaternion.AngleAxis(-14f, camRight) * holdRot;

            int safeBites = Mathf.Max(1, biteCount);
            for (int b = 0; b < safeBites; b++)
            {
                float t = (b + 1) / (float)safeBites;
                Vector3 targetScale = Vector3.Lerp(startScale, startScale * finalScaleFactor, t);

                await UniTask.WhenAll(
                    Tween.Position(prop.transform, bitePos, biteDuration, Ease.InOutSine).ToUniTask(cancellationToken: token),
                    Tween.Rotation(prop.transform, biteRot, biteDuration, Ease.InOutSine).ToUniTask(cancellationToken: token),
                    Tween.Scale(prop.transform, targetScale, biteDuration, Ease.OutQuad).ToUniTask(cancellationToken: token));

                await UniTask.WhenAll(
                    Tween.Position(prop.transform, holdPos, biteDuration, Ease.InOutSine).ToUniTask(cancellationToken: token),
                    Tween.Rotation(prop.transform, holdRot, biteDuration, Ease.InOutSine).ToUniTask(cancellationToken: token));

                await UniTask.Delay(TimeSpan.FromSeconds(0.20f), cancellationToken: token);
            }

            await Tween.Scale(prop.transform, Vector3.zero, 0.22f, Ease.InQuad)
                .ToUniTask(cancellationToken: token);
            prop.SetActive(false);
        }

        private async UniTask PlayDinnerDialogueAsync(CancellationToken token)
        {
            if (_dinnerConversation == null)
            {
                Debug.LogError("[DinnerChairInteractable] _dinnerConversation не назначен! Запустите меню MoBA -> Setup Dinner Cutscene.", this);
                return;
            }

            await _runner.RunAsync(
                _sayaTransform,
                null,
                _playerControlLock,
                _dinnerConversation,
                turnDuration: 0f,
                rotateNpc: false,
                onCompleted: null,
                token);
        }

        private Vector3 ResolveTableSurfaceCenter(Vector3 chairPos, Vector3 toTable)
        {
            if (_tableTransform != null)
            {
                Bounds tb = GetObjectBounds(_tableTransform.gameObject);
                float topY = tb.size.y > 0.05f ? tb.max.y + 0.04f : _tableTransform.position.y + 0.85f;
                Vector3 towardPlayerSide = new Vector3(_tableTransform.position.x, topY, _tableTransform.position.z) - toTable * 0.28f;
                return towardPlayerSide;
            }

            return chairPos + toTable * 0.85f + Vector3.up * 0.85f;
        }

        private GameObject SpawnFoodProp(
            GameObject prefab,
            string name,
            Vector3 worldSurfacePos,
            Quaternion worldRot,
            Color fallbackColor,
            Vector3 fallbackScale,
            List<GameObject> spawnedList,
            bool isCylinder = false)
        {
            GameObject obj;
            if (prefab != null)
            {
                obj = Instantiate(prefab, worldSurfacePos, worldRot);
                foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
                {
                    Destroy(col);
                }
                foreach (GroceryPickup pickup in obj.GetComponentsInChildren<GroceryPickup>(true))
                {
                    Destroy(pickup);
                }

                Bounds b = GetObjectBounds(obj);
                if (b.size.y > 0.001f)
                {
                    obj.transform.position += Vector3.up * (worldSurfacePos.y - b.min.y);
                }
            }
            else
            {
                obj = GameObject.CreatePrimitive(isCylinder ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                Collider col = obj.GetComponent<Collider>();
                if (col != null) Destroy(col);
                obj.transform.SetPositionAndRotation(worldSurfacePos, worldRot);
                obj.transform.localScale = fallbackScale;

                Bounds b = GetObjectBounds(obj);
                if (b.size.y > 0.001f)
                {
                    obj.transform.position += Vector3.up * (worldSurfacePos.y - b.min.y);
                }

                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    var mat = new Material(lit);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
                    r.sharedMaterial = mat;
                }
            }

            obj.name = name;
            spawnedList.Add(obj);
            return obj;
        }

        private static Bounds GetObjectBounds(GameObject go)
        {
            Bounds b = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private void CancelCutscene()
        {
            if (_cutsceneCts != null)
            {
                _cutsceneCts.Cancel();
                _cutsceneCts.Dispose();
                _cutsceneCts = null;
            }
        }
    }
}
