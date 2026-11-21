#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoBA.EditorTools
{
    /// <summary>
    /// Расстановка плоских кровавых декалей снаружи и внутри реальной геометрии MainHouse и Shop
    /// с жёсткой проверкой прилегания всех углов к поверхности (без выпирания и парения в воздухе).
    /// </summary>
    internal static class HorrorDecalPlacer
    {
        public static int PlaceBloodDecals(
            Transform buildingRoot,
            string groupName,
            Material[] decalMaterials,
            bool isShop)
        {
            if (buildingRoot == null || decalMaterials == null || decalMaterials.Length == 0) return 0;

            Transform existing = buildingRoot.Find(groupName);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            // Удаляем также старые декали, которые могли быть привязаны к дверям при прошлом прогоне
            CleanupExistingDoorDecals(buildingRoot);

            if (!TryGetBuildingBounds(buildingRoot, out Bounds bounds)) return 0;

            var ownedColliders = new HashSet<Collider>(buildingRoot.GetComponentsInChildren<Collider>(true));
            var tempColliders = AddTemporaryMeshColliders(buildingRoot, ownedColliders);

            GameObject group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Create Horror Blood Decals");
            group.transform.SetParent(buildingRoot, false);

            var placedPositions = new List<Vector3>();
            int placed = 0;

            try
            {
                float floorY = bounds.min.y + 0.15f;
                int exteriorTarget = isShop ? 14 : 14;
                int interiorFloorTarget = isShop ? 7 : 9;
                int interiorWallTarget = isShop ? 5 : 7;

                placed += ScatterDoorDecals(buildingRoot, ownedColliders, decalMaterials, placedPositions);
                placed += ScatterExteriorWallDecals(group.transform, bounds, floorY, ownedColliders, decalMaterials, placedPositions, exteriorTarget, isShop);
                placed += ScatterInteriorFloorDecals(group.transform, bounds, floorY, ownedColliders, decalMaterials, placedPositions, interiorFloorTarget, isShop);
                placed += ScatterInteriorWallDecals(group.transform, bounds, floorY, ownedColliders, decalMaterials, placedPositions, interiorWallTarget, isShop);
            }
            finally
            {
                for (int i = 0; i < tempColliders.Count; i++)
                {
                    if (tempColliders[i] != null) UnityEngine.Object.DestroyImmediate(tempColliders[i]);
                }
            }

            return placed;
        }

        private static void CleanupExistingDoorDecals(Transform buildingRoot)
        {
            foreach (Door door in buildingRoot.GetComponentsInChildren<Door>(true))
            {
                if (door == null) continue;
                for (int i = door.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = door.transform.GetChild(i);
                    if (child != null && child.name.StartsWith("BloodDoor_", StringComparison.Ordinal))
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                    }
                }
            }
        }

        private static int ScatterDoorDecals(
            Transform buildingRoot,
            HashSet<Collider> owned,
            Material[] mats,
            List<Vector3> placedPos)
        {
            int count = 0;
            Door[] doors = buildingRoot.GetComponentsInChildren<Door>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                Door door = doors[i];
                if (door == null) continue;

                // Не вешаем крупные пятна на маленькие дверцы шкафчиков
                if (door.name.IndexOf("doorL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    door.name.IndexOf("doorR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Renderer rend = door.GetComponent<Renderer>();
                if (rend == null) continue;
                Bounds b = rend.bounds;
                if (b.size.y < 1.2f) continue;

                Vector3[] probeDirs = { door.transform.forward, -door.transform.forward, door.transform.right, -door.transform.right };
                for (int d = 0; d < probeDirs.Length; d++)
                {
                    Vector3 dir = probeDirs[d];
                    Vector3 origin = b.center + Vector3.up * 0.1f - dir * 0.65f;
                    if (!TryRaycastOwned(origin, dir, 1.3f, owned, out RaycastHit hit)) continue;
                    if (hit.collider == null || !hit.collider.transform.IsChildOf(door.transform) && hit.collider.transform != door.transform) continue;
                    if (Mathf.Abs(hit.normal.y) > 0.25f) continue;

                    float size = Mathf.Min(0.65f, Mathf.Min(b.size.x, b.size.z) < 0.3f ? 0.58f : 0.65f);
                    Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    if (!DoesQuadFitOnSurface(hit.point, hit.normal, rot, size, size * 0.92f, owned, door.transform)) continue;

                    Material mat = mats[(i + d) % 2 == 0 ? 1 : 2];
                    SpawnDecalQuad(door.transform, $"BloodDoor_{count + 1}", hit.point, hit.normal, rot, size, mat);
                    placedPos.Add(hit.point);
                    count++;
                    break;
                }
            }
            return count;
        }

        private static int ScatterExteriorWallDecals(
            Transform parent,
            Bounds b,
            float floorY,
            HashSet<Collider> owned,
            Material[] mats,
            List<Vector3> placedPos,
            int maxCount,
            bool isShop)
        {
            int count = 0;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            float[] heights = { floorY + 0.85f, floorY + 1.45f, floorY + 1.95f };
            float[] tValues = { -0.72f, -0.45f, -0.18f, 0.08f, 0.34f, 0.62f };

            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            for (int d = 0; d < dirs.Length && count < maxCount; d++)
            {
                Vector3 dir = dirs[d];
                Vector3 tangent = new Vector3(dir.z, 0f, -dir.x);
                float startDist = Mathf.Abs(dir.x) > 0.5f ? e.x + 2.5f : e.z + 2.5f;
                float span = Mathf.Abs(tangent.x) > 0.5f ? e.x : e.z;

                for (int ti = 0; ti < tValues.Length && count < maxCount; ti++)
                {
                    float y = heights[(d + ti) % heights.Length];
                    Vector3 origin = new Vector3(c.x, y, c.z) - dir * startDist + tangent * (tValues[ti] * span);
                    if (!TryRaycastOwned(origin, dir, startDist * 2.1f, owned, out RaycastHit hit)) continue;
                    if (Mathf.Abs(hit.normal.y) > 0.35f || IsExcludedSurface(hit.collider)) continue;

                    float minSpacing = isShop ? 1.55f : 1.75f;
                    if (!HasSpacing(placedPos, hit.point, minSpacing)) continue;

                    float size = 0.78f + ((d + ti * 3) % 3) * 0.16f;
                    Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    if (!TryFitSizeOnSurface(hit.point, hit.normal, rot, ref size, owned, null)) continue;

                    Material mat = mats[(d + ti) % 2 == 0 ? 1 : 2];
                    SpawnDecalQuad(parent, $"BloodExt_{count + 1}", hit.point, hit.normal, rot, size, mat);
                    placedPos.Add(hit.point);
                    count++;
                }
            }
            return count;
        }

        private static int ScatterInteriorFloorDecals(
            Transform parent,
            Bounds b,
            float floorY,
            HashSet<Collider> owned,
            Material[] mats,
            List<Vector3> placedPos,
            int maxCount,
            bool isShop)
        {
            int count = 0;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            float rayY = floorY + 2.35f;

            for (int ix = -2; ix <= 2 && count < maxCount; ix++)
            {
                for (int iz = -2; iz <= 2 && count < maxCount; iz++)
                {
                    if ((ix + iz) % 2 != 0 && !isShop) continue;
                    Vector3 origin = new Vector3(c.x + ix * (e.x * 0.28f), rayY, c.z + iz * (e.z * 0.28f));
                    if (!TryRaycastOwned(origin, Vector3.down, 3.2f, owned, out RaycastHit hit)) continue;
                    if (hit.normal.y < 0.92f || hit.point.y > floorY + 0.6f || IsExcludedSurface(hit.collider)) continue;
                    if (!HasSpacing(placedPos, hit.point, 1.55f)) continue;

                    float size = 0.90f + (Mathf.Abs(ix * 3 + iz) % 3) * 0.22f;
                    float angle = Mathf.Abs(hit.point.x * 37f + hit.point.z * 53f) % 360f;
                    Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.forward) * Quaternion.Euler(0f, 0f, angle);
                    if (!TryFitSizeOnSurface(hit.point, hit.normal, rot, ref size, owned, null)) continue;

                    Material mat = mats[(Mathf.Abs(ix + iz) % 2 == 0) ? 0 : 2];
                    SpawnDecalQuad(parent, $"BloodFloor_{count + 1}", hit.point, hit.normal, rot, size, mat);
                    placedPos.Add(hit.point);
                    count++;
                }
            }
            return count;
        }

        private static int ScatterInteriorWallDecals(
            Transform parent,
            Bounds b,
            float floorY,
            HashSet<Collider> owned,
            Material[] mats,
            List<Vector3> placedPos,
            int maxCount,
            bool isShop)
        {
            int count = 0;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            Vector3[] probeOffsets =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(e.x * 0.35f, 0f, e.z * 0.25f),
                new Vector3(-e.x * 0.35f, 0f, -e.z * 0.25f),
                new Vector3(-e.x * 0.30f, 0f, e.z * 0.30f),
                new Vector3(e.x * 0.30f, 0f, -e.z * 0.30f)
            };
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

            for (int p = 0; p < probeOffsets.Length && count < maxCount; p++)
            {
                for (int d = 0; d < dirs.Length && count < maxCount; d++)
                {
                    Vector3 origin = new Vector3(c.x + probeOffsets[p].x, floorY + 1.45f, c.z + probeOffsets[p].z);
                    if (!TryRaycastOwned(origin, dirs[d], Mathf.Max(e.x, e.z) * 1.1f, owned, out RaycastHit hit)) continue;
                    if (Mathf.Abs(hit.normal.y) > 0.25f || IsExcludedSurface(hit.collider)) continue;
                    if (!HasSpacing(placedPos, hit.point, 1.65f)) continue;

                    float size = 0.72f + ((p + d) % 3) * 0.14f;
                    Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
                    if (!TryFitSizeOnSurface(hit.point, hit.normal, rot, ref size, owned, null)) continue;

                    Material mat = mats[(p + d) % 2 == 0 ? 1 : 2];
                    SpawnDecalQuad(parent, $"BloodIntWall_{count + 1}", hit.point, hit.normal, rot, size, mat);
                    placedPos.Add(hit.point);
                    count++;
                }
            }
            return count;
        }

        private static bool TryFitSizeOnSurface(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Quaternion rot,
            ref float size,
            HashSet<Collider> owned,
            Transform requiredRoot)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (DoesQuadFitOnSurface(hitPoint, hitNormal, rot, size, size * 0.92f, owned, requiredRoot))
                {
                    return true;
                }
                size *= 0.75f;
                if (size < 0.42f) return false;
            }
            return false;
        }

        /// <summary>
        /// Проверяет центр и 4 угла квада (плюс середины краёв), чтобы пятно крови полностью лежало
        /// на одной плоской поверхности и не свисало в дверных проёмах, на углах и краях мебели.
        /// </summary>
        private static bool DoesQuadFitOnSurface(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Quaternion rot,
            float width,
            float height,
            HashSet<Collider> owned,
            Transform requiredRoot)
        {
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            float hw = width * 0.5f;
            float hh = height * 0.5f;

            Vector2[] samples =
            {
                new Vector2(0f, 0f),
                new Vector2(-hw, -hh),
                new Vector2(hw, -hh),
                new Vector2(-hw, hh),
                new Vector2(hw, hh),
                new Vector2(0f, hh),
                new Vector2(0f, -hh),
                new Vector2(-hw, 0f),
                new Vector2(hw, 0f)
            };

            for (int i = 0; i < samples.Length; i++)
            {
                Vector3 samplePos = hitPoint + right * samples[i].x + up * samples[i].y;
                Vector3 rayStart = samplePos + hitNormal * 0.10f;

                if (!TryRaycastOwned(rayStart, -hitNormal, 0.18f, owned, out RaycastHit backHit))
                {
                    return false;
                }

                if (requiredRoot != null &&
                    backHit.collider.transform != requiredRoot &&
                    !backHit.collider.transform.IsChildOf(requiredRoot))
                {
                    return false;
                }

                if (requiredRoot == null && IsExcludedSurface(backHit.collider))
                {
                    return false;
                }

                // Все точки квада должны лежать на одной плоскости без провалов и выступов
                if (Mathf.Abs(backHit.distance - 0.10f) > 0.025f) return false;
                if (Vector3.Dot(backHit.normal, hitNormal) < 0.94f) return false;

                // Проверяем, что перед квадом нет пересекающей геометрии (косяков, стен, мебели)
                if (Physics.Raycast(samplePos + hitNormal * 0.015f, hitNormal, 0.08f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SpawnDecalQuad(
            Transform parent,
            string name,
            Vector3 hitPoint,
            Vector3 hitNormal,
            Quaternion rot,
            float size,
            Material mat)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Collider col = quad.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            quad.transform.SetParent(parent, true);
            quad.transform.position = hitPoint + hitNormal * 0.008f;
            quad.transform.rotation = rot;
            quad.transform.localScale = new Vector3(size, size * 0.92f, 1f);

            MeshRenderer mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        private static List<Collider> AddTemporaryMeshColliders(Transform root, HashSet<Collider> owned)
        {
            var temps = new List<Collider>();
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || !mr.enabled) continue;
                if (mr.GetComponent<Collider>() != null) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temps.Add(mc);
                owned.Add(mc);
            }
            Physics.SyncTransforms();
            return temps;
        }

        private static bool TryRaycastOwned(
            Vector3 origin,
            Vector3 dir,
            float maxDist,
            HashSet<Collider> owned,
            out RaycastHit bestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float bestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (!owned.Contains(hits[i].collider)) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestHit = hits[i];
                }
            }
            return bestDist < float.MaxValue;
        }

        private static bool HasSpacing(List<Vector3> existing, Vector3 candidate, float minDist)
        {
            float minSqr = minDist * minDist;
            for (int i = 0; i < existing.Count; i++)
            {
                if ((existing[i] - candidate).sqrMagnitude < minSqr) return false;
            }
            return true;
        }

        private static bool IsExcludedSurface(Collider col)
        {
            if (col == null) return true;
            if (col.GetComponentInParent<Door>() != null || col.GetComponentInParent<CupBoard>() != null)
            {
                return true;
            }

            string[] excludedTokens =
            {
                "Glass", "Window", "Saya", "Mirror", "Sofa", "Couch", "Chair", "Table",
                "Bed", "Cabinet", "KitchenBench", "Fridge", "Stove", "Oven", "Sink",
                "TV", "Lamp", "Towel", "Toilet", "Bath", "Shower", "Shelf", "draw"
            };

            Transform cur = col.transform;
            while (cur != null)
            {
                string n = cur.name;
                for (int i = 0; i < excludedTokens.Length; i++)
                {
                    if (n.IndexOf(excludedTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                cur = cur.parent;
            }

            MeshRenderer mr = col.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.renderQueue >= 2900)
            {
                return true;
            }
            return false;
        }

        private static bool TryGetBuildingBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || !mr.enabled) continue;
                if (!has) { bounds = mr.bounds; has = true; }
                else bounds.Encapsulate(mr.bounds);
            }
            return has;
        }
    }
}
#endif
