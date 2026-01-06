using UnityEngine;

public class EnemyEffects : MonoBehaviour
{
    [SerializeField] private GameObject _particles;
    private EnemyHealth _enemyHealth;
    private void Start()
    {
        _enemyHealth = GetComponent<EnemyHealth>();
        _enemyHealth.OnTakeDamage.AddListener(PlayHitEff);
    }
    void PlayHitEff(int damageAmount)
    {
        GameObject particles = Instantiate(_particles, transform.position, Quaternion.identity);
        particles.transform.localScale = Vector3.one * Mathf.Lerp(1,3, damageAmount / 20);
        Destroy(particles, 0.3f);
    }
}
