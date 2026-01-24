using UnityEngine;
using Zenject;
using KinematicCharacterController.Examples;
public class InputMovementController 
{
    [Inject] private ExampleCharacterController _exampleCharacterController;
    [Inject] private ExamplePlayer _examPlayer;
    private PlayerCharacterInputs _playerCharacterInputs;
    [Inject]
    public void Construct()
    {
        _playerCharacterInputs = new PlayerCharacterInputs();
        _playerCharacterInputs.CameraTransform = Camera.main.transform;
        _examPlayer.OnPlayerMoveEvent.AddListener(OnPlayerMove);
        _examPlayer.OnPlayerCrouchEvent.AddListener(OnPlayerCrouch);
        _examPlayer.OnPlayerJumpEvent.AddListener(OnPlayerJump);
    }
    private void OnPlayerJump(ExamplePlayer.PressedStateEventArgs pressedState)
    {
        _playerCharacterInputs.JumpDown = pressedState.State == ExamplePlayer.PressedState.Started;
		_exampleCharacterController.SetInputs(ref _playerCharacterInputs);
	}
    private void OnPlayerMove(ExamplePlayer.Vector2EventArgs vector2EventArgs)
    {
        _playerCharacterInputs.MoveAxisRight = vector2EventArgs.Value.x;
        _playerCharacterInputs.MoveAxisForward = vector2EventArgs.Value.y; 
        _exampleCharacterController.SetInputs(ref _playerCharacterInputs);
    }
    private void OnPlayerCrouch(ExamplePlayer.PressedStateEventArgs pressedState)
    {
        _playerCharacterInputs.CrouchDown = pressedState.State == ExamplePlayer.PressedState.Started;
        _playerCharacterInputs.CrouchUp = pressedState.State == ExamplePlayer.PressedState.Canceled;
        _exampleCharacterController.SetInputs(ref _playerCharacterInputs);
    }
}
