using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;

public class HasteRuneLogic : MonoBehaviour
{
	[SerializeField] private PlayerLogic _PlayerLogic;

	public GameObject Player;
	void Start()
	{
		_PlayerLogic = FindAnyObjectByType<PlayerLogic>();
	}
	public void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			gameObject.SetActive(false);
			_PlayerLogic.StartCoroutine(_PlayerLogic.RespawnRunesAfterTime());
			_PlayerLogic.PlayerSpeed = _PlayerLogic.MaxPlayerSpeed;
			_PlayerLogic.StartCoroutine(_PlayerLogic.ReturnPastSpeed());
		}
	}

}
