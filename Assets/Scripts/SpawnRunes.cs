using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
public class SpawnRunes : MonoBehaviour
{
    [SerializeField] private GameObject[] runesArray;
    private ParticleRunes particleRunes;
    private float respawnTime = 10f;
    void Start()
    {
        particleRunes = FindAnyObjectByType<ParticleRunes>();

        StartCoroutine(RespawnRunesAfterTime());
    }
    public IEnumerator RespawnRunesAfterTime()
    {
        yield return new WaitForSeconds(respawnTime);
        particleRunes.State = true;
        yield return new WaitForSeconds(0.2f);
        particleRunes.State = false;
        Instantiate(runesArray[Random.Range(0, runesArray.Length)], transform.position, Quaternion.identity);
        particleRunes.State = false;
    }
  

}

