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
        [SerializeField] private AudioClip _grassWalkClip;
        [SerializeField] private AudioClip _grassRunClip;
        [SerializeField] private AudioClip _grassJumpClip;

        [Header("Volume")]
        [SerializeField] private float _walkVolume = 0.78f;
        [SerializeField] private float _runVolume = 0.92f;
        [SerializeField] private float _jumpVolume = 0.88f;

        private ExampleCharacterController _characterController;
        private ExamplePlayer _player;
        private IStaminaService _staminaService;
        private IPlayerControlLock _playerControlLock;

        private readonly RaycastHit[] _groundHits = new RaycastHit[4];
        private LocomotionAudioState _currentState = LocomotionAudioState.None;
        private int _floorLayer = -1;
        private float _nextJumpAudioTime;
        private bool _subscribed;

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
            StopAllAudio();
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
                StopAllAudio();
                return;
            }

            bool isGrounded = _characterController.Motor.GroundingStatus.IsStableOnGround;
            bool isMoving = isGrounded &&
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

            // Останавливаем звук шагов/бега перед прыжком
            StopLocomotionAudio();

            if (_grassJumpClip == null || _jumpSource == null) return;
            if (Time.unscaledTime < _nextJumpAudioTime) return;

            _jumpSource.Stop();
            _jumpSource.volume = _jumpVolume;
            _jumpSource.PlayOneShot(_grassJumpClip, _jumpVolume);
            _nextJumpAudioTime = Time.unscaledTime + Mathf.Max(0.25f, _grassJumpClip.length);
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

            if (_floorLayer >= 0 && groundCol.gameObject.layer == _floorLayer)
            {
                return SurfaceType.Floor;
            }

            return IsHouseFloorHierarchy(groundCol.transform) ? SurfaceType.Floor : SurfaceType.Grass;
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
