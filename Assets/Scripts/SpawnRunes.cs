using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
public class SpawnRunes : MonoBehaviour
{
	[SerializeField] private GameObject[] runesArray;
	[SerializeField] private GameObject _particles;
	private float respawnTime = 10f;
	[SerializeField] GameObject _player;
	void Start()
	{
		StartCoroutine(RespawnRunesAfterTime());
	}
	public IEnumerator RespawnRunesAfterTime()
	{
			yield return new WaitForSeconds(respawnTime);
			Vector3 randomVec = _player.transform.position + new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10));
			Instantiate(runesArray[Random.Range(0, runesArray.Length)], randomVec, Quaternion.identity);
			GameObject gameobject = Instantiate(_particles, randomVec, Quaternion.identity);		
			yield return new WaitForSeconds(0.1f);
			DestroyImmediate(gameobject, true);
	}
}