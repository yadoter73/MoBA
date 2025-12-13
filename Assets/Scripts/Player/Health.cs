using System.Collections;
using UnityEngine;
using Zenject;

public class Health : MonoBehaviour
{
    [SerializeField] private float _currentHp;
    [SerializeField] private EnemyAI _enemy;
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _regenRate = 5f;

    private float lastAttackTime;
    private bool isRegen;

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
    }
    IEnumerator RegenHp()
    {
        while (true)
        {
            if (!isRegen && Time.time - lastAttackTime >= 15f)
            {
                isRegen = true;
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
            Destroy(gameObject, 1.5f);
        }
    }
}
