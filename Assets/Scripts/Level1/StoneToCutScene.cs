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
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using Zenject;

public class Stone : MonoBehaviour, IInteractable, IConditionalInteractionPrompt
{
    private enum QuestStage
    {
        WaitingForStoneApproach = 0,
        PlayingLakeCutscene = 1,
        WaitingForSayaRopedApproach = 2,
        PlayingBlinkAndDialogue = 3,
        SayaWalkingToHouse = 4,
        SayaSeatedInHouse = 5
    }

    [SerializeField] private PlayableDirector _cutscene;
    [SerializeField] private GameObject _saya;
    [SerializeField] private GameObject _sayaRoped;
    [SerializeField] private LineToSaya _lineToSaya;

    [Header("Quest Settings")]
    [SerializeField] private float _stoneAutoTriggerDistance = 4.8f;
    [SerializeField] private float _sayaRopedTriggerDistance = 18.0f; // 15-20 м по заданию
    [SerializeField] private float _sayaWalkSpeed = 7.4f;

    [Inject(Id = "TextScript", Optional = true)] private TextRenaming _textRen;
    [Inject(Id = "PlayerTransform", Optional = true)] private Transform _player;
    [Inject(Id = "NpcConversation", Optional = true)] private NPCConversation _npcConversation;
    [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;

    public event Action OnSayaSeated;
    public bool IsSayaSeated => _stage == QuestStage.SayaSeatedInHouse;
    public Transform HouseTransform => _houseTransform;
    public Transform SayaChairTransform => _targetChair;
    public Transform DiningTableTransform => _diningTable;
    public Transform SayaTransform => _saya != null ? _saya.transform : null;
    public InteractionPromptType PromptType => InteractionPromptType.Interact;
    public bool CanInteractNow => _stage == QuestStage.WaitingForStoneApproach;

    private QuestStage _stage = QuestStage.WaitingForStoneApproach;
    private Image _blinkOverlayImage;
    private SayaSeatedPoseRig _sayaPoseRig;
    private Transform _houseTransform;
    private Transform _houseLeftDoor;
    private Transform _houseRightDoor;
    private Transform _targetChair;
    private Transform _diningTable;
    private bool _hasEnteredHouse;

    private static readonly Vector3 SayaRopedWorldPos = new Vector3(-56.8f, 13.76f, 11.81f);
    private static readonly Vector2 SayaGroundXZ = new Vector2(-56.8f, 11.81f);
    private const float SayaScaleMultiplier = 1.32f;

    private void Awake()
    {
        ResolveReferences();
        if (_cutscene != null && _cutscene.state != PlayState.Playing)
        {
            SetCutsceneCamerasEnabled(false);
        }

        // Инициализируем риг Саи на Awake до первого кадра Animator, чтобы зафиксировать точный bind-покой костей
        if (_saya != null)
        {
            _sayaPoseRig = _saya.GetComponent<SayaSeatedPoseRig>();
            if (_sayaPoseRig == null)
            {
                _sayaPoseRig = _saya.AddComponent<SayaSeatedPoseRig>();
            }
            _sayaPoseRig.Initialize(_player);
        }
    }

    private void Start()
    {
        ResolveReferences();

        // Прячем Саю на верёвке до окончания кат-сцены у озера и задаём корректный масштаб
        if (_sayaRoped != null)
        {
            _sayaRoped.transform.localScale = Vector3.one * SayaScaleMultiplier;
            _sayaRoped.transform.position = SayaRopedWorldPos;
            _sayaRoped.SetActive(false);
        }

        // На старте у реки Саи НЕ должно быть видно — она появится только после падения и утопления камня
        if (_saya != null)
        {
            EnsureSayaScaleAndCollision(_saya, isSitting: false);
            _sayaPoseRig = _saya.GetComponent<SayaSeatedPoseRig>();
            if (_sayaPoseRig == null)
            {
                _sayaPoseRig = _saya.AddComponent<SayaSeatedPoseRig>();
            }
            _sayaPoseRig.Initialize(_player);
            _sayaPoseRig.SetStandingIdle(true);
            _saya.SetActive(false);
        }

        // На старте боковые стрелки к озеру не включаем (3D-указатель над озером остаётся активным в PointerInmpulse)
    }

    private void OnEnable()
    {
        if (_cutscene != null)
        {
            _cutscene.stopped += OnCutsceneEnd;
        }
    }

    private void OnDisable()
    {
        if (_cutscene != null)
        {
            _cutscene.stopped -= OnCutsceneEnd;
        }
    }

    private void SetCutsceneCamerasEnabled(bool enabled)
    {
        if (_cutscene == null) return;
        foreach (var vcam in _cutscene.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
        {
            if (vcam != null) vcam.enabled = enabled;
        }
    }

    private void ResolveReferences()
    {
        if (_player == null)
        {
            var cc = FindAnyObjectByType<ExampleCharacterController>();
            if (cc != null) _player = cc.transform;
        }

        if (_textRen == null)
        {
            _textRen = FindAnyObjectByType<TextRenaming>();
        }

        if (_lineToSaya == null)
        {
            _lineToSaya = FindAnyObjectByType<LineToSaya>();
        }

        if (_npcConversation == null)
        {
            _npcConversation = FindAnyObjectByType<NPCConversation>();
        }

        if (_sayaRoped == null)
        {
            GameObject foundRoped = GameObject.Find("SayaRoped");
            if (foundRoped != null) _sayaRoped = foundRoped;
        }

        if (_houseTransform == null)
        {
            GameObject mainHouse = GameObject.Find("MainHouse");
            if (mainHouse != null)
            {
                _houseTransform = mainHouse.transform;
                _houseLeftDoor = FindDeepChild(_houseTransform, "leftDoor");
                _houseRightDoor = FindDeepChild(_houseTransform, "rightDoor");
                _targetChair = FindDeepChild(_houseTransform, "PFB_DiningChair (3)");
                if (_targetChair == null) _targetChair = FindDeepChild(_houseTransform, "PFB_DiningChair (1)");
                _diningTable = FindDeepChild(_houseTransform, "PFB_DiningTable");
            }
        }
    }

    private void Update()
    {
        if (_player == null) return;

        switch (_stage)
        {
            case QuestStage.WaitingForStoneApproach:
            {
                // Игрок сам подходит к реке и берёт камень через взаимодействие (Press E -> Interact)
                break;
            }

            case QuestStage.WaitingForSayaRopedApproach:
            {
                if (_sayaRoped == null || !_sayaRoped.activeInHierarchy) break;

                Vector3 toRoped = _sayaRoped.transform.position - _player.position;
                float flatDist = new Vector2(toRoped.x, toRoped.z).magnitude;
                float fullDist = toRoped.magnitude;

                // Когда игрок подходит к Сае на верёвке на расстояние ~15-20 м
                if (flatDist <= _sayaRopedTriggerDistance || fullDist <= _sayaRopedTriggerDistance + 1.5f)
                {
                    _stage = QuestStage.PlayingBlinkAndDialogue;
                    RunBlinkAndDialogueSequence().Forget();
                }
                break;
            }

            case QuestStage.SayaWalkingToHouse:
            {
                if (!_hasEnteredHouse && IsPlayerInsideHouse())
                {
                    _hasEnteredHouse = true;
                    if (PointerInmpulse.Instance != null)
                        PointerInmpulse.Instance.DisableAllPointers();
                }
                break;
            }

            case QuestStage.SayaSeatedInHouse:
            {
                if (!_hasEnteredHouse && IsPlayerInsideHouse())
                {
                    _hasEnteredHouse = true;
                    if (PointerInmpulse.Instance != null)
                        PointerInmpulse.Instance.DisableAllPointers();
                }

                if (_saya != null)
                {
                    float distToSeatedSaya = Vector3.Distance(_player.position, _saya.transform.position);
                    if (distToSeatedSaya <= 12.0f)
                    {
                        if (PointerInmpulse.Instance != null)
                            PointerInmpulse.Instance.DisableAllPointers();
                        if (_lineToSaya != null)
                            _lineToSaya.DeactivateImmediate();
                        if (_textRen != null)
                            _textRen.HideObjective(0.8f);
                    }
                }
                break;
            }
        }
    }

    private bool IsPlayerInsideHouse()
    {
        if (_player == null) return false;

        Vector3 houseCenter = _houseTransform != null
            ? _houseTransform.position
            : new Vector3(77.42f, 2.59f, -73.97f);

        Vector3 doorPos = new Vector3(69.98f, houseCenter.y + 0.46f, -51.5f);
        if (_houseLeftDoor != null && _houseRightDoor != null)
        {
            doorPos = (_houseLeftDoor.position + _houseRightDoor.position) * 0.5f;
        }
        else if (_houseLeftDoor != null)
        {
            doorPos = _houseLeftDoor.position;
        }

        Vector3 intoHouse = new Vector3(houseCenter.x - doorPos.x, 0f, houseCenter.z - doorPos.z);
        if (intoHouse.sqrMagnitude < 0.001f) intoHouse = Vector3.back;
        intoHouse.Normalize();

        Vector3 playerFromDoor = new Vector3(_player.position.x - doorPos.x, 0f, _player.position.z - doorPos.z);
        bool pastDoorThreshold = Vector3.Dot(playerFromDoor, intoHouse) > 1.2f;
        bool withinHouseFootprint = Mathf.Abs(_player.position.x - houseCenter.x) < 22f &&
                                    Mathf.Abs(_player.position.z - houseCenter.z) < 22f &&
                                    _player.position.y >= houseCenter.y - 0.5f;

        return pastDoorThreshold && withinHouseFootprint;
    }

    public void Interact(int id)
    {
        if (_stage == QuestStage.WaitingForStoneApproach)
        {
            TriggerLakeCutscene(0.1f);
        }
    }

    private void TriggerLakeCutscene(float delay)
    {
        if (_stage != QuestStage.WaitingForStoneApproach) return;
        _stage = QuestStage.PlayingLakeCutscene;

        if (PointerInmpulse.Instance != null)
        {
            PointerInmpulse.Instance.DisableAllPointers();
        }

        if (_textRen != null)
        {
            _textRen.HideObjective(0.3f);
        }

        PlayLakeCutsceneAsync(delay, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PlayLakeCutsceneAsync(float delay, CancellationToken token)
    {
        IDisposable controlLock = _playerControlLock?.AcquireLock(unlockCursor: false);
        CinemachineVirtualCameraBase camStone = null;
        CinemachineVirtualCameraBase camSaya = null;
        Camera mainCam = Camera.main;
        CinemachineBrain brain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;
        bool wasBrainEnabled = brain != null && brain.enabled;

        try
        {
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
            }

            // Останавливаем PlayableDirector, чтобы его старый Timeline-клип не подбрасывал Саю в воздух
            if (_cutscene != null)
            {
                _cutscene.Stop();
                foreach (CinemachineVirtualCameraBase vcam in _cutscene.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
                {
                    if (vcam == null) continue;
                    if (vcam.name == "CinemachineCamera") camStone = vcam;
                    else if (vcam.name == "CinemachineCamera (1)") camSaya = vcam;
                    vcam.enabled = false;
                }
            }

            // Гарантируем, что Сая скрыта до тех пор, пока камень не утонет в реке
            if (_saya != null)
            {
                _saya.SetActive(false);
            }

            // Определяем реальную высоту поверхности воды
            float waterY = 7.51f;
            GameObject waterPlane = GameObject.Find("Water_Plane");
            if (waterPlane != null)
            {
                waterY = waterPlane.transform.position.y;
            }

            // Отключаем коллайдер камня и его Animator (чтобы ничего не мешало полёту)
            Collider stoneCol = GetComponent<Collider>();
            if (stoneCol != null) stoneCol.enabled = false;
            Animator stoneAnim = GetComponent<Animator>();
            if (stoneAnim != null) stoneAnim.enabled = false;

            Vector3 stoneWaterEntry = new Vector3(-59.19f, waterY + 0.15f, 153.11f);
            Vector3 stoneGroundPos = transform.position;
            Quaternion stoneGroundRot = transform.rotation;

            // Временно отключаем CinemachineBrain, чтобы плавно управлять камерой от первого лица
            if (brain != null)
            {
                brain.enabled = false;
            }

            // Шаг 1А: Игрок тянется к камню — камень поднимается с земли в правую руку перед камерой
            if (mainCam != null)
            {
                Transform camTf = mainCam.transform;
                Quaternion camStartRot = camTf.rotation;
                Vector3 toStoneDir = (stoneGroundPos - camTf.position);
                Quaternion camLookStoneRot = toStoneDir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(toStoneDir.normalized, Vector3.up)
                    : camStartRot;

                const float pickupDuration = 0.58f;
                float elapsed = 0f;
                while (elapsed < pickupDuration)
                {
                    elapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(elapsed / pickupDuration);
                    float easeOut = 1f - Mathf.Pow(1f - u, 3f);

                    // Слегка опускаем взгляд на камень в начале, затем выравниваемся
                    float lookBlend = Mathf.Sin(u * Mathf.PI) * 0.45f;
                    camTf.rotation = Quaternion.Slerp(camStartRot, camLookStoneRot, lookBlend);

                    Vector3 handPos = camTf.position + camTf.forward * 0.52f + camTf.right * 0.24f - camTf.up * 0.16f;
                    Vector3 arcLift = Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.14f);
                    transform.position = Vector3.Lerp(stoneGroundPos, handPos, easeOut) + arcLift;
                    transform.rotation = Quaternion.Slerp(stoneGroundRot, camTf.rotation * Quaternion.Euler(18f, -25f, 12f), easeOut);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                // Шаг 1Б: Игрок поднимает взгляд вверх и в сторону озера, замахиваясь камнем
                Quaternion camAfterPickupRot = camTf.rotation;
                Vector3 flatToLake = stoneWaterEntry - camTf.position;
                flatToLake.y = 0f;
                if (flatToLake.sqrMagnitude < 0.001f) flatToLake = camTf.forward;
                Quaternion lakeYawRot = Quaternion.LookRotation(flatToLake.normalized, Vector3.up);
                Quaternion lookUpSkyRot = lakeYawRot * Quaternion.Euler(-28f, 0f, 0f);

                const float lookUpDuration = 0.82f;
                elapsed = 0f;
                while (elapsed < lookUpDuration)
                {
                    elapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(elapsed / lookUpDuration);
                    float smooth = -(Mathf.Cos(Mathf.PI * u) - 1f) * 0.5f;

                    camTf.rotation = Quaternion.Slerp(camAfterPickupRot, lookUpSkyRot, smooth);

                    // Плавный замах рукой назад и вверх во второй половине поднятия взгляда
                    float windUp = Mathf.Clamp01((u - 0.35f) / 0.65f);
                    float windUpSmooth = windUp * windUp * (3f - 2f * windUp);
                    Vector3 holdOffset = camTf.forward * (0.52f - 0.16f * windUpSmooth)
                                       + camTf.right * (0.24f + 0.08f * windUpSmooth)
                                       + camTf.up * (-0.16f + 0.22f * windUpSmooth);
                    transform.position = camTf.position + holdOffset;
                    transform.rotation = camTf.rotation * Quaternion.Euler(18f - 26f * windUpSmooth, -25f, 12f);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                // Шаг 1В: Короткий резкий бросок рукой вперёд с лёгкой отдачей камеры
                Vector3 releaseTargetPos = camTf.position + camTf.forward * 0.68f + camTf.right * 0.14f + camTf.up * 0.04f;
                Vector3 windUpPos = transform.position;
                Quaternion throwPitchRot = lookUpSkyRot * Quaternion.Euler(7f, 0f, -1.2f);
                const float snapDuration = 0.13f;
                elapsed = 0f;
                while (elapsed < snapDuration)
                {
                    elapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(elapsed / snapDuration);
                    float ease = u * u;
                    camTf.rotation = Quaternion.Slerp(lookUpSkyRot, throwPitchRot, ease);
                    transform.position = Vector3.Lerp(windUpPos, releaseTargetPos, ease);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            // Шаг 2: Камень вылетает из рук и летит по параболе в озеро, камера провожает его взглядом
            Vector3 stoneStart = transform.position;
            const float throwDuration = 0.95f;
            const float arcHeight = 5.6f;
            float flyElapsed = 0f;
            while (flyElapsed < throwDuration)
            {
                flyElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(flyElapsed / throwDuration);
                Vector3 pos = Vector3.Lerp(stoneStart, stoneWaterEntry, u);
                pos.y += 4f * arcHeight * u * (1f - u);
                transform.position = pos;
                transform.Rotate(new Vector3(360f, 210f, 150f) * Time.deltaTime, Space.Self);

                if (mainCam != null)
                {
                    Vector3 lookDir = pos - mainCam.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetLook = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                        mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, targetLook, 1f - Mathf.Exp(-8f * Time.deltaTime));
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            transform.position = stoneWaterEntry;

            // Шаг 3: Камень уходит под воду и медленно тонет
            Vector3 stoneSinkEnd = new Vector3(stoneWaterEntry.x, waterY - 4.2f, stoneWaterEntry.z);
            const float sinkDuration = 0.75f;
            float sinkElapsed = 0f;
            while (sinkElapsed < sinkDuration)
            {
                sinkElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(sinkElapsed / sinkDuration);
                float ease = u * u;
                transform.position = Vector3.Lerp(stoneWaterEntry, stoneSinkEnd, ease);
                transform.Rotate(new Vector3(70f, 40f, 30f) * Time.deltaTime, Space.Self);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            MeshRenderer stoneRend = GetComponent<MeshRenderer>();
            if (stoneRend != null) stoneRend.enabled = false;

            // Короткая тревожная пауза после того, как камень утонул
            await UniTask.Delay(TimeSpan.FromSeconds(0.45f), cancellationToken: token);

            // Возвращаем управление CinemachineBrain и переключаемся на камеру выхода Саи из озера
            if (brain != null)
            {
                brain.enabled = wasBrainEnabled;
            }
            if (camSaya != null)
            {
                camSaya.Priority = 110;
                camSaya.enabled = true;
            }

            if (_saya != null)
            {
                EnsureSayaScaleAndCollision(_saya, isSitting: false);
                EnsureArmatureGrounded(_saya.transform);

                Vector2 riverStartXZ = new Vector2(-67.2f, 147.2f);
                Vector2 shoreEndXZ = new Vector2(-53.2f, 133.2f);
                Vector3 walkDir = new Vector3(shoreEndXZ.x - riverStartXZ.x, 0f, shoreEndXZ.y - riverStartXZ.y).normalized;
                Quaternion walkRot = Quaternion.LookRotation(walkDir, Vector3.up);

                float riverBedY = SampleGroundHeight(riverStartXZ.x, riverStartXZ.y, waterY - 3.6f);
                float startY = Mathf.Min(riverBedY, waterY - 2.6f);

                _saya.transform.position = new Vector3(riverStartXZ.x, startY, riverStartXZ.y);
                _saya.transform.rotation = walkRot;
                _saya.SetActive(true);

                if (_sayaPoseRig == null)
                {
                    _sayaPoseRig = _saya.GetComponent<SayaSeatedPoseRig>() ?? _saya.AddComponent<SayaSeatedPoseRig>();
                }
                _sayaPoseRig.Initialize(_player);
                _sayaPoseRig.StartWalking();
                _sayaPoseRig.SetWalkPlaybackSpeed(0.95f);

                const float emergeWalkDuration = 3.4f;
                float emergeElapsed = 0f;
                while (emergeElapsed < emergeWalkDuration)
                {
                    emergeElapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(emergeElapsed / emergeWalkDuration);
                    float x = Mathf.Lerp(riverStartXZ.x, shoreEndXZ.x, u);
                    float z = Mathf.Lerp(riverStartXZ.y, shoreEndXZ.y, u);

                    float groundY = SampleGroundHeight(x, z, _saya.transform.position.y);
                    // В самом начале Сая скрыта под водой и плавно выходит по дну на берег — строго по рельефу, не вылетая
                    float y = u < 0.18f
                        ? Mathf.Lerp(startY, groundY, Mathf.SmoothStep(0f, 1f, u / 0.18f))
                        : groundY;

                    _saya.transform.position = new Vector3(x, y, z);
                    _saya.transform.rotation = walkRot;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                _sayaPoseRig.SetStandingIdle(true);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
            }

            // Шаг 4: Плавное затемнение, переключение на следующий этап ("Найти Саю" у верёвки) и возврат камеры игроку
            Image blackImg = EnsureBlinkOverlayReady(0f);
            if (blackImg != null)
            {
                blackImg.gameObject.SetActive(true);
                Canvas parentCanvas = blackImg.GetComponentInParent<Canvas>(true);
                if (parentCanvas != null) parentCanvas.enabled = true;
                await Tween.Alpha(blackImg, 1.0f, 0.45f, Ease.InQuad).ToUniTask(cancellationToken: token);
            }

            if (camStone != null) { camStone.Priority = 0; camStone.enabled = false; }
            if (camSaya != null) { camSaya.Priority = 0; camSaya.enabled = false; }

            OnCutsceneEnd(null);

            if (blackImg != null)
            {
                blackImg.color = new Color(0f, 0f, 0f, 1f);
                blackImg.gameObject.SetActive(true);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
                await Tween.Alpha(blackImg, 0.0f, 0.45f, Ease.OutQuad).ToUniTask(cancellationToken: token);
                blackImg.gameObject.SetActive(false);
            }
        }
        catch (OperationCanceledException)
        {
            if (camStone != null) { camStone.Priority = 0; camStone.enabled = false; }
            if (camSaya != null) { camSaya.Priority = 0; camSaya.enabled = false; }
        }
        finally
        {
            if (brain != null)
            {
                brain.enabled = wasBrainEnabled;
            }
            controlLock?.Dispose();
        }
    }

    private void OnCutsceneEnd(PlayableDirector director)
    {
        if (_stage != QuestStage.PlayingLakeCutscene && _stage != QuestStage.WaitingForStoneApproach)
            return;

        SetCutsceneCamerasEnabled(false);
        _stage = QuestStage.WaitingForSayaRopedApproach;

        // Убираем чёрный экран после Timeline кат-сцены озера
        EnsureBlinkOverlayReady(0f);

        // Появляется Сая на верёвке
        if (_sayaRoped != null)
        {
            _sayaRoped.transform.localScale = Vector3.one * SayaScaleMultiplier;
            _sayaRoped.transform.position = SayaRopedWorldPos;
            _sayaRoped.SetActive(true);
        }

        // Подготавливаем наземную Саю под верёвкой (скрыта до моргания)
        if (_saya != null)
        {
            EnsureSayaScaleAndCollision(_saya, isSitting: false);
            float groundY = SampleGroundHeight(SayaGroundXZ.x, SayaGroundXZ.y, 3.2f);
            _saya.transform.position = new Vector3(SayaGroundXZ.x, groundY, SayaGroundXZ.y);
            _saya.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _saya.SetActive(false);
        }

        if (_textRen != null)
        {
            _textRen.SayaText();
        }

        if (_lineToSaya != null)
        {
            if (_sayaRoped != null)
            {
                _lineToSaya.SetTarget(_sayaRoped, 16f);
            }
            _lineToSaya.ActivateMarker(1f).Forget();
        }

        // На этапе жёлтой стрелки до Саи на верёвке красные боковые стрелки не показываем
        if (PointerInmpulse.Instance != null)
        {
            PointerInmpulse.Instance.DisableAllPointers();
        }
    }

    private async UniTaskVoid RunBlinkAndDialogueSequence()
    {
        if (PointerInmpulse.Instance != null)
            PointerInmpulse.Instance.DisableAllPointers();

        if (_lineToSaya != null)
            _lineToSaya.DeactivateImmediate();

        if (_textRen != null)
            _textRen.HideObjective(0.25f);

        var horrorFx = UnityEngine.Object.FindAnyObjectByType<HorrorPostProcess.HorrorShaderController>();
        if (horrorFx != null)
        {
            horrorFx.SetFearLevelImmediate(0.78f);
            horrorFx.SetFearLevel(0f);
        }

        Image blinkImg = EnsureBlinkOverlayReady(0f);

        // Моргание героя при подходе к висящей Сае -> 1 секунда чёрного экрана
        if (blinkImg != null)
        {
            blinkImg.gameObject.SetActive(true);
            Canvas parentCanvas = blinkImg.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null) parentCanvas.enabled = true;

            // Первый быстрый шок-морг (красный отсвет + полузакрытие век)
            blinkImg.color = new Color(0.45f, 0.02f, 0.02f, 0f);
            await Tween.Alpha(blinkImg, 0.72f, 0.14f, Ease.OutQuad).ToUniTask();
            await Tween.Alpha(blinkImg, 0.18f, 0.12f, Ease.InOutSine).ToUniTask();

            // Полное закрытие глаз в чёрный экран ровно на 1 секунду
            blinkImg.color = new Color(0f, 0f, 0f, 0.18f);
            await Tween.Alpha(blinkImg, 1.0f, 0.16f, Ease.InQuad).ToUniTask();
        }
        else
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.42f));
        }

        // Пока глаза закрыты (1 секунду чёрный экран): Сая на верёвке исчезает, и Сая появляется стоящей на земле
        if (_sayaRoped != null)
        {
            _sayaRoped.SetActive(false);
        }

        if (_saya != null)
        {
            EnsureSayaScaleAndCollision(_saya, isSitting: false);
            float groundY = SampleGroundHeight(SayaGroundXZ.x, SayaGroundXZ.y, 3.2f);
            Vector3 groundPos = new Vector3(SayaGroundXZ.x, groundY, SayaGroundXZ.y);
            _saya.transform.position = groundPos;

            if (_player != null)
            {
                Vector3 lookDir = _player.position - groundPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    _saya.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }

            EnsureArmatureGrounded(_saya.transform);
            _saya.SetActive(true);

            if (_sayaPoseRig == null)
                _sayaPoseRig = _saya.GetComponent<SayaSeatedPoseRig>() ?? _saya.AddComponent<SayaSeatedPoseRig>();
            _sayaPoseRig.Initialize(_player);
            _sayaPoseRig.SetStandingIdle(true);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(1.0f));

        // Открываем глаза — Сая уже стоит на земле перед игроком
        if (blinkImg != null)
        {
            await Tween.Alpha(blinkImg, 0.0f, 0.45f, Ease.OutQuad).ToUniTask();
            blinkImg.gameObject.SetActive(false);
        }

        // Запускаем диалоговое окно: "Ну что, пошли?"
        await PlaySayaDialogueAsync("Ну что, пошли?");

        // Показываем сверху посередине надпись "Следовать за Саей"
        if (_textRen != null)
        {
            _textRen.ShowFollowSayaObjective("Следовать за Саей");
        }

        // Когда следуем за Саей, жёлтая стрелка выключена — работают только боковые красные стрелки (пока игрок не зайдёт в дом)
        if (_lineToSaya != null)
        {
            _lineToSaya.DeactivateImmediate();
        }

        if (_saya != null)
        {
            _hasEnteredHouse = IsPlayerInsideHouse();
            if (PointerInmpulse.Instance != null)
            {
                if (_hasEnteredHouse)
                    PointerInmpulse.Instance.DisableAllPointers();
                else
                    PointerInmpulse.Instance.SetGuidanceTarget(_saya.transform, enableWorldPointers: false);
            }
        }

        // Сая идёт к дому, открывает дверь и садится на стул
        _stage = QuestStage.SayaWalkingToHouse;
        await WalkSayaToHouseAndSitAsync();
    }

    private async UniTask PlaySayaDialogueAsync(string lineText)
    {
        bool conversationEnded = false;
        ConversationManager.ConversationEndEvent onEnded = () => conversationEnded = true;

        if (ConversationManager.Instance != null && _npcConversation != null)
        {
            ConversationManager.OnConversationEnded += onEnded;
            try
            {
                // Запускаем один раз: _npcConversation уже содержит имя "Сая" и фразу "Ну что, пошли?"
                ConversationManager.Instance.StartConversation(_npcConversation);

                float elapsed = 0f;
                const float maxWait = 5.6f;
                while (!conversationEnded && elapsed < maxWait)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed > 0.85f && !ConversationManager.Instance.IsConversationActive)
                    {
                        break;
                    }
                    if (elapsed > 0.65f && WasAdvancePressedThisFrame())
                    {
                        break;
                    }
                    await UniTask.Yield();
                }

                if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
                {
                    ConversationManager.Instance.EndConversation();
                }
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f));
            }
            finally
            {
                ConversationManager.OnConversationEnded -= onEnded;
            }
        }
        else
        {
            _ = lineText;
            await UniTask.Delay(TimeSpan.FromSeconds(2.2f));
        }
    }

    private static bool WasAdvancePressedThisFrame()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                return true;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
        return false;
    }

    private async UniTask WalkSayaToHouseAndSitAsync()
    {
        if (_saya == null) return;

        ResolveReferences();
        EnsureArmatureGrounded(_saya.transform);

        if (_sayaPoseRig != null)
        {
            _sayaPoseRig.StartWalking();
        }

        // Маршрут от дерева (-56.8, 11.81) до крыльца дома, через двойную дверь и к стулу у стола
        List<Vector3> waypoints = new List<Vector3>
        {
            new Vector3(-38.0f, 0f, 16.0f),
            new Vector3(-20.0f, 0f, 12.0f),
            new Vector3(0.0f, 0f, -8.0f),
            new Vector3(25.0f, 0f, -25.0f),
            new Vector3(50.0f, 0f, -38.0f),
            new Vector3(69.98f, 0f, -49.2f),  // Перед входной дверью MainHouse (индекс 5)
            new Vector3(69.98f, 3.04f, -54.5f), // Внутри прихожей сразу за дверью (индекс 6)
            new Vector3(69.98f, 3.05f, -66.5f)  // Внутри зала перед столом (индекс 7)
        };

        // Точка перед стулом с внешней стороны (от стола)
        Vector3 chairSeatWorldPos = new Vector3(71.43f, 3.09f, -70.22f);
        Vector3 tableWorldPos = new Vector3(69.46f, 3.05f, -72.36f);

        if (_targetChair != null)
        {
            chairSeatWorldPos = _targetChair.position;
        }
        if (_diningTable != null)
        {
            tableWorldPos = _diningTable.position;
        }

        Vector3 sitForwardDir = (_targetChair != null)
            ? new Vector3(_targetChair.forward.x, 0f, _targetChair.forward.z)
            : (tableWorldPos - chairSeatWorldPos);
        sitForwardDir.y = 0f;
        if (sitForwardDir.sqrMagnitude < 0.001f) sitForwardDir = Vector3.left;
        sitForwardDir.Normalize();

        Vector3 toTableApprox = tableWorldPos - chairSeatWorldPos;
        toTableApprox.y = 0f;
        if (toTableApprox.sqrMagnitude > 0.001f && Vector3.Dot(sitForwardDir, toTableApprox) < 0f)
        {
            sitForwardDir = -sitForwardDir;
        }
        Vector3 awayFromTableDir = -sitForwardDir;

        Vector3 standBeforeChairPos = chairSeatWorldPos + awayFromTableDir * 1.45f;
        standBeforeChairPos.y = 3.05f;
        waypoints.Add(standBeforeChairPos);

        bool doorsOpened = false;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (_saya == null) return;

            bool isIndoor = i >= 6;
            Vector3 wp = waypoints[i];

            while (_saya != null)
            {
                Vector3 currentPos = _saya.transform.position;
                Vector2 toWp = new Vector2(wp.x - currentPos.x, wp.z - currentPos.z);
                float dist = toWp.magnitude;

                // Когда Сая подходит к входной двери дома — открываем её заранее
                if (!doorsOpened && i >= 5 && dist <= 4.2f)
                {
                    doorsOpened = true;
                    OpenMainHouseEntranceDoors();
                    await UniTask.Delay(TimeSpan.FromSeconds(0.45f));
                }

                if (dist <= 0.25f)
                {
                    break;
                }

                // Адаптивная скорость: если игрок далеко позади, Сая идёт чуть медленнее, чтобы он не потерял её
                float speed = _sayaWalkSpeed;
                if (_player != null && !isIndoor)
                {
                    float playerDist = Vector3.Distance(_player.position, currentPos);
                    if (playerDist > 26f) speed = _sayaWalkSpeed * 0.62f;
                    else if (playerDist < 8f) speed = _sayaWalkSpeed * 1.12f;
                }

                if (_sayaPoseRig != null)
                {
                    _sayaPoseRig.SetWalkPlaybackSpeed(Mathf.Clamp(speed / 6.2f, 0.85f, 1.35f));
                }

                Vector2 dir2D = toWp / dist;
                Vector3 moveDir = new Vector3(dir2D.x, 0f, dir2D.y);

                float step = speed * Time.deltaTime;
                Vector3 nextPos = currentPos + moveDir * Mathf.Min(step, dist);

                if (isIndoor)
                {
                    float indoorFloorY = _houseTransform != null ? _houseTransform.position.y + 0.46f : 3.05f;
                    nextPos.y = Mathf.MoveTowards(currentPos.y, indoorFloorY, Time.deltaTime * 4f);
                }
                else
                {
                    nextPos.y = SampleGroundHeight(nextPos.x, nextPos.z, currentPos.y);
                }

                _saya.transform.position = nextPos;

                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                _saya.transform.rotation = Quaternion.Slerp(_saya.transform.rotation, targetRot, Time.deltaTime * 8.5f);

                await UniTask.Yield();
            }
        }

        // Сая дошла до стула: 1) отодвигаем стул, 2) садимся на него, 3) пододвигаемся вместе со стулом к столу
        if (_saya != null)
        {
            float floorY = _houseTransform != null ? _houseTransform.position.y + 0.46f : 3.05f;
            Quaternion sitRotation = Quaternion.LookRotation(sitForwardDir, Vector3.up);

            // Определяем реальную высоту сиденья по коллайдеру/мешу стула, чтобы Сая не проваливалась в стол/сиденье
            float seatSurfaceY = floorY + 1.80f;
            if (_targetChair != null)
            {
                Bounds chairBounds = new Bounds(_targetChair.position, Vector3.zero);
                bool hasBounds = false;
                foreach (Collider col in _targetChair.GetComponentsInChildren<Collider>(true))
                {
                    if (!hasBounds) { chairBounds = col.bounds; hasBounds = true; }
                    else chairBounds.Encapsulate(col.bounds);
                }
                if (!hasBounds)
                {
                    foreach (Renderer rend in _targetChair.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!hasBounds) { chairBounds = rend.bounds; hasBounds = true; }
                        else chairBounds.Encapsulate(rend.bounds);
                    }
                }
                if (hasBounds)
                {
                    seatSurfaceY = Mathf.Lerp(chairBounds.min.y, chairBounds.max.y, 0.46f);
                }
            }

            CapsuleCollider sayaCollider = EnsureSayaScaleAndCollision(_saya, isSitting: false);
            if (sayaCollider != null) sayaCollider.enabled = false;

            if (_sayaPoseRig != null)
            {
                await _sayaPoseRig.TransitionToSittingAsync(_targetChair, sitRotation, floorY, seatSurfaceY, awayFromTableDir);
            }

            EnsureSayaScaleAndCollision(_saya, isSitting: true);
            if (sayaCollider != null) sayaCollider.enabled = true;
        }

        _stage = QuestStage.SayaSeatedInHouse;
        OnSayaSeated?.Invoke();
    }

    /// <summary>
    /// Мгновенно переводит Саю в сидячее положение за столом при загрузке сохранения.
    /// </summary>
    public void RestoreSayaSeatedStateImmediate()
    {
        if (_stage == QuestStage.SayaSeatedInHouse) return;

        Collider stoneCol = GetComponent<Collider>();
        if (stoneCol != null) stoneCol.enabled = false;
        Animator stoneAnim = GetComponent<Animator>();
        if (stoneAnim != null) stoneAnim.enabled = false;

        if (_lineToSaya != null) _lineToSaya.ClearQuestTarget(0f);
        if (_sayaRoped != null) _sayaRoped.SetActive(false);

        if (_saya != null)
        {
            _saya.SetActive(true);
            float floorY = _houseTransform != null ? _houseTransform.position.y + 0.46f : 3.05f;
            Vector3 sitForwardDir = Vector3.forward;
            if (_targetChair != null && _diningTable != null)
            {
                Vector3 toTable = _diningTable.position - _targetChair.position;
                toTable.y = 0f;
                if (toTable.sqrMagnitude > 0.001f) sitForwardDir = toTable.normalized;
            }
            Vector3 awayFromTableDir = -sitForwardDir;
            float seatSurfaceY = floorY + 1.80f;
            if (_targetChair != null)
            {
                Renderer r = _targetChair.GetComponentInChildren<Renderer>(true);
                if (r != null) seatSurfaceY = Mathf.Lerp(r.bounds.min.y, r.bounds.max.y, 0.46f);
            }

            if (_sayaPoseRig != null)
            {
                _sayaPoseRig.TransitionToSittingAsync(
                    _targetChair,
                    Quaternion.LookRotation(sitForwardDir, Vector3.up),
                    floorY,
                    seatSurfaceY,
                    awayFromTableDir).Forget();
            }

            CapsuleCollider sayaCol = EnsureSayaScaleAndCollision(_saya, isSitting: true);
            if (sayaCol != null) sayaCol.enabled = true;
        }

        _stage = QuestStage.SayaSeatedInHouse;
        OnSayaSeated?.Invoke();
    }

    private static CapsuleCollider EnsureSayaScaleAndCollision(GameObject sayaObj, bool isSitting)
    {
        if (sayaObj == null) return null;

        sayaObj.transform.localScale = Vector3.one * SayaScaleMultiplier;

        CapsuleCollider col = sayaObj.GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = sayaObj.AddComponent<CapsuleCollider>();
        }

        col.isTrigger = false;
        col.direction = 1;
        if (isSitting)
        {
            col.radius = 0.28f;
            col.height = 1.15f;
            col.center = new Vector3(0f, 0.62f, 0.04f);
        }
        else
        {
            col.radius = 0.24f;
            col.height = 1.72f;
            col.center = new Vector3(0f, 0.86f, 0f);
        }
        return col;
    }

    private void OpenMainHouseEntranceDoors()
    {
        OpenSingleHouseDoor(_houseLeftDoor, 88f);
        OpenSingleHouseDoor(_houseRightDoor, -88f);
    }

    private static void OpenSingleHouseDoor(Transform doorTf, float openLocalYaw)
    {
        if (doorTf == null) return;

        Door doorComp = doorTf.GetComponent<Door>();
        if (doorComp != null)
        {
            // Открываем только закрытую дверь (уже открытую не закрываем) и сохраняем возможность взаимодействовать с ней
            if (!doorComp.IsOpen)
            {
                doorComp.Open();
            }
        }
        else
        {
            // Если дверь уже открыта (отклонена от начального закрытого положения 0 более чем на 20 градусов), не трогаем её
            Vector3 euler = doorTf.localEulerAngles;
            bool alreadyOpen = Mathf.Abs(Mathf.DeltaAngle(euler.y, 0f)) > 20f;
            if (!alreadyOpen && Mathf.Abs(Mathf.DeltaAngle(euler.y, openLocalYaw)) > 10f)
            {
                Tween.LocalRotation(doorTf, Quaternion.Euler(euler.x, openLocalYaw, euler.z), 0.65f, Ease.InOutBack);
            }
        }
    }

    private static void EnsureArmatureGrounded(Transform sayaRoot)
    {
        if (sayaRoot == null) return;
        Transform armature = sayaRoot.Find("Armature");
        if (armature != null)
        {
            Vector3 lp = armature.localPosition;
            if (lp.y < -0.5f)
            {
                armature.localPosition = new Vector3(0f, 0.0281f, 0f);
            }
        }
    }

    private float SampleGroundHeight(float x, float z, float fallbackY)
    {
        Vector3 rayOrigin = new Vector3(x, fallbackY + 60f, z);
        int terrainLayerMask = 1 << 6; // Layer 6 = Terrain
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 150f, terrainLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        if (Terrain.activeTerrain != null)
        {
            Vector3 tPos = new Vector3(x, 0f, z);
            return Terrain.activeTerrain.SampleHeight(tPos) + Terrain.activeTerrain.transform.position.y;
        }

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 150f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return fallbackY;
    }

    private Image EnsureBlinkOverlayReady(float initialAlpha)
    {
        if (_blinkOverlayImage == null)
        {
            GameObject blackCanvasGo = GameObject.Find("BlackImageCanvas");
            if (blackCanvasGo != null)
            {
                Transform blackScreenTf = blackCanvasGo.transform.Find("BlackScreen");
                if (blackScreenTf != null)
                {
                    _blinkOverlayImage = blackScreenTf.GetComponent<Image>();
                }
            }

            if (_blinkOverlayImage == null)
            {
                GameObject canvasGo = new GameObject("BlinkCutsceneCanvas", typeof(Canvas), typeof(CanvasScaler));
                Canvas c = canvasGo.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 32766;

                GameObject imgGo = new GameObject("BlinkScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imgGo.transform.SetParent(canvasGo.transform, false);
                RectTransform rt = imgGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                _blinkOverlayImage = imgGo.GetComponent<Image>();
            }
        }

        if (_blinkOverlayImage != null)
        {
            _blinkOverlayImage.raycastTarget = false;
            _blinkOverlayImage.color = new Color(0f, 0f, 0f, initialAlpha);
            _blinkOverlayImage.gameObject.SetActive(initialAlpha > 0.001f);
        }

        return _blinkOverlayImage;
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}

/// <summary>
/// Управляет состояниями анимации Саи (стояние, ходьба по Walk.anim и процедурная посадка на стул за столом).
/// </summary>
public class SayaSeatedPoseRig : MonoBehaviour
{
    private Animator _animator;
    private Transform _player;
    private Transform _armature;

    private Transform _hips;
    private Transform _spine;
    private Transform _chest;
    private Transform _head;
    private Transform _leftUpperLeg;
    private Transform _leftLowerLeg;
    private Transform _leftFoot;
    private Transform _rightUpperLeg;
    private Transform _rightLowerLeg;
    private Transform _rightFoot;
    private Transform _leftShoulder;
    private Transform _leftUpperArm;
    private Transform _leftLowerArm;
    private Transform _leftHand;
    private Transform _rightShoulder;
    private Transform _rightUpperArm;
    private Transform _rightLowerArm;
    private Transform _rightHand;

    private bool _bindPoseCaptured;
    private Quaternion _bindLeftShoulder = Quaternion.identity;
    private Quaternion _bindLeftUpperArm = Quaternion.identity;
    private Quaternion _bindLeftLowerArm = Quaternion.identity;
    private Quaternion _bindLeftHand = Quaternion.identity;
    private Quaternion _bindRightShoulder = Quaternion.identity;
    private Quaternion _bindRightUpperArm = Quaternion.identity;
    private Quaternion _bindRightLowerArm = Quaternion.identity;
    private Quaternion _bindRightHand = Quaternion.identity;

    private Quaternion _sitStartLeftShoulder = Quaternion.identity;
    private Quaternion _sitStartLeftUpperArm = Quaternion.identity;
    private Quaternion _sitStartLeftLowerArm = Quaternion.identity;
    private Quaternion _sitStartLeftHand = Quaternion.identity;
    private Quaternion _sitStartRightShoulder = Quaternion.identity;
    private Quaternion _sitStartRightUpperArm = Quaternion.identity;
    private Quaternion _sitStartRightLowerArm = Quaternion.identity;
    private Quaternion _sitStartRightHand = Quaternion.identity;

    private bool _isSitting;
    private float _sitBlend;

    public void Initialize(Transform player)
    {
        _player = player;
        _animator = GetComponent<Animator>();
        _armature = transform.Find("Armature");
        if (_armature != null && _armature.localPosition.y < 0f)
        {
            _armature.localPosition = new Vector3(0f, 0.0281f, 0f);
        }

        _hips = FindBone("J_Bip_C_Hips");
        _spine = FindBone("J_Bip_C_Spine");
        _chest = FindBone("J_Bip_C_Chest");
        _head = FindBone("J_Bip_C_Head");
        _leftUpperLeg = FindBone("J_Bip_L_UpperLeg");
        _leftLowerLeg = FindBone("J_Bip_L_LowerLeg");
        _leftFoot = FindBone("J_Bip_L_Foot");
        _rightUpperLeg = FindBone("J_Bip_R_UpperLeg");
        _rightLowerLeg = FindBone("J_Bip_R_LowerLeg");
        _rightFoot = FindBone("J_Bip_R_Foot");
        _leftShoulder = FindBone("J_Bip_L_Shoulder");
        _leftUpperArm = FindBone("J_Bip_L_UpperArm");
        _leftLowerArm = FindBone("J_Bip_L_LowerArm");
        _leftHand = FindBone("J_Bip_L_Hand");
        _rightShoulder = FindBone("J_Bip_R_Shoulder");
        _rightUpperArm = FindBone("J_Bip_R_UpperArm");
        _rightLowerArm = FindBone("J_Bip_R_LowerArm");
        _rightHand = FindBone("J_Bip_R_Hand");

        if (!_bindPoseCaptured)
        {
            _bindPoseCaptured = true;
            if (_leftShoulder != null) _bindLeftShoulder = _leftShoulder.localRotation;
            if (_leftUpperArm != null) _bindLeftUpperArm = _leftUpperArm.localRotation;
            if (_leftLowerArm != null) _bindLeftLowerArm = _leftLowerArm.localRotation;
            if (_leftHand != null) _bindLeftHand = _leftHand.localRotation;
            if (_rightShoulder != null) _bindRightShoulder = _rightShoulder.localRotation;
            if (_rightUpperArm != null) _bindRightUpperArm = _rightUpperArm.localRotation;
            if (_rightLowerArm != null) _bindRightLowerArm = _rightLowerArm.localRotation;
            if (_rightHand != null) _bindRightHand = _rightHand.localRotation;
        }
    }

    public void SetStandingIdle(bool idle)
    {
        _isSitting = false;
        _sitBlend = 0f;
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.Play("Armature_ArmatureAction", 0, 0f);
            _animator.Update(0f);
            _animator.speed = idle ? 0f : 1f;
        }
    }

    public void StartWalking()
    {
        _isSitting = false;
        _sitBlend = 0f;
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            _animator.enabled = true;
            _animator.speed = 1.15f;
            _animator.Play("Armature_ArmatureAction", 0, 0f);
        }
    }

    public void SetWalkPlaybackSpeed(float speed)
    {
        if (_animator != null && !_isSitting)
        {
            _animator.speed = speed;
        }
    }

    public async UniTask TransitionToSittingAsync(Transform chairTf, Quaternion sitRot, float floorY, float seatSurfaceY, Vector3 awayFromTableDir)
    {
        // Снимаем высоту таза до отключения аниматора (масштаб уже учтён через мировую позицию _hips)
        float seatedRootY = floorY;
        if (_hips != null)
        {
            float standingHipsOffset = Mathf.Max(0.70f, _hips.position.y - transform.position.y);
            float seatedHipsOffset = standingHipsOffset * (0.0235f / 0.0277f);
            float desiredHipsWorldY = seatSurfaceY + 0.25f;
            seatedRootY = desiredHipsWorldY - seatedHipsOffset;
        }

        if (_animator != null)
        {
            _animator.speed = 0f;
            _animator.enabled = false;
        }

        Vector3 dirBack = awayFromTableDir.sqrMagnitude > 0.001f ? awayFromTableDir.normalized : Vector3.right;
        Vector3 dirForward = -dirBack;

        Vector3 chairStartPos = chairTf != null ? chairTf.position : new Vector3(transform.position.x, seatSurfaceY, transform.position.z);
        Vector3 chairPulledPos = chairStartPos + dirBack * 1.15f;
        Vector3 chairFinalPos = chairStartPos + dirBack * 0.32f;

        Vector3 startRootPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 standInFrontPos = new Vector3(
            chairPulledPos.x + dirForward.x * 0.16f,
            floorY,
            chairPulledPos.z + dirForward.z * 0.16f);

        // Шаг 1: Сая отодвигает стул от стола и разворачивается к нему
        const float pullDuration = 0.50f;
        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pullDuration));
            transform.position = Vector3.Lerp(startRootPos, standInFrontPos, t);
            transform.rotation = Quaternion.Slerp(startRot, sitRot, t);
            if (chairTf != null)
            {
                chairTf.position = Vector3.Lerp(chairStartPos, chairPulledPos, t);
            }
            await UniTask.Yield();
        }

        // Шаг 2: Сая усаживается на отодвинутый стул (сгибание ног/рук и опускание таза на подушку)
        Vector3 seatedPulledRootPos = new Vector3(
            chairPulledPos.x - dirForward.x * 0.08f,
            seatedRootY,
            chairPulledPos.z - dirForward.z * 0.08f);

        CaptureSitStartArmPose();
        _isSitting = true;
        const float sitDuration = 0.62f;
        elapsed = 0f;
        while (elapsed < sitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sitDuration));
            _sitBlend = t;
            transform.position = Vector3.Lerp(standInFrontPos, seatedPulledRootPos, t);
            transform.rotation = sitRot;
            await UniTask.Yield();
        }
        _sitBlend = 1f;
        transform.position = seatedPulledRootPos;
        transform.rotation = sitRot;

        // Шаг 3: Сая вместе со стулом плавно пододвигается вперёд к обеденному столу
        Vector3 slideDelta = chairFinalPos - chairPulledPos;
        Vector3 seatedFinalRootPos = seatedPulledRootPos + slideDelta;
        const float pushInDuration = 0.55f;
        elapsed = 0f;
        while (elapsed < pushInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pushInDuration));
            transform.position = Vector3.Lerp(seatedPulledRootPos, seatedFinalRootPos, t);
            if (chairTf != null)
            {
                chairTf.position = Vector3.Lerp(chairPulledPos, chairFinalPos, t);
            }
            await UniTask.Yield();
        }

        transform.position = seatedFinalRootPos;
        transform.rotation = sitRot;
        if (chairTf != null) chairTf.position = chairFinalPos;
    }

    private void CaptureSitStartArmPose()
    {
        if (_leftShoulder != null) _sitStartLeftShoulder = _leftShoulder.localRotation;
        if (_leftUpperArm != null) _sitStartLeftUpperArm = _leftUpperArm.localRotation;
        if (_leftLowerArm != null) _sitStartLeftLowerArm = _leftLowerArm.localRotation;
        if (_leftHand != null) _sitStartLeftHand = _leftHand.localRotation;
        if (_rightShoulder != null) _sitStartRightShoulder = _rightShoulder.localRotation;
        if (_rightUpperArm != null) _sitStartRightUpperArm = _rightUpperArm.localRotation;
        if (_rightLowerArm != null) _sitStartRightLowerArm = _rightLowerArm.localRotation;
        if (_rightHand != null) _sitStartRightHand = _rightHand.localRotation;
    }

    private void ApplySeatedArm(
        Transform shoulder,
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Quaternion bindShoulder,
        Quaternion bindUpperArm,
        Quaternion bindLowerArm,
        Quaternion bindHand,
        Quaternion startShoulder,
        Quaternion startUpperArm,
        Quaternion startLowerArm,
        Quaternion startHand,
        bool isLeft,
        float blend)
    {
        if (upperArm == null || lowerArm == null || hand == null) return;

        // Сначала ставим кости в исходный bind-покой (T-pose), чтобы убрать скручивание из Walk.anim
        if (shoulder != null) shoulder.localRotation = bindShoulder;
        upperArm.localRotation = bindUpperArm;
        lowerArm.localRotation = bindLowerArm;
        hand.localRotation = bindHand;

        Vector3 torsoCenter = _chest != null ? _chest.position : transform.position;
        Vector3 outSide = Vector3.ProjectOnPlane(upperArm.position - torsoCenter, transform.up);
        if (outSide.sqrMagnitude < 0.0001f)
            outSide = isLeft ? -transform.right : transform.right;
        else
            outSide.Normalize();

        // Плечо мягко опущено вдоль корпуса и чуть вперёд, без выворота локтя
        Vector3 curUpperDir = (lowerArm.position - upperArm.position).normalized;
        Vector3 targetUpperDir = (-transform.up * 0.88f + transform.forward * 0.36f + outSide * 0.28f).normalized;
        upperArm.rotation = Quaternion.FromToRotation(curUpperDir, targetUpperDir) * upperArm.rotation;
        Quaternion targetUpperLocal = upperArm.localRotation;

        // Предплечье согнуто вперёд и чуть внутрь на колени / к краю стола
        Vector3 curLowerDir = (hand.position - lowerArm.position).normalized;
        Vector3 targetLowerDir = (transform.forward * 0.88f - transform.up * 0.22f - outSide * 0.42f).normalized;
        lowerArm.rotation = Quaternion.FromToRotation(curLowerDir, targetLowerDir) * lowerArm.rotation;
        Quaternion targetLowerLocal = lowerArm.localRotation;

        if (shoulder != null) shoulder.localRotation = Quaternion.Slerp(startShoulder, bindShoulder, blend);
        upperArm.localRotation = Quaternion.Slerp(startUpperArm, targetUpperLocal, blend);
        lowerArm.localRotation = Quaternion.Slerp(startLowerArm, targetLowerLocal, blend);
        hand.localRotation = Quaternion.Slerp(startHand, bindHand, blend);
    }

    private void LateUpdate()
    {
        if (_armature != null && _armature.localPosition.y < 0f)
        {
            _armature.localPosition = new Vector3(0f, 0.0281f, 0f);
        }

        if (!_isSitting || _sitBlend <= 0.001f) return;

        float t = _sitBlend;

        // Положение таза в локальных координатах Armature (scale = 100), высота добирается через targetRootPos.y под сиденье стула
        if (_hips != null)
        {
            _hips.localPosition = Vector3.Lerp(new Vector3(0f, 0.0277f, 0.0001f), new Vector3(0f, 0.0235f, -0.0004f), t);
            _hips.localRotation = Quaternion.Slerp(_hips.localRotation, Quaternion.Euler(-174f, 0f, 0f), t);
        }

        // Бёдра горизонтально вперёд (-86 градусов по X в локальной системе J_Bip_UpperLeg)
        if (_leftUpperLeg != null)
            _leftUpperLeg.localRotation = Quaternion.Slerp(_leftUpperLeg.localRotation, Quaternion.Euler(-86f, -4f, 0f), t);
        if (_rightUpperLeg != null)
            _rightUpperLeg.localRotation = Quaternion.Slerp(_rightUpperLeg.localRotation, Quaternion.Euler(-86f, 4f, 0f), t);

        // Колени согнуты под прямым углом вниз (+86 градусов по X в локальной системе J_Bip_LowerLeg)
        if (_leftLowerLeg != null)
            _leftLowerLeg.localRotation = Quaternion.Slerp(_leftLowerLeg.localRotation, Quaternion.Euler(86f, 0f, 0f), t);
        if (_rightLowerLeg != null)
            _rightLowerLeg.localRotation = Quaternion.Slerp(_rightLowerLeg.localRotation, Quaternion.Euler(86f, 0f, 0f), t);

        // Стопы ровно на полу
        if (_leftFoot != null)
            _leftFoot.localRotation = Quaternion.Slerp(_leftFoot.localRotation, Quaternion.Euler(-90f, 0f, 0f), t);
        if (_rightFoot != null)
            _rightFoot.localRotation = Quaternion.Slerp(_rightFoot.localRotation, Quaternion.Euler(-90f, 0f, 0f), t);

        // Спина прямая с лёгким дыханием
        float breath = Mathf.Sin(Time.time * 1.8f) * 1.5f;
        if (_spine != null)
            _spine.localRotation = Quaternion.Slerp(_spine.localRotation, Quaternion.Euler(-178.5f + breath * 0.4f, 0f, 0f), t);
        if (_chest != null)
            _chest.localRotation = Quaternion.Slerp(_chest.localRotation, Quaternion.Euler(-8f + breath, 0f, 0f), t);

        // Руки анатомически корректно (от bind-позы через мировые оси костей, без вывернутых плеч и локтей)
        ApplySeatedArm(
            _leftShoulder, _leftUpperArm, _leftLowerArm, _leftHand,
            _bindLeftShoulder, _bindLeftUpperArm, _bindLeftLowerArm, _bindLeftHand,
            _sitStartLeftShoulder, _sitStartLeftUpperArm, _sitStartLeftLowerArm, _sitStartLeftHand,
            isLeft: true, blend: t);
        ApplySeatedArm(
            _rightShoulder, _rightUpperArm, _rightLowerArm, _rightHand,
            _bindRightShoulder, _bindRightUpperArm, _bindRightLowerArm, _bindRightHand,
            _sitStartRightShoulder, _sitStartRightUpperArm, _sitStartRightLowerArm, _sitStartRightHand,
            isLeft: false, blend: t);

        // Когда игрок заходит в дом, Сая слегка поворачивает голову в его сторону
        if (_head != null && _player != null && t > 0.9f)
        {
            Vector3 toPlayer = _player.position - transform.position;
            if (toPlayer.sqrMagnitude < 144f)
            {
                Vector3 localDir = transform.InverseTransformDirection(toPlayer.normalized);
                float headYaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg * 0.45f, -32f, 32f);
                _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(-4f, headYaw, 0f), Time.deltaTime * 3.5f);
            }
        }
    }

    private Transform FindBone(string boneName)
    {
        return FindDeep(transform, boneName);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform res = FindDeep(parent.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }
}
