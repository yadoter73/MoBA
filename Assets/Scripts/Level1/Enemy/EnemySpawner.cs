using UnityEngine;
using UnityEngine.AI;
using Zenject;

public class EnemySpawner : MonoBehaviour
{
    private const int TargetMonsterCount = 5;

    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private GameObject[] _enemies;
    [Inject] DiContainer _diContainer;
    [Inject(Optional = true)] private ArenaBounds _arenaBounds;
    [Inject(Id = "PlayerTransform", Optional = true)] private Transform _player;

    void Start()
    {
        if (_enemies == null || _enemies.Length == 0) return;

        int spawnCount = Mathf.Max(TargetMonsterCount, _enemies.Length);
        if (_arenaBounds == null) _arenaBounds = FindAnyObjectByType<ArenaBounds>();

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = _enemies[i % _enemies.Length];
            if (prefab == null) continue;

            Vector3 spawnPoint = ResolveSpawnPosition(i);
            _diContainer.InstantiatePrefab(prefab, spawnPoint, Quaternion.identity, null);
        }
    }

    private Vector3 ResolveSpawnPosition(int index)
    {
        if (_spawnPoints != null && index < _spawnPoints.Length && _spawnPoints[index] != null)
        {
            Transform sp = _spawnPoints[index];
            // Если по ошибке в инспекторе привязан Prefab-ассет (не объект сцены), ищем одноимённый объект на сцене
            if (!sp.gameObject.scene.IsValid())
            {
                GameObject sceneObj = GameObject.Find(sp.name);
                if (sceneObj != null) sp = sceneObj.transform;
            }

            if (sp.gameObject.scene.IsValid())
            {
                Vector3 p = sp.position;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 35f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
                return p;
            }
        }

        // Резервная точка внутри ArenaBounds, если в открытой сцене было меньше 4 точек спавна
        Bounds bounds = _arenaBounds != null
            ? _arenaBounds.Bounds
            : new Bounds(transform.position, new Vector3(160f, 30f, 160f));

        Vector3 fallback = new Vector3(22f, bounds.center.y, 148f);
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector3 probe = new Vector3(
                Random.Range(bounds.min.x + 22f, bounds.max.x - 22f),
                bounds.center.y,
                Random.Range(bounds.min.z + 22f, bounds.max.z - 22f)
            );
            if (_player != null && Vector3.Distance(probe, _player.position) < 28f)
                continue;
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 35f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return NavMesh.SamplePosition(fallback, out NavMeshHit fallbackHit, 45f, NavMesh.AllAreas)
            ? fallbackHit.position
            : fallback;
    }
}
