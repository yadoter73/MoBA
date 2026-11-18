using System;
using System.Collections.Generic;
using KinematicCharacterController.Examples;
using Unity.Cinemachine;
using UnityEngine;

namespace MoBA.Player
{
    /// <summary>
    /// Управляет блокировкой движения, вращения камеры, взаимодействий и состояния курсора без Find*.
    /// </summary>
    public sealed class PlayerControlService : IPlayerControlLock, IDisposable
    {
        private sealed class LockHandle : IDisposable
        {
            private readonly PlayerControlService _owner;
            private readonly bool _unlockCursor;
            private bool _disposed;

            public LockHandle(PlayerControlService owner, bool unlockCursor)
            {
                _owner = owner;
                _unlockCursor = unlockCursor;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.ReleaseLock(this);
            }

            public bool UnlocksCursor => _unlockCursor;
        }

        private readonly ExampleCharacterController _characterController;
        private readonly CinemachineInputAxisController _cameraAxisController;
        private readonly List<LockHandle> _activeLocks = new();

        private bool _savedControllerEnabled;
        private bool _savedMotorEnabled;
        private bool _savedAxisEnabled;
        private CursorLockMode _savedCursorLockState;
        private bool _savedCursorVisible;
        private bool _hasSnapshot;

        public bool IsLocked => _activeLocks.Count > 0;
        public bool IsCursorUnlocked
        {
            get
            {
                for (int i = 0; i < _activeLocks.Count; i++)
                {
                    if (_activeLocks[i].UnlocksCursor) return true;
                }
                return false;
            }
        }

        public PlayerControlService(
            ExampleCharacterController characterController,
            CinemachineInputAxisController cameraAxisController)
        {
            _characterController = characterController;
            _cameraAxisController = cameraAxisController;
        }

        public IDisposable AcquireLock(bool unlockCursor = true)
        {
            bool wasLocked = IsLocked;
            var handle = new LockHandle(this, unlockCursor);
            _activeLocks.Add(handle);

            if (!wasLocked)
            {
                ApplyLockState();
            }
            else if (unlockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return handle;
        }

        public void Dispose()
        {
            if (_activeLocks.Count == 0) return;
            _activeLocks.Clear();
            RestoreLockState();
        }

        private void ReleaseLock(LockHandle handle)
        {
            if (!_activeLocks.Remove(handle)) return;

            if (_activeLocks.Count == 0)
            {
                RestoreLockState();
                return;
            }

            if (!IsCursorUnlocked && _hasSnapshot)
            {
                Cursor.lockState = _savedCursorLockState;
                Cursor.visible = _savedCursorVisible;
            }
        }

        private void ApplyLockState()
        {
            _savedCursorLockState = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;

            if (_characterController != null)
            {
                _savedControllerEnabled = _characterController.enabled;
                var zeroInputs = new PlayerCharacterInputs();
                _characterController.SetInputs(ref zeroInputs);

                if (_characterController.Motor != null)
                {
                    _savedMotorEnabled = _characterController.Motor.enabled;
                    _characterController.Motor.BaseVelocity = Vector3.zero;
                    _characterController.Motor.enabled = false;
                }

                _characterController.enabled = false;
            }

            if (_cameraAxisController != null)
            {
                _savedAxisEnabled = _cameraAxisController.enabled;
                _cameraAxisController.enabled = false;
            }

            _hasSnapshot = true;

            if (IsCursorUnlocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RestoreLockState()
        {
            if (!_hasSnapshot) return;
            _hasSnapshot = false;

            if (_characterController != null)
            {
                var zeroInputs = new PlayerCharacterInputs();
                _characterController.SetInputs(ref zeroInputs);
                _characterController.enabled = _savedControllerEnabled;

                if (_characterController.Motor != null)
                {
                    _characterController.Motor.BaseVelocity = Vector3.zero;
                    _characterController.Motor.enabled = _savedMotorEnabled;
                }
            }

            if (_cameraAxisController != null)
            {
                _cameraAxisController.enabled = _savedAxisEnabled;
            }

            Cursor.lockState = _savedCursorLockState;
            Cursor.visible = _savedCursorVisible;
        }
    }
}
