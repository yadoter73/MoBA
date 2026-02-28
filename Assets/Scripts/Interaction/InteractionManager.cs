using KinematicCharacterController.Examples;
using System;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Zenject;
public class InteractionManager : MonoBehaviour
{
	[SerializeField] private LayerMask _layerMask;

	[Inject] private ExamplePlayer _examPlayer;
	[Inject(Id = "HeadTransform")] private Transform alax;

	private RaycastHit _hitInfo;
	private float _interactRange = 5f;
	private bool ray { get; set; }

	public UnityEvent<bool> OnInteractebleEvent { get; private set; } = new();

	private void Start()
	{
		_examPlayer.OnPlayerInteractEvent.AddListener(TryToInteract);
	}
	private void FixedUpdate()
	{
		try
		{
			Ray r = new Ray(alax.position, alax.forward);
			RaycastHit prevHit = _hitInfo;
			ray = Physics.Raycast(r, out _hitInfo, _interactRange, _layerMask);
			
			if (prevHit.collider != _hitInfo.collider)
			{
				OnInteractebleEvent?.Invoke(_hitInfo.collider == null);
			}
		}
		catch (NullReferenceException)
		{
			return;
		}

	}
	void TryToInteract(ExamplePlayer.PressedStateEventArgs state)
	{
		if (state.State != ExamplePlayer.PressedState.Started) return;

		if (ray == true && _hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactOBJ))
		{
			interactOBJ.Interact(-1);
		}
	}
}
