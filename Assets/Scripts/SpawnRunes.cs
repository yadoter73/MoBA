using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
public class SpawnRunes : MonoBehaviour
{
    [SerializeField] private GameObject[] runesArray;
    [SerializeField] private GameObject _particles;
    [SerializeField] GameObject _player;

    private float respawnTime = 60f;
    private int timeRemaining = 60;
    private GameObject RunesArray;
    float CurrentTime;
    void Start()
    {
        StartCoroutine(RespawnRunesAfterTime());
        CurrentTime = timeRemaining;
    }
    public IEnumerator RespawnRunesAfterTime()
    {
        InvokeRepeating(nameof(UpdateTimer), 0f, 1f);
        yield return new WaitForSeconds(respawnTime);
        Vector3 randomVec = _player.transform.position + new Vector3(Random.Range(-15, 15), 0, Random.Range(-15, 15));
        RunesArray = Instantiate(runesArray[Random.Range(0, runesArray.Length)], randomVec, Quaternion.identity);
        GameObject gameobject = Instantiate(_particles, randomVec, Quaternion.identity);
        yield return new WaitForSeconds(0.1f);
        DestroyImmediate(gameobject, true);
    }
    void UpdateTimer()
    {
        
        if (CurrentTime > 0)
        {
            CurrentTime--;
        }
        else if (CurrentTime <= 0)
        {
            Destroy(RunesArray);
            CancelInvoke(nameof(UpdateTimer));
        }
    }
}