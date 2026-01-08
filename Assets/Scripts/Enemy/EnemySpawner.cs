using UnityEngine;
using Zenject;
using PrimeTween;
using System.Collections;
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject objectToSpawn;
    [SerializeField] private float radius = 30f;
    [Inject(Id = "PlayerTransform")] private Transform _player;
    [Inject] DiContainer _container; 

    private void Start()
    {
        Tween.Delay(10).OnComplete(() => StartCoroutine(SpawnEnemies()));
    }
    IEnumerator SpawnEnemies()
    {
        while (true)
        {
            float x = Random.Range(-15, 15);
            float z = Random.Range(-15, 15);

            Vector3 spawnPosition = new Vector3(
                _player.position.x + x,
                _player.position.y,
                _player.position.z + z
            );
            float randomDelay = Random.Range(30, 100);
            Debug.Log($"{randomDelay} randomDelay");
            _container.InstantiatePrefab(objectToSpawn, spawnPosition, Quaternion.identity,null);
            yield return new WaitForSeconds(randomDelay);
        }
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