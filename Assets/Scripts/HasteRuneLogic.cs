using Unity.VisualScripting;
using UnityEngine;
using System.Collections;

public class HasteRuneLogic : MonoBehaviour
{
    [SerializeField] private PlayerLogic _PlayerLogic;
    [SerializeField] private SpawnRunes _spawnRunes;
    public Collider Collider;
    public MeshRenderer MeshRenderer;

    void Start()
    {
        _spawnRunes = FindAnyObjectByType<SpawnRunes>();
        _PlayerLogic = FindAnyObjectByType<PlayerLogic>();
        Collider = GetComponent<CapsuleCollider>();
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
        _PlayerLogic.PlayerSpeed = _PlayerLogic.MaxPlayerSpeed;
        StartCoroutine(ReturnPastSpeed());
    }
    public IEnumerator ReturnPastSpeed()
    {
        yield return new WaitForSeconds(25);
        _PlayerLogic.PlayerSpeed = 2.85f;
    }

}
