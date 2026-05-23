using UnityEngine;

public class ShipLoader : MonoBehaviour
{
    [Header("Каталог и цель")]
    public ShipCatalogSO catalog;
    [Tooltip("Активный корабль игрока. Для новой сборки сюда попадет созданный корпус-префаб.")]
    public ShipPhysics targetShip;
    [Tooltip("Точка, где создается корпус-префаб. Если пусто, используется позиция старого сценового корабля или этого объекта.")]
    public Transform spawnPoint;
    [Tooltip("Родитель для созданного корпуса. Если пусто, корпус создается в корне сцены.")]
    public Transform spawnedParent;

    [Header("Поведение спавнера")]
    [Tooltip("Удалять ранее созданный корпус при смене корпуса в сборке.")]
    public bool destroySpawnedShipOnRebuild = true;
    [Tooltip("Отключать старый сценовый корабль после создания корпуса-префаба.")]
    public bool disableSceneShipWhenSpawning = true;
    [SerializeField] private string spawnedHullId = "";
    [SerializeField] private GameObject spawnedShipObject;

    private void Reset()
    {
        targetShip = FindFirstObjectByType<ShipPhysics>();
    }

    public bool ApplyAssembly(PlayerProgress progress, TechTreeDefinitionSO techTree, out string message)
    {
        message = "";
        if (catalog == null || !catalog.HasAssemblyParts())
        {
            message = "В каталоге нет деталей для новой сборки.";
            return false;
        }

        if (!ShipAssemblyBuilder.TryBuild(catalog, techTree, progress, out ShipAssemblyResult result))
        {
            message = result.message;
            return false;
        }

        ShipPhysics ship = EnsureShipForHull(result.hull, out string spawnMessage);
        if (ship == null)
        {
            message = spawnMessage;
            return false;
        }

        ShipAssemblyRuntime runtime = ship.GetComponent<ShipAssemblyRuntime>();
        if (runtime == null)
        {
            runtime = ship.gameObject.AddComponent<ShipAssemblyRuntime>();
        }

        runtime.shipPhysics = ship;
        bool visualApplied = runtime.ApplyAssembly(result, catalog, out string visualMessage);
        targetShip = ship;

        message = result.message;
        if (!string.IsNullOrWhiteSpace(spawnMessage))
        {
            message += " " + spawnMessage;
        }

        if (!string.IsNullOrWhiteSpace(visualMessage))
        {
            message += " " + visualMessage;
        }

        return visualApplied;
    }

    private ShipPhysics EnsureShipForHull(ShipPartDefinitionSO hull, out string message)
    {
        message = "";
        if (hull == null)
        {
            message = "Корпус сборки не найден.";
            return null;
        }

        if (hull.prefab == null)
        {
            if (targetShip == null)
            {
                targetShip = FindFirstObjectByType<ShipPhysics>();
            }

            if (targetShip == null)
            {
                message = "У корпуса не задан префаб, и в сцене нет запасного корабля.";
                return null;
            }

            message = "У корпуса нет префаба, применены только характеристики к сценовому кораблю.";
            EnsureRuntimeComponents(targetShip);
            return targetShip;
        }

        if (spawnedShipObject != null && targetShip != null && spawnedHullId == hull.partId)
        {
            EnsureRuntimeComponents(targetShip);
            return targetShip;
        }

        ShipPhysics previousShip = targetShip != null ? targetShip : FindFirstObjectByType<ShipPhysics>();
        GameObject previousObject = previousShip != null ? previousShip.gameObject : null;
        bool previousWasSpawned = previousObject != null && previousObject == spawnedShipObject;

        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : previousShip != null ? previousShip.transform.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null
            ? spawnPoint.rotation
            : previousShip != null ? previousShip.transform.rotation : transform.rotation;

        if (destroySpawnedShipOnRebuild)
        {
            DestroySpawnedShip();
        }

        GameObject instance = Instantiate(hull.prefab, spawnPosition, spawnRotation, spawnedParent);
        instance.name = hull.displayName;

        ShipPhysics ship = instance.GetComponent<ShipPhysics>();
        if (ship == null)
        {
            ship = instance.GetComponentInChildren<ShipPhysics>();
        }

        if (ship == null)
        {
            ship = instance.AddComponent<ShipPhysics>();
        }

        EnsureRuntimeComponents(ship);

        spawnedShipObject = ship.gameObject;
        spawnedHullId = hull.partId;
        targetShip = ship;

        if (disableSceneShipWhenSpawning && previousObject != null && !previousWasSpawned && previousObject != ship.gameObject)
        {
            previousObject.SetActive(false);
        }

        message = "Создан корпус-префаб: " + hull.displayName + ".";
        return ship;
    }

    private static void EnsureRuntimeComponents(ShipPhysics ship)
    {
        if (ship == null) return;

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = ship.gameObject.AddComponent<Rigidbody>();
        }

        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (ship.GetComponent<ShipAssemblyRuntime>() == null)
        {
            ship.gameObject.AddComponent<ShipAssemblyRuntime>();
        }
    }

    private void DestroySpawnedShip()
    {
        if (spawnedShipObject == null) return;

        GameObject oldShip = spawnedShipObject;
        spawnedShipObject = null;
        spawnedHullId = "";

        if (Application.isPlaying)
        {
            Destroy(oldShip);
        }
        else
        {
            DestroyImmediate(oldShip);
        }
    }
}
