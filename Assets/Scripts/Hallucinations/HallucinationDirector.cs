using System.Threading;
using Cysharp.Threading.Tasks;
using MoBA.Player;
using UnityEngine;
using Zenject;

namespace MoBA.Hallucinations
{
    /// <summary>
    /// Координатор механики галлюцинаций:
    /// 1) Случайный таймер 60–120 с (целые секунды) -> ждёт резкого разворота игрока на >= 130° за 0.2–0.5 с -> пробег тёмного монстра перед игроком.
    /// 2) Случайный таймер 120–240 с (целые секунды) -> ждёт, пока игрок подойдёт к зеркалу и посмотрит в него 1 с -> пробег тёмного монстра за спиной игрока (виден в отражении).
    /// </summary>
    public class HallucinationDirector : MonoBehaviour
    {
        [Header("Timers (Seconds)")]
        [SerializeField] private int _sharpTurnMinCooldownSeconds = 60;
        [SerializeField] private int _sharpTurnMaxCooldownSeconds = 120;
        [SerializeField] private int _mirrorMinCooldownSeconds = 120;
        [SerializeField] private int _mirrorMaxCooldownSeconds = 240;
        [SerializeField] private float _sharpTurnCooldownSeconds = 120f;
        [SerializeField] private float _mirrorCooldownSeconds = 240f;

        [Header("Sharp Turn Detection")]
        [SerializeField] private float _sharpTurnAngleDegrees = 130f;
        [SerializeField] private float _sharpTurnMinSeconds = 0.2f;
        [SerializeField] private float _sharpTurnMaxSeconds = 0.5f;

        [Header("Mirror Gaze Detection")]
        [SerializeField] private float _mirrorMaxDistance = 14f;
        [SerializeField] private float _mirrorMaxLookAngleDegrees = 35f;
        [SerializeField] private float _mirrorGazeHoldSeconds = 1.0f;
        [SerializeField] private MirrorReflection[] _mirrors;

        [Header("Runner")]
        [SerializeField] private HallucinationShadowRunner _runner;

        [Header("Audio")]
        [SerializeField] private AudioSource _hallucinationAudioSource;
        [SerializeField] private AudioClip _hallucinationClip;
        [SerializeField] private float _hallucinationAudioVolume = 0.95f;

        [Inject(Id = "HeadTransform", Optional = true)] private Transform _playerHead;
        [Inject(Id = "PlayerTransform", Optional = true)] private Transform _playerTransform;
        [Inject(Id = "Layer", Optional = true)] private LayerMask _groundMask;
        [Inject(Optional = true)] private IPlayerControlLock _playerControlLock;

        private SharpTurnDetector _turnDetector;
        private MirrorGazeDetector _mirrorDetector;

        private float _sharpTurnTimer;
        private float _mirrorTimer;
        private float _mirrorGazeHoldTimer;
        private MirrorReflection _watchedMirror;
        private bool _wasLockedLastFrame;

        public bool IsSharpTurnArmed => _sharpTurnTimer >= _sharpTurnCooldownSeconds;
        public bool IsMirrorArmed => _mirrorTimer >= _mirrorCooldownSeconds;

        private void Awake()
        {
            if (_runner == null)
            {
                _runner = GetComponent<HallucinationShadowRunner>();
            }
            if (_hallucinationAudioSource == null)
            {
                _hallucinationAudioSource = GetComponent<AudioSource>();
                if (_hallucinationAudioSource == null)
                {
                    _hallucinationAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            _hallucinationAudioSource.playOnAwake = false;
            _hallucinationAudioSource.loop = false;
            _hallucinationAudioSource.spatialBlend = 0f;
            if (_mirrorMaxDistance < 8f)
            {
                _mirrorMaxDistance = 14f;
            }

            RollNextSharpTurnCooldown();
            RollNextMirrorCooldown();

            _turnDetector = new SharpTurnDetector(_sharpTurnAngleDegrees, _sharpTurnMinSeconds, _sharpTurnMaxSeconds);
            _mirrorDetector = new MirrorGazeDetector(_mirrors, _mirrorMaxDistance, _mirrorMaxLookAngleDegrees);
        }

        private void Start()
        {
            if (_playerHead == null)
            {
                Debug.LogError("[HallucinationDirector] HeadTransform не внедрён через Zenject.", this);
            }
            if (_runner == null)
            {
                Debug.LogError("[HallucinationDirector] Не назначен HallucinationShadowRunner.", this);
            }
        }

        private void Update()
        {
            if (_playerHead == null || _runner == null) return;

            bool isLocked = _playerControlLock != null && _playerControlLock.IsLocked;
            if (isLocked || Time.timeScale <= 0f)
            {
                if (!_wasLockedLastFrame)
                {
                    _turnDetector.Reset();
                    ResetMirrorGazeHold();
                    _wasLockedLastFrame = true;
                }
                return;
            }

            if (_wasLockedLastFrame)
            {
                _turnDetector.Reset();
                ResetMirrorGazeHold();
                _wasLockedLastFrame = false;
            }

            if (_runner.IsRunning)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (_sharpTurnTimer < _sharpTurnCooldownSeconds)
            {
                _sharpTurnTimer += dt;
            }

            if (_mirrorTimer < _mirrorCooldownSeconds)
            {
                _mirrorTimer += dt;
            }

            // Приоритет 1: зеркало (после 120–240 с игрок должен подойти и смотреть в зеркало 1 с)
            if (IsMirrorArmed && _mirrorDetector.TryFindLookedAtMirror(_playerHead, _playerTransform, out MirrorReflection mirror))
            {
                if (mirror == _watchedMirror)
                {
                    _mirrorGazeHoldTimer += dt;
                }
                else
                {
                    _watchedMirror = mirror;
                    _mirrorGazeHoldTimer = 0f;
                }

                if (_mirrorGazeHoldTimer >= Mathf.Max(0f, _mirrorGazeHoldSeconds))
                {
                    _mirrorTimer = 0f;
                    RollNextMirrorCooldown();
                    ResetMirrorGazeHold();
                    _turnDetector.Reset();
                    TriggerMirrorHallucinationAsync(mirror, this.GetCancellationTokenOnDestroy()).Forget();
                    return;
                }
            }
            else
            {
                ResetMirrorGazeHold();
            }

            // Приоритет 2: резкий разворот на >= 130° за 0.2–0.5 с (после 60–120 с)
            if (IsSharpTurnArmed && _turnDetector.SampleAndCheck(_playerHead.eulerAngles.y, Time.time))
            {
                _sharpTurnTimer = 0f;
                RollNextSharpTurnCooldown();
                TriggerFrontHallucinationAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private void RollNextSharpTurnCooldown()
        {
            int min = Mathf.Min(_sharpTurnMinCooldownSeconds, _sharpTurnMaxCooldownSeconds);
            int max = Mathf.Max(_sharpTurnMinCooldownSeconds, _sharpTurnMaxCooldownSeconds);
            _sharpTurnCooldownSeconds = UnityEngine.Random.Range(min, max + 1);
        }

        private void RollNextMirrorCooldown()
        {
            int min = Mathf.Min(_mirrorMinCooldownSeconds, _mirrorMaxCooldownSeconds);
            int max = Mathf.Max(_mirrorMinCooldownSeconds, _mirrorMaxCooldownSeconds);
            _mirrorCooldownSeconds = UnityEngine.Random.Range(min, max + 1);
        }

        private void ResetMirrorGazeHold()
        {
            _mirrorGazeHoldTimer = 0f;
            _watchedMirror = null;
        }

        /// <summary>
        /// Ручной запуск фронтальной галлюцинации (для отладки из меню редактора).
        /// </summary>
        public void DebugTriggerFrontHallucination()
        {
            if (_playerHead == null || _runner == null || _runner.IsRunning) return;
            TriggerFrontHallucinationAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Ручной запуск зеркальной галлюцинации (для отладки из меню редактора).
        /// Выбирает зеркало, в которое смотрит игрок, либо ближайшее зеркало на сцене.
        /// </summary>
        public void DebugTriggerMirrorHallucination()
        {
            if (_playerHead == null || _runner == null || _runner.IsRunning) return;

            MirrorReflection mirror = null;
            if (_mirrorDetector != null && _mirrorDetector.TryFindLookedAtMirror(_playerHead, _playerTransform, out MirrorReflection lookedAt))
            {
                mirror = lookedAt;
            }
            else if (_mirrors != null && _mirrors.Length > 0)
            {
                float bestSqr = float.MaxValue;
                Vector3 headPos = _playerHead.position;
                for (int i = 0; i < _mirrors.Length; i++)
                {
                    if (_mirrors[i] == null) continue;
                    float sqr = (_mirrors[i].WorldCenter - headPos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        mirror = _mirrors[i];
                    }
                }
            }

            TriggerMirrorHallucinationAsync(mirror, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid TriggerFrontHallucinationAsync(CancellationToken token)
        {
            PlayHallucinationAudio();
            await _runner.RunInFrontOfPlayerAsync(_playerHead, _playerTransform, _groundMask, token);
        }

        private async UniTaskVoid TriggerMirrorHallucinationAsync(MirrorReflection mirror, CancellationToken token)
        {
            PlayHallucinationAudio();
            await _runner.RunBehindPlayerInMirrorAsync(_playerHead, _playerTransform, mirror, _groundMask, token);
        }

        private void PlayHallucinationAudio()
        {
            if (_hallucinationAudioSource == null || _hallucinationClip == null) return;
            if (Time.timeScale <= 0f || PlayerDeathHandler.IsDead) return;

            _hallucinationAudioSource.volume = _hallucinationAudioVolume;
            _hallucinationAudioSource.PlayOneShot(_hallucinationClip, _hallucinationAudioVolume);
        }
    }
}
