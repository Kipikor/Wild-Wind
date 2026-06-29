using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipAssemblyRuntime : MonoBehaviour
{
    private static Material runtimeShipHullMaterial;
    private static Material runtimeShipTrimMaterial;
    private static Material runtimeShipGlassMaterial;
    private static Material runtimeShipAccentMaterial;

    [Header("Сборка корабля")]
    [InspectorName("Физика корабля")]
    [Tooltip("Компонент физики, на который применяются итоговые характеристики сборки.")]
    public ShipPhysics shipPhysics;

    [InspectorName("Корень модулей")]
    [Tooltip("Запасной родитель для визуальных модулей, если нужный сокет не найден. Обычно можно оставить пустым.")]
    public Transform moduleRoot;

    [InspectorName("Очищать старые модули")]
    [Tooltip("Если включено, при пересборке удаляются визуальные модули, созданные прошлой сборкой.")]
    public bool clearOldModules = true;

    [SerializeField] private List<GameObject> spawnedModules = new List<GameObject>();
    [SerializeField] private GameObject spawnedHullVisualObject;
    [SerializeField] private string spawnedHullVisualPartId = "";
    private readonly List<Renderer> hiddenFallbackRenderers = new List<Renderer>();

    private void Reset()
    {
        shipPhysics = GetComponent<ShipPhysics>();
    }

    public bool ApplyAssembly(ShipAssemblyResult result, ShipCatalogSO catalog, out string message)
    {
        message = "";
        if (result == null || !result.isValid)
        {
            message = "Нет готовой сборки корабля.";
            return false;
        }

        if (shipPhysics == null)
        {
            shipPhysics = GetComponent<ShipPhysics>();
        }

        if (shipPhysics == null)
        {
            message = "На префабе корпуса нет компонента ShipPhysics.";
            return false;
        }

        result.stats.ApplyTo(shipPhysics);

        if (clearOldModules)
        {
            ClearSpawnedModules();
        }

        int spawnedCount = 0;
        int missingVisuals = 0;
        int missingSockets = 0;

        for (int i = 0; i < result.installedModules.Count; i++)
        {
            InstalledModuleState installed = result.installedModules[i];
            ShipPartDefinitionSO module = catalog != null ? catalog.GetPartById(installed.moduleId) : null;
            if (module == null || module.prefab == null)
            {
                missingVisuals++;
                continue;
            }

            Transform parent = FindMountPoint(installed.slotId);
            if (parent == null)
            {
                parent = moduleRoot != null ? moduleRoot : transform;
                missingSockets++;
            }

            GameObject spawned = Instantiate(module.prefab, parent);
            spawned.name = module.displayName;
            spawned.transform.localPosition = Vector3.zero;
            spawned.transform.localRotation = Quaternion.identity;
            spawned.transform.localScale = Vector3.one;
            spawnedModules.Add(spawned);
            spawnedCount++;
        }

        ApplyHullVisual(result.hull);

        message = "Визуальная сборка применена: " + spawnedCount;
        if (missingVisuals > 0)
        {
            message += ", без префаба: " + missingVisuals;
        }

        if (missingSockets > 0)
        {
            message += ", без точного сокета: " + missingSockets;
        }

        return true;
    }

    public void ClearSpawnedModules()
    {
        for (int i = spawnedModules.Count - 1; i >= 0; i--)
        {
            GameObject spawned = spawnedModules[i];
            if (spawned == null) continue;

            if (Application.isPlaying)
            {
                Destroy(spawned);
            }
            else
            {
                DestroyImmediate(spawned);
            }
        }

        spawnedModules.Clear();
    }

    private void ApplyHullVisual(ShipPartDefinitionSO hull)
    {
        RestoreFallbackRenderers();
        ClearSpawnedHullVisual();

        if (hull == null || hull.visualPrefab == null)
        {
            return;
        }

        spawnedHullVisualObject = Instantiate(hull.visualPrefab, transform);
        spawnedHullVisualObject.name = hull.visualPrefab.name + " Visual";
        spawnedHullVisualObject.transform.localPosition = hull.visualLocalPosition;
        spawnedHullVisualObject.transform.localRotation = Quaternion.Euler(hull.visualLocalEulerAngles);
        spawnedHullVisualObject.transform.localScale = SanitizeVisualScale(hull.visualLocalScale);
        spawnedHullVisualPartId = hull.partId ?? "";
        ApplyHullVisualFallbackMaterials(spawnedHullVisualObject);

        if (hull.hidePrefabRenderersWhenVisualPrefabSet && HasEnabledRenderers(spawnedHullVisualObject))
        {
            HideFallbackRenderers(spawnedHullVisualObject.transform);
        }

        if (shipPhysics != null)
        {
            shipPhysics.BindGunGroupsFromVisual(spawnedHullVisualObject.transform);
        }
    }

    private void ClearSpawnedHullVisual()
    {
        if (spawnedHullVisualObject == null)
        {
            spawnedHullVisualPartId = "";
            return;
        }

        GameObject oldVisual = spawnedHullVisualObject;
        spawnedHullVisualObject = null;
        spawnedHullVisualPartId = "";

        if (Application.isPlaying)
        {
            Destroy(oldVisual);
        }
        else
        {
            DestroyImmediate(oldVisual);
        }
    }

    private void HideFallbackRenderers(Transform visualRoot)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform == null) continue;
            if (visualRoot != null && renderer.transform.IsChildOf(visualRoot)) continue;
            if (!renderer.enabled) continue;

            renderer.enabled = false;
            hiddenFallbackRenderers.Add(renderer);
        }
    }

    private void RestoreFallbackRenderers()
    {
        for (int i = hiddenFallbackRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = hiddenFallbackRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        hiddenFallbackRenderers.Clear();
    }

    private static bool HasEnabledRenderers(GameObject root)
    {
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 SanitizeVisualScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static void ApplyHullVisualFallbackMaterials(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = ResolveFallbackMaterialForRenderer(renderer);
                continue;
            }

            bool changed = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (!ShouldReplaceVisualMaterial(materials[materialIndex]))
                {
                    continue;
                }

                materials[materialIndex] = ResolveFallbackMaterialForRenderer(renderer);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool ShouldReplaceVisualMaterial(Material material)
    {
        if (material == null || material.shader == null)
        {
            return true;
        }

        string materialName = material.name ?? "";
        if (materialName.IndexOf("Default", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            materialName.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!TryGetMaterialColor(material, out Color color))
        {
            return false;
        }

        float max = Mathf.Max(color.r, color.g, color.b);
        float min = Mathf.Min(color.r, color.g, color.b);
        bool flatBright = max >= 0.78f && max - min <= 0.12f;
        bool flatGray = max - min <= 0.035f && max >= 0.42f;
        return flatBright || flatGray;
    }

    private static bool TryGetMaterialColor(Material material, out Color color)
    {
        color = Color.white;
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        return false;
    }

    private static Material ResolveFallbackMaterialForRenderer(Renderer renderer)
    {
        string name = renderer != null ? renderer.name : "";
        if (ContainsAny(name, "Muzzle", "Barrel", "Turret", "Weapon", "Mantlet", "MG_", "76mm", "20mm", "Antenna"))
        {
            return GetOrCreateFallbackMaterial(ref runtimeShipTrimMaterial, "Runtime Ship Weapon Dark", new Color(0.055f, 0.07f, 0.085f, 1f), 0.08f, 0.42f);
        }

        if (ContainsAny(name, "Bridge", "Window", "Glass", "Cabin"))
        {
            return GetOrCreateFallbackMaterial(ref runtimeShipGlassMaterial, "Runtime Ship Warm Glass", new Color(0.95f, 0.62f, 0.26f, 1f), 0f, 0.65f);
        }

        if (ContainsAny(name, "Blue", "Armor", "Cap"))
        {
            return GetOrCreateFallbackMaterial(ref runtimeShipAccentMaterial, "Runtime Ship Blue Armor", new Color(0.18f, 0.28f, 0.42f, 1f), 0.04f, 0.38f);
        }

        return GetOrCreateFallbackMaterial(ref runtimeShipHullMaterial, "Runtime Ship Hull Steel", new Color(0.48f, 0.56f, 0.62f, 1f), 0.03f, 0.34f);
    }

    private static Material GetOrCreateFallbackMaterial(ref Material material, string name, Color color, float metallic, float smoothness)
    {
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || !shader.isSupported)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };
        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(value) || parts == null)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (!string.IsNullOrWhiteSpace(part) &&
                value.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private Transform FindMountPoint(string slotId)
    {
        ShipSocket[] sockets = GetComponentsInChildren<ShipSocket>(true);
        for (int i = 0; i < sockets.Length; i++)
        {
            ShipSocket socket = sockets[i];
            if (socket != null && socket.slotId == slotId)
            {
                return socket.MountPoint;
            }
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            ShipSocket socket = sockets[i];
            if (socket != null && !string.IsNullOrWhiteSpace(socket.slotId) && slotId.EndsWith(":" + socket.slotId))
            {
                return socket.MountPoint;
            }
        }

        return null;
    }
}
