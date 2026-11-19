using System;
using KinematicCharacterController.Examples;
using UnityEngine;
using Zenject;

namespace MoBA.Player
{
    /// <summary>
    /// Управляет стаминой и ускорением бега игрока через KCC-адаптер (без UI).
    /// Спринт работает только при движении вперёд (W / W+A / W+D):
    /// ровно 6 сек бега (100% -> 0%), 1 сек задержка, 3 сек полное восстановление (0% -> 100%).
    /// </summary>
    public sealed class StaminaService : IStaminaService, IInitializable, ITickable, IDisposable
    {
        private const float DefaultMaxSprintDuration = 6f;
        private const float DefaultRegenDelay = 1f;
        private const float DefaultFullRegenDuration = 3f;
        private const float DefaultExhaustionRecoveryThreshold = 0.35f;
        private const float DefaultForwardInputThreshold = 0.1f;
        private const float DefaultSprintSpeedMultiplier = 1.45f;

        private readonly ExamplePlayer _player;
        private readonly ExampleCharacterController _characterController;
        private readonly IPlayerControlLock _playerControlLock;

        private float _stamina = 1f;
        private float _regenDelayTimer;
        private bool _sprintHeld;
        private bool _isSprinting;
        private bool _isExhausted;
        private bool _mustReleaseSprint;
        private bool _subscribed;

        public float NormalizedStamina => _stamina;
        public bool IsSprinting => _isSprinting;
        public bool IsExhausted => _isExhausted;
        public float MaxSprintDuration => DefaultMaxSprintDuration;
        public float SprintSpeedMultiplier => DefaultSprintSpeedMultiplier;

        public StaminaService(
            ExamplePlayer player,
            ExampleCharacterController characterController,
            [Inject(Optional = true)] IPlayerControlLock playerControlLock)
        {
            _player = player;
            _characterController = characterController;
            _playerControlLock = playerControlLock;
        }

        public void Initialize()
        {
            if (_player == null)
            {
                Debug.LogError("[StaminaService] ExamplePlayer не назначен в Zenject.");
                return;
            }

            if (_characterController == null)
            {
                Debug.LogError("[StaminaService] ExampleCharacterController не назначен в Zenject.");
                return;
            }

            _stamina = 1f;
            _regenDelayTimer = 0f;
            _sprintHeld = false;
            _isSprinting = false;
            _isExhausted = false;
            _mustReleaseSprint = false;
            _characterController.MoveSpeedMultiplier = 1f;

            _player.OnPlayerSprintEvent.AddListener(OnSprintInput);
            _subscribed = true;
        }

        public void Tick()
        {
            if (_characterController == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f || Time.timeScale <= 0f)
            {
                return;
            }

            bool isBlocked = (_playerControlLock != null && _playerControlLock.IsLocked)
                             || !_characterController.enabled
                             || PlayerDeathHandler.IsDead;

            // Спринт разрешён только при движении вперёд (W, W+A, W+D). Чистые A/D/S не ускоряются.
            bool hasForwardInput = _characterController.ForwardMoveInput > DefaultForwardInputThreshold;

            bool canSprint = _sprintHeld
                             && !isBlocked
                             && !_characterController.IsCrouching
                             && hasForwardInput
                             && !_isExhausted
                             && !_mustReleaseSprint
                             && _stamina > 0f;

            if (canSprint)
            {
                // Инвариант: всегда, когда MoveSpeedMultiplier > 1f, стамина гарантированно расходуется.
                _characterController.MoveSpeedMultiplier = DefaultSprintSpeedMultiplier;
                _isSprinting = true;
                _regenDelayTimer = DefaultRegenDelay;
                _stamina = Mathf.Max(0f, _stamina - (dt / DefaultMaxSprintDuration));

                if (_stamina <= 0f)
                {
                    _stamina = 0f;
                    _isExhausted = true;
                    _mustReleaseSprint = true;
                    _isSprinting = false;
                    _characterController.MoveSpeedMultiplier = 1f;
                }
                return;
            }

            _isSprinting = false;
            _characterController.MoveSpeedMultiplier = 1f;

            if (!isBlocked)
            {
                Regenerate(dt);
            }
        }

        public void Dispose()
        {
            if (_subscribed && _player != null)
            {
                _player.OnPlayerSprintEvent.RemoveListener(OnSprintInput);
                _subscribed = false;
            }

            if (_characterController != null)
            {
                _characterController.MoveSpeedMultiplier = 1f;
            }
        }

        private void OnSprintInput(ExamplePlayer.PressedStateEventArgs args)
        {
            if (args == null) return;

            if (args.State == ExamplePlayer.PressedState.Canceled)
            {
                _sprintHeld = false;
                _mustReleaseSprint = false;
            }
            else
            {
                _sprintHeld = true;
            }
        }

        private void Regenerate(float dt)
        {
            if (_stamina >= 1f)
            {
                _stamina = 1f;
                _isExhausted = false;
                return;
            }

            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer = Mathf.Max(0f, _regenDelayTimer - dt);
                return;
            }

            _stamina = Mathf.Min(1f, _stamina + (dt / DefaultFullRegenDuration));
            if (_isExhausted && _stamina >= DefaultExhaustionRecoveryThreshold)
            {
                _isExhausted = false;
            }
        }
    }
}
