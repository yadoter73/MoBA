using UnityEngine;

public class ParticleRunes : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleEffect;

    public bool State;
    void Start()
    {
        particleEffect = GetComponent<ParticleSystem>();
    }
    void Update()
    {
        if(State == true)
        {
            particleEffect.Play();
        }
        else
        {
            particleEffect.Stop();
        }
    }
}
