using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipAssemblyRuntime : MonoBehaviour
{
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
