using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System;

namespace KinematicCharacterController.Examples
{
    public class ExamplePlayer : MonoBehaviour
    {
        public UnityEvent<Vector2EventArgs> OnPlayerMoveEvent { get; set; } = new();
        public UnityEvent<PressedStateEventArgs> OnPlayerCrouchEvent { get; set; } = new();
		public UnityEvent<PressedStateEventArgs> OnPlayerJumpEvent { get; set; } = new();
		public UnityEvent<PressedStateEventArgs> OnPlayerInteractEvent { get; set; } = new();
        public UnityEvent<PressedStateEventArgs> OnPlayerPauseEvent { get; set; } = new();
        public void OnPlayerMove(InputAction.CallbackContext ctx)
        {
            Vector2 value = ctx.ReadValue<Vector2>();
            OnPlayerMoveEvent?.Invoke(new Vector2EventArgs(value));
        }
        public void OnCrouch(InputAction.CallbackContext ctx)
        {
            PressedState pressedState = GetPressedState(ctx);
            OnPlayerCrouchEvent?.Invoke(new PressedStateEventArgs(pressedState));
        }
        public void OnJump(InputAction.CallbackContext ctx)
        {
            PressedState pressedState = GetPressedState(ctx);
            OnPlayerJumpEvent?.Invoke(new PressedStateEventArgs(pressedState));
        }
        public void OnInteract(InputAction.CallbackContext ctx)
        {
			PressedState pressedState = GetPressedState(ctx);
			OnPlayerInteractEvent?.Invoke(new PressedStateEventArgs(pressedState));
		}
        public void OnPause(InputAction.CallbackContext ctx)
        {
			PressedState pressedState = GetPressedState(ctx);
			OnPlayerPauseEvent?.Invoke(new PressedStateEventArgs(pressedState));
		}

        private PressedState GetPressedState(InputAction.CallbackContext ctx)
        {
            if (ctx.canceled) return PressedState.Canceled;
            if (ctx.started) return PressedState.Started;
            return PressedState.Performed;
        }

        public enum PressedState
        {
            Started = 1,
            Performed = 2,
            Canceled = 0
        }

      
        public class PressedStateEventArgs : EventArgs
        {
            public PressedState State { get; private set; }
            public PressedStateEventArgs(PressedState state) { State = state; }
        }

        public class Vector2EventArgs : EventArgs
        {
            public Vector2 Value { get; private set; }
            public Vector2EventArgs(Vector2 value) { Value = value; }
        }

        public class TriggerEventArgs : EventArgs
        {
            public float Value { get; private set; }
            public TriggerEventArgs(float value) { Value = value; }
        }

    }
}