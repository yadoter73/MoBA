using UnityEngine;

namespace MoBA.Audio
{
    /// <summary>
    /// 3D-звуки врагов:
    /// - Обычные враги (1–4): зацикленный звук бега только в ChaseState;
    /// - Гигантский 5-й враг (DemonFast Variant): зацикленный тяжёлый шаг в патруле и погоне
    ///   + одиночный рёв обнаружения игрока при входе в ChaseState с кулдауном.
    /// </summary>
    public sealed class EnemyAudioController : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool _isGiantMonster;

        [Header("3D Audio Sources")]
        [SerializeField] private AudioSource _moveLoopSource;
        [SerializeField] private AudioSource _spotVoiceSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _moveLoopClip;
        [SerializeField] private AudioClip _spotPlayerClip;

        [Header("Tuning")]
        [SerializeField] private float _moveVolume = 0.90f;
        [SerializeField] private float _spotVolume = 1.0f;
        [SerializeField] private float _patrolPitch = 1.0f;
        [SerializeField] private float _chasePitch = 1.24f;
        [SerializeField] private float _spotCooldown = 4.5f;
        [SerializeField] private float _minDistance = 4.5f;
        [SerializeField] private float _maxDistance = 55f;

        private bool _isMoving;
        private bool _isChasing;
        private float _nextAllowedSpotTime;

        private void Awake()
        {
            EnsureSourcesConfigured();
        }

        private void OnDisable()
        {
            StopAll();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || PlayerDeathHandler.IsDead)
            {
                StopAll();
                return;
            }

            UpdateMovementLoop();
        }

        public void OnMovementStateChanged(bool moving, bool chasing)
        {
            _isMoving = moving;
            _isChasing = chasing;

            if (Time.timeScale <= 0f || PlayerDeathHandler.IsDead)
            {
                StopAll();
                return;
            }

            UpdateMovementLoop();
        }

        public void OnEnterChaseState()
        {
            if (!_isGiantMonster || _spotPlayerClip == null || _spotVoiceSource == null) return;
            if (Time.timeScale <= 0f || PlayerDeathHandler.IsDead) return;
            if (Time.time < _nextAllowedSpotTime || _spotVoiceSource.isPlaying) return;

            _spotVoiceSource.volume = _spotVolume;
            _spotVoiceSource.pitch = 1f;
            _spotVoiceSource.PlayOneShot(_spotPlayerClip, _spotVolume);
            _nextAllowedSpotTime = Time.time + Mathf.Max(_spotCooldown, _spotPlayerClip.length);
        }

        private void UpdateMovementLoop()
        {
            if (_moveLoopSource == null || _moveLoopClip == null || _moveLoopClip.length <= 0f)
            {
                return;
            }

            // У обычных монстров звук играет только в погоне (ChaseState), у гиганта — и в патруле, и в погоне
            bool shouldPlay = _isGiantMonster ? _isMoving : (_isMoving && _isChasing);
            if (!shouldPlay)
            {
                if (_moveLoopSource.isPlaying)
                {
                    _moveLoopSource.Stop();
                }
                return;
            }

            float targetPitch = _isGiantMonster
                ? (_isChasing ? _chasePitch : _patrolPitch)
                : 1.0f;

            _moveLoopSource.pitch = targetPitch;
            _moveLoopSource.volume = _moveVolume;
            _moveLoopSource.loop = true;

            if (_moveLoopSource.clip != _moveLoopClip)
            {
                _moveLoopSource.Stop();
                _moveLoopSource.clip = _moveLoopClip;
            }

            if (!_moveLoopSource.isPlaying)
            {
                _moveLoopSource.Play();
            }
        }

        private void StopAll()
        {
            if (_moveLoopSource != null && _moveLoopSource.isPlaying)
            {
                _moveLoopSource.Stop();
            }
            if (_spotVoiceSource != null && _spotVoiceSource.isPlaying)
            {
                _spotVoiceSource.Stop();
            }
        }

        private void EnsureSourcesConfigured()
        {
            if (_moveLoopSource == null)
            {
                _moveLoopSource = GetComponent<AudioSource>();
                if (_moveLoopSource == null) _moveLoopSource = gameObject.AddComponent<AudioSource>();
            }
            Configure3DSource(_moveLoopSource, loop: true);

            if (_isGiantMonster && _spotVoiceSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                if (sources.Length >= 2)
                {
                    _spotVoiceSource = sources[0] == _moveLoopSource ? sources[1] : sources[0];
                }
                else
                {
                    _spotVoiceSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (_spotVoiceSource != null)
            {
                Configure3DSource(_spotVoiceSource, loop: false);
            }
        }

        private void Configure3DSource(AudioSource src, bool loop)
        {
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = _minDistance;
            src.maxDistance = _maxDistance;
            src.dopplerLevel = 0f;
        }
    }
}
