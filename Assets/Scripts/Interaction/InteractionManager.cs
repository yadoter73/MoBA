using KinematicCharacterController.Examples;
using MoBA.Player;
using UnityEngine;
using UnityEngine.Events;
using Zenject;
public class InteractionManager : MonoBehaviour
{
	[SerializeField] private LayerMask _layerMask;

	[Inject] private ExamplePlayer _examPlayer;
	[Inject(Id = "HeadTransform")] private Transform alax;
	[Inject(Optional = true)] private IPlayerControlLock _playerControlLock;

	private RaycastHit _hitInfo;
	private float _interactRange = 5f;
	private bool ray { get; set; }

	public UnityEvent<bool> OnInteractebleEvent { get; private set; } = new();

	private void Start()
	{
		if (_examPlayer == null)
		{
			Debug.LogError("[InteractionManager] Зависимость ExamplePlayer не внедрена.", this);
			return;
		}
		_examPlayer.OnPlayerInteractEvent.AddListener(TryToInteract);
	}
	private void FixedUpdate()
	{
		if (alax == null) return;

		if (_playerControlLock != null && _playerControlLock.IsLocked)
		{
			if (ray)
			{
				ray = false;
				_hitInfo = default;
				OnInteractebleEvent?.Invoke(false);
			}
			return;
		}

		Ray r = new Ray(alax.position, alax.forward);
		RaycastHit prevHit = _hitInfo;
		ray = Physics.Raycast(r, out _hitInfo, _interactRange, _layerMask);

		if (_hitInfo.collider != prevHit.collider)
		{
			OnInteractebleEvent?.Invoke(_hitInfo.collider != null);
		}
	}
	void TryToInteract(ExamplePlayer.PressedStateEventArgs state)
	{
		if (state.State != ExamplePlayer.PressedState.Started) return;
		if (_playerControlLock != null && _playerControlLock.IsLocked) return;

		if (ray && _hitInfo.collider != null && _hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactOBJ))
		{
			interactOBJ.Interact(0);
		}
	}
}
