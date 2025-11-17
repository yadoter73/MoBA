using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
public class SpawnRunes : MonoBehaviour
{
    [SerializeField] private GameObject[] runesArray;
    [SerializeField] private GameObject _particles;
    [SerializeField] private HasteRuneLogic _hasteRune;
    [SerializeField] private FovScaleLogic _fovLogic;
    [SerializeField] GameObject _player;

    private float respawnTime = 10f;
    private float timeRemaining = 20f;
    private GameObject RunesArray;
    float CurrentTime;
    void Start()
    {
        _hasteRune = FindAnyObjectByType<HasteRuneLogic>();
        _fovLogic = FindAnyObjectByType<FovScaleLogic>();
        StartCoroutine(RespawnRunesAfterTime());
        CurrentTime = timeRemaining;
        timeRemaining = 20 + respawnTime;
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
            Debug.Log(CurrentTime);
        }
        else if (CurrentTime <= 0)
        {
            if(_hasteRune.Collider.enabled == true && _hasteRune.MeshRenderer.enabled == true && _hasteRune != null)
            {
                _hasteRune.Collider.enabled = false;
                _hasteRune.MeshRenderer.enabled = false;
            }
            else if (_fovLogic.Collider.enabled == true && _fovLogic.MeshRenderer.enabled == true && _fovLogic != null)
            {
                _fovLogic.Collider.enabled = false;
                _fovLogic.MeshRenderer.enabled = false;
            }
            Debug.Log("alax");
            CancelInvoke(nameof(UpdateTimer));
        }
    }
}