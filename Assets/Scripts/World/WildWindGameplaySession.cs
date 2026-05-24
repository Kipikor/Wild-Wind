using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class GameplaySessionSaveData
{
    public const int CurrentVersion = 1;
    public const string DefaultPlayerShipId = "player_ship";
    public const string DefaultStarterHullId = "starter_hull";
    public const string DefaultDockId = "Island1";

    private static readonly Vector3 StarterDockOffset = new Vector3(-520f, 115f, -360f);
    private static readonly Quaternion StarterRotation = Quaternion.Euler(0f, 42f, 0f);

    public int version = CurrentVersion;
    public string selectedSaveFileName = "";
    public string playerShipId = DefaultPlayerShipId;
    public string shipHullId = DefaultStarterHullId;
    public GameSessionMode mode = GameSessionMode.Docked;
    public DockingLocationKind dockKind = DockingLocationKind.Island;
    public string dockId = DefaultDockId;
    public bool hasPlayerPose = true;
    public Vector3 playerPosition;
    public Quaternion playerRotation = Quaternion.identity;
    public long savedUtcTicks;

    public bool IsUsable => version > 0 && hasPlayerPose && !string.IsNullOrWhiteSpace(playerShipId);

    public static GameplaySessionSaveData CreateInitial(WorldManifestData manifest, string saveFileName, PlayerProgress progress)
    {
        GameplaySessionSaveData data = new GameplaySessionSaveData
        {
            version = CurrentVersion,
            selectedSaveFileName = saveFileName ?? "",
            playerShipId = DefaultPlayerShipId,
            shipHullId = ResolveShipHullId(progress),
            mode = progress != null ? progress.currentMode : GameSessionMode.Docked,
            dockKind = progress != null ? progress.currentDockKind : DockingLocationKind.Island,
            dockId = ResolveDockId(progress),
            hasPlayerPose = true,
            playerPosition = ResolveInitialPlayerPosition(manifest, progress),
            playerRotation = ResolveInitialPlayerRotation(progress),
            savedUtcTicks = DateTime.UtcNow.Ticks
        };

        data.Normalize();
        return data;
    }

    public void Normalize()
    {
        if (version <= 0)
        {
            version = CurrentVersion;
        }

        selectedSaveFileName ??= "";
        playerShipId = string.IsNullOrWhiteSpace(playerShipId) ? DefaultPlayerShipId : playerShipId.Trim();
        shipHullId = string.IsNullOrWhiteSpace(shipHullId) ? DefaultStarterHullId : shipHullId.Trim();
        dockId = string.IsNullOrWhiteSpace(dockId) ? DefaultDockId : dockId.Trim();
        if (!hasPlayerPose)
        {
            playerPosition = Vector3.zero;
        }

        if (playerRotation.x == 0f && playerRotation.y == 0f && playerRotation.z == 0f && playerRotation.w == 0f)
        {
            playerRotation = StarterRotation;
        }

        if (savedUtcTicks <= 0)
        {
            savedUtcTicks = DateTime.UtcNow.Ticks;
        }
    }

    public static Vector3 ResolveStarterDockPosition(WorldManifestData manifest, string dockId)
    {
        string targetDockId = string.IsNullOrWhiteSpace(dockId) ? DefaultDockId : dockId;
        if (manifest != null && manifest.islands != null)
        {
            for (int i = 0; i < manifest.islands.Count; i++)
            {
                WorldRegionRuntime.WorldIslandRecord island = manifest.islands[i];
                if (island != null && island.id == targetDockId)
                {
                    return island.positionMeters + StarterDockOffset;
                }
            }
        }

        return new Vector3(-520f, 2615f, -360f);
    }

    private static string ResolveShipHullId(PlayerProgress progress)
    {
        return progress != null && !string.IsNullOrWhiteSpace(progress.selectedHullId)
            ? progress.selectedHullId
            : DefaultStarterHullId;
    }

    private static string ResolveDockId(PlayerProgress progress)
    {
        return progress != null && !string.IsNullOrWhiteSpace(progress.currentDockId)
            ? progress.currentDockId
            : DefaultDockId;
    }

    private static Vector3 ResolveInitialPlayerPosition(WorldManifestData manifest, PlayerProgress progress)
    {
        if (progress != null && progress.currentMode == GameSessionMode.Flight && progress.hasCurrentFlightPose)
        {
            return progress.currentFlightPosition;
        }

        if (progress != null && progress.hasCurrentDockPosition)
        {
            return progress.currentDockPosition;
        }

        return ResolveStarterDockPosition(manifest, ResolveDockId(progress));
    }

    private static Quaternion ResolveInitialPlayerRotation(PlayerProgress progress)
    {
        if (progress != null && progress.currentMode == GameSessionMode.Flight && progress.hasCurrentFlightPose)
        {
            return progress.currentFlightRotation;
        }

        return StarterRotation;
    }
}

[DisallowMultipleComponent]
public sealed class WildWindGameplaySession : MonoBehaviour
{
    public const string SessionObjectName = "Wild Wind Gameplay Session";
    private const string PlayerShipObjectName = "Player Session Ship";
    private const string LegacyPlayerShipProxyName = "Player Ship Proxy";

    [Header("References")]
    [SerializeField, InspectorName("World Runtime")] private WorldRegionRuntime world;
    [SerializeField, InspectorName("World Runtime State")] private WorldRuntimeState runtimeState;
    [SerializeField, InspectorName("Bubble Streamer")] private WorldBubbleStreamer streamer;
    [SerializeField, InspectorName("Meta Game State")] private MetaGameState meta;
    [SerializeField, InspectorName("Player Focus")] private Transform focus;
    [SerializeField, InspectorName("Player Ship Root")] private Transform playerShipRoot;

    [Header("Session")]
    [SerializeField, InspectorName("Selected Save File")] private string selectedSaveFileName = "";
    [SerializeField, InspectorName("Player Ship Id")] private string playerShipId = GameplaySessionSaveData.DefaultPlayerShipId;
    [SerializeField, InspectorName("Ship Hull Id")] private string shipHullId = GameplaySessionSaveData.DefaultStarterHullId;
    [SerializeField, InspectorName("Mode")] private GameSessionMode mode = GameSessionMode.Docked;
    [SerializeField, InspectorName("Dock Kind")] private DockingLocationKind dockKind = DockingLocationKind.Island;
    [SerializeField, InspectorName("Dock Id")] private string dockId = GameplaySessionSaveData.DefaultDockId;
    [SerializeField, InspectorName("Initialized")] private bool initialized;

    public bool IsReady => initialized && world != null && focus != null && playerShipRoot != null;
    public string SelectedSaveFileName => selectedSaveFileName;
    public string PlayerShipId => playerShipId;
    public string ShipHullId => shipHullId;
    public GameSessionMode CurrentMode => mode;
    public DockingLocationKind CurrentDockKind => dockKind;
    public string CurrentDockId => dockId;
    public Transform PlayerShipRoot => playerShipRoot;
    public Vector3 PlayerPosition => playerShipRoot != null ? playerShipRoot.position : focus != null ? focus.position : Vector3.zero;
    public Quaternion PlayerRotation => playerShipRoot != null ? playerShipRoot.rotation : Quaternion.identity;
    public string LastActionMessage { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallGameplaySessionBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForCurrentWorldScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForCurrentWorldScene();
    }

    public static WildWindGameplaySession EnsureSessionForLoadedWorld(MetaGameState metaGameState, string saveFileName)
    {
        WildWindGameplaySession session = EnsureForCurrentWorldScene();
        if (session == null)
        {
            return null;
        }

        session.Configure(metaGameState, saveFileName);
        session.InitializeFromCurrentStateIfNeeded();
        return session;
    }

    private static WildWindGameplaySession EnsureForCurrentWorldScene()
    {
        if (FindFirstObjectByType<WorldRegionRuntime>() == null)
        {
            return null;
        }

        WildWindGameplaySession existing = FindFirstObjectByType<WildWindGameplaySession>();
        if (existing != null)
        {
            return existing;
        }

        GameObject sessionObject = new GameObject(SessionObjectName);
        return sessionObject.AddComponent<WildWindGameplaySession>();
    }

    private void Awake()
    {
        ResolveReferences();
        InitializeFromCurrentStateIfNeeded();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsurePlayerShipRoot();
        FollowFocus();
    }

    public void Configure(MetaGameState metaGameState, string saveFileName)
    {
        meta = metaGameState != null ? metaGameState : meta;
        selectedSaveFileName = saveFileName ?? selectedSaveFileName;
        ResolveReferences();
    }

    public void InitializeFromCurrentStateIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        ResolveReferences();
        WorldManifestData manifest = world != null ? WorldManifestData.FromRuntime(world, "session_runtime") : null;
        PlayerProgress progress = meta != null ? meta.progress : null;
        ApplySaveData(GameplaySessionSaveData.CreateInitial(manifest, selectedSaveFileName, progress));
    }

    public void ApplySaveData(GameplaySessionSaveData data)
    {
        ResolveReferences();
        if (data == null || !data.IsUsable)
        {
            WorldManifestData manifest = world != null ? WorldManifestData.FromRuntime(world, "session_runtime") : null;
            data = GameplaySessionSaveData.CreateInitial(manifest, selectedSaveFileName, meta != null ? meta.progress : null);
        }

        data.Normalize();
        selectedSaveFileName = data.selectedSaveFileName;
        playerShipId = data.playerShipId;
        shipHullId = data.shipHullId;
        mode = data.mode;
        dockKind = data.dockKind;
        dockId = data.dockId;

        EnsurePlayerShipRoot();
        ApplyPose(data.playerPosition, data.playerRotation);
        SyncToMetaProgress();
        initialized = true;
    }

    public GameplaySessionSaveData CreateSaveData()
    {
        ResolveReferences();
        EnsurePlayerShipRoot();
        SyncFromMetaProgress();
        FollowFocus();

        GameplaySessionSaveData data = new GameplaySessionSaveData
        {
            version = GameplaySessionSaveData.CurrentVersion,
            selectedSaveFileName = selectedSaveFileName ?? "",
            playerShipId = string.IsNullOrWhiteSpace(playerShipId) ? GameplaySessionSaveData.DefaultPlayerShipId : playerShipId,
            shipHullId = string.IsNullOrWhiteSpace(shipHullId) ? GameplaySessionSaveData.DefaultStarterHullId : shipHullId,
            mode = mode,
            dockKind = dockKind,
            dockId = string.IsNullOrWhiteSpace(dockId) ? GameplaySessionSaveData.DefaultDockId : dockId,
            hasPlayerPose = true,
            playerPosition = PlayerPosition,
            playerRotation = PlayerRotation,
            savedUtcTicks = DateTime.UtcNow.Ticks
        };

        data.Normalize();
        return data;
    }

    public bool TryBeginFreeFlight(out string reason)
    {
        reason = "";
        ResolveReferences();
        InitializeFromCurrentStateIfNeeded();

        if (mode == GameSessionMode.Flight)
        {
            reason = "Session is already in flight.";
            return true;
        }

        if (meta == null)
        {
            reason = "MetaGameState is missing.";
            LastActionMessage = reason;
            return false;
        }

        if (!meta.BeginFreeFlight())
        {
            reason = "MetaGameState rejected free flight.";
            LastActionMessage = reason;
            return false;
        }

        RefreshFromMetaProgress();
        EnsurePlayerShipRoot();
        FollowFocus();
        RefreshRuntimeBubble();
        reason = "Free flight started.";
        LastActionMessage = reason;
        return true;
    }

    public bool TryDockAtCurrentDock(out string reason)
    {
        return TryDockAt(dockId, dockKind, PlayerPosition, out reason);
    }

    public bool TryDockAtNearestAvailableDock(out string reason)
    {
        reason = "";
        DockingPort dock = FindNearestAvailableDock(PlayerPosition);
        if (dock == null)
        {
            reason = "No available dock in range.";
            LastActionMessage = reason;
            return false;
        }

        return TryDockAt(dock.dockId, dock.kind, dock.DockPosition, out reason);
    }

    public bool TryDockAt(string targetDockId, DockingLocationKind targetDockKind, Vector3 dockPosition, out string reason)
    {
        reason = "";
        ResolveReferences();
        InitializeFromCurrentStateIfNeeded();

        if (meta == null)
        {
            reason = "MetaGameState is missing.";
            LastActionMessage = reason;
            return false;
        }

        string resolvedDockId = string.IsNullOrWhiteSpace(targetDockId) ? GameplaySessionSaveData.DefaultDockId : targetDockId;
        EnsurePlayerShipRoot();
        ApplyPose(dockPosition, PlayerRotation);

        if (!meta.DockAt(resolvedDockId, targetDockKind))
        {
            reason = "MetaGameState rejected docking.";
            LastActionMessage = reason;
            return false;
        }

        RefreshFromMetaProgress();
        ApplyPose(dockPosition, PlayerRotation);
        reason = "Docked at " + resolvedDockId + ".";
        LastActionMessage = reason;
        return true;
    }

    public void SyncToMetaProgress()
    {
        if (meta == null || meta.progress == null)
        {
            return;
        }

        if (mode == GameSessionMode.Docked)
        {
            meta.progress.SetDocked(dockId, dockKind, PlayerPosition);
            return;
        }

        meta.progress.SetFlight(meta.progress.activeFlightMissionId);
        meta.progress.SetFlightPose(PlayerPosition, PlayerRotation);
    }

    public void RefreshFromMetaProgress()
    {
        SyncFromMetaProgress();
    }

    public void RefreshRuntimeBubble()
    {
        ResolveReferences();
        if (runtimeState != null && world != null)
        {
            runtimeState.RefreshActiveBubble(PlayerPosition, world.ActiveBubbleRadiusMeters);
        }

        streamer?.RefreshNow();
    }

    private void SyncFromMetaProgress()
    {
        if (meta == null || meta.progress == null)
        {
            return;
        }

        selectedSaveFileName = string.IsNullOrWhiteSpace(selectedSaveFileName) ? meta.EffectiveSaveFileName : selectedSaveFileName;
        mode = meta.progress.currentMode;
        dockKind = meta.progress.currentDockKind;
        dockId = string.IsNullOrWhiteSpace(meta.progress.currentDockId) ? GameplaySessionSaveData.DefaultDockId : meta.progress.currentDockId;
        if (!string.IsNullOrWhiteSpace(meta.progress.selectedHullId))
        {
            shipHullId = meta.progress.selectedHullId;
        }
    }

    private void ResolveReferences()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (runtimeState == null)
        {
            runtimeState = FindFirstObjectByType<WorldRuntimeState>();
        }

        if (streamer == null)
        {
            streamer = FindFirstObjectByType<WorldBubbleStreamer>();
        }

        if (meta == null)
        {
            meta = FindFirstObjectByType<MetaGameState>();
        }

        if (focus == null && world != null)
        {
            focus = world.Focus;
        }
    }

    private void EnsurePlayerShipRoot()
    {
        if (meta != null && meta.shipLoader != null && meta.shipLoader.targetShip != null)
        {
            playerShipRoot = meta.shipLoader.targetShip.transform;
            return;
        }

        if (playerShipRoot != null)
        {
            return;
        }

        GameObject existing = GameObject.Find(PlayerShipObjectName);
        if (existing == null)
        {
            existing = GameObject.Find(LegacyPlayerShipProxyName);
        }

        if (existing == null)
        {
            existing = new GameObject(PlayerShipObjectName);
            BuildFallbackShipVisual(existing.transform);
        }

        playerShipRoot = existing.transform;
    }

    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
        {
            rotation = Quaternion.identity;
        }

        if (playerShipRoot != null)
        {
            Rigidbody body = playerShipRoot.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.transform.SetPositionAndRotation(position, rotation);
                Physics.SyncTransforms();
            }
            else
            {
                playerShipRoot.SetPositionAndRotation(position, rotation);
            }
        }

        if (focus != null)
        {
            focus.position = position;
        }

        RefreshRuntimeBubble();
    }

    private void FollowFocus()
    {
        if (focus == null || playerShipRoot == null)
        {
            return;
        }

        if (meta != null && meta.shipLoader != null && meta.shipLoader.targetShip != null &&
            playerShipRoot == meta.shipLoader.targetShip.transform)
        {
            focus.position = playerShipRoot.position;
            return;
        }

        if ((playerShipRoot.position - focus.position).sqrMagnitude > 0.0001f)
        {
            playerShipRoot.position = focus.position;
        }
    }

    private static void BuildFallbackShipVisual(Transform root)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = new Color(0.52f, 0.42f, 0.29f, 1f),
            hideFlags = HideFlags.DontSave
        };

        CreatePrimitive("Session Balloon", PrimitiveType.Capsule, root, Vector3.zero, new Vector3(120f, 240f, 120f), material, new Vector3(0f, 0f, 90f));
        CreatePrimitive("Session Cabin", PrimitiveType.Cube, root, new Vector3(0f, -115f, 0f), new Vector3(160f, 70f, 95f), material, Vector3.zero);
    }

    private static void CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Vector3 euler)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = Quaternion.Euler(euler);
        primitive.transform.localScale = localScale;

        MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }

    private static DockingPort FindNearestAvailableDock(Vector3 position)
    {
        DockingPort[] docks = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        DockingPort best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < docks.Length; i++)
        {
            DockingPort dock = docks[i];
            if (dock == null || !dock.canEndSession || !dock.Contains(position))
            {
                continue;
            }

            float distance = Vector3.Distance(position, dock.DockPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = dock;
            }
        }

        return best;
    }
}
