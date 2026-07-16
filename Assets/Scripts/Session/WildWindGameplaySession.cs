using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class GameplaySessionAccountData
{
    public const int CurrentVersion = 1;
    public const string DefaultAccountId = "runtime_account";
    public const string DefaultPlayerShipId = "player_ship";
    public const string DefaultStarterHullId = "starter_airship_hull";
    public const string DefaultDockId = "capital";

    private static readonly Vector3 StarterDockPosition = new Vector3(-520f, 2615f, -360f);
    private static readonly Quaternion StarterRotation = Quaternion.Euler(0f, 42f, 0f);

    public int version = CurrentVersion;
    public string accountId = DefaultAccountId;
    public string playerShipId = DefaultPlayerShipId;
    public string shipHullId = DefaultStarterHullId;
    public GameSessionMode mode = GameSessionMode.Docked;
    public string dockId = DefaultDockId;
    public bool hasPlayerPose = true;
    public Vector3 playerPosition;
    public Quaternion playerRotation = Quaternion.identity;

    public bool IsUsable => version > 0 && hasPlayerPose && !string.IsNullOrWhiteSpace(playerShipId);

    public static GameplaySessionAccountData CreateInitial(string accountId, PlayerProgress progress)
    {
        GameplaySessionAccountData data = new GameplaySessionAccountData
        {
            version = CurrentVersion,
            accountId = ResolveAccountId(accountId),
            playerShipId = DefaultPlayerShipId,
            shipHullId = ResolveShipHullId(progress),
            mode = progress != null ? progress.currentMode : GameSessionMode.Docked,
            dockId = ResolveDockId(progress),
            hasPlayerPose = true,
            playerPosition = ResolveInitialPlayerPosition(progress),
            playerRotation = ResolveInitialPlayerRotation(progress)
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

        accountId = ResolveAccountId(accountId);
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

    }

    public static Vector3 ResolveStarterDockPosition(string dockId)
    {
        return StarterDockPosition;
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

    private static string ResolveAccountId(string candidate)
    {
        return string.IsNullOrWhiteSpace(candidate) ? DefaultAccountId : candidate.Trim();
    }

    private static Vector3 ResolveInitialPlayerPosition(PlayerProgress progress)
    {
        if (progress != null && progress.currentMode == GameSessionMode.Flight && progress.hasCurrentFlightPose)
        {
            return progress.currentFlightPosition;
        }

        if (progress != null && progress.hasCurrentDockPosition)
        {
            return progress.currentDockPosition;
        }

        return ResolveStarterDockPosition(ResolveDockId(progress));
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
    private const string PlayerFocusObjectName = "Player Session Focus";

    [Header("References")]
    [SerializeField, InspectorName("Meta Game State")] private MetaGameState meta;
    [SerializeField, InspectorName("Player Focus")] private Transform focus;
    [SerializeField, InspectorName("Player Ship Root")] private Transform playerShipRoot;

    [Header("Session")]
    [SerializeField, InspectorName("Account Id")] private string accountId = GameplaySessionAccountData.DefaultAccountId;
    [SerializeField, InspectorName("Player Ship Id")] private string playerShipId = GameplaySessionAccountData.DefaultPlayerShipId;
    [SerializeField, InspectorName("Ship Hull Id")] private string shipHullId = GameplaySessionAccountData.DefaultStarterHullId;
    [SerializeField, InspectorName("Mode")] private GameSessionMode mode = GameSessionMode.Docked;
    [SerializeField, InspectorName("Dock Id")] private string dockId = GameplaySessionAccountData.DefaultDockId;
    [SerializeField, InspectorName("Initialized")] private bool initialized;

    public bool IsReady => initialized && focus != null && playerShipRoot != null;
    public string AccountId => accountId;
    public string PlayerShipId => playerShipId;
    public string ShipHullId => shipHullId;
    public GameSessionMode CurrentMode => mode;
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
        EnsureForCurrentSessionScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForCurrentSessionScene();
    }

    public static WildWindGameplaySession EnsureSessionForLoadedGameplayScene(MetaGameState metaGameState, string accountId)
    {
        WildWindGameplaySession session = EnsureForCurrentSessionScene();
        if (session == null)
        {
            return null;
        }

        WildWindGameplayBootstrap.EnsureGameplayBindings(metaGameState);
        session.Configure(metaGameState, accountId);
        session.InitializeFromCurrentStateIfNeeded();
        session.RefreshFromMetaProgress();
        return session;
    }

    private static WildWindGameplaySession EnsureForCurrentSessionScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
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

    public void Configure(MetaGameState metaGameState, string accountId)
    {
        meta = metaGameState != null ? metaGameState : meta;
        this.accountId = ResolveAccountId(accountId);
        ResolveReferences();
    }

    public void InitializeFromCurrentStateIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        ResolveReferences();
        PlayerProgress progress = meta != null ? meta.progress : null;
        ApplyAccountData(GameplaySessionAccountData.CreateInitial(accountId, progress));
    }

    public void ApplyAccountData(GameplaySessionAccountData data)
    {
        ResolveReferences();
        if (data == null || !data.IsUsable)
        {
            data = GameplaySessionAccountData.CreateInitial(accountId, meta != null ? meta.progress : null);
        }

        data.Normalize();
        accountId = ResolveAccountId(data.accountId);
        data.accountId = accountId;
        playerShipId = data.playerShipId;
        shipHullId = data.shipHullId;
        mode = data.mode;
        dockId = data.dockId;

        EnsurePlayerShipRoot();
        ApplyPose(data.playerPosition, data.playerRotation);
        SyncToMetaProgress();
        initialized = true;
    }

    public GameplaySessionAccountData CreateAccountData()
    {
        ResolveReferences();
        EnsurePlayerShipRoot();
        SyncFromMetaProgress();
        FollowFocus();

        GameplaySessionAccountData data = new GameplaySessionAccountData
        {
            version = GameplaySessionAccountData.CurrentVersion,
            accountId = ResolveAccountId(accountId),
            playerShipId = string.IsNullOrWhiteSpace(playerShipId) ? GameplaySessionAccountData.DefaultPlayerShipId : playerShipId,
            shipHullId = string.IsNullOrWhiteSpace(shipHullId) ? GameplaySessionAccountData.DefaultStarterHullId : shipHullId,
            mode = mode,
            dockId = string.IsNullOrWhiteSpace(dockId) ? GameplaySessionAccountData.DefaultDockId : dockId,
            hasPlayerPose = true,
            playerPosition = PlayerPosition,
            playerRotation = PlayerRotation
        };

        data.Normalize();
        return data;
    }

    public bool TryDockAtCurrentDock(out string reason)
    {
        return TryDockAt(dockId, PlayerPosition, out reason);
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
        return TryDockAt(dock.dockId, dock.DockPosition, out reason);
    }

    public bool TryDockAt(string targetDockId, Vector3 dockPosition, out string reason)
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

        string resolvedDockId = string.IsNullOrWhiteSpace(targetDockId) ? GameplaySessionAccountData.DefaultDockId : targetDockId;
        EnsurePlayerShipRoot();
        ApplyPose(dockPosition, PlayerRotation);

        if (!meta.DockAt(resolvedDockId))
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
            meta.progress.SetDocked(dockId, PlayerPosition);
            return;
        }

        meta.progress.SetFlight();
        meta.progress.SetFlightPose(PlayerPosition, PlayerRotation);
    }

    public void RefreshFromMetaProgress(bool applyPose = false)
    {
        ResolveReferences();
        EnsurePlayerShipRoot();
        SyncFromMetaProgress();
        if (applyPose && meta != null && meta.progress != null)
        {
            if (mode == GameSessionMode.Flight && meta.progress.hasCurrentFlightPose)
            {
                ApplyPose(meta.progress.currentFlightPosition, meta.progress.currentFlightRotation);
            }
            else if (mode == GameSessionMode.Docked && meta.progress.hasCurrentDockPosition)
            {
                ApplyPose(meta.progress.currentDockPosition, PlayerRotation);
            }
        }
    }

    public void RefreshRuntimeBubble()
    {
    }

    private void SyncFromMetaProgress()
    {
        if (meta == null || meta.progress == null)
        {
            return;
        }

        accountId = ResolveAccountId(accountId);
        mode = meta.progress.currentMode;
        dockId = string.IsNullOrWhiteSpace(meta.progress.currentDockId) ? GameplaySessionAccountData.DefaultDockId : meta.progress.currentDockId;
        if (!string.IsNullOrWhiteSpace(meta.progress.selectedHullId))
        {
            shipHullId = meta.progress.selectedHullId;
        }
    }

    private static string ResolveAccountId(string candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate.Trim();
        }

        return GameplaySessionAccountData.DefaultAccountId;
    }

    private void ResolveReferences()
    {
        if (meta == null)
        {
            meta = FindFirstObjectByType<MetaGameState>();
        }

        if (focus == null)
        {
            GameObject existingFocus = GameObject.Find(PlayerFocusObjectName);
            if (existingFocus == null)
            {
                existingFocus = new GameObject(PlayerFocusObjectName);
                existingFocus.transform.position = GameplaySessionAccountData.ResolveStarterDockPosition(GameplaySessionAccountData.DefaultDockId);
            }

            focus = existingFocus.transform;
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
            existing = new GameObject(PlayerShipObjectName);
            existing.transform.position = GameplaySessionAccountData.ResolveStarterDockPosition(GameplaySessionAccountData.DefaultDockId);
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
