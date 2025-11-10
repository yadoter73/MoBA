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
        particleRunes = GetComponent<ParticleRunes>();

        StartCoroutine(RespawnRunesAfterTime());
    }
    public IEnumerator RespawnRunesAfterTime()
    {
        particleRunes.State = false;
        Debug.Log(particleRunes.State);
        yield return new WaitForSeconds(respawnTime);
        Instantiate(runesArray[Random.Range(0, runesArray.Length)], transform.position, Quaternion.identity);
        particleRunes.State = true;
    }

}

