using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class LineToSaya : MonoBehaviour
{
	[SerializeField] private GameObject _arrow;
	[SerializeField] private GameObject _target;
	[SerializeField] private LayerMask _groundLayer;

	private Tween _fadeTween;

	private float _offsetToGround = 0.2f;
	private float _distanceToplayer = 2f;
	private float _deactivateDistance = 5f;

	private GameObject _arrowInstatiate;
	private bool _active = false;

	[SerializeField] private Material _material;
	[SerializeField] private Color _targetColor;
	[SerializeField] private Color _initColor;
	private void Start()
	{
		if (_arrow != null)
		{
			_arrowInstatiate = Instantiate(_arrow);
			_arrowInstatiate.SetActive(false);
		}
		_initColor = _material.color;
	}
	void FixedUpdate()
	{
		if (!_active || _arrowInstatiate == null || _target == null) return;

		float sqrDistance = (transform.position - _target.transform.position).sqrMagnitude;

		if (sqrDistance < _deactivateDistance * _deactivateDistance)
		{
			_active = false;
			DeactivateLine(1f).Forget();
			return;
		}
		Vector3 direction = (_target.transform.position - transform.position).normalized;
		direction.y = 0;

		Vector3 targetPos = transform.position + direction * _distanceToplayer;

		RaycastHit hit;

		if (Physics.Raycast(new Vector3(targetPos.x, transform.position.y + 100f, targetPos.z), Vector3.down, out hit, 100f * 2f, _groundLayer))
		{
			_arrowInstatiate.transform.position = hit.point + hit.normal * _offsetToGround;
			_arrowInstatiate.transform.rotation = Quaternion.LookRotation(direction, hit.normal);
		}
	}
	public async UniTask ActivateMarker(float duration)
	{
		_active = true;

		_fadeTween.Stop();

		if (_arrowInstatiate == null && _arrow != null)
		{
			_arrowInstatiate = Instantiate(_arrow);
		}
		else if (_arrowInstatiate != null)
		{
			_arrowInstatiate.SetActive(true);
		}

		_fadeTween = Tween.MaterialColor(_material, _initColor, _targetColor, duration: duration, ease: Ease.InOutQuad);
		await _fadeTween.ToUniTask();
	}
	public async UniTask DeactivateLine(float duration)
	{
		_active = false;

		_fadeTween.Stop();

		_fadeTween = Tween.MaterialColor(_material, _targetColor, _initColor, duration: duration, ease: Ease.InOutQuad);
		await _fadeTween.ToUniTask();

		_arrowInstatiate.SetActive(false);
	}

}
