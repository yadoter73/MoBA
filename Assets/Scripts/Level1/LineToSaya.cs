using UnityEngine;

public class LineToSaya : MonoBehaviour
{
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _target;
    [SerializeField] private LayerMask _GroundLayer;

    private float _OffsetToGround = 0.2f;
    private float _distanceToplayer = 2f;

    private GameObject _arrowInstatiate;

    private void Start()
    {
        if (_arrow != null)
        {
            _arrowInstatiate = Instantiate(_arrow);
        }
    }
    void FixedUpdate()
    {
        Vector3 direction = (_target.transform.position - transform.position).normalized;
        direction.y = 0;

        Vector3 targetPos = transform.position + direction * _distanceToplayer;

        RaycastHit hit;
        float rayStartHeight = 100f; 

        if (Physics.Raycast(new Vector3(targetPos.x, transform.position.y + rayStartHeight, targetPos.z),Vector3.down, out hit, rayStartHeight * 2f, _GroundLayer))
        {
            _arrowInstatiate.transform.position = hit.point + hit.normal * _OffsetToGround;
            _arrowInstatiate.transform.rotation = Quaternion.LookRotation(direction, hit.normal);
        }
    }
}
