using PrimeTween;
using UnityEngine;

namespace MoBA.VFX
{
    /// <summary>
    /// Мягкое тревожное мерцание точечного фонаря через PrimeTween.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [SerializeField] private float _minIntensity = 0.45f;
        [SerializeField] private float _maxIntensity = 2.6f;
        [SerializeField] private float _cycleDuration = 1.4f;

        private Light _light;
        private Sequence _sequence;

        private void OnEnable()
        {
            _light = GetComponent<Light>();
            if (_light == null) return;

            _light.intensity = _maxIntensity;
            _sequence.Stop();
            _sequence = Sequence.Create(cycles: -1, cycleMode: Sequence.SequenceCycleMode.Restart)
                .Chain(Tween.LightIntensity(_light, _minIntensity, _cycleDuration * 0.25f, Ease.InOutSine))
                .Chain(Tween.LightIntensity(_light, _maxIntensity * 0.85f, _cycleDuration * 0.15f, Ease.OutQuad))
                .Chain(Tween.LightIntensity(_light, _minIntensity * 0.6f, _cycleDuration * 0.12f, Ease.InQuad))
                .Chain(Tween.LightIntensity(_light, _maxIntensity, _cycleDuration * 0.48f, Ease.OutSine));
        }

        private void OnDisable()
        {
            _sequence.Stop();
        }
    }
}
