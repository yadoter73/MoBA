using UnityEngine;
using UnityEngine.UI;

namespace HorrorPostProcess
{
    /// <summary>
    /// Управляет параметрами материала Custom/RetroHorrorDitherUI на RawImage
    /// (хроматическая аберрация, процедурный шум и виньетка) в зависимости от уровня страха.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public class HorrorShaderController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("RawImage, выводящий RenderTexture. Если не задан, берётся с этого же объекта.")]
        [SerializeField] private RawImage _targetRawImage;

        [Tooltip("Необязательный исходный материал (если пусто, используется текущий материал RawImage).")]
        [SerializeField] private Material _overrideMaterial = null;

        [Header("Состояние страха (0 = покой, 1 = паника)")]
        [Range(0f, 1f)]
        [SerializeField] private float _initialFearLevel = 0f;

        [Min(0.01f)]
        [Tooltip("Скорость плавного перехода уровня страха.")]
        [SerializeField] private float _smoothSpeed = 4.0f;

        [Header("Хроматическая аберрация (_AberrationOffset, в текселях)")]
        [SerializeField] private float _calmAberration = 0f;
        [SerializeField] private float _panicAberration = 2.6f;

        [Header("Процедурный шум (_NoiseAmount)")]
        [SerializeField] private float _calmNoise = 0f;
        [SerializeField] private float _panicNoise = 0.045f;

        [Header("Виньетка (_VignetteIntensity)")]
        [SerializeField] private float _calmVignette = 0.18f;
        [SerializeField] private float _panicVignette = 0.78f;

        private static readonly int AberrationOffsetId = Shader.PropertyToID("_AberrationOffset");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");

        private Material _sourceMaterial;
        private Material _runtimeMaterial;
        private float _currentFear;
        private float _targetFear;

        public float CurrentFearLevel => _currentFear;
        public float TargetFearLevel => _targetFear;

        private void Awake()
        {
            if (_targetRawImage == null)
                _targetRawImage = GetComponent<RawImage>();

            InitializeRuntimeMaterial();
            SetFearLevelImmediate(_initialFearLevel);
        }

        private void OnEnable()
        {
            if (_runtimeMaterial != null && _targetRawImage != null && _targetRawImage.material != _runtimeMaterial)
                _targetRawImage.material = _runtimeMaterial;

            ApplyProperties(_currentFear);
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentFear, _targetFear))
                return;

            _currentFear = Mathf.MoveTowards(_currentFear, _targetFear, _smoothSpeed * Time.unscaledDeltaTime);
            ApplyProperties(_currentFear);
        }

        private void OnDestroy()
        {
            if (_targetRawImage != null && _sourceMaterial != null && _targetRawImage.material == _runtimeMaterial)
                _targetRawImage.material = _sourceMaterial;

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        /// <summary>
        /// Плавно изменяет уровень страха/тревоги в рантайме (0..1).
        /// Например: SetFearLevel(Mathf.InverseLerp(maxDist, minDist, distanceToMonster)).
        /// </summary>
        public void SetFearLevel(float t)
        {
            _targetFear = Mathf.Clamp01(t);
        }

        /// <summary>
        /// Мгновенно устанавливает уровень страха (0..1) без сглаживания.
        /// </summary>
        public void SetFearLevelImmediate(float t)
        {
            float clamped = Mathf.Clamp01(t);
            _targetFear = clamped;
            _currentFear = clamped;
            ApplyProperties(clamped);
        }

        private void InitializeRuntimeMaterial()
        {
            if (_targetRawImage == null)
                return;

            _sourceMaterial = _overrideMaterial != null ? _overrideMaterial : _targetRawImage.material;
            if (_sourceMaterial == null)
            {
                Debug.LogWarning($"[{nameof(HorrorShaderController)}] На {name} отсутствует материал для RawImage.", this);
                return;
            }

            _runtimeMaterial = new Material(_sourceMaterial)
            {
                name = $"{_sourceMaterial.name} (Runtime)",
                hideFlags = HideFlags.DontSave
            };

            _targetRawImage.material = _runtimeMaterial;
        }

        private void ApplyProperties(float fear)
        {
            if (_runtimeMaterial == null)
                return;

            // Нелинейная кривая делает нарастание паники более выразительным при приближении угрозы
            float eased = fear * fear * (3f - 2f * fear);

            _runtimeMaterial.SetFloat(AberrationOffsetId, Mathf.Lerp(_calmAberration, _panicAberration, eased));
            _runtimeMaterial.SetFloat(NoiseAmountId, Mathf.Lerp(_calmNoise, _panicNoise, fear));
            _runtimeMaterial.SetFloat(VignetteIntensityId, Mathf.Lerp(_calmVignette, _panicVignette, eased));
        }
    }
}
