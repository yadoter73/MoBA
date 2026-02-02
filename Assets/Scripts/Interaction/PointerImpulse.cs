using UnityEngine;
using PrimeTween;
using System.Collections;
using System.Collections.Generic;

public class PointerInmpulse : MonoBehaviour
{
    [SerializeField] private GameObject[] _pointers;
    private float viewAngle = 80f;
    private float viewDistance = 100f;

    private Dictionary<Transform, Coroutine> _runningCoroutines = new();
    private void FixedUpdate()
    {
        float angleThreshold = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        foreach (GameObject obj in _pointers)
        {
            if (obj == null) continue;
            Transform pointer = obj.transform;
            bool isVisible = CheckVisibility(pointer, origin, forward, angleThreshold);

            if (isVisible)
            {
                obj.SetActive(true);
                if (!_runningCoroutines.ContainsKey(pointer))
                {
                    _runningCoroutines[pointer] = StartCoroutine(PointersMoving(pointer));
                }
            }
            else
            {
                obj.SetActive(false);
                if (_runningCoroutines.TryGetValue(pointer, out Coroutine running))
                {
                    StopCoroutine(running);
                    _runningCoroutines.Remove(pointer);
                }
            }

        }
    }

    bool CheckVisibility(Transform target, Vector3 origin, Vector3 forward, float threshold)
    {
        Vector3 dirToTarget = target.position - origin;
        float distance = dirToTarget.magnitude;

        if (distance > viewDistance) return false;

        dirToTarget /= distance;
        float dot = Vector3.Dot(forward, dirToTarget);

        return dot > threshold;
    }
    IEnumerator PointersMoving(Transform pointer)
    {
        Vector3 startPos = pointer.position;
        float upPos = startPos.y + 4f;
        float downPos = startPos.y;
        while (true)
        {

            yield return Tween.LocalPositionY(pointer, upPos, 1f, Ease.InOutBack);
            yield return null;
            yield return Tween.LocalPositionY(pointer, downPos, 1f, Ease.InOutBack);
        }
    }
}

