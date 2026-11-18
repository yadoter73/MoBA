using UnityEngine;
using UnityEngine.Rendering.Universal;
using PrimeTween;
using DialogueEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;


public class CarCrashAwake : MonoBehaviour
{
	[SerializeField] private NPCConversation _playerConversation;
	[SerializeField] private Image _image;
	[SerializeField] private Image _imageEnd;
	private float duration = 3f;

	private void Awake()
	{
		// Смягчаем слишком плотный чёрный туман на дистанции фар (5..25м), оставляя дальний лес в темноте
		if (RenderSettings.fogDensity > 0.06f)
		{
			RenderSettings.fogDensity = 0.055f;
		}

		EnsureCarHeadlights();
	}

	private static void EnsureCarHeadlights()
	{
		GameObject mainCar = GameObject.Find("MainCar");
		if (mainCar == null) return;

		// Фары привязаны к кузову машины с самого 0-го кадра, едут и поворачиваются вместе с ним
		ConfigureHeadlight(mainCar.transform, "Headlight_Left", new Vector3(-0.62f, 0.08f, 2.28f), new Vector3(9.5f, 2.5f, 0f));
		ConfigureHeadlight(mainCar.transform, "Headlight_Right", new Vector3(0.62f, 0.08f, 2.28f), new Vector3(9.5f, -2.5f, 0f));
	}

	private static void ConfigureHeadlight(Transform carRoot, string lightName, Vector3 localPos, Vector3 localEuler)
	{
		Transform existing = carRoot.Find(lightName);
		GameObject lightGo = existing != null ? existing.gameObject : new GameObject(lightName);
		if (existing == null)
		{
			lightGo.transform.SetParent(carRoot, worldPositionStays: false);
		}

		lightGo.SetActive(true);
		lightGo.transform.localPosition = localPos;
		lightGo.transform.localRotation = Quaternion.Euler(localEuler);
		lightGo.transform.localScale = Vector3.one;

		Light spot = lightGo.GetComponent<Light>();
		if (spot == null) spot = lightGo.AddComponent<Light>();
		spot.enabled = true;
		spot.type = LightType.Spot;
		spot.color = new Color(1f, 0.94f, 0.80f, 1f);
		spot.intensity = 220f;
		spot.range = 28f;
		spot.spotAngle = 64f;
		spot.innerSpotAngle = 28f;
		spot.shadows = LightShadows.None;
		spot.renderMode = LightRenderMode.ForcePixel;

		if (lightGo.GetComponent<UniversalAdditionalLightData>() == null)
		{
			lightGo.AddComponent<UniversalAdditionalLightData>();
		}
	}

	public void Conversation(float delay)
	{
		Tween.Delay(delay).OnComplete(() => ConversationManager.Instance.StartConversation(_playerConversation));
	}
	public void Play(int level)
	{
		UniTask.Delay(TimeSpan.FromSeconds(0.5f)).ContinueWith(() =>
		{
			SceneManager.LoadScene(level);
		}).Forget();
	}
	public void fading()
	{
		_imageEnd.gameObject.SetActive(true);
		Color targetColor = _image.color;
		Tween.MaterialColor(_imageEnd.material,
							targetColor,
							duration,
							Ease.InOutQuad).OnComplete(() =>
											Play(1));
	}
}
