using UnityEngine;

[ExecuteAlways]
public class ArenaBounds : MonoBehaviour
{
    [SerializeField] private Bounds _bounds;
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private LayerMask _groundMask;
    public Bounds Bounds => _bounds;

    private void Reset()
    {
        UpdateBoundsFromComponents();
    }

    private void OnValidate()
    {
        UpdateBoundsFromComponents();
    }

    private void UpdateBoundsFromComponents()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            _bounds = col.bounds;
        }
        else
        {
            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                _bounds = rend.bounds;
            }
            else
            {
                _bounds = new Bounds(transform.position, Vector3.one * 10f);
            }
        }
    }

    public Vector3 GetRandomPointInside()
    {
        Vector3 min = _bounds.min;
        Vector3 max = _bounds.max;

        return new Vector3(
            Random.Range(min.x, max.x),
            Random.Range(min.y + 1f, max.y),
            Random.Range(min.z, max.z)
        );
    }

    public void UpdateBoundsFromCollider()
    {
        var col = GetComponent<Collider>();
        if (col != null) _bounds = col.bounds;
    }

    public float MaxExtentMagnitude => _bounds.extents.magnitude;
}