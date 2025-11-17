using System.Collections;
using UnityEngine;

public class FovScaleLogic : MonoBehaviour
{
	[SerializeField] private SpawnRunes _spawnRunes;
	[SerializeField] private float transitionDuration = 2f;

	public MeshRenderer MeshRenderer;
	public Collider Collider;
	void Start()
	{
		_spawnRunes = FindAnyObjectByType<SpawnRunes>();
		Collider = GetComponent<SphereCollider>();
		MeshRenderer = GetComponent<MeshRenderer>();
	}
	public void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			Collider.enabled = false;
			MeshRenderer.enabled = false;
			TakeRunes();
			_spawnRunes.StartCoroutine(_spawnRunes.RespawnRunesAfterTime());
		}
	}
	void TakeRunes()
	{
		StartCoroutine(ReturnFOV());
	}

	public IEnumerator ReturnFOV()
	{
		float startFOV = 56f;
		float targetFOV = 66f;
		float time = 0f;

		Camera.main.fieldOfView = startFOV;

		while (time < transitionDuration)
		{
			time += Time.deltaTime;
			yield return new WaitForEndOfFrame();
			Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, targetFOV, Time.deltaTime);
		}

		yield return new WaitForSeconds(5);

		while (time >= 0)
		{
			time -= Time.deltaTime;
			yield return new WaitForEndOfFrame();

			Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, startFOV, Time.deltaTime);
		}

	}
}
