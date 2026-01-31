using Unity.Cinemachine;
using UnityEngine;
using Zenject;
using KinematicCharacterController.Examples;
using System.Collections;

public class ImpulseSource : MonoBehaviour
{
    [Inject] private ExampleCharacterController _exampleCharacter;
    [Inject] private InputMovementController _inputMovementController;

    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField] private float _impulseCD;

    private Rigidbody _rb;

    void Start()
    {
        _rb = _exampleCharacter.GetComponent<Rigidbody>();
        _exampleCharacter.OnMoveStart.AddListener(() => StartCoroutine(Impulse()));
        _exampleCharacter.OnMoveEnd.AddListener(() => StopCoroutine(Impulse()));
    }
    private IEnumerator Impulse()
    {
        while (true)
        {
            _impulseSource.GenerateImpulse((_inputMovementController.PlayerCharacterInputs.MoveAxisForward != 0  || _inputMovementController.PlayerCharacterInputs.MoveAxisRight != 0) ? 1 : 0);
            yield return new WaitForSeconds(_impulseCD);
        }
    }

}
