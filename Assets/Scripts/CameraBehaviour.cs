using System.Collections;
using UnityEngine;

public class CameraBehaviour : MonoBehaviour
{
    [SerializeField] private float _screenSpeed = 5f;
    [SerializeField] private GameObject alax;
    void Update()
    {
        Vector3 mousePos = Input.mousePosition;

        if (Input.GetKey(KeyCode.Alpha1))
        {
            transform.position = new Vector3(alax.transform.position.x, transform.position.y, alax.transform.position.z + 2);
        }

        if (mousePos.x < 50f)
        {
            transform.Translate(Vector3.left * _screenSpeed * Time.deltaTime);
        }
        else if (mousePos.x > Screen.width - 50f)
        {
            transform.Translate(Vector3.right * _screenSpeed * Time.deltaTime);
        }
        if (mousePos.y > Screen.height - 50f)
        {
            transform.Translate(Vector3.forward * _screenSpeed * Time.deltaTime);
        }
        else if (mousePos.y < 50f)
        {
            transform.Translate(Vector3.back * _screenSpeed * Time.deltaTime);
        }

    }
}
