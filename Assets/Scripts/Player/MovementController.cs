using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class MovementController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float _airControlMultiplier = 0.5f;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float _stepInterval;
    public float Gravity = -9.81f;

    [Header("Stamina System")]
    //[SerializeField] private UnityEngine.UI.Slider _staminaSlider;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float regenRate = 10f;
    [SerializeField] private float regenDelay = 1.5f;
    [SerializeField] private float staminaCostPerSecond = 20f;

    private CharacterController characterController;
    private PlayerController playerController;
    private Transform _head;
    private float speed;
    private float _lastUseTime;
    private bool _isSprinting;

    [SerializeField] private LayerMask groundLayers;

    public void Initialize(CharacterController characterController, PlayerController playerController)
    {
        this.characterController = characterController;
        this.playerController = playerController;
        _head = Camera.main.transform;
        currentStamina = maxStamina;
        //if (_staminaSlider != null)
        //{
        //    _staminaSlider.maxValue = maxStamina;
        //    _staminaSlider.value = currentStamina;
        //}
        //StartCoroutine(Santa());
    }

    private void UpdateLogic()
    {
        bool attemptingToSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (attemptingToSprint && currentStamina > 1f)
        {
            _isSprinting = true;
            TryUseStamina(staminaCostPerSecond * Time.deltaTime);
            _lastUseTime = Time.time;
        }
        else
        {
            _isSprinting = false;
        }

        if (Time.time > _lastUseTime + regenDelay && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(currentStamina + regenRate * Time.deltaTime, maxStamina);
        }
        //_staminaSlider.value = currentStamina;
    }

    public void Move(bool isGrounded)
    {
        UpdateLogic();

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector3 moveDirection = _head.right * horizontalInput + _head.forward * verticalInput;

        speed = _isSprinting ? runSpeed : walkSpeed;

        if (!isGrounded)
        {
            speed *= _airControlMultiplier;
        }

        characterController.Move(moveDirection * speed * Time.deltaTime);

        bool isMoving = moveDirection.magnitude > 0.8f;

        if (isMoving && playerController.StepCoroutine == null)
        {
            playerController.StepCoroutine = playerController.StartCoroutine(StepCoroutine());
        }
        else if (!isMoving && playerController.StepCoroutine != null)
        {
            playerController.StopCoroutine(playerController.StepCoroutine);
            playerController.StepCoroutine = null;
        }
    }

    private IEnumerator StepCoroutine()
    {
        while (true)
        {
            //PlayStepSound();
            float stepInterval = (speed == runSpeed) ? _stepInterval : _stepInterval * walkSpeed / runSpeed;

            impulseSource.DefaultVelocity.y = (speed == runSpeed) ? 0.15f : 0.15f * walkSpeed / runSpeed;
            impulseSource.ImpulseDefinition.ImpulseDuration = stepInterval;

            yield return new WaitForSeconds(stepInterval);
        }
    }
    //private void PlayStepSound()
    //{
    //    RaycastHit hit;

    //    if (Physics.Raycast(characterController.transform.position, Vector3.down, out hit, 1.5f, groundLayers))
    //    {
    //        string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);

    //        if (layerName == "Wood")
    //        {
    //            PlaySound(sounds[0]);
    //        }
    //        else if (layerName == "Snow")
    //        {
    //            PlaySound(sounds[2]);
    //        }
    //    }
    //}
    public bool TryUseStamina(float amount)
    {
        if (currentStamina > amount)
        {
            currentStamina -= amount;
            return true;
        }
        return false;
    }

    public void Restore(float amount)
    {
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
    }
}