using DialogueEditor;
using KinematicCharacterController.Examples;
using MoBA.Interaction;
using MoBA.Narrative;
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
	[SerializeField] private float _interactRange = 5f;
	private readonly RaycastHit[] _obstructionHits = new RaycastHit[16];
	private bool ray { get; set; }
	private InteractionPromptType? _currentPrompt;

	public UnityEvent<bool> OnInteractebleEvent { get; private set; } = new();
	public UnityEvent<InteractionPromptType?> OnInteractionPromptChanged { get; private set; } = new();

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
			if (ray || _currentPrompt.HasValue)
			{
				ray = false;
				_hitInfo = default;
				PublishPromptState(null);
			}
			return;
		}

		Ray r = new Ray(alax.position, alax.forward);
		RaycastHit prevHit = _hitInfo;
		ray = Physics.Raycast(r, out _hitInfo, _interactRange, _layerMask);

		if (ray && IsBlockedByObstacle(r, _hitInfo))
		{
			ray = false;
			_hitInfo = default;
		}

		InteractionPromptType? nextPrompt = ResolvePromptType(_hitInfo.collider);
		if (_hitInfo.collider != prevHit.collider || nextPrompt != _currentPrompt)
		{
			PublishPromptState(nextPrompt);
		}
	}

	private bool IsBlockedByObstacle(Ray rayToTarget, RaycastHit targetHit)
	{
		if (targetHit.collider == null) return false;

		// Препятствия вплотную к цели (рамы, столбы, петли, забор рядом с калиткой) не считаются стеной
		float checkDistance = targetHit.distance - 0.4f;
		if (checkDistance <= 0.01f) return false;

		const int ignoreRaycastMask = ~(1 << 2);
		int count = Physics.RaycastNonAlloc(
			rayToTarget,
			_obstructionHits,
			checkDistance,
			ignoreRaycastMask,
			QueryTriggerInteraction.Ignore);

		if (count <= 0) return false;

		Transform playerRoot = _examPlayer != null ? _examPlayer.transform.root : alax.root;
		Transform targetTransform = targetHit.collider.transform;
		Component interactableComp = targetTransform.GetComponentInParent<IInteractable>() as Component;
		Transform interactableRoot = interactableComp != null ? interactableComp.transform : targetTransform;

		for (int i = 0; i < count; i++)
		{
			Collider col = _obstructionHits[i].collider;
			if (col == null || col == targetHit.collider) continue;

			Transform hitTf = col.transform;
			// Игнорируем коллайдеры самого игрока и дочерние коллайдеры самой цели взаимодействия
			if (playerRoot != null && hitTf.IsChildOf(playerRoot)) continue;
			if (hitTf.IsChildOf(interactableRoot)) continue;

			// Если расстояние до препятствия почти равно расстоянию до цели (< 0.20м разницы),
			// это скользящее касание дверной коробки/прилавка/петель прямо у объекта, а не стена перед ним.
			float distDiff = targetHit.distance - _obstructionHits[i].distance;
			if (distDiff < 0.20f) continue;

			return true;
		}

		return false;
	}

	private void PublishPromptState(InteractionPromptType? nextPrompt)
	{
		_currentPrompt = nextPrompt;
		OnInteractionPromptChanged?.Invoke(nextPrompt);
		OnInteractebleEvent?.Invoke(nextPrompt.HasValue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		if (_hitInfo.collider != null)
		{
			SayaTalkInteractable sayaTalk = _hitInfo.collider.GetComponentInParent<SayaTalkInteractable>();
			if (sayaTalk != null)
			{
				Debug.Log($"[Saya] prompt resolved: {nextPrompt?.ToString() ?? "null"} (CanInteractNow={sayaTalk.CanInteractNow}, hit={_hitInfo.collider.name}, dist={_hitInfo.distance:F2})", sayaTalk);
				return;
			}

			MonsterTalkInteractable monsterTalk = _hitInfo.collider.GetComponentInParent<MonsterTalkInteractable>();
			if (monsterTalk != null)
			{
				Debug.Log($"[Monster] prompt resolved: {nextPrompt?.ToString() ?? "null"} (CanInteractNow={monsterTalk.CanInteractNow}, hit={_hitInfo.collider.name}, dist={_hitInfo.distance:F2})", monsterTalk);
			}
		}
#endif
	}

	private static InteractionPromptType? ResolvePromptType(Collider hitCollider)
	{
		if (hitCollider == null) return null;

		IConditionalInteractionPrompt conditional = hitCollider.GetComponent<IConditionalInteractionPrompt>()
			?? hitCollider.GetComponentInParent<IConditionalInteractionPrompt>();
		if (conditional != null)
		{
			return conditional.CanInteractNow ? conditional.PromptType : (InteractionPromptType?)null;
		}

		IInteractionPrompt promptSource = hitCollider.GetComponent<IInteractionPrompt>()
			?? hitCollider.GetComponentInParent<IInteractionPrompt>();
		if (promptSource != null)
		{
			return promptSource.PromptType;
		}

		if (hitCollider.GetComponent<IInteractable>() != null || hitCollider.GetComponentInParent<IInteractable>() != null)
		{
			return InteractionPromptType.Interact;
		}

		if (hitCollider.GetComponentInParent<NPCConversation>() != null)
		{
			return InteractionPromptType.Talk;
		}

		return null;
	}

	void TryToInteract(ExamplePlayer.PressedStateEventArgs state)
	{
		if (state.State != ExamplePlayer.PressedState.Started) return;
		if (_playerControlLock != null && _playerControlLock.IsLocked) return;

		if (ray && _hitInfo.collider != null)
		{
			IInteractable interactOBJ = _hitInfo.collider.GetComponent<IInteractable>()
				?? _hitInfo.collider.GetComponentInParent<IInteractable>();
			if (interactOBJ != null)
			{
				interactOBJ.Interact(0);
			}
		}
	}
}
