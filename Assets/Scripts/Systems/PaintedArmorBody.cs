using System;
using System.Collections.Generic;
using UnityEngine;

public enum PaintedArmorBoxFace
{
    [InspectorName("Лоб (-Z)")]
    FrontNegativeZ,
    [InspectorName("Корма (+Z)")]
    RearPositiveZ,
    [InspectorName("Левый борт (-X)")]
    LeftNegativeX,
    [InspectorName("Правый борт (+X)")]
    RightPositiveX,
    [InspectorName("Верхняя палуба (+Y)")]
    TopPositiveY,
    [InspectorName("Нижняя броня (-Y)")]
    BottomNegativeY
}

[Serializable]
public class PaintedArmorFace
{
    [InspectorName("Грань")]
    [Tooltip("Какая сторона бронекороба использует эти параметры. Это пока простой вариант будущей покраски граней модели.")]
    public PaintedArmorBoxFace face = PaintedArmorBoxFace.FrontNegativeZ;
    [InspectorName("ID бронезоны")]
    [Tooltip("Технический id зоны. Используется в логах, сохранениях, правилах урона и будущих настройках ремонта.")]
    public string zoneId = "front";
    [InspectorName("Название")]
    [Tooltip("Человеческое название зоны, которое видно в инспекторе и сообщениях попаданий.")]
    public string displayNameRu = "Лобовая броня";
    [InspectorName("Толщина брони, мм")]
    [Tooltip("Реальная толщина целого бронелиста. Расчетная толщина = эта толщина * процент остатка прочности бронелиста.")]
    [Min(0f)] public float armorMm = 40f;
    [InspectorName("Максимальная прочность бронелиста")]
    [Tooltip("Прочность этой грани как отдельного бронелиста. Когда прочность падает до 0, расчетная толщина становится 0.")]
    [Min(1f)] public float maxArmorHp = 100f;
    [InspectorName("Текущая прочность бронелиста")]
    [Tooltip("Текущая прочность бронелиста. Чем она ниже, тем меньше расчетная толщина брони.")]
    [Min(0f)] public float armorHp = 100f;
    [InspectorName("Угол рикошета, град")]
    [Tooltip("Если угол встречи больше этого значения, бронебойное попадание уходит в рикошет.")]
    [Range(0f, 89f)] public float ricochetAngleDeg = 70f;
    [InspectorName("Цвет отладки")]
    [Tooltip("Цвет этой грани в тестовом бронекоробе и gizmo-подсветке.")]
    public Color debugColor = new Color(1f, 0.6f, 0.1f, 0.35f);

    public float ArmorIntegrity01 => maxArmorHp > 0.001f ? Mathf.Clamp01(armorHp / maxArmorHp) : 0f;
    public float CurrentArmorMm => Mathf.Max(0f, armorMm) * ArmorIntegrity01;
    public bool IsDestroyed => ArmorIntegrity01 <= 0.001f;

    public ArmorSurface ToSurface()
    {
        return new ArmorSurface
        {
            zoneId = zoneId,
            displayNameRu = displayNameRu,
            armorMm = CurrentArmorMm,
            baseArmorMm = armorMm,
            armorIntegrity01 = ArmorIntegrity01,
            ricochetAngleDeg = ricochetAngleDeg,
            overmatchCaliberMultiplier = 0f,
            structureDamageMultiplier = 1f,
            highExplosiveSurfaceDamageMultiplier = 1f,
            ramDamageMultiplier = 1f
        };
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
[RequireComponent(typeof(BoxCollider))]
public class PaintedArmorBody : MonoBehaviour
{
    [Header("Броневая оболочка")]
    [InspectorName("Корабль-владелец")]
    [Tooltip("DamageableShip, который получит урон при попадании в эту броневую оболочку.")]
    public DamageableShip owner;
    [InspectorName("Размер бронекороба")]
    [Tooltip("Размер простой броневой оболочки. Она отделена от физики полета и нужна только для попаданий.")]
    public Vector3 boxSize = new Vector3(18f, 6f, 10f);
    [InspectorName("Покрашенные грани")]
    [Tooltip("Настройки брони для каждой стороны коробки. Позже этот же подход расширим до покраски граней настоящей модели.")]
    public List<PaintedArmorFace> faces = new List<PaintedArmorFace>();

    [Header("Отладка")]
    [InspectorName("Показывать грани")]
    [Tooltip("Рисует цветные gizmo-подсказки граней бронекороба в сцене.")]
    public bool drawFaces = true;
    [InspectorName("Показывать только при выборе")]
    [Tooltip("Если включено, цветная подсветка граней видна только когда объект выбран.")]
    public bool drawOnlyWhenSelected = false;
    [InspectorName("Автопересборка визуала")]
    [Tooltip("Автоматически обновляет цветной визуал бронекороба при изменении параметров в инспекторе.")]
    public bool rebuildVisualMeshOnValidate = true;

    private BoxCollider cachedCollider;

    private void Reset()
    {
        owner = GetComponentInParent<DamageableShip>();
        EnsureDefaultFaces();
        EnsureCollider();
        RebuildVisualMesh();
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<DamageableShip>();
        }

        EnsureDefaultFaces();
        EnsureCollider();
    }

    private void OnValidate()
    {
        boxSize = new Vector3(
            Mathf.Max(0.1f, boxSize.x),
            Mathf.Max(0.1f, boxSize.y),
            Mathf.Max(0.1f, boxSize.z));
        EnsureDefaultFaces();
        ClampFaceState();
        EnsureCollider();

        if (rebuildVisualMeshOnValidate
            && GetComponent<MeshFilter>() != null
            && GetComponent<MeshRenderer>() != null)
        {
            RebuildVisualMesh();
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
            : EstimateSurfaceNormal(hit.point);

        return ReceiveHit(context);
    }

    public DamageHitResult ReceiveHit(DamageHitContext context)
    {
        DamageableShip target = owner != null ? owner : GetComponentInParent<DamageableShip>();
        if (target == null)
        {
            return new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "Попадание в броневую оболочку без DamageableShip."
            };
        }

        PaintedArmorFace face = FindFace(context.hitPoint, context.hitNormal);
        if (face == null)
        {
            return new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "Броневая грань для попадания не найдена."
            };
        }

        if (context.hitNormal.sqrMagnitude < 0.001f)
        {
            context.hitNormal = GetWorldNormal(face.face);
        }

        context.internalTravelDistance = CalculateExitDistance(context.hitPoint, context.incomingDirection);
        context.deferResultLogging = true;

        DamageHitResult result = target.ApplyHit(face.ToSurface(), context);
        float armorDamageMultiplier = GetArmorDamageMultiplier(result.outcome);
        float plateDamage = face.ApplyArmorPlateDamage(context.armorPlateDamage * armorDamageMultiplier);
        result.armorPlateDamage = plateDamage;
        result.remainingArmorPlateHp = face.armorHp;
        result.maxArmorPlateHp = face.maxArmorHp;
        result.message += $" Бронелист: -{plateDamage:0.0}, осталось {face.armorHp:0.0}/{face.maxArmorHp:0.0}, расчетная толщина {face.CurrentArmorMm:0.0} мм.";
        target.FinalizeHitResult(result);
        return result;
    }

    public void ResetArmorState()
    {
        EnsureDefaultFaces();
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i] != null)
            {
                faces[i].ResetArmorHp();
            }
        }

        RebuildVisualMesh();
    }

    public Vector3 EstimateSurfaceNormal(Vector3 hitPoint)
    {
        PaintedArmorFace face = FindFace(hitPoint, Vector3.zero);
        return face != null ? GetWorldNormal(face.face) : transform.forward;
    }

    public PaintedArmorFace FindFace(Vector3 hitPoint, Vector3 hitNormal)
    {
        PaintedArmorBoxFace side = DetermineFace(hitPoint, hitNormal);
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i] != null && faces[i].face == side)
            {
                return faces[i];
            }
        }

        return faces.Count > 0 ? faces[0] : null;
    }

    public void EnsureCollider()
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<BoxCollider>();
        }

        if (cachedCollider == null) return;

        cachedCollider.center = Vector3.zero;
        cachedCollider.size = boxSize;
        cachedCollider.isTrigger = true;
    }

    public void RebuildVisualMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        Mesh mesh = BuildBoxMesh();
        mesh.name = "Painted Armor Box";
        meshFilter.sharedMesh = mesh;

        Material[] materials = new Material[6];
        for (int i = 0; i < 6; i++)
        {
            PaintedArmorFace face = GetFace((PaintedArmorBoxFace)i);
            Color color = face != null ? face.debugColor : Color.gray;
            materials[i] = CreateMaterial(color);
        }

        meshRenderer.sharedMaterials = materials;
    }

    private PaintedArmorBoxFace DetermineFace(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitNormal.sqrMagnitude > 0.001f)
        {
            Vector3 localNormal = transform.InverseTransformDirection(hitNormal.normalized);
            Vector3 absNormal = new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z));
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
            {
                return localNormal.x >= 0f ? PaintedArmorBoxFace.RightPositiveX : PaintedArmorBoxFace.LeftNegativeX;
            }

            if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
            {
                return localNormal.y >= 0f ? PaintedArmorBoxFace.TopPositiveY : PaintedArmorBoxFace.BottomNegativeY;
            }

            return localNormal.z >= 0f ? PaintedArmorBoxFace.RearPositiveZ : PaintedArmorBoxFace.FrontNegativeZ;
        }

        Vector3 localPoint = transform.InverseTransformPoint(hitPoint);
        Vector3 half = Vector3.Max(boxSize * 0.5f, Vector3.one * 0.001f);
        Vector3 normalized = new Vector3(localPoint.x / half.x, localPoint.y / half.y, localPoint.z / half.z);
        Vector3 abs = new Vector3(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y), Mathf.Abs(normalized.z));

        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return normalized.x >= 0f ? PaintedArmorBoxFace.RightPositiveX : PaintedArmorBoxFace.LeftNegativeX;
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return normalized.y >= 0f ? PaintedArmorBoxFace.TopPositiveY : PaintedArmorBoxFace.BottomNegativeY;
        }

        return normalized.z >= 0f ? PaintedArmorBoxFace.RearPositiveZ : PaintedArmorBoxFace.FrontNegativeZ;
    }

    private PaintedArmorFace GetFace(PaintedArmorBoxFace side)
    {
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i] != null && faces[i].face == side)
            {
                return faces[i];
            }
        }

        return null;
    }

    private Vector3 GetWorldNormal(PaintedArmorBoxFace side)
    {
        return transform.TransformDirection(GetLocalNormal(side)).normalized;
    }

    private static Vector3 GetLocalNormal(PaintedArmorBoxFace side)
    {
        switch (side)
        {
            case PaintedArmorBoxFace.RearPositiveZ:
                return Vector3.forward;
            case PaintedArmorBoxFace.LeftNegativeX:
                return Vector3.left;
            case PaintedArmorBoxFace.RightPositiveX:
                return Vector3.right;
            case PaintedArmorBoxFace.TopPositiveY:
                return Vector3.up;
            case PaintedArmorBoxFace.BottomNegativeY:
                return Vector3.down;
            default:
                return Vector3.back;
        }
    }

    private Mesh BuildBoxMesh()
    {
        Vector3 half = boxSize * 0.5f;
        Vector3[] vertices = new Vector3[24];
        int[][] faceTriangles = new int[6][];

        SetFace(vertices, faceTriangles, 0, new Vector3(-half.x, -half.y, -half.z), new Vector3(-half.x, half.y, -half.z), new Vector3(half.x, half.y, -half.z), new Vector3(half.x, -half.y, -half.z));
        SetFace(vertices, faceTriangles, 1, new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z), new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z));
        SetFace(vertices, faceTriangles, 2, new Vector3(-half.x, -half.y, -half.z), new Vector3(-half.x, -half.y, half.z), new Vector3(-half.x, half.y, half.z), new Vector3(-half.x, half.y, -half.z));
        SetFace(vertices, faceTriangles, 3, new Vector3(half.x, -half.y, -half.z), new Vector3(half.x, half.y, -half.z), new Vector3(half.x, half.y, half.z), new Vector3(half.x, -half.y, half.z));
        SetFace(vertices, faceTriangles, 4, new Vector3(-half.x, half.y, -half.z), new Vector3(-half.x, half.y, half.z), new Vector3(half.x, half.y, half.z), new Vector3(half.x, half.y, -half.z));
        SetFace(vertices, faceTriangles, 5, new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z), new Vector3(half.x, -half.y, half.z), new Vector3(-half.x, -half.y, half.z));

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.subMeshCount = 6;
        for (int i = 0; i < 6; i++)
        {
            mesh.SetTriangles(faceTriangles[i], i);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetFace(Vector3[] vertices, int[][] triangles, int faceIndex, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int offset = faceIndex * 4;
        vertices[offset] = a;
        vertices[offset + 1] = b;
        vertices[offset + 2] = c;
        vertices[offset + 3] = d;
        triangles[faceIndex] = new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 };
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        return material;
    }

    private void EnsureDefaultFaces()
    {
        if (faces == null)
        {
            faces = new List<PaintedArmorFace>();
        }

        EnsureFace(PaintedArmorBoxFace.FrontNegativeZ, "front", "Лобовая плита", 65f, Color.red);
        EnsureFace(PaintedArmorBoxFace.RearPositiveZ, "rear", "Корма", 28f, Color.yellow);
        EnsureFace(PaintedArmorBoxFace.LeftNegativeX, "left", "Левый борт", 38f, new Color(1f, 0.5f, 0.1f));
        EnsureFace(PaintedArmorBoxFace.RightPositiveX, "right", "Правый борт", 38f, new Color(1f, 0.5f, 0.1f));
        EnsureFace(PaintedArmorBoxFace.TopPositiveY, "top", "Верхняя палуба", 22f, Color.cyan);
        EnsureFace(PaintedArmorBoxFace.BottomNegativeY, "bottom", "Нижняя броня", 18f, Color.blue);
    }

    private void EnsureFace(PaintedArmorBoxFace side, string id, string nameRu, float armorMm, Color color)
    {
        if (GetFace(side) != null) return;

        faces.Add(new PaintedArmorFace
        {
            face = side,
            zoneId = id,
            displayNameRu = nameRu,
            armorMm = armorMm,
            maxArmorHp = Mathf.Max(50f, armorMm * 4f),
            armorHp = Mathf.Max(50f, armorMm * 4f),
            ricochetAngleDeg = 70f,
            debugColor = new Color(color.r, color.g, color.b, 0.75f)
        });
    }

    private void ClampFaceState()
    {
        if (faces == null) return;
        for (int i = 0; i < faces.Count; i++)
        {
            PaintedArmorFace face = faces[i];
            if (face == null) continue;
            face.maxArmorHp = Mathf.Max(1f, face.maxArmorHp);
            face.armorHp = Mathf.Clamp(face.armorHp, 0f, face.maxArmorHp);
        }
    }

    private float CalculateExitDistance(Vector3 worldPoint, Vector3 worldDirection)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
        Vector3 half = Vector3.Max(boxSize * 0.5f, Vector3.one * 0.001f);
        float best = float.PositiveInfinity;

        CheckAxis(localPoint.x, localDirection.x, half.x, ref best);
        CheckAxis(localPoint.y, localDirection.y, half.y, ref best);
        CheckAxis(localPoint.z, localDirection.z, half.z, ref best);

        if (float.IsInfinity(best) || best <= 0.001f)
        {
            return boxSize.magnitude;
        }

        return Mathf.Max(0.1f, best * transform.lossyScale.magnitude / Mathf.Sqrt(3f));
    }

    private static void CheckAxis(float point, float direction, float half, ref float best)
    {
        if (Mathf.Abs(direction) <= 0.0001f) return;

        float plane = direction > 0f ? half : -half;
        float distance = (plane - point) / direction;
        if (distance > 0.001f && distance < best)
        {
            best = distance;
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

    private void OnDrawGizmos()
    {
        if (!drawFaces || drawOnlyWhenSelected) return;
        DrawFaceGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFaces) return;
        DrawFaceGizmos();
    }

    private void DrawFaceGizmos()
    {
        if (faces == null) return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        for (int i = 0; i < faces.Count; i++)
        {
            PaintedArmorFace face = faces[i];
            if (face == null) continue;

            Gizmos.color = new Color(face.debugColor.r, face.debugColor.g, face.debugColor.b, 0.18f);
            Vector3 center;
            Vector3 size;
            GetFaceCube(face.face, out center, out size);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(face.debugColor.r, face.debugColor.g, face.debugColor.b, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }

        Gizmos.matrix = previous;
    }

    private void GetFaceCube(PaintedArmorBoxFace side, out Vector3 center, out Vector3 size)
    {
        Vector3 half = boxSize * 0.5f;
        float thickness = 0.06f;
        switch (side)
        {
            case PaintedArmorBoxFace.RearPositiveZ:
                center = new Vector3(0f, 0f, half.z);
                size = new Vector3(boxSize.x, boxSize.y, thickness);
                break;
            case PaintedArmorBoxFace.LeftNegativeX:
                center = new Vector3(-half.x, 0f, 0f);
                size = new Vector3(thickness, boxSize.y, boxSize.z);
                break;
            case PaintedArmorBoxFace.RightPositiveX:
                center = new Vector3(half.x, 0f, 0f);
                size = new Vector3(thickness, boxSize.y, boxSize.z);
                break;
            case PaintedArmorBoxFace.TopPositiveY:
                center = new Vector3(0f, half.y, 0f);
                size = new Vector3(boxSize.x, thickness, boxSize.z);
                break;
            case PaintedArmorBoxFace.BottomNegativeY:
                center = new Vector3(0f, -half.y, 0f);
                size = new Vector3(boxSize.x, thickness, boxSize.z);
                break;
            default:
                center = new Vector3(0f, 0f, -half.z);
                size = new Vector3(boxSize.x, boxSize.y, thickness);
                break;
        }
    }
}
