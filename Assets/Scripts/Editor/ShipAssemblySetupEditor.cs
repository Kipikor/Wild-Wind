using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ShipAssemblySetupEditor
{
    private const string DataFolder = "Assets/Data";
    private const string PartsFolder = "Assets/Data/ShipParts";
    private const string PrefabsFolder = "Assets/Data/ShipPrefabs";
    private const string ModulePrefabsFolder = "Assets/Data/ShipPrefabs/Modules";
    private const string TechTreesFolder = "Assets/Data/TechTrees";
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";

    private const string StarterHullPath = "Assets/Data/ShipParts/StarterHull.asset";
    private const string StarterEnginePath = "Assets/Data/ShipParts/StarterEngine.asset";
    private const string StarterPropellerPath = "Assets/Data/ShipParts/StarterPropeller.asset";
    private const string StarterClaudiumLoopPath = "Assets/Data/ShipParts/StarterClaudiumLoop.asset";
    private const string StarterCargoRackPath = "Assets/Data/ShipParts/StarterCargoRack.asset";
    private const string StarterGasHarvesterPath = "Assets/Data/ShipParts/StarterGasHarvester.asset";
    private const string StarterMiningHoldPath = "Assets/Data/ShipParts/StarterMiningHold.asset";
    private const string StarterObservationPostPath = "Assets/Data/ShipParts/StarterObservationPost.asset";

    private const string StarterHullPrefabPath = "Assets/Data/ShipPrefabs/StarterHull.prefab";
    private const string StarterEnginePrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterEngine.prefab";
    private const string StarterPropellerPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterPropeller.prefab";
    private const string StarterClaudiumLoopPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterClaudiumLoop.prefab";
    private const string StarterCargoRackPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterCargoRack.prefab";
    private const string StarterGasHarvesterPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterGasHarvester.prefab";
    private const string StarterMiningHoldPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterMiningHold.prefab";
    private const string StarterObservationPostPrefabPath = "Assets/Data/ShipPrefabs/Modules/StarterObservationPost.prefab";

    public static void BuildStarterAssemblySetup()
    {
        EnsureFolder(DataFolder);
        EnsureFolder(PartsFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(ModulePrefabsFolder);
        EnsureFolder(TechTreesFolder);

        ShipPartDefinitionSO hull = CreateOrLoadPart(StarterHullPath);
        ShipPartDefinitionSO engine = CreateOrLoadPart(StarterEnginePath);
        ShipPartDefinitionSO propeller = CreateOrLoadPart(StarterPropellerPath);
        ShipPartDefinitionSO claudiumLoop = CreateOrLoadPart(StarterClaudiumLoopPath);
        ShipPartDefinitionSO cargoRack = CreateOrLoadPart(StarterCargoRackPath);
        ShipPartDefinitionSO gasHarvester = CreateOrLoadPart(StarterGasHarvesterPath);
        ShipPartDefinitionSO miningHold = CreateOrLoadPart(StarterMiningHoldPath);
        ShipPartDefinitionSO observationPost = CreateOrLoadPart(StarterObservationPostPath);

        GameObject hullPrefab = CreateStarterHullPrefab();
        GameObject enginePrefab = CreateModulePrefab(StarterEnginePrefabPath, "Паровой двигатель I", PrimitiveType.Cube, new Vector3(1.2f, 0.8f, 1.6f));
        GameObject propellerPrefab = CreateModulePrefab(StarterPropellerPrefabPath, "Деревянный винт 2.8 м", PrimitiveType.Cylinder, new Vector3(2.8f, 0.08f, 2.8f));
        GameObject claudiumLoopPrefab = CreateModulePrefab(StarterClaudiumLoopPrefabPath, "Клавдиевый контур I", PrimitiveType.Sphere, new Vector3(0.9f, 0.9f, 0.9f));
        GameObject cargoRackPrefab = CreateModulePrefab(StarterCargoRackPrefabPath, "Грузовая полка", PrimitiveType.Cube, new Vector3(1.7f, 0.25f, 1.1f));
        GameObject gasHarvesterPrefab = CreateModulePrefab(StarterGasHarvesterPrefabPath, "Харвестер облаков I", PrimitiveType.Cylinder, new Vector3(0.9f, 0.6f, 0.9f));
        GameObject miningHoldPrefab = CreateModulePrefab(StarterMiningHoldPrefabPath, "Противоударный кузов I", PrimitiveType.Cube, new Vector3(1.8f, 0.55f, 1.25f));
        GameObject observationPostPrefab = CreateModulePrefab(StarterObservationPostPrefabPath, "Наблюдательный пост I", PrimitiveType.Sphere, new Vector3(0.85f, 0.85f, 0.85f));

        ConfigureStarterHull(hull, hullPrefab);
        ConfigureStarterEngine(engine, enginePrefab);
        ConfigureStarterPropeller(propeller, propellerPrefab);
        ConfigureStarterClaudiumLoop(claudiumLoop, claudiumLoopPrefab);
        ConfigureStarterCargoRack(cargoRack, cargoRackPrefab);
        ConfigureStarterGasHarvester(gasHarvester, gasHarvesterPrefab);
        ConfigureStarterMiningHold(miningHold, miningHoldPrefab);
        ConfigureStarterObservationPost(observationPost, observationPostPrefab);

        ShipCatalogSO catalog = CreateOrLoadAsset<ShipCatalogSO>(CatalogPath);
        ConfigureCatalog(catalog, hull, engine, propeller, claudiumLoop, cargoRack, gasHarvester, miningHold, observationPost);
        ApplyCsvSpecialModuleConfigsToCatalog(catalog);

        TechTreeDefinitionSO techTree = CreateOrLoadAsset<TechTreeDefinitionSO>(TechTreePath);
        ConfigureTechTree(techTree, hull, engine, propeller, claudiumLoop, cargoRack, gasHarvester, miningHold, observationPost);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorUtility.DisplayDialog("Базовая сборка корабля", "Базовый сетап корпуса, модулей, префабов, каталога и древа техники собран.", "OK");
    }

    public static bool ValidateBuildStarterAssemblySetup()
    {
        return !Application.isPlaying;
    }

    public static void PrepareSceneForAssembly()
    {
        ShipLoader loader = Object.FindFirstObjectByType<ShipLoader>();
        if (loader == null)
        {
            GameObject loaderObject = new GameObject("Загрузчик корабля");
            loader = loaderObject.AddComponent<ShipLoader>();
            Undo.RegisterCreatedObjectUndo(loaderObject, "Создать загрузчик корабля");
        }

        Undo.RecordObject(loader, "Подготовить сцену под сборку корабля");

        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        if (catalog != null)
        {
            loader.catalog = catalog;
        }

        if (loader.targetShip == null)
        {
            loader.targetShip = Object.FindFirstObjectByType<ShipPhysics>();
        }

        if (loader.spawnPoint == null)
        {
            GameObject spawnPoint = GameObject.Find("Точка создания корабля");
            if (spawnPoint == null)
            {
                spawnPoint = new GameObject("Точка создания корабля");
                Undo.RegisterCreatedObjectUndo(spawnPoint, "Создать точку сборки корабля");
            }

            if (loader.targetShip != null)
            {
                spawnPoint.transform.position = loader.targetShip.transform.position;
                spawnPoint.transform.rotation = loader.targetShip.transform.rotation;
            }

            loader.spawnPoint = spawnPoint.transform;
        }

        EditorUtility.SetDirty(loader);
        EditorUtility.DisplayDialog("Сцена подготовлена", "Загрузчик корабля и точка создания корпуса готовы. Старый сценовый корабль можно оставить как запасной: после создания корпуса-префаба он отключится.", "OK");
    }

    public static bool ValidatePrepareSceneForAssembly()
    {
        return !Application.isPlaying;
    }

    private static void ConfigureStarterHull(ShipPartDefinitionSO hull, GameObject prefab)
    {
        Undo.RecordObject(hull, "Настроить стартовый корпус");
        hull.partId = "starter_hull";
        hull.displayName = "Стартовый корпус";
        hull.description = "Простой деревянно-металлический корпус для первых вылетов.";
        hull.kind = ShipPartKind.Hull;
        hull.prefab = prefab;
        hull.engineFuelId = "";

        hull.slots = new List<ShipSlotDefinition>
        {
            RequiredSlot("engine_main", "Маршевый двигатель", "engine_main", "starter_engine"),
            RequiredSlot("propeller_main", "Винт", "propeller_main", "starter_propeller"),
            RequiredSlot("claudium_loop", "Клавдиевый контур", "claudium_loop", "starter_claudium_loop"),
            OptionalSlot("utility_01", "Вспомогательный слот", "utility")
        };

        hull.compatibleSlotTypeIds.Clear();
        hull.grantedSlots.Clear();
        hull.statModifiers = new List<ShipStatModifier>
        {
            Set(ShipStatId.BaseMass, 900f),
            Set(ShipStatId.HullMaxTakeoffMassKg, 3200f),
            Set(ShipStatId.AirDensity, 1.225f),
            Set(ShipStatId.DragCoefficient, 0.8f),
            Set(ShipStatId.FrontalArea, 8f),
            Set(ShipStatId.SideResistance, 2.0f),
            Set(ShipStatId.VerticalAreaFactor, 4.0f),
            Set(ShipStatId.GyroTurnTorque, 12000f),
            Set(ShipStatId.GyroTurnDamping, 0.8f),
            Set(ShipStatId.MaxAutoTurnRateDeg, 5f),
            Set(ShipStatId.MaxStructuralTurnRateDeg, 15f),
            Set(ShipStatId.MaxStructuralVerticalSpeed, 10f),
            Set(ShipStatId.MaxAutoVerticalSpeed, 10f),
            Set(ShipStatId.AltitudeStiffness, 0.2f),
            Set(ShipStatId.AltitudeDamping, 1.2f),
            Set(ShipStatId.AltitudeDriftTolerance, 0.15f),
            Set(ShipStatId.HeadingStiffness, 0.5f),
            Set(ShipStatId.HeadingDamping, 0.5f),
            Set(ShipStatId.WaypointRadius, 10f),
            Set(ShipStatId.SpeedStiffness, 0.8f),
            Set(ShipStatId.SpeedDamping, 0.3f)
        };

        EditorUtility.SetDirty(hull);
    }

    private static void ConfigureStarterEngine(ShipPartDefinitionSO engine, GameObject prefab)
    {
        Undo.RecordObject(engine, "Настроить стартовый двигатель");
        engine.partId = "starter_engine";
        engine.displayName = "Паровой двигатель I";
        engine.description = "Базовый общий двигатель: крутит винт и питает клавдиевый контур.";
        engine.kind = ShipPartKind.Module;
        engine.prefab = prefab;
        engine.engineFuelId = "charcoal";
        engine.slots.Clear();
        engine.compatibleSlotTypeIds = new List<string> { "engine_main" };
        engine.grantedSlots.Clear();
        engine.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 380f),
            Set(ShipStatId.EngineMaxPower, 190f),
            Set(ShipStatId.EngineFuelEfficiency, 0.32f)
        };

        EditorUtility.SetDirty(engine);
    }

    private static void ConfigureStarterPropeller(ShipPartDefinitionSO propeller, GameObject prefab)
    {
        Undo.RecordObject(propeller, "Настроить стартовый винт");
        propeller.partId = "starter_propeller";
        propeller.displayName = "Деревянный винт 2.8 м";
        propeller.description = "Простой винт с автоматом шага для спокойного первого полета.";
        propeller.kind = ShipPartKind.Module;
        propeller.prefab = prefab;
        propeller.engineFuelId = "";
        propeller.slots.Clear();
        propeller.compatibleSlotTypeIds = new List<string> { "propeller_main" };
        propeller.grantedSlots.Clear();
        propeller.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 90f),
            Set(ShipStatId.PropellerMaxSpeedMS, 30f),
            Set(ShipStatId.PropellerEfficiency, 0.78f)
        };

        EditorUtility.SetDirty(propeller);
    }

    private static void ConfigureStarterClaudiumLoop(ShipPartDefinitionSO claudiumLoop, GameObject prefab)
    {
        Undo.RecordObject(claudiumLoop, "Настроить стартовый клавдиевый контур");
        claudiumLoop.partId = "starter_claudium_loop";
        claudiumLoop.displayName = "Клавдиевый контур I";
        claudiumLoop.description = "Базовый контур подъема. Забирает мощность общего двигателя с приоритетом перед винтом.";
        claudiumLoop.kind = ShipPartKind.Module;
        claudiumLoop.prefab = prefab;
        claudiumLoop.engineFuelId = "";
        claudiumLoop.slots.Clear();
        claudiumLoop.compatibleSlotTypeIds = new List<string> { "claudium_loop" };
        claudiumLoop.grantedSlots.Clear();
        claudiumLoop.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 160f),
            Set(ShipStatId.ClaudiumConsumptionPerTonSecond, 0.005f),
            Set(ShipStatId.ClaudiumLiftEfficiency, 10f),
            Set(ShipStatId.ClaudiumMaxLiftKg, 3200f),
            Set(ShipStatId.ClaudiumLiftSmoothing, 2.5f)
        };

        EditorUtility.SetDirty(claudiumLoop);
    }

    private static void ConfigureStarterCargoRack(ShipPartDefinitionSO cargoRack, GameObject prefab)
    {
        Undo.RecordObject(cargoRack, "Настроить грузовую полку");
        cargoRack.partId = "starter_cargo_rack";
        cargoRack.displayName = "Грузовая полка";
        cargoRack.description = "Необязательный модуль для проверки дополнительных покупаемых деталей.";
        cargoRack.kind = ShipPartKind.Module;
        cargoRack.prefab = prefab;
        cargoRack.engineFuelId = "";
        cargoRack.slots.Clear();
        cargoRack.compatibleSlotTypeIds = new List<string> { "utility" };
        cargoRack.grantedSlots.Clear();
        cargoRack.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 120f)
        };

        EditorUtility.SetDirty(cargoRack);
    }

    private static void ConfigureStarterGasHarvester(ShipPartDefinitionSO harvester, GameObject prefab)
    {
        Undo.RecordObject(harvester, "Configure starter gas harvester");
        harvester.partId = "starter_gas_harvester";
        harvester.displayName = "Харвестер облаков I";
        harvester.description = "Стартовый модуль для сбора сырого концентрата из газовых облаков.";
        harvester.completedTechId = "";
        harvester.kind = ShipPartKind.Module;
        harvester.prefab = prefab;
        harvester.engineFuelId = "";
        harvester.slots.Clear();
        harvester.compatibleSlotTypeIds = new List<string> { "utility" };
        harvester.grantedSlots.Clear();
        harvester.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 95f),
            Set(ShipStatId.GasHarvesterVolumeM3PerSecond, 12f),
            Set(ShipStatId.GasHarvesterPowerDrawKw, 22f),
            Set(ShipStatId.GasHarvesterRadiusMeters, 22f),
            Set(ShipStatId.GasHarvesterCycleSeconds, 5f)
        };

        EditorUtility.SetDirty(harvester);
    }

    private static void ConfigureStarterMiningHold(ShipPartDefinitionSO hold, GameObject prefab)
    {
        Undo.RecordObject(hold, "Configure starter mining hold");
        hold.partId = "starter_mining_hold";
        hold.displayName = "Противоударный кузов I";
        hold.description = "Стартовый модуль для сбора падающих кусков руды под нестабильными глыбами.";
        hold.completedTechId = "";
        hold.kind = ShipPartKind.Module;
        hold.prefab = prefab;
        hold.engineFuelId = "";
        hold.slots.Clear();
        hold.compatibleSlotTypeIds = new List<string> { "utility" };
        hold.grantedSlots.Clear();
        hold.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 55f),
            Set(ShipStatId.MiningImpactHoldCapacityKg, 60f)
        };

        EditorUtility.SetDirty(hold);
    }

    private static void ConfigureStarterObservationPost(ShipPartDefinitionSO post, GameObject prefab)
    {
        Undo.RecordObject(post, "Configure starter observation post");
        post.partId = "starter_observation_post";
        post.displayName = "Наблюдательный пост I";
        post.description = "Простой научно-разведывательный модуль: расширяет радиус наблюдения и перерабатывает бумагу в информацию.";
        post.completedTechId = "";
        post.kind = ShipPartKind.Module;
        post.prefab = prefab;
        post.engineFuelId = "";
        post.slots.Clear();
        post.compatibleSlotTypeIds = new List<string> { "utility" };
        post.grantedSlots.Clear();
        post.statModifiers = new List<ShipStatModifier>
        {
            Add(ShipStatId.BaseMass, 80f),
            Set(ShipStatId.ObservationRadiusMeters, 550f),
            Set(ShipStatId.ObservationFactsAtHalfRadiusPerSecond, 2f),
            Set(ShipStatId.ObservationRockInfoEfficiency, 0.75f),
            Set(ShipStatId.ObservationCloudInfoEfficiency, 0.7f),
            Set(ShipStatId.ObservationLeviathanInfoEfficiency, 0.45f)
        };

        EditorUtility.SetDirty(post);
    }

    private static void ConfigureCatalog(ShipCatalogSO catalog, params ShipPartDefinitionSO[] parts)
    {
        Undo.RecordObject(catalog, "Настроить каталог кораблей");
        catalog.starterHullId = "starter_hull";
        catalog.parts ??= new List<ShipPartDefinitionSO>();
        catalog.parts.Clear();
        catalog.parts.AddRange(parts);
        EditorUtility.SetDirty(catalog);
    }

    private static void ApplyCsvSpecialModuleConfigsToCatalog(ShipCatalogSO catalog)
    {
        if (catalog == null) return;

        WorldConfigDatabase config = new WorldConfigDatabase();
        config.LoadFromAssetsConfigFolder("Data/Config");
        if (!config.isLoaded)
        {
            Debug.LogWarning("Не удалось прочитать CSV спец-модулей: " + config.lastError);
            return;
        }

        int applied = ShipAssemblyBuilder.ApplySpecialModuleConfigs(catalog, config);
        if (catalog.parts != null)
        {
            for (int i = 0; i < catalog.parts.Count; i++)
            {
                if (catalog.parts[i] != null)
                {
                    EditorUtility.SetDirty(catalog.parts[i]);
                }
            }
        }

        EditorUtility.SetDirty(catalog);
        Debug.Log("CSV спец-модулей применён к каталогу кораблей. Обновлено модулей: " + applied + ".");
    }

    private static void ConfigureTechTree(
        TechTreeDefinitionSO techTree,
        ShipPartDefinitionSO hull,
        ShipPartDefinitionSO engine,
        ShipPartDefinitionSO propeller,
        ShipPartDefinitionSO claudiumLoop,
        ShipPartDefinitionSO cargoRack,
        ShipPartDefinitionSO gasHarvester,
        ShipPartDefinitionSO miningHold,
        ShipPartDefinitionSO observationPost)
    {
        Undo.RecordObject(techTree, "Настроить базовое древо техники");
        techTree.nodes ??= new List<TechTreeNode>();
        techTree.nodes.Clear();

        techTree.nodes.Add(new TechTreeNode
        {
            nodeId = "fundamentals_airship",
            displayName = "Основы воздушного судна",
            kind = TechTreeNodeKind.Fundamental,
            tier = 1,
            researchCostXp = 0,
            purchasePrice = 0,
            startsResearched = true,
            startsPurchased = false,
            editorPosition = new Vector2(0f, 0f)
        });

        techTree.nodes.Add(CreatePartNode("starter_hull_node", "Стартовый корпус", TechTreeNodeKind.Hull, hull, 1, 0, 0, true, true, 220f, 0f));
        techTree.nodes.Add(CreatePartNode("starter_engine_node", "Паровой двигатель I", TechTreeNodeKind.Module, engine, 1, 0, 0, true, true, 440f, -120f));
        techTree.nodes.Add(CreatePartNode("starter_propeller_node", "Деревянный винт 2.8 м", TechTreeNodeKind.Module, propeller, 1, 0, 0, true, true, 440f, 0f));
        techTree.nodes.Add(CreatePartNode("starter_claudium_loop_node", "Клавдиевый контур I", TechTreeNodeKind.Module, claudiumLoop, 1, 0, 0, true, true, 440f, 120f));

        TechTreeNode cargoNode = CreatePartNode("starter_cargo_rack_node", "Грузовая полка", TechTreeNodeKind.Module, cargoRack, 1, 40, 75, false, false, 660f, 120f);
        cargoNode.prerequisiteNodeIds.Add("fundamentals_airship");
        cargoNode.experienceShipIds.Add("starter_hull");
        techTree.nodes.Add(cargoNode);

        techTree.nodes.Add(CreatePartNode("starter_gas_harvester_node", "Харвестер облаков I", TechTreeNodeKind.Module, gasHarvester, 1, 0, 0, true, true, 660f, -120f));
        techTree.nodes.Add(CreatePartNode("starter_mining_hold_node", "Противоударный кузов I", TechTreeNodeKind.Module, miningHold, 1, 0, 0, true, true, 660f, -240f));
        techTree.nodes.Add(CreatePartNode("starter_observation_post_node", "Наблюдательный пост I", TechTreeNodeKind.Module, observationPost, 1, 0, 0, true, true, 660f, 240f));

        EditorUtility.SetDirty(techTree);
    }

    private static TechTreeNode CreatePartNode(
        string nodeId,
        string displayName,
        TechTreeNodeKind kind,
        ShipPartDefinitionSO part,
        int tier,
        int researchCost,
        int purchasePrice,
        bool startsResearched,
        bool startsPurchased,
        float x,
        float y)
    {
        TechTreeNode node = new TechTreeNode
        {
            nodeId = nodeId,
            displayName = displayName,
            kind = kind,
            tier = tier,
            partDefinition = part,
            partId = part != null ? part.partId : "",
            researchCostXp = researchCost,
            purchasePrice = purchasePrice,
            startsResearched = startsResearched,
            startsPurchased = startsPurchased,
            editorPosition = new Vector2(x, y)
        };

        node.prerequisiteNodeIds.Add("fundamentals_airship");
        node.experienceShipIds.Add("starter_hull");
        return node;
    }

    private static ShipSlotDefinition RequiredSlot(string slotId, string displayName, string slotTypeId, string allowedPartId)
    {
        ShipSlotDefinition slot = OptionalSlot(slotId, displayName, slotTypeId);
        slot.required = true;
        slot.allowedPartIds.Add(allowedPartId);
        return slot;
    }

    private static ShipSlotDefinition OptionalSlot(string slotId, string displayName, string slotTypeId)
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

    private static ShipStatModifier Set(ShipStatId stat, float value)
    {
        return Modifier(stat, ShipStatOperation.Set, value);
    }

    private static ShipStatModifier Add(ShipStatId stat, float value)
    {
        return Modifier(stat, ShipStatOperation.Add, value);
    }

    private static ShipStatModifier Modifier(ShipStatId stat, ShipStatOperation operation, float value)
    {
        return new ShipStatModifier
        {
            stat = stat,
            operation = operation,
            value = value
        };
    }

    private static GameObject CreateStarterHullPrefab()
    {
        GameObject root = new GameObject("Стартовый корпус");
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 900f;
        body.useGravity = false;

        ShipPhysics physics = root.AddComponent<ShipPhysics>();
        physics.baseMass = 900f;
        physics.hullMaxTakeoffMassKg = 3200f;
        physics.targetTrimMass = 900f;
        physics.enginePowerKwAt100 = 190f;
        physics.engineFuelId = "charcoal";
        physics.engineFuelEfficiency = 0.18f;
        physics.engineFuelEnergyKwhPerKg = 8f;
        physics.engineFuelStockKg = 0f;
        physics.engineResponseRate01PerSecond = 0.10f;
        physics.neutralStopBrakeEnabled = false;
        physics.neutralStopBrakeMaxDecelerationMS2 = 8f;
        physics.neutralStopBrakeStopTimeSeconds = 0.75f;
        physics.neutralStopBrakeDeadzoneMS = 0.05f;
        physics.enginePowerLever = 0.88f;
        physics.claudiumLoopResponseRate01PerSecond = 0.10f;
        physics.gyroTurnTorque = 12000f;
        physics.gyroTurnDamping = 0.8f;

        ShipAssemblyRuntime runtime = root.AddComponent<ShipAssemblyRuntime>();
        runtime.shipPhysics = physics;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(2.6f, 1.2f, 6.8f);
        collider.center = Vector3.zero;

        Transform visualRoot = new GameObject("Визуал").transform;
        visualRoot.SetParent(root.transform, false);
        GameObject hullVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hullVisual.name = "Корпус";
        hullVisual.transform.SetParent(visualRoot, false);
        hullVisual.transform.localScale = new Vector3(2.4f, 0.9f, 6.2f);
        RemovePrimitiveCollider(hullVisual);

        Transform socketsRoot = new GameObject("Сокеты").transform;
        socketsRoot.SetParent(root.transform, false);
        AddSocket(socketsRoot, "engine_main", "engine_main", "Двигатель", new Vector3(0f, 0.25f, -1.3f));
        AddSocket(socketsRoot, "propeller_main", "propeller_main", "Винт", new Vector3(0f, 0.1f, -3.7f));
        AddSocket(socketsRoot, "claudium_loop", "claudium_loop", "Клавдиевый контур", new Vector3(0f, 0.85f, 0.3f));
        AddSocket(socketsRoot, "utility_01", "utility", "Вспомогательный слот", new Vector3(0f, -0.05f, 1.7f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, StarterHullPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(StarterHullPrefabPath);
    }

    private static GameObject CreateModulePrefab(string path, string objectName, PrimitiveType primitive, Vector3 scale)
    {
        GameObject root = new GameObject(objectName);
        GameObject visual = GameObject.CreatePrimitive(primitive);
        visual.name = "Визуал";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = scale;
        RemovePrimitiveCollider(visual);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void AddSocket(Transform parent, string slotId, string slotTypeId, string name, Vector3 localPosition)
    {
        GameObject socketObject = new GameObject(name);
        socketObject.transform.SetParent(parent, false);
        socketObject.transform.localPosition = localPosition;

        ShipSocket socket = socketObject.AddComponent<ShipSocket>();
        socket.slotId = slotId;
        socket.slotTypeId = slotTypeId;
        socket.mountPoint = socketObject.transform;
    }

    private static void RemovePrimitiveCollider(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static ShipPartDefinitionSO CreateOrLoadPart(string path)
    {
        return CreateOrLoadAsset<ShipPartDefinitionSO>(path);
    }

    private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            SyncMainObjectName(asset, path);
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        SyncMainObjectName(asset, path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void SyncMainObjectName(Object asset, string path)
    {
        string expectedName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (asset.name == expectedName) return;

        asset.name = expectedName;
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            parent = "Assets";
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
