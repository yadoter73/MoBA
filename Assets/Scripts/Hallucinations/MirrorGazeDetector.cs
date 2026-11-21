using UnityEngine;

namespace MoBA.Hallucinations
{
    /// <summary>
    /// Проверяет, стоит ли игрок рядом с одним из зеркал и смотрит ли в его отражающую сторону.
    /// </summary>
    public sealed class MirrorGazeDetector
    {
        private readonly MirrorReflection[] _mirrors;
        private readonly float _maxDistanceSqr;
        private readonly float _minLookDot;
        private readonly RaycastHit[] _losHits = new RaycastHit[8];

        public MirrorGazeDetector(MirrorReflection[] mirrors, float maxDistance = 4.5f, float maxLookAngleDegrees = 32f)
        {
            _mirrors = mirrors;
            float clampedDist = Mathf.Max(0.5f, maxDistance);
            _maxDistanceSqr = clampedDist * clampedDist;
            _minLookDot = Mathf.Cos(Mathf.Clamp(maxLookAngleDegrees, 5f, 80f) * Mathf.Deg2Rad);
        }

        public bool TryFindLookedAtMirror(Transform playerHead, Transform playerRoot, out MirrorReflection targetMirror)
        {
            targetMirror = null;
            if (playerHead == null || _mirrors == null || _mirrors.Length == 0)
            {
                return false;
            }

            Vector3 headPos = playerHead.position;
            Vector3 headForward = playerHead.forward;
            float bestDot = _minLookDot;

            for (int i = 0; i < _mirrors.Length; i++)
            {
                MirrorReflection mirror = _mirrors[i];
                if (mirror == null || !mirror.isActiveAndEnabled) continue;

                Vector3 mirrorCenter = mirror.WorldCenter;
                Vector3 toMirror = mirrorCenter - headPos;
                float sqrDist = toMirror.sqrMagnitude;
                if (sqrDist < 0.04f || sqrDist > _maxDistanceSqr) continue;

                Vector3 mirrorNormal = mirror.WorldNormal;
                // Игрок должен находиться перед лицевой (отражающей) стороной зеркала
                if (Vector3.Dot(-toMirror, mirrorNormal) <= 0.05f) continue;

                float dist = Mathf.Sqrt(sqrDist);
                Vector3 dirToMirror = toMirror / dist;
                float lookDot = Vector3.Dot(headForward, dirToMirror);
                if (lookDot < bestDot) continue;

                if (IsObstructed(headPos, dirToMirror, dist, playerRoot, mirror.transform)) continue;

                bestDot = lookDot;
                targetMirror = mirror;
            }

            return targetMirror != null;
        }

        private bool IsObstructed(Vector3 origin, Vector3 dir, float distance, Transform playerRoot, Transform mirrorTransform)
        {
            float checkDist = distance - 0.2f;
            if (checkDist <= 0.05f) return false;

            const int mask = ~(1 << 2); // Исключаем Ignore Raycast
            int count = Physics.RaycastNonAlloc(origin, dir, _losHits, checkDist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = _losHits[i].collider;
                if (col == null) continue;
                Transform tf = col.transform;
                if (playerRoot != null && tf.IsChildOf(playerRoot)) continue;
                if (mirrorTransform != null && tf.IsChildOf(mirrorTransform)) continue;
                return true;
            }

            return false;
        }
    }
}
