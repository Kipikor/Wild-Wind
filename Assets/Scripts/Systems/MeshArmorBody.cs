using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    [InspectorName("Треугольники mesh")]
    public List<int> triangleIndices = new List<int>();
    [InspectorName("Цвет отладки")]
    public Color debugColor = new Color(1f, 0.6f, 0.1f, 0.35f);

    public ArmorSurface ToSurface(float ricochetAngleDeg)
    {
        return new ArmorSurface
        {
            zoneId = plateId,
            displayNameRu = displayNameRu,
            armorMm = Mathf.Max(0f, armorMm),
            ricochetAngleDeg = ricochetAngleDeg,
            overmatchCaliberMultiplier = 0f,
            structureDamageMultiplier = 1f,
            highExplosiveSurfaceDamageMultiplier = 1f,
            ramDamageMultiplier = 1f
        };
    }

    public bool ContainsTriangle(int triangleIndex)
    {
        return triangleIndices != null && triangleIndices.Contains(triangleIndex);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
public class MeshArmorBody : MonoBehaviour
{
    private static readonly Regex ArmorValueRegex = new Regex(@"(?<value>\d+(?:[\.,]\d+)?)", RegexOptions.Compiled);

    [Header("Броневая mesh-оболочка")]
    [InspectorName("Корабль-владелец")]
    public DamageableShip owner;
    [InspectorName("MeshCollider")]
    public MeshCollider meshCollider;
    [InspectorName("Сделать коллайдер триггером")]
    [Tooltip("Для статичной mesh-брони обычно лучше оставить выключенным. Raycast попаданий работает и по обычному коллайдеру.")]
    public bool colliderIsTrigger;
    [InspectorName("Convex коллайдер для физики")]
    [Tooltip("Нужно включать, если этот mesh должен двигаться от Rigidbody. Unity не поддерживает динамический Rigidbody с non-convex MeshCollider.")]
    public bool convexColliderForPhysics;
    [InspectorName("Бронелисты")]
    public List<MeshArmorPlate> plates = new List<MeshArmorPlate>();
    [InspectorName("Угол рикошета, град")]
    [Tooltip("Общий угол авторикошета для всех бронелистов этой mesh-брони.")]
    [Range(0f, 89f)] public float ricochetAngleDeg = 70f;

    [Header("Автосборка")]
    [InspectorName("Порог угла нормали")]
    [Tooltip("Треугольники с похожей нормалью считаются одной плоской гранью.")]
    [Range(1f, 30f)] public float normalAngleThresholdDeg = 8f;
    [InspectorName("Порог расстояния плоскости")]
    [Tooltip("Параллельные, но разнесенные плоскости становятся разными бронелистами.")]
    [Min(0.001f)] public float planeDistanceThreshold = 0.05f;
    [InspectorName("Броня по умолчанию, мм")]
    [Min(0f)] public float defaultArmorMm = 40f;
    [InspectorName("Use material armor names")]
    public bool useMaterialArmorNames = true;

    [Header("Отладка")]
    [InspectorName("Показывать бронелисты")]
    public bool drawPlates = true;
    [InspectorName("Показывать только при выборе")]
    public bool drawOnlyWhenSelected = true;
    [InspectorName("Рисовать не больше треугольников")]
    [Min(1)] public int maxDrawnTriangles = 180;

    private MeshFilter meshFilter;
    [NonSerialized] private bool lastRebuildUsedMaterialArmorNames;

    public bool LastRebuildUsedMaterialArmorNames => lastRebuildUsedMaterialArmorNames;

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
        planeDistanceThreshold = Mathf.Max(0.001f, planeDistanceThreshold);
        ricochetAngleDeg = Mathf.Clamp(ricochetAngleDeg, 0f, 89f);
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
        meshCollider.convex = convexColliderForPhysics;
        meshCollider.isTrigger = colliderIsTrigger;
    }

    public void RebuildPlatesFromMesh()
    {
        lastRebuildUsedMaterialArmorNames = false;
        Mesh mesh = GetMesh();
        if (mesh == null)
        {
            plates = new List<MeshArmorPlate>();
            return;
        }

        if (!mesh.isReadable)
        {
            plates = new List<MeshArmorPlate>();
            Debug.LogWarning($"{name}: mesh '{mesh.name}' is not readable. Enable Read/Write on the model import settings before rebuilding mesh armor.", this);
            return;
        }

        if (useMaterialArmorNames && TryRebuildPlatesFromMaterials(mesh))
        {
            lastRebuildUsedMaterialArmorNames = true;
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
            Color color = Color.HSVToRGB((i * 0.137f) % 1f, 0.7f, 1f);
            string plateName = GuessPlateName(group.normal, i + 1);

            plates.Add(new MeshArmorPlate
            {
                plateId = $"mesh_plate_{i + 1:00}",
                displayNameRu = plateName,
                armorMm = defaultArmorMm,
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
            plate = FindPlateByNormal(context.hitNormal, context.hitPoint);
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

        DamageHitResult result = target.ApplyHit(plate.ToSurface(ricochetAngleDeg), context);
        target.FinalizeHitResult(result);
        return result;
    }

    public void ResetArmorState()
    {
        ClampPlateState();
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

    private MeshArmorPlate FindPlateByNormal(Vector3 worldNormal, Vector3 worldPoint)
    {
        Mesh mesh = GetMesh();
        if (mesh == null || !mesh.isReadable || plates == null || worldNormal.sqrMagnitude <= 0.001f) return null;

        Vector3 localNormal = transform.InverseTransformDirection(worldNormal.normalized);
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        bool hasPoint = worldPoint.sqrMagnitude > 0.000001f;
        MeshArmorPlate best = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < plates.Count; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate == null || plate.triangleIndices == null || plate.triangleIndices.Count == 0) continue;

            Vector3 plateNormal = CalculateTriangleNormal(mesh, plate.triangleIndices[0]);
            float angle = Mathf.Min(
                Vector3.Angle(localNormal, plateNormal),
                Vector3.Angle(-localNormal, plateNormal));
            float planeDistance = 0f;
            if (hasPoint)
            {
                Vector3 plateCenter = CalculateTriangleCenter(mesh, plate.triangleIndices[0]);
                planeDistance = Mathf.Abs(Vector3.Dot(localPoint - plateCenter, plateNormal));
            }

            float score = angle * 10f + planeDistance;
            if (score < bestScore)
            {
                bestScore = score;
                best = plate;
            }
        }

        return best;
    }

    private Vector3 EstimateWorldNormal(int triangleIndex)
    {
        Mesh mesh = GetMesh();
        if (mesh == null || !mesh.isReadable) return transform.forward;

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

    private bool TryRebuildPlatesFromMaterials(Mesh mesh)
    {
        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null || mesh.subMeshCount <= 0)
        {
            return false;
        }

        Material[] materials = meshRenderer.sharedMaterials;
        List<MeshArmorPlate> materialPlates = new List<MeshArmorPlate>();
        bool foundArmorMaterial = false;
        int triangleIndex = 0;

        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            int[] subMeshTriangles = mesh.GetTriangles(subMesh);
            int triangleCount = subMeshTriangles.Length / 3;
            if (triangleCount <= 0)
            {
                continue;
            }

            Material material = subMesh < materials.Length ? materials[subMesh] : null;
            string materialName = CleanMaterialName(material != null ? material.name : string.Empty);
            float armorMm;
            string zoneId;
            string displayName;
            bool parsed = TryParseArmorMaterialName(materialName, out armorMm, out zoneId, out displayName);
            if (parsed)
            {
                foundArmorMaterial = true;
            }
            else
            {
                armorMm = defaultArmorMm;
                zoneId = MakeSafeZoneId(string.IsNullOrWhiteSpace(materialName) ? $"submesh_{subMesh + 1:00}" : materialName);
                displayName = string.IsNullOrWhiteSpace(materialName) ? $"Submesh {subMesh + 1:00}" : materialName;
            }

            List<int> subMeshTriangleIndices = new List<int>(triangleCount);
            for (int i = 0; i < triangleCount; i++)
            {
                subMeshTriangleIndices.Add(triangleIndex + i);
            }

            triangleIndex += triangleCount;
            Color color = Color.HSVToRGB((materialPlates.Count * 0.137f) % 1f, 0.7f, 1f);
            materialPlates.Add(new MeshArmorPlate
            {
                plateId = zoneId,
                displayNameRu = displayName,
                armorMm = Mathf.Max(0f, armorMm),
                triangleIndices = subMeshTriangleIndices,
                debugColor = new Color(color.r, color.g, color.b, 0.35f)
            });
        }

        if (!foundArmorMaterial)
        {
            return false;
        }

        plates = materialPlates;
        return true;
    }

    private static bool TryParseArmorMaterialName(string materialName, out float armorMm, out string zoneId, out string displayName)
    {
        armorMm = 0f;
        zoneId = string.Empty;
        displayName = materialName;
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return false;
        }

        string lowerName = materialName.ToLowerInvariant();
        bool hasArmorMarker = lowerName.Contains("armor")
            || lowerName.Contains("armour")
            || lowerName.Contains("bron")
            || lowerName.StartsWith("br_", StringComparison.Ordinal)
            || lowerName.Contains("_br_");
        if (!hasArmorMarker)
        {
            return false;
        }

        Match match = ArmorValueRegex.Match(materialName);
        if (!match.Success)
        {
            return false;
        }

        string valueText = match.Groups["value"].Value.Replace(',', '.');
        if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out armorMm))
        {
            return false;
        }

        zoneId = MakeSafeZoneId(materialName);
        displayName = materialName;
        return true;
    }

    private static string CleanMaterialName(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return string.Empty;
        }

        return materialName.Replace(" (Instance)", string.Empty).Trim();
    }

    private static string MakeSafeZoneId(string source)
    {
        string value = CleanMaterialName(source).ToLowerInvariant();
        StringBuilder builder = new StringBuilder(value.Length + 8);
        bool lastWasSeparator = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        string id = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(id))
        {
            return "armor_material";
        }

        if (char.IsDigit(id[0]))
        {
            return "armor_" + id;
        }

        return id;
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
        if (plates == null)
        {
            plates = new List<MeshArmorPlate>();
        }

        for (int i = 0; i < plates.Count; i++)
        {
            MeshArmorPlate plate = plates[i];
            if (plate == null) continue;
            plate.armorMm = Mathf.Max(0f, plate.armorMm);
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

    private static Vector3 CalculateTriangleCenter(Mesh mesh, int triangleIndex)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int offset = triangleIndex * 3;
        if (offset < 0 || offset + 2 >= triangles.Length) return Vector3.zero;

        return (vertices[triangles[offset]]
            + vertices[triangles[offset + 1]]
            + vertices[triangles[offset + 2]]) / 3f;
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
        if (mesh == null || !mesh.isReadable || plates == null) return;

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
