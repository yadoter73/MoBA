using UnityEngine;
using PrimeTween;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
public class PointerInmpulse : MonoBehaviour
{
    [SerializeField] private GameObject[] _pointers;
    private float viewAngle = 80f;
    private float viewDistance = 100f;

    private Dictionary<Transform, CancellationTokenSource> _tasks = new();
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
                if (!_tasks.ContainsKey(pointer))
                {
                    var cts = new CancellationTokenSource();
                    _tasks[pointer] = cts;
                    PointersMoving(pointer, cts.Token).Forget();
                }
            }
            else
            {
                obj.SetActive(false);
                if (_tasks.TryGetValue(pointer, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _tasks.Remove(pointer);
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
    private async UniTask PointersMoving(Transform pointer, CancellationToken token)
    {
        Vector3 startPos = pointer.position;
        float upPos = startPos.y + 4f;
        float downPos = startPos.y;
        try
        {
            while (true)
            {
                await Tween.LocalPositionY(pointer, upPos, 1f, Ease.InOutBack).ToUniTask(cancellationToken: token);
                await UniTask.Delay(700, cancellationToken: token);
                await Tween.LocalPositionY(pointer, downPos, 1f, Ease.InOutBack).ToUniTask(cancellationToken: token);
                await UniTask.Yield(token);
            }
        }
        catch (System.OperationCanceledException) { }   
    }
}

