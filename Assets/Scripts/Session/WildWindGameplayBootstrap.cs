using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildWindGameplayBootstrap
{
    private const string ShipLoaderObjectName = "Player Ship Loader";
    private const string PlayerShipProxyName = "Player Ship Proxy";
    private const string PlayerSessionShipName = "Player Session Ship";
    private const string RuntimePlayerShipName = "Active Korshun Session Ship";
    private const string RuntimeFallbackCatalogName = "Runtime Starter Ship Catalog";
    private const string RuntimeKorshunVisualModelId = "WW_Imperial_PatrolFrigate_R02_Korshun";
    public const int RuntimeQualityLevelIndex = 0;
    public const int RuntimeTargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallGameplayLaunchBootstrap()
    {
        ApplyRuntimeFramePolicy();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BootstrapGameplayLaunch();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyRuntimeFramePolicy();
        BootstrapGameplayLaunch();
    }

    private static void BootstrapGameplayLaunch()
    {
        ApplyRuntimeFramePolicy();
        if (WildWindBigTestRunner.IsMainSessionCheckInProgress)
        {
            return;
        }

        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (WildWindBigTestRunner.IsEditorBigTestLaunchPending() &&
            !WildWindBigTestRunner.IsSessionLoopLaunchInProgress)
        {
#if UNITY_EDITOR
            WildWindBigTestRunner.TryStartPendingEditorBigTest();
#endif
            return;
        }

        MetaGameState meta = Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            GameObject metaObject = new GameObject("MetaGameState");
            metaObject.SetActive(false);
            meta = metaObject.AddComponent<MetaGameState>();
            ConfigureMeta(meta);
            metaObject.SetActive(true);
            WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(meta, meta.RuntimeAccountId);
            Debug.Log("[WildWindGameplayBootstrap] Created MetaGameState and entered the runtime account port: " + meta.RuntimeAccountId);
            return;
        }

        ConfigureMeta(meta);
        meta.EnsureProgressInitialized();
        WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(meta, meta.RuntimeAccountId);
        Debug.Log("[WildWindGameplayBootstrap] Entered the runtime account port: " + meta.RuntimeAccountId);
    }

    private static void ConfigureMeta(MetaGameState meta)
    {
        if (meta == null)
        {
            return;
        }

        meta.runtimeAccountId = GameplaySessionAccountData.DefaultAccountId;
        EnsureGameplayBindings(meta);
    }

    public static void EnsureGameplayBindings(MetaGameState meta)
    {
        if (meta == null)
        {
            return;
        }

        if (meta.shipLoader == null)
        {
            meta.shipLoader = Object.FindFirstObjectByType<ShipLoader>();
        }

        if (meta.shipLoader == null)
        {
            GameObject loaderObject = new GameObject(ShipLoaderObjectName);
            meta.shipLoader = loaderObject.AddComponent<ShipLoader>();
        }

        if (meta.shipLoader.targetShip == null)
        {
            meta.shipLoader.targetShip = EnsurePlayerShipPhysics();
        }

        if (meta.shipLoader.catalog == null)
        {
            meta.shipLoader.catalog = meta.catalog != null
                ? meta.catalog
                : CreateRuntimeFallbackCatalog();
        }

        if (meta.catalog == null && meta.shipLoader.catalog != null)
        {
            meta.catalog = meta.shipLoader.catalog;
        }

        if (meta.shipLoader.spawnPoint == null && meta.shipLoader.targetShip != null)
        {
            meta.shipLoader.spawnPoint = meta.shipLoader.targetShip.transform;
        }

        meta.EnsureProgressInitialized();
        if (meta.CurrentCatalog != null && meta.CurrentCatalog.HasAssemblyParts())
        {
            meta.ApplySelectedShip();
        }
    }

    public static bool IsRuntimeFramePolicyAppliedForTests()
    {
        return QualitySettings.GetQualityLevel() == RuntimeQualityLevelIndex
            && QualitySettings.vSyncCount == 0
            && Application.targetFrameRate == RuntimeTargetFrameRate
            && GetRenderFrameIntervalForTests() == 1;
    }

    public static int GetRenderFrameIntervalForTests()
    {
#if UNITY_2019_3_OR_NEWER
        return UnityEngine.Rendering.OnDemandRendering.renderFrameInterval;
#else
        return 1;
#endif
    }

    private static void ApplyRuntimeFramePolicy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        int qualityCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (qualityCount > RuntimeQualityLevelIndex && QualitySettings.GetQualityLevel() != RuntimeQualityLevelIndex)
        {
            QualitySettings.SetQualityLevel(RuntimeQualityLevelIndex, true);
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = RuntimeTargetFrameRate;

#if UNITY_2019_3_OR_NEWER
        UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = 1;
#endif
    }

    private static ShipPhysics EnsurePlayerShipPhysics()
    {
        ShipPhysics ship = Object.FindFirstObjectByType<ShipPhysics>();
        if (ship != null)
        {
            EnsureShipRigidbody(ship);
            return ship;
        }

        GameObject shipObject = GameObject.Find(PlayerShipProxyName);
        if (shipObject == null)
        {
            shipObject = GameObject.Find(PlayerSessionShipName);
        }

        if (shipObject == null)
        {
            shipObject = new GameObject(RuntimePlayerShipName);
            shipObject.transform.position = GameplaySessionAccountData.ResolveStarterDockPosition(GameplaySessionAccountData.DefaultDockId);
        }
        else if (shipObject.name == PlayerShipProxyName || shipObject.name == PlayerSessionShipName)
        {
            shipObject.name = RuntimePlayerShipName;
        }

        ship = shipObject.GetComponent<ShipPhysics>();
        if (ship == null)
        {
            ship = shipObject.AddComponent<ShipPhysics>();
        }

        EnsureShipRigidbody(ship);
        return ship;
    }

    private static ShipCatalogSO CreateRuntimeFallbackCatalog()
    {
        ShipCatalogSO catalog = ScriptableObject.CreateInstance<ShipCatalogSO>();
        catalog.name = RuntimeFallbackCatalogName;
        catalog.hideFlags = HideFlags.DontSave;
        catalog.starterHullId = GameplaySessionAccountData.DefaultStarterHullId;
        catalog.parts = new List<ShipPartDefinitionSO>();

        ShipPartDefinitionSO hull = CreateRuntimePart(
            GameplaySessionAccountData.DefaultStarterHullId,
            "Starter airship hull",
            ShipPartKind.Hull);
        hull.description = "Runtime fallback hull used when the scene has no authored ShipCatalogSO.";
        hull.runtimeShipTreeId = "capital_patrol_frigate_r02";
        hull.fuelResourceId = "charcoal";
        hull.visualPrefab = LoadRuntimeKorshunVisualPrefab();
        hull.visualLocalScale = Vector3.one;
        hull.slots = new List<ShipSlotDefinition>
        {
            CreateRuntimeSlot(SessionExtractionConstants.StarterLowSlotId, "Low slot", SessionExtractionConstants.LowSlotTypeId),
            CreateRuntimeSlot(SessionExtractionConstants.StarterHighSlotId, "High slot I", SessionExtractionConstants.HighSlotTypeId),
            CreateRuntimeSlot(SessionExtractionConstants.StarterSecondHighSlotId, "High slot II", SessionExtractionConstants.HighSlotTypeId),
            CreateRuntimeSlot(SessionExtractionConstants.StarterThirdHighSlotId, "High slot III", SessionExtractionConstants.HighSlotTypeId),
            CreateRuntimeSlot(SessionExtractionConstants.StarterMidSlotId, "Mid slot", SessionExtractionConstants.MidSlotTypeId)
        };
        AddRuntimeStat(hull, ShipStatId.BaseMass, ShipStatOperation.Set, 1200f);
        AddRuntimeStat(hull, ShipStatId.HullMaxTakeoffMassKg, ShipStatOperation.Set, 4000f);
        AddRuntimeStat(hull, ShipStatId.ClaudiumMaxLiftKg, ShipStatOperation.Set, 4000f);
        AddRuntimeStat(hull, ShipStatId.HullForwardThrustKgf, ShipStatOperation.Set, 250f);
        AddRuntimeStat(hull, ShipStatId.HullCruiseReferenceSpeedMS, ShipStatOperation.Set, 24f);
        AddRuntimeStat(hull, ShipStatId.FuelConsumptionKgPerMinute, ShipStatOperation.Set, 1.2f);
        AddRuntimeStat(hull, ShipStatId.StrategicYawRateDegPerSecond, ShipStatOperation.Set, 28f);
        AddRuntimeStat(hull, ShipStatId.StrategicYawAccelerationDegPerSecond2, ShipStatOperation.Set, 90f);
        AddRuntimeStat(hull, ShipStatId.StrategicVerticalSpeedMS, ShipStatOperation.Set, 9f);
        AddRuntimeStat(hull, ShipStatId.StrategicVerticalAccelerationMS2, ShipStatOperation.Set, 9f);
        AddRuntimeStat(hull, ShipStatId.StructureHp, ShipStatOperation.Set, 1200f);
        catalog.parts.Add(hull);

        catalog.parts.Add(CreateRuntimeModule(SessionExtractionConstants.StarterCargoRackModuleId, "Cargo Rack", SessionExtractionConstants.LowSlotTypeId, 120f, 0f, 0f, 250f, 0f));
        catalog.parts.Add(CreateRuntimeModule(SessionExtractionConstants.StarterGasExtractorModuleId, "Gas Extractor I", SessionExtractionConstants.HighSlotTypeId, 95f, 0f, 0f, 0f, 120f));
        catalog.parts.Add(CreateRuntimeModule(SessionExtractionConstants.StarterMiningHoldModuleId, "Impact Ore Hold I", SessionExtractionConstants.HighSlotTypeId, 55f, 60f, 1f, 85f, 0f));
        catalog.parts.Add(CreateRuntimeModule(SessionExtractionConstants.StarterObservationPostModuleId, "Observation Post I", SessionExtractionConstants.MidSlotTypeId, 80f, 0f, 0f, 35f, 0f));
        catalog.parts.Add(CreateRuntimeModule(SessionExtractionConstants.StarterLeviathanSalvageModuleId, "Leviathan Salvage Rig I", SessionExtractionConstants.HighSlotTypeId, 140f, 0f, 0f, 80f, 0f));
        return catalog;
    }

    private static ShipPartDefinitionSO CreateRuntimePart(string partId, string displayName, ShipPartKind kind)
    {
        ShipPartDefinitionSO part = ScriptableObject.CreateInstance<ShipPartDefinitionSO>();
        part.name = partId;
        part.hideFlags = HideFlags.DontSave;
        part.partId = partId;
        part.displayName = displayName;
        part.kind = kind;
        part.slots = new List<ShipSlotDefinition>();
        part.compatibleSlotTypeIds = new List<string>();
        part.grantedSlots = new List<ShipSlotDefinition>();
        part.statModifiers = new List<ShipStatModifier>();
        return part;
    }

    private static ShipPartDefinitionSO CreateRuntimeModule(
        string partId,
        string displayName,
        string slotTypeId,
        float baseMassKg,
        float miningHoldKg,
        float miningDamageMultiplier,
        float cargoVanKg,
        float gasCylinderKg)
    {
        ShipPartDefinitionSO module = CreateRuntimePart(partId, displayName, ShipPartKind.Module);
        module.compatibleSlotTypeIds.Add(slotTypeId);
        AddRuntimeStat(module, ShipStatId.BaseMass, ShipStatOperation.Add, baseMassKg);
        AddRuntimeStat(module, ShipStatId.MiningImpactHoldCapacityKg, ShipStatOperation.Set, miningHoldKg);
        AddRuntimeStat(module, ShipStatId.MiningImpactDamageTakenMultiplier, ShipStatOperation.Set, miningDamageMultiplier);
        AddRuntimeStat(module, ShipStatId.CargoVanCapacityKg, ShipStatOperation.Add, cargoVanKg);
        AddRuntimeStat(module, ShipStatId.GasCylinderCapacityKg, ShipStatOperation.Add, gasCylinderKg);
        return module;
    }

    private static ShipSlotDefinition CreateRuntimeSlot(string slotId, string displayName, string slotTypeId)
    {
        return new ShipSlotDefinition
        {
            slotId = slotId,
            displayName = displayName,
            slotTypeId = slotTypeId,
            required = false,
            allowedPartIds = new List<string>()
        };
    }

    private static void AddRuntimeStat(ShipPartDefinitionSO part, ShipStatId stat, ShipStatOperation operation, float value)
    {
        if (part == null || value <= 0f)
        {
            return;
        }

        part.statModifiers.Add(new ShipStatModifier
        {
            stat = stat,
            operation = operation,
            value = value
        });
    }

    private static GameObject LoadRuntimeKorshunVisualPrefab()
    {
        GameObject resourcePrefab = Resources.Load<GameObject>("ShipImports/Models/BlenderShips/" + RuntimeKorshunVisualModelId);
        if (resourcePrefab != null)
        {
            return resourcePrefab;
        }

#if UNITY_EDITOR
        System.Type databaseType = System.Type.GetType("UnityEditor.Asset" + "Database, UnityEditor");
        System.Reflection.MethodInfo loadMethod = null;
        if (databaseType != null)
        {
            System.Reflection.MethodInfo[] methods = databaseType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                System.Reflection.MethodInfo method = methods[i];
                if (method == null ||
                    !string.Equals(method.Name, "LoadAssetAtPath", System.StringComparison.Ordinal) ||
                    !method.IsGenericMethodDefinition)
                {
                    continue;
                }

                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    loadMethod = method;
                    break;
                }
            }
        }

        System.Reflection.MethodInfo genericLoadMethod = loadMethod != null
            ? loadMethod.MakeGenericMethod(typeof(GameObject))
            : null;
        return genericLoadMethod != null
            ? genericLoadMethod.Invoke(null, new object[] { "Assets/ShipImports/Models/BlenderShips/" + RuntimeKorshunVisualModelId + ".fbx" }) as GameObject
            : null;
#else
        return null;
#endif
    }

    private static void EnsureShipRigidbody(ShipPhysics ship)
    {
        if (ship == null)
        {
            return;
        }

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = ship.gameObject.AddComponent<Rigidbody>();
        }

        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.useGravity = false;
        body.isKinematic = false;

        if (ship.GetComponent<CoreTacticalShipMotor>() == null)
        {
            ship.gameObject.AddComponent<CoreTacticalShipMotor>();
        }
    }
}
