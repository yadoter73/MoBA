using UnityEngine;

public class PlayerEffect : MonoBehaviour
{
    [SerializeField] private GameObject _particles;
    private PlayerLogic _player;
    void Start()
    {
        _player = GetComponent<PlayerLogic>();
        _player.OnAttack.AddListener(OnAttackEff);
    }
    void OnAttackEff()
    {
        GameObject gameobjectParticles = Instantiate(_particles, transform.position, Quaternion.identity);
        Destroy(gameobjectParticles, 0.3f);
    }
}
