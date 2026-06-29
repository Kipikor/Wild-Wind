using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CoreTacticalCombatSortieController : MonoBehaviour
{
    private const string ControllerObjectName = "Core Tactical Combat Sortie";
    private const string TacticalRootName = "Core Tactical Runtime Sortie Root";
    private const int EnemyFrigateCount = 1;
    private const float CommandPlaneAltitudeMeters = 80f;
    private const float PlayerSpawnDistanceMeters = 10000f;
    private const float EnemySpawnMinRadiusMeters = 1200f;
    private const float EnemySpawnMaxRadiusMeters = 3100f;
    private const float ExitDirectionBeyondBoundaryMeters = 16000f;
    private const float TacticalBatteryMax = 100f;

    private static readonly CoreTacticalWeaponGroup[] TacticalHudWeaponGroups =
    {
        CoreTacticalWeaponGroup.MainBattery,
        CoreTacticalWeaponGroup.Secondary76mm,
        CoreTacticalWeaponGroup.Secondary152mm,
        CoreTacticalWeaponGroup.Missiles,
        CoreTacticalWeaponGroup.MachineGuns,
        CoreTacticalWeaponGroup.Autocannon30mm
    };

    public MetaGameState metaGameState;

    private static CoreTacticalCombatSortieController activeInstance;

    private readonly List<CoreTacticalShipMotor> enemyShips = new List<CoreTacticalShipMotor>();
    private readonly List<BehaviourState> suppressedBehaviours = new List<BehaviourState>();
    private readonly List<CameraState> suppressedCameras = new List<CameraState>();
    private readonly List<RendererState> suppressedRenderers = new List<RendererState>();
    private readonly List<ColliderState> suppressedColliders = new List<ColliderState>();
    private readonly List<Rect> missionGuiRects = new List<Rect>();
    private readonly List<TacticalHudAction> hudActions = new List<TacticalHudAction>(16);

    private GameObject tacticalRoot;
    private CoreTacticalFleetController fleet;
    private CoreTacticalShipMotor playerShip;
    private CoreTacticalClaudianSlipDrive playerSlipDrive;
    private ShipPhysics sessionShip;
    private Rigidbody sessionShipBody;
    private bool sessionShipWasEnabled;
    private bool sessionShipSuppressed;
    private bool built;
    private bool extractionUnlocked;
    private bool extractionComplete;
    private bool autoExitRequested;
    private Vector3 exitDirection = Vector3.forward;
    private Vector3 exitPosition;
    private LineRenderer exitLine;
    private Material exitMaterial;
    private string missionStatus = "";
    private int totalEnemies;
    private Vector3 missionCenter;
    private float missionRadiusMeters = 6500f;
    private float tacticalBatteryCurrent = TacticalBatteryMax;
    private int pendingFreightReward;
    private int pendingShipExperienceReward;
    private int pendingReputationReward;
    private ShipTreeEntryConfig playerShipEntry;
    private string playerLoadoutSummary = "2x 30 mm autocannons";

    private GUIStyle hudSmallStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudHeaderStyle;
    private GUIStyle hudCenterStyle;
    private GUIStyle hudTinyCenterStyle;

    private enum RuntimeLoadoutKind
    {
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
        public Color color;
        public string loadoutSummary;
    }

    private enum TacticalHudActionKind
    {
        Weapon,
        Slip,
        Exit
    }

    private struct TacticalHudAction
    {
        public TacticalHudActionKind kind;
        public CoreTacticalWeaponGroup group;
        public string hotkey;
        public string icon;
        public string chargeText;
        public string centerText;
        public bool available;
        public bool disabled;
        public bool active;
        public float shutter;
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
    }

    private void DrawTopStatusStrip()
    {
        float width = Mathf.Min(560f, Mathf.Max(300f, Screen.width - 520f));
        Rect rect = new Rect((Screen.width - width) * 0.5f, 10f, width, 38f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.62f);

        int aliveEnemies = CountAliveEnemies();
        int destroyedEnemies = Mathf.Max(0, totalEnemies - aliveEnemies);
        string phase = extractionUnlocked ? "Extraction" : "Combat";
        string text = phase + " | Enemy ships " + destroyedEnemies + "/" + totalEnemies
            + " | Alt " + (fleet != null ? fleet.CommandPlaneAltitudeMeters.ToString("0") : "0") + " m";
        GUI.Label(new Rect(rect.x + 14f, rect.y + 4f, rect.width - 28f, 16f), text, hudCenterStyle);
        GUI.Label(new Rect(rect.x + 14f, rect.y + 20f, rect.width - 28f, 14f), BuildSlipStatusLine(), hudTinyCenterStyle);
    }

    private void DrawObjectivesPanel()
    {
        Rect rect = new Rect(14f, 14f, 306f, 116f);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.58f);

        int aliveEnemies = CountAliveEnemies();
        int destroyedEnemies = Mathf.Max(0, totalEnemies - aliveEnemies);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 18f), "Objectives", hudHeaderStyle);
        DrawObjectiveLine(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 18f), destroyedEnemies >= totalEnemies, "Destroy enemy ships " + destroyedEnemies + "/" + totalEnemies);
        DrawObjectiveLine(new Rect(rect.x + 12f, rect.y + 54f, rect.width - 24f, 18f), extractionComplete, "Extract in Claudian slip");
        GUI.Label(new Rect(rect.x + 12f, rect.y + 78f, rect.width - 24f, 28f), missionStatus, hudSmallStyle);
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
        const float panelHeight = 142f;
        Rect rect = new Rect((Screen.width - panelWidth) * 0.5f, Screen.height - panelHeight - 14f, panelWidth, panelHeight);
        RegisterGuiRect(rect);
        DrawPanel(rect, 0.64f);

        GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, 220f, 18f), selectedShip.displayName, hudHeaderStyle);
        DrawSelectedShipStats(selectedShip, new Rect(rect.x + 14f, rect.y + 30f, 210f, 100f));

        CoreTacticalWeaponControl weaponControl = selectedShip.GetComponent<CoreTacticalWeaponControl>();
        BuildHudActions(selectedShip, weaponControl);
        DrawActionPanel(new Rect(rect.x + 236f, rect.y + 18f, rect.width - 250f, rect.height - 30f), weaponControl);
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

        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16f), "Speed: " + speed.ToString("0") + " m/s", hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 17f, rect.width, 16f), "Hull: " + GetShipHealthLine(ship), hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 34f, rect.width, 16f), "Battery: " + GetBatteryLine(ship), hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 51f, rect.width, 16f), "Mass: " + massTons.ToString("0") + " t", hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 68f, rect.width, 16f), "Payload: 0 t", hudSmallStyle);
        GUI.Label(new Rect(rect.x, rect.y + 85f, rect.width, 16f), "Profile: " + profileSize.ToString("0") + " m | Detect: 4.2 km", hudSmallStyle);
    }

    private void BuildHudActions(CoreTacticalShipMotor selectedShip, CoreTacticalWeaponControl weaponControl)
    {
        hudActions.Clear();
        if (weaponControl != null)
        {
            for (int i = 0; i < TacticalHudWeaponGroups.Length; i++)
            {
                CoreTacticalWeaponGroup group = TacticalHudWeaponGroups[i];
                int capacity = weaponControl.GetCapacity(group);
                if (capacity <= 0)
                {
                    continue;
                }

                int remaining = weaponControl.GetRemaining(group);
                bool enabled = weaponControl.IsFireEnabled(group);
                hudActions.Add(new TacticalHudAction
                {
                    kind = TacticalHudActionKind.Weapon,
                    group = group,
                    hotkey = GetActionHotkey(group),
                    icon = GetActionIcon(group),
                    chargeText = FormatCompactAmount(remaining),
                    centerText = remaining <= 0 ? "EMPTY" : "",
                    available = remaining > 0 && !weaponControl.fireSuppressed,
                    disabled = !enabled || weaponControl.fireSuppressed,
                    active = false,
                    shutter = remaining <= 0 ? 1f : 0f
                });
            }
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
                ? Mathf.Clamp01((playerSlipDrive.CurrentForwardSpeedMultiplier - 1f) / Mathf.Max(0.1f, playerSlipDrive.targetForwardSpeedMultiplier - 1f))
                : 0f
        });

        hudActions.Add(new TacticalHudAction
        {
            kind = TacticalHudActionKind.Exit,
            hotkey = "Y",
            icon = "EXIT",
            chargeText = "",
            centerText = autoExitRequested ? "RUN" : "",
            available = extractionUnlocked && !extractionComplete,
            disabled = !extractionUnlocked || extractionComplete,
            active = autoExitRequested,
            shutter = autoExitRequested ? 0.5f : 0f
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
        if (GUI.Button(new Rect(rect.x, rect.y, rect.width, rect.height + (!string.IsNullOrWhiteSpace(action.chargeText) ? chargeRect.height + 1f : 0f)), GUIContent.none, GUIStyle.none))
        {
            HandleHudAction(action, weaponControl);
        }

        GUI.enabled = wasEnabled;
    }

    private void HandleHudAction(TacticalHudAction action, CoreTacticalWeaponControl weaponControl)
    {
        switch (action.kind)
        {
            case TacticalHudActionKind.Weapon:
                if (weaponControl != null)
                {
                    weaponControl.ToggleFireEnabled(action.group);
                }

                break;
            case TacticalHudActionKind.Slip:
                ToggleClaudianSlip();
                break;
            case TacticalHudActionKind.Exit:
                BeginAutoExit();
                break;
        }
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
        DrawBar(new Rect(x, y, width, 5f), GetShipHealth01(ship), friendly ? new Color(0.20f, 0.92f, 0.36f, 0.96f) : new Color(1f, 0.14f, 0.10f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
        if (friendly)
        {
            DrawBar(new Rect(x, y + 7f, width, 4f), GetBattery01(ship), new Color(0.20f, 0.56f, 1f, 0.96f), new Color(0f, 0f, 0f, 0.62f));
        }
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
        return ship == null ? 0f : Mathf.Clamp01(tacticalBatteryCurrent / TacticalBatteryMax);
    }

    private string GetBatteryLine(CoreTacticalShipMotor ship)
    {
        return ship == null ? "-- / --" : tacticalBatteryCurrent.ToString("0") + " / " + TacticalBatteryMax.ToString("0");
    }

    private static string GetActionHotkey(CoreTacticalWeaponGroup group)
    {
        return group switch
        {
            CoreTacticalWeaponGroup.MainBattery => "1",
            CoreTacticalWeaponGroup.Secondary76mm => "2",
            CoreTacticalWeaponGroup.Secondary152mm => "3",
            CoreTacticalWeaponGroup.Missiles => "4",
            CoreTacticalWeaponGroup.MachineGuns => "5",
            CoreTacticalWeaponGroup.Autocannon30mm => "1",
            _ => "-"
        };
    }

    private static string GetActionIcon(CoreTacticalWeaponGroup group)
    {
        return group switch
        {
            CoreTacticalWeaponGroup.MainBattery => "406",
            CoreTacticalWeaponGroup.Secondary76mm => "76",
            CoreTacticalWeaponGroup.Secondary152mm => "152",
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
        int aliveEnemies = CountAliveEnemies();
        int destroyedEnemies = Mathf.Max(0, totalEnemies - aliveEnemies);
        string objective = extractionUnlocked
            ? "Exit: use AUTO EXIT to accelerate outward in Claudian slip."
            : "Objective: destroy the enemy ships.";

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
            "Altitude: " + (playerShip != null ? playerShip.transform.position.y.ToString("0") : "0") + " m | Speed: " + speed.ToString("0") + " m/s\n" +
            "Enemy ships destroyed: " + destroyedEnemies + "/" + totalEnemies + "\n" +
            BuildSlipStatusLine() + "\n" +
            objective + "\n" +
            missionStatus;
        Rect rect = new Rect(14f, Screen.height - 156f, 720f, 142f);
        RegisterGuiRect(rect);
        GUI.Box(rect, "");
        GUI.Label(new Rect(26f, Screen.height - 148f, 696f, 124f), text);
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
            int remaining = weaponControl.GetRemaining(group);
            int capacity = weaponControl.GetCapacity(group);
            GUI.Label(new Rect(26f, 108f, 214f, 22f), "Autocannons: " + remaining + " / " + capacity);
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

        GUI.enabled = wasEnabled && extractionUnlocked && !extractionComplete;
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

        built = true;
        SuppressSessionFlightActors();
        ClearLegacySortieRuntimeObjects();
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
        pendingFreightReward = Mathf.Max(0, zone.completionFreightAward);
        pendingShipExperienceReward = Mathf.Max(0, zone.completionDesignExperienceAward);
        pendingReputationReward = 0;
        exitDirection = ResolveExitDirection(zone);
        Vector3 playerPosition = center - exitDirection * PlayerSpawnDistanceMeters;
        playerPosition.y = fleet.commandPlaneAltitudeMeters;
        Quaternion playerRotation = Quaternion.LookRotation(exitDirection, Vector3.up);

        playerShipEntry = ResolveSelectedShipEntry(ResolveMeta());
        RuntimeShipProfile playerProfile = BuildPlayerRuntimeProfile(playerShipEntry);
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
        playerShip.transform.SetParent(tacticalRoot.transform, true);
        ConfigureRuntimeLoadout(playerShip, playerProfile, null, CoreTacticalCombatTeam.Enemy);
        ConfigurePioneerSlipDrive(playerShip);
        playerShip.SetSelected(true);
        playerShip.SetCommand(center, exitDirection);
        if (playerShip.Body != null)
        {
            playerShip.Body.linearVelocity = exitDirection * 220f;
        }

        SpawnEnemyForPlayerProfile(center, playerProfile);
        totalEnemies = enemyShips.Count;
        MirrorSessionShipToTacticalPlayer();
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

        return FindShipTreeEntry(meta, "capital_starter") ?? FindShipTreeEntry(meta, "pioneer");
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

    private RuntimeShipProfile BuildPlayerRuntimeProfile(ShipTreeEntryConfig ship)
    {
        string classId = NormalizeKey(ship != null ? ship.shipClassId : "");
        string roleId = NormalizeKey(ship != null ? ship.roleId : "");
        string shipId = ship != null && !string.IsNullOrWhiteSpace(ship.shipId) ? ship.shipId : "capital_starter";
        string displayName = ship != null && !string.IsNullOrWhiteSpace(ship.DisplayNameRu) ? ship.DisplayNameRu : "Пионер";
        Color color = ship != null && ship.visualColor != default ? ship.visualColor : new Color(0.16f, 0.38f, 0.86f, 1f);

        if (classId == "battleship")
        {
            return new RuntimeShipProfile
            {
                shipId = shipId,
                displayName = displayName,
                classId = classId,
                roleId = roleId,
                loadout = RuntimeLoadoutKind.BattleshipFull,
                hullSizeMeters = new Vector3(62f, 20f, 350f),
                maxForwardSpeedMS = 46f,
                forwardAccelerationMS2 = 5.4f,
                brakingAccelerationMS2 = 9.6f,
                maxYawRateDegPerSecond = 9f,
                maxReverseSpeedMS = 10f,
                maxLateralSpeedMS = 3.2f,
                massKg = 14000000f,
                color = color,
                loadoutSummary = "main battery, PMK, missiles, machine-gun screen"
            };
        }

        if (classId == "cruiser")
        {
            return new RuntimeShipProfile
            {
                shipId = shipId,
                displayName = displayName,
                classId = classId,
                roleId = roleId,
                loadout = RuntimeLoadoutKind.ArtilleryCruiser,
                hullSizeMeters = new Vector3(32f, 12f, 150f),
                maxForwardSpeedMS = 99f,
                forwardAccelerationMS2 = 12.6f,
                brakingAccelerationMS2 = 19.2f,
                maxYawRateDegPerSecond = 18f,
                maxReverseSpeedMS = 21f,
                maxLateralSpeedMS = 9.0f,
                massKg = 3200000f,
                color = color,
                loadoutSummary = "cruiser main battery, PMK, machine guns"
            };
        }

        return new RuntimeShipProfile
        {
            shipId = shipId,
            displayName = displayName,
            classId = string.IsNullOrWhiteSpace(classId) ? "frigate" : classId,
            roleId = roleId,
            loadout = RuntimeLoadoutKind.FrigateAutocannon,
            hullSizeMeters = new Vector3(15f, 7f, 60f),
            maxForwardSpeedMS = 460f,
            forwardAccelerationMS2 = 75f,
            brakingAccelerationMS2 = 95f,
            maxYawRateDegPerSecond = 360f,
            maxReverseSpeedMS = 90f,
            maxLateralSpeedMS = 52f,
            massKg = 520000f,
            color = color,
            loadoutSummary = "30 mm autocannon battery"
        };
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static void ConfigureRuntimeLoadout(
        CoreTacticalShipMotor ship,
        RuntimeShipProfile profile,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam)
    {
        if (ship == null)
        {
            return;
        }

        switch (profile.loadout)
        {
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

    private void ConfigurePioneerWeapons(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return;
        }

        RuntimeShipProfile fallbackProfile = BuildPlayerRuntimeProfile(null);
        ConfigureRuntimeLoadout(ship, fallbackProfile, null, CoreTacticalCombatTeam.Enemy);
    }

    private void ConfigurePioneerSlipDrive(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return;
        }

        playerSlipDrive = ship.gameObject.AddComponent<CoreTacticalClaudianSlipDrive>();
        playerSlipDrive.ship = ship;
        playerSlipDrive.weaponControl = ship.GetComponent<CoreTacticalWeaponControl>();
        playerSlipDrive.minimumEngageSpeedMS = 320f;
        playerSlipDrive.targetForwardSpeedMultiplier = 3.4f;
        playerSlipDrive.rampUpSeconds = 20f;
        playerSlipDrive.rampDownSeconds = 3.0f;
    }

    private void SpawnEnemyForPlayerProfile(Vector3 center, RuntimeShipProfile playerProfile)
    {
        SpawnEnemyFrigates(center);
        if (playerProfile.loadout == RuntimeLoadoutKind.ArtilleryCruiser
            || playerProfile.loadout == RuntimeLoadoutKind.BattleshipFull)
        {
            SpawnEnemyCruisers(center, 1);
        }
    }

    private void SpawnEnemyFrigates(Vector3 center)
    {
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
                190f,
                28f,
                40f,
                145f,
                34f,
                20f,
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
                health.maxHealth = 95f;
                health.ResetHealth();
            }

            CoreTacticalEnemyFrigateOrbitBrain brain = frigate.gameObject.AddComponent<CoreTacticalEnemyFrigateOrbitBrain>();
            brain.target = playerShip;
            brain.orbitRadiusMeters = Random.Range(1500f, 2600f);
            brain.orbitDirection = i % 2 == 0 ? 1f : -1f;
            brain.commandRefreshIntervalSeconds = Random.Range(0.26f, 0.42f);
            brain.orbitLeadDegrees = Random.Range(18f, 36f);

            RuntimeShipProfile enemyProfile = BuildEnemyFrigateProfile();
            ConfigureRuntimeLoadout(frigate, enemyProfile, playerShip, CoreTacticalCombatTeam.Friendly);
            enemyShips.Add(frigate);
        }
    }

    private void SpawnEnemyCruisers(Vector3 center, int count)
    {
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
                102f,
                12.0f,
                19.2f,
                18f,
                21f,
                7.8f,
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
                health.maxHealth = 420f;
                health.ResetHealth();
            }

            CoreTacticalEnemyCruiserBrain brain = cruiser.gameObject.AddComponent<CoreTacticalEnemyCruiserBrain>();
            brain.target = playerShip;

            RuntimeShipProfile enemyProfile = BuildEnemyCruiserProfile();
            ConfigureRuntimeLoadout(cruiser, enemyProfile, playerShip, CoreTacticalCombatTeam.Friendly);
            enemyShips.Add(cruiser);
        }
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
            maxForwardSpeedMS = 190f,
            forwardAccelerationMS2 = 28f,
            brakingAccelerationMS2 = 40f,
            maxYawRateDegPerSecond = 145f,
            maxReverseSpeedMS = 34f,
            maxLateralSpeedMS = 20f,
            massKg = 520000f,
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
            maxForwardSpeedMS = 102f,
            forwardAccelerationMS2 = 12f,
            brakingAccelerationMS2 = 19.2f,
            maxYawRateDegPerSecond = 18f,
            maxReverseSpeedMS = 21f,
            maxLateralSpeedMS = 7.8f,
            massKg = 3200000f,
            color = new Color(0.82f, 0.12f, 0.09f, 1f),
            loadoutSummary = "enemy cruiser artillery"
        };
    }

    private void UpdateMission()
    {
        int aliveEnemies = CountAliveEnemies();
        if (!extractionUnlocked && aliveEnemies <= 0)
        {
            UnlockExtraction();
        }

        if (!extractionUnlocked || extractionComplete || playerShip == null)
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

            missionStatus = "Exit direction is open. Press AUTO EXIT when ready.";
            return;
        }

        CommandPlayerToExit();
        if (playerSlipDrive == null || !playerSlipDrive.IsActive)
        {
            SortieSessionState sortie = ResolveMeta().ActiveSortie;
            if (sortie != null)
            {
                sortie.ResetExtractionRunup();
            }

            missionStatus = "Slip exit needs active Claudian slip. " + BuildSlipSpeedLine();
            return;
        }

        Rigidbody body = playerShip.Body;
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        bool runupReady = ResolveMeta().RecordActiveSortieExtractionRunup(
            playerShip.transform.position,
            velocity,
            playerShip.transform.forward,
            Time.deltaTime,
            true);

        if (!runupReady)
        {
            missionStatus = ResolveMeta().ActiveSortieExtractionRunupStatus;
            return;
        }

        extractionComplete = true;
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
        extractionUnlocked = true;
        SortieZoneDefinition zone = ResolveMeta().ActiveSortie.zone;
        exitDirection = ResolveExitDirection(zone);
        Vector3 center = zone.centerPosition;
        center.y = fleet.commandPlaneAltitudeMeters;
        exitPosition = center + exitDirection * (zone.radiusMeters + ExitDirectionBeyondBoundaryMeters);
        exitPosition.y = fleet.commandPlaneAltitudeMeters;
        CreateExitMarker(center);
        missionStatus = "All enemy ships destroyed. Exit direction opened.";
    }

    private void ToggleClaudianSlip()
    {
        if (playerSlipDrive == null || extractionComplete)
        {
            return;
        }

        playerSlipDrive.SetArmed(!playerSlipDrive.IsArmed);
        if (playerSlipDrive.IsArmed)
        {
            if (extractionUnlocked)
            {
                CommandPlayerToExit();
            }

            missionStatus = "Claudian slip armed. Build speed to enter the field.";
        }
        else
        {
            missionStatus = "Claudian slip disabled. Weapons are free again.";
        }
    }

    private void BeginAutoExit()
    {
        if (!extractionUnlocked || extractionComplete || playerShip == null)
        {
            return;
        }

        autoExitRequested = true;
        if (playerSlipDrive != null)
        {
            playerSlipDrive.SetArmed(true);
        }

        CommandPlayerToExit();
        missionStatus = "Auto exit engaged. " + (playerShip != null ? playerShip.displayName : "Ship") + " is aligning to the slip direction.";
    }

    private void CommandPlayerToExit()
    {
        if (playerShip == null)
        {
            return;
        }

        Vector3 forward = exitDirection.sqrMagnitude > 0.0001f ? exitDirection.normalized : playerShip.transform.forward;
        Vector3 commandPosition = playerShip.transform.position + forward * 20000f;
        commandPosition.y = fleet != null ? fleet.CommandPlaneAltitudeMeters : playerShip.transform.position.y;
        playerShip.SetCommand(commandPosition, forward);
    }

    private string BuildSlipStatusLine()
    {
        if (playerSlipDrive == null)
        {
            return "Claudian slip: unavailable";
        }

        string state = playerSlipDrive.IsActive
            ? "active"
            : playerSlipDrive.IsArmed ? "armed" : "off";
        return "Claudian slip: " + state
            + " | speed " + playerSlipDrive.CurrentForwardSpeedMS.ToString("0")
            + "/" + playerSlipDrive.minimumEngageSpeedMS.ToString("0")
            + " m/s | x" + playerSlipDrive.CurrentForwardSpeedMultiplier.ToString("0.0");
    }

    private string BuildSlipSpeedLine()
    {
        if (playerSlipDrive == null)
        {
            return "Speed: -- / --";
        }

        return "Speed: " + playerSlipDrive.CurrentForwardSpeedMS.ToString("0")
            + " / " + playerSlipDrive.minimumEngageSpeedMS.ToString("0")
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
        if (playerShip == null || sessionShip == null)
        {
            return;
        }

        Vector3 position = playerShip.transform.position;
        Quaternion rotation = playerShip.transform.rotation;
        sessionShip.transform.SetPositionAndRotation(position, rotation);
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
        sessionShip = meta != null && meta.shipLoader != null ? meta.shipLoader.targetShip : null;
        if (sessionShip == null)
        {
            sessionShip = FindFirstObjectByType<ShipPhysics>();
        }

        if (sessionShip != null)
        {
            sessionShipSuppressed = true;
            sessionShipWasEnabled = sessionShip.enabled;
            sessionShip.enabled = false;
            sessionShipBody = sessionShip.GetComponent<Rigidbody>();
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

            Renderer[] renderers = sessionShip.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                suppressedRenderers.Add(new RendererState { renderer = renderers[i], enabled = renderers[i].enabled });
                renderers[i].enabled = false;
            }

            Collider[] colliders = sessionShip.GetComponentsInChildren<Collider>(true);
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
        SuppressBehaviour(FindFirstObjectByType<SafeOreSortieController>());
        SuppressBehaviour(FindFirstObjectByType<SortieResourceCacheController>());
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

    private void ClearLegacySortieRuntimeObjects()
    {
        DestroyObjectByName("Safe Ore Sortie Boulders");
        DestroyObjectByName("Sortie Resource Caches");
        DestroyObjectByName("Manual Sortie Encounter");

        MiningFragment[] fragments = FindObjectsByType<MiningFragment>(FindObjectsSortMode.None);
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] != null)
            {
                Destroy(fragments[i].gameObject);
            }
        }
    }

    private static void DestroyObjectByName(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            Destroy(target);
        }
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

        if (sessionShipSuppressed && sessionShip != null)
        {
            MetaGameState meta = ResolveMeta();
            sessionShip.enabled = meta == null || meta.CurrentMode != GameSessionMode.Docked
                ? sessionShipWasEnabled
                : false;
        }

        sessionShipSuppressed = false;
    }

    private void CreateTacticalCamera()
    {
        GameObject cameraObject = new GameObject("Core Tactical Combat Camera");
        cameraObject.transform.SetParent(tacticalRoot.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 65000f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.028f, 0.036f, 0.044f, 1f);
        CoreTacticalCameraRig cameraRig = cameraObject.AddComponent<CoreTacticalCameraRig>();
        cameraRig.fleet = fleet;
        cameraRig.targetCamera = camera;
        cameraRig.distanceMeters = 5200f;
        cameraRig.minDistanceMeters = 90f;
        cameraRig.maxDistanceMeters = 26000f;
        cameraRig.yawDegrees = -138f;
        cameraRig.elevationDegrees = 55f;
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
    public float targetForwardSpeedMultiplier = 3.4f;
    public float rampUpSeconds = 20f;
    public float rampDownSeconds = 3f;

    private bool armed;
    private bool active;
    private float currentForwardSpeedMS;
    private float currentForwardSpeedMultiplier = 1f;

    public bool IsArmed => armed;
    public bool IsActive => active;
    public float CurrentForwardSpeedMS => currentForwardSpeedMS;
    public float CurrentForwardSpeedMultiplier => currentForwardSpeedMultiplier;

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

        if (!armed)
        {
            active = false;
        }
        else if (!active && currentForwardSpeedMS >= Mathf.Max(0f, minimumEngageSpeedMS))
        {
            active = true;
        }

        float targetMultiplier = active ? Mathf.Max(1f, targetForwardSpeedMultiplier) : 1f;
        float rampSeconds = active ? Mathf.Max(0.1f, rampUpSeconds) : Mathf.Max(0.1f, rampDownSeconds);
        currentForwardSpeedMultiplier = Mathf.MoveTowards(
            currentForwardSpeedMultiplier,
            targetMultiplier,
            Mathf.Max(0.01f, targetForwardSpeedMultiplier - 1f) * deltaSeconds / rampSeconds);

        ApplyState(active, currentForwardSpeedMultiplier);
    }

    public void SetArmed(bool value)
    {
        armed = value;
        if (!armed)
        {
            active = false;
            if (weaponControl != null)
            {
                weaponControl.fireSuppressed = false;
            }
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

    private void ApplyState(bool suppressWeapons, float speedMultiplier)
    {
        if (ship != null)
        {
            ship.forwardSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        }

        if (weaponControl != null)
        {
            weaponControl.fireSuppressed = suppressWeapons;
        }
    }
}
