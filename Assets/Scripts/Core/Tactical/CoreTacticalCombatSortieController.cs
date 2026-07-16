using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class CoreTacticalCombatSortieController : MonoBehaviour
{
    private const string ControllerObjectName = "Core Tactical Combat Sortie";
    private const string TacticalRootName = "Core Tactical Runtime Sortie Root";
    private const int EnemyFrigateCount = 3;
    private const int RequiredEnemyKillObjectiveCount = 4;
    private const float RequiredOreObjectiveKg = 300f;
    private const float CommandPlaneAltitudeMeters = 80f;
    private const float PlayerSpawnDistanceMeters = 10000f;
    private const float EnemySpawnMinRadiusMeters = 1200f;
    private const float EnemySpawnMaxRadiusMeters = 3100f;
    private const float ExitDirectionBeyondBoundaryMeters = 16000f;
    private const float ExitCommandBoundaryMarginMeters = 260f;
    private const float ExitCommandOutwardStepMeters = 360f;
    private const float ClaudianSlipTargetSpeedMS = 365f;
    private const float ClaudianSlipOverspeedBrakeSeconds = 8f;
    private const float TacticalBatteryMax = 100f;
    private const float ClassFrigateAverageSpeedMS = 60f;
    private const float ClassCruiserAverageSpeedMS = 45f;
    private const float ClassBattleshipAverageSpeedMS = 30f;
    private const float ClassFrigateSpeedMultiplier = 2.25f;
    private const float ClassCruiserSpeedMultiplier = 1.5f;
    private const float ClassBattleshipSpeedMultiplier = 1f;
    private const float ClassFrigateAverageYawDegPerSecond = 30f;
    private const float ClassCruiserAverageYawDegPerSecond = 18f;
    private const float ClassBattleshipAverageYawDegPerSecond = 12f;
    private const float ClassFrigateAcceleration90PercentSeconds = 8f;
    private const float ClassCruiserAcceleration90PercentSeconds = 12f;
    private const float ClassBattleshipAcceleration90PercentSeconds = 20f;
    private const float ExplosiveRadiusReferenceMassKg = 50f;
    private const float ExplosiveRadiusReferenceMeters = 20f;
    private const float ExplosiveRadiusMassExponent = 0.5f;
    private const int CoreTacticalGasCloudCount = 3;
    private const float SmallRocketSplashDamageToRadius = 0.025f;
    private const float SmallRocketSplashRadiusMinMeters = 2f;
    private const float SmallRocketSplashRadiusMaxMeters = 5f;
    private const float InspectionClickPickHalfSizePixels = 18f;
    private const float InspectionPickMinimumHalfSizePixels = 12f;
    private const float HarpoonTargetPickRadiusPixels = 48f;
    private const int HarpoonTargetRingSegments = 48;
    private const float NaturalLogTen = 2.3025851f;
    private const float EnemyFrigateMaxSpeedMS = ClassFrigateAverageSpeedMS * ClassFrigateSpeedMultiplier;
    private const float EnemyFrigateAccelerationMS2 = EnemyFrigateMaxSpeedMS * NaturalLogTen / ClassFrigateAcceleration90PercentSeconds;
    private const float EnemyFrigateBrakingMS2 = EnemyFrigateAccelerationMS2;
    private const float EnemyFrigateYawDegPerSecond = ClassFrigateAverageYawDegPerSecond;
    private const float EnemyFrigateReverseSpeedMS = EnemyFrigateMaxSpeedMS * 0.22f;
    private const float EnemyFrigateLateralSpeedMS = EnemyFrigateMaxSpeedMS * 0.12f;
    private const float EnemyCruiserMaxSpeedMS = ClassCruiserAverageSpeedMS * ClassCruiserSpeedMultiplier;
    private const float EnemyCruiserAccelerationMS2 = EnemyCruiserMaxSpeedMS * NaturalLogTen / ClassCruiserAcceleration90PercentSeconds;
    private const float EnemyCruiserBrakingMS2 = EnemyCruiserAccelerationMS2;
    private const float EnemyCruiserYawDegPerSecond = ClassCruiserAverageYawDegPerSecond;
    private const float EnemyCruiserReverseSpeedMS = EnemyCruiserMaxSpeedMS * 0.22f;
    private const float EnemyCruiserLateralSpeedMS = EnemyCruiserMaxSpeedMS * 0.12f;

    private static readonly CoreTacticalWeaponGroup[] TacticalHudWeaponGroups =
    {
        CoreTacticalWeaponGroup.MainBattery,
        CoreTacticalWeaponGroup.Secondary76mm,
        CoreTacticalWeaponGroup.Secondary152mm,
        CoreTacticalWeaponGroup.Torpedoes,
        CoreTacticalWeaponGroup.Missiles,
        CoreTacticalWeaponGroup.MachineGuns,
        CoreTacticalWeaponGroup.Autocannon30mm
    };

    public MetaGameState metaGameState;

    private static CoreTacticalCombatSortieController activeInstance;

    private readonly List<CoreTacticalShipMotor> enemyShips = new List<CoreTacticalShipMotor>();
    private readonly List<CoreTacticalOreBoulder> oreBoulders = new List<CoreTacticalOreBoulder>();
    private readonly List<CoreTacticalGasCloud> gasClouds = new List<CoreTacticalGasCloud>();
    private readonly List<BehaviourState> suppressedBehaviours = new List<BehaviourState>();
    private readonly List<CameraState> suppressedCameras = new List<CameraState>();
    private readonly List<RendererState> suppressedRenderers = new List<RendererState>();
    private readonly List<ColliderState> suppressedColliders = new List<ColliderState>();
    private readonly List<BehaviourState> suppressedSessionShipBehaviours = new List<BehaviourState>();
    private readonly List<Rect> missionGuiRects = new List<Rect>();
    private readonly List<TacticalHudAction> hudActions = new List<TacticalHudAction>(16);
    private readonly List<CoreTacticalInventoryRow> inventoryRows = new List<CoreTacticalInventoryRow>(16);
    private readonly List<LineRenderer> torpedoAllowedSectorLines = new List<LineRenderer>(2);
    private readonly Vector3[] torpedoAimSectorPoints = new Vector3[35];
    private readonly Vector3[] harpoonTargetRingPoints = new Vector3[HarpoonTargetRingSegments + 1];

    private GameObject tacticalRoot;
    private CoreTacticalFleetController fleet;
    private CoreTacticalShipMotor playerShip;
    private CoreTacticalClaudianSlipDrive playerSlipDrive;
    private CoreTacticalMiningRig playerMiningRig;
    private Transform sessionShipRoot;
    private Rigidbody sessionShipBody;
    private bool built;
    private bool extractionUnlocked;
    private bool extractionComplete;
    private bool autoExitRequested;
    private Vector3 exitDirection = Vector3.forward;
    private Vector3 exitPosition;
    private LineRenderer exitLine;
    private Material exitMaterial;
    private LineRenderer missionBoundaryLine;
    private Material missionBoundaryMaterial;
    private LineRenderer torpedoAimLine;
    private Material torpedoAimMaterial;
    private Material torpedoAllowedSectorMaterial;
    private LineRenderer harpoonTargetRingLine;
    private Material harpoonTargetRingMaterial;
    private bool torpedoAimActive;
    private CoreTacticalShipMotor torpedoAimShip;
    private CoreTacticalMissileLauncher torpedoAimLauncher;
    private CoreTacticalHarpoonLauncher torpedoAimHarpoonLauncher;
    private bool torpedoAimIsHarpoonTargetSelection;
    private Transform harpoonHoverTargetTransform;
    private Rigidbody harpoonHoverTargetBody;
    private CoreTacticalPrototypeHealth harpoonHoverTargetHealth;
    private CoreTacticalAutomatonWreck harpoonHoverTargetWreck;
    private string harpoonHoverTargetName;
    private bool harpoonAutoCatchActive;
    private CoreTacticalShipMotor harpoonAutoCatchShip;
    private float nextHarpoonAutoCatchScanTime;
    private bool suppressInspectionClickThisFrame;
    private Vector3 torpedoAimDirection = Vector3.forward;
    private Vector3 torpedoAimPoint;
    private string missionStatus = "";
    private int totalEnemies;
    private Vector3 missionCenter;
    private float missionRadiusMeters = 6500f;
    private float tacticalBatteryCurrent = TacticalBatteryMax;
    private int pendingFreightReward;
    private int pendingShipExperienceReward;
    private int pendingReputationReward;
    private ShipTreeEntryConfig playerShipEntry;
    private RuntimeShipProfile playerRuntimeProfile;
    private string playerLoadoutSummary = "2x 30 mm autocannons";
    private bool inventoryWindowOpen;
    private CoreTacticalOreBoulder inspectedOreBoulder;
    private CoreTacticalLeviathanController inspectedLeviathan;
    private CoreTacticalShipMotor inspectedShip;
    private CoreTacticalGasCloud inspectedGasCloud;

    private GUIStyle hudSmallStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudHeaderStyle;
    private GUIStyle hudCenterStyle;
    private GUIStyle hudTinyCenterStyle;

    private enum RuntimeLoadoutKind
    {
        None,
        FrigateAutocannon,
        ArtilleryCruiser,
        BattleshipFull
    }

    private struct RuntimeShipProfile
    {
        public string shipId;
        public string displayName;
        public string classId;
        public string roleId;
        public RuntimeLoadoutKind loadout;
        public Vector3 hullSizeMeters;
        public float maxForwardSpeedMS;
        public float forwardAccelerationMS2;
        public float brakingAccelerationMS2;
        public float maxYawRateDegPerSecond;
        public float maxReverseSpeedMS;
        public float maxLateralSpeedMS;
        public float massKg;
        public float structureHp;
        public CoreTacticalResistanceSet resistances;
        public float cargoCapacityTons;
        public float citadelHp;
        public float powerPlantModuleHp;
        public Color color;
        public string loadoutSummary;
        public float weaponRangeMultiplier;
        public float reloadMultiplier;
        public float dispersionMultiplier;
        public float reloadRateMultiplier;
        public string hullPackageId;
        public string powerPackageId;
        public string citadelPackageId;
        public string mainPackageId;
        public string secondaryPackageId;
        public string smallPackageId;
        public string auxiliaryPackageId;
    }

    public struct CoreTacticalRuntimeProfileSnapshot
    {
        public string shipId;
        public string displayName;
        public string classId;
        public string roleId;
        public string loadoutKind;
        public Vector3 hullSizeMeters;
        public float maxForwardSpeedMS;
        public float forwardAccelerationMS2;
        public float brakingAccelerationMS2;
        public float maxYawRateDegPerSecond;
        public float maxReverseSpeedMS;
        public float maxLateralSpeedMS;
        public float massKg;
        public float structureHp;
        public CoreTacticalResistanceSet resistances;
        public float cargoCapacityTons;
        public float citadelHp;
        public float powerPlantModuleHp;
        public float actualMaxForwardSpeedMS;
        public float actualForwardAccelerationMS2;
        public float actualBrakingAccelerationMS2;
        public float actualMaxYawRateDegPerSecond;
        public float actualFlatSpeedMS;
        public float actualTargetDistanceMeters;
        public string loadoutSummary;
        public string hullPackageId;
        public string powerPackageId;
        public string citadelPackageId;
        public string mainPackageId;
        public string secondaryPackageId;
        public string smallPackageId;
        public string auxiliaryPackageId;
        public bool mainBatteryActive;
        public bool secondary76Active;
        public bool secondary152Active;
        public bool missileActive;
        public bool machineGunActive;
        public bool autocannonActive;
        public bool torpedoActive;
        public int activeWeaponGroupCount;
        public bool hasAnyFireableWeapon;
        public bool hasAnyNonMissileWeapon;
    }

    private enum TacticalHudActionKind
    {
        Weapon,
        Module,
        Inventory,
        Slip,
        Exit
    }

    private struct TacticalHudAction
    {
        public TacticalHudActionKind kind;
        public CoreTacticalWeaponGroup group;
        public CoreTacticalMiningModule module;
        public string hotkey;
        public string icon;
        public string chargeText;
        public string centerText;
        public bool available;
        public bool disabled;
        public bool active;
        public bool orbit;
        public float shutter;
    }

    private struct HarpoonTargetCandidate
    {
        public Transform targetTransform;
        public Rigidbody targetBody;
        public CoreTacticalPrototypeHealth targetHealth;
        public CoreTacticalAutomatonWreck targetWreck;
        public string targetName;
        public float score;
    }

    private struct BehaviourState
    {
        public Behaviour behaviour;
        public bool enabled;
    }

    private struct CameraState
    {
        public Camera camera;
        public bool enabled;
        public string tag;
    }

    private struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    private struct ColliderState
    {
        public Collider collider;
        public bool enabled;
    }

    public static CoreTacticalCombatSortieController EnsureForActiveSortie(MetaGameState meta = null)
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return null;
        }

        MetaGameState resolvedMeta = meta != null ? meta : Object.FindFirstObjectByType<MetaGameState>();
        if (!IsCoreTacticalCombatSortieActive(resolvedMeta))
        {
            return null;
        }

        CoreTacticalCombatSortieController existing = Object.FindFirstObjectByType<CoreTacticalCombatSortieController>();
        if (existing != null)
        {
            existing.metaGameState = resolvedMeta;
            return existing;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        CoreTacticalCombatSortieController controller = controllerObject.AddComponent<CoreTacticalCombatSortieController>();
        controller.metaGameState = resolvedMeta;
        return controller;
    }

    public static bool IsCoreTacticalCombatSortieActive(MetaGameState meta)
    {
        SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        return meta != null
            && meta.CurrentMode == GameSessionMode.Flight
            && sortie != null
            && sortie.active
            && zone != null
            && zone.sortieId == SessionExtractionConstants.CoreTacticalIntroCombatSortieId;
    }

    public static bool AreCoreMissionObjectivesCompleteForTests(int destroyedEnemies, float minedOreKg)
    {
        return destroyedEnemies >= RequiredEnemyKillObjectiveCount
            && minedOreKg + 0.001f >= RequiredOreObjectiveKg;
    }

    public bool TryBuildForTests()
    {
        return built || TryBuild();
    }

    public static bool TryGetActivePlayerRuntimeSnapshotForTests(out CoreTacticalRuntimeProfileSnapshot snapshot)
    {
        snapshot = default;
        CoreTacticalCombatSortieController instance = activeInstance;
        if (instance == null || !instance.built || instance.playerShip == null)
        {
            return false;
        }

        snapshot = CreateRuntimeProfileSnapshot(
            instance.playerRuntimeProfile,
            instance.playerShip,
            instance.playerShip.GetComponent<CoreTacticalWeaponControl>());
        return true;
    }

    public static bool IsPointerOverMissionGui(Vector2 screenPosition)
    {
        CoreTacticalCombatSortieController instance = activeInstance;
        if (instance == null || instance.missionGuiRects.Count == 0)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        for (int i = 0; i < instance.missionGuiRects.Count; i++)
        {
            if (instance.missionGuiRects[i].Contains(guiPosition))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryInspectScreenPointFromSelection(Vector2 screenPosition)
    {
        CoreTacticalCombatSortieController instance = activeInstance;
        return instance != null && instance.TryInspectScreenRect(BuildInspectionPickRect(screenPosition));
    }

    public static bool TryInspectScreenRectFromSelection(Rect screenRect)
    {
        CoreTacticalCombatSortieController instance = activeInstance;
        return instance != null && instance.TryInspectScreenRect(screenRect);
    }

    public static bool TryInspectHitFromSelection(RaycastHit hit)
    {
        CoreTacticalCombatSortieController instance = activeInstance;
        return instance != null && instance.TryInspectHit(hit);
    }

    public static Rect BuildInspectionPickRectForTests(Vector2 screenPosition)
    {
        return BuildInspectionPickRect(screenPosition);
    }

    public static bool TryGetWorldObjectScreenRectForTests(Camera camera, GameObject target, out Rect rect)
    {
        return TryGetWorldObjectScreenRect(camera, target, InspectionPickMinimumHalfSizePixels, out rect);
    }

    public static bool IsInspectableCombatShipForTests(CoreTacticalShipMotor ship)
    {
        return IsInspectableCombatShip(ship);
    }

    public static bool TrySelectNearestHarpoonTargetForTests(
        CoreTacticalShipMotor autoCatchShip,
        CoreTacticalHarpoonLauncher launcher,
        IReadOnlyList<CoreTacticalShipMotor> candidates,
        out Transform targetTransform)
    {
        targetTransform = null;
        if (autoCatchShip == null || launcher == null || candidates == null)
        {
            return false;
        }

        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!TryBuildHarpoonShipTarget(
                    candidates[i],
                    out Transform candidateTransform,
                    out Rigidbody candidateBody,
                    out _,
                    out _)
                || candidateTransform == autoCatchShip.transform
                || candidateTransform.IsChildOf(autoCatchShip.transform)
                || !launcher.CanLaunchAtTarget(candidateTransform, candidateBody))
            {
                continue;
            }

            Vector3 launchPosition = launcher.GetManualLaunchPosition();
            Vector3 toTarget = candidateTransform.position - launchPosition;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > launcher.ManualRangeMeters)
            {
                continue;
            }

            float score = distance;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            targetTransform = candidateTransform;
        }

        return targetTransform != null;
    }

    private void Start()
    {
        activeInstance = this;
        TryBuild();
    }

    private void Update()
    {
        if (!built)
        {
            TryBuild();
            return;
        }

        if (!IsCoreTacticalCombatSortieActive(ResolveMeta()))
        {
            Destroy(gameObject);
            return;
        }

        MirrorSessionShipToTacticalPlayer();
        UpdateTorpedoAimMode();
        UpdateHarpoonAutoCatch();
        HandleTacticalActionHotkeys();
        HandleMiningModuleHotkeys();
        HandleOreBoulderInspectionInput();
        UpdateMission();
    }

    private void LateUpdate()
    {
        if (built)
        {
            MirrorSessionShipToTacticalPlayer();
        }
    }

    private void OnGUI()
    {
        if (!built)
        {
            return;
        }

        EnsureHudStyles();
        missionGuiRects.Clear();
        DrawWorldUnitBars();
        DrawCoreTacticalHud();
    }

    private void EnsureHudStyles()
    {
        if (hudLabelStyle != null)
        {
            return;
        }

        hudSmallStyle = new GUIStyle(GUI.skin.label);
        hudSmallStyle.fontSize = 11;
        hudSmallStyle.normal.textColor = new Color(0.72f, 0.92f, 1f, 0.90f);
        hudSmallStyle.alignment = TextAnchor.MiddleLeft;

        hudLabelStyle = new GUIStyle(GUI.skin.label);
        hudLabelStyle.fontSize = 12;
        hudLabelStyle.normal.textColor = new Color(0.84f, 0.96f, 1f, 0.96f);
        hudLabelStyle.alignment = TextAnchor.MiddleLeft;

        hudHeaderStyle = new GUIStyle(GUI.skin.label);
        hudHeaderStyle.fontSize = 12;
        hudHeaderStyle.fontStyle = FontStyle.Bold;
        hudHeaderStyle.normal.textColor = new Color(0.94f, 1f, 1f, 1f);
        hudHeaderStyle.alignment = TextAnchor.MiddleLeft;

        hudCenterStyle = new GUIStyle(GUI.skin.label);
        hudCenterStyle.fontSize = 13;
        hudCenterStyle.fontStyle = FontStyle.Bold;
        hudCenterStyle.normal.textColor = new Color(0.93f, 1f, 1f, 1f);
        hudCenterStyle.alignment = TextAnchor.MiddleCenter;

        hudTinyCenterStyle = new GUIStyle(GUI.skin.label);
        hudTinyCenterStyle.fontSize = 10;
        hudTinyCenterStyle.normal.textColor = new Color(0.88f, 1f, 1f, 0.92f);
        hudTinyCenterStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void DrawCoreTacticalHud()
    {
        DrawTopStatusStrip();
        DrawObjectivesPanel();
        DrawRewardsPanel();
        DrawSquadPanel();
        DrawMiniMap();
        DrawSelectedShipPanel();
        DrawShipInventoryWindow();
        DrawOreBoulderInfoPanel();
        DrawLeviathanInfoPanel();
        DrawInspectedShipInfoPanel();
        DrawGasCloudInfoPanel();
    }

    private void DrawTopStatusStrip()
    {
        float width = Mathf.Min(700f, Mathf.Max(360f, Screen.width - 520f));
        Rect rect = new Rect((Screen.width - width) * 0.5f, 10f, width, 38f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.62f);

        int destroyedEnemies = GetDestroyedEnemyObjectiveCount();
        float minedOreKg = GetMissionOreProgressKg();
        string phase = extractionComplete ? "Extracted" : autoExitRequested ? "Extraction" : "Sortie";
        string text = phase
            + " | Enemy ships " + Mathf.Min(destroyedEnemies, RequiredEnemyKillObjectiveCount) + "/" + RequiredEnemyKillObjectiveCount
            + " | Ore " + Mathf.Min(minedOreKg, RequiredOreObjectiveKg).ToString("0") + "/" + RequiredOreObjectiveKg.ToString("0") + " kg"
            + " | Alt " + (fleet != null ? fleet.CommandPlaneAltitudeMeters.ToString("0") : "0") + " m";
        GUI.Label(new Rect(rect.x + 14f, rect.y + 4f, rect.width - 28f, 16f), text, hudCenterStyle);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 20f, rect.width - 28f, 14f), BuildSlipStatusLine(), hudTinyCenterStyle);
    }

    private void DrawObjectivesPanel()
    {
        Rect rect = new Rect(14f, 14f, 306f, 138f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.58f);

        int destroyedEnemies = GetDestroyedEnemyObjectiveCount();
        float minedOreKg = GetMissionOreProgressKg();
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 18f), "Bonus Objectives", hudHeaderStyle);
        DrawObjectiveLine(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 18f), IsEnemyObjectiveComplete(), "Destroy enemy ships " + Mathf.Min(destroyedEnemies, RequiredEnemyKillObjectiveCount) + "/" + RequiredEnemyKillObjectiveCount);
        DrawObjectiveLine(new Rect(rect.x + 12f, rect.y + 54f, rect.width - 24f, 18f), IsOreObjectiveComplete(), "Mine any ore " + Mathf.Min(minedOreKg, RequiredOreObjectiveKg).ToString("0") + "/" + RequiredOreObjectiveKg.ToString("0") + " kg");
        DrawObjectiveLine(new Rect(rect.x + 12f, rect.y + 76f, rect.width - 24f, 18f), extractionComplete, "Extract in Claudian slip");
        GUI.Label(new Rect(rect.x + 12f, rect.y + 100f, rect.width - 24f, 28f), missionStatus, hudSmallStyle);
    }

    private void DrawObjectiveLine(Rect rect, bool complete, string text)
    {
        Color oldColor = GUI.color;
        GUI.color = complete ? new Color(0.46f, 1f, 0.60f, 0.95f) : new Color(0.92f, 0.98f, 1f, 0.90f);
        GUI.Label(rect, (complete ? "[x] " : "[ ] ") + text, hudLabelStyle);
        GUI.color = oldColor;
    }

    private void DrawRewardsPanel()
    {
        Rect rect = new Rect(Screen.width - 286f, 14f, 272f, 100f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.58f);

        SortieZoneDefinition zone = ResolveMeta() != null && ResolveMeta().ActiveSortie != null
            ? ResolveMeta().ActiveSortie.zone
            : null;
        int freight = zone != null ? Mathf.Max(0, zone.completionFreightAward) : pendingFreightReward;
        int experience = zone != null ? Mathf.Max(0, zone.completionDesignExperienceAward) : pendingShipExperienceReward;

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 18f), "Rewards", hudHeaderStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 18f), "Freight: +" + FormatCompactAmount(freight), hudLabelStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 54f, rect.width - 24f, 18f), "Ship XP: +" + FormatCompactAmount(experience), hudLabelStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 74f, rect.width - 24f, 18f), "Reputation: +" + FormatCompactAmount(pendingReputationReward), hudLabelStyle);
    }

    private void DrawSquadPanel()
    {
        Rect rect = new Rect(14f, Screen.height - 136f, 334f, 122f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.60f);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 18f), "Squadron", hudHeaderStyle);

        float cardY = rect.y + 30f;
        float cardWidth = 96f;
        float cardHeight = 58f;
        DrawSquadShipCard(new Rect(rect.x + 12f, cardY, cardWidth, cardHeight), playerShip, "1");
        DrawSquadShipCard(new Rect(rect.x + 118f, cardY, cardWidth, cardHeight), null, "2");
        DrawSquadShipCard(new Rect(rect.x + 224f, cardY, cardWidth, cardHeight), null, "3");

        Rect behaviorA = new Rect(rect.x + 12f, rect.y + 94f, 148f, 20f);
        Rect behaviorB = new Rect(rect.x + 174f, rect.y + 94f, 148f, 20f);
        DrawHudRect(behaviorA, new Color(0.08f, 0.23f, 0.20f, 0.78f));
        DrawHudRect(behaviorB, new Color(0.12f, 0.16f, 0.24f, 0.78f));
        GUI.Label(behaviorA, "Free fire", hudTinyCenterStyle);
        GUI.Label(behaviorB, "Hold formation", hudTinyCenterStyle);
    }

    private void DrawSquadShipCard(Rect rect, CoreTacticalShipMotor ship, string slot)
    {
        bool available = ship != null;
        bool selected = available && ship.IsSelected;
        DrawHudRect(rect, selected ? new Color(0.13f, 0.27f, 0.32f, 0.88f) : new Color(0.04f, 0.12f, 0.16f, 0.78f));
        DrawOutlinedRect(rect, selected ? new Color(1f, 0.78f, 0.22f, 0.95f) : new Color(0.20f, 0.72f, 0.92f, 0.45f));

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && available)
        {
            if (fleet != null)
            {
                IReadOnlyList<CoreTacticalShipMotor> ships = fleet.Ships;
                for (int i = 0; i < ships.Count; i++)
                {
                    if (ships[i] != null)
                    {
                        ships[i].SetSelected(ships[i] == ship);
                    }
                }
            }
            else
            {
                ship.SetSelected(true);
            }
        }

        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 18f, 14f), slot, hudTinyCenterStyle);
        GUI.Label(new Rect(rect.x + 24f, rect.y + 4f, rect.width - 30f, 16f), available ? ship.displayName : "Empty", hudSmallStyle);
        DrawBar(new Rect(rect.x + 8f, rect.y + 27f, rect.width - 16f, 8f), available ? GetShipHealth01(ship) : 0f, new Color(0.24f, 0.90f, 0.38f, 0.95f), new Color(0.05f, 0.14f, 0.08f, 0.85f));
        DrawBar(new Rect(rect.x + 8f, rect.y + 39f, rect.width - 16f, 8f), available ? GetBattery01(ship) : 0f, new Color(0.20f, 0.58f, 1f, 0.95f), new Color(0.03f, 0.08f, 0.18f, 0.85f));
    }

    private void DrawSelectedShipPanel()
    {
        CoreTacticalShipMotor selectedShip = GetSelectedFriendlyShip();
        if (selectedShip == null)
        {
            return;
        }

        const float panelWidth = 660f;
        const float panelHeight = 160f;
        Rect rect = new Rect((Screen.width - panelWidth) * 0.5f, Screen.height - panelHeight - 14f, panelWidth, panelHeight);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, 220f, 18f), selectedShip.displayName, hudHeaderStyle);
        DrawSelectedShipStats(selectedShip, new Rect(rect.x + 14f, rect.y + 30f, 210f, 118f));

        CoreTacticalWeaponControl weaponControl = selectedShip.GetComponent<CoreTacticalWeaponControl>();
        BuildHudActions(selectedShip, weaponControl);
        DrawActionPanel(new Rect(rect.x + 236f, rect.y + 18f, rect.width - 250f, rect.height - 30f), weaponControl);
    }

    private void DrawShipInventoryWindow()
    {
        if (!inventoryWindowOpen)
        {
            return;
        }

        CoreTacticalShipMotor selectedShip = GetSelectedFriendlyShip();
        CoreTacticalMiningRig rig = selectedShip != null ? selectedShip.GetComponent<CoreTacticalMiningRig>() : null;
        if (rig == null)
        {
            inventoryWindowOpen = false;
            return;
        }

        const float width = 346f;
        const float height = 248f;
        float x = Mathf.Clamp(Screen.width - width - 24f, 16f, Mathf.Max(16f, Screen.width - width - 16f));
        float y = Mathf.Clamp(128f, 70f, Mathf.Max(70f, Screen.height - height - 240f));
        Rect rect = new Rect(x, y, width, height);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.72f);

        GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 58f, 18f), "Ship inventory", hudHeaderStyle);
        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y + 8f, 22f, 20f), "X"))
        {
            inventoryWindowOpen = false;
            return;
        }

        GUI.Label(new Rect(rect.x + 14f, rect.y + 34f, rect.width - 28f, 18f), selectedShip.displayName, hudLabelStyle);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 54f, rect.width - 28f, 16f),
            "Cargo: " + rig.InventoryUsedKg.ToString("0") + " / " + rig.InventoryCapacityKg.ToString("0") + " kg",
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 72f, rect.width - 28f, 16f),
            "Free: " + rig.InventoryFreeKg.ToString("0") + " kg | Raw: " + rig.DirtyOreKg.ToString("0") + " kg | Clean: " + rig.CleanOreKg.ToString("0") + " kg",
            hudSmallStyle);

        Rect listRect = new Rect(rect.x + 12f, rect.y + 96f, rect.width - 24f, 104f);
        DrawHudRect(listRect, new Color(0.01f, 0.06f, 0.08f, 0.76f));
        DrawOutlinedRect(listRect, new Color(0.18f, 0.66f, 0.86f, 0.32f));

        rig.BuildInventoryRows(inventoryRows);
        if (inventoryRows.Count == 0)
        {
            GUI.Label(new Rect(listRect.x + 10f, listRect.y + 8f, listRect.width - 20f, 18f), "Empty", hudSmallStyle);
        }
        else
        {
            int rowCount = Mathf.Min(inventoryRows.Count, 5);
            for (int i = 0; i < rowCount; i++)
            {
                CoreTacticalInventoryRow row = inventoryRows[i];
                float rowY = listRect.y + 8f + i * 18f;
                GUI.Label(new Rect(listRect.x + 10f, rowY, listRect.width - 112f, 18f), row.displayName, hudSmallStyle);
                GUI.Label(new Rect(listRect.xMax - 96f, rowY, 84f, 18f), row.amountKg.ToString("0") + " kg", hudSmallStyle);
            }

            if (inventoryRows.Count > rowCount)
            {
                GUI.Label(new Rect(listRect.x + 10f, listRect.yMax - 20f, listRect.width - 20f, 18f), "+" + (inventoryRows.Count - rowCount).ToString() + " more stacks", hudSmallStyle);
            }
        }

        string message = rig.LastInventoryMessage;
        if (!string.IsNullOrWhiteSpace(message))
        {
            GUI.Label(new Rect(rect.x + 14f, rect.yMax - 38f, rect.width - 28f, 30f), message, hudSmallStyle);
        }
    }

    private void DrawOreBoulderInfoPanel()
    {
        if (inspectedOreBoulder == null || inspectedOreBoulder.CurrentHealth01 <= 0f)
        {
            return;
        }

        Rect rect = new Rect(14f, 140f, 306f, 162f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 50f, 18f), inspectedOreBoulder.OreDisplayName, hudHeaderStyle);
        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y + 8f, 22f, 20f), "X"))
        {
            inspectedOreBoulder = null;
            return;
        }

        GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 16f),
            "HP: " + inspectedOreBoulder.CurrentHealth.ToString("0") + " / " + inspectedOreBoulder.MaxHealth.ToString("0"),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 49f, rect.width - 24f, 16f),
            "Diameter: " + inspectedOreBoulder.EffectiveDiameterMeters.ToString("0") + " m | Resist: " + FormatDamageResistances(inspectedOreBoulder.Resistances),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, rect.width - 24f, 16f),
            "Mass: " + FormatMassKg(inspectedOreBoulder.PhysicalMassKg),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 83f, rect.width - 24f, 16f),
            "Drift: " + FormatVerticalDrift(inspectedOreBoulder.VerticalDriftMS),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 100f, rect.width - 24f, 16f),
            "Shedding: " + inspectedOreBoulder.NaturalIntegrityLossPerSecond.ToString("0.0") + " HP/s | " + inspectedOreBoulder.NaturalFragmentMassPerSecond.ToString("0.0") + " kg/s",
            hudSmallStyle);

        CoreTacticalShipMotor commandShip = GetOreCommandShip();
        bool hasCommandShip = commandShip != null;
        bool isTargeted = IsShipTargetingInspectedOreBoulder(commandShip);
        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = hasCommandShip && !isTargeted;
        if (GUI.Button(new Rect(rect.x + 12f, rect.y + 126f, 132f, 24f), "TARGET"))
        {
            SetInspectedOreBoulderAsPriorityTarget(commandShip);
        }

        GUI.enabled = hasCommandShip && isTargeted;
        if (GUI.Button(new Rect(rect.x + 154f, rect.y + 126f, 132f, 24f), "CLEAR"))
        {
            ClearSelectedShipPriorityTarget(commandShip);
        }

        GUI.enabled = previousGuiEnabled;
    }

    private void DrawLeviathanInfoPanel()
    {
        CoreTacticalShipMotor leviathanShip = ResolveInspectedLeviathanShip();
        if (inspectedLeviathan == null || leviathanShip == null || !IsAlive(leviathanShip))
        {
            return;
        }

        Rect rect = new Rect(14f, 140f, 306f, 182f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 50f, 18f), leviathanShip.displayName, hudHeaderStyle);
        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y + 8f, 22f, 20f), "X"))
        {
            inspectedLeviathan = null;
            return;
        }

        CoreTacticalDamageProfile damageProfile = leviathanShip.GetComponent<CoreTacticalDamageProfile>();
        string resistanceLine = damageProfile != null ? FormatDamageResistances(damageProfile.resistances) : FormatDamageResistances(default);
        string attackState = inspectedLeviathan.IsAttackCommitted
            ? "attacking"
            : inspectedLeviathan.Aggression01 >= 0.999f ? "ready" : "watching";

        GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 16f),
            "HP: " + GetShipHealthLine(leviathanShip),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 49f, rect.width - 24f, 16f),
            "Length: " + inspectedLeviathan.lengthMeters.ToString("0") + " m | Mass: " + FormatMassKg(leviathanShip.massKg),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, rect.width - 24f, 16f),
            "Resist: " + resistanceLine,
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 83f, rect.width - 24f, 16f),
            "Aggression: " + (inspectedLeviathan.Aggression01 * 100f).ToString("0") + "% | " + attackState,
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 100f, rect.width - 24f, 16f),
            "Bite danger below " + (inspectedLeviathan.lengthMeters * CoreTacticalLeviathanController.BiteAllowedTargetLengthRatio).ToString("0") + " m",
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 117f, rect.width - 24f, 16f),
            "Speed: " + leviathanShip.maxForwardSpeedMS.ToString("0") + " m/s | Range: " + inspectedLeviathan.aggressionRadiusMeters.ToString("0") + " m",
            hudSmallStyle);

        CoreTacticalShipMotor commandShip = GetOreCommandShip();
        bool hasCommandShip = commandShip != null;
        bool isTargeted = IsShipTargetingInspectedLeviathan(commandShip);
        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = hasCommandShip && !isTargeted;
        if (GUI.Button(new Rect(rect.x + 12f, rect.y + 146f, 132f, 24f), "ATTACK"))
        {
            SetInspectedLeviathanAsPriorityTarget(commandShip);
        }

        GUI.enabled = hasCommandShip && isTargeted;
        if (GUI.Button(new Rect(rect.x + 154f, rect.y + 146f, 132f, 24f), "CLEAR"))
        {
            ClearSelectedShipPriorityTarget(commandShip);
        }

        GUI.enabled = previousGuiEnabled;
    }

    private void DrawInspectedShipInfoPanel()
    {
        if (inspectedShip == null || !IsAlive(inspectedShip))
        {
            return;
        }

        Rect rect = new Rect(14f, 140f, 306f, 166f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 50f, 18f), inspectedShip.displayName, hudHeaderStyle);
        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y + 8f, 22f, 20f), "X"))
        {
            inspectedShip = null;
            return;
        }

        CoreTacticalDamageProfile damageProfile = inspectedShip.GetComponent<CoreTacticalDamageProfile>();
        string resistanceLine = damageProfile != null ? FormatDamageResistances(damageProfile.resistances) : FormatDamageResistances(default);
        Vector3 size = inspectedShip.hullSizeMeters;

        GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 16f),
            "HP: " + GetShipHealthLine(inspectedShip),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 49f, rect.width - 24f, 16f),
            "Size: " + size.z.ToString("0") + " m | Mass: " + FormatMassKg(inspectedShip.massKg),
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, rect.width - 24f, 16f),
            "Resist: " + resistanceLine,
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 83f, rect.width - 24f, 16f),
            "Speed: " + inspectedShip.maxForwardSpeedMS.ToString("0") + " m/s | Turn: " + inspectedShip.maxYawRateDegPerSecond.ToString("0") + " deg/s",
            hudSmallStyle);

        CoreTacticalShipMotor commandShip = GetOreCommandShip();
        bool hasCommandShip = commandShip != null;
        bool isTargeted = IsShipTargetingTarget(commandShip, inspectedShip);
        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = hasCommandShip && !isTargeted;
        if (GUI.Button(new Rect(rect.x + 12f, rect.y + 128f, 132f, 24f), "ATTACK"))
        {
            SetPriorityTarget(commandShip, inspectedShip, inspectedShip.displayName);
        }

        GUI.enabled = hasCommandShip && isTargeted;
        if (GUI.Button(new Rect(rect.x + 154f, rect.y + 128f, 132f, 24f), "CLEAR"))
        {
            ClearSelectedShipPriorityTarget(commandShip);
        }

        GUI.enabled = previousGuiEnabled;
    }

    private void DrawGasCloudInfoPanel()
    {
        if (inspectedGasCloud == null)
        {
            return;
        }

        Rect rect = new Rect(14f, 140f, 306f, 148f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 50f, 18f), inspectedGasCloud.DisplayName, hudHeaderStyle);
        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y + 8f, 22f, 20f), "X"))
        {
            inspectedGasCloud = null;
            return;
        }

        GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 16f),
            "Raw: " + inspectedGasCloud.RawVolumeLiters.ToString("0") + " L | Useful: " + inspectedGasCloud.UsefulVolumeLiters.ToString("0") + " L",
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 49f, rect.width - 24f, 16f),
            "Concentration: " + (inspectedGasCloud.UsefulConcentration01 * 100f).ToString("0") + "% | Volume: " + (inspectedGasCloud.Volume01 * 100f).ToString("0") + "%",
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 66f, rect.width - 24f, 16f),
            "Harvest: " + (inspectedGasCloud.CanHarvest ? "yes" : "no") + " | Damage: " + inspectedGasCloud.ChemicalDamagePerMinute.ToString("0") + "/min",
            hudSmallStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 83f, rect.width - 24f, 16f),
            "Drift: " + FormatFlatDrift(inspectedGasCloud.DriftVelocityMS),
            hudSmallStyle);
    }

    private CoreTacticalShipMotor GetOreCommandShip()
    {
        return GetSelectedFriendlyShip() ?? playerShip;
    }

    private CoreTacticalShipMotor ResolveInspectedLeviathanShip()
    {
        if (inspectedLeviathan == null)
        {
            return null;
        }

        return inspectedLeviathan.ship != null
            ? inspectedLeviathan.ship
            : inspectedLeviathan.GetComponent<CoreTacticalShipMotor>();
    }

    private bool IsShipTargetingInspectedOreBoulder(CoreTacticalShipMotor commandShip)
    {
        if (commandShip == null || inspectedOreBoulder == null)
        {
            return false;
        }

        CoreTacticalShipMotor targetShip = inspectedOreBoulder.GetComponent<CoreTacticalShipMotor>();
        return IsShipTargetingTarget(commandShip, targetShip);
    }

    private bool IsShipTargetingTarget(CoreTacticalShipMotor commandShip, CoreTacticalShipMotor targetShip)
    {
        if (commandShip == null || targetShip == null)
        {
            return false;
        }

        CoreTacticalPriorityTargetControl priority = commandShip.GetComponent<CoreTacticalPriorityTargetControl>();
        return priority != null
            && priority.TryGetPriorityTarget(CoreTacticalCombatTeam.Enemy, out CoreTacticalShipMotor priorityTarget)
            && priorityTarget == targetShip;
    }

    private void SetInspectedOreBoulderAsPriorityTarget(CoreTacticalShipMotor commandShip)
    {
        if (commandShip == null || inspectedOreBoulder == null)
        {
            return;
        }

        CoreTacticalShipMotor targetShip = inspectedOreBoulder.GetComponent<CoreTacticalShipMotor>();
        if (!CoreTacticalOreTargetingRules.IsValidExplicitTarget(targetShip, CoreTacticalCombatTeam.Enemy))
        {
            return;
        }

        SetPriorityTarget(commandShip, targetShip, inspectedOreBoulder.OreDisplayName);
    }

    private bool IsShipTargetingInspectedLeviathan(CoreTacticalShipMotor commandShip)
    {
        CoreTacticalShipMotor targetShip = ResolveInspectedLeviathanShip();
        return IsShipTargetingTarget(commandShip, targetShip);
    }

    private void SetInspectedLeviathanAsPriorityTarget(CoreTacticalShipMotor commandShip)
    {
        CoreTacticalShipMotor targetShip = ResolveInspectedLeviathanShip();
        if (commandShip == null || targetShip == null)
        {
            return;
        }

        if (!CoreTacticalOreTargetingRules.IsValidExplicitTarget(targetShip, CoreTacticalCombatTeam.Enemy))
        {
            return;
        }

        SetPriorityTarget(commandShip, targetShip, targetShip.displayName);
    }

    private void SetPriorityTarget(CoreTacticalShipMotor commandShip, CoreTacticalShipMotor targetShip, string targetName)
    {
        if (commandShip == null || !CoreTacticalOreTargetingRules.IsValidExplicitTarget(targetShip, CoreTacticalCombatTeam.Enemy))
        {
            return;
        }

        CoreTacticalPriorityTargetControl priority = commandShip.GetComponent<CoreTacticalPriorityTargetControl>();
        if (priority == null)
        {
            priority = commandShip.gameObject.AddComponent<CoreTacticalPriorityTargetControl>();
        }

        priority.SetPriorityTarget(targetShip);
        missionStatus = "Target selected: " + (string.IsNullOrWhiteSpace(targetName) ? targetShip.displayName : targetName);
    }

    private void ClearSelectedShipPriorityTarget(CoreTacticalShipMotor commandShip)
    {
        CoreTacticalPriorityTargetControl priority = commandShip != null ? commandShip.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        if (priority != null)
        {
            priority.ClearPriorityTarget();
        }

        missionStatus = "Target cleared.";
    }

    private void DrawSelectedShipStats(CoreTacticalShipMotor ship, Rect rect)
    {
        float speed = 0f;
        if (ship != null && ship.Body != null)
        {
            Vector3 velocity = ship.Body.linearVelocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
        }

        float massTons = ship != null ? Mathf.Max(0f, ship.massKg / 1000f) : 0f;
        float profileSize = ship != null
            ? Mathf.Max(ship.hullSizeMeters.x, Mathf.Max(ship.hullSizeMeters.y, ship.hullSizeMeters.z))
            : 0f;

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16f), "Speed: " + FormatShipSpeedLine(ship, speed), hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 17f, rect.width, 16f), "Hull: " + GetShipHealthLine(ship), hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 34f, rect.width, 16f), "Battery: " + GetBatteryLine(ship), hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 51f, rect.width, 16f), "Mass: " + massTons.ToString("0") + " t", hudSmallStyle);
        if (playerMiningRig != null && ship == playerShip)
        {
            GUI.Label(new Rect(rect.x, rect.y + 68f, rect.width, 16f), playerMiningRig.BuildCargoLine(), hudSmallStyle);
            GUI.Label(new Rect(rect.x, rect.y + 85f, rect.width, 16f), playerMiningRig.BuildInstalledModuleStatusLine(), hudSmallStyle);
            GUI.Label(new Rect(rect.x, rect.y + 102f, rect.width, 16f), "Profile: " + profileSize.ToString("0") + " m | Detect: 4.2 km", hudSmallStyle);
        }
        else
        {
            GUI.Label(new Rect(rect.x, rect.y + 68f, rect.width, 16f), "Payload: 0 t", hudSmallStyle);
            GUI.Label(new Rect(rect.x, rect.y + 85f, rect.width, 16f), "Profile: " + profileSize.ToString("0") + " m | Detect: 4.2 km", hudSmallStyle);
        }
    }

    private void BuildHudActions(CoreTacticalShipMotor selectedShip, CoreTacticalWeaponControl weaponControl)
    {
        hudActions.Clear();
        if (weaponControl != null)
        {
            for (int i = 0; i < TacticalHudWeaponGroups.Length; i++)
            {
                CoreTacticalWeaponGroup group = TacticalHudWeaponGroups[i];
                if (!weaponControl.IsWeaponGroupActive(group))
                {
                    continue;
                }

                bool enabled = weaponControl.IsFireEnabled(group);
                bool isTorpedoAction = group == CoreTacticalWeaponGroup.Torpedoes;
                bool torpedoLauncherReady = !isTorpedoAction || TryGetManualTorpedoSlotLauncher(selectedShip, out _, out _);
                bool activeHarpoonLink = isTorpedoAction && TryGetActiveHarpoonLauncher(selectedShip, out _);
                bool designatedHarpoonTarget = isTorpedoAction && TryGetDesignatedHarpoonLauncher(selectedShip, out _);
                bool autoHarpoonCatch = isTorpedoAction && harpoonAutoCatchActive && harpoonAutoCatchShip == selectedShip;
                string cooldownText = BuildWeaponCooldownText(selectedShip, group);
                hudActions.Add(new TacticalHudAction
                {
                    kind = TacticalHudActionKind.Weapon,
                    group = group,
                    hotkey = GetActionHotkey(group),
                    icon = GetActionIcon(weaponControl, group),
                    chargeText = "",
                    centerText = !string.IsNullOrWhiteSpace(cooldownText)
                        ? cooldownText
                        : autoHarpoonCatch ? "AUTO"
                        : activeHarpoonLink ? "LINK"
                        : designatedHarpoonTarget ? "LOCK"
                        : isTorpedoAction && torpedoAimActive && torpedoAimShip == selectedShip ? torpedoAimIsHarpoonTargetSelection ? "TGT" : "AIM" : "",
                    available = (!weaponControl.fireSuppressed || activeHarpoonLink) && torpedoLauncherReady,
                    disabled = (!activeHarpoonLink && weaponControl.fireSuppressed) || !torpedoLauncherReady || (!enabled && !isTorpedoAction),
                    active = autoHarpoonCatch || (isTorpedoAction && torpedoAimActive && torpedoAimShip == selectedShip),
                    orbit = autoHarpoonCatch,
                    shutter = 0f
                });
            }
        }

        if (playerMiningRig != null && selectedShip == playerShip)
        {
            AddMiningModuleHudAction(CoreTacticalMiningModule.Magnet, "M", "MAG");
            AddMiningModuleHudAction(CoreTacticalMiningModule.Drill, "L", "DRL");
            AddMiningModuleHudAction(CoreTacticalMiningModule.Crusher, "C", "CRSH");
            AddMiningModuleHudAction(CoreTacticalMiningModule.Siphon, "S", "SIPH");
            AddMiningModuleHudAction(CoreTacticalMiningModule.CloudConcentrator, "K", "CONC");
            hudActions.Add(new TacticalHudAction
            {
                kind = TacticalHudActionKind.Inventory,
                hotkey = "I",
                icon = "INV",
                chargeText = "",
                centerText = inventoryWindowOpen ? "OPEN" : "",
                available = true,
                disabled = false,
                active = inventoryWindowOpen,
                shutter = 0f
            });
        }

        hudActions.Add(new TacticalHudAction
        {
            kind = TacticalHudActionKind.Slip,
            hotkey = "F",
            icon = "SLIP",
            chargeText = "",
            centerText = playerSlipDrive != null && playerSlipDrive.IsActive
                ? "ON"
                : playerSlipDrive != null && playerSlipDrive.IsArmed ? "ARM" : "",
            available = playerSlipDrive != null && !extractionComplete,
            disabled = playerSlipDrive == null || extractionComplete,
            active = playerSlipDrive != null && playerSlipDrive.IsActive,
            shutter = playerSlipDrive != null && playerSlipDrive.IsActive
                ? Mathf.Clamp01((playerSlipDrive.CurrentForwardSpeedMultiplier - 1f) / Mathf.Max(0.1f, playerSlipDrive.TargetForwardSpeedMultiplier - 1f))
                : 0f
        });

        hudActions.Add(new TacticalHudAction
        {
            kind = TacticalHudActionKind.Exit,
            hotkey = "Y",
            icon = "EXIT",
            chargeText = "",
            centerText = autoExitRequested ? "RUN" : "",
            available = !extractionComplete,
            disabled = extractionComplete,
            active = autoExitRequested,
            shutter = autoExitRequested ? 0.5f : 0f
        });
    }

    public static string GetWeaponCooldownTextForTests(CoreTacticalShipMotor ship, CoreTacticalWeaponGroup group)
    {
        return BuildWeaponCooldownText(ship, group);
    }

    public static string FormatWeaponCooldownSecondsForTests(float remainingSeconds)
    {
        return FormatWeaponCooldownSeconds(remainingSeconds);
    }

    public static string GetMiningModuleCooldownTextForTests(CoreTacticalMiningRig rig, CoreTacticalMiningModule module)
    {
        return rig != null ? FormatWeaponCooldownSeconds(rig.GetModuleCooldownRemainingSeconds(module)) : "";
    }

    public static float GetMiningModuleCooldownShutterForTests(CoreTacticalMiningRig rig, CoreTacticalMiningModule module)
    {
        return rig != null ? rig.GetModuleCooldown01(module) : 0f;
    }

    private static string BuildWeaponCooldownText(CoreTacticalShipMotor ship, CoreTacticalWeaponGroup group)
    {
        return FormatWeaponCooldownSeconds(GetWeaponCooldownRemainingSeconds(ship, group));
    }

    private static string FormatWeaponCooldownSeconds(float remainingSeconds)
    {
        if (remainingSeconds <= 0.05f)
        {
            return "";
        }

        return Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)).ToString();
    }

    private static float GetWeaponCooldownRemainingSeconds(CoreTacticalShipMotor ship, CoreTacticalWeaponGroup group)
    {
        if (ship == null || group == CoreTacticalWeaponGroup.MachineGuns)
        {
            return 0f;
        }

        float remaining = 0f;
        CoreTacticalMissileLauncher[] launchers = ship.GetComponents<CoreTacticalMissileLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalMissileLauncher launcher = launchers[i];
            if (launcher != null && launcher.weaponGroup == group)
            {
                remaining = Mathf.Max(remaining, launcher.ReloadCooldownRemainingSeconds);
            }
        }

        CoreTacticalHarpoonLauncher[] harpoonLaunchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < harpoonLaunchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher launcher = harpoonLaunchers[i];
            if (launcher != null && launcher.weaponGroup == group)
            {
                remaining = Mathf.Max(remaining, launcher.ReloadCooldownRemainingSeconds);
            }
        }

        switch (group)
        {
            case CoreTacticalWeaponGroup.MainBattery:
                CoreTacticalMainBattery mainBattery = ship.GetComponent<CoreTacticalMainBattery>();
                if (mainBattery != null)
                {
                    remaining = Mathf.Max(remaining, mainBattery.GetReloadCooldownRemainingSecondsForHud());
                }

                break;
            case CoreTacticalWeaponGroup.Secondary76mm:
            case CoreTacticalWeaponGroup.Secondary152mm:
                CoreTacticalSecondaryMountBattery secondaryBattery = ship.GetComponent<CoreTacticalSecondaryMountBattery>();
                if (secondaryBattery != null)
                {
                    remaining = Mathf.Max(remaining, secondaryBattery.GetReloadCooldownRemainingSecondsForHud(group));
                }

                break;
            case CoreTacticalWeaponGroup.Autocannon30mm:
                CoreTacticalFrigateAutocannonBattery autocannon = ship.GetComponent<CoreTacticalFrigateAutocannonBattery>();
                if (autocannon != null)
                {
                    remaining = Mathf.Max(remaining, autocannon.GetReloadCooldownRemainingSecondsForHud());
                }

                break;
        }

        return remaining;
    }

    private void AddMiningModuleHudAction(CoreTacticalMiningModule module, string hotkey, string icon)
    {
        if (playerMiningRig == null || !playerMiningRig.IsModuleInstalled(module))
        {
            return;
        }

        bool enabled = playerMiningRig.IsModuleEnabled(module);
        bool brownout = playerMiningRig.IsModuleBrownout(module);
        float cooldownRemaining = playerMiningRig.GetModuleCooldownRemainingSeconds(module);
        string cooldownText = FormatWeaponCooldownSeconds(cooldownRemaining);
        hudActions.Add(new TacticalHudAction
        {
            kind = TacticalHudActionKind.Module,
            module = module,
            hotkey = hotkey,
            icon = icon,
            chargeText = "",
            centerText = !string.IsNullOrWhiteSpace(cooldownText) ? cooldownText : playerMiningRig.GetModuleStatus(module),
            available = true,
            disabled = brownout,
            active = enabled,
            shutter = playerMiningRig.GetModuleCooldown01(module)
        });
    }

    private void DrawActionPanel(Rect rect, CoreTacticalWeaponControl weaponControl)
    {
        const float slotSize = 50f;
        const float chargeHeight = 13f;
        const float gap = 7f;
        const int maxColumns = 8;

        int count = Mathf.Min(hudActions.Count, 16);
        for (int i = 0; i < count; i++)
        {
            int column = i % maxColumns;
            int row = i / maxColumns;
            Rect slotRect = new Rect(rect.x + column * (slotSize + gap), rect.y + row * (slotSize + chargeHeight + gap), slotSize, slotSize);
            Rect chargeRect = new Rect(slotRect.x, slotRect.yMax + 1f, slotSize, chargeHeight);
            DrawActionSlot(slotRect, chargeRect, hudActions[i], weaponControl);
        }
    }

    private void DrawActionSlot(Rect rect, Rect chargeRect, TacticalHudAction action, CoreTacticalWeaponControl weaponControl)
    {
        Color baseColor = action.disabled
            ? new Color(0.32f, 0.08f, 0.07f, 0.86f)
            : action.active
                ? new Color(0.10f, 0.34f, 0.32f, 0.88f)
                : new Color(0.05f, 0.13f, 0.18f, 0.88f);
        DrawHudRect(rect, baseColor);
        DrawOutlinedRect(rect, action.disabled ? new Color(0.95f, 0.26f, 0.18f, 0.78f) : new Color(0.22f, 0.78f, 0.98f, 0.55f));
        if (action.orbit)
        {
            DrawActionOrbit(rect);
        }

        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 12f), action.hotkey, hudTinyCenterStyle);
        GUI.Label(new Rect(rect.x + 4f, rect.y + 15f, rect.width - 8f, 20f), action.icon, hudCenterStyle);

        if (!string.IsNullOrWhiteSpace(action.centerText))
        {
            GUI.Label(new Rect(rect.x + 4f, rect.y + 31f, rect.width - 8f, 15f), action.centerText, hudTinyCenterStyle);
        }

        float shutter = Mathf.Clamp01(action.shutter);
        if (shutter > 0.001f)
        {
            Color shutterColor = action.active ? new Color(0.82f, 0.10f, 0.08f, 0.38f) : new Color(0f, 0f, 0f, 0.48f);
            Rect shutterRect = new Rect(rect.x, rect.y, rect.width, rect.height * shutter);
            DrawHudRect(shutterRect, shutterColor);
        }

        if (!string.IsNullOrWhiteSpace(action.chargeText))
        {
            DrawHudRect(chargeRect, new Color(0.02f, 0.07f, 0.09f, 0.84f));
            DrawOutlinedRect(chargeRect, new Color(0.18f, 0.62f, 0.86f, 0.38f));
            GUI.Label(chargeRect, action.chargeText, hudTinyCenterStyle);
        }

        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && action.available;
        Rect hitRect = new Rect(rect.x, rect.y, rect.width, rect.height + (!string.IsNullOrWhiteSpace(action.chargeText) ? chargeRect.height + 1f : 0f));
        Event currentEvent = Event.current;
        if (currentEvent != null
            && currentEvent.type == EventType.MouseDown
            && currentEvent.button == 1
            && hitRect.Contains(currentEvent.mousePosition))
        {
            HandleHudActionRightClick(action, weaponControl);
            currentEvent.Use();
        }

        if (GUI.Button(hitRect, GUIContent.none, GUIStyle.none))
        {
            HandleHudAction(action, weaponControl);
        }

        GUI.enabled = wasEnabled;
    }

    private static void DrawActionOrbit(Rect rect)
    {
        float t = Mathf.Repeat(Time.time * 1.8f, 1f);
        Color color = new Color(0.28f, 0.92f, 1f, 0.92f);
        float length = Mathf.Max(9f, rect.width * 0.28f);
        float thickness = 2f;
        int segment = Mathf.FloorToInt(t * 4f) & 3;
        float local = Mathf.Repeat(t * 4f, 1f);
        switch (segment)
        {
            case 0:
                DrawHudRect(new Rect(Mathf.Lerp(rect.x, rect.xMax - length, local), rect.y - 1f, length, thickness), color);
                break;
            case 1:
                DrawHudRect(new Rect(rect.xMax - 1f, Mathf.Lerp(rect.y, rect.yMax - length, local), thickness, length), color);
                break;
            case 2:
                DrawHudRect(new Rect(Mathf.Lerp(rect.xMax - length, rect.x, local), rect.yMax - 1f, length, thickness), color);
                break;
            default:
                DrawHudRect(new Rect(rect.x - 1f, Mathf.Lerp(rect.yMax - length, rect.y, local), thickness, length), color);
                break;
        }
    }

    private void HandleHudAction(TacticalHudAction action, CoreTacticalWeaponControl weaponControl)
    {
        switch (action.kind)
        {
            case TacticalHudActionKind.Weapon:
                if (weaponControl != null)
                {
                    if (action.group == CoreTacticalWeaponGroup.Torpedoes)
                    {
                        BeginTorpedoAim(GetSelectedFriendlyShip());
                    }
                    else
                    {
                        weaponControl.ToggleFireEnabled(action.group);
                    }
                }

                break;
            case TacticalHudActionKind.Module:
                if (playerMiningRig != null)
                {
                    playerMiningRig.ToggleModule(action.module);
                }

                break;
            case TacticalHudActionKind.Inventory:
                inventoryWindowOpen = !inventoryWindowOpen;
                break;
            case TacticalHudActionKind.Slip:
                ToggleClaudianSlip();
                break;
            case TacticalHudActionKind.Exit:
                BeginAutoExit();
                break;
        }
    }

    private void HandleHudActionRightClick(TacticalHudAction action, CoreTacticalWeaponControl weaponControl)
    {
        if (action.kind != TacticalHudActionKind.Weapon || action.group != CoreTacticalWeaponGroup.Torpedoes || weaponControl == null)
        {
            return;
        }

        CoreTacticalShipMotor ship = GetSelectedFriendlyShip();
        if (ship == null || !TryGetHarpoonLauncher(ship, out _))
        {
            return;
        }

        ToggleHarpoonAutoCatch(ship);
    }

    private void ToggleHarpoonAutoCatch(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            harpoonAutoCatchActive = false;
            harpoonAutoCatchShip = null;
            return;
        }

        if (harpoonAutoCatchActive && harpoonAutoCatchShip == ship)
        {
            harpoonAutoCatchActive = false;
            harpoonAutoCatchShip = null;
            missionStatus = "Harpoon auto-catch disabled.";
            return;
        }

        harpoonAutoCatchActive = true;
        harpoonAutoCatchShip = ship;
        nextHarpoonAutoCatchScanTime = 0f;
        CancelTorpedoAim();
        missionStatus = "Harpoon auto-catch enabled. Free launchers will catch valid targets.";
    }

    private void UpdateHarpoonAutoCatch()
    {
        if (!harpoonAutoCatchActive)
        {
            return;
        }

        if (harpoonAutoCatchShip == null || !IsAlive(harpoonAutoCatchShip))
        {
            harpoonAutoCatchActive = false;
            harpoonAutoCatchShip = null;
            return;
        }

        if (Time.time < nextHarpoonAutoCatchScanTime)
        {
            return;
        }

        nextHarpoonAutoCatchScanTime = Time.time + 0.25f;
        CoreTacticalWeaponControl weaponControl = harpoonAutoCatchShip.GetComponent<CoreTacticalWeaponControl>();
        if (weaponControl == null
            || weaponControl.fireSuppressed
            || !weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Torpedoes))
        {
            return;
        }

        CoreTacticalHarpoonLauncher[] launchers = harpoonAutoCatchShip.GetComponents<CoreTacticalHarpoonLauncher>();
        int designatedCount = 0;
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher launcher = launchers[i];
            if (launcher == null
                || launcher.weaponGroup != CoreTacticalWeaponGroup.Torpedoes
                || launcher.HasActiveLink
                || launcher.HasFlyingProjectile)
            {
                continue;
            }

            if (TryFindBestHarpoonTargetForLauncher(launcher, out HarpoonTargetCandidate target))
            {
                if (launcher.DesignateTarget(target.targetTransform, target.targetBody, target.targetHealth, target.targetWreck))
                {
                    designatedCount++;
                }
            }
            else
            {
                launcher.ClearDesignatedTarget();
            }
        }

        if (designatedCount > 0)
        {
            missionStatus = "Harpoon auto-catch designated " + designatedCount + " target" + (designatedCount == 1 ? "." : "s.");
        }
    }

    private bool TryFindBestHarpoonTargetForLauncher(CoreTacticalHarpoonLauncher launcher, out HarpoonTargetCandidate best)
    {
        best = default;
        best.score = float.PositiveInfinity;
        if (launcher == null || harpoonAutoCatchShip == null)
        {
            return false;
        }

        for (int i = 0; i < enemyShips.Count; i++)
        {
            if (TryBuildHarpoonShipTarget(enemyShips[i], out Transform targetTransform, out Rigidbody targetBody, out CoreTacticalPrototypeHealth targetHealth, out string targetName))
            {
                TryConsiderHarpoonTarget(launcher, targetTransform, targetBody, targetHealth, null, targetName, ref best);
            }
        }

        CoreTacticalLeviathanController[] leviathans = FindObjectsByType<CoreTacticalLeviathanController>(FindObjectsSortMode.None);
        for (int i = 0; i < leviathans.Length; i++)
        {
            CoreTacticalLeviathanController leviathan = leviathans[i];
            CoreTacticalShipMotor ship = leviathan != null ? leviathan.ship : null;
            if (ship != null
                && TryBuildHarpoonShipTarget(ship, out Transform targetTransform, out Rigidbody targetBody, out CoreTacticalPrototypeHealth targetHealth, out string targetName))
            {
                TryConsiderHarpoonTarget(launcher, targetTransform, targetBody, targetHealth, null, targetName, ref best);
            }
        }

        CoreTacticalAutomatonWreck[] wrecks = FindObjectsByType<CoreTacticalAutomatonWreck>(FindObjectsSortMode.None);
        for (int i = 0; i < wrecks.Length; i++)
        {
            CoreTacticalAutomatonWreck wreck = wrecks[i];
            if (wreck == null || !CoreTacticalHarpoonLauncher.IsCatchTargetValid(wreck.transform, null, wreck))
            {
                continue;
            }

            TryConsiderHarpoonTarget(
                launcher,
                wreck.transform,
                wreck.GetComponent<Rigidbody>(),
                null,
                wreck,
                string.IsNullOrWhiteSpace(wreck.displayName) ? wreck.name : wreck.displayName,
                ref best);
        }

        return best.targetTransform != null;
    }

    private void TryConsiderHarpoonTarget(
        CoreTacticalHarpoonLauncher launcher,
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        string targetName,
        ref HarpoonTargetCandidate best)
    {
        if (launcher == null
            || targetTransform == null
            || targetTransform == harpoonAutoCatchShip.transform
            || targetTransform.IsChildOf(harpoonAutoCatchShip.transform))
        {
            return;
        }

        Vector3 launchPosition = launcher.GetManualLaunchPosition();
        Vector3 toTarget = targetTransform.position - launchPosition;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f
            || distance > launcher.ManualRangeMeters
            || !launcher.IsDirectionInsideManualSector(toTarget)
            || !launcher.CanLaunchAtTarget(targetTransform, targetBody))
        {
            return;
        }

        float score = distance;
        if (score >= best.score)
        {
            return;
        }

        best.targetTransform = targetTransform;
        best.targetBody = targetBody;
        best.targetHealth = targetHealth;
        best.targetWreck = targetWreck;
        best.targetName = targetName;
        best.score = score;
    }

    private void BeginTorpedoAim(CoreTacticalShipMotor ship)
    {
        if (ship != null && TryGetActiveHarpoonLauncher(ship, out CoreTacticalHarpoonLauncher activeHarpoon))
        {
            activeHarpoon.ReleaseActiveLink("manual release");
            missionStatus = "Harpoon cable released.";
            CancelTorpedoAim();
            return;
        }

        if (ship == null || !TryGetManualTorpedoSlotLauncher(ship, out _, out CoreTacticalHarpoonLauncher harpoonSlot))
        {
            missionStatus = "No torpedo or harpoon launcher ready on the selected ship.";
            CancelTorpedoAim();
            return;
        }

        CoreTacticalWeaponControl weaponControl = ship.GetComponent<CoreTacticalWeaponControl>();
        if (weaponControl == null || !weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Torpedoes))
        {
            missionStatus = "No torpedo weapon group on the selected ship.";
            CancelTorpedoAim();
            return;
        }

        if (weaponControl.fireSuppressed)
        {
            missionStatus = harpoonSlot != null
                ? "Harpoons are locked while Claudian slip is active."
                : "Torpedoes are locked while Claudian slip is active.";
            CancelTorpedoAim();
            return;
        }

        torpedoAimActive = true;
        torpedoAimShip = ship;
        torpedoAimLauncher = null;
        torpedoAimHarpoonLauncher = null;
        torpedoAimIsHarpoonTargetSelection = harpoonSlot != null;
        torpedoAimDirection = FlattenDirection(ship.transform.forward, Vector3.forward);
        torpedoAimPoint = ship.transform.position + torpedoAimDirection * 300f;
        CoreTacticalFleetController.TacticalPointerInputBlocked = true;
        EnsureTorpedoAimLine();
        missionStatus = torpedoAimIsHarpoonTargetSelection
            ? "Harpoon armed. Click a wreck, leviathan, or enemy to designate the catch target."
            : "Side launcher armed. Aim inside a side sector and click LMB to fire.";
    }

    private void UpdateTorpedoAimMode()
    {
        if (!torpedoAimActive)
        {
            return;
        }

        CoreTacticalFleetController.TacticalPointerInputBlocked = true;
        if (torpedoAimShip == null
            || !IsAlive(torpedoAimShip)
            || !TryGetManualTorpedoSlotLauncher(torpedoAimShip, out _, out _))
        {
            CancelTorpedoAim();
            return;
        }

        if (TryReadMousePosition(out Vector2 mousePosition)
            && TryProjectMouseToCommandPlane(mousePosition, out Vector3 aimPoint))
        {
            Vector3 direction = aimPoint - torpedoAimShip.transform.position;
            torpedoAimDirection = FlattenDirection(direction, torpedoAimShip.transform.forward);
            torpedoAimPoint = aimPoint;
        }

        if (torpedoAimIsHarpoonTargetSelection)
        {
            UpdateHarpoonTargetHover();
            DrawHarpoonTargetSelectionLine();
            if (WasTorpedoAimCancelPressed())
            {
                CancelTorpedoAim();
                missionStatus = "Harpoon target selection cancelled.";
                return;
            }

            if (WasLeftMousePressed())
            {
                suppressInspectionClickThisFrame = true;
                bool hasHoveredTarget = harpoonHoverTargetTransform != null;
                if ((hasHoveredTarget
                        || (TryReadMousePosition(out Vector2 clickPosition)
                            && TryPickHarpoonTargetAtScreenPoint(clickPosition, out Transform targetTransform, out Rigidbody targetBody, out CoreTacticalPrototypeHealth targetHealth, out CoreTacticalAutomatonWreck targetWreck, out string targetName)
                            && StoreHarpoonHoverTarget(targetTransform, targetBody, targetHealth, targetWreck, targetName)))
                    && TryDesignateHarpoonTarget(torpedoAimShip, harpoonHoverTargetTransform, harpoonHoverTargetBody, harpoonHoverTargetHealth, harpoonHoverTargetWreck))
                {
                    missionStatus = "Harpoon target designated: " + harpoonHoverTargetName + ".";
                }
                else
                {
                    missionStatus = "No catchable harpoon target under cursor.";
                }

                CancelTorpedoAim();
            }

            return;
        }

        TryResolveTorpedoAimLauncher(torpedoAimShip, torpedoAimDirection, out torpedoAimLauncher, out torpedoAimHarpoonLauncher);
        DrawTorpedoAimSector();

        if (WasTorpedoAimCancelPressed())
        {
            CancelTorpedoAim();
            missionStatus = "Torpedo launch cancelled.";
            return;
        }

        if (WasLeftMousePressed())
        {
            if (torpedoAimLauncher != null && torpedoAimLauncher.TryLaunchManualFan(torpedoAimDirection))
            {
                missionStatus = "Torpedoes away.";
            }
            else
            {
                missionStatus = "Aim inside a live side launcher sector.";
            }

            CancelTorpedoAim();
        }
    }

    private void CancelTorpedoAim()
    {
        torpedoAimActive = false;
        torpedoAimShip = null;
        torpedoAimLauncher = null;
        torpedoAimHarpoonLauncher = null;
        torpedoAimIsHarpoonTargetSelection = false;
        ClearHarpoonHoverTarget();
        HideTorpedoAimLine();
        CoreTacticalFleetController.TacticalPointerInputBlocked = false;
    }

    private bool TryGetTorpedoLauncher(CoreTacticalShipMotor ship, out CoreTacticalMissileLauncher launcher)
    {
        launcher = null;
        if (ship == null)
        {
            return false;
        }

        CoreTacticalMissileLauncher[] launchers = ship.GetComponents<CoreTacticalMissileLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalMissileLauncher candidate = launchers[i];
            if (candidate != null && candidate.weaponGroup == CoreTacticalWeaponGroup.Torpedoes)
            {
                launcher = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetHarpoonLauncher(CoreTacticalShipMotor ship, out CoreTacticalHarpoonLauncher launcher)
    {
        launcher = null;
        if (ship == null)
        {
            return false;
        }

        CoreTacticalHarpoonLauncher[] launchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher candidate = launchers[i];
            if (candidate != null && candidate.weaponGroup == CoreTacticalWeaponGroup.Torpedoes)
            {
                launcher = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetManualTorpedoSlotLauncher(
        CoreTacticalShipMotor ship,
        out CoreTacticalMissileLauncher missileLauncher,
        out CoreTacticalHarpoonLauncher harpoonLauncher)
    {
        bool hasMissile = TryGetTorpedoLauncher(ship, out missileLauncher);
        bool hasHarpoon = TryGetHarpoonLauncher(ship, out harpoonLauncher);
        return hasMissile || hasHarpoon;
    }

    private bool TryGetActiveHarpoonLauncher(CoreTacticalShipMotor ship, out CoreTacticalHarpoonLauncher launcher)
    {
        launcher = null;
        if (ship == null)
        {
            return false;
        }

        CoreTacticalHarpoonLauncher[] launchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher candidate = launchers[i];
            if (candidate != null && candidate.weaponGroup == CoreTacticalWeaponGroup.Torpedoes && candidate.HasActiveLink)
            {
                launcher = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetDesignatedHarpoonLauncher(CoreTacticalShipMotor ship, out CoreTacticalHarpoonLauncher launcher)
    {
        launcher = null;
        if (ship == null)
        {
            return false;
        }

        CoreTacticalHarpoonLauncher[] launchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher candidate = launchers[i];
            if (candidate != null
                && candidate.weaponGroup == CoreTacticalWeaponGroup.Torpedoes
                && candidate.HasDesignatedTarget)
            {
                launcher = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryDesignateHarpoonTarget(
        CoreTacticalShipMotor ship,
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck)
    {
        if (ship == null || targetTransform == null)
        {
            return false;
        }

        CoreTacticalHarpoonLauncher[] launchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        CoreTacticalHarpoonLauncher best = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher candidate = launchers[i];
            if (candidate == null
                || candidate.weaponGroup != CoreTacticalWeaponGroup.Torpedoes
                || candidate.HasActiveLink
                || candidate.HasFlyingProjectile
                || !candidate.CanLaunchAtTarget(targetTransform, targetBody))
            {
                continue;
            }

            Vector3 launchPosition = candidate.GetManualLaunchPosition();
            Vector3 toTarget = targetTransform.position - launchPosition;
            float score = candidate.GetManualAimDeltaDegrees(toTarget) * 10f + Vector3.Distance(launchPosition, targetTransform.position) * 0.001f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            return false;
        }

        for (int i = 0; i < launchers.Length; i++)
        {
            if (launchers[i] != null && launchers[i] != best)
            {
                launchers[i].ClearDesignatedTarget();
            }
        }

        return best.DesignateTarget(targetTransform, targetBody, targetHealth, targetWreck);
    }

    private bool TryResolveTorpedoAimLauncher(
        CoreTacticalShipMotor ship,
        Vector3 aimDirection,
        out CoreTacticalMissileLauncher launcher,
        out CoreTacticalHarpoonLauncher harpoonLauncher)
    {
        launcher = null;
        harpoonLauncher = null;
        if (ship == null)
        {
            return false;
        }

        CoreTacticalMissileLauncher[] launchers = ship.GetComponents<CoreTacticalMissileLauncher>();
        float bestDelta = float.PositiveInfinity;
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalMissileLauncher candidate = launchers[i];
            if (candidate == null
                || candidate.weaponGroup != CoreTacticalWeaponGroup.Torpedoes
                || !candidate.IsDirectionInsideManualSector(aimDirection))
            {
                continue;
            }

            float delta = candidate.GetManualAimDeltaDegrees(aimDirection);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                launcher = candidate;
            }
        }

        CoreTacticalHarpoonLauncher[] harpoonLaunchers = ship.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < harpoonLaunchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher candidate = harpoonLaunchers[i];
            if (candidate == null
                || candidate.weaponGroup != CoreTacticalWeaponGroup.Torpedoes
                || !candidate.IsDirectionInsideManualSector(aimDirection))
            {
                continue;
            }

            float delta = candidate.GetManualAimDeltaDegrees(aimDirection);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                launcher = null;
                harpoonLauncher = candidate;
            }
        }

        return launcher != null || harpoonLauncher != null;
    }

    private void UpdateHarpoonTargetHover()
    {
        if (!TryReadMousePosition(out Vector2 mousePosition)
            || !TryPickHarpoonTargetAtScreenPoint(
                mousePosition,
                out Transform targetTransform,
                out Rigidbody targetBody,
                out CoreTacticalPrototypeHealth targetHealth,
                out CoreTacticalAutomatonWreck targetWreck,
                out string targetName)
            || !IsHarpoonTargetReachableForSelection(targetTransform))
        {
            ClearHarpoonHoverTarget();
            return;
        }

        StoreHarpoonHoverTarget(targetTransform, targetBody, targetHealth, targetWreck, targetName);
        UpdateHarpoonTargetRing();
    }

    private bool StoreHarpoonHoverTarget(
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        string targetName)
    {
        if (targetTransform == null)
        {
            return false;
        }

        harpoonHoverTargetTransform = targetTransform;
        harpoonHoverTargetBody = targetBody;
        harpoonHoverTargetHealth = targetHealth;
        harpoonHoverTargetWreck = targetWreck;
        harpoonHoverTargetName = string.IsNullOrWhiteSpace(targetName) ? targetTransform.name : targetName;
        UpdateHarpoonTargetRing();
        return true;
    }

    private void ClearHarpoonHoverTarget()
    {
        harpoonHoverTargetTransform = null;
        harpoonHoverTargetBody = null;
        harpoonHoverTargetHealth = null;
        harpoonHoverTargetWreck = null;
        harpoonHoverTargetName = "";
        HideHarpoonTargetRing();
    }

    private bool IsHarpoonTargetReachableForSelection(Transform targetTransform)
    {
        if (torpedoAimShip == null || targetTransform == null)
        {
            return false;
        }

        if (targetTransform == torpedoAimShip.transform || targetTransform.IsChildOf(torpedoAimShip.transform))
        {
            return false;
        }

        CoreTacticalHarpoonLauncher[] launchers = torpedoAimShip.GetComponents<CoreTacticalHarpoonLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher launcher = launchers[i];
            if (launcher == null || launcher.weaponGroup != CoreTacticalWeaponGroup.Torpedoes)
            {
                continue;
            }

            Vector3 launchPosition = launcher.GetManualLaunchPosition();
            Vector3 toTarget = targetTransform.position - launchPosition;
            toTarget.y = 0f;
            if (toTarget.magnitude <= launcher.ManualRangeMeters
                && launcher.IsDirectionInsideManualSector(toTarget)
                && launcher.CanLaunchAtTarget(targetTransform, null))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawHarpoonTargetSelectionLine()
    {
        EnsureTorpedoAllowedSectorLineCount(0);
        EnsureTorpedoAimLine();
        if (torpedoAimLine == null || torpedoAimShip == null)
        {
            return;
        }

        CoreTacticalHarpoonLauncher selectionLauncher = null;
        TryGetHarpoonLauncher(torpedoAimShip, out selectionLauncher);
        Vector3 origin = selectionLauncher != null ? selectionLauncher.GetManualLaunchPosition() : torpedoAimShip.transform.position;
        origin.y = fleet != null ? fleet.CommandPlaneAltitudeMeters : origin.y;
        Vector3 end = harpoonHoverTargetTransform != null ? harpoonHoverTargetTransform.position : torpedoAimPoint;
        end.y = origin.y;
        if (selectionLauncher != null)
        {
            Vector3 delta = end - origin;
            float range = selectionLauncher.ManualRangeMeters;
            if (delta.magnitude > range)
            {
                end = origin + delta.normalized * range;
            }
        }

        torpedoAimLine.enabled = true;
        torpedoAimLine.positionCount = 2;
        torpedoAimLine.SetPosition(0, origin);
        torpedoAimLine.SetPosition(1, end);
    }

    private void UpdateHarpoonTargetRing()
    {
        if (harpoonHoverTargetTransform == null)
        {
            HideHarpoonTargetRing();
            return;
        }

        EnsureHarpoonTargetRing();
        if (harpoonTargetRingLine == null)
        {
            return;
        }

        float radius = ResolveHarpoonTargetRingRadius(harpoonHoverTargetTransform, harpoonHoverTargetWreck);
        Vector3 center = harpoonHoverTargetTransform.position;
        center.y = fleet != null ? fleet.CommandPlaneAltitudeMeters + 1.2f : center.y + 1.2f;
        float phase = Time.time * 1.8f;
        for (int i = 0; i <= HarpoonTargetRingSegments; i++)
        {
            float angle = phase + i * Mathf.PI * 2f / HarpoonTargetRingSegments;
            harpoonTargetRingPoints[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        harpoonTargetRingLine.enabled = true;
        harpoonTargetRingLine.positionCount = HarpoonTargetRingSegments + 1;
        for (int i = 0; i < harpoonTargetRingLine.positionCount; i++)
        {
            harpoonTargetRingLine.SetPosition(i, harpoonTargetRingPoints[i]);
        }
    }

    private void EnsureHarpoonTargetRing()
    {
        if (harpoonTargetRingLine != null)
        {
            return;
        }

        if (harpoonTargetRingMaterial == null)
        {
            harpoonTargetRingMaterial = CreateTransparentMaterial(new Color(0.18f, 0.88f, 1f, 0.88f));
        }

        GameObject ringObject = new GameObject("Harpoon Target Lock Ring");
        ringObject.transform.SetParent(tacticalRoot != null ? tacticalRoot.transform : transform, false);
        harpoonTargetRingLine = ringObject.AddComponent<LineRenderer>();
        harpoonTargetRingLine.sharedMaterial = harpoonTargetRingMaterial;
        harpoonTargetRingLine.useWorldSpace = true;
        harpoonTargetRingLine.loop = true;
        harpoonTargetRingLine.startWidth = 3.5f;
        harpoonTargetRingLine.endWidth = 3.5f;
        harpoonTargetRingLine.numCapVertices = 2;
        harpoonTargetRingLine.numCornerVertices = 2;
        harpoonTargetRingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        harpoonTargetRingLine.receiveShadows = false;
    }

    private void HideHarpoonTargetRing()
    {
        if (harpoonTargetRingLine != null)
        {
            harpoonTargetRingLine.enabled = false;
        }
    }

    private static float ResolveHarpoonTargetRingRadius(Transform targetTransform, CoreTacticalAutomatonWreck targetWreck)
    {
        if (targetWreck != null)
        {
            return Mathf.Clamp(targetWreck.ApproximateLengthMeters * 0.42f, 16f, 95f);
        }

        CoreTacticalLeviathanController leviathan = targetTransform != null ? targetTransform.GetComponent<CoreTacticalLeviathanController>() : null;
        if (leviathan != null)
        {
            return Mathf.Clamp(leviathan.lengthMeters * 0.42f, 18f, 140f);
        }

        CoreTacticalShipMotor ship = targetTransform != null ? targetTransform.GetComponent<CoreTacticalShipMotor>() : null;
        if (ship != null)
        {
            return Mathf.Clamp(Mathf.Max(ship.hullSizeMeters.x, ship.hullSizeMeters.z) * 0.42f, 18f, 120f);
        }

        return 24f;
    }

    private void EnsureTorpedoAimLine()
    {
        if (torpedoAimLine != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("Manual Torpedo Aim Sector");
        lineObject.transform.SetParent(tacticalRoot != null ? tacticalRoot.transform : transform, false);
        torpedoAimLine = lineObject.AddComponent<LineRenderer>();
        if (torpedoAimMaterial == null)
        {
            torpedoAimMaterial = CreateTransparentMaterial(new Color(0.16f, 0.82f, 1f, 0.68f));
        }

        torpedoAimLine.sharedMaterial = torpedoAimMaterial;
        torpedoAimLine.useWorldSpace = true;
        torpedoAimLine.loop = false;
        torpedoAimLine.startWidth = 7f;
        torpedoAimLine.endWidth = 3f;
        torpedoAimLine.numCapVertices = 2;
        torpedoAimLine.numCornerVertices = 2;
        torpedoAimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        torpedoAimLine.receiveShadows = false;
    }

    private void EnsureTorpedoAllowedSectorLineCount(int count)
    {
        int safeCount = Mathf.Clamp(count, 0, 4);
        if (torpedoAllowedSectorMaterial == null)
        {
            torpedoAllowedSectorMaterial = CreateTransparentMaterial(new Color(0.12f, 0.62f, 1f, 0.30f));
        }

        while (torpedoAllowedSectorLines.Count < safeCount)
        {
            GameObject lineObject = new GameObject("Manual Torpedo Allowed Sector");
            lineObject.transform.SetParent(tacticalRoot != null ? tacticalRoot.transform : transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = torpedoAllowedSectorMaterial;
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = 4f;
            line.endWidth = 2f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            torpedoAllowedSectorLines.Add(line);
        }

        for (int i = 0; i < torpedoAllowedSectorLines.Count; i++)
        {
            if (torpedoAllowedSectorLines[i] != null)
            {
                torpedoAllowedSectorLines[i].enabled = i < safeCount;
            }
        }
    }

    private void DrawTorpedoAimSector()
    {
        if (torpedoAimShip == null)
        {
            HideTorpedoAimLine();
            return;
        }

        CoreTacticalMissileLauncher[] launchers = torpedoAimShip.GetComponents<CoreTacticalMissileLauncher>();
        CoreTacticalHarpoonLauncher[] harpoonLaunchers = torpedoAimShip.GetComponents<CoreTacticalHarpoonLauncher>();
        int sectorCount = 0;
        for (int i = 0; i < launchers.Length; i++)
        {
            if (launchers[i] != null && launchers[i].weaponGroup == CoreTacticalWeaponGroup.Torpedoes)
            {
                sectorCount++;
            }
        }

        for (int i = 0; i < harpoonLaunchers.Length; i++)
        {
            if (harpoonLaunchers[i] != null && harpoonLaunchers[i].weaponGroup == CoreTacticalWeaponGroup.Torpedoes)
            {
                sectorCount++;
            }
        }

        EnsureTorpedoAllowedSectorLineCount(sectorCount);
        int sectorIndex = 0;
        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalMissileLauncher launcher = launchers[i];
            if (launcher == null || launcher.weaponGroup != CoreTacticalWeaponGroup.Torpedoes)
            {
                continue;
            }

            if (sectorIndex < torpedoAllowedSectorLines.Count)
            {
                DrawTorpedoSectorLine(
                    torpedoAllowedSectorLines[sectorIndex],
                    launcher.GetManualLaunchPosition(),
                    launcher.GetManualAimCenterDirection(),
                    launcher.ManualAimSectorDegrees,
                    launcher.ManualRangeMeters);
            }

            sectorIndex++;
        }

        for (int i = 0; i < harpoonLaunchers.Length; i++)
        {
            CoreTacticalHarpoonLauncher launcher = harpoonLaunchers[i];
            if (launcher == null || launcher.weaponGroup != CoreTacticalWeaponGroup.Torpedoes)
            {
                continue;
            }

            if (sectorIndex < torpedoAllowedSectorLines.Count)
            {
                DrawTorpedoSectorLine(
                    torpedoAllowedSectorLines[sectorIndex],
                    launcher.GetManualLaunchPosition(),
                    launcher.GetManualAimCenterDirection(),
                    launcher.ManualAimSectorDegrees,
                    launcher.ManualRangeMeters);
            }

            sectorIndex++;
        }

        EnsureTorpedoAimLine();
        if (torpedoAimLine == null)
        {
            return;
        }

        if (torpedoAimLauncher == null && torpedoAimHarpoonLauncher == null)
        {
            torpedoAimLine.enabled = false;
            return;
        }

        torpedoAimLine.enabled = true;
        if (torpedoAimHarpoonLauncher != null)
        {
            Vector3 harpoonLaunchPosition = torpedoAimHarpoonLauncher.GetManualLaunchPosition();
            Vector3 harpoonAimVector = torpedoAimPoint - harpoonLaunchPosition;
            DrawTorpedoSectorLine(
                torpedoAimLine,
                harpoonLaunchPosition,
                torpedoAimDirection,
                torpedoAimHarpoonLauncher.GetManualScatterAngleDegrees(harpoonAimVector),
                Vector3.Distance(harpoonLaunchPosition, torpedoAimPoint));
        }
        else
        {
            DrawTorpedoSectorLine(
                torpedoAimLine,
                torpedoAimLauncher.GetManualLaunchPosition(),
                torpedoAimDirection,
                torpedoAimLauncher.ManualFanAngleDegrees,
                torpedoAimLauncher.ManualRangeMeters);
        }
    }

    private void DrawTorpedoSectorLine(LineRenderer line, Vector3 origin, Vector3 centerDirection, float angleDegrees, float rangeMeters)
    {
        if (line == null)
        {
            return;
        }

        const int arcSegments = 30;
        origin.y = fleet != null ? fleet.CommandPlaneAltitudeMeters : origin.y;
        float range = Mathf.Max(1f, rangeMeters);
        float fanAngle = Mathf.Max(0f, angleDegrees);
        Vector3 safeCenterDirection = FlattenDirection(centerDirection, torpedoAimShip != null ? torpedoAimShip.transform.forward : Vector3.forward);
        torpedoAimSectorPoints[0] = origin;
        torpedoAimSectorPoints[1] = origin + Quaternion.AngleAxis(-fanAngle * 0.5f, Vector3.up) * safeCenterDirection * range;
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, t);
            torpedoAimSectorPoints[i + 2] = origin + Quaternion.AngleAxis(angle, Vector3.up) * safeCenterDirection * range;
        }

        torpedoAimSectorPoints[arcSegments + 3] = origin;
        line.positionCount = arcSegments + 4;
        for (int i = 0; i < line.positionCount; i++)
        {
            line.SetPosition(i, torpedoAimSectorPoints[i]);
        }
    }

    private void HideTorpedoAimLine()
    {
        if (torpedoAimLine != null)
        {
            Destroy(torpedoAimLine.gameObject);
            torpedoAimLine = null;
        }

        for (int i = 0; i < torpedoAllowedSectorLines.Count; i++)
        {
            if (torpedoAllowedSectorLines[i] != null)
            {
                Destroy(torpedoAllowedSectorLines[i].gameObject);
            }
        }

        torpedoAllowedSectorLines.Clear();
    }

    private bool TryProjectMouseToCommandPlane(Vector2 mousePosition, out Vector3 point)
    {
        Camera camera = Camera.main;
        float altitude = fleet != null ? fleet.CommandPlaneAltitudeMeters : CommandPlaneAltitudeMeters;
        return CoreTacticalFleetController.TryProjectCameraScreenPointToCommandPlane(
            camera,
            mousePosition,
            altitude,
            out point);
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        fallback.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static bool TryReadMousePosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#endif
        position = default;
        return false;
    }

    private static bool WasLeftMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private static bool WasTorpedoAimCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        return (mouse != null && mouse.rightButton.wasPressedThisFrame)
            || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
#else
        return false;
#endif
    }

    private void HandleMiningModuleHotkeys()
    {
        if (playerMiningRig == null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.mKey.wasPressedThisFrame)
        {
            playerMiningRig.ToggleModule(CoreTacticalMiningModule.Magnet);
        }

        if (keyboard.lKey.wasPressedThisFrame)
        {
            playerMiningRig.ToggleModule(CoreTacticalMiningModule.Drill);
        }

        if (keyboard.cKey.wasPressedThisFrame)
        {
            playerMiningRig.ToggleModule(CoreTacticalMiningModule.Crusher);
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            playerMiningRig.ToggleModule(CoreTacticalMiningModule.Siphon);
        }

        if (keyboard.kKey.wasPressedThisFrame)
        {
            playerMiningRig.ToggleModule(CoreTacticalMiningModule.CloudConcentrator);
        }

        if (keyboard.iKey.wasPressedThisFrame)
        {
            inventoryWindowOpen = !inventoryWindowOpen;
        }
#endif
    }

    private void HandleTacticalActionHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.fKey.wasPressedThisFrame)
        {
            ToggleClaudianSlip();
        }

        if (keyboard.yKey.wasPressedThisFrame)
        {
            BeginAutoExit();
        }
#endif
    }

    private bool TryPickHarpoonTargetAtScreenPoint(
        Vector2 screenPosition,
        out Transform targetTransform,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck targetWreck,
        out string targetName)
    {
        targetTransform = null;
        targetBody = null;
        targetHealth = null;
        targetWreck = null;
        targetName = "";

        Camera camera = Camera.main != null ? Camera.main : fleet != null ? fleet.InputCameraForTests : null;
        if (camera == null)
        {
            return false;
        }

        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 60000f, ~0, QueryTriggerInteraction.Ignore)
            && TryResolveHarpoonTargetCollider(hit.collider, out targetTransform, out targetBody, out targetHealth, out targetWreck, out targetName))
        {
            return true;
        }

        return TryPickHarpoonTargetFromScreenRect(
            BuildHarpoonTargetPickRect(screenPosition),
            out targetTransform,
            out targetBody,
            out targetHealth,
            out targetWreck,
            out targetName);
    }

    private bool TryPickHarpoonTargetFromScreenRect(
        Rect screenRect,
        out Transform targetTransform,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck targetWreck,
        out string targetName)
    {
        targetTransform = null;
        targetBody = null;
        targetHealth = null;
        targetWreck = null;
        targetName = "";

        Camera camera = Camera.main != null ? Camera.main : fleet != null ? fleet.InputCameraForTests : null;
        if (camera == null || screenRect.width <= 0.001f || screenRect.height <= 0.001f)
        {
            return false;
        }

        for (int i = 0; i < enemyShips.Count; i++)
        {
            CoreTacticalShipMotor enemy = enemyShips[i];
            if (!TryBuildHarpoonShipTarget(enemy, out targetTransform, out targetBody, out targetHealth, out targetName)
                || !TryGetWorldObjectScreenRect(camera, enemy.gameObject, InspectionPickMinimumHalfSizePixels, out Rect enemyRect)
                || !screenRect.Overlaps(enemyRect, true))
            {
                continue;
            }

            return true;
        }

        if (fleet != null)
        {
            IReadOnlyList<CoreTacticalShipMotor> ships = fleet.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                CoreTacticalShipMotor ship = ships[i];
                if (!TryBuildHarpoonShipTarget(ship, out targetTransform, out targetBody, out targetHealth, out targetName)
                    || !TryGetWorldObjectScreenRect(camera, ship.gameObject, InspectionPickMinimumHalfSizePixels, out Rect shipRect)
                    || !screenRect.Overlaps(shipRect, true))
                {
                    continue;
                }

                return true;
            }
        }

        CoreTacticalAutomatonWreck[] wrecks = FindObjectsByType<CoreTacticalAutomatonWreck>(FindObjectsSortMode.None);
        for (int i = 0; i < wrecks.Length; i++)
        {
            CoreTacticalAutomatonWreck wreck = wrecks[i];
            if (wreck == null
                || !CoreTacticalHarpoonLauncher.IsCatchTargetValid(wreck.transform, null, wreck)
                || !TryGetWorldObjectScreenRect(camera, wreck.gameObject, InspectionPickMinimumHalfSizePixels, out Rect wreckRect)
                || !screenRect.Overlaps(wreckRect, true))
            {
                continue;
            }

            targetTransform = wreck.transform;
            targetBody = wreck.GetComponent<Rigidbody>();
            targetHealth = null;
            targetWreck = wreck;
            targetName = string.IsNullOrWhiteSpace(wreck.displayName) ? wreck.name : wreck.displayName;
            return true;
        }

        return false;
    }

    private bool TryResolveHarpoonTargetCollider(
        Collider collider,
        out Transform targetTransform,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck targetWreck,
        out string targetName)
    {
        targetTransform = null;
        targetBody = null;
        targetHealth = null;
        targetWreck = null;
        targetName = "";
        if (collider == null)
        {
            return false;
        }

        CoreTacticalAutomatonWreck wreck = collider.GetComponentInParent<CoreTacticalAutomatonWreck>();
        if (wreck != null && CoreTacticalHarpoonLauncher.IsCatchTargetValid(wreck.transform, null, wreck))
        {
            targetTransform = wreck.transform;
            targetBody = wreck.GetComponent<Rigidbody>();
            targetWreck = wreck;
            targetName = string.IsNullOrWhiteSpace(wreck.displayName) ? wreck.name : wreck.displayName;
            return true;
        }

        CoreTacticalShipMotor ship = collider.GetComponentInParent<CoreTacticalShipMotor>();
        return TryBuildHarpoonShipTarget(ship, out targetTransform, out targetBody, out targetHealth, out targetName);
    }

    private static bool TryBuildHarpoonShipTarget(
        CoreTacticalShipMotor ship,
        out Transform targetTransform,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out string targetName)
    {
        targetTransform = null;
        targetBody = null;
        targetHealth = null;
        targetName = "";
        if (ship == null)
        {
            return false;
        }

        targetHealth = ship.GetComponent<CoreTacticalPrototypeHealth>();
        if (!CoreTacticalHarpoonLauncher.IsCatchTargetValid(ship.transform, targetHealth, null))
        {
            return false;
        }

        targetTransform = ship.transform;
        targetBody = ship.Body != null ? ship.Body : ship.GetComponent<Rigidbody>();
        targetName = string.IsNullOrWhiteSpace(ship.displayName) ? ship.name : ship.displayName;
        return true;
    }

    private void HandleOreBoulderInspectionInput()
    {
        if (suppressInspectionClickThisFrame)
        {
            suppressInspectionClickThisFrame = false;
            return;
        }

        if (!WasLeftMousePressed() || !TryReadMousePosition(out Vector2 mousePosition))
        {
            return;
        }

        if (IsPointerOverMissionGui(mousePosition))
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Ray ray = camera.ScreenPointToRay(mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 60000f, ~0, QueryTriggerInteraction.Ignore))
        {
            TryInspectScreenRect(BuildInspectionPickRect(mousePosition));
            return;
        }

        if (!TryInspectHit(hit))
        {
            TryInspectScreenRect(BuildInspectionPickRect(mousePosition));
        }
    }

    private bool TryInspectHit(RaycastHit hit)
    {
        CoreTacticalGasCloud cloud = hit.collider != null ? hit.collider.GetComponentInParent<CoreTacticalGasCloud>() : null;
        if (cloud != null)
        {
            InspectGasCloud(cloud);
            return true;
        }

        CoreTacticalOreBoulder boulder = hit.collider != null ? hit.collider.GetComponentInParent<CoreTacticalOreBoulder>() : null;
        if (boulder != null)
        {
            InspectOreBoulder(boulder);
            return true;
        }

        CoreTacticalLeviathanController leviathan = hit.collider != null ? hit.collider.GetComponentInParent<CoreTacticalLeviathanController>() : null;
        CoreTacticalShipMotor leviathanShip = leviathan != null ? leviathan.GetComponent<CoreTacticalShipMotor>() : null;
        if (leviathan != null && leviathanShip != null && IsAlive(leviathanShip))
        {
            InspectLeviathan(leviathan);
            return true;
        }

        CoreTacticalShipMotor ship = hit.collider != null ? hit.collider.GetComponentInParent<CoreTacticalShipMotor>() : null;
        if (IsInspectableCombatShip(ship))
        {
            InspectShip(ship);
            return true;
        }

        return false;
    }

    private bool TryInspectScreenRect(Rect screenRect)
    {
        Camera camera = Camera.main != null ? Camera.main : fleet != null ? fleet.InputCameraForTests : null;
        if (camera == null || screenRect.width <= 0.001f || screenRect.height <= 0.001f)
        {
            return false;
        }

        for (int i = 0; i < enemyShips.Count; i++)
        {
            CoreTacticalShipMotor enemy = enemyShips[i];
            if (enemy == null || !IsAlive(enemy))
            {
                continue;
            }

            if (!TryGetWorldObjectScreenRect(camera, enemy.gameObject, InspectionPickMinimumHalfSizePixels, out Rect enemyRect)
                || !screenRect.Overlaps(enemyRect, true))
            {
                continue;
            }

            InspectShip(enemy);
            return true;
        }

        if (fleet != null)
        {
            IReadOnlyList<CoreTacticalShipMotor> ships = fleet.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                CoreTacticalShipMotor ship = ships[i];
                if (!IsInspectableCombatShip(ship))
                {
                    continue;
                }

                if (!TryGetWorldObjectScreenRect(camera, ship.gameObject, InspectionPickMinimumHalfSizePixels, out Rect shipRect)
                    || !screenRect.Overlaps(shipRect, true))
                {
                    continue;
                }

                InspectShip(ship);
                return true;
            }
        }

        for (int i = 0; i < oreBoulders.Count; i++)
        {
            CoreTacticalOreBoulder boulder = oreBoulders[i];
            if (boulder == null || boulder.CurrentHealth01 <= 0f)
            {
                continue;
            }

            if (!TryGetWorldObjectScreenRect(camera, boulder.gameObject, InspectionPickMinimumHalfSizePixels, out Rect boulderRect)
                || !screenRect.Overlaps(boulderRect, true))
            {
                continue;
            }

            InspectOreBoulder(boulder);
            return true;
        }

        for (int i = 0; i < gasClouds.Count; i++)
        {
            CoreTacticalGasCloud cloud = gasClouds[i];
            if (cloud == null)
            {
                continue;
            }

            if (!TryGetWorldObjectScreenRect(camera, cloud.gameObject, InspectionPickMinimumHalfSizePixels, out Rect cloudRect)
                || !screenRect.Overlaps(cloudRect, true))
            {
                continue;
            }

            InspectGasCloud(cloud);
            return true;
        }

        return false;
    }

    private void InspectOreBoulder(CoreTacticalOreBoulder boulder)
    {
        inspectedOreBoulder = boulder;
        inspectedLeviathan = null;
        inspectedShip = null;
        inspectedGasCloud = null;
    }

    private void InspectLeviathan(CoreTacticalLeviathanController leviathan)
    {
        inspectedLeviathan = leviathan;
        inspectedOreBoulder = null;
        inspectedShip = null;
        inspectedGasCloud = null;
    }

    private void InspectShip(CoreTacticalShipMotor ship)
    {
        CoreTacticalLeviathanController leviathan = ship != null ? ship.GetComponent<CoreTacticalLeviathanController>() : null;
        if (leviathan != null)
        {
            InspectLeviathan(leviathan);
            return;
        }

        inspectedShip = ship;
        inspectedOreBoulder = null;
        inspectedLeviathan = null;
        inspectedGasCloud = null;
    }

    private void InspectGasCloud(CoreTacticalGasCloud cloud)
    {
        inspectedGasCloud = cloud;
        inspectedOreBoulder = null;
        inspectedLeviathan = null;
        inspectedShip = null;
    }

    private static bool IsInspectableCombatShip(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (ship.GetComponent<CoreTacticalOreBoulder>() != null)
        {
            return true;
        }

        CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
        return combatant != null && combatant.team == CoreTacticalCombatTeam.Enemy && combatant.IsAlive;
    }

    private static Rect BuildInspectionPickRect(Vector2 screenPosition)
    {
        float halfSize = Mathf.Max(1f, InspectionClickPickHalfSizePixels);
        return Rect.MinMaxRect(
            screenPosition.x - halfSize,
            screenPosition.y - halfSize,
            screenPosition.x + halfSize,
            screenPosition.y + halfSize);
    }

    private static Rect BuildHarpoonTargetPickRect(Vector2 screenPosition)
    {
        float halfSize = Mathf.Max(InspectionClickPickHalfSizePixels, HarpoonTargetPickRadiusPixels);
        return Rect.MinMaxRect(
            screenPosition.x - halfSize,
            screenPosition.y - halfSize,
            screenPosition.x + halfSize,
            screenPosition.y + halfSize);
    }

    private static bool TryGetWorldObjectScreenRect(Camera camera, GameObject target, float minimumHalfSizePixels, out Rect rect)
    {
        rect = default;
        if (camera == null || target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Vector3 screenPoint = camera.WorldToScreenPoint(target.transform.position);
            if (screenPoint.z <= 0f)
            {
                return false;
            }

            rect = Rect.MinMaxRect(screenPoint.x, screenPoint.y, screenPoint.x, screenPoint.y);
            rect = ExpandScreenRectToMinimum(rect, minimumHalfSizePixels);
            return true;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        bool hasVisiblePoint = false;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screenPoint = camera.WorldToScreenPoint(corners[i]);
            if (screenPoint.z <= 0f)
            {
                continue;
            }

            hasVisiblePoint = true;
            minX = Mathf.Min(minX, screenPoint.x);
            minY = Mathf.Min(minY, screenPoint.y);
            maxX = Mathf.Max(maxX, screenPoint.x);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }

        if (!hasVisiblePoint)
        {
            return false;
        }

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        rect = ExpandScreenRectToMinimum(rect, minimumHalfSizePixels);
        return true;
    }

    private static Rect ExpandScreenRectToMinimum(Rect rect, float minimumHalfSizePixels)
    {
        float halfSize = Mathf.Max(0f, minimumHalfSizePixels);
        if (halfSize <= 0f)
        {
            return rect;
        }

        Vector2 center = rect.center;
        float halfWidth = Mathf.Max(rect.width * 0.5f, halfSize);
        float halfHeight = Mathf.Max(rect.height * 0.5f, halfSize);
        return Rect.MinMaxRect(
            center.x - halfWidth,
            center.y - halfHeight,
            center.x + halfWidth,
            center.y + halfHeight);
    }

    private void DrawMiniMap()
    {
        Rect rect = new Rect(Screen.width - 230f, Screen.height - 230f, 216f, 216f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.62f);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 16f), "Map", hudHeaderStyle);

        Rect mapRect = new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, rect.height - 40f);
        DrawHudRect(mapRect, new Color(0.00f, 0.08f, 0.11f, 0.82f));
        DrawOutlinedRect(mapRect, new Color(0.22f, 0.86f, 1f, 0.35f));
        DrawMiniMapGrid(mapRect);

        if (extractionUnlocked)
        {
            Vector2 exitPoint = WorldToMiniMap(exitPosition, mapRect);
            DrawMiniMapMarker(exitPoint, new Color(1f, 0.82f, 0.22f, 1f), 7f);
        }

        if (playerShip != null)
        {
            DrawMiniMapMarker(WorldToMiniMap(playerShip.transform.position, mapRect), new Color(0.24f, 1f, 0.54f, 1f), 6f);
        }

        for (int i = 0; i < enemyShips.Count; i++)
        {
            CoreTacticalShipMotor enemy = enemyShips[i];
            if (enemy == null || !IsAlive(enemy))
            {
                continue;
            }

            DrawMiniMapMarker(WorldToMiniMap(enemy.transform.position, mapRect), new Color(1f, 0.18f, 0.12f, 1f), 5f);
        }

        for (int i = 0; i < oreBoulders.Count; i++)
        {
            CoreTacticalOreBoulder boulder = oreBoulders[i];
            if (boulder == null || boulder.CurrentHealth01 <= 0f)
            {
                continue;
            }

            DrawMiniMapMarker(WorldToMiniMap(boulder.transform.position, mapRect), new Color(0.92f, 0.78f, 0.28f, 1f), 6f);
        }
    }

    private void DrawMiniMapGrid(Rect rect)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.20f, 0.78f, 1f, 0.16f);
        for (int i = 1; i < 4; i++)
        {
            float x = rect.x + rect.width * i / 4f;
            GUI.DrawTexture(new Rect(x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
            float y = rect.y + rect.height * i / 4f;
            GUI.DrawTexture(new Rect(rect.x, y, rect.width, 1f), Texture2D.whiteTexture);
        }

        GUI.color = oldColor;
    }

    private Vector2 WorldToMiniMap(Vector3 worldPosition, Rect mapRect)
    {
        float radius = Mathf.Max(100f, missionRadiusMeters + PlayerSpawnDistanceMeters * 0.35f);
        Vector3 delta = worldPosition - missionCenter;
        Vector2 local = new Vector2(delta.x / radius, delta.z / radius);
        local = Vector2.ClampMagnitude(local, 1f);
        return new Vector2(mapRect.center.x + local.x * mapRect.width * 0.46f, mapRect.center.y - local.y * mapRect.height * 0.46f);
    }

    private void DrawMiniMapMarker(Vector2 center, Color color, float size)
    {
        Rect marker = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        DrawHudRect(marker, color);
    }

    private void DrawWorldUnitBars()
    {
        if (Camera.main == null)
        {
            return;
        }

        if (playerShip != null)
        {
            DrawWorldBarForShip(playerShip, true);
        }

        for (int i = 0; i < enemyShips.Count; i++)
        {
            if (enemyShips[i] != null)
            {
                DrawWorldBarForShip(enemyShips[i], false);
            }
        }

        for (int i = 0; i < oreBoulders.Count; i++)
        {
            if (oreBoulders[i] != null)
            {
                DrawWorldBarForBoulder(oreBoulders[i]);
            }
        }
    }

    private void DrawWorldBarForShip(CoreTacticalShipMotor ship, bool friendly)
    {
        if (ship == null || !IsAlive(ship))
        {
            return;
        }

        Camera camera = Camera.main;
        Vector3 world = ship.transform.position + Vector3.up * (Mathf.Max(10f, ship.hullSizeMeters.y + 16f));
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            return;
        }

        float width = friendly ? 58f : 46f;
        float x = screen.x - width * 0.5f;
        float y = Screen.height - screen.y;
        CoreTacticalLeviathanController leviathan = ship.GetComponent<CoreTacticalLeviathanController>();
        Color healthColor = friendly
            ? new Color(0.20f, 0.92f, 0.36f, 0.96f)
            : leviathan != null ? new Color(0.66f, 0.22f, 0.96f, 0.96f) : new Color(1f, 0.14f, 0.10f, 0.96f);
        DrawBar(new Rect(x, y, width, 5f), GetShipHealth01(ship), healthColor, new Color(0f, 0f, 0f, 0.62f));
        if (friendly)
        {
            DrawBar(new Rect(x, y + 7f, width, 4f), GetBattery01(ship), new Color(0.20f, 0.56f, 1f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
        }
        else if (leviathan != null)
        {
            DrawBar(new Rect(x, y + 7f, width, 4f), leviathan.Aggression01, new Color(1f, 0.82f, 0.12f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
        }
    }

    private void DrawWorldBarForBoulder(CoreTacticalOreBoulder boulder)
    {
        if (boulder == null || boulder.CurrentHealth01 <= 0f)
        {
            return;
        }

        Camera camera = Camera.main;
        Vector3 world = boulder.transform.position + Vector3.up * Mathf.Max(16f, boulder.transform.localScale.y * 0.72f);
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            return;
        }

        float width = 58f;
        float x = screen.x - width * 0.5f;
        float y = Screen.height - screen.y;
        DrawBar(new Rect(x, y, width, 5f), boulder.CurrentHealth01, new Color(0.93f, 0.78f, 0.24f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
        DrawBar(new Rect(x, y + 7f, width, 4f), boulder.CurrentMass01, new Color(0.58f, 0.86f, 0.72f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
    }

    private CoreTacticalShipMotor GetSelectedFriendlyShip()
    {
        if (fleet != null)
        {
            IReadOnlyList<CoreTacticalShipMotor> ships = fleet.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                CoreTacticalShipMotor ship = ships[i];
                if (ship == null || !ship.IsSelected)
                {
                    continue;
                }

                CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
                if (combatant == null || combatant.team == CoreTacticalCombatTeam.Friendly)
                {
                    return ship;
                }
            }
        }

        return playerShip != null && playerShip.IsSelected ? playerShip : null;
    }

    private static bool IsAlive(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = ship.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private int GetDestroyedEnemyObjectiveCount()
    {
        return Mathf.Max(0, totalEnemies - CountAliveEnemies());
    }

    private float GetMissionOreProgressKg()
    {
        return playerMiningRig != null ? playerMiningRig.TotalOreCollectedKg : 0f;
    }

    private bool IsEnemyObjectiveComplete()
    {
        return GetDestroyedEnemyObjectiveCount() >= RequiredEnemyKillObjectiveCount;
    }

    private bool IsOreObjectiveComplete()
    {
        return GetMissionOreProgressKg() + 0.001f >= RequiredOreObjectiveKg;
    }

    private bool AreMissionObjectivesComplete()
    {
        return AreCoreMissionObjectivesCompleteForTests(GetDestroyedEnemyObjectiveCount(), GetMissionOreProgressKg());
    }

    private string BuildLockedMissionObjectiveStatus()
    {
        List<string> remaining = new List<string>(2);
        if (!IsEnemyObjectiveComplete())
        {
            remaining.Add("enemy ships " + Mathf.Min(GetDestroyedEnemyObjectiveCount(), RequiredEnemyKillObjectiveCount) + "/" + RequiredEnemyKillObjectiveCount);
        }

        if (!IsOreObjectiveComplete())
        {
            remaining.Add("ore " + Mathf.Min(GetMissionOreProgressKg(), RequiredOreObjectiveKg).ToString("0") + "/" + RequiredOreObjectiveKg.ToString("0") + " kg");
        }

        return remaining.Count == 0
            ? "Bonus objectives complete. Exit is already open."
            : "Optional bonus objectives: " + string.Join(" | ", remaining) + ". Exit is available any time.";
    }

    private static float GetShipHealth01(CoreTacticalShipMotor ship)
    {
        CoreTacticalPrototypeHealth health = ship != null ? ship.GetComponent<CoreTacticalPrototypeHealth>() : null;
        if (health == null || health.maxHealth <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(health.currentHealth / health.maxHealth);
    }

    private static string GetShipHealthLine(CoreTacticalShipMotor ship)
    {
        CoreTacticalPrototypeHealth health = ship != null ? ship.GetComponent<CoreTacticalPrototypeHealth>() : null;
        if (health == null)
        {
            return "-- / --";
        }

        return Mathf.CeilToInt(health.currentHealth) + " / " + Mathf.CeilToInt(health.maxHealth);
    }

    private float GetBattery01(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return 0f;
        }

        if (playerMiningRig != null && ship == playerShip)
        {
            return playerMiningRig.Battery01;
        }

        return Mathf.Clamp01(tacticalBatteryCurrent / TacticalBatteryMax);
    }

    private string GetBatteryLine(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return "-- / --";
        }

        if (playerMiningRig != null && ship == playerShip)
        {
            return playerMiningRig.BuildEnergyLine();
        }

        return tacticalBatteryCurrent.ToString("0") + " / " + TacticalBatteryMax.ToString("0");
    }

    private static string GetActionHotkey(CoreTacticalWeaponGroup group)
    {
        return group switch
        {
            CoreTacticalWeaponGroup.MainBattery => "1",
            CoreTacticalWeaponGroup.Secondary76mm => "2",
            CoreTacticalWeaponGroup.Secondary152mm => "3",
            CoreTacticalWeaponGroup.Torpedoes => "4",
            CoreTacticalWeaponGroup.Missiles => "5",
            CoreTacticalWeaponGroup.MachineGuns => "6",
            CoreTacticalWeaponGroup.Autocannon30mm => "1",
            _ => "-"
        };
    }

    private static string GetActionIcon(CoreTacticalWeaponControl weaponControl, CoreTacticalWeaponGroup group)
    {
        string runtimeIcon = weaponControl != null ? weaponControl.GetRuntimeIcon(group) : "";
        if (!string.IsNullOrWhiteSpace(runtimeIcon))
        {
            return runtimeIcon;
        }

        return group switch
        {
            CoreTacticalWeaponGroup.MainBattery => "406",
            CoreTacticalWeaponGroup.Secondary76mm => "76",
            CoreTacticalWeaponGroup.Secondary152mm => "152",
            CoreTacticalWeaponGroup.Torpedoes => "TRP",
            CoreTacticalWeaponGroup.Missiles => "MSL",
            CoreTacticalWeaponGroup.MachineGuns => "MG",
            CoreTacticalWeaponGroup.Autocannon30mm => "33",
            _ => "?"
        };
    }

    private static string FormatCompactAmount(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount < 10000)
        {
            return amount.ToString();
        }

        if (amount < 1000000)
        {
            return (amount / 1000f).ToString("0.00") + "k";
        }

        return (amount / 1000000f).ToString("0.00") + "M";
    }

    private static string FormatMassKg(float massKg)
    {
        massKg = Mathf.Max(0f, massKg);
        if (massKg >= 1000000000f)
        {
            return (massKg / 1000000000f).ToString("0.00") + " Mt";
        }

        if (massKg >= 1000000f)
        {
            return (massKg / 1000000f).ToString("0.00") + " kt";
        }

        if (massKg >= 1000f)
        {
            return (massKg / 1000f).ToString("0.0") + " t";
        }

        return massKg.ToString("0") + " kg";
    }

    private static string FormatVerticalDrift(float driftMS)
    {
        if (Mathf.Abs(driftMS) <= 0.001f)
        {
            return "steady";
        }

        return (driftMS > 0f ? "up " : "down ") + Mathf.Abs(driftMS).ToString("0.000") + " m/s";
    }

    private static string FormatFlatDrift(Vector3 driftMS)
    {
        Vector3 flat = driftMS;
        flat.y = 0f;
        if (flat.sqrMagnitude <= 0.000001f)
        {
            return "steady";
        }

        return flat.magnitude.ToString("0.00") + " m/s";
    }

    private void DrawPanel(Rect rect, float alpha)
    {
        DrawHudRect(rect, new Color(0.015f, 0.045f, 0.060f, alpha));
        DrawOutlinedRect(rect, new Color(0.16f, 0.70f, 0.94f, 0.38f));
    }

    private static void DrawBar(Rect rect, float value01, Color fillColor, Color backColor)
    {
        DrawHudRect(rect, backColor);
        Rect fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height);
        DrawHudRect(fill, fillColor);
        DrawOutlinedRect(rect, new Color(0f, 0f, 0f, 0.38f));
    }

    private static void DrawHudRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private static void DrawOutlinedRect(Rect rect, Color color)
    {
        DrawHudRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        DrawHudRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        DrawHudRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        DrawHudRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private void DrawMissionHud()
    {
        int destroyedEnemies = GetDestroyedEnemyObjectiveCount();
        float minedOreKg = GetMissionOreProgressKg();
        string objective = extractionUnlocked
            ? "Exit: AUTO EXIT is available at any time."
            : "Exit: route is preparing. Bonus: destroy 4 enemy ships and mine 300 kg of any ore.";

        float speed = 0f;
        if (playerShip != null && playerShip.Body != null)
        {
            Vector3 velocity = playerShip.Body.linearVelocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
        }

        string text =
            "Core Tactical Combat Sortie\n" +
            "Ship: " + (playerShip != null ? playerShip.displayName : "Ship") + " | " + playerLoadoutSummary + "\n" +
            "Altitude: " + (playerShip != null ? playerShip.transform.position.y.ToString("0") : "0") + " m | Speed: " + FormatShipSpeedLine(playerShip, speed) + "\n" +
            "Enemy ships destroyed: " + Mathf.Min(destroyedEnemies, RequiredEnemyKillObjectiveCount) + "/" + RequiredEnemyKillObjectiveCount + "\n" +
            "Ore mined: " + Mathf.Min(minedOreKg, RequiredOreObjectiveKg).ToString("0") + "/" + RequiredOreObjectiveKg.ToString("0") + " kg\n" +
            BuildSlipStatusLine() + "\n" +
            objective + "\n" +
            missionStatus;
        Rect rect = new Rect(14f, Screen.height - 174f, 720f, 160f);
        RegisterGuiRect(rect);
        GUI.Box(rect, "");
        GUI.Label(new Rect(26f, Screen.height - 166f, 696f, 142f), text);
    }

    private void DrawShipControlPanel()
    {
        Rect panelRect = new Rect(14f, 14f, 360f, 154f);
        RegisterGuiRect(panelRect);
        GUI.Box(panelRect, "");
        GUI.Label(new Rect(26f, 22f, 332f, 22f), playerShip != null ? playerShip.displayName : "Ship");
        GUI.Label(new Rect(26f, 46f, 332f, 22f), "Selection: " + (playerShip != null && playerShip.IsSelected ? "selected" : "none"));

        if (GUI.Button(new Rect(26f, 72f, 102f, 28f), "SELECT"))
        {
            if (playerShip != null)
            {
                playerShip.SetSelected(true);
            }
        }

        if (GUI.Button(new Rect(138f, 72f, 102f, 28f), "DESELECT"))
        {
            if (playerShip != null)
            {
                playerShip.SetSelected(false);
            }
        }

        CoreTacticalWeaponControl weaponControl = playerShip != null ? playerShip.GetComponent<CoreTacticalWeaponControl>() : null;
        if (weaponControl != null)
        {
            CoreTacticalWeaponGroup group = CoreTacticalWeaponGroup.Autocannon30mm;
            bool enabled = weaponControl.IsFireEnabled(group);
            bool active = weaponControl.IsWeaponGroupActive(group);
            GUI.Label(new Rect(26f, 108f, 214f, 22f), "Autocannons: " + (active ? "infinite" : "not installed"));
            if (GUI.Button(new Rect(250f, 104f, 96f, 30f), enabled ? "FIRE" : "HOLD"))
            {
                weaponControl.ToggleFireEnabled(group);
            }
        }
    }

    private void DrawClaudianSlipButtons()
    {
        Rect panelRect = new Rect(Screen.width - 304f, Screen.height - 164f, 280f, 140f);
        Rect statusRect = new Rect(Screen.width - 292f, Screen.height - 154f, 256f, 28f);
        Rect slipButtonRect = new Rect(Screen.width - 292f, Screen.height - 120f, 256f, 42f);
        Rect exitButtonRect = new Rect(Screen.width - 292f, Screen.height - 70f, 256f, 42f);
        RegisterGuiRect(panelRect);

        string slipLabel = playerSlipDrive == null
            ? "CLAUDIAN SLIP"
            : playerSlipDrive.IsActive
                ? "CLAUDIAN SLIP ACTIVE"
                : playerSlipDrive.IsArmed ? "CLAUDIAN SLIP ARMED" : "CLAUDIAN SLIP OFF";

        GUI.Box(panelRect, "");
        GUI.Label(statusRect, BuildSlipSpeedLine());

        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && playerSlipDrive != null && !extractionComplete;
        if (GUI.Button(slipButtonRect, slipLabel))
        {
            ToggleClaudianSlip();
        }

        GUI.enabled = wasEnabled && !extractionComplete;
        if (GUI.Button(exitButtonRect, autoExitRequested ? "AUTO EXIT ACTIVE" : "AUTO EXIT"))
        {
            BeginAutoExit();
        }

        GUI.enabled = wasEnabled;
    }

    private void RegisterGuiRect(Rect rect)
    {
        missionGuiRects.Add(rect);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        RestoreSuppressedSessionActors();
        if (exitMaterial != null)
        {
            Destroy(exitMaterial);
        }

        if (missionBoundaryMaterial != null)
        {
            Destroy(missionBoundaryMaterial);
        }

        HideTorpedoAimLine();
        if (torpedoAimMaterial != null)
        {
            Destroy(torpedoAimMaterial);
        }

        if (torpedoAllowedSectorMaterial != null)
        {
            Destroy(torpedoAllowedSectorMaterial);
        }

        if (harpoonTargetRingLine != null)
        {
            Destroy(harpoonTargetRingLine.gameObject);
            harpoonTargetRingLine = null;
        }

        if (harpoonTargetRingMaterial != null)
        {
            Destroy(harpoonTargetRingMaterial);
        }

        CoreTacticalFleetController.TacticalPointerInputBlocked = false;
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }

    private bool TryBuild()
    {
        MetaGameState meta = ResolveMeta();
        if (!IsCoreTacticalCombatSortieActive(meta))
        {
            return false;
        }

        activeInstance = this;
        built = true;
        SuppressSessionFlightActors();
        BuildTacticalScene(meta.ActiveSortie.zone);
        missionStatus = "Weapons free. " + playerLoadoutSummary + " online.";
        return true;
    }

    private void BuildTacticalScene(SortieZoneDefinition zone)
    {
        zone.Normalize();
        tacticalRoot = new GameObject(TacticalRootName);
        tacticalRoot.transform.SetParent(transform, false);

        fleet = gameObject.AddComponent<CoreTacticalFleetController>();
        fleet.commandPlaneAltitudeMeters = ResolveCommandPlaneAltitude(zone);
        fleet.minCommandAltitudeMeters = fleet.commandPlaneAltitudeMeters - 120f;
        fleet.maxCommandAltitudeMeters = fleet.commandPlaneAltitudeMeters + 220f;
        fleet.gridHalfSizeMeters = 14500f;
        fleet.gridStepMeters = 500f;
        fleet.gridLineWidthMeters = 1.4f;
        fleet.formationSpacingMeters = 220f;
        fleet.selectAllOnStart = true;
        fleet.showPrototypeHud = false;
        fleet.restrictSelectionToTeam = true;
        fleet.selectableTeam = CoreTacticalCombatTeam.Friendly;

        CreateTacticalCamera();
        CreateTacticalLight();

        Vector3 center = zone.centerPosition;
        center.y = fleet.commandPlaneAltitudeMeters;
        missionCenter = center;
        missionRadiusMeters = Mathf.Max(100f, zone.radiusMeters);
        fleet.SetCommandGridWorldAnchor(missionCenter);
        CreateMissionBoundaryMarker();
        pendingFreightReward = Mathf.Max(0, zone.completionFreightAward);
        pendingShipExperienceReward = Mathf.Max(0, zone.completionDesignExperienceAward);
        pendingReputationReward = 0;
        exitDirection = ResolveExitDirection(zone);
        EnsureExitAvailable(zone);
        Vector3 playerPosition = center - exitDirection * PlayerSpawnDistanceMeters;
        playerPosition.y = fleet.commandPlaneAltitudeMeters;
        Quaternion playerRotation = Quaternion.LookRotation(exitDirection, Vector3.up);

        MetaGameState activeMeta = ResolveMeta();
        DockedDevelopmentShipState selectedDockSlot = activeMeta != null ? activeMeta.GetSelectedDevelopmentDockShipSlot() : null;
        playerShipEntry = ResolveSelectedShipEntry(activeMeta);
        RuntimeShipProfile playerProfile = BuildPlayerRuntimeProfile(playerShipEntry, selectedDockSlot, activeMeta != null ? activeMeta.SessionConfig : null);
        playerRuntimeProfile = playerProfile;
        playerLoadoutSummary = playerProfile.loadoutSummary;

        playerShip = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
            fleet,
            playerProfile.shipId,
            playerProfile.displayName,
            playerPosition,
            playerRotation,
            playerProfile.hullSizeMeters,
            playerProfile.maxForwardSpeedMS,
            playerProfile.forwardAccelerationMS2,
            playerProfile.brakingAccelerationMS2,
            playerProfile.maxYawRateDegPerSecond,
            playerProfile.maxReverseSpeedMS,
            playerProfile.maxLateralSpeedMS,
            playerProfile.massKg,
            true,
            playerProfile.color);
        ConfigureRuntimeDamageProfile(playerShip, playerProfile);
        playerShip.transform.SetParent(tacticalRoot.transform, true);
        AttachImportedPlayerVisual(playerShip, playerProfile);
        ConfigureRuntimeLoadout(playerShip, playerProfile, null, CoreTacticalCombatTeam.Enemy, activeMeta != null ? activeMeta.SessionConfig : null);
        ConfigurePlayerSlipDrive(playerShip);
        ConfigurePlayerMiningRig(playerShip, activeMeta);
        playerShip.SetSelected(true);
        Vector3 entryFlyThroughTarget = center + exitDirection * PlayerSpawnDistanceMeters;
        entryFlyThroughTarget.y = fleet.commandPlaneAltitudeMeters;
        playerShip.SetCommand(entryFlyThroughTarget, exitDirection);
        PrimePlayerEntryVelocity(playerShip, exitDirection, ClaudianSlipTargetSpeedMS);
        if (playerSlipDrive != null)
        {
            playerSlipDrive.EnterActiveSlipAtFullSpeed(exitDirection);
        }

        SpawnEnemyForPlayerProfile(center, playerProfile);
        SpawnCoreTacticalOreBoulders(center, activeMeta != null ? activeMeta.SessionConfig : null, zone);
        SpawnCoreTacticalGasClouds(center, activeMeta != null ? activeMeta.SessionConfig : null, zone);
        totalEnemies = enemyShips.Count;
        MirrorSessionShipToTacticalPlayer();
        ApplyTacticalCameraAfterFleetSpawn();
    }

    private float ResolveCommandPlaneAltitude(SortieZoneDefinition zone)
    {
        if (zone != null && zone.entryPosition != Vector3.zero)
        {
            return zone.entryPosition.y;
        }

        return CommandPlaneAltitudeMeters;
    }

    private ShipTreeEntryConfig ResolveSelectedShipEntry(MetaGameState meta)
    {
        if (meta == null)
        {
            return null;
        }

        DockedDevelopmentShipState selectedSlot = meta.GetSelectedDevelopmentDockShipSlot();
        if (selectedSlot != null && selectedSlot.HasShip)
        {
            ShipTreeEntryConfig dockShip = FindShipTreeEntry(meta, selectedSlot.shipId);
            if (dockShip != null)
            {
                return dockShip;
            }
        }

        return FindShipTreeEntry(meta, "capital_patrol_frigate_r02");
    }

    private static ShipTreeEntryConfig FindShipTreeEntry(MetaGameState meta, string shipId)
    {
        if (meta == null || string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        IReadOnlyList<ShipTreeEntryConfig> ships = meta.GetShipTreeEntryConfigs();
        if (ships == null)
        {
            return null;
        }

        for (int i = 0; i < ships.Count; i++)
        {
            ShipTreeEntryConfig ship = ships[i];
            if (ship != null && string.Equals(ship.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
            {
                return ship;
            }
        }

        return null;
    }

    private static RuntimeShipProfile BuildPlayerRuntimeProfile(ShipTreeEntryConfig ship, DockedDevelopmentShipState selectedSlot, SessionConfigDatabase config)
    {
        string classId = NormalizeKey(ship != null ? ship.shipClassId : "");
        string roleId = NormalizeKey(ship != null ? ship.roleId : "");
        string shipId = ship != null && !string.IsNullOrWhiteSpace(ship.shipId) ? ship.shipId : "capital_patrol_frigate_r02";
        string displayName = ship != null && !string.IsNullOrWhiteSpace(ship.DisplayNameRu) ? ship.DisplayNameRu : "РљРѕСЂС€СѓРЅ";
        Color color = ship != null && ship.visualColor != default ? ship.visualColor : new Color(0.16f, 0.38f, 0.86f, 1f);
        string effectiveClassId = string.IsNullOrWhiteSpace(classId) ? "frigate" : classId;

        KorshunHullPackageConfig hullPackage = ResolveHullPackage(config, shipId, selectedSlot != null ? selectedSlot.GetLoadoutPackageId("hull") : "");
        KorshunPowerPlantConfig powerPlant = ResolvePowerPlant(config, shipId, selectedSlot != null ? selectedSlot.GetLoadoutPackageId("power") : "");
        ShipCitadelPackageConfig citadel = ResolveCitadelPackage(config, shipId, selectedSlot != null ? selectedSlot.GetLoadoutPackageId("citadel") : "");

        Vector3 fallbackSize = effectiveClassId == "battleship"
            ? new Vector3(62f, 20f, 350f)
            : effectiveClassId == "cruiser"
                ? new Vector3(32f, 12f, 150f)
                : new Vector3(15f, 7f, 60f);
        float fallbackSpeed = effectiveClassId == "battleship" ? ClassBattleshipAverageSpeedMS : effectiveClassId == "cruiser" ? ClassCruiserAverageSpeedMS : ClassFrigateAverageSpeedMS;
        float fallbackAcceleration = CalculateClassAccelerationMS2(effectiveClassId, fallbackSpeed);
        float fallbackTurnRate = effectiveClassId == "battleship" ? ClassBattleshipAverageYawDegPerSecond : effectiveClassId == "cruiser" ? ClassCruiserAverageYawDegPerSecond : ClassFrigateAverageYawDegPerSecond;
        float length = hullPackage != null && hullPackage.lengthM > 0f ? hullPackage.lengthM : fallbackSize.z;
        float fallbackStructureHp = effectiveClassId == "battleship" ? 140000f : effectiveClassId == "cruiser" ? 58000f : 13500f;
        float fallbackCargoTons = ship != null && ship.cargoCapacityTons > 0f
            ? ship.cargoCapacityTons
            : effectiveClassId == "battleship" ? 900f : effectiveClassId == "cruiser" ? 220f : 40f;
        float structureHp = hullPackage != null && hullPackage.structureHp > 0f ? hullPackage.structureHp : fallbackStructureHp;
        CoreTacticalResistanceSet resistances = hullPackage != null
            ? hullPackage.Resistances
            : CoreTacticalDamageProfile.ResolveClassBaselineResistances(effectiveClassId);
        if (citadel != null)
        {
            resistances = AddResistanceBonus(resistances, citadel.ResistanceBonuses);
        }

        float cargoCapacityTons = hullPackage != null && hullPackage.cargoCapacityTons > 0f ? hullPackage.cargoCapacityTons : fallbackCargoTons;
        float speed = hullPackage != null && hullPackage.cruiseSpeedMS > 0f ? hullPackage.cruiseSpeedMS : fallbackSpeed;
        float acceleration = hullPackage != null && hullPackage.accelerationMS2 > 0f ? hullPackage.accelerationMS2 : fallbackAcceleration;
        float turnRate = hullPackage != null && hullPackage.turnRateDegPerSecond > 0f ? hullPackage.turnRateDegPerSecond : fallbackTurnRate;
        if (powerPlant != null)
        {
            speed += powerPlant.speedDeltaMS;
            acceleration += powerPlant.accelerationDeltaMS2;
            turnRate += powerPlant.turnRateDeltaDegPerSecond;
        }

        if (citadel != null)
        {
            speed += citadel.speedDeltaMS;
            acceleration += citadel.accelerationDeltaMS2;
            turnRate += citadel.turnRateDeltaDegPerSecond;
        }

        speed *= ResolveClassSpeedMultiplier(effectiveClassId);

        Vector3 hullSize = new Vector3(fallbackSize.x, fallbackSize.y, length);
        float classAccelerationFloor = CalculateClassAccelerationMS2(effectiveClassId, speed);
        acceleration = Mathf.Max(acceleration, classAccelerationFloor);

        RuntimeLoadoutKind loadout = effectiveClassId == "battleship"
            ? RuntimeLoadoutKind.BattleshipFull
            : effectiveClassId == "cruiser"
                ? RuntimeLoadoutKind.ArtilleryCruiser
                : RuntimeLoadoutKind.FrigateAutocannon;
        float massKg = effectiveClassId == "battleship" ? 14000000f : effectiveClassId == "cruiser" ? 3200000f : 520000f;
        float rangeMultiplier = hullPackage != null ? hullPackage.weaponRangeMultiplier : 1f;
        float reloadMultiplier = hullPackage != null ? hullPackage.reloadMultiplier : 1f;
        float dispersionMultiplier = hullPackage != null ? hullPackage.dispersionMultiplier : 1f;
        float reloadRateMultiplier = citadel != null ? citadel.reloadRateMultiplier : 1f;
        string mainPackageId = selectedSlot != null ? selectedSlot.GetLoadoutPackageId("main") : "";
        string secondaryPackageId = selectedSlot != null ? selectedSlot.GetLoadoutPackageId("secondary") : "";
        string smallPackageId = selectedSlot != null ? selectedSlot.GetLoadoutPackageId("small") : "";
        string auxiliaryPackageId = selectedSlot != null ? selectedSlot.GetLoadoutPackageId("auxiliary") : "";
        string summary = BuildRuntimeLoadoutSummary(config, hullPackage, powerPlant, citadel, mainPackageId, secondaryPackageId, smallPackageId, auxiliaryPackageId);

        return new RuntimeShipProfile
        {
            shipId = shipId,
            displayName = displayName,
            classId = effectiveClassId,
            roleId = roleId,
            loadout = loadout,
            hullSizeMeters = hullSize,
            maxForwardSpeedMS = Mathf.Max(6f, speed),
            forwardAccelerationMS2 = Mathf.Max(1f, acceleration),
            brakingAccelerationMS2 = Mathf.Max(1f, acceleration),
            maxYawRateDegPerSecond = Mathf.Max(2f, turnRate),
            maxReverseSpeedMS = Mathf.Max(2f, speed * 0.22f),
            maxLateralSpeedMS = Mathf.Max(1f, speed * 0.12f),
            massKg = massKg,
            structureHp = Mathf.Max(1f, structureHp),
            resistances = resistances,
            cargoCapacityTons = Mathf.Max(0.1f, cargoCapacityTons),
            citadelHp = citadel != null ? Mathf.Max(0f, citadel.citadelHp) : 0f,
            powerPlantModuleHp = powerPlant != null ? Mathf.Max(0f, powerPlant.moduleHp) : 0f,
            color = color,
            loadoutSummary = string.IsNullOrWhiteSpace(summary) ? "configured ship packages" : summary,
            weaponRangeMultiplier = rangeMultiplier,
            reloadMultiplier = reloadMultiplier,
            dispersionMultiplier = dispersionMultiplier,
            reloadRateMultiplier = reloadRateMultiplier,
            hullPackageId = hullPackage != null ? hullPackage.id : "",
            powerPackageId = powerPlant != null ? powerPlant.id : "",
            citadelPackageId = citadel != null ? citadel.id : "",
            mainPackageId = mainPackageId,
            secondaryPackageId = secondaryPackageId,
            smallPackageId = smallPackageId,
            auxiliaryPackageId = auxiliaryPackageId
        };
    }

    private static CoreTacticalResistanceSet AddResistanceBonus(CoreTacticalResistanceSet baseSet, CoreTacticalResistanceSet bonusSet)
    {
        return new CoreTacticalResistanceSet(
            baseSet.kineticPercent + bonusSet.kineticPercent,
            baseSet.thermalPercent + bonusSet.thermalPercent,
            baseSet.chemicalPercent + bonusSet.chemicalPercent,
            baseSet.explosivePercent + bonusSet.explosivePercent);
    }

    private static string FormatDamageResistances(CoreTacticalResistanceSet resistanceSet)
    {
        resistanceSet.Normalize();
        return "K "
            + resistanceSet.kineticPercent.ToString("0")
            + "% / T "
            + resistanceSet.thermalPercent.ToString("0")
            + "% / C "
            + resistanceSet.chemicalPercent.ToString("0")
            + "% / X "
            + resistanceSet.explosivePercent.ToString("0")
            + "%";
    }

    public static bool TryBuildRuntimeProfileSnapshotForTests(
        ShipTreeEntryConfig ship,
        DockedDevelopmentShipState selectedSlot,
        SessionConfigDatabase config,
        out CoreTacticalRuntimeProfileSnapshot snapshot)
    {
        RuntimeShipProfile profile = BuildPlayerRuntimeProfile(ship, selectedSlot, config);
        snapshot = CreateRuntimeProfileSnapshot(profile, null, null);
        return !string.IsNullOrWhiteSpace(snapshot.shipId);
    }

    public static bool TryConfigureRuntimeLoadoutForTests(
        CoreTacticalShipMotor ship,
        ShipTreeEntryConfig shipEntry,
        DockedDevelopmentShipState selectedSlot,
        SessionConfigDatabase config,
        out CoreTacticalRuntimeProfileSnapshot snapshot)
    {
        snapshot = default;
        if (ship == null)
        {
            return false;
        }

        RuntimeShipProfile profile = BuildPlayerRuntimeProfile(shipEntry, selectedSlot, config);
        ship.maxForwardSpeedMS = profile.maxForwardSpeedMS;
        ship.forwardAccelerationMS2 = profile.forwardAccelerationMS2;
        ship.brakingAccelerationMS2 = profile.brakingAccelerationMS2;
        ship.maxYawRateDegPerSecond = profile.maxYawRateDegPerSecond;
        ship.maxReverseSpeedMS = profile.maxReverseSpeedMS;
        ship.maxLateralSpeedMS = profile.maxLateralSpeedMS;
        ConfigureRuntimeDamageProfile(ship, profile);
        ConfigureRuntimeLoadout(ship, profile, null, CoreTacticalCombatTeam.Enemy, config);
        snapshot = CreateRuntimeProfileSnapshot(profile, ship, ship.GetComponent<CoreTacticalWeaponControl>());
        return true;
    }

    private static CoreTacticalRuntimeProfileSnapshot CreateRuntimeProfileSnapshot(RuntimeShipProfile profile, CoreTacticalShipMotor ship, CoreTacticalWeaponControl weaponControl)
    {
        CoreTacticalRuntimeProfileSnapshot snapshot = new CoreTacticalRuntimeProfileSnapshot
        {
            shipId = profile.shipId,
            displayName = profile.displayName,
            classId = profile.classId,
            roleId = profile.roleId,
            loadoutKind = profile.loadout.ToString(),
            hullSizeMeters = profile.hullSizeMeters,
            maxForwardSpeedMS = profile.maxForwardSpeedMS,
            forwardAccelerationMS2 = profile.forwardAccelerationMS2,
            brakingAccelerationMS2 = profile.brakingAccelerationMS2,
            maxYawRateDegPerSecond = profile.maxYawRateDegPerSecond,
            maxReverseSpeedMS = profile.maxReverseSpeedMS,
            maxLateralSpeedMS = profile.maxLateralSpeedMS,
            massKg = profile.massKg,
            structureHp = profile.structureHp,
            resistances = profile.resistances,
            cargoCapacityTons = profile.cargoCapacityTons,
            citadelHp = profile.citadelHp,
            powerPlantModuleHp = profile.powerPlantModuleHp,
            actualMaxForwardSpeedMS = ship != null ? ship.maxForwardSpeedMS : profile.maxForwardSpeedMS,
            actualForwardAccelerationMS2 = ship != null ? ship.forwardAccelerationMS2 : profile.forwardAccelerationMS2,
            actualBrakingAccelerationMS2 = ship != null ? ship.brakingAccelerationMS2 : profile.brakingAccelerationMS2,
            actualMaxYawRateDegPerSecond = ship != null ? ship.maxYawRateDegPerSecond : profile.maxYawRateDegPerSecond,
            actualFlatSpeedMS = GetFlatSpeedMS(ship),
            actualTargetDistanceMeters = GetFlatTargetDistanceMeters(ship),
            loadoutSummary = profile.loadoutSummary,
            hullPackageId = profile.hullPackageId,
            powerPackageId = profile.powerPackageId,
            citadelPackageId = profile.citadelPackageId,
            mainPackageId = profile.mainPackageId,
            secondaryPackageId = profile.secondaryPackageId,
            smallPackageId = profile.smallPackageId,
            auxiliaryPackageId = profile.auxiliaryPackageId
        };

        if (weaponControl != null)
        {
            snapshot.mainBatteryActive = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.MainBattery);
            snapshot.secondary76Active = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Secondary76mm);
            snapshot.secondary152Active = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Secondary152mm);
            snapshot.missileActive = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Missiles);
            snapshot.machineGunActive = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.MachineGuns);
            snapshot.autocannonActive = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Autocannon30mm);
            snapshot.torpedoActive = weaponControl.IsWeaponGroupActive(CoreTacticalWeaponGroup.Torpedoes);
            snapshot.activeWeaponGroupCount = CountActiveWeaponGroups(weaponControl);
            snapshot.hasAnyFireableWeapon = snapshot.activeWeaponGroupCount > 0;
            snapshot.hasAnyNonMissileWeapon = snapshot.mainBatteryActive
                || snapshot.secondary76Active
                || snapshot.secondary152Active
                || snapshot.machineGunActive
                || snapshot.autocannonActive;
        }

        return snapshot;
    }

    private static float GetFlatSpeedMS(CoreTacticalShipMotor ship)
    {
        if (ship == null || ship.Body == null)
        {
            return 0f;
        }

        Vector3 velocity = ship.Body.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private static void PrimePlayerEntryVelocity(CoreTacticalShipMotor ship, Vector3 direction, float minimumSpeedMS)
    {
        if (ship == null || ship.Body == null)
        {
            return;
        }

        float speedFloor = Mathf.Max(0f, minimumSpeedMS);
        Vector3 flatVelocity = ship.Body.linearVelocity;
        flatVelocity.y = 0f;
        if (flatVelocity.magnitude >= speedFloor - 0.1f)
        {
            return;
        }

        Vector3 nextVelocity = FlattenDirection(direction, ship.transform.forward) * speedFloor;
        nextVelocity.y = ship.Body.linearVelocity.y;
        ship.Body.linearVelocity = nextVelocity;
    }

    private static float GetFlatTargetDistanceMeters(CoreTacticalShipMotor ship)
    {
        if (ship == null || ship.Body == null)
        {
            return 0f;
        }

        Vector3 delta = ship.TargetPosition - ship.Body.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private static int CountActiveWeaponGroups(CoreTacticalWeaponControl weaponControl)
    {
        if (weaponControl == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < CoreTacticalWeaponControl.WeaponGroupCount; i++)
        {
            CoreTacticalWeaponGroup group = (CoreTacticalWeaponGroup)i;
            if (weaponControl.IsWeaponGroupActive(group))
            {
                count++;
            }
        }

        return count;
    }

    private static float CalculateClassAccelerationMS2(string classId, float maxForwardSpeedMS)
    {
        float timeTo90 = classId == "battleship"
            ? ClassBattleshipAcceleration90PercentSeconds
            : classId == "cruiser"
                ? ClassCruiserAcceleration90PercentSeconds
                : ClassFrigateAcceleration90PercentSeconds;
        return Mathf.Max(0.1f, maxForwardSpeedMS) * NaturalLogTen / Mathf.Max(0.1f, timeTo90);
    }

    private static float ResolveClassSpeedMultiplier(string classId)
    {
        if (classId == "battleship")
        {
            return ClassBattleshipSpeedMultiplier;
        }

        if (classId == "cruiser")
        {
            return ClassCruiserSpeedMultiplier;
        }

        return ClassFrigateSpeedMultiplier;
    }

    private static KorshunHullPackageConfig ResolveHullPackage(SessionConfigDatabase config, string shipId, string selectedPackageId)
    {
        KorshunHullPackageConfig selected = config != null ? config.GetKorshunHullPackage(selectedPackageId) : null;
        if (selected != null && string.Equals(selected.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
        {
            return selected;
        }

        if (config == null) return null;
        for (int i = 0; i < config.korshunHullPackages.Count; i++)
        {
            KorshunHullPackageConfig package = config.korshunHullPackages[i];
            if (package != null && package.IsBasePackage && string.Equals(package.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
            {
                return package;
            }
        }

        return null;
    }

    private static KorshunPowerPlantConfig ResolvePowerPlant(SessionConfigDatabase config, string shipId, string selectedPackageId)
    {
        KorshunPowerPlantConfig selected = config != null ? config.GetKorshunPowerPlant(selectedPackageId) : null;
        if (selected != null && string.Equals(selected.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
        {
            return selected;
        }

        if (config == null) return null;
        for (int i = 0; i < config.korshunPowerPlants.Count; i++)
        {
            KorshunPowerPlantConfig package = config.korshunPowerPlants[i];
            if (package != null && package.IsBasePackage && string.Equals(package.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
            {
                return package;
            }
        }

        return null;
    }

    private static ShipCitadelPackageConfig ResolveCitadelPackage(SessionConfigDatabase config, string shipId, string selectedPackageId)
    {
        ShipCitadelPackageConfig selected = config != null ? config.GetShipCitadelPackage(selectedPackageId) : null;
        if (selected != null && string.Equals(selected.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
        {
            return selected;
        }

        if (config == null) return null;
        for (int i = 0; i < config.shipCitadelPackages.Count; i++)
        {
            ShipCitadelPackageConfig package = config.shipCitadelPackages[i];
            if (package != null && package.IsBasePackage && string.Equals(package.shipId, shipId, System.StringComparison.OrdinalIgnoreCase))
            {
                return package;
            }
        }

        return null;
    }

    private static string BuildRuntimeLoadoutSummary(
        SessionConfigDatabase config,
        KorshunHullPackageConfig hull,
        KorshunPowerPlantConfig power,
        ShipCitadelPackageConfig citadel,
        string mainPackageId,
        string secondaryPackageId,
        string smallPackageId,
        string auxiliaryPackageId)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        AppendRuntimePackageName(builder, hull != null ? hull.DisplayNameRu : "");
        AppendRuntimePackageName(builder, power != null ? power.DisplayNameRu : "");
        AppendRuntimePackageName(builder, citadel != null ? citadel.DisplayNameRu : "");
        AppendRuntimePackageName(builder, ResolvePackageDisplayName(config, mainPackageId));
        AppendRuntimePackageName(builder, ResolvePackageDisplayName(config, secondaryPackageId));
        AppendRuntimePackageName(builder, ResolvePackageDisplayName(config, smallPackageId));
        AppendRuntimePackageName(builder, ResolvePackageDisplayName(config, auxiliaryPackageId));
        return builder.ToString();
    }

    private static void AppendRuntimePackageName(System.Text.StringBuilder builder, string value)
    {
        if (builder == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(value);
    }

    private static string ResolvePackageDisplayName(SessionConfigDatabase config, string packageId)
    {
        if (config == null || string.IsNullOrWhiteSpace(packageId))
        {
            return "";
        }

        KorshunWeaponPackageConfig weapon = config.GetKorshunWeaponPackage(packageId);
        if (weapon != null) return weapon.DisplayNameRu;
        KorshunAuxiliaryPackageConfig auxiliary = config.GetKorshunAuxiliaryPackage(packageId);
        if (auxiliary != null) return auxiliary.DisplayNameRu;
        return "";
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static void AttachImportedPlayerVisual(CoreTacticalShipMotor ship, RuntimeShipProfile profile)
    {
        if (ship == null)
        {
            return;
        }

        GameObject visualPrefab = LoadImportedShipVisual(profile.shipId);
        if (visualPrefab == null)
        {
            return;
        }

        Renderer rootRenderer = ship.GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }
        ship.hideRuntimeWeaponVisuals = true;

        GameObject visual = Instantiate(visualPrefab, ship.transform);
        visual.name = "Imported Visual - " + profile.displayName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = GetInverseScale(ship.transform.localScale);

        DisableImportedVisualPhysics(visual);
        FitImportedVisualToHull(ship.transform, visual, profile.hullSizeMeters);
        ApplyImportedShipCombatLoadoutVisual(visual.transform, profile);

        CoreTacticalShipVisualWeaponBinding binding = ship.GetComponent<CoreTacticalShipVisualWeaponBinding>();
        if (binding == null)
        {
            binding = ship.gameObject.AddComponent<CoreTacticalShipVisualWeaponBinding>();
        }

        binding.Initialize(ship, visual.transform);
    }

    private static GameObject LoadImportedShipVisual(string shipId)
    {
#if UNITY_EDITOR
        string path = GetImportedShipVisualPath(shipId);
        return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private static string GetImportedShipVisualPath(string shipId)
    {
        switch (NormalizeKey(shipId))
        {
            case "capital_patrol_frigate_r02":
                return "Assets/ShipImports/Models/BlenderShips/WW_Imperial_PatrolFrigate_R02_Korshun.fbx";
            case "capital_artillery_cruiser_r02":
                return "Assets/ShipImports/Models/BlenderShips/WW_Imperial_ArtilleryCruiser_R02_Barbet.fbx";
            case "capital_heavy_battleship_r02":
                return "Assets/ShipImports/Models/BlenderShips/WW_Imperial_Battleship_Val.fbx";
            default:
                return "";
        }
    }

    private static Vector3 GetInverseScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
            Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
            Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
    }

    private static void DisableImportedVisualPhysics(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Destroy(bodies[i]);
        }
    }

    private static void FitImportedVisualToHull(Transform shipRoot, GameObject visual, Vector3 hullSizeMeters)
    {
        if (shipRoot == null || visual == null)
        {
            return;
        }

        if (!TryGetRendererBounds(visual, out Bounds bounds))
        {
            return;
        }

        float targetLength = Mathf.Max(1f, hullSizeMeters.z);
        float importedLength = Mathf.Max(bounds.size.z, bounds.size.x);
        if (importedLength > 0.001f)
        {
            float fitScale = targetLength / importedLength;
            visual.transform.localScale *= fitScale;
        }

        if (TryGetRendererBounds(visual, out bounds))
        {
            visual.transform.position += shipRoot.position - bounds.center;
        }
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static void ApplyImportedShipCombatLoadoutVisual(Transform modelRoot, RuntimeShipProfile profile)
    {
        if (modelRoot == null)
        {
            return;
        }

        string shipId = NormalizeKey(profile.shipId);
        if (shipId == "capital_patrol_frigate_r02")
        {
            ApplyImportedKorshunCombatLoadoutVisual(modelRoot, profile);
            return;
        }

        if (shipId == "capital_artillery_cruiser_r02")
        {
            ApplyImportedBarbetCombatSmallVisual(modelRoot, profile.smallPackageId);
        }
    }

    private static void ApplyImportedKorshunCombatLoadoutVisual(Transform modelRoot, RuntimeShipProfile profile)
    {
        if (modelRoot == null || NormalizeKey(profile.shipId) != "capital_patrol_frigate_r02")
        {
            return;
        }

        ApplyImportedKorshunCombatMainVisual(modelRoot, profile.mainPackageId);
        ApplyImportedKorshunCombatAuxiliaryVisual(modelRoot, profile.auxiliaryPackageId);
    }

    private static void ApplyImportedKorshunCombatMainVisual(Transform modelRoot, string packageId)
    {
        HideImportedKorshunCombatMainWeapons(modelRoot);
        string normalizedId = NormalizeKey(packageId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            normalizedId = "korshun_main_76mm_twin";
        }

        if (normalizedId.Contains("37mm_mg_aura"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_30mm_Single", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_Turret_30mm_Single_Fore", 0f, 1f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_30mm_Single", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_Turret_30mm_Single_Aft", 0f, 1f);
            return;
        }

        if (normalizedId.Contains("57mm_triple_autocannon"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_30mm_Single", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_Autocannon_57mm_Triple_Fore", 0f, 1.35f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_30mm_Single", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_Autocannon_57mm_Triple_Aft", 0f, 1.35f);
            return;
        }

        if (normalizedId.Contains("76mm_twin"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_76mm_Twin", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_Turret_76mm_Twin_Fore", 0f, 1f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_76mm_Twin", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_Turret_76mm_Twin_Aft", 0f, 1f);
            return;
        }

        if (normalizedId.Contains("100mm_single"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_100mm_Single", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_Turret_100mm_Single_Fore", 0f, 1f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_100mm_Single", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_Turret_100mm_Single_Aft", 0f, 1f);
            return;
        }

        if (normalizedId.Contains("nurs_turret"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RocketLauncher_Pod", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_RocketLauncher_Pod_Fore", 0f, 1.2f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RocketLauncher_Pod", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_RocketLauncher_Pod_Aft", 0f, 1.2f);
            return;
        }

        if (normalizedId.Contains("200mm_mortar"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_200mm_Mortar", "Korshun_Base_M_76mm_Fore", "Korshun_CombatMain_Mortar_200mm_Fore", 0f, 0.95f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_200mm_Mortar", "Korshun_Base_M_76mm_Aft", "Korshun_CombatMain_Mortar_200mm_Aft", 0f, 0.95f);
        }
    }

    private static void ApplyImportedBarbetCombatSmallVisual(Transform modelRoot, string packageId)
    {
        HideImportedBarbetCombatSmallEquipment(modelRoot);
        string normalizedId = NormalizeKey(packageId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return;
        }

        if (normalizedId.Contains("siphon"))
        {
            CreateImportedBarbetCombatSideEquipment(modelRoot, "WW_Turret_GasSiphon_Single", "Barbet_CombatSmall_GasSiphon_Left", -1, 0.92f);
            CreateImportedBarbetCombatSideEquipment(modelRoot, "WW_Turret_GasSiphon_Single", "Barbet_CombatSmall_GasSiphon_Right", 1, 0.92f);
            return;
        }

        if (normalizedId.Contains("magnet"))
        {
            CreateImportedBarbetCombatSideEquipment(modelRoot, "WW_Turret_Magnet_Single", "Barbet_CombatSmall_Magnet_Left", -1, 1f);
            CreateImportedBarbetCombatSideEquipment(modelRoot, "WW_Turret_Magnet_Single", "Barbet_CombatSmall_Magnet_Right", 1, 1f);
            return;
        }

        if (normalizedId.Contains("harpoon"))
        {
            CreateImportedBarbetCombatHarpoonPlaceholder(modelRoot, "Barbet_CombatSmall_HarpoonCannon_Left", -1, 1f);
            CreateImportedBarbetCombatHarpoonPlaceholder(modelRoot, "Barbet_CombatSmall_HarpoonCannon_Right", 1, 1f);
        }
    }

    private static void ApplyImportedKorshunCombatAuxiliaryVisual(Transform modelRoot, string packageId)
    {
        HideImportedKorshunCombatAuxiliaryWeapons(modelRoot);
        string normalizedId = NormalizeKey(packageId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            normalizedId = "korshun_aux_torpedo_triple_side";
        }

        if (normalizedId.Contains("torpedo_triple_side"))
        {
            SetDescendantActiveByNameContains(modelRoot, "Korshun_TorpedoLauncher_3Tube", true);
            return;
        }

        if (normalizedId.Contains("side_nurs"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RocketLauncher_Pod", "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_RocketLauncher_Pod_Left", 0f, 1.05f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RocketLauncher_Pod", "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_RocketLauncher_Pod_Right", 0f, 1.05f);
            return;
        }

        if (normalizedId.Contains("harpoon"))
        {
            CreateImportedKorshunCombatHarpoonPlaceholder(modelRoot, "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_HarpoonCannon_Left", -90f, 1f);
            CreateImportedKorshunCombatHarpoonPlaceholder(modelRoot, "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_HarpoonCannon_Right", 90f, 1f);
            return;
        }

        if (normalizedId.Contains("magnet"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_Magnet_Single", "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_Magnet_Left", 0f, 0.9f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_Magnet_Single", "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_Magnet_Right", 0f, 0.9f);
            return;
        }

        if (normalizedId.Contains("siphon"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_GasSiphon_Single", "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_GasSiphon_Left", 0f, 0.82f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_GasSiphon_Single", "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_GasSiphon_Right", 0f, 0.82f);
            return;
        }

        if (normalizedId.Contains("repair_beam"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RepairBeam_Single", "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_RepairBeam_Left", 0f, 0.8f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_RepairBeam_Single", "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_RepairBeam_Right", 0f, 0.8f);
            return;
        }

        if (normalizedId.Contains("scanner_hacker"))
        {
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_HackingDish_Single", "Korshun_Base_S_Torpedo_Left", "Korshun_CombatAux_HackingDish_Left", 0f, 0.68f);
            CreateImportedKorshunCombatEquipment(modelRoot, "WW_Turret_HackingDish_Single", "Korshun_Base_S_Torpedo_Right", "Korshun_CombatAux_HackingDish_Right", 0f, 0.68f);
        }
    }

    private static void HideImportedKorshunCombatMainWeapons(Transform modelRoot)
    {
        SetDescendantActiveByNameContains(modelRoot, "Korshun_Turret_76mm_Twin", false);
        SetDescendantActiveByNameContains(modelRoot, "Korshun_Autocannon_57mm_Triple", false);
        SetDescendantActiveByNameContains(modelRoot, "Korshun_RocketLauncher_Pod_Preview", false);
        SetDescendantActiveByNameContains(modelRoot, "Korshun_30mmSingle_Preview", false);
        SetDescendantActiveByNameContains(modelRoot, "Korshun_100mm_Single_Blockout", false);
        DestroyDescendantsByNameContains(modelRoot, "Korshun_CombatMain_");
    }

    private static void HideImportedKorshunCombatAuxiliaryWeapons(Transform modelRoot)
    {
        SetDescendantActiveByNameContains(modelRoot, "Korshun_TorpedoLauncher_3Tube", false);
        DestroyDescendantsByNameContains(modelRoot, "Korshun_CombatAux_");
    }

    private static void HideImportedBarbetCombatSmallEquipment(Transform modelRoot)
    {
        DestroyDescendantsByNameContains(modelRoot, "Barbet_CombatSmall_");
    }

    private static bool CreateImportedKorshunCombatEquipment(
        Transform modelRoot,
        string equipmentModelId,
        string targetMountName,
        string instanceName,
        float yawDegrees,
        float scale)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(equipmentModelId) || string.IsNullOrWhiteSpace(targetMountName))
        {
            return false;
        }

        Transform targetMount = FindDescendantByExactName(modelRoot, targetMountName);
        GameObject prefab = LoadImportedKorshunCombatEquipmentPrefab(equipmentModelId);
        if (targetMount == null || prefab == null)
        {
            return false;
        }

        GameObject module = Instantiate(prefab, modelRoot);
        module.name = string.IsNullOrWhiteSpace(instanceName) ? equipmentModelId + "_Combat" : instanceName;
        module.transform.localPosition = Vector3.zero;
        module.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        module.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        DisableImportedVisualPhysics(module);
        module.SetActive(true);

        if (!TryCalculateRendererBoundsInSpace(targetMount, modelRoot, out Bounds targetMountBounds)
            || !TryCalculateRendererBoundsInSpace(module.transform, modelRoot, out Bounds moduleBounds))
        {
            DestroyCombatLoadoutObject(module);
            return false;
        }

        Vector3 targetAnchor = new Vector3(targetMountBounds.center.x, targetMountBounds.max.y, targetMountBounds.center.z);
        Vector3 moduleAnchor = new Vector3(moduleBounds.center.x, moduleBounds.min.y, moduleBounds.center.z);
        module.transform.localPosition += targetAnchor - moduleAnchor;
        return true;
    }

    private static bool CreateImportedKorshunCombatHarpoonPlaceholder(
        Transform modelRoot,
        string targetMountName,
        string instanceName,
        float yawDegrees,
        float scale)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(targetMountName))
        {
            return false;
        }

        Transform targetMount = FindDescendantByExactName(modelRoot, targetMountName);
        if (targetMount == null)
        {
            return false;
        }

        GameObject module = CreateHarpoonPlaceholderObject(instanceName, yawDegrees, scale);
        module.transform.SetParent(modelRoot, false);
        module.transform.localPosition = Vector3.zero;
        if (!TryCalculateRendererBoundsInSpace(targetMount, modelRoot, out Bounds targetMountBounds)
            || !TryCalculateRendererBoundsInSpace(module.transform, modelRoot, out Bounds moduleBounds))
        {
            DestroyCombatLoadoutObject(module);
            return false;
        }

        Vector3 targetAnchor = new Vector3(targetMountBounds.center.x, targetMountBounds.max.y, targetMountBounds.center.z);
        Vector3 moduleAnchor = new Vector3(moduleBounds.center.x, moduleBounds.min.y, moduleBounds.center.z);
        module.transform.localPosition += targetAnchor - moduleAnchor;
        return true;
    }

    private static bool CreateImportedBarbetCombatSideEquipment(
        Transform modelRoot,
        string equipmentModelId,
        string instanceName,
        int sideSign,
        float scale)
    {
        if (modelRoot == null || string.IsNullOrWhiteSpace(equipmentModelId))
        {
            return false;
        }

        if (!TryCalculateRendererBoundsInSpace(modelRoot, modelRoot, out Bounds shipBounds))
        {
            return false;
        }

        GameObject prefab = LoadImportedKorshunCombatEquipmentPrefab(equipmentModelId);
        if (prefab == null)
        {
            return false;
        }

        int normalizedSideSign = sideSign < 0 ? -1 : 1;
        GameObject module = Instantiate(prefab, modelRoot);
        module.name = string.IsNullOrWhiteSpace(instanceName) ? equipmentModelId + "_BarbetCombatSmall" : instanceName;
        module.transform.localPosition = Vector3.zero;
        module.transform.localRotation = Quaternion.Euler(0f, normalizedSideSign < 0 ? -90f : 90f, 0f);
        module.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        DisableImportedVisualPhysics(module);
        module.SetActive(true);

        if (!TryCalculateRendererBoundsInSpace(module.transform, modelRoot, out Bounds moduleBounds))
        {
            DestroyCombatLoadoutObject(module);
            return false;
        }

        Vector3 targetAnchor = new Vector3(
            shipBounds.center.x + shipBounds.extents.x * 0.72f * normalizedSideSign,
            shipBounds.center.y + shipBounds.extents.y * 0.34f,
            shipBounds.center.z - shipBounds.extents.z * 0.06f);
        Vector3 moduleAnchor = new Vector3(moduleBounds.center.x, moduleBounds.min.y, moduleBounds.center.z);
        module.transform.localPosition += targetAnchor - moduleAnchor;
        return true;
    }

    private static bool CreateImportedBarbetCombatHarpoonPlaceholder(
        Transform modelRoot,
        string instanceName,
        int sideSign,
        float scale)
    {
        if (modelRoot == null)
        {
            return false;
        }

        if (!TryCalculateRendererBoundsInSpace(modelRoot, modelRoot, out Bounds shipBounds))
        {
            return false;
        }

        int normalizedSideSign = sideSign < 0 ? -1 : 1;
        GameObject module = CreateHarpoonPlaceholderObject(instanceName, normalizedSideSign < 0 ? -90f : 90f, scale);
        module.transform.SetParent(modelRoot, false);
        module.transform.localPosition = Vector3.zero;

        if (!TryCalculateRendererBoundsInSpace(module.transform, modelRoot, out Bounds moduleBounds))
        {
            DestroyCombatLoadoutObject(module);
            return false;
        }

        Vector3 targetAnchor = new Vector3(
            shipBounds.center.x + shipBounds.extents.x * 0.72f * normalizedSideSign,
            shipBounds.center.y + shipBounds.extents.y * 0.34f,
            shipBounds.center.z - shipBounds.extents.z * 0.06f);
        Vector3 moduleAnchor = new Vector3(moduleBounds.center.x, moduleBounds.min.y, moduleBounds.center.z);
        module.transform.localPosition += targetAnchor - moduleAnchor;
        return true;
    }

    private static GameObject CreateHarpoonPlaceholderObject(string instanceName, float yawDegrees, float scale)
    {
        GameObject module = new GameObject(string.IsNullOrWhiteSpace(instanceName) ? "Combat_HarpoonCannon" : instanceName);
        module.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        module.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

        Material darkMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(0.14f, 0.22f, 0.24f, 1f), 1.45f);
        Material boltMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(0.54f, 0.84f, 0.94f, 1f), 1.95f);

        GameObject carriage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carriage.name = "Harpoon Carriage";
        carriage.transform.SetParent(module.transform, false);
        carriage.transform.localScale = new Vector3(5.2f, 2.8f, 6.8f);
        AssignCombatPlaceholderMaterial(carriage, darkMaterial);

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Harpoon Pressure Barrel";
        barrel.transform.SetParent(module.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 1.65f, 4.4f);
        barrel.transform.localScale = new Vector3(1.65f, 1.45f, 11.5f);
        AssignCombatPlaceholderMaterial(barrel, darkMaterial);

        GameObject cableBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cableBox.name = "Harpoon Cable Drum";
        cableBox.transform.SetParent(module.transform, false);
        cableBox.transform.localPosition = new Vector3(0f, 1.25f, -2.6f);
        cableBox.transform.localScale = new Vector3(4.4f, 2.0f, 2.3f);
        AssignCombatPlaceholderMaterial(cableBox, darkMaterial);

        GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bolt.name = "Loaded Harpoon Bolt";
        bolt.transform.SetParent(module.transform, false);
        bolt.transform.localPosition = new Vector3(0f, 2.05f, 10.1f);
        bolt.transform.localScale = new Vector3(0.62f, 0.62f, 5.4f);
        AssignCombatPlaceholderMaterial(bolt, boltMaterial);

        DisableImportedVisualPhysics(module);
        module.SetActive(true);
        return module;
    }

    private static void AssignCombatPlaceholderMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static GameObject LoadImportedKorshunCombatEquipmentPrefab(string equipmentModelId)
    {
#if UNITY_EDITOR
        string normalizedModelId = string.IsNullOrWhiteSpace(equipmentModelId) ? "" : equipmentModelId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModelId))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipImports/Models/Turrets/" + normalizedModelId + ".fbx");
#else
        return null;
#endif
    }

    private static Transform FindDescendantByExactName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            string childName = child != null ? child.name ?? "" : "";
            if (child != null
                && (string.Equals(childName, name, System.StringComparison.OrdinalIgnoreCase)
                    || childName.StartsWith(name + ".", System.StringComparison.OrdinalIgnoreCase)))
            {
                return child;
            }
        }

        return null;
    }

    private static void SetDescendantActiveByNameContains(Transform root, string token, bool active)
    {
        if (root == null || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
            {
                continue;
            }

            if (child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                child.gameObject.SetActive(active);
            }
        }
    }

    private static void DestroyDescendantsByNameContains(Transform root, string token)
    {
        if (root == null || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            Transform child = children[i];
            if (child == null || child == root)
            {
                continue;
            }

            if (child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DestroyCombatLoadoutObject(child.gameObject);
            }
        }
    }

    private static void DestroyCombatLoadoutObject(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private static bool TryCalculateRendererBoundsInSpace(Transform root, Transform reference, out Bounds bounds)
    {
        bounds = default;
        if (root == null || reference == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 worldCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localCorner = reference.InverseTransformPoint(worldCorner);
                        if (!found)
                        {
                            bounds = new Bounds(localCorner, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            bounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        return found;
    }

    private static void ConfigureRuntimeLoadout(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        SessionConfigDatabase config = null)
    {
        if (ship == null)
        {
            return;
        }

        ConfigureRuntimeDamageProfile(ship, profile);

        if (config != null
            && HasConfiguredPackageProfile(profile)
            && HasResolvedRuntimePackage(profile, config)
            && !HasUnresolvedPrimaryWeaponSelection(profile, config))
        {
            ConfigurePackageRuntimeLoadout(ship, profile, target, targetTeam, config);
            return;
        }

        ConfigureLegacyRuntimeLoadout(ship, profile, target, targetTeam);
    }

    private static void ConfigureRuntimeDamageProfile(CoreTacticalShipMotor ship, RuntimeShipProfile profile)
    {
        if (ship == null)
        {
            return;
        }

        CoreTacticalPrototypeHealth health = GetOrAddComponent<CoreTacticalPrototypeHealth>(ship);
        if (health != null)
        {
            health.maxHealth = Mathf.Max(1f, profile.structureHp);
        }

        CoreTacticalDamageProfile damageProfile = GetOrAddComponent<CoreTacticalDamageProfile>(ship);
        if (damageProfile != null)
        {
            damageProfile.ConfigureDefense(
                profile.classId,
                health != null ? health.maxHealth : Mathf.Max(1f, profile.structureHp),
                profile.resistances,
                profile.citadelHp,
                profile.powerPlantModuleHp);
        }

        if (health != null)
        {
            health.ResetHealth();
        }
    }

    private static void ConfigureLegacyRuntimeLoadout(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam)
    {
        switch (profile.loadout)
        {
            case RuntimeLoadoutKind.None:
                break;
            case RuntimeLoadoutKind.BattleshipFull:
                CoreTacticalPrototypeBootstrap.ConfigureBattleshipFullLoadout(ship, target, targetTeam);
                break;
            case RuntimeLoadoutKind.ArtilleryCruiser:
                CoreTacticalPrototypeBootstrap.ConfigureCruiserArtilleryLoadout(ship, target, targetTeam);
                break;
            default:
                CoreTacticalPrototypeBootstrap.ConfigureFrigateAutocannonLoadout(ship, target, targetTeam);
                CoreTacticalFrigateAutocannonBattery battery = ship.GetComponent<CoreTacticalFrigateAutocannonBattery>();
                if (battery != null)
                {
                    battery.maxRangeMeters = 4700f;
                    battery.targetAwarenessRangeMeters = 6500f;
                }

                break;
        }
    }

    private static bool HasConfiguredPackageProfile(RuntimeShipProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.mainPackageId)
            || !string.IsNullOrWhiteSpace(profile.secondaryPackageId)
            || !string.IsNullOrWhiteSpace(profile.smallPackageId)
            || !string.IsNullOrWhiteSpace(profile.auxiliaryPackageId);
    }

    private static bool HasResolvedRuntimePackage(RuntimeShipProfile profile, SessionConfigDatabase config)
    {
        if (config == null)
        {
            return false;
        }

        return config.GetKorshunWeaponPackage(profile.mainPackageId) != null
            || config.GetKorshunWeaponPackage(profile.secondaryPackageId) != null
            || config.GetKorshunWeaponPackage(profile.smallPackageId) != null
            || config.GetKorshunAuxiliaryPackage(profile.auxiliaryPackageId) != null
            || config.GetKorshunAuxiliaryPackage(profile.smallPackageId) != null;
    }

    private static bool HasUnresolvedPrimaryWeaponSelection(RuntimeShipProfile profile, SessionConfigDatabase config)
    {
        if (config == null)
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(profile.mainPackageId) && config.GetKorshunWeaponPackage(profile.mainPackageId) == null)
            || (!string.IsNullOrWhiteSpace(profile.secondaryPackageId) && config.GetKorshunWeaponPackage(profile.secondaryPackageId) == null);
    }

    private static void ConfigurePackageRuntimeLoadout(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        SessionConfigDatabase config)
    {
        CoreTacticalWeaponControl weaponControl = EnsureWeaponControl(ship, profile);
        CoreTacticalPriorityTargetControl priority = GetOrAddComponent<CoreTacticalPriorityTargetControl>(ship);
        if (priority != null && target != null)
        {
            priority.SetPriorityTarget(target);
        }

        CoreTacticalWeaponTargetCoordinator coordinator = GetOrAddComponent<CoreTacticalWeaponTargetCoordinator>(ship);
        if (coordinator != null)
        {
            coordinator.owner = ship;
            coordinator.targetTeam = targetTeam;
            coordinator.maxTargetRangeMeters = 24000f;
        }

        float maxRange = 0f;
        CoreTacticalBalanceConfig tacticalBalance = config != null ? config.coreTacticalBalance : null;
        ConfigureWeaponPackage(ship, profile, target, targetTeam, config.GetKorshunWeaponPackage(profile.mainPackageId), "main", weaponControl, tacticalBalance, ref maxRange);
        ConfigureWeaponPackage(ship, profile, target, targetTeam, config.GetKorshunWeaponPackage(profile.secondaryPackageId), "secondary", weaponControl, tacticalBalance, ref maxRange);
        ConfigureWeaponPackage(ship, profile, target, targetTeam, config.GetKorshunWeaponPackage(profile.smallPackageId), "small", weaponControl, tacticalBalance, ref maxRange);
        ConfigureAuxiliaryPackage(ship, profile, target, targetTeam, config.GetKorshunAuxiliaryPackage(profile.auxiliaryPackageId), weaponControl, tacticalBalance, ref maxRange);
        ConfigureAuxiliaryPackage(ship, profile, target, targetTeam, config.GetKorshunAuxiliaryPackage(profile.smallPackageId), weaponControl, tacticalBalance, ref maxRange);

        if (coordinator != null && maxRange > 0f)
        {
            coordinator.maxTargetRangeMeters = Mathf.Max(2400f, maxRange * 1.35f);
        }
    }

    private static CoreTacticalWeaponControl EnsureWeaponControl(CoreTacticalShipMotor ship, RuntimeShipProfile profile)
    {
        CoreTacticalWeaponControl weaponControl = GetOrAddComponent<CoreTacticalWeaponControl>(ship);
        if (weaponControl == null)
        {
            return null;
        }

        weaponControl.ClearRuntimeWeaponGroups();
        return weaponControl;
    }

    private static void ConfigureWeaponPackage(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        KorshunWeaponPackageConfig package,
        string role,
        CoreTacticalWeaponControl weaponControl,
        CoreTacticalBalanceConfig tacticalBalance,
        ref float maxRange)
    {
        if (ship == null || package == null)
        {
            return;
        }

        float range = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, profile.weaponRangeMultiplier));
        float reloadSeconds = ResolveRuntimeReloadSeconds(GetPackageReloadSeconds(package), profile);
        float dispersionAtMaxRange = package.dispersionMPerKm * range * 0.001f * Mathf.Max(0.01f, profile.dispersionMultiplier);
        CoreTacticalProjectileDetonationMode detonationMode = GetPackageDetonationMode(package);
        float explosiveSplashRadiusMeters = ResolvePackageExplosiveSplashRadiusMeters(package, tacticalBalance);
        maxRange = Mathf.Max(maxRange, range);

        if (package.isMachinegunAura)
        {
            CoreTacticalMachineGunMountBattery aura = GetOrAddComponent<CoreTacticalMachineGunMountBattery>(ship);
            if (aura != null)
            {
                aura.owner = ship;
                aura.target = target;
                aura.targetTeam = targetTeam;
                aura.radiusMeters = range;
                aura.damagePerMountTick = Mathf.Max(0.01f, package.machinegunDamagePerSecond * aura.damageTickIntervalSeconds / 12f);
                aura.machineGunResistanceIgnorePercent = ResolveMachineGunResistanceIgnorePercent(package);
                aura.tracerRatePerSecondPerMount = Mathf.Clamp(package.machinegunDamagePerSecond * 0.16f, 12f, 60f);
            }

            weaponControl?.SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup.MachineGuns);
            weaponControl?.SetRuntimeGroupPresentation(CoreTacticalWeaponGroup.MachineGuns, package.DisplayNameRu, "MG");
            return;
        }

        if (IsRocketLikeWeapon(package))
        {
            ConfigureMissilePackage(
                ship,
                target,
                targetTeam,
                package.localNameRu,
                range,
                package.damage,
                package.damageType,
                package.resistanceIgnorePercent,
                ResolvePackageCaliberMm(package),
                GetPackageFireChancePercent(package, tacticalBalance),
                package.projectileSpeedMS,
                reloadSeconds,
                explosiveSplashRadiusMeters,
                package.barrelsOrProjectiles,
                false,
                "main_rocket",
                2,
                weaponControl,
                profile);
            return;
        }

        bool mainCapitalGun = string.Equals(role, "main", System.StringComparison.OrdinalIgnoreCase)
            && (profile.loadout == RuntimeLoadoutKind.ArtilleryCruiser || profile.loadout == RuntimeLoadoutKind.BattleshipFull);
        if (mainCapitalGun)
        {
            CoreTacticalMainBattery battery = GetOrAddComponent<CoreTacticalMainBattery>(ship);
            if (battery != null)
            {
                battery.owner = ship;
                battery.target = target;
                battery.targetTeam = targetTeam;
                battery.maxRangeMeters = range;
                battery.targetAwarenessRangeMeters = Mathf.Max(range * 1.25f, range + 1000f);
                battery.reloadSeconds = Mathf.Max(0.4f, reloadSeconds);
                battery.muzzleVelocityMS = Mathf.Max(1f, package.projectileSpeedMS);
                battery.burstRadiusMeters = Mathf.Max(0.01f, explosiveSplashRadiusMeters);
                battery.dispersionAtMaxRangeMeters = Mathf.Max(0f, dispersionAtMaxRange);
                battery.shellDamage = Mathf.Max(1f, package.damage);
                battery.shellDamageType = package.damageType;
                battery.shellResistanceIgnorePercent = Mathf.Max(0f, package.resistanceIgnorePercent);
                battery.shellCaliberMm = ResolvePackageCaliberMm(package);
                battery.shellDirectImpactFuseThresholdMeters = GetPackageDirectImpactFuseThresholdMeters(package);
                battery.shellFireChancePercent = GetPackageFireChancePercent(package, tacticalBalance);
                battery.shellVisualScale = Mathf.Clamp(package.resistanceIgnorePercent * 0.045f, 3.2f, 13f);
                battery.detonationMode = detonationMode;
            }

            weaponControl?.SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup.MainBattery);
            weaponControl?.SetRuntimeGroupPresentation(CoreTacticalWeaponGroup.MainBattery, package.DisplayNameRu, BuildWeaponPackageIcon(package));
            return;
        }

        if (string.Equals(role, "secondary", System.StringComparison.OrdinalIgnoreCase))
        {
            CoreTacticalSecondaryMountBattery battery = GetOrAddComponent<CoreTacticalSecondaryMountBattery>(ship);
            if (battery != null)
            {
                bool heavySecondary = package.resistanceIgnorePercent >= 100f || package.damage >= 320f;
                battery.owner = ship;
                battery.target = target;
                battery.targetTeam = targetTeam;
                battery.maxRangeMeters = range;
                battery.targetAwarenessRangeMeters = Mathf.Max(range * 1.30f, range + 900f);
                battery.dispersionAtMaxRangeMeters = Mathf.Max(0f, dispersionAtMaxRange);
                battery.detonationMode = detonationMode;
                battery.include76mmMounts = !heavySecondary;
                battery.include152mmMounts = heavySecondary;
                if (heavySecondary)
                {
                    battery.reloadSeconds152mm = Mathf.Max(0.4f, reloadSeconds);
                    battery.muzzleVelocity152mmMS = Mathf.Max(1f, package.projectileSpeedMS);
                    battery.shellDamage152mm = Mathf.Max(1f, package.damage);
                    battery.shellDamageType152mm = package.damageType;
                    battery.shellResistanceIgnore152mm = Mathf.Max(0f, package.resistanceIgnorePercent);
                    battery.shellCaliber152mm = ResolvePackageCaliberMm(package);
                    battery.shellDirectImpactFuseThreshold152mm = GetPackageDirectImpactFuseThresholdMeters(package);
                    battery.shellFireChance152mm = GetPackageFireChancePercent(package, tacticalBalance);
                    battery.burstRadius152mmMeters = Mathf.Max(0.01f, explosiveSplashRadiusMeters);
                }
                else
                {
                    battery.reloadSeconds76mm = Mathf.Max(0.4f, reloadSeconds);
                    battery.muzzleVelocity76mmMS = Mathf.Max(1f, package.projectileSpeedMS);
                    battery.shellDamage76mm = Mathf.Max(1f, package.damage);
                    battery.shellDamageType76mm = package.damageType;
                    battery.shellResistanceIgnore76mm = Mathf.Max(0f, package.resistanceIgnorePercent);
                    battery.shellCaliber76mm = ResolvePackageCaliberMm(package);
                    battery.shellDirectImpactFuseThreshold76mm = GetPackageDirectImpactFuseThresholdMeters(package);
                    battery.shellFireChance76mm = GetPackageFireChancePercent(package, tacticalBalance);
                    battery.burstRadius76mmMeters = Mathf.Max(0.01f, explosiveSplashRadiusMeters);
                }
            }

            CoreTacticalWeaponGroup weaponGroup = package.resistanceIgnorePercent >= 100f || package.damage >= 320f
                ? CoreTacticalWeaponGroup.Secondary152mm
                : CoreTacticalWeaponGroup.Secondary76mm;
            weaponControl?.SetRuntimeWeaponGroupActive(weaponGroup);
            weaponControl?.SetRuntimeGroupPresentation(weaponGroup, package.DisplayNameRu, BuildWeaponPackageIcon(package));
            return;
        }

        CoreTacticalFrigateAutocannonBattery autocannon = GetOrAddComponent<CoreTacticalFrigateAutocannonBattery>(ship);
        if (autocannon != null)
        {
            autocannon.owner = ship;
            autocannon.target = target;
            autocannon.targetTeam = targetTeam;
            autocannon.maxRangeMeters = range;
            autocannon.targetAwarenessRangeMeters = Mathf.Max(range * 1.35f, range + 900f);
            autocannon.reloadSeconds = Mathf.Max(0.05f, reloadSeconds);
            autocannon.barrelsPerMount = Mathf.Max(1, package.barrelsOrProjectiles);
            autocannon.barrelShotSpacingSeconds = 0.08f;
            autocannon.barrelSpacingMeters = Mathf.Clamp(ResolvePackageCaliberMm(package) * 0.018f, 0.55f, 1.8f);
            autocannon.muzzleVelocityMS = Mathf.Max(1f, package.projectileSpeedMS);
            autocannon.burstRadiusMeters = Mathf.Max(0.01f, explosiveSplashRadiusMeters);
            autocannon.dispersionAtMaxRangeMeters = Mathf.Max(0f, dispersionAtMaxRange);
            autocannon.shellDamage = Mathf.Max(0.1f, package.damage);
            autocannon.shellDamageType = package.damageType;
            autocannon.shellResistanceIgnorePercent = Mathf.Max(0f, package.resistanceIgnorePercent);
            autocannon.shellCaliberMm = ResolvePackageCaliberMm(package);
            autocannon.shellDirectImpactFuseThresholdMeters = GetPackageDirectImpactFuseThresholdMeters(package);
            autocannon.shellFireChancePercent = GetPackageFireChancePercent(package, tacticalBalance);
            autocannon.shellVisualScale = Mathf.Clamp(package.resistanceIgnorePercent * 0.018f, 0.6f, 3.4f);
            autocannon.detonationMode = detonationMode;
        }

        weaponControl?.SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup.Autocannon30mm);
        weaponControl?.SetRuntimeGroupPresentation(CoreTacticalWeaponGroup.Autocannon30mm, package.DisplayNameRu, BuildWeaponPackageIcon(package));
    }

    private static void ConfigureAuxiliaryPackage(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        KorshunAuxiliaryPackageConfig package,
        CoreTacticalWeaponControl weaponControl,
        CoreTacticalBalanceConfig tacticalBalance,
        ref float maxRange)
    {
        if (ship == null || package == null)
        {
            return;
        }

        string kind = NormalizeKey(package.kind);
        if (kind == "torpedo" || kind == "rocket_salvo")
        {
            float range = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, profile.weaponRangeMultiplier));
            float reloadSeconds = ResolveRuntimeReloadSeconds(Mathf.Max(1f, package.reloadSeconds), profile);
            maxRange = Mathf.Max(maxRange, range);
            ConfigureMissilePackage(
                ship,
                target,
                targetTeam,
                package.localNameRu,
                range,
                package.damage,
                CoreTacticalDamageType.Explosive,
                ResolveAuxiliaryExplosiveResistanceIgnorePercent(package, kind == "torpedo"),
                kind == "torpedo" ? 533f : 200f,
                kind == "torpedo" ? 0f : 12f,
                package.projectileSpeedMS,
                reloadSeconds,
                ResolveAuxiliaryExplosiveSplashRadiusMeters(package, kind == "torpedo", tacticalBalance),
                package.projectilesPerSalvo,
                kind == "torpedo",
                kind == "torpedo" ? "torpedo" : "side_rocket",
                2,
                weaponControl,
                profile);
            return;
        }

        if (kind == "harpoon")
        {
            float range = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, profile.weaponRangeMultiplier));
            float reloadSeconds = ResolveRuntimeReloadSeconds(Mathf.Max(1f, package.reloadSeconds), profile);
            maxRange = Mathf.Max(maxRange, range);
            ConfigureHarpoonPackage(ship, package, range, reloadSeconds, weaponControl);
            return;
        }

        if (kind == "magnet" || kind == "salvage_magnet")
        {
            maxRange = Mathf.Max(maxRange, Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, profile.weaponRangeMultiplier)));
            return;
        }

        if (kind == "repair_beam" || kind == "scanner_hacker")
        {
            float range = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, profile.weaponRangeMultiplier));
            CoreTacticalAuxiliaryBeamEmitter emitter = ship.gameObject.AddComponent<CoreTacticalAuxiliaryBeamEmitter>();
            emitter.Configure(
                ship,
                target,
                targetTeam,
                ResolveAuxiliaryBeamPalette(kind),
                range,
                package.cycleSeconds,
                package.cooldownSeconds,
                package.repairHpPerCycle);
            maxRange = Mathf.Max(maxRange, range);
        }
    }

    private static void ConfigureHarpoonPackage(
        CoreTacticalShipMotor ship,
        KorshunAuxiliaryPackageConfig package,
        float range,
        float reloadSeconds,
        CoreTacticalWeaponControl weaponControl)
    {
        if (ship == null || package == null)
        {
            return;
        }

        float towForceKg = Mathf.Max(1f, package.energyCost > 0f ? package.energyCost : 280f);
        int launcherCount = 2;
        for (int i = 0; i < launcherCount; i++)
        {
            CoreTacticalHarpoonLauncher launcher = ship.gameObject.AddComponent<CoreTacticalHarpoonLauncher>();
            launcher.weaponGroup = CoreTacticalWeaponGroup.Torpedoes;
            launcher.visualLauncherRole = "harpoon";
            launcher.Configure(
                ship,
                i == 0 ? -1 : 1,
                package.DisplayNameRu,
                range,
                Mathf.Max(1f, package.damage),
                Mathf.Max(1f, package.projectileSpeedMS),
                reloadSeconds,
                towForceKg);
        }

        weaponControl?.SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup.Torpedoes);
        weaponControl?.SetRuntimeGroupPresentation(CoreTacticalWeaponGroup.Torpedoes, package.DisplayNameRu, "HRP");
    }

    private static CoreTacticalUtilityBeamPalette ResolveAuxiliaryBeamPalette(string kind)
    {
        switch (NormalizeKey(kind))
        {
            case "magnet":
                return CoreTacticalUtilityBeamPalette.Magnet;
            case "salvage_magnet":
                return CoreTacticalUtilityBeamPalette.Salvage;
            case "scanner_hacker":
                return CoreTacticalUtilityBeamPalette.Scanner;
            default:
                return CoreTacticalUtilityBeamPalette.Repair;
        }
    }

    private static void ConfigureMissilePackage(
        CoreTacticalShipMotor ship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        string displayName,
        float range,
        float damage,
        CoreTacticalDamageType damageType,
        float resistanceIgnorePercent,
        float caliberMm,
        float fireChancePercent,
        float projectileSpeedMS,
        float reloadSeconds,
        float splashRadius,
        int projectilesPerSalvo,
        bool torpedoLike,
        string visualLauncherRole,
        int requestedLauncherCount,
        CoreTacticalWeaponControl weaponControl,
        RuntimeShipProfile profile)
    {
        CoreTacticalWeaponGroup weaponGroup = torpedoLike ? CoreTacticalWeaponGroup.Torpedoes : CoreTacticalWeaponGroup.Missiles;
        int launcherCount = Mathf.Clamp(requestedLauncherCount, 1, 4);
        int projectilesPerLauncher = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, projectilesPerSalvo) / (float)launcherCount));
        bool rocketSalvoLike = !torpedoLike;
        for (int i = 0; i < launcherCount; i++)
        {
            CoreTacticalMissileLauncher launcher = ship.gameObject.AddComponent<CoreTacticalMissileLauncher>();
            launcher.owner = ship;
            launcher.target = target;
            launcher.targetTeam = targetTeam;
            launcher.sideSign = i == 0 ? -1 : 1;
            launcher.visualLauncherRole = visualLauncherRole;
            launcher.missileName = string.IsNullOrWhiteSpace(displayName) ? "Core Tactical Missile" : displayName;
            launcher.guidanceMode = CoreTacticalMissileGuidanceMode.DirectChase;
            launcher.weaponGroup = weaponGroup;
            launcher.launchIntervalSeconds = rocketSalvoLike
                ? Mathf.Max(0.2f, reloadSeconds)
                : Mathf.Max(0.2f, reloadSeconds / Mathf.Max(1, projectilesPerLauncher));
            launcher.manualLaunchOnly = torpedoLike;
            launcher.projectilesPerManualSalvo = projectilesPerLauncher;
            launcher.automaticBurstProjectileCount = rocketSalvoLike ? projectilesPerLauncher : 1;
            launcher.automaticBurstShotIntervalSeconds = rocketSalvoLike ? 0.12f : 0.08f;
            launcher.automaticBurstSpreadDegrees = rocketSalvoLike ? Mathf.Clamp(projectilesPerLauncher * 0.42f, 5f, 11f) : 0f;
            launcher.automaticBurstUsesChaoticCloud = rocketSalvoLike;
            launcher.automaticBurstCloudScatterDegrees = rocketSalvoLike ? Mathf.Clamp(projectilesPerLauncher * 0.55f, 6f, 13f) : 0f;
            launcher.automaticBurstCloudForwardJitterDegrees = rocketSalvoLike ? 3.5f : 0f;
            launcher.automaticBurstShotIntervalJitterSeconds = rocketSalvoLike ? 0.05f : 0f;
            launcher.automaticBurstChaosAmplitudeDegrees = rocketSalvoLike ? 14f : 0f;
            launcher.automaticBurstChaosFrequencyHz = rocketSalvoLike ? 2.45f : 0f;
            launcher.manualFanAngleDegrees = torpedoLike ? 15f : 0f;
            launcher.manualAimSectorDegrees = torpedoLike ? 120f : 360f;
            launcher.manualCooldownSeconds = Mathf.Max(0.2f, reloadSeconds);
            launcher.missileSpeedMS = Mathf.Max(1f, projectileSpeedMS);
            launcher.missileLifetimeSeconds = Mathf.Max(3f, range / Mathf.Max(1f, launcher.missileSpeedMS));
            launcher.explosionRadiusMeters = Mathf.Max(0.1f, splashRadius);
            launcher.proximityRadiusMeters = Mathf.Max(3f, launcher.explosionRadiusMeters);
            launcher.missileDamage = Mathf.Max(1f, damage);
            launcher.missileDamageType = damageType;
            launcher.missileResistanceIgnorePercent = Mathf.Max(0f, resistanceIgnorePercent);
            launcher.missileCaliberMm = Mathf.Max(0f, caliberMm);
            launcher.missileFireChancePercent = Mathf.Max(0f, fireChancePercent);
            launcher.missileTurnRateDegPerSecond = 0f;
            launcher.fullDamageInsideExplosionRadius = torpedoLike;
            launcher.missileColor = torpedoLike ? new Color(0.70f, 0.94f, 1f, 1f) : new Color(1f, 0.46f, 0.12f, 1f);
            launcher.trailColor = torpedoLike ? new Color(0.32f, 0.74f, 1f, 0.86f) : new Color(1f, 0.62f, 0.20f, 0.92f);
        }

        weaponControl?.SetRuntimeWeaponGroupActive(weaponGroup);
        weaponControl?.SetRuntimeGroupPresentation(weaponGroup, displayName, torpedoLike ? "TRP" : "MSL");
    }

    private static float ResolveSmallRocketSplashRadiusMeters(float damage)
    {
        return Mathf.Clamp(
            Mathf.Max(0f, damage) * SmallRocketSplashDamageToRadius,
            SmallRocketSplashRadiusMinMeters,
            SmallRocketSplashRadiusMaxMeters);
    }

    private static float ResolvePackageExplosiveSplashRadiusMeters(KorshunWeaponPackageConfig package, CoreTacticalBalanceConfig tacticalBalance)
    {
        if (package == null)
        {
            return 0f;
        }

        return ResolveExplosiveSplashRadiusMeters(package.explosiveKg, package.splashRadiusM, tacticalBalance);
    }

    private static float ResolveAuxiliaryExplosiveSplashRadiusMeters(KorshunAuxiliaryPackageConfig package, bool torpedoLike, CoreTacticalBalanceConfig tacticalBalance)
    {
        if (package == null)
        {
            return torpedoLike ? 30f : SmallRocketSplashRadiusMinMeters;
        }

        float fallbackRadius = torpedoLike ? 30f : ResolveSmallRocketSplashRadiusMeters(package.damage);
        return ResolveExplosiveSplashRadiusMeters(package.explosiveKg, fallbackRadius, tacticalBalance);
    }

    private static float ResolveExplosiveSplashRadiusMeters(float explosiveKg, float fallbackRadiusMeters, CoreTacticalBalanceConfig tacticalBalance)
    {
        if (explosiveKg <= 0.001f)
        {
            return Mathf.Max(0f, fallbackRadiusMeters);
        }

        float referenceMassKg = tacticalBalance != null
            ? tacticalBalance.explosiveRadiusReferenceMassKg
            : ExplosiveRadiusReferenceMassKg;
        float referenceRadiusMeters = tacticalBalance != null
            ? tacticalBalance.explosiveRadiusReferenceMeters
            : ExplosiveRadiusReferenceMeters;
        float massExponent = tacticalBalance != null
            ? tacticalBalance.explosiveRadiusMassExponent
            : ExplosiveRadiusMassExponent;
        float massRatio = Mathf.Max(0f, explosiveKg) / Mathf.Max(0.001f, referenceMassKg);
        return Mathf.Max(0f, Mathf.Max(0f, referenceRadiusMeters) * Mathf.Pow(massRatio, Mathf.Max(0.001f, massExponent)));
    }

    private static string BuildWeaponPackageIcon(KorshunWeaponPackageConfig package)
    {
        if (package == null)
        {
            return "";
        }

        string id = NormalizeKey(package.id);
        if (id.Contains("nurs") || id.Contains("rocket"))
        {
            return "MSL";
        }

        string caliber = ExtractMmIcon(id);
        if (!string.IsNullOrWhiteSpace(caliber))
        {
            return caliber;
        }

        return package.resistanceIgnorePercent > 0f
            ? Mathf.RoundToInt(package.resistanceIgnorePercent).ToString()
            : "";
    }

    private static CoreTacticalProjectileDetonationMode GetPackageDetonationMode(KorshunWeaponPackageConfig package)
    {
        string shellType = package != null ? NormalizeKey(package.shellType) : "";
        return shellType == "ap" || shellType == "kinetic_direct"
            ? CoreTacticalProjectileDetonationMode.DirectImpact
            : CoreTacticalProjectileDetonationMode.AirBurst;
    }

    private static float ResolvePackageCaliberMm(KorshunWeaponPackageConfig package)
    {
        if (package == null)
        {
            return 0f;
        }

        string caliberText = ExtractMmIcon(NormalizeKey(package.id));
        if (!string.IsNullOrWhiteSpace(caliberText) && float.TryParse(caliberText, out float caliber))
        {
            return Mathf.Max(0f, caliber);
        }

        return Mathf.Max(0f, package.resistanceIgnorePercent);
    }

    private static float GetPackageDirectImpactFuseThresholdMeters(KorshunWeaponPackageConfig package)
    {
        if (package == null || GetPackageDetonationMode(package) != CoreTacticalProjectileDetonationMode.DirectImpact)
        {
            return 0f;
        }

        return Mathf.Max(2f, ResolvePackageCaliberMm(package) * 0.075f);
    }

    private static float GetPackageFireChancePercent(KorshunWeaponPackageConfig package, CoreTacticalBalanceConfig tacticalBalance)
    {
        if (package == null || GetPackageDetonationMode(package) != CoreTacticalProjectileDetonationMode.AirBurst)
        {
            return 0f;
        }

        return Mathf.Clamp(8f + Mathf.Sqrt(Mathf.Max(0f, ResolvePackageExplosiveSplashRadiusMeters(package, tacticalBalance))) * 1.8f, 8f, 22f);
    }

    private static float ResolveMachineGunResistanceIgnorePercent(KorshunWeaponPackageConfig package)
    {
        float caliber = ResolvePackageCaliberMm(package);
        if (caliber <= 0.001f)
        {
            caliber = 37f;
        }

        return Mathf.Clamp(caliber * 0.27f, 4f, 18f);
    }

    private static float ResolveAuxiliaryExplosiveResistanceIgnorePercent(KorshunAuxiliaryPackageConfig package, bool torpedoLike)
    {
        if (package == null)
        {
            return torpedoLike ? 180f : 45f;
        }

        return torpedoLike
            ? Mathf.Clamp(package.damage * 0.10f, 120f, 320f)
            : Mathf.Clamp(package.damage * 0.08f, 30f, 180f);
    }

    private static string ExtractMmIcon(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        int mmIndex = value.IndexOf("mm", System.StringComparison.OrdinalIgnoreCase);
        if (mmIndex <= 0)
        {
            return "";
        }

        int start = mmIndex - 1;
        while (start >= 0 && char.IsDigit(value[start]))
        {
            start--;
        }

        start++;
        return start < mmIndex ? value.Substring(start, mmIndex - start) : "";
    }

    private static bool IsRocketLikeWeapon(KorshunWeaponPackageConfig package)
    {
        if (package == null)
        {
            return false;
        }

        string id = NormalizeKey(package.id);
        string name = NormalizeKey(package.localNameRu);
        return id.Contains("nurs") || id.Contains("rocket") || name.Contains("РЅСѓСЂСЃ") || name.Contains("СЂР°Рє");
    }

    private static float GetPackageReloadSeconds(KorshunWeaponPackageConfig package)
    {
        if (package == null)
        {
            return 1f;
        }

        if (package.reloadSeconds > 0f)
        {
            return package.reloadSeconds;
        }

        if (package.shotsPerMinute > 0f)
        {
            return 60f / Mathf.Max(0.01f, package.shotsPerMinute);
        }

        return 1f;
    }

    private static float ResolveRuntimeReloadSeconds(float baseReloadSeconds, RuntimeShipProfile profile)
    {
        float hullReloadScalar = Mathf.Max(0.01f, profile.reloadMultiplier);
        float citadelRate = Mathf.Max(0.01f, profile.reloadRateMultiplier);
        return Mathf.Max(0.01f, Mathf.Max(0.01f, baseReloadSeconds) * hullReloadScalar / citadelRate);
    }

    private static T GetOrAddComponent<T>(CoreTacticalShipMotor ship) where T : Component
    {
        if (ship == null)
        {
            return null;
        }

        T component = ship.GetComponent<T>();
        return component != null ? component : ship.gameObject.AddComponent<T>();
    }

    private void ConfigurePlayerSlipDrive(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return;
        }

        playerSlipDrive = ship.gameObject.AddComponent<CoreTacticalClaudianSlipDrive>();
        playerSlipDrive.ship = ship;
        playerSlipDrive.weaponControl = ship.GetComponent<CoreTacticalWeaponControl>();
        playerSlipDrive.minimumEngageSpeedMS = Mathf.Max(0f, ship.maxForwardSpeedMS * 0.8f);
        playerSlipDrive.targetForwardSpeedMS = ClaudianSlipTargetSpeedMS;
        playerSlipDrive.rampUpSeconds = 6f;
        playerSlipDrive.rampDownSeconds = 8.0f;
        playerSlipDrive.overspeedBrakeSeconds = ClaudianSlipOverspeedBrakeSeconds;
        playerSlipDrive.requireOutsideMissionZone = false;
        playerSlipDrive.missionZoneCenter = missionCenter;
        playerSlipDrive.missionZoneRadiusMeters = missionRadiusMeters;
    }

    private void ConfigurePlayerMiningRig(CoreTacticalShipMotor ship, MetaGameState activeMeta)
    {
        if (ship == null)
        {
            return;
        }

        playerMiningRig = GetOrAddComponent<CoreTacticalMiningRig>(ship);
        if (playerMiningRig != null)
        {
            float profileCapacityKg = Mathf.Max(1f, playerRuntimeProfile.cargoCapacityTons * 1000f);
            float startingPayloadKg = 0f;
            float runtimeCapacityKg = profileCapacityKg;
            if (activeMeta != null && activeMeta.progress != null)
            {
                startingPayloadKg = Mathf.Max(0f, activeMeta.progress.GetShipPayloadMassKg(activeMeta.SessionConfig));
                float freeCargoKg = Mathf.Max(0f, activeMeta.GetRemainingShipCargoCapacityKg());
                runtimeCapacityKg = Mathf.Max(profileCapacityKg, startingPayloadKg + freeCargoKg);
            }

            playerMiningRig.Initialize(ship, activeMeta, CoreTacticalCombatTeam.Enemy, runtimeCapacityKg, startingPayloadKg);
            playerMiningRig.SetStartingInventoryRows(BuildStartingInventoryRows(activeMeta));
            ConfigurePlayerMiningModules(playerMiningRig, activeMeta != null ? activeMeta.SessionConfig : null);
        }
    }

    private void ConfigurePlayerMiningModules(CoreTacticalMiningRig rig, SessionConfigDatabase config)
    {
        if (rig == null)
        {
            return;
        }

        bool hasMagnet = false;
        bool hasDrill = false;
        bool hasCrusher = false;
        bool hasSiphon = false;
        bool hasCloudConcentrator = false;
        rig.salvageMagnetInstalled = false;
        ApplyMiningModulePackage(rig, config != null ? config.GetKorshunAuxiliaryPackage(playerRuntimeProfile.auxiliaryPackageId) : null, ref hasMagnet, ref hasDrill, ref hasCrusher, ref hasSiphon, ref hasCloudConcentrator);
        ApplyMiningModulePackage(rig, config != null ? config.GetKorshunAuxiliaryPackage(playerRuntimeProfile.smallPackageId) : null, ref hasMagnet, ref hasDrill, ref hasCrusher, ref hasSiphon, ref hasCloudConcentrator);
        rig.SetInstalledModules(hasMagnet, hasDrill, hasCrusher, hasSiphon, hasCloudConcentrator);
    }

    private void ApplyMiningModulePackage(
        CoreTacticalMiningRig rig,
        KorshunAuxiliaryPackageConfig package,
        ref bool hasMagnet,
        ref bool hasDrill,
        ref bool hasCrusher,
        ref bool hasSiphon,
        ref bool hasCloudConcentrator)
    {
        if (rig == null || package == null)
        {
            return;
        }

        string kind = NormalizeKey(package.kind);
        if (kind == "magnet" || kind == "salvage_magnet")
        {
            hasMagnet = true;
            rig.magnetRangeMeters = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, playerRuntimeProfile.weaponRangeMultiplier));
            if (kind == "salvage_magnet")
            {
                rig.salvageMagnetInstalled = true;
                rig.salvageWreckRangeMeters = Mathf.Min(1000f, Mathf.Max(1f, rig.magnetRangeMeters));
                if (package.cycleSeconds > 0f)
                {
                    rig.salvageWreckCycleSeconds = Mathf.Max(0.1f, package.cycleSeconds);
                }

                if (package.damage > 0f)
                {
                    rig.salvageAccessRatingPercent = package.damage;
                }

                if (package.energyCost > 0f)
                {
                    rig.salvageWreckEnergyPerSecond = package.energyCost / Mathf.Max(0.1f, rig.salvageWreckCycleSeconds);
                }
            }

            if (package.energyCost > 0f && package.cycleSeconds > 0f)
            {
                rig.magnetEnergyPerSecondPerFragment = Mathf.Max(0f, package.energyCost / Mathf.Max(0.1f, package.cycleSeconds));
            }

            return;
        }

        if (kind == "drill" || kind == "drill_laser" || kind == "mining_laser" || kind == "ore_drill")
        {
            hasDrill = true;
            rig.drillRangeMeters = Mathf.Max(1f, package.rangeM * Mathf.Max(0.01f, playerRuntimeProfile.weaponRangeMultiplier));
            if (package.damage > 0f)
            {
                rig.drillDamagePerSecond = package.damage;
            }

            if (package.energyCost > 0f)
            {
                rig.drillEnergyPerSecond = package.cycleSeconds > 0f
                    ? package.energyCost / Mathf.Max(0.1f, package.cycleSeconds)
                    : package.energyCost;
            }

            return;
        }

        if (kind == "crusher" || kind == "ore_crusher")
        {
            hasCrusher = true;
            if (package.cycleSeconds > 0f)
            {
                rig.crusherCycleSeconds = package.cycleSeconds;
            }

            if (package.energyCost > 0f)
            {
                rig.crusherEnergyPerCycle = package.energyCost;
            }

            if (package.damage > 0f)
            {
                rig.crusherRawKgPerCycle = package.damage;
            }

            return;
        }

        if (kind == "siphon")
        {
            hasSiphon = true;
            rig.siphonLitersPerSecond = Mathf.Max(1f, package.damage);
            rig.siphonEnergyPerSecond = Mathf.Max(0f, package.energyCost);
            rig.siphonChannelCount = Mathf.Max(1, package.projectilesPerSalvo);
            return;
        }

        if (kind == "cloud_concentrator")
        {
            hasCloudConcentrator = true;
            rig.cloudConcentratorCycleSeconds = Mathf.Max(0.1f, package.cycleSeconds);
            rig.cloudConcentratorWaterLitersPerCycle = Mathf.Max(0f, package.damage);
            rig.cloudConcentratorEnergyPerCycle = Mathf.Max(0f, package.energyCost);
        }
    }

    private static List<CoreTacticalInventoryRow> BuildStartingInventoryRows(MetaGameState activeMeta)
    {
        List<CoreTacticalInventoryRow> rows = new List<CoreTacticalInventoryRow>();
        if (activeMeta == null || activeMeta.progress == null)
        {
            return rows;
        }

        PlayerProgress progress = activeMeta.progress;
        SessionConfigDatabase config = activeMeta.SessionConfig;
        if (progress.shipCargo != null)
        {
            for (int i = 0; i < progress.shipCargo.Count; i++)
            {
                ResourceStack stack = progress.shipCargo[i];
                if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
                {
                    continue;
                }

                rows.Add(new CoreTacticalInventoryRow
                {
                    displayName = config != null ? config.GetItemNameRu(stack.resourceId) : stack.resourceId,
                    amountKg = config != null ? config.GetItemTransportMassKg(stack.resourceId, stack.amount) : stack.amount
                });
            }
        }

        AddTankInventoryRow(rows, config, progress.shipFuelTank, "Fuel");
        AddTankInventoryRow(rows, config, progress.shipClaudiumTank, "Claudium");
        return rows;
    }

    private static void AddTankInventoryRow(List<CoreTacticalInventoryRow> rows, SessionConfigDatabase config, ShipConsumableTankState tank, string label)
    {
        if (rows == null || tank == null || tank.amountKg <= 0.001f)
        {
            return;
        }

        string itemName = config != null ? config.GetItemNameRu(tank.resourceId) : tank.resourceId;
        rows.Add(new CoreTacticalInventoryRow
        {
            displayName = label + ": " + itemName,
            amountKg = tank.amountKg
        });
    }

    private void SpawnCoreTacticalOreBoulders(Vector3 center, SessionConfigDatabase config, SortieZoneDefinition zone)
    {
        oreBoulders.Clear();
        float floorY = zone != null ? zone.stormFloorY : center.y - 720f;
        CoreTacticalOreBoulderDefinition[] definitions =
        {
            BuildCoreTacticalOreBoulderDefinition(config, "windshale", 200000f, new Vector3(78f, 50f, 66f), 8200f, 26f, 0.05f, 4.6f, 0.05f, 18f, 130f, 0.50f),
            BuildCoreTacticalOreBoulderDefinition(config, "dawnspar", 280000f, new Vector3(92f, 58f, 76f), 11200f, 38f, 0.075f, 3.8f, -0.035f, 28f, 190f, 0.42f),
            BuildCoreTacticalOreBoulderDefinition(config, "claudreef", 360000f, new Vector3(106f, 64f, 88f), 14600f, 48f, 0.12f, 2.8f, 0.025f, 55f, 260f, 0.62f)
        };

        for (int i = 0; i < definitions.Length; i++)
        {
            float angle = (35f + i * 112f) * Mathf.Deg2Rad;
            float radius = 950f + i * 620f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            position.y = center.y + 28f + i * 22f;
            CoreTacticalOreBoulder boulder = SpawnCoreTacticalOreBoulder(definitions[i], position, Quaternion.Euler(0f, i * 41f, i * 17f), floorY);
            if (boulder != null)
            {
                oreBoulders.Add(boulder);
            }
        }
    }

    private void SpawnCoreTacticalGasClouds(Vector3 center, SessionConfigDatabase config, SortieZoneDefinition zone)
    {
        gasClouds.Clear();
        CoreTacticalGasCloudDefinition[] definitions =
        {
            BuildCoreTacticalGasCloudDefinition(config, "common_cloud", true, 9000f, 1800f, 18f, new Vector3(0.55f, 0f, 0.18f), 0),
            BuildCoreTacticalGasCloudDefinition(config, "wet_cloud", true, 7600f, 2280f, 34f, new Vector3(0.30f, 0f, -0.42f), 1),
            BuildCoreTacticalGasCloudDefinition(config, "common_cloud", false, 11000f, 0f, 12f, new Vector3(-0.25f, 0f, 0.35f), 2)
        };

        for (int i = 0; i < Mathf.Min(CoreTacticalGasCloudCount, definitions.Length); i++)
        {
            float angle = (-42f + i * 92f) * Mathf.Deg2Rad;
            float radius = 1450f + i * 430f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            position.y = center.y + 70f + i * 18f;
            CoreTacticalGasCloud cloud = SpawnCoreTacticalGasCloud(definitions[i], position);
            if (cloud != null)
            {
                gasClouds.Add(cloud);
            }
        }
    }

    private static CoreTacticalGasCloudDefinition BuildCoreTacticalGasCloudDefinition(
        SessionConfigDatabase config,
        string condensateTypeId,
        bool harvestable,
        float rawLiters,
        float usefulLiters,
        float chemicalDamagePerMinute,
        Vector3 driftVelocityMS,
        int shapeIndex)
    {
        GasCondensateTypeConfig gasType = config != null && config.isLoaded ? config.GetGasCondensateType(condensateTypeId) : null;
        string displayName = gasType != null && !string.IsNullOrWhiteSpace(gasType.localNameRu)
            ? gasType.localNameRu + " РѕР±Р»Р°РєРѕ"
            : condensateTypeId + " cloud";
        Color color = gasType != null ? gasType.color : new Color(0.72f, 0.86f, 1f, 0.24f);
        color.a = Mathf.Clamp(color.a > 0f ? color.a : 0.24f, 0.16f, 0.34f);

        if (!harvestable)
        {
            displayName = "РџСѓСЃС‚РѕРµ РІРѕРґСЏРЅРѕРµ РѕР±Р»Р°РєРѕ";
            color = new Color(0.78f, 0.88f, 1f, 0.20f);
            usefulLiters = 0f;
        }

        CoreTacticalGasCloudDefinition definition = new CoreTacticalGasCloudDefinition
        {
            condensateTypeId = gasType != null ? gasType.id : condensateTypeId,
            condensateItemId = harvestable && gasType != null ? gasType.condensateItemId : "",
            displayName = displayName,
            color = color,
            rawVolumeLiters = Mathf.Max(0f, rawLiters),
            usefulVolumeLiters = Mathf.Clamp(usefulLiters, 0f, Mathf.Max(0f, rawLiters)),
            chemicalDamagePerMinute = chemicalDamagePerMinute,
            driftVelocityMS = driftVelocityMS,
            harvestable = harvestable
        };

        definition.lobeOffsets = new[] { Vector3.zero };
        definition.lobeSizes = new[] { new Vector3(560f, 180f, 380f) };

        definition.Normalize();
        return definition;
    }

    private CoreTacticalGasCloud SpawnCoreTacticalGasCloud(CoreTacticalGasCloudDefinition definition, Vector3 position)
    {
        if (definition == null)
        {
            return null;
        }

        GameObject cloudObject = new GameObject("Core Tactical Gas Cloud - " + definition.displayName);
        cloudObject.transform.position = position;
        cloudObject.transform.SetParent(tacticalRoot != null ? tacticalRoot.transform : transform, true);

        CoreTacticalGasCloud cloud = cloudObject.AddComponent<CoreTacticalGasCloud>();
        cloud.Initialize(definition);
        return cloud;
    }

    private static CoreTacticalOreBoulderDefinition BuildCoreTacticalOreBoulderDefinition(
        SessionConfigDatabase config,
        string oreTypeId,
        float massKg,
        Vector3 sizeMeters,
        float health,
        float kineticResistancePercent,
        float concentration01,
        float naturalIntegrityLossPerSecond,
        float verticalDriftMS,
        float chunkMinKg,
        float chunkMaxKg,
        float weaponRetention01)
    {
        OreTypeConfig oreType = config != null && config.isLoaded ? config.GetOreType(oreTypeId) : null;
        string displayName = oreType != null && !string.IsNullOrWhiteSpace(oreType.localNameRu)
            ? oreType.localNameRu + " РіР»С‹Р±Р°"
            : oreTypeId + " boulder";
        CoreTacticalOreBoulderDefinition definition = new CoreTacticalOreBoulderDefinition
        {
            oreTypeId = oreType != null ? oreType.id : oreTypeId,
            oreItemId = oreType != null && !string.IsNullOrWhiteSpace(oreType.oreItemId) ? oreType.oreItemId : oreTypeId + "_ore",
            displayName = displayName,
            color = oreType != null ? oreType.color : new Color(0.55f, 0.50f, 0.45f, 1f),
            massKg = massKg,
            sizeMeters = sizeMeters,
            maxHealth = health,
            kineticResistancePercent = kineticResistancePercent,
            thermalResistancePercent = 35f,
            chemicalResistancePercent = 15f,
            explosiveResistancePercent = 65f,
            usefulOreConcentration01 = concentration01,
            naturalIntegrityLossPerSecond = ResolveOreBoulderNaturalIntegrityLossPerSecond(sizeMeters, naturalIntegrityLossPerSecond),
            verticalDriftMS = verticalDriftMS,
            chunkMinKg = chunkMinKg,
            chunkMaxKg = chunkMaxKg,
            weaponRetention01 = weaponRetention01,
            fragmentFallSpeedMS = oreType != null ? oreType.fragmentFallSpeedMS : 3.8f
        };
        definition.Normalize();
        return definition;
    }

    private static float ResolveOreBoulderNaturalIntegrityLossPerSecond(Vector3 sizeMeters, float fallback)
    {
        float averageDiameter = Mathf.Max(1f, (Mathf.Max(0f, sizeMeters.x) + Mathf.Max(0f, sizeMeters.y) + Mathf.Max(0f, sizeMeters.z)) / 3f);
        return averageDiameter > 1f ? averageDiameter : Mathf.Max(0f, fallback);
    }

    private CoreTacticalOreBoulder SpawnCoreTacticalOreBoulder(CoreTacticalOreBoulderDefinition definition, Vector3 position, Quaternion rotation, float stormFloorY)
    {
        if (definition == null)
        {
            return null;
        }

        GameObject boulderObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        boulderObject.name = "Core Tactical Ore Boulder - " + definition.displayName;
        boulderObject.transform.SetPositionAndRotation(position, rotation);
        boulderObject.transform.SetParent(tacticalRoot != null ? tacticalRoot.transform : transform, true);

        Rigidbody body = boulderObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.mass = Mathf.Max(1f, definition.massKg);

        CoreTacticalShipMotor motor = boulderObject.AddComponent<CoreTacticalShipMotor>();
        motor.massKg = definition.massKg;
        motor.maxForwardSpeedMS = 0.1f;
        motor.maxReverseSpeedMS = 0f;
        motor.maxLateralSpeedMS = 0f;
        motor.forwardAccelerationMS2 = 0.01f;
        motor.lateralAccelerationMS2 = 0.01f;
        motor.brakingAccelerationMS2 = 0.01f;
        motor.maxYawRateDegPerSecond = 0.01f;
        motor.obstacleAvoidanceEnabled = false;
        motor.avoidOtherShips = false;
        motor.InitializePrototypeShip("ore_boulder_" + definition.oreTypeId, definition.displayName, definition.sizeMeters, definition.color);
        motor.enabled = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        CoreTacticalCombatant combatant = boulderObject.AddComponent<CoreTacticalCombatant>();
        combatant.team = CoreTacticalCombatTeam.Enemy;
        combatant.ship = motor;

        CoreTacticalPrototypeHealth health = boulderObject.AddComponent<CoreTacticalPrototypeHealth>();
        health.maxHealth = definition.maxHealth;
        health.currentHealth = definition.maxHealth;
        health.destroyOnDeath = false;

        CoreTacticalDamageProfile damageProfile = boulderObject.AddComponent<CoreTacticalDamageProfile>();
        damageProfile.ConfigureDefense(
            "ore_boulder",
            health.maxHealth,
            definition.Resistances,
            0f,
            0f);
        health.ResetHealth();

        CoreTacticalOreBoulder boulder = boulderObject.AddComponent<CoreTacticalOreBoulder>();
        boulder.Initialize(definition, health, stormFloorY);

        Renderer renderer = boulderObject.GetComponent<Renderer>();
        CoreTacticalOreBoulder.ApplyRendererColor(renderer, definition.color);
        return boulder;
    }

    private void SpawnEnemyForPlayerProfile(Vector3 center, RuntimeShipProfile playerProfile)
    {
        SpawnEnemyFrigates(center);
        SpawnEnemyCruisers(center, 1);
        SpawnSmallAutomatons(center);
        SpawnCoreTacticalLeviathans(center);
    }

    private void SpawnEnemyFrigates(Vector3 center)
    {
        RuntimeShipProfile enemyProfile = BuildEnemyFrigateProfile();
        for (int i = 0; i < EnemyFrigateCount; i++)
        {
            float angle = (i * 360f / EnemyFrigateCount + Random.Range(-13f, 13f)) * Mathf.Deg2Rad;
            float radius = Random.Range(EnemySpawnMinRadiusMeters, EnemySpawnMaxRadiusMeters);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
            Vector3 position = center + direction * radius;
            position.y = fleet.commandPlaneAltitudeMeters;

            Vector3 toCenter = center - position;
            toCenter.y = 0f;
            Quaternion rotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up)
                : Quaternion.identity;

            CoreTacticalShipMotor frigate = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                fleet,
                "enemy_intro_frigate_" + (i + 1),
                "Enemy Frigate " + (i + 1),
                position,
                rotation,
                new Vector3(15f, 7f, 60f),
                EnemyFrigateMaxSpeedMS,
                EnemyFrigateAccelerationMS2,
                EnemyFrigateBrakingMS2,
                EnemyFrigateYawDegPerSecond,
                EnemyFrigateReverseSpeedMS,
                EnemyFrigateLateralSpeedMS,
                520000f,
                false,
                new Color(0.95f, 0.11f, 0.08f, 1f));
            frigate.transform.SetParent(tacticalRoot.transform, true);

            CoreTacticalCombatant combatant = frigate.GetComponent<CoreTacticalCombatant>();
            if (combatant != null)
            {
                combatant.team = CoreTacticalCombatTeam.Enemy;
                combatant.ship = frigate;
            }

            CoreTacticalPrototypeHealth health = frigate.GetComponent<CoreTacticalPrototypeHealth>();
            if (health != null)
            {
                ConfigureRuntimeDamageProfile(frigate, enemyProfile);
            }

            ConfigureAutomatonWreckSpawner(frigate, enemyProfile, 60f);

            CoreTacticalEnemyFrigateOrbitBrain brain = frigate.gameObject.AddComponent<CoreTacticalEnemyFrigateOrbitBrain>();
            brain.target = playerShip;
            brain.orbitRadiusMeters = Random.Range(1500f, 2600f);
            brain.orbitDirection = i % 2 == 0 ? 1f : -1f;
            brain.commandRefreshIntervalSeconds = Random.Range(0.26f, 0.42f);
            brain.orbitLeadDegrees = Random.Range(18f, 36f);

            enemyShips.Add(frigate);
        }
    }

    private void SpawnEnemyCruisers(Vector3 center, int count)
    {
        RuntimeShipProfile enemyProfile = BuildEnemyCruiserProfile();
        int cruiserCount = Mathf.Max(0, count);
        for (int i = 0; i < cruiserCount; i++)
        {
            float angle = (i * 360f / Mathf.Max(1, cruiserCount) + 46f + Random.Range(-9f, 9f)) * Mathf.Deg2Rad;
            float radius = Random.Range(EnemySpawnMinRadiusMeters + 1800f, EnemySpawnMaxRadiusMeters + 3600f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
            Vector3 position = center + direction * radius;
            position.y = fleet.commandPlaneAltitudeMeters;

            Vector3 toCenter = center - position;
            toCenter.y = 0f;
            Quaternion rotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up)
                : Quaternion.identity;

            CoreTacticalShipMotor cruiser = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                fleet,
                "enemy_intro_cruiser_" + (i + 1),
                "Enemy Cruiser " + (i + 1),
                position,
                rotation,
                new Vector3(32f, 12f, 150f),
                EnemyCruiserMaxSpeedMS,
                EnemyCruiserAccelerationMS2,
                EnemyCruiserBrakingMS2,
                EnemyCruiserYawDegPerSecond,
                EnemyCruiserReverseSpeedMS,
                EnemyCruiserLateralSpeedMS,
                3200000f,
                false,
                new Color(0.82f, 0.12f, 0.09f, 1f));
            cruiser.transform.SetParent(tacticalRoot.transform, true);

            CoreTacticalCombatant combatant = cruiser.GetComponent<CoreTacticalCombatant>();
            if (combatant != null)
            {
                combatant.team = CoreTacticalCombatTeam.Enemy;
                combatant.ship = cruiser;
            }

            CoreTacticalPrototypeHealth health = cruiser.GetComponent<CoreTacticalPrototypeHealth>();
            if (health != null)
            {
                ConfigureRuntimeDamageProfile(cruiser, enemyProfile);
            }

            ConfigureAutomatonWreckSpawner(cruiser, enemyProfile, 150f);

            CoreTacticalEnemyCruiserBrain brain = cruiser.gameObject.AddComponent<CoreTacticalEnemyCruiserBrain>();
            brain.target = playerShip;

            enemyShips.Add(cruiser);
        }
    }

    private void SpawnSmallAutomatons(Vector3 center)
    {
        float[] sizesMeters = { 5f, 10f, 20f };
        for (int i = 0; i < sizesMeters.Length; i++)
        {
            float sizeMeters = sizesMeters[i];
            RuntimeShipProfile profile = BuildSmallAutomatonProfile(sizeMeters, i);
            float angle = (205f + i * 38f + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            float radius = Random.Range(EnemySpawnMinRadiusMeters * 0.62f, EnemySpawnMinRadiusMeters + 900f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
            Vector3 position = center + direction * radius;
            position.y = fleet.commandPlaneAltitudeMeters + Random.Range(-12f, 12f);

            Vector3 toCenter = center - position;
            toCenter.y = 0f;
            Quaternion rotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up)
                : Quaternion.identity;

            CoreTacticalShipMotor automaton = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                fleet,
                profile.shipId,
                profile.displayName,
                position,
                rotation,
                profile.hullSizeMeters,
                profile.maxForwardSpeedMS,
                profile.forwardAccelerationMS2,
                profile.brakingAccelerationMS2,
                profile.maxYawRateDegPerSecond,
                profile.maxReverseSpeedMS,
                profile.maxLateralSpeedMS,
                profile.massKg,
                false,
                profile.color);
            automaton.transform.SetParent(tacticalRoot.transform, true);

            CoreTacticalCombatant combatant = automaton.GetComponent<CoreTacticalCombatant>();
            if (combatant != null)
            {
                combatant.team = CoreTacticalCombatTeam.Enemy;
                combatant.ship = automaton;
            }

            ConfigureRuntimeLoadout(automaton, profile, playerShip, CoreTacticalCombatTeam.Friendly);

            ConfigureAutomatonWreckSpawner(automaton, profile, sizeMeters);

            CoreTacticalEnemyFrigateOrbitBrain brain = automaton.gameObject.AddComponent<CoreTacticalEnemyFrigateOrbitBrain>();
            brain.target = playerShip;
            brain.orbitRadiusMeters = Mathf.Lerp(850f, 1750f, Mathf.InverseLerp(5f, 20f, sizeMeters));
            brain.orbitDirection = i % 2 == 0 ? -1f : 1f;
            brain.commandRefreshIntervalSeconds = Random.Range(0.18f, 0.34f);
            brain.orbitLeadDegrees = Random.Range(28f, 58f);

            enemyShips.Add(automaton);
        }
    }

    private void SpawnCoreTacticalLeviathans(Vector3 center)
    {
        float[] lengthsMeters = { 10f, 24f, 72f };
        for (int i = 0; i < lengthsMeters.Length; i++)
        {
            float lengthMeters = lengthsMeters[i];
            RuntimeShipProfile profile = BuildLeviathanProfile(lengthMeters, i);
            float angle = (318f + i * 34f + Random.Range(-7f, 7f)) * Mathf.Deg2Rad;
            float radius = Random.Range(EnemySpawnMinRadiusMeters + 800f, EnemySpawnMaxRadiusMeters + 2200f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
            Vector3 position = center + direction * radius;
            position.y = fleet.commandPlaneAltitudeMeters + Random.Range(-24f, 18f);

            Vector3 toCenter = center - position;
            toCenter.y = 0f;
            Quaternion rotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized, Vector3.up)
                : Quaternion.identity;

            CoreTacticalShipMotor leviathanShip = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                fleet,
                profile.shipId,
                profile.displayName,
                position,
                rotation,
                profile.hullSizeMeters,
                profile.maxForwardSpeedMS,
                profile.forwardAccelerationMS2,
                profile.brakingAccelerationMS2,
                profile.maxYawRateDegPerSecond,
                profile.maxReverseSpeedMS,
                profile.maxLateralSpeedMS,
                profile.massKg,
                false,
                profile.color);
            leviathanShip.transform.SetParent(tacticalRoot.transform, true);

            CoreTacticalCombatant combatant = leviathanShip.GetComponent<CoreTacticalCombatant>();
            if (combatant != null)
            {
                combatant.team = CoreTacticalCombatTeam.Enemy;
                combatant.ship = leviathanShip;
            }

            ConfigureRuntimeDamageProfile(leviathanShip, profile);

            CoreTacticalLeviathanController leviathan = leviathanShip.gameObject.AddComponent<CoreTacticalLeviathanController>();
            leviathan.Configure(lengthMeters);
            leviathan.aggressionRadiusMeters = Mathf.Lerp(850f, 1850f, Mathf.InverseLerp(10f, 72f, lengthMeters));
            leviathan.closeAggressionRadiusMeters = Mathf.Lerp(220f, 620f, Mathf.InverseLerp(10f, 72f, lengthMeters));
            leviathan.feedingSearchRadiusMeters = Mathf.Lerp(650f, 1350f, Mathf.InverseLerp(10f, 72f, lengthMeters));
            leviathan.fleeDistanceMeters = Mathf.Lerp(640f, 1500f, Mathf.InverseLerp(10f, 72f, lengthMeters));

            enemyShips.Add(leviathanShip);
        }
    }

    private static void ConfigureAutomatonWreckSpawner(CoreTacticalShipMotor ship, RuntimeShipProfile profile, float sizeMeters)
    {
        if (ship == null)
        {
            return;
        }

        CoreTacticalAutomatonWreckSpawner wreckSpawner = ship.GetComponent<CoreTacticalAutomatonWreckSpawner>();
        if (wreckSpawner == null)
        {
            wreckSpawner = ship.gameObject.AddComponent<CoreTacticalAutomatonWreckSpawner>();
        }

        float size = Mathf.Max(1f, sizeMeters);
        float accessDifficulty = size <= 5.5f ? -50f : size <= 180f ? -20f : 20f;
        wreckSpawner.Configure(
            profile.displayName + " Wreck",
            EstimateAutomatonWreckMassKg(size),
            Mathf.Max(40f, profile.structureHp * 0.32f),
            accessDifficulty,
            Mathf.Max(1.5f, size * 0.38f),
            CoreTacticalAutomatonWreck.BuildDefaultManifest(size));
    }

    private static float EstimateAutomatonWreckMassKg(float sizeMeters)
    {
        float size = Mathf.Max(1f, sizeMeters);
        return Mathf.Max(80f, size * 120f + size * size * 1.2f);
    }

    private static RuntimeShipProfile BuildSmallAutomatonProfile(float sizeMeters, int index)
    {
        float size = Mathf.Max(1f, sizeMeters);
        float t = Mathf.InverseLerp(5f, 20f, size);
        return new RuntimeShipProfile
        {
            shipId = "enemy_automaton_" + Mathf.RoundToInt(size).ToString("0") + "m_" + (index + 1),
            displayName = Mathf.RoundToInt(size).ToString("0") + "m Automaton",
            classId = "automaton",
            roleId = "swarm",
            loadout = RuntimeLoadoutKind.None,
            hullSizeMeters = new Vector3(size * 0.62f, size * 0.42f, size),
            maxForwardSpeedMS = Mathf.Lerp(155f, 105f, t),
            forwardAccelerationMS2 = Mathf.Lerp(62f, 36f, t),
            brakingAccelerationMS2 = Mathf.Lerp(78f, 44f, t),
            maxYawRateDegPerSecond = Mathf.Lerp(42f, 26f, t),
            maxReverseSpeedMS = Mathf.Lerp(34f, 22f, t),
            maxLateralSpeedMS = Mathf.Lerp(38f, 26f, t),
            massKg = Mathf.Lerp(1800f, 98000f, t * t),
            structureHp = Mathf.Lerp(120f, 780f, t * t),
            resistances = new CoreTacticalResistanceSet(
                Mathf.Lerp(18f, 34f, t),
                Mathf.Lerp(10f, 22f, t),
                Mathf.Lerp(6f, 16f, t),
                Mathf.Lerp(12f, 24f, t)),
            citadelHp = 0f,
            powerPlantModuleHp = 0f,
            color = Color.Lerp(new Color(0.95f, 0.36f, 0.12f, 1f), new Color(0.68f, 0.17f, 0.09f, 1f), t),
            loadoutSummary = "unarmed automaton drone"
        };
    }

    private static RuntimeShipProfile BuildLeviathanProfile(float lengthMeters, int index)
    {
        float length = Mathf.Max(10f, lengthMeters);
        float t = Mathf.InverseLerp(10f, 72f, length);
        return new RuntimeShipProfile
        {
            shipId = "leviathan_" + Mathf.RoundToInt(length).ToString("0") + "m_" + (index + 1),
            displayName = Mathf.RoundToInt(length).ToString("0") + "m Leviathan",
            classId = "leviathan",
            roleId = "predator",
            loadout = RuntimeLoadoutKind.None,
            hullSizeMeters = new Vector3(Mathf.Max(3.6f, length * 0.24f), Mathf.Max(2.8f, length * 0.15f), length),
            maxForwardSpeedMS = Mathf.Lerp(132f, 76f, t),
            forwardAccelerationMS2 = Mathf.Lerp(58f, 24f, t),
            brakingAccelerationMS2 = Mathf.Lerp(72f, 34f, t),
            maxYawRateDegPerSecond = Mathf.Lerp(54f, 20f, t),
            maxReverseSpeedMS = Mathf.Lerp(20f, 12f, t),
            maxLateralSpeedMS = Mathf.Lerp(22f, 12f, t),
            massKg = Mathf.Lerp(12000f, 2800000f, t * t),
            structureHp = Mathf.Lerp(900f, 16000f, t * t),
            resistances = new CoreTacticalResistanceSet(
                Mathf.Lerp(28f, 40f, t),
                Mathf.Lerp(64f, 80f, t),
                Mathf.Lerp(82f, 90f, t),
                Mathf.Lerp(36f, 50f, t)),
            citadelHp = 0f,
            powerPlantModuleHp = 0f,
            color = new Color(0.56f, 0.20f, 0.86f, 1f),
            loadoutSummary = "resistant predatory leviathan"
        };
    }

    private static RuntimeShipProfile BuildEnemyFrigateProfile()
    {
        return new RuntimeShipProfile
        {
            shipId = "enemy_intro_frigate",
            displayName = "Enemy Frigate",
            classId = "frigate",
            roleId = "combat",
            loadout = RuntimeLoadoutKind.FrigateAutocannon,
            hullSizeMeters = new Vector3(15f, 7f, 60f),
            maxForwardSpeedMS = EnemyFrigateMaxSpeedMS,
            forwardAccelerationMS2 = EnemyFrigateAccelerationMS2,
            brakingAccelerationMS2 = EnemyFrigateBrakingMS2,
            maxYawRateDegPerSecond = EnemyFrigateYawDegPerSecond,
            maxReverseSpeedMS = EnemyFrigateReverseSpeedMS,
            maxLateralSpeedMS = EnemyFrigateLateralSpeedMS,
            massKg = 520000f,
            structureHp = 2400f,
            resistances = CoreTacticalDamageProfile.ResolveClassBaselineResistances("frigate"),
            citadelHp = 1000f,
            powerPlantModuleHp = 500f,
            color = new Color(0.95f, 0.11f, 0.08f, 1f),
            loadoutSummary = "enemy 30 mm autocannon battery"
        };
    }

    private static RuntimeShipProfile BuildEnemyCruiserProfile()
    {
        return new RuntimeShipProfile
        {
            shipId = "enemy_intro_cruiser",
            displayName = "Enemy Cruiser",
            classId = "cruiser",
            roleId = "artillery",
            loadout = RuntimeLoadoutKind.ArtilleryCruiser,
            hullSizeMeters = new Vector3(32f, 12f, 150f),
            maxForwardSpeedMS = EnemyCruiserMaxSpeedMS,
            forwardAccelerationMS2 = EnemyCruiserAccelerationMS2,
            brakingAccelerationMS2 = EnemyCruiserBrakingMS2,
            maxYawRateDegPerSecond = EnemyCruiserYawDegPerSecond,
            maxReverseSpeedMS = EnemyCruiserReverseSpeedMS,
            maxLateralSpeedMS = EnemyCruiserLateralSpeedMS,
            massKg = 3200000f,
            structureHp = 9000f,
            resistances = CoreTacticalDamageProfile.ResolveClassBaselineResistances("cruiser"),
            citadelHp = 3600f,
            powerPlantModuleHp = 1500f,
            color = new Color(0.82f, 0.12f, 0.09f, 1f),
            loadoutSummary = "enemy cruiser artillery"
        };
    }

    private void UpdateMission()
    {
        EnsureExitAvailable();

        if (extractionComplete || playerShip == null)
        {
            return;
        }

        UpdateExitMarker();
        if (!autoExitRequested)
        {
            SortieSessionState sortie = ResolveMeta().ActiveSortie;
            if (sortie != null)
            {
                sortie.ResetExtractionRunup();
            }

            missionStatus = AreMissionObjectivesComplete()
                ? "Bonus objectives complete. Press AUTO EXIT when ready."
                : BuildLockedMissionObjectiveStatus();
            return;
        }

        CommandPlayerToExit();
        Rigidbody body = playerShip.Body;
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        bool runupReady = ResolveMeta().RecordActiveSortieExtractionRunup(
            playerShip.transform.position,
            velocity,
            playerShip.transform.forward,
            Time.deltaTime,
            playerSlipDrive != null && playerSlipDrive.IsActive);

        if (!runupReady)
        {
            missionStatus = ResolveMeta().ActiveSortieExtractionRunupStatus;
            return;
        }

        extractionComplete = true;
        if (playerMiningRig != null && !playerMiningRig.TryFlushDirtyOreToRuntimeCargo(out string lowGradeUnloadMessage))
        {
            extractionComplete = false;
            missionStatus = lowGradeUnloadMessage;
            return;
        }

        MirrorSessionShipToTacticalPlayer();
        if (ResolveMeta().TryExtractActiveSortie(out string message))
        {
            missionStatus = message;
            Destroy(gameObject);
        }
        else
        {
            extractionComplete = false;
            missionStatus = message;
        }
    }

    private void UnlockExtraction()
    {
        EnsureExitAvailable();
        missionStatus = "Bonus combat and ore objectives complete. Exit remains open.";
    }

    private void EnsureExitAvailable(SortieZoneDefinition zoneOverride = null)
    {
        if (extractionUnlocked && exitLine != null)
        {
            return;
        }

        if (fleet == null)
        {
            return;
        }

        SortieZoneDefinition zone = zoneOverride;
        if (zone == null)
        {
            MetaGameState meta = ResolveMeta();
            SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
            zone = sortie != null ? sortie.zone : null;
        }

        if (zone == null)
        {
            return;
        }

        zone.Normalize();
        extractionUnlocked = true;
        exitDirection = ResolveExitDirection(zone);
        Vector3 center = zone.centerPosition;
        center.y = fleet.commandPlaneAltitudeMeters;
        exitPosition = center + exitDirection * (Mathf.Max(100f, zone.radiusMeters) + ExitDirectionBeyondBoundaryMeters);
        exitPosition.y = fleet.commandPlaneAltitudeMeters;
        if (exitLine == null)
        {
            CreateExitMarker(center);
        }
        else
        {
            UpdateExitMarker();
        }
    }

    private void ToggleClaudianSlip()
    {
        if (playerSlipDrive == null || extractionComplete)
        {
            return;
        }

        if (playerSlipDrive.IsArmed)
        {
            playerSlipDrive.SetArmed(false);
            missionStatus = "Claudian slip disabled. Weapons are free again.";
            return;
        }

        if (TrySetClaudianSlipArmed(true, out string status))
        {
            EnsureExitAvailable();
            if (autoExitRequested && extractionUnlocked)
            {
                CommandPlayerToExit();
            }

            missionStatus = playerSlipDrive.IsActive
                ? "Claudian slip active."
                : "Claudian slip armed. " + BuildSlipActivationWaitLine();
        }
        else
        {
            missionStatus = status;
        }
    }

    private void BeginAutoExit()
    {
        if (extractionComplete || playerShip == null)
        {
            return;
        }

        EnsureExitAvailable();
        if (!extractionUnlocked)
        {
            missionStatus = "Exit route is still initializing.";
            return;
        }

        if (autoExitRequested)
        {
            CancelAutoExit();
            return;
        }

        autoExitRequested = true;
        CommandPlayerToExit();
        missionStatus = "Auto exit engaged. "
            + (playerShip != null ? playerShip.displayName : "Ship")
            + " is leaving the mission circle.";
    }

    private void CancelAutoExit()
    {
        autoExitRequested = false;
        if (playerSlipDrive != null)
        {
            playerSlipDrive.SetArmed(false);
        }

        if (playerShip != null)
        {
            playerShip.StopCommandAtCurrentPosition();
            playerShip.SetSelected(true);
        }

        MetaGameState meta = ResolveMeta();
        SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
        if (sortie != null)
        {
            sortie.ResetExtractionRunup();
        }

        missionStatus = "Auto exit cancelled. Ship control restored.";
    }

    private bool TrySetClaudianSlipArmed(bool value, out string status)
    {
        status = "";
        if (playerSlipDrive == null)
        {
            status = "Claudian slip is unavailable.";
            return false;
        }

        if (!value)
        {
            playerSlipDrive.SetArmed(false);
            return true;
        }

        if (HasClaudianSlipInterference())
        {
            playerSlipDrive.SetArmed(false);
            status = "Claudian slip is blocked by harmful interference.";
            return false;
        }

        if (!playerSlipDrive.TrySetArmedWhenAllowed(true, out status))
        {
            return false;
        }

        return true;
    }

    private bool HasClaudianSlipInterference()
    {
        return playerShip != null && CoreTacticalHarpoonLink.HasActiveLinkForShip(playerShip);
    }

    private string BuildSlipActivationWaitLine()
    {
        if (playerSlipDrive == null)
        {
            return "Speed: -- / --";
        }

        if (playerSlipDrive.CurrentForwardSpeedMS + 0.05f < playerSlipDrive.minimumEngageSpeedMS)
        {
            return "Needs " + playerSlipDrive.minimumEngageSpeedMS.ToString("0") + " m/s to engage. " + BuildSlipSpeedLine();
        }

        return "Waiting for field lock. " + BuildSlipSpeedLine();
    }

    private void CommandPlayerToExit()
    {
        if (playerShip == null)
        {
            return;
        }

        Vector3 forward = ResolveCurrentExitDirection();
        Vector3 commandPosition = ResolveCurrentExitCommandPosition(forward);
        commandPosition.y = fleet != null ? fleet.CommandPlaneAltitudeMeters : playerShip.transform.position.y;
        exitDirection = forward;
        exitPosition = commandPosition;
        playerShip.SetCommand(commandPosition, forward);
        UpdateExitMarker();
    }

    private Vector3 ResolveCurrentExitDirection()
    {
        if (playerShip == null)
        {
            return exitDirection.sqrMagnitude > 0.0001f ? exitDirection.normalized : Vector3.forward;
        }

        Vector3 currentPosition = playerShip.Body != null ? playerShip.Body.position : playerShip.transform.position;
        Vector3 fromCenter = currentPosition - missionCenter;
        fromCenter.y = 0f;
        if (fromCenter.sqrMagnitude > 0.0001f)
        {
            return fromCenter.normalized;
        }

        Vector3 flatVelocity = playerShip.Body != null ? playerShip.Body.linearVelocity : Vector3.zero;
        flatVelocity.y = 0f;
        if (flatVelocity.sqrMagnitude > 0.0001f)
        {
            return flatVelocity.normalized;
        }

        return FlattenDirection(exitDirection, playerShip.transform.forward);
    }

    private Vector3 ResolveCurrentExitCommandPosition(Vector3 outward)
    {
        outward = FlattenDirection(outward, exitDirection);
        Vector3 currentPosition = playerShip != null
            ? playerShip.Body != null ? playerShip.Body.position : playerShip.transform.position
            : missionCenter;
        Vector3 center = missionCenter;
        center.y = currentPosition.y;
        Vector3 fromCenter = currentPosition - center;
        fromCenter.y = 0f;
        float currentDistance = fromCenter.magnitude;
        float boundaryDistance = Mathf.Max(100f, missionRadiusMeters) + ResolveExitBoundaryMarginMeters();
        float targetDistance = Mathf.Max(boundaryDistance, currentDistance + ExitCommandOutwardStepMeters);
        Vector3 commandPosition = center + outward * targetDistance;
        commandPosition.y = currentPosition.y;
        return commandPosition;
    }

    private float ResolveExitBoundaryMarginMeters()
    {
        SortieZoneDefinition zone = null;
        MetaGameState meta = ResolveMeta();
        SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
        if (sortie != null)
        {
            zone = sortie.zone;
        }

        float tolerance = zone != null ? Mathf.Max(0f, zone.extractionBoundaryToleranceMeters) : 0f;
        return Mathf.Max(ExitCommandBoundaryMarginMeters, tolerance + ExitCommandBoundaryMarginMeters);
    }

    private string BuildSlipStatusLine()
    {
        if (playerSlipDrive == null)
        {
            return "Claudian slip: unavailable";
        }

        string state = playerSlipDrive.IsActive
            ? "active"
            : playerSlipDrive.IsBlockedByMissionZone ? "zone locked" : playerSlipDrive.IsArmed ? "armed" : "off";
        return "Claudian slip: " + state
            + " | speed " + playerSlipDrive.CurrentForwardSpeedMS.ToString("0")
            + "/" + playerSlipDrive.targetForwardSpeedMS.ToString("0")
            + " m/s | x" + playerSlipDrive.CurrentForwardSpeedMultiplier.ToString("0.0");
    }

    private static string FormatShipSpeedLine(CoreTacticalShipMotor ship, float currentSpeedMS)
    {
        if (ship == null)
        {
            return currentSpeedMS.ToString("0") + "/-- m/s";
        }

        return currentSpeedMS.ToString("0")
            + "/"
            + Mathf.Max(0f, ship.maxForwardSpeedMS).ToString("0")
            + " m/s";
    }

    private string BuildSlipSpeedLine()
    {
        if (playerSlipDrive == null)
        {
            return "Speed: -- / --";
        }

        return "Speed: " + playerSlipDrive.CurrentForwardSpeedMS.ToString("0")
            + " / " + playerSlipDrive.targetForwardSpeedMS.ToString("0")
            + " m/s    x" + playerSlipDrive.CurrentForwardSpeedMultiplier.ToString("0.0");
    }

    private void CreateExitMarker(Vector3 center)
    {
        if (exitLine != null)
        {
            return;
        }

        exitMaterial = CreateTransparentMaterial(new Color(0.18f, 0.86f, 1f, 0.34f));
        GameObject lineObject = new GameObject("Claudian Slip Exit Direction");
        lineObject.transform.SetParent(tacticalRoot.transform, false);
        exitLine = lineObject.AddComponent<LineRenderer>();
        exitLine.sharedMaterial = exitMaterial;
        exitLine.positionCount = 2;
        exitLine.useWorldSpace = true;
        exitLine.startWidth = 30f;
        exitLine.endWidth = 90f;
        exitLine.numCapVertices = 6;
        exitLine.startColor = new Color(0.18f, 0.86f, 1f, 0.12f);
        exitLine.endColor = new Color(0.18f, 0.86f, 1f, 0.42f);
        exitLine.SetPosition(0, center);
        exitLine.SetPosition(1, exitPosition);
    }

    private void UpdateExitMarker()
    {
        if (exitLine == null)
        {
            return;
        }

        float pulse = 0.28f + Mathf.Sin(Time.time * 3.2f) * 0.08f;
        if (exitMaterial != null)
        {
            exitMaterial.color = new Color(0.18f, 0.86f, 1f, pulse);
        }

        if (exitLine != null && ResolveMeta().ActiveSortie != null)
        {
            Vector3 center = ResolveMeta().ActiveSortie.zone.centerPosition;
            center.y = exitPosition.y;
            exitLine.SetPosition(0, center);
            exitLine.SetPosition(1, exitPosition);
        }
    }

    private int CountAliveEnemies()
    {
        int alive = 0;
        for (int i = enemyShips.Count - 1; i >= 0; i--)
        {
            CoreTacticalShipMotor enemy = enemyShips[i];
            if (enemy == null)
            {
                enemyShips.RemoveAt(i);
                continue;
            }

            CoreTacticalCombatant combatant = enemy.GetComponent<CoreTacticalCombatant>();
            if (combatant == null || combatant.IsAlive)
            {
                alive++;
            }
        }

        return alive;
    }

    private Vector3 ResolveExitDirection(SortieZoneDefinition zone)
    {
        if (playerShip != null && zone != null)
        {
            Vector3 fromCenter = playerShip.transform.position - zone.centerPosition;
            fromCenter.y = 0f;
            if (fromCenter.sqrMagnitude > 0.0001f)
            {
                return fromCenter.normalized;
            }
        }

        return Vector3.forward;
    }

    private void MirrorSessionShipToTacticalPlayer()
    {
        if (playerShip == null || sessionShipRoot == null)
        {
            return;
        }

        Vector3 position = playerShip.transform.position;
        Quaternion rotation = playerShip.transform.rotation;
        sessionShipRoot.SetPositionAndRotation(position, rotation);
        if (sessionShipBody != null)
        {
            sessionShipBody.position = position;
            sessionShipBody.rotation = rotation;
            if (!sessionShipBody.isKinematic)
            {
                sessionShipBody.linearVelocity = Vector3.zero;
                sessionShipBody.angularVelocity = Vector3.zero;
            }
        }

        ResolveMeta()?.RememberActiveSortiePosition(position);
    }

    private void SuppressSessionFlightActors()
    {
        MetaGameState meta = ResolveMeta();
        sessionShipRoot = ResolveSessionShipRoot(meta);

        if (sessionShipRoot != null)
        {
            Behaviour[] shipBehaviours = sessionShipRoot.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < shipBehaviours.Length; i++)
            {
                Behaviour behaviour = shipBehaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                suppressedSessionShipBehaviours.Add(new BehaviourState { behaviour = behaviour, enabled = behaviour.enabled });
                behaviour.enabled = false;
            }

            sessionShipBody = sessionShipRoot.GetComponent<Rigidbody>();
            if (sessionShipBody == null)
            {
                sessionShipBody = sessionShipRoot.GetComponentInChildren<Rigidbody>(true);
            }

            if (sessionShipBody != null)
            {
                sessionShipBody.useGravity = false;
                if (!sessionShipBody.isKinematic)
                {
                    sessionShipBody.linearVelocity = Vector3.zero;
                    sessionShipBody.angularVelocity = Vector3.zero;
                    sessionShipBody.isKinematic = true;
                }
            }

            Renderer[] renderers = sessionShipRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                suppressedRenderers.Add(new RendererState { renderer = renderers[i], enabled = renderers[i].enabled });
                renderers[i].enabled = false;
            }

            Collider[] colliders = sessionShipRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                suppressedColliders.Add(new ColliderState { collider = colliders[i], enabled = colliders[i].enabled });
                colliders[i].enabled = false;
            }
        }

        SuppressBehaviour(FindFirstObjectByType<WildWindSessionCameraController>());
        SuppressBehaviour(FindFirstObjectByType<WildWindFlightControlBridge>());
        SuppressBehaviour(FindFirstObjectByType<SortieBoundaryController>());
        SuppressBehaviour(FindFirstObjectByType<ManualSortieEncounterController>());
        SuppressBehaviour(FindFirstObjectByType<SortieLocationIsolationController>());
        SuppressBehaviour(FindFirstObjectByType<WildWindGameplayHud>());

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            SuppressBehaviour(canvases[i]);
        }

        GraphicRaycaster[] raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
        for (int i = 0; i < raycasters.Length; i++)
        {
            SuppressBehaviour(raycasters[i]);
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.enabled)
            {
                continue;
            }

            suppressedCameras.Add(new CameraState { camera = camera, enabled = camera.enabled, tag = camera.tag });
            camera.enabled = false;
            if (camera.CompareTag("MainCamera"))
            {
                camera.tag = "Untagged";
            }
        }
    }

    private static Transform ResolveSessionShipRoot(MetaGameState meta)
    {
        WildWindGameplaySession session = FindFirstObjectByType<WildWindGameplaySession>();
        if (session != null && session.PlayerShipRoot != null)
        {
            return session.PlayerShipRoot;
        }

        if (meta != null && meta.shipLoader != null && meta.shipLoader.targetShip != null)
        {
            return meta.shipLoader.targetShip.transform;
        }

        return null;
    }

    private void SuppressBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this || !behaviour.enabled)
        {
            return;
        }

        suppressedBehaviours.Add(new BehaviourState { behaviour = behaviour, enabled = behaviour.enabled });
        behaviour.enabled = false;
    }

    private void RestoreSuppressedSessionActors()
    {
        for (int i = 0; i < suppressedCameras.Count; i++)
        {
            CameraState state = suppressedCameras[i];
            if (state.camera == null) continue;
            state.camera.tag = string.IsNullOrWhiteSpace(state.tag) ? "Untagged" : state.tag;
            state.camera.enabled = state.enabled;
        }

        suppressedCameras.Clear();

        for (int i = 0; i < suppressedBehaviours.Count; i++)
        {
            BehaviourState state = suppressedBehaviours[i];
            if (state.behaviour != null)
            {
                state.behaviour.enabled = state.enabled;
            }
        }

        suppressedBehaviours.Clear();

        bool restoreSessionShipBehaviours = ResolveMeta() == null || ResolveMeta().CurrentMode != GameSessionMode.Docked;
        for (int i = 0; i < suppressedSessionShipBehaviours.Count; i++)
        {
            BehaviourState state = suppressedSessionShipBehaviours[i];
            if (state.behaviour != null && restoreSessionShipBehaviours)
            {
                state.behaviour.enabled = state.enabled;
            }
        }

        suppressedSessionShipBehaviours.Clear();

        for (int i = 0; i < suppressedRenderers.Count; i++)
        {
            RendererState state = suppressedRenderers[i];
            if (state.renderer != null)
            {
                state.renderer.enabled = state.enabled;
            }
        }

        suppressedRenderers.Clear();

        for (int i = 0; i < suppressedColliders.Count; i++)
        {
            ColliderState state = suppressedColliders[i];
            if (state.collider != null)
            {
                state.collider.enabled = state.enabled;
            }
        }

        suppressedColliders.Clear();

    }

    private void CreateTacticalCamera()
    {
        GameObject cameraObject = new GameObject("Core Tactical Combat Camera");
        cameraObject.transform.SetParent(tacticalRoot.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.usePhysicalProperties = false;
        camera.lensShift = Vector2.zero;
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 65000f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.ResetProjectionMatrix();
        camera.ResetAspect();
        if (fleet != null)
        {
            fleet.SetInputCamera(camera);
        }

        CoreTacticalCameraRig cameraRig = cameraObject.AddComponent<CoreTacticalCameraRig>();
        cameraRig.fleet = fleet;
        cameraRig.targetCamera = camera;
        cameraRig.distanceMeters = 5200f;
        cameraRig.minDistanceMeters = 90f;
        cameraRig.maxDistanceMeters = 26000f;
        cameraRig.yawDegrees = -138f;
        cameraRig.elevationDegrees = 55f;
        if (fleet != null)
        {
            fleet.SetInputCamera(camera);
        }
    }

    private void ApplyTacticalCameraAfterFleetSpawn()
    {
        if (fleet == null || fleet.InputCameraForTests == null)
        {
            return;
        }

        CoreTacticalCameraRig cameraRig = fleet.InputCameraForTests.GetComponent<CoreTacticalCameraRig>();
        if (cameraRig != null)
        {
            cameraRig.ApplyCurrentTransformForInput();
        }
    }

    private void CreateMissionBoundaryMarker()
    {
        if (missionBoundaryLine != null || tacticalRoot == null || missionRadiusMeters <= 0f)
        {
            return;
        }

        missionBoundaryMaterial = CreateTransparentMaterial(new Color(0.22f, 0.86f, 1f, 0.48f));
        GameObject lineObject = new GameObject("Core Tactical Mission Boundary");
        lineObject.transform.SetParent(tacticalRoot.transform, false);
        missionBoundaryLine = lineObject.AddComponent<LineRenderer>();
        missionBoundaryLine.sharedMaterial = missionBoundaryMaterial;
        missionBoundaryLine.useWorldSpace = true;
        missionBoundaryLine.startWidth = 26f;
        missionBoundaryLine.endWidth = 26f;
        missionBoundaryLine.numCapVertices = 4;
        missionBoundaryLine.loop = true;

        const int segments = 128;
        missionBoundaryLine.positionCount = segments;
        Vector3 center = missionCenter;
        center.y = fleet != null ? fleet.commandPlaneAltitudeMeters + 0.45f : center.y;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 point = center + new Vector3(
                Mathf.Cos(angle) * missionRadiusMeters,
                0f,
                Mathf.Sin(angle) * missionRadiusMeters);
            missionBoundaryLine.SetPosition(i, point);
        }
    }

    private void CreateTacticalLight()
    {
        GameObject lightObject = new GameObject("Core Tactical Combat Sun");
        lightObject.transform.SetParent(tacticalRoot.transform, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.94f, 0.84f, 1f);
        light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }

        Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.color = color;
        return material;
    }

    private static float FlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.magnitude;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalClaudianSlipDrive : MonoBehaviour
{
    public CoreTacticalShipMotor ship;
    public CoreTacticalWeaponControl weaponControl;
    public float minimumEngageSpeedMS = 320f;
    public float targetForwardSpeedMS = 365f;
    public float rampUpSeconds = 20f;
    public float rampDownSeconds = 8f;
    public float overspeedBrakeSeconds = 8f;
    public bool requireOutsideMissionZone;
    public Vector3 missionZoneCenter;
    public float missionZoneRadiusMeters;

    private bool armed;
    private bool active;
    private bool blockedByMissionZone;
    private float currentForwardSpeedMS;
    private float currentForwardSpeedMultiplier = 1f;

    public bool IsArmed => armed;
    public bool IsActive => active;
    public bool IsBlockedByMissionZone => blockedByMissionZone;
    public float CurrentForwardSpeedMS => currentForwardSpeedMS;
    public float CurrentForwardSpeedMultiplier => currentForwardSpeedMultiplier;
    public float TargetForwardSpeedMultiplier => CalculateTargetForwardSpeedMultiplier();

    private void Awake()
    {
        ship ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl ??= GetComponent<CoreTacticalWeaponControl>();
    }

    private void OnDisable()
    {
        ApplyState(false, 1f);
    }

    private void OnDestroy()
    {
        ApplyState(false, 1f);
    }

    private void Update()
    {
        ship ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl ??= GetComponent<CoreTacticalWeaponControl>();
        if (ship == null)
        {
            return;
        }

        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        currentForwardSpeedMS = MeasureForwardSpeed();
        blockedByMissionZone = IsInsideMissionZone();
        float targetSpeedMultiplier = CalculateTargetForwardSpeedMultiplier();

        if (CoreTacticalHarpoonLink.HasActiveLinkForShip(ship))
        {
            armed = false;
            active = false;
            currentForwardSpeedMultiplier = Mathf.MoveTowards(
                currentForwardSpeedMultiplier,
                1f,
                Mathf.Max(0.01f, targetSpeedMultiplier - 1f) * deltaSeconds / Mathf.Max(0.1f, rampDownSeconds));
            ApplyState(false, 1f);
            ApplyOverspeedBrake(false, deltaSeconds);
            return;
        }

        if (blockedByMissionZone)
        {
            armed = false;
            active = false;
            currentForwardSpeedMultiplier = Mathf.MoveTowards(
                currentForwardSpeedMultiplier,
                1f,
                Mathf.Max(0.01f, targetSpeedMultiplier - 1f) * deltaSeconds / Mathf.Max(0.1f, rampDownSeconds));
            ApplyState(false, 1f);
            ApplyOverspeedBrake(false, deltaSeconds);
            return;
        }

        if (!armed)
        {
            active = false;
        }
        else if (!active && currentForwardSpeedMS >= Mathf.Max(0f, minimumEngageSpeedMS))
        {
            active = true;
        }

        float targetMultiplier = active ? targetSpeedMultiplier : 1f;
        float rampSeconds = active ? Mathf.Max(0.1f, rampUpSeconds) : Mathf.Max(0.1f, rampDownSeconds);
        currentForwardSpeedMultiplier = Mathf.MoveTowards(
            currentForwardSpeedMultiplier,
            targetMultiplier,
            Mathf.Max(0.01f, targetSpeedMultiplier - 1f) * deltaSeconds / rampSeconds);

        ApplyState(active, active ? currentForwardSpeedMultiplier : 1f);
        ApplyOverspeedBrake(active, deltaSeconds);
        currentForwardSpeedMS = MeasureForwardSpeed();
    }

    public void SetArmed(bool value)
    {
        blockedByMissionZone = IsInsideMissionZone();
        if (value && blockedByMissionZone)
        {
            armed = false;
            active = false;
            ApplyState(false, 1f);
            return;
        }

        armed = value;
        if (!armed)
        {
            active = false;
            ApplyState(false, 1f);
        }
    }

    public bool TrySetArmedWhenAllowed(bool value, out string status)
    {
        status = "";
        if (!value)
        {
            SetArmed(false);
            return true;
        }

        ship ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl ??= GetComponent<CoreTacticalWeaponControl>();
        if (ship == null)
        {
            armed = false;
            active = false;
            ApplyState(false, 1f);
            status = "Claudian slip needs a ship.";
            return false;
        }

        currentForwardSpeedMS = MeasureForwardSpeed();
        blockedByMissionZone = IsInsideMissionZone();
        if (blockedByMissionZone)
        {
            armed = false;
            active = false;
            ApplyState(false, 1f);
            status = "Claudian slip cannot engage inside the mission zone. Move beyond the boundary first.";
            return false;
        }

        if (currentForwardSpeedMS + 0.05f < Mathf.Max(0f, minimumEngageSpeedMS))
        {
            armed = false;
            active = false;
            ApplyState(false, 1f);
            status = "Claudian slip needs " + minimumEngageSpeedMS.ToString("0") + " m/s to engage.";
            return false;
        }

        armed = true;
        return true;
    }

    public void EnterActiveSlipAtFullSpeed(Vector3 forwardDirection)
    {
        ship ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl ??= GetComponent<CoreTacticalWeaponControl>();
        blockedByMissionZone = IsInsideMissionZone();
        if (ship == null || blockedByMissionZone)
        {
            armed = false;
            active = false;
            currentForwardSpeedMultiplier = 1f;
            ApplyState(false, 1f);
            return;
        }

        armed = true;
        active = true;
        currentForwardSpeedMultiplier = CalculateTargetForwardSpeedMultiplier();
        ApplyState(true, currentForwardSpeedMultiplier);

        if (ship.Body != null)
        {
            Vector3 direction = FlattenDirection(forwardDirection, ship.transform.forward);
            Vector3 currentVelocity = ship.Body.linearVelocity;
            Vector3 slipVelocity = direction * Mathf.Max(0f, targetForwardSpeedMS);
            slipVelocity.y = currentVelocity.y;
            ship.Body.linearVelocity = slipVelocity;
            currentForwardSpeedMS = Mathf.Max(0f, Vector3.Dot(slipVelocity, direction));
        }
    }

    private float MeasureForwardSpeed()
    {
        if (ship == null || ship.Body == null)
        {
            return 0f;
        }

        Vector3 flatVelocity = ship.Body.linearVelocity;
        flatVelocity.y = 0f;
        Vector3 forward = ship.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return flatVelocity.magnitude;
        }

        return Mathf.Max(0f, Vector3.Dot(flatVelocity, forward.normalized));
    }

    private float CalculateTargetForwardSpeedMultiplier()
    {
        if (ship == null)
        {
            return 1f;
        }

        float baseSpeed = Mathf.Max(0.01f, ship.maxForwardSpeedMS);
        return Mathf.Max(1f, Mathf.Max(0f, targetForwardSpeedMS) / baseSpeed);
    }

    private void ApplyOverspeedBrake(bool useSlipTargetSpeed, float deltaSeconds)
    {
        if (ship == null || ship.Body == null)
        {
            return;
        }

        float normalMaxSpeed = Mathf.Max(0f, ship.maxForwardSpeedMS);
        float targetSpeed = useSlipTargetSpeed
            ? Mathf.Max(normalMaxSpeed, targetForwardSpeedMS)
            : normalMaxSpeed;
        Vector3 velocity = ship.Body.linearVelocity;
        Vector3 flatVelocity = velocity;
        flatVelocity.y = 0f;
        float flatSpeed = flatVelocity.magnitude;
        if (flatSpeed <= targetSpeed + 0.05f)
        {
            return;
        }

        float brakeSeconds = Mathf.Max(0.1f, overspeedBrakeSeconds);
        float brakeAccelerationMS2 = Mathf.Max(
            0.01f,
            (Mathf.Max(normalMaxSpeed, targetForwardSpeedMS) - normalMaxSpeed) / brakeSeconds);
        float nextSpeed = Mathf.Max(targetSpeed, flatSpeed - brakeAccelerationMS2 * deltaSeconds);
        Vector3 nextFlatVelocity = flatVelocity.normalized * nextSpeed;
        velocity.x = nextFlatVelocity.x;
        velocity.z = nextFlatVelocity.z;
        ship.Body.linearVelocity = velocity;
    }

    private void ApplyState(bool suppressWeapons, float speedMultiplier)
    {
        if (ship != null)
        {
            ship.forwardSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
            ship.forwardAccelerationMultiplier = Mathf.Max(1f, speedMultiplier);
        }

        if (weaponControl != null)
        {
            weaponControl.fireSuppressed = suppressWeapons;
        }
    }

    private bool IsInsideMissionZone()
    {
        if (!requireOutsideMissionZone || missionZoneRadiusMeters <= 0f || ship == null)
        {
            return false;
        }

        Vector3 delta = ship.transform.position - missionZoneCenter;
        delta.y = 0f;
        return delta.magnitude <= missionZoneRadiusMeters;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        fallback.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }
}
