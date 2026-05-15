using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MeshArmorPlate
{
    [InspectorName("ID бронелиста")]
    public string plateId = "plate";
    [InspectorName("Название")]
    public string displayNameRu = "Бронелист";
    [InspectorName("Толщина брони, мм")]
    [Min(0f)] public float armorMm = 40f;
    [InspectorName("Максимальная прочность бронелиста")]
    [Min(1f)] public float maxArmorHp = 160f;
    [InspectorName("Текущая прочность бронелиста")]
    [Min(0f)] public float armorHp = 160f;
    [InspectorName("Угол рикошета, град")]
    [Range(0f, 89f)] public float ricochetAngleDeg = 70f;
    [InspectorName("Треугольники mesh")]
    public List<int> triangleIndices = new List<int>();
    [InspectorName("Цвет отладки")]
    public Color debugColor = new Color(1f, 0.6f, 0.1f, 0.35f);

    public float ArmorIntegrity01 => maxArmorHp > 0.001f ? Mathf.Clamp01(armorHp / maxArmorHp) : 0f;
    public float CurrentArmorMm => Mathf.Max(0f, armorMm) * ArmorIntegrity01;
    public bool IsDestroyed => ArmorIntegrity01 <= 0.001f;

    public ArmorSurface ToSurface()
    {
        return new ArmorSurface
        {
            zoneId = plateId,
            displayNameRu = displayNameRu,
            armorMm = CurrentArmorMm,
            baseArmorMm = armorMm,
            armorIntegrity01 = ArmorIntegrity01,
            ricochetAngleDeg = ricochetAngleDeg,
            overmatchCaliberMultiplier = 0f,
            structureDamageMultiplier = 1f,
            moduleDamageMultiplier = 1f,
            highExplosiveSurfaceDamageMultiplier = 1f,
            ramDamageMultiplier = 1f,
            protectedModuleIds = null
        };
    }

    public bool ContainsTriangle(int triangleIndex)
    {
        return triangleIndices != null && triangleIndices.Contains(triangleIndex);
    }

    public float ApplyArmorPlateDamage(float amount)
    {
        float previous = armorHp;
        armorHp = Mathf.Clamp(armorHp - Mathf.Max(0f, amount), 0f, Mathf.Max(1f, maxArmorHp));
        return previous - armorHp;
    }

    public void ResetArmorHp()
    {
        maxArmorHp = Mathf.Max(1f, maxArmorHp);
        armorHp = maxArmorHp;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
public class MeshArmorBody : MonoBehaviour
{
    [Header("Броневая mesh-оболочка")]
    [InspectorName("Корабль-владелец")]
    public DamageableShip owner;
    [InspectorName("MeshCollider")]
    public MeshCollider meshCollider;
    [InspectorName("Сделать коллайдер триггером")]
    [Tooltip("Для статичной mesh-брони обычно лучше оставить выключенным. Raycast попаданий работает и по обычному коллайдеру.")]
    public bool colliderIsTrigger;
    [InspectorName("Бронелисты")]
    public List<MeshArmorPlate> plates = new List<MeshArmorPlate>();

    [Header("Автосборка")]
    [InspectorName("Порог угла нормали")]
    [Tooltip("Треугольники с похожей нормалью считаются одной плоской гранью.")]
    [Range(1f, 30f)] public float normalAngleThresholdDeg = 8f;
    [InspectorName("Порог расстояния плоскости")]
    [Tooltip("Параллельные, но разнесенные плоскости становятся разными бронелистами.")]
    [Min(0.001f)] public float planeDistanceThreshold = 0.05f;
    [InspectorName("Броня по умолчанию, мм")]
    [Min(0f)] public float defaultArmorMm = 40f;
    [InspectorName("Прочность на 1 мм брони")]
    [Min(1f)] public float hpPerArmorMm = 4f;

    [Header("Отладка")]
    [InspectorName("Показывать бронелисты")]
    public bool drawPlates = true;
    [InspectorName("Показывать только при выборе")]
    public bool drawOnlyWhenSelected = true;
    [InspectorName("Рисовать не больше треугольников")]
    [Min(1)] public int maxDrawnTriangles = 180;

    private MeshFilter meshFilter;

    private void Reset()
    {
        owner = GetComponentInParent<DamageableShip>();
        EnsureMeshCollider();
        if (plates == null || plates.Count == 0)
        {
            RebuildPlatesFromMesh();
        }
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<DamageableShip>();
        }

        EnsureMeshCollider();
    }

    private void OnValidate()
    {
        defaultArmorMm = Mathf.Max(0f, defaultArmorMm);
        hpPerArmorMm = Mathf.Max(1f, hpPerArmorMm);
        planeDistanceThreshold = Mathf.Max(0.001f, planeDistanceThreshold);
        maxDrawnTriangles = Mathf.Max(1, maxDrawnTriangles);
        ClampPlateState();
        SyncExistingMeshCollider();
    }

    public void EnsureMeshCollider()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
        }

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        if (meshFilter != null && meshCollider != null)
        {
            SyncMeshCollider();
        }
    }

    private void SyncExistingMeshCollider()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
        }

        if (meshCollider != null && meshFilter != null)
        {
            SyncMeshCollider();
        }
    }

    private void SyncMeshCollider()
    {
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = colliderIsTrigger;
    }

    public void RebuildPlatesFromMesh()
    {
        Mesh mesh = GetMesh();
        if (mesh == null)
        {
            plates = new List<MeshArmorPlate>();
            return;
        }

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        List<BuildPlateGroup> groups = new List<BuildPlateGroup>();

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int triangleIndex = i / 3;
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= 0.000001f) continue;

            normal.Normalize();
            Vector3 center = (a + b + c) / 3f;
            float plane = Vector3.Dot(normal, center);
            BuildPlateGroup group = FindBuildGroup(groups, normal, plane);
            if (group == null)
            {
                group = new BuildPlateGroup(normal, plane);
                groups.Add(group);
            }

            group.triangleIndices.Add(triangleIndex);
            group.center += center;
        }

        plates = new List<MeshArmorPlate>();
        for (int i = 0; i < groups.Count; i++)
        {
            BuildPlateGroup group = groups[i];
            group.center /= Mathf.Max(1, group.triangleIndices.Count);
            float armorHp = Mathf.Max(1f, defaultArmorMm * hpPerArmorMm);
            Color color = Color.HSVToRGB((i * 0.137f) % 1f, 0.7f, 1f);
            plates.Add(new MeshArmorPlate
            {
                plateId = $"mesh_plate_{i + 1:00}",
                displayNameRu = GuessPlateName(group.normal, i + 1),
                armorMm = defaultArmorMm,
                maxArmorHp = armorHp,
                armorHp = armorHp,
                ricochetAngleDeg = 70f,
                triangleIndices = new List<int>(group.triangleIndices),
                debugColor = new Color(color.r, color.g, color.b, 0.35f)
            });
        }
    }

    public DamageHitResult ReceiveHit(DamageHitContext context, RaycastHit hit)
    {
        if (context.hitPoint == Vector3.zero)
        {
            context.hitPoint = hit.point;
        }

        context.hitNormal = hit.normal.sqrMagnitude > 0.001f
            ? hit.normal
            : EstimateWorldNormal(hit.triangleIndex);

        return ReceiveHit(context, hit.triangleIndex);
    }

    public DamageHitResult ReceiveHit(DamageHitContext context, int triangleIndex)
    {
        DamageableShip target = owner != null ? owner : GetComponentInParent<DamageableShip>();
        if (target == null)
        {
            return new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "Попадание в mesh-броню без DamageableShip."
            };
        }

        MeshArmorPlate plate = FindPlate(triangleIndex);
        if (plate == null)
        {
            plate = FindPlateByNormal(context.hitNormal);
        }

        if (plate == null)
        {
            return new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "Для треугольника mesh не найден бронелист."
            };
        }

        Vector3 incoming = context.incomingDirection.sqrMagnitude > 0.001f
            ? context.incomingDirection.normalized
            : -context.hitNormal.normalized;

        context.incomingDirection = incoming;
        context.internalTravelDistance = CalculateBoundsExitDistance(context.hitPoint, incoming);
        context.deferResultLogging = true;

        DamageHitResult result = target.ApplyHit(plate.ToSurface(), context);
        float armorDamageMultiplier = GetArmorDamageMultiplier(result.outcome);
        float plateDamage = plate.ApplyArmorPlateDamage(context.armorPlateDamage * armorDamageMultiplier);
        result.armorPlateDamage = plateDamage;
        result.remainingArmorPlateHp = plate.armorHp;
        result.maxArmorPlateHp = plate.maxArmorHp;
        result.message += $" Бронелист: -{plateDamage:0.0}, осталось {plate.armorHp:0.0}/{plate.maxArmorHp:0.0}, расчетная толщина {plate.CurrentArmorMm:0.0} мм.";
        target.FinalizeHitResult(result);
        return result;
    }

    public void ResetArmorState()
    {
        if (plates == null) return;

        for (int i = 0; i < plates.Count; i++)
        {
            if (plates[i] != null)
            {
                plates[i].ResetArmorHp();
            }
        }
    }

    public MeshArmorPlate FindPlate(int triangleIndex)
    {
        if (plates == null) return null;

        for (int i = 0; i < plates.Count; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate != null && plate.ContainsTriangle(triangleIndex))
            {
                return plate;
            }
        }

        return null;
    }

    private MeshArmorPlate FindPlateByNormal(Vector3 worldNormal)
    {
        Mesh mesh = GetMesh();
        if (mesh == null || plates == null || worldNormal.sqrMagnitude <= 0.001f) return null;

        Vector3 localNormal = transform.InverseTransformDirection(worldNormal.normalized);
        MeshArmorPlate best = null;
        float bestAngle = float.PositiveInfinity;
        for (int i = 0; i < plates.Count; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate == null || plate.triangleIndices == null || plate.triangleIndices.Count == 0) continue;

            Vector3 plateNormal = CalculateTriangleNormal(mesh, plate.triangleIndices[0]);
            float angle = Vector3.Angle(localNormal, plateNormal);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = plate;
            }
        }

        return best;
    }

    private Vector3 EstimateWorldNormal(int triangleIndex)
    {
        Mesh mesh = GetMesh();
        if (mesh == null) return transform.forward;

        Vector3 localNormal = CalculateTriangleNormal(mesh, triangleIndex);
        return transform.TransformDirection(localNormal).normalized;
    }

    private Mesh GetMesh()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        return meshFilter != null ? meshFilter.sharedMesh : null;
    }

    private BuildPlateGroup FindBuildGroup(List<BuildPlateGroup> groups, Vector3 normal, float plane)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            BuildPlateGroup group = groups[i];
            if (Vector3.Angle(group.normal, normal) > normalAngleThresholdDeg) continue;
            if (Mathf.Abs(group.plane - plane) > planeDistanceThreshold) continue;
            return group;
        }

        return null;
    }

    private float CalculateBoundsExitDistance(Vector3 worldPoint, Vector3 worldDirection)
    {
        Bounds bounds = meshCollider != null ? meshCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 direction = worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : transform.forward;
        float best = float.PositiveInfinity;

        CheckWorldAxis(worldPoint.x, direction.x, bounds.min.x, bounds.max.x, ref best);
        CheckWorldAxis(worldPoint.y, direction.y, bounds.min.y, bounds.max.y, ref best);
        CheckWorldAxis(worldPoint.z, direction.z, bounds.min.z, bounds.max.z, ref best);

        if (float.IsInfinity(best) || best <= 0.001f)
        {
            return bounds.size.magnitude;
        }

        return Mathf.Max(0.1f, best);
    }

    private static void CheckWorldAxis(float point, float direction, float min, float max, ref float best)
    {
        if (Mathf.Abs(direction) <= 0.0001f) return;

        float plane = direction > 0f ? max : min;
        float distance = (plane - point) / direction;
        if (distance > 0.001f && distance < best)
        {
            best = distance;
        }
    }

    private void ClampPlateState()
    {
        if (plates == null) return;

        for (int i = 0; i < plates.Count; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate == null) continue;
            plate.maxArmorHp = Mathf.Max(1f, plate.maxArmorHp);
            plate.armorHp = Mathf.Clamp(plate.armorHp, 0f, plate.maxArmorHp);
            plate.armorMm = Mathf.Max(0f, plate.armorMm);
        }
    }

    private static float GetArmorDamageMultiplier(DamageHitOutcome outcome)
    {
        switch (outcome)
        {
            case DamageHitOutcome.Penetration:
                return 2f;
            case DamageHitOutcome.Ricochet:
                return 0.5f;
            default:
                return 1f;
        }
    }

    private static Vector3 CalculateTriangleNormal(Mesh mesh, int triangleIndex)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int offset = triangleIndex * 3;
        if (offset < 0 || offset + 2 >= triangles.Length) return Vector3.forward;

        Vector3 a = vertices[triangles[offset]];
        Vector3 b = vertices[triangles[offset + 1]];
        Vector3 c = vertices[triangles[offset + 2]];
        Vector3 normal = Vector3.Cross(b - a, c - a);
        return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.forward;
    }

    private static string GuessPlateName(Vector3 normal, int index)
    {
        Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        string prefix;
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            prefix = normal.x >= 0f ? "Правый борт" : "Левый борт";
        }
        else if (abs.y >= abs.x && abs.y >= abs.z)
        {
            prefix = normal.y >= 0f ? "Верхняя грань" : "Нижняя грань";
        }
        else
        {
            prefix = normal.z >= 0f ? "Корма" : "Лоб";
        }

        return $"{prefix} {index:00}";
    }

    private void OnDrawGizmos()
    {
        if (!drawPlates || drawOnlyWhenSelected) return;
        DrawPlateGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPlates) return;
        DrawPlateGizmos();
    }

    private void DrawPlateGizmos()
    {
        Mesh mesh = GetMesh();
        if (mesh == null || plates == null) return;

        int drawn = 0;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < plates.Count && drawn < maxDrawnTriangles; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate == null || plate.triangleIndices == null) continue;

            Gizmos.color = new Color(plate.debugColor.r, plate.debugColor.g, plate.debugColor.b, 0.9f);
            for (int j = 0; j < plate.triangleIndices.Count && drawn < maxDrawnTriangles; j++)
            {
                int offset = plate.triangleIndices[j] * 3;
                if (offset < 0 || offset + 2 >= triangles.Length) continue;

                Vector3 a = transform.TransformPoint(vertices[triangles[offset]]);
                Vector3 b = transform.TransformPoint(vertices[triangles[offset + 1]]);
                Vector3 c = transform.TransformPoint(vertices[triangles[offset + 2]]);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, a);
                drawn++;
            }
        }
    }

    private class BuildPlateGroup
    {
        public readonly Vector3 normal;
        public readonly float plane;
        public Vector3 center;
        public readonly List<int> triangleIndices = new List<int>();

        public BuildPlateGroup(Vector3 normal, float plane)
        {
            this.normal = normal;
            this.plane = plane;
        }
    }
}
