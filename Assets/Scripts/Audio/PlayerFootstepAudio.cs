using System.Collections.Generic;
using KinematicCharacterController.Examples;
using MoBA.Player;
using UnityEngine;
using Zenject;

namespace MoBA.Audio
{
    /// <summary>
    /// Управляет звуками шагов, бега и прыжка игрока с разделением поверхностей Floor / Grass.
    /// Использует один AudioSource для ходьбы и бега, исключая любое наложение дорожек.
    /// </summary>
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        private enum SurfaceType
        {
            Grass = 0,
            Floor = 1
        }

        private enum LocomotionAudioState
        {
            None = 0,
            GrassWalk = 1,
            GrassRun = 2,
            FloorWalk = 3,
            FloorRun = 4
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _locomotionSource;
        [SerializeField] private AudioSource _jumpSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _floorWalkClip;
        [SerializeField] private AudioClip _floorRunClip;
        [SerializeField] private AudioClip _floorJumpClip;
        [SerializeField] private AudioClip _floorLandClip;
        [SerializeField] private AudioClip _grassWalkClip;
        [SerializeField] private AudioClip _grassRunClip;
        [SerializeField] private AudioClip _grassJumpClip;
        [SerializeField] private AudioClip _grassLandClip;

        [Header("Volume")]
        [SerializeField] private float _walkVolume = 0.78f;
        [SerializeField] private float _runVolume = 0.92f;
        [SerializeField] private float _jumpVolume = 0.88f;
        [SerializeField] private float _landVolume = 0.90f;

        [Header("Landing Detection")]
        [SerializeField] private float _minFallHeightForLand = 0.55f;

        private ExampleCharacterController _characterController;
        private ExamplePlayer _player;
        private IStaminaService _staminaService;
        private IPlayerControlLock _playerControlLock;

        private readonly RaycastHit[] _groundHits = new RaycastHit[4];
        private readonly Dictionary<AudioClip, AudioClip> _trimmedClips = new();
        private LocomotionAudioState _currentState = LocomotionAudioState.None;
        private int _floorLayer = -1;
        private float _nextJumpAudioTime;
        private float _nextLandAudioTime;
        private bool _subscribed;
        private bool _wasGroundedLastFrame = true;
        private bool _jumpedSinceGrounded;
        private float _airTime;
        private float _peakAirY;
        private Collider _lastResolvedGroundCol;
        private SurfaceType _lastResolvedSurface;
        private bool _hasLastResolvedSurface;

        [Inject]
        public void Construct(
            ExampleCharacterController characterController,
            ExamplePlayer player,
            [Inject(Optional = true)] IStaminaService staminaService,
            [Inject(Optional = true)] IPlayerControlLock playerControlLock)
        {
            _characterController = characterController;
            _player = player;
            _staminaService = staminaService;
            _playerControlLock = playerControlLock;
        }

        private void Awake()
        {
            _floorLayer = LayerMask.NameToLayer("Floor");
            if (_characterController == null) _characterController = GetComponent<ExampleCharacterController>();
            if (_player == null) _player = GetComponentInParent<ExamplePlayer>();

            EnsureAudioSources();
            PreloadClips();
        }

        private void PreloadClips()
        {
            PreloadClip(_floorJumpClip);
            PreloadClip(_floorLandClip);
            PreloadClip(_grassJumpClip);
            PreloadClip(_grassLandClip);
        }

        private void PreloadClip(AudioClip clip)
        {
            if (clip == null) return;
            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
            GetTrimmedSingleImpactClip(clip);
        }

        private void OnEnable()
        {
            if (_player != null && !_subscribed)
            {
                _player.OnPlayerJumpEvent.AddListener(OnPlayerJump);
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed && _player != null)
            {
                _player.OnPlayerJumpEvent.RemoveListener(OnPlayerJump);
                _subscribed = false;
            }
            _wasGroundedLastFrame = true;
            _jumpedSinceGrounded = false;
            _airTime = 0f;
            _peakAirY = 0f;
            StopAllAudio();
        }

        private void OnDestroy()
        {
            foreach (var kvp in _trimmedClips)
            {
                if (kvp.Value != null && kvp.Value != kvp.Key)
                {
                    Destroy(kvp.Value);
                }
            }
            _trimmedClips.Clear();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f ||
                PlayerDeathHandler.IsDead ||
                (_playerControlLock != null && _playerControlLock.IsLocked) ||
                _characterController == null ||
                !_characterController.enabled ||
                _characterController.Motor == null)
            {
                _wasGroundedLastFrame = true;
                _jumpedSinceGrounded = false;
                _airTime = 0f;
                _peakAirY = 0f;
                StopAllAudio();
                return;
            }

            var groundStatus = _characterController.Motor.GroundingStatus;
            bool isGrounded = groundStatus.IsStableOnGround || groundStatus.FoundAnyGround;
            float currentY = _characterController.transform.position.y;

            if (!isGrounded)
            {
                if (_wasGroundedLastFrame)
                {
                    _peakAirY = currentY;
                }
                else
                {
                    _peakAirY = Mathf.Max(_peakAirY, currentY);
                }
                _airTime += Time.unscaledDeltaTime;
            }
            else
            {
                if (!_wasGroundedLastFrame)
                {
                    float fallHeight = _peakAirY - currentY;
                    bool shouldPlayLand = (_jumpedSinceGrounded && _airTime >= 0.12f) ||
                                          (!_jumpedSinceGrounded && _airTime >= 0.14f && fallHeight >= _minFallHeightForLand);
                    if (shouldPlayLand)
                    {
                        PlayLandingAudio();
                    }
                }

                _jumpedSinceGrounded = false;
                _airTime = 0f;
                _peakAirY = currentY;
            }
            _wasGroundedLastFrame = isGrounded;

            bool isMoving = groundStatus.IsStableOnGround &&
                            _characterController.HasMoveInput &&
                            _characterController.IsMovingHorizontally;

            if (!isMoving)
            {
                StopLocomotionAudio();
                return;
            }

            bool isRunning = (_staminaService != null && _staminaService.IsSprinting) ||
                             _characterController.MoveSpeedMultiplier > 1.05f;

            SurfaceType surface = ResolveSurface();
            LocomotionAudioState targetState = ResolveTargetState(surface, isRunning, out AudioClip targetClip, out float targetVolume);

            if (targetClip == null || targetClip.length <= 0f)
            {
                StopLocomotionAudio();
                return;
            }

            // При смене состояния (Walk <-> Run или Grass <-> Floor) мгновенно глушим предыдущий звук без наложения
            if (_currentState != targetState || _locomotionSource.clip != targetClip)
            {
                _locomotionSource.Stop();
                _currentState = targetState;
                _locomotionSource.clip = targetClip;
                _locomotionSource.volume = targetVolume;
                _locomotionSource.pitch = 1f;
                _locomotionSource.loop = true;
                _locomotionSource.time = 0f;
                _locomotionSource.Play();
                return;
            }

            _locomotionSource.volume = targetVolume;
            _locomotionSource.loop = true;
            if (!_locomotionSource.isPlaying)
            {
                _locomotionSource.time = 0f;
                _locomotionSource.Play();
            }
        }

        private void OnPlayerJump(ExamplePlayer.PressedStateEventArgs args)
        {
            if (args == null || args.State != ExamplePlayer.PressedState.Started) return;
            if (Time.timeScale <= 0f || PlayerDeathHandler.IsDead) return;
            if (_playerControlLock != null && _playerControlLock.IsLocked) return;
            if (_characterController == null || !_characterController.enabled || _characterController.Motor == null) return;
            if (!_characterController.Motor.GroundingStatus.IsStableOnGround) return;

            _jumpedSinceGrounded = true;

            // Останавливаем звук шагов/бега перед прыжком
            StopLocomotionAudio();

            SurfaceType surface = ResolveSurface();
            AudioClip jumpClip = surface == SurfaceType.Floor && _floorJumpClip != null
                ? _floorJumpClip
                : _grassJumpClip;

            if (jumpClip == null || _jumpSource == null) return;
            if (Time.unscaledTime < _nextJumpAudioTime) return;

            _jumpSource.Stop();
            PlayOneShotTrimmed(jumpClip, _jumpVolume);
            _nextJumpAudioTime = Time.unscaledTime + Mathf.Max(0.25f, jumpClip.length);
        }

        private void PlayLandingAudio()
        {
            if (_jumpSource == null) return;
            if (Time.unscaledTime < _nextLandAudioTime) return;

            SurfaceType surface = ResolveSurface();
            AudioClip landClip = surface == SurfaceType.Floor ? _floorLandClip : _grassLandClip;
            if (landClip == null || landClip.length <= 0f) return;

            _jumpSource.Stop();
            PlayOneShotTrimmed(landClip, _landVolume);
            _nextLandAudioTime = Time.unscaledTime + 0.18f;
        }

        private void PlayOneShotTrimmed(AudioClip clip, float volume)
        {
            AudioClip clipToPlay = GetTrimmedSingleImpactClip(clip);
            if (clipToPlay == null) return;

            _jumpSource.clip = clipToPlay;
            _jumpSource.loop = false;
            _jumpSource.volume = volume;
            _jumpSource.pitch = 1f;
            _jumpSource.time = 0f;
            _jumpSource.Play();
        }

        private AudioClip GetTrimmedSingleImpactClip(AudioClip clip)
        {
            if (clip == null) return null;
            if (_trimmedClips.TryGetValue(clip, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    return clip;
                }
            }

            int channels = Mathf.Max(1, clip.channels);
            int freq = clip.frequency;
            int totalFrames = clip.samples;
            if (totalFrames <= 0 || freq <= 0)
            {
                _trimmedClips[clip] = clip;
                return clip;
            }

            float[] data = new float[totalFrames * channels];
            if (!clip.GetData(data, 0))
            {
                _trimmedClips[clip] = clip;
                return clip;
            }

            const float startThreshold = 0.018f;
            const float endSilenceThreshold = 0.012f;

            int startFrame = 0;
            bool foundStart = false;
            for (int frame = 0; frame < totalFrames; frame++)
            {
                int baseIdx = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    if (Mathf.Abs(data[baseIdx + ch]) >= startThreshold)
                    {
                        int preRoll = Mathf.RoundToInt(0.004f * freq);
                        startFrame = Mathf.Max(0, frame - preRoll);
                        foundStart = true;
                        break;
                    }
                }
                if (foundStart) break;
            }

            if (!foundStart)
            {
                _trimmedClips[clip] = clip;
                return clip;
            }

            int minBurstFrames = Mathf.RoundToInt(0.12f * freq);
            int silenceGapFrames = Mathf.RoundToInt(0.14f * freq);
            int maxSingleImpactFrames = Mathf.RoundToInt(0.85f * freq);
            int tailPadFrames = Mathf.RoundToInt(0.03f * freq);

            int lastActiveFrame = startFrame;
            int silentFrames = 0;
            int scanLimit = Mathf.Min(totalFrames, startFrame + maxSingleImpactFrames);

            for (int frame = startFrame; frame < scanLimit; frame++)
            {
                int baseIdx = frame * channels;
                bool active = false;
                for (int ch = 0; ch < channels; ch++)
                {
                    if (Mathf.Abs(data[baseIdx + ch]) >= endSilenceThreshold)
                    {
                        active = true;
                        break;
                    }
                }

                if (active)
                {
                    lastActiveFrame = frame;
                    silentFrames = 0;
                }
                else if (frame - startFrame >= minBurstFrames)
                {
                    silentFrames++;
                    if (silentFrames >= silenceGapFrames)
                    {
                        break;
                    }
                }
            }

            int endFrame = Mathf.Min(totalFrames, lastActiveFrame + tailPadFrames);
            int trimmedFrames = endFrame - startFrame;
            if (trimmedFrames <= 0 || (startFrame == 0 && endFrame >= totalFrames - Mathf.RoundToInt(0.02f * freq)))
            {
                _trimmedClips[clip] = clip;
                return clip;
            }

            float[] trimmedData = new float[trimmedFrames * channels];
            System.Array.Copy(data, startFrame * channels, trimmedData, 0, trimmedData.Length);

            int fadeFrames = Mathf.Min(trimmedFrames, Mathf.RoundToInt(0.025f * freq));
            for (int i = 0; i < fadeFrames; i++)
            {
                int frame = trimmedFrames - fadeFrames + i;
                float gain = 1f - ((i + 1) / (float)fadeFrames);
                int baseIdx = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    trimmedData[baseIdx + ch] *= gain;
                }
            }

            AudioClip trimmedClip = AudioClip.Create($"{clip.name}_Single", trimmedFrames, channels, freq, false);
            trimmedClip.SetData(trimmedData, 0);
            _trimmedClips[clip] = trimmedClip;
            return trimmedClip;
        }

        private SurfaceType ResolveSurface()
        {
            Collider groundCol = _characterController.Motor.GroundingStatus.GroundCollider;
            if (groundCol == null)
            {
                Vector3 origin = _characterController.transform.position + Vector3.up * 0.4f;
                int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 1.6f, ~0, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                for (int i = 0; i < hits; i++)
                {
                    Collider c = _groundHits[i].collider;
                    if (c == null || c.transform.IsChildOf(_characterController.transform)) continue;
                    if (_groundHits[i].distance < bestDist)
                    {
                        bestDist = _groundHits[i].distance;
                        groundCol = c;
                    }
                }
            }

            if (groundCol == null || groundCol is TerrainCollider)
            {
                return SurfaceType.Grass;
            }

            if (_hasLastResolvedSurface && groundCol == _lastResolvedGroundCol)
            {
                return _lastResolvedSurface;
            }

            SurfaceType resolved = (_floorLayer >= 0 && groundCol.gameObject.layer == _floorLayer) ||
                                   IsHouseFloorHierarchy(groundCol.transform)
                ? SurfaceType.Floor
                : SurfaceType.Grass;

            _lastResolvedGroundCol = groundCol;
            _lastResolvedSurface = resolved;
            _hasLastResolvedSurface = true;
            return resolved;
        }

        private static bool IsHouseFloorHierarchy(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                string n = cur.name;
                if (n == "MainHouse" || n == "Shop" || n == "BigHouses" ||
                    n.Contains("Building_Base") || n.Contains("House") || n.Contains("Carpet") || n.Contains("Floor"))
                {
                    return true;
                }
                cur = cur.parent;
            }
            return false;
        }

        private LocomotionAudioState ResolveTargetState(
            SurfaceType surface,
            bool isRunning,
            out AudioClip clip,
            out float volume)
        {
            volume = isRunning ? _runVolume : _walkVolume;
            if (surface == SurfaceType.Floor)
            {
                if (isRunning)
                {
                    clip = _floorRunClip;
                    return LocomotionAudioState.FloorRun;
                }
                clip = _floorWalkClip;
                return LocomotionAudioState.FloorWalk;
            }

            if (isRunning)
            {
                clip = _grassRunClip;
                return LocomotionAudioState.GrassRun;
            }
            clip = _grassWalkClip;
            return LocomotionAudioState.GrassWalk;
        }

        private void StopLocomotionAudio()
        {
            _currentState = LocomotionAudioState.None;
            if (_locomotionSource != null && _locomotionSource.isPlaying)
            {
                _locomotionSource.Stop();
            }
        }

        private void StopAllAudio()
        {
            StopLocomotionAudio();
            if (_jumpSource != null && _jumpSource.isPlaying)
            {
                _jumpSource.Stop();
            }
        }

        private void EnsureAudioSources()
        {
            if (_locomotionSource == null)
            {
                _locomotionSource = gameObject.AddComponent<AudioSource>();
            }
            _locomotionSource.playOnAwake = false;
            _locomotionSource.loop = true;
            _locomotionSource.spatialBlend = 0f;

            if (_jumpSource == null)
            {
                _jumpSource = gameObject.AddComponent<AudioSource>();
            }
            _jumpSource.playOnAwake = false;
            _jumpSource.loop = false;
            _jumpSource.spatialBlend = 0f;
        }
    }
}
