#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MoBA.EditorTools
{
    public struct DemonAnimMetrics
    {
        public AnimatorController Controller;
        public float BaseScale;
        public float QuadrupedHeight;
        public float QuadrupedWidth;
        public float QuadrupedLength;
        public float QuadrupedCenterZ;
        public float QuadrupedMaxZ;
        public float QuadrupedHeadY;
        public float WalkHeight;
    }

    /// <summary>
    /// Генерирует процедурные клипы анимаций для 46-костного рига Demon.prefab
    /// с точным выравниванием нижней точки меша по земле (y = 0) через InverseTransformVector.
    /// </summary>
    public static class DemonFastAnimationBuilder
    {
        public const string AnimFolder = "Assets/Animations/DemonFast";
        public const string ControllerPath = "Assets/Prefabs/DemonFast Variant.controller";

        private const string ArmaturePath = "Арматура";
        private const string Spine0Path = "Арматура/Bone";
        private const string Spine1Path = "Арматура/Bone/Bone.001";
        private const string ChestPath = "Арматура/Bone/Bone.001/Bone.002";
        private const string HeadPath = "Арматура/Bone/Bone.001/Bone.002/Bone.003";

        private const string RShoulderPath = "Арматура/Bone/Bone.001/Bone.002/Bone.004";
        private const string RUpperArmPath = "Арматура/Bone/Bone.001/Bone.002/Bone.004/Bone.006";
        private const string RForearmPath = "Арматура/Bone/Bone.001/Bone.002/Bone.004/Bone.006/Bone.007";
        private const string RHandPath = "Арматура/Bone/Bone.001/Bone.002/Bone.004/Bone.006/Bone.007/Bone.008";

        private const string LShoulderPath = "Арматура/Bone/Bone.001/Bone.002/Bone.005";
        private const string LUpperArmPath = "Арматура/Bone/Bone.001/Bone.002/Bone.005/Bone.016";
        private const string LForearmPath = "Арматура/Bone/Bone.001/Bone.002/Bone.005/Bone.016/Bone.017";
        private const string LHandPath = "Арматура/Bone/Bone.001/Bone.002/Bone.005/Bone.016/Bone.017/Bone.018";

        private const string LHipPath = "Арматура/Bone.026";
        private const string LThighPath = "Арматура/Bone.026/Bone.029";
        private const string LShinPath = "Арматура/Bone.026/Bone.029/Bone.030";
        private const string LFootPath = "Арматура/Bone.026/Bone.029/Bone.030/Bone.032";

        private const string RHipPath = "Арматура/Bone.027";
        private const string RThighPath = "Арматура/Bone.027/Bone.028";
        private const string RShinPath = "Арматура/Bone.027/Bone.028/Bone.031";
        private const string RFootPath = "Арматура/Bone.027/Bone.028/Bone.031/Bone.033";

        private static readonly string[] AnimatedBones =
        {
            Spine0Path, Spine1Path, ChestPath, HeadPath,
            RShoulderPath, RUpperArmPath, RForearmPath, RHandPath,
            LShoulderPath, LUpperArmPath, LForearmPath, LHandPath,
            LHipPath, LThighPath, LShinPath, LFootPath,
            RHipPath, RThighPath, RShinPath, RFootPath
        };

        private sealed class RigContext
        {
            public GameObject Root;
            public Transform Armature;
            public SkinnedMeshRenderer Smr;
            public Mesh BakeTempMesh;
            public Vector3 ArmatureRestLocalPos;
            public readonly Dictionary<string, Transform> Bones = new();
            public readonly Dictionary<string, Quaternion> RestLocalRot = new();
            public Transform HeadEnd;
        }

        public static DemonAnimMetrics BuildAnimationsAndController(GameObject baseDemonPrefab)
        {
            EnsureFolder(AnimFolder);

            RigContext rig = CreateRigContext(baseDemonPrefab);
            try
            {
                AnimationClip idleClip = BakeClip(rig, $"{AnimFolder}/DemonFast_Idle.anim", 2.4f, 24, true, ApplyIdlePose, out _, out _);
                AnimationClip walkClip = BakeClip(rig, $"{AnimFolder}/DemonFast_PatrolWalk.anim", 1.25f, 24, true, ApplyWalkPose, out Bounds walkBounds, out _);
                AnimationClip chaseClip = BakeClip(rig, $"{AnimFolder}/DemonFast_QuadrupedChase.anim", 0.54f, 24, true, ApplyQuadrupedChasePose, out Bounds chaseBounds, out float chaseHeadY);
                AnimationClip attackClip = BakeClip(rig, $"{AnimFolder}/DemonFast_AttackLunge.anim", 0.56f, 24, false, ApplyAttackLungePose, out _, out _);

                AnimatorController controller = BuildController(idleClip, walkClip, chaseClip, attackClip);

                return new DemonAnimMetrics
                {
                    Controller = controller,
                    BaseScale = rig.Root.transform.localScale.y,
                    QuadrupedHeight = chaseBounds.size.y,
                    QuadrupedWidth = chaseBounds.size.x,
                    QuadrupedLength = chaseBounds.size.z,
                    QuadrupedCenterZ = chaseBounds.center.z,
                    QuadrupedMaxZ = chaseBounds.max.z,
                    QuadrupedHeadY = chaseHeadY,
                    WalkHeight = walkBounds.size.y
                };
            }
            finally
            {
                if (rig.BakeTempMesh != null) UnityEngine.Object.DestroyImmediate(rig.BakeTempMesh);
                if (rig.Root != null) UnityEngine.Object.DestroyImmediate(rig.Root);
            }
        }

        public static Bounds ComputeWorldSkinnedMeshBounds(SkinnedMeshRenderer smr, Mesh tempMesh)
        {
            smr.BakeMesh(tempMesh, false);
            Vector3[] verts = tempMesh.vertices;
            if (verts == null || verts.Length == 0)
            {
                return smr.bounds;
            }

            Vector3 pos = smr.transform.position;
            Quaternion rot = smr.transform.rotation;
            Vector3 first = pos + rot * verts[0];
            Vector3 min = first;
            Vector3 max = first;
            for (int i = 1; i < verts.Length; i++)
            {
                Vector3 wp = pos + rot * verts[i];
                if (wp.x < min.x) min.x = wp.x;
                if (wp.y < min.y) min.y = wp.y;
                if (wp.z < min.z) min.z = wp.z;
                if (wp.x > max.x) max.x = wp.x;
                if (wp.y > max.y) max.y = wp.y;
                if (wp.z > max.z) max.z = wp.z;
            }

            Bounds b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        private static RigContext CreateRigContext(GameObject baseDemonPrefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseDemonPrefab);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            var ctx = new RigContext
            {
                Root = inst,
                Armature = inst.transform.Find(ArmaturePath),
                Smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true),
                BakeTempMesh = new Mesh()
            };
            ctx.ArmatureRestLocalPos = ctx.Armature.localPosition;

            foreach (string path in AnimatedBones)
            {
                Transform t = inst.transform.Find(path);
                if (t == null) throw new InvalidOperationException($"[DemonFastAnimationBuilder] Кость не найдена: {path}");
                ctx.Bones[path] = t;
                ctx.RestLocalRot[path] = t.localRotation;
            }

            Transform headEnd = inst.transform.Find(HeadPath + "/Bone.003_end");
            ctx.HeadEnd = headEnd != null ? headEnd : ctx.Bones[HeadPath];
            return ctx;
        }

        private static void ResetPose(RigContext rig)
        {
            rig.Armature.localPosition = rig.ArmatureRestLocalPos;
            foreach (string path in AnimatedBones)
            {
                rig.Bones[path].localRotation = rig.RestLocalRot[path];
            }
            RotateWorld(rig.Bones[LUpperArmPath], -18f, 0f, -6f);
            RotateWorld(rig.Bones[LForearmPath], -10f, 0f, 0f);
        }

        private static void RotateWorld(Transform bone, float pitchRightX, float yawUpY, float rollForwardZ)
        {
            Quaternion delta = Quaternion.AngleAxis(yawUpY, Vector3.up)
                               * Quaternion.AngleAxis(pitchRightX, Vector3.right)
                               * Quaternion.AngleAxis(rollForwardZ, Vector3.forward);
            bone.rotation = delta * bone.rotation;
        }

        private static void GroundRigByMesh(RigContext rig, float targetMinY = 0f)
        {
            Bounds meshBounds = ComputeWorldSkinnedMeshBounds(rig.Smr, rig.BakeTempMesh);
            float deltaWorldY = targetMinY - meshBounds.min.y;
            Transform parent = rig.Armature.parent != null ? rig.Armature.parent : rig.Root.transform;
            Vector3 localDelta = parent.InverseTransformVector(new Vector3(0f, deltaWorldY, 0f));
            rig.Armature.localPosition += localDelta;
        }

        private static void ApplyIdlePose(RigContext rig, float u)
        {
            float rad = u * Mathf.PI * 2f;
            float breath = Mathf.Sin(rad);
            float sway = Mathf.Sin(rad * 0.5f);

            RotateWorld(rig.Bones[Spine0Path], 6f + breath * 2.5f, sway * 3f, sway * 2f);
            RotateWorld(rig.Bones[Spine1Path], 4f + breath * 2f, 0f, -sway * 1.5f);
            RotateWorld(rig.Bones[ChestPath], 5f - breath * 3f, -sway * 2f, 0f);
            RotateWorld(rig.Bones[HeadPath], -10f + breath * 3f, Mathf.Sin(rad) * 8f, Mathf.Cos(rad) * 4f);

            RotateWorld(rig.Bones[RUpperArmPath], 4f + breath * 3f, 0f, -4f - breath * 2f);
            RotateWorld(rig.Bones[RForearmPath], -18f + breath * 4f, 0f, 0f);
            RotateWorld(rig.Bones[LUpperArmPath], 4f - breath * 3f, 0f, 4f + breath * 2f);
            RotateWorld(rig.Bones[LForearmPath], -18f - breath * 4f, 0f, 0f);

            RotateWorld(rig.Bones[LThighPath], -4f, 0f, 0f);
            RotateWorld(rig.Bones[LShinPath], 6f, 0f, 0f);
            RotateWorld(rig.Bones[RThighPath], -4f, 0f, 0f);
            RotateWorld(rig.Bones[RShinPath], 6f, 0f, 0f);

            // Плотное прижатие стоп к земле без зазора на склонах
            GroundRigByMesh(rig, -0.05f);
        }

        private static void ApplyWalkPose(RigContext rig, float u)
        {
            float rad = u * Mathf.PI * 2f;
            float s = Mathf.Sin(rad);

            RotateWorld(rig.Bones[Spine0Path], 12f + Mathf.Sin(rad * 2f) * 2f, s * 5f, -s * 3f);
            RotateWorld(rig.Bones[Spine1Path], 6f, -s * 3f, s * 2f);
            RotateWorld(rig.Bones[ChestPath], 6f, -s * 6f, 0f);
            RotateWorld(rig.Bones[HeadPath], -14f + Mathf.Cos(rad * 2f) * 2.5f, s * 6f, 0f);

            RotateWorld(rig.Bones[RUpperArmPath], -s * 24f + 6f, 0f, -4f);
            RotateWorld(rig.Bones[RForearmPath], -24f - Mathf.Max(0f, s) * 14f, 0f, 0f);
            RotateWorld(rig.Bones[LUpperArmPath], s * 24f + 6f, 0f, 4f);
            RotateWorld(rig.Bones[LForearmPath], -24f - Mathf.Max(0f, -s) * 14f, 0f, 0f);

            float lSwing = s;
            float rSwing = -s;
            RotateWorld(rig.Bones[LThighPath], -lSwing * 28f - 4f, 0f, 0f);
            RotateWorld(rig.Bones[LShinPath], Mathf.Max(0f, lSwing) * 26f + 6f, 0f, 0f);
            RotateWorld(rig.Bones[LFootPath], -Mathf.Max(0f, -lSwing) * 16f, 0f, 0f);

            RotateWorld(rig.Bones[RThighPath], -rSwing * 28f - 4f, 0f, 0f);
            RotateWorld(rig.Bones[RShinPath], Mathf.Max(0f, rSwing) * 26f + 6f, 0f, 0f);
            RotateWorld(rig.Bones[RFootPath], -Mathf.Max(0f, -rSwing) * 16f, 0f, 0f);

            GroundRigByMesh(rig, -0.05f);
        }

        private static void ApplyQuadrupedChasePose(RigContext rig, float u)
        {
            float rad = u * Mathf.PI * 2f;
            // Диагональный цикл четверенек: левая рука + правая нога (diagA) в противофазе к правой руке + левой ноге (diagB)
            float diagA = Mathf.Sin(rad);
            float diagB = -diagA;
            float spinePulse = Mathf.Sin(rad * 2f);

            // Очень низкое хищное положение: таз и позвоночник прижаты к земле (~98° вперёд), корпус стелется низко
            RotateWorld(rig.Bones[Spine0Path], 70f + spinePulse * 2f, diagA * 5f, -diagA * 3f);
            RotateWorld(rig.Bones[Spine1Path], 16f + spinePulse * 1.5f, diagA * 4f, 0f);
            RotateWorld(rig.Bones[ChestPath], 12f, -diagA * 6f, 0f);
            // Голова вытянута вперёд к игроку
            RotateWorld(rig.Bones[HeadPath], -82f - spinePulse * 2f, diagA * 3f, 0f);

            // Передние лапы: сильнее согнуты в локтях и разведены в стороны, чтобы их высота по Y точно совпадала с задними лапами!
            RotateWorld(rig.Bones[LShoulderPath], -18f, 0f, 22f);
            RotateWorld(rig.Bones[LUpperArmPath], -44f - diagA * 26f, -14f, 28f);
            RotateWorld(rig.Bones[LForearmPath], -58f + Mathf.Max(0f, -diagA) * 20f, 0f, 0f);
            RotateWorld(rig.Bones[LHandPath], 72f, 0f, 0f);

            RotateWorld(rig.Bones[RShoulderPath], -18f, 0f, -22f);
            RotateWorld(rig.Bones[RUpperArmPath], -44f - diagB * 26f, 14f, -28f);
            RotateWorld(rig.Bones[RForearmPath], -58f + Mathf.Max(0f, -diagB) * 20f, 0f, 0f);
            RotateWorld(rig.Bones[RHandPath], 72f, 0f, 0f);

            // Задние лапы: умеренный присед, стопы выходят на ту же плоскость земли y = 0, что и передние лапы
            RotateWorld(rig.Bones[RThighPath], -34f - diagA * 26f, 10f, -12f);
            RotateWorld(rig.Bones[RShinPath], 54f + Mathf.Max(0f, diagA) * 20f, 0f, 0f);
            RotateWorld(rig.Bones[RFootPath], -26f + diagA * 14f, 0f, 0f);

            RotateWorld(rig.Bones[LThighPath], -34f - diagB * 26f, -10f, 12f);
            RotateWorld(rig.Bones[LShinPath], 54f + Mathf.Max(0f, diagB) * 20f, 0f, 0f);
            RotateWorld(rig.Bones[LFootPath], -26f + diagB * 14f, 0f, 0f);

            // Плотно прижимаем все 4 конечности к земле без левитации и прыжков
            GroundRigByMesh(rig, -0.06f);
        }

        private static void ApplyAttackLungePose(RigContext rig, float u)
        {
            float lunge = u < 0.25f
                ? -Mathf.Sin((u / 0.25f) * Mathf.PI * 0.5f) * 0.35f
                : (u < 0.65f
                    ? Mathf.Lerp(-0.35f, 1.0f, Mathf.SmoothStep(0f, 1f, (u - 0.25f) / 0.40f))
                    : Mathf.Lerp(1.0f, 0.20f, (u - 0.65f) / 0.35f));

            // Низкий хищный бросок вперёд из положения на четвереньках
            RotateWorld(rig.Bones[Spine0Path], 56f + lunge * 14f, 0f, 0f);
            RotateWorld(rig.Bones[Spine1Path], 14f + lunge * 8f, 0f, 0f);
            RotateWorld(rig.Bones[ChestPath], 10f + lunge * 6f, 0f, 0f);
            RotateWorld(rig.Bones[HeadPath], -66f - lunge * 14f, 0f, 0f);

            float slashPitch = -54f - lunge * 44f;
            RotateWorld(rig.Bones[RUpperArmPath], slashPitch, -lunge * 22f, -20f + lunge * 30f);
            RotateWorld(rig.Bones[RForearmPath], -36f + lunge * 22f, 0f, 0f);
            RotateWorld(rig.Bones[LUpperArmPath], slashPitch, lunge * 22f, 20f - lunge * 30f);
            RotateWorld(rig.Bones[LForearmPath], -36f + lunge * 22f, 0f, 0f);

            RotateWorld(rig.Bones[LThighPath], -34f + lunge * 16f, -10f, 10f);
            RotateWorld(rig.Bones[LShinPath], 54f - lunge * 14f, 0f, 0f);
            RotateWorld(rig.Bones[RThighPath], -34f + lunge * 16f, 10f, -10f);
            RotateWorld(rig.Bones[RShinPath], 54f - lunge * 14f, 0f, 0f);

            GroundRigByMesh(rig, -0.05f);
        }

        private static AnimationClip BakeClip(
            RigContext rig,
            string assetPath,
            float duration,
            int sampleSteps,
            bool loop,
            Action<RigContext, float> poseAction,
            out Bounds aggregateBounds,
            out float avgHeadY)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            clip.ClearCurves();
            clip.frameRate = 30f;

            var armPosX = new AnimationCurve();
            var armPosY = new AnimationCurve();
            var armPosZ = new AnimationCurve();

            var rotCurves = new Dictionary<string, AnimationCurve[]>();
            var prevQ = new Dictionary<string, Quaternion>();
            foreach (string path in AnimatedBones)
            {
                rotCurves[path] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            }

            aggregateBounds = new Bounds();
            bool hasBounds = false;
            float headYSum = 0f;

            for (int i = 0; i <= sampleSteps; i++)
            {
                float u = (float)i / sampleSteps;
                float t = u * duration;
                float evalU = (loop && i == sampleSteps) ? 0f : u;

                ResetPose(rig);
                poseAction(rig, evalU);

                Bounds frameBounds = ComputeWorldSkinnedMeshBounds(rig.Smr, rig.BakeTempMesh);
                if (!hasBounds)
                {
                    aggregateBounds = frameBounds;
                    hasBounds = true;
                }
                else
                {
                    aggregateBounds.Encapsulate(frameBounds);
                }
                headYSum += rig.HeadEnd.position.y;

                Vector3 ap = rig.Armature.localPosition;
                armPosX.AddKey(t, ap.x);
                armPosY.AddKey(t, ap.y);
                armPosZ.AddKey(t, ap.z);

                foreach (string path in AnimatedBones)
                {
                    Quaternion q = rig.Bones[path].localRotation;
                    if (prevQ.TryGetValue(path, out Quaternion pq) && Quaternion.Dot(pq, q) < 0f)
                    {
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    }
                    prevQ[path] = q;

                    AnimationCurve[] c = rotCurves[path];
                    c[0].AddKey(t, q.x);
                    c[1].AddKey(t, q.y);
                    c[2].AddKey(t, q.z);
                    c[3].AddKey(t, q.w);
                }
            }

            avgHeadY = headYSum / (sampleSteps + 1);
            Debug.Log($"[DemonFastAnim] {Path.GetFileNameWithoutExtension(assetPath)} (at baseScale {rig.Root.transform.localScale.y:F2}): minY={aggregateBounds.min.y:F3} maxY={aggregateBounds.max.y:F3} H={aggregateBounds.size.y:F3} W={aggregateBounds.size.x:F3} L={aggregateBounds.size.z:F3} maxZ={aggregateBounds.max.z:F3} avgHeadY={avgHeadY:F3}");

            clip.SetCurve(ArmaturePath, typeof(Transform), "m_LocalPosition.x", armPosX);
            clip.SetCurve(ArmaturePath, typeof(Transform), "m_LocalPosition.y", armPosY);
            clip.SetCurve(ArmaturePath, typeof(Transform), "m_LocalPosition.z", armPosZ);

            foreach (string path in AnimatedBones)
            {
                AnimationCurve[] c = rotCurves[path];
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", c[0]);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", c[1]);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", c[2]);
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", c[3]);
            }

            clip.EnsureQuaternionContinuity();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController BuildController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip chaseClip,
            AnimationClip attackClip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("isMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("isChasing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Roar", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.AddState("Idle", new Vector3(240, 60, 0));
            AnimatorState walk = sm.AddState("Walk", new Vector3(520, 60, 0));
            AnimatorState chase = sm.AddState("Chase", new Vector3(520, 200, 0));
            AnimatorState attack = sm.AddState("Attack", new Vector3(240, 200, 0));

            idle.motion = idleClip;
            walk.motion = walkClip;
            chase.motion = chaseClip;
            attack.motion = attackClip;
            sm.defaultState = idle;

            AddBoolTransition(idle, walk, 0.18f, ("isMoving", true), ("isChasing", false));
            AddBoolTransition(idle, chase, 0.16f, ("isChasing", true));
            AddBoolTransition(walk, idle, 0.18f, ("isMoving", false), ("isChasing", false));
            AddBoolTransition(walk, chase, 0.18f, ("isChasing", true));
            AddBoolTransition(chase, walk, 0.18f, ("isMoving", true), ("isChasing", false));
            AddBoolTransition(chase, idle, 0.18f, ("isMoving", false), ("isChasing", false));

            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attack);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.08f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.95f;
            attackToIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            float duration,
            params (string param, bool value)[] conditions)
        {
            AnimatorStateTransition tr = from.AddTransition(to);
            tr.hasExitTime = false;
            tr.duration = duration;
            tr.interruptionSource = TransitionInterruptionSource.Source;
            foreach (var (param, val) in conditions)
            {
                tr.AddCondition(val ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
            }
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
