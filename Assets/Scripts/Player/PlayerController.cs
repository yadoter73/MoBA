using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private MovementController _movementController;
    [SerializeField] private JumpController _jumpController;
    [SerializeField] private CrouchController _crouchController;
    private CharacterController _characterController;
    private Vector3 _velocity;
    private bool _isGrounded;

    public Coroutine StepCoroutine { get; set; }
    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        _characterController = GetComponent<CharacterController>();
        _movementController.Initialize(_characterController, this);
        _crouchController.Initialize(_characterController);
    }

    private void Update()
    {
        _isGrounded = _characterController.isGrounded;
        _movementController.Move(_isGrounded);
        _jumpController.HandleJump(ref _velocity, _isGrounded);
        ApplyGravity();
    }

    private void ApplyGravity()
    {
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _jumpController.Gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }
}
