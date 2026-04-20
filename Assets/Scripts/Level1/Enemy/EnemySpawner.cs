using System.ComponentModel;
using UnityEngine;
using Zenject;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private GameObject[] _enemies;
    [Inject] DiContainer _diContainer;
    void Start()
    {
		int count = Mathf.Min(3, _enemies.Length, _spawnPoints.Length);
		for (int i = 0; i < count; i++)
        {
			GameObject prefab = _enemies[i];
            Vector3 spawnPoint = _spawnPoints[i].position;
            _diContainer.InstantiatePrefab(prefab, spawnPoint, Quaternion.identity, null);
	    }
    }

}
