using KinematicCharacterController;
using UnityEngine;

namespace MoBA.Interaction
{
    /// <summary>
    /// Вспомогательный класс для интеграции движущихся дверей, калиток и ящиков
    /// с системой KinematicCharacterController (PhysicsMover + корректные коллайдеры).
    /// </summary>
    public static class PhysicsMoverHelper
    {
        public enum EaseType
        {
            Linear,
            InOutSine,
            OutCubic,
            InOutQuad
        }

        /// <summary>
        /// Гарантирует наличие кинематического Rigidbody, PhysicsMover и валидных коллайдеров для выталкивания персонажа.
        /// Невыпуклые MeshCollider переводятся в convex, так как Physics.ComputePenetration в KCC не работает с non-convex MeshCollider.
        /// </summary>
        public static PhysicsMover EnsurePhysicsMover(GameObject target, IMoverController controller)
        {
            if (target == null) return null;

            EnsureConvexColliders(target);

            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = target.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            PhysicsMover mover = target.GetComponent<PhysicsMover>();
            if (mover == null)
            {
                mover = target.AddComponent<PhysicsMover>();
            }

            mover.MoverController = controller;
            mover.ValidateData();
            mover.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            return mover;
        }

        private static void EnsureConvexColliders(GameObject target)
        {
            MeshCollider[] meshColliders = target.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                MeshCollider mc = meshColliders[i];
                if (mc == null || mc.isTrigger || mc.convex) continue;

                // Выпуклый MeshCollider необходим для Physics.ComputePenetration внутри KinematicCharacterMotor.
                mc.convex = true;
            }
        }

        public static float EvaluateEase(float t, EaseType ease)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case EaseType.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case EaseType.OutCubic:
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                case EaseType.InOutQuad:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                default:
                    return t;
            }
        }
    }
}
