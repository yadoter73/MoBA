using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace KinematicCharacterController.Examples
{
    public class ExamplePlayer : MonoBehaviour
    {
        private PlayerInput player_Input;
        public UnityEvent OnPlayerMoveEvent { get; set; }
        private void Awake()
        {
            player_Input = GetComponent<PlayerInput>();
        }
        public void OnPlayerMovement()
        {

        }
    }
}