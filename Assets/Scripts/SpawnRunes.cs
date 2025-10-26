using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
public class SpawnRunes : MonoBehaviour
{
    [SerializeField] private GameObject[] runesArray;
    
    private float respawnTime = 10f;
    void Start()
    {
        StartCoroutine(RespawnRunesAfterTime());
    }
    public IEnumerator RespawnRunesAfterTime()
    {
        Debug.Log("руна появится через 6 минут");
        yield return new WaitForSeconds(respawnTime);
        Instantiate(runesArray[Random.Range(0, runesArray.Length)], transform.position, Quaternion.identity);
    }

}

