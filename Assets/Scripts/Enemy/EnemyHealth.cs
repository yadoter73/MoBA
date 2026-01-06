using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public int MaxHealth
    {
        get => _maxHealth; 
        set
        {
            _maxHealth = value;
        }
    }

    [SerializeField] private int _maxHealth = 100;
    private int currentHealth;
    public UnityEvent<int> OnTakeDamage{ get; private set; } = new();


    void Start()
    {
        currentHealth = _maxHealth;
    }
    public void TakeDamage(int damageAmount)
    {
        OnTakeDamage?.Invoke(damageAmount);
        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        { 
            
            Destroy(gameObject, 0.3f);
        }
    }
    
}
