using UnityEngine;
using Zenject;
using PrimeTween;

public class SpawnerWithTween : MonoBehaviour
{
    [SerializeField] private GameObject objectToSpawn;
    [Inject(Id = "PlayerTransform")] private Transform _player;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float spawnInterval = 20f;

    private void Start()
    {
        StartRepeatingSpawn();
    }
    private void StartRepeatingSpawn()
    {
        Tween.Delay(this, spawnInterval, () =>
        {
            SpawnObject();
            StartRepeatingSpawn();
        });
    } 

    void SpawnObject()
    {
        Vector2 randomPoint = Random.insideUnitCircle * radius;
        Vector3 spawnPosition = new Vector3(
            _player.position.x + randomPoint.x,
            _player.position.y,
            _player.position.z + randomPoint.y
        );
        Instantiate(objectToSpawn, spawnPosition, Quaternion.identity);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_player.position, radius);
    }
}