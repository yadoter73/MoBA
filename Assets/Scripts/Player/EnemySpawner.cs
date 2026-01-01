using UnityEngine;
using Zenject;
using PrimeTween;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject objectToSpawn;
    [SerializeField] private float radius = 30f;
    [SerializeField] private Vector2 spawnInterval = new Vector2(30f, 120f);
    [Inject(Id = "PlayerTransform")] private Transform _player;
    [Inject] private IInstantiator _instantiator;

    private Sequence _spawnSequence;

    private void Start()
    {Tween.Delay(10).OnComplete(() => _spawnSequence = Sequence.Create()
            .ChainCallback(SpawnObject));  
    }

    private void OnDestroy()
    {
        _spawnSequence.Stop();
    }

    private void SpawnObject()
    {
        if (_player == null) return;

        Vector2 randomPoint = Random.insideUnitCircle * radius;
        Vector3 spawnPosition = new Vector3(
            _player.position.x + randomPoint.x,
            _player.position.y,
            _player.position.z + randomPoint.y
        );
        _instantiator.InstantiatePrefab(objectToSpawn, spawnPosition, Quaternion.identity, null);

        float randomDelay = Random.Range(spawnInterval.x, spawnInterval.y);
        Debug.Log(randomDelay);
        Tween.Delay(randomDelay).OnComplete(() => SpawnObject());
    }
    private void OnDrawGizmos()
    {
        if (_player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_player.position, radius);
        }
    }
}