using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    [SerializeField] private GameObject _camera;
    void Start()
    {
        transform.rotation = Quaternion.Euler(transform.rotation.x, 10, transform.rotation.z);
    }
}
