using System.Collections;
using UnityEngine;
using Zenject;

public class Health : MonoBehaviour
{
    [SerializeField] private float _currentHp;
    [SerializeField] private EnemyAI _enemy;
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _regenRate = 5f;
    [SerializeField] private GameObject _particles;
    private float lastAttackTime;
    private bool isRegen;
    private GameObject gameobjectParticles;

    private void Start()
    {
        _enemy = FindAnyObjectByType<EnemyAI>();
        _currentHp = _maxHp;
        lastAttackTime = Time.time;
        StartCoroutine(RegenHp());
        isRegen = true;
    }

    public void isAttacked()
    {
        _currentHp -= _enemy.EnemyDmg;
        lastAttackTime = Time.time;
        isRegen = false;
        Destroy(gameobjectParticles, 0.3f);
    }
    IEnumerator RegenHp()
    {
        while (true)
        {
            if (!isRegen && Time.time - lastAttackTime >= 15f)
            {
                isRegen = true;
                gameobjectParticles = Instantiate(_particles, transform.position, Quaternion.identity);
                gameobjectParticles.transform.parent = transform;
            }

            if (isRegen && _currentHp < _maxHp)
            {
                _currentHp += _regenRate * Time.deltaTime;
                _currentHp = Mathf.Min(_currentHp, _maxHp); 
            }
            yield return null;
        }
    }
    private void FixedUpdate()
    {
        if (_currentHp <= 0)
        {
            Destroy(gameObject, 0.3f);
        }
    }
}
