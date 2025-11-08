using System.Collections;
using UnityEngine;

public class FovScaleLogic : MonoBehaviour
{
	[SerializeField] private SpawnRunes _spawnRunes;
	[SerializeField] private GameObject _mainCam;

	private MeshRenderer meshRenderer;
    private SphereCollider collider;
	void Start()
    {
		_spawnRunes = FindAnyObjectByType<SpawnRunes>();
		collider = GetComponent<SphereCollider>();
		meshRenderer = GetComponent<MeshRenderer>();
		_mainCam = Camera.main.transform.parent.gameObject;
	}
	public void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			collider.enabled = false;
			meshRenderer.enabled = false;
			_spawnRunes.StartCoroutine(_spawnRunes.RespawnRunesAfterTime());
			TakeRunes();
		}
	}
	void TakeRunes()
	{
		Vector3 camPos = _mainCam.transform.position;
		Vector3 newcamPos = _mainCam.transform.position;

		newcamPos.y += 1;
		StartCoroutine(ReturnFOV(camPos,newcamPos));

		camPos = _mainCam.transform.position;
		newcamPos = _mainCam.transform.position;

		newcamPos.y -= 1;
		StartCoroutine(ReturnFOV(camPos, newcamPos));
	}
		float step = 0.05f;
	public IEnumerator ReturnFOV(Vector3 initpos, Vector3 targetpos)
	{
		_mainCam.transform.position = initpos;
		while(_mainCam.transform.position != targetpos)
		{
			_mainCam.transform.position = Vector3.Lerp(_mainCam.transform.position, targetpos, step);
			yield return new WaitForEndOfFrame();
		}	
	}
}
