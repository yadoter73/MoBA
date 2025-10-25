using Unity.VisualScripting;
using UnityEngine;
using System.Collections;

public class HasteRuneLogic : MonoBehaviour
{
    [SerializeField] private SpawnRunes _spawnRunes;
    [SerializeField] private PlayerLogic _PlayerLogic;

    private CapsuleCollider collider;
    private MeshRenderer meshRenderer;

    public GameObject Player;
    void Start()
    {
        _spawnRunes = FindAnyObjectByType<SpawnRunes>();
        _PlayerLogic = FindAnyObjectByType<PlayerLogic>();
        collider = FindAnyObjectByType<CapsuleCollider>();
        meshRenderer = FindAnyObjectByType<MeshRenderer>();
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
        _PlayerLogic.PlayerSpeed = _PlayerLogic.MaxPlayerSpeed;
        StartCoroutine(ReturnPastSpeed());

    }
    public IEnumerator ReturnPastSpeed()
    {
        yield return new WaitForSeconds(25);
        _PlayerLogic.PlayerSpeed = 2.85f;
    }

}
