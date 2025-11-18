using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
public class SpawnRunes : MonoBehaviour
{
    [SerializeField] private GameObject[] runesArray;
    [SerializeField] private GameObject _particles;
    [SerializeField] GameObject _player;

    private const float respawnTime = 30f;
    private const float InitialDisappearTime = 30f;  
    private float CurrentTime;

    private GameObject RunesArray;
    private GameObject _currentSpawnedRune;
    void Start()
    {
        InvokeRepeating(nameof(UpdateTimer), 0f, 1f);
        StartCoroutine(RespawnRunesAfterTime());
        CurrentTime = InitialDisappearTime;
    }
    public IEnumerator RespawnRunesAfterTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(respawnTime);
            if (_currentSpawnedRune != null)
            {
                Destroy(_currentSpawnedRune);
                _currentSpawnedRune = null;
            }
            Vector3 randomVec = _player.transform.position + new Vector3(Random.Range(-20, 20), 0, Random.Range(-20, 20));
            GameObject gameobjectParticles = Instantiate(_particles, randomVec, Quaternion.identity);
            Destroy(gameobjectParticles, 0.3f);
            if (runesArray.Length > 0)
            {
                _currentSpawnedRune = Instantiate(runesArray[Random.Range(0, runesArray.Length)], randomVec, Quaternion.identity);
            }
            CurrentTime = InitialDisappearTime;
        }
    }
    void UpdateTimer()
    {

        if (CurrentTime > 0)
        {
            CurrentTime--;
            Debug.Log(CurrentTime);
        }
        else if (CurrentTime <= 0)
        {
            if (_currentSpawnedRune != null)
            {
                Destroy(_currentSpawnedRune);
                _currentSpawnedRune = null;
            }
        }
    }
}