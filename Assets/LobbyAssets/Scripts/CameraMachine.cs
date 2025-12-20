using Unity.VisualScripting;
using UnityEngine;

public class CameraMachine : MonoBehaviour
{
    void Update()
    {
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		transform.position = ray.direction * 1;
	}
}
