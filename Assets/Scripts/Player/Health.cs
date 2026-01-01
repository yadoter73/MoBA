using System.Collections;
using UnityEngine;
using Zenject;

public class Health : MonoBehaviour
{
    [SerializeField] private float _currentHp;
    [Inject] private EnemyAI _enemyAI;
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _regenRate = 10f;
    [SerializeField] private GameObject _particles;
    private float lastAttackTime;
    private bool isRegen;
    private GameObject gameobjectParticles;

    private void Start()
    {
        _currentHp = _maxHp;
        lastAttackTime = Time.time;
        StartCoroutine(RegenHp());
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

            if (isRegen)
            {
                if (_currentHp < _maxHp)
                {
                    _currentHp += _regenRate * Time.deltaTime;
                    _currentHp = Mathf.Min(_currentHp, _maxHp);
                }
                else
                {
                    StopRegen();
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    void StopRegen()
    {
        isRegen = false;
        lastAttackTime = Time.time;
        if (_currentHp == _maxHp)
        {
           Destroy(gameobjectParticles, 0.3f);
        }
    }
    public void isAttacked(float damage)
    {
        StopRegen();
        lastAttackTime = Time.time;

        _currentHp -= damage;
  
        if (_currentHp <= 0)
        {
            StopRegen();
            gameObject.SetActive(false);
        }
    }
}
