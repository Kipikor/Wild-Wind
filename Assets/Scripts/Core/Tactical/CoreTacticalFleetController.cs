using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class CoreTacticalFleetController : MonoBehaviour
{
    private const float MinCommandDragMeters = 8f;
    private const float MouseClickMaxPixels = 4f;
    private const float SelectionDragPixels = 6f;
    private const float SelectionRaycastMaxDistanceMeters = 60000f;
    private const int RoutePreviewPointCapacity = 24;
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly CoreTacticalWeaponGroup[] WeaponPanelGroups =
    {
        CoreTacticalWeaponGroup.MainBattery,
        CoreTacticalWeaponGroup.Secondary76mm,
        CoreTacticalWeaponGroup.Secondary152mm,
        CoreTacticalWeaponGroup.Missiles,
        CoreTacticalWeaponGroup.MachineGuns,
        CoreTacticalWeaponGroup.Autocannon30mm
    };

    [Header("Command Plane")]
    public float commandPlaneAltitudeMeters = 80f;
    public float minCommandAltitudeMeters = 15f;
    public float maxCommandAltitudeMeters = 220f;
    public float altitudeChangeSpeedMS = 18f;
    public float gridHalfSizeMeters = 1200f;
    public float gridStepMeters = 80f;
    public float gridLineWidthMeters = 0.65f;

    [Header("Formation")]
    public float formationSpacingMeters = 72f;

    [Header("Input")]
    public bool selectAllOnStart = false;
    public bool showPrototypeHud = true;
    public bool restrictSelectionToTeam = false;
    public CoreTacticalCombatTeam selectableTeam = CoreTacticalCombatTeam.Friendly;

    private readonly List<CoreTacticalShipMotor> ships = new List<CoreTacticalShipMotor>();
    private readonly List<LineRenderer> commandLines = new List<LineRenderer>();
    private readonly List<Vector3[]> commandRoutePointBuffers = new List<Vector3[]>();
    private readonly List<GameObject> ghostShips = new List<GameObject>();
    private MeshFilter gridMeshFilter;
    private MeshRenderer gridMeshRenderer;
    private Mesh gridMesh;
    private Material gridMaterial;
    private Material lineMaterial;
    private Material ghostMaterial;
    private Camera mainCamera;
    private bool draftActive;
    private bool draftHoldFacingMode;
    private bool leftMouseDownTracked;
    private bool selectionDragActive;
    private bool priorityTargetDragActive;
    private bool rightMouseDownTracked;
    private bool rightMouseCommandGesture;
    private Vector2 leftMouseDownScreenPosition;
    private Vector2 leftMouseCurrentScreenPosition;
    private Vector2 rightMouseDownScreenPosition;
    private Vector3 draftTarget;
    private Vector3 draftForward = Vector3.forward;
    private float lastGridHalfSize = -1f;
    private float lastGridStep = -1f;
    private float lastGridLineWidth = -1f;
    private Rect weaponControlPanelGuiRect;
    private bool weaponControlPanelVisible;

    public IReadOnlyList<CoreTacticalShipMotor> Ships => ships;
    public float CommandPlaneAltitudeMeters => commandPlaneAltitudeMeters;

    private void Awake()
    {
        mainCamera = Camera.main;
        EnsureMaterials();
        EnsureGrid();
    }

    private void Start()
    {
        if (selectAllOnStart)
        {
            SelectAll();
        }

        ApplyCommandAltitudeToShips();
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        HandleAltitudeInput();
        HandleSelectionInput();
        HandleCommandInput();
        UpdateGrid();
        UpdateCommandLines();
    }

    private void OnGUI()
    {
        if (showPrototypeHud)
        {
            string text =
                "Wild Wind Core Tactical Prototype\n" +
                "WASD / RMB drag: pan camera | MMB drag: tilt camera | Mouse wheel: zoom\n" +
                "LMB click: select ship | LMB drag: box select | Shift+LMB: add/toggle\n" +
                "Ctrl+LMB click/drag enemy: set priority target for selected ships\n" +
                "RMB click: move selected ships nose-first on the command plane\n" +
                "Shift+RMB hold/drag: command final facing and hold it during movement.\n" +
                "Q/E: lower/raise shared command plane | Altitude: " + commandPlaneAltitudeMeters.ToString("0") + " m\n" +
                "The ships use Rigidbody forces for flat and altitude motion; yaw is rate-limited.";
            GUI.Label(new Rect(14f, 14f, 760f, 112f), text);
            DrawSelectedWeaponControls();
        }

        if (leftMouseDownTracked && selectionDragActive)
        {
            DrawSelectionRect(BuildGuiRect(leftMouseDownScreenPosition, leftMouseCurrentScreenPosition), priorityTargetDragActive);
        }
    }

    private void DrawSelectedWeaponControls()
    {
        weaponControlPanelVisible = false;
        if (!TryGetSelectedWeaponControl(out CoreTacticalShipMotor ship, out CoreTacticalWeaponControl weaponControl))
        {
            return;
        }

        const float x = 14f;
        const float y = 132f;
        const float width = 420f;
        const float headerHeight = 28f;
        const float priorityHeight = 24f;
        const float rowHeight = 25f;
        const float padding = 8f;
        int visibleGroupCount = GetVisibleWeaponGroupCount(weaponControl);
        float height = headerHeight + priorityHeight + padding + Mathf.Max(1, visibleGroupCount) * rowHeight + padding;
        weaponControlPanelGuiRect = new Rect(x, y, width, height);
        weaponControlPanelVisible = true;

        GUI.Box(weaponControlPanelGuiRect, "");
        GUI.Label(new Rect(x + padding, y + 6f, width - padding * 2f, 22f), ship.displayName + " weapon control");
        GUI.Label(new Rect(x + padding, y + headerHeight, width - 98f, 20f), "Priority target: " + GetPriorityTargetName(ship));
        if (GUI.Button(new Rect(x + width - 86f, y + headerHeight - 1f, 78f, 20f), "CLEAR"))
        {
            ClearPriorityTarget(ship);
        }

        float rowY = y + headerHeight + priorityHeight + padding;
        for (int i = 0; i < WeaponPanelGroups.Length; i++)
        {
            if (weaponControl.GetCapacity(WeaponPanelGroups[i]) <= 0)
            {
                continue;
            }

            DrawWeaponControlRow(weaponControl, WeaponPanelGroups[i], x + padding, rowY, width - padding * 2f, rowHeight - 3f);
            rowY += rowHeight;
        }
    }

    private static int GetVisibleWeaponGroupCount(CoreTacticalWeaponControl weaponControl)
    {
        if (weaponControl == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < WeaponPanelGroups.Length; i++)
        {
            if (weaponControl.GetCapacity(WeaponPanelGroups[i]) > 0)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawWeaponControlRow(
        CoreTacticalWeaponControl weaponControl,
        CoreTacticalWeaponGroup group,
        float x,
        float y,
        float width,
        float height)
    {
        bool enabled = weaponControl.IsFireEnabled(group);
        int remaining = weaponControl.GetRemaining(group);
        int capacity = weaponControl.GetCapacity(group);
        string ammoText = remaining + " / " + capacity;
        GUI.Label(
            new Rect(x, y + 2f, width - 88f, height),
            CoreTacticalWeaponControl.GetDisplayName(group) + ": " + ammoText);

        Color oldColor = GUI.color;
        if (!enabled)
        {
            GUI.color = new Color(1f, 0.58f, 0.44f, 1f);
        }
        else if (remaining <= 0)
        {
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        }
        else
        {
            GUI.color = new Color(0.62f, 0.90f, 0.62f, 1f);
        }

        if (GUI.Button(new Rect(x + width - 78f, y, 78f, height), enabled ? "FIRE" : "HOLD"))
        {
            weaponControl.ToggleFireEnabled(group);
        }

        GUI.color = oldColor;
    }

    private bool TryGetSelectedWeaponControl(out CoreTacticalShipMotor selectedShip, out CoreTacticalWeaponControl weaponControl)
    {
        selectedShip = null;
        weaponControl = null;
        for (int i = 0; i < ships.Count; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship == null || !ship.IsSelected)
            {
                continue;
            }

            CoreTacticalWeaponControl candidate = ship.GetComponent<CoreTacticalWeaponControl>();
            if (candidate == null)
            {
                continue;
            }

            selectedShip = ship;
            weaponControl = candidate;
            return true;
        }

        return false;
    }

    private bool IsPointerOverWeaponControlPanel(Vector2 screenPosition)
    {
        if (CoreTacticalCombatSortieController.IsPointerOverMissionGui(screenPosition))
        {
            return true;
        }

        if (!weaponControlPanelVisible)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return weaponControlPanelGuiRect.Contains(guiPosition);
    }

    public void RegisterShip(CoreTacticalShipMotor ship)
    {
        if (ship == null || ships.Contains(ship))
        {
            return;
        }

        for (int i = 0; i < ships.Count; i++)
        {
            SetShipCollisionIgnored(ship, ships[i], false);
        }

        ships.Add(ship);
        ship.SetCommandPlaneAltitude(commandPlaneAltitudeMeters);
        EnsureCommandLineCapacity();
        EnsureGhostCapacity();
    }

    public Vector3 GetFleetCenter()
    {
        if (ships.Count == 0)
        {
            return new Vector3(0f, commandPlaneAltitudeMeters, 0f);
        }

        Vector3 total = Vector3.zero;
        int count = 0;
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] == null)
            {
                continue;
            }

            total += ships[i].transform.position;
            count++;
        }

        return count > 0 ? total / count : new Vector3(0f, commandPlaneAltitudeMeters, 0f);
    }

    private void HandleAltitudeInput()
    {
        float axis = 0f;
        if (IsRaiseAltitudePressed()) axis += 1f;
        if (IsLowerAltitudePressed()) axis -= 1f;
        if (Mathf.Abs(axis) <= 0.001f)
        {
            return;
        }

        commandPlaneAltitudeMeters = Mathf.Clamp(
            commandPlaneAltitudeMeters + axis * altitudeChangeSpeedMS * Time.unscaledDeltaTime,
            minCommandAltitudeMeters,
            maxCommandAltitudeMeters);
        ApplyCommandAltitudeToShips();
        if (draftActive)
        {
            draftTarget.y = commandPlaneAltitudeMeters;
        }
    }

    private void HandleSelectionInput()
    {
        if (WasSelectAllPressed())
        {
            SelectAll();
        }

        int slot = ReadNumberSelection();
        if (slot >= 0 && slot < ships.Count)
        {
            SelectOnly(ships[slot]);
        }

        if (WasLeftMousePressed() && TryReadMousePosition(out Vector2 pressPosition))
        {
            if (IsPointerOverWeaponControlPanel(pressPosition))
            {
                leftMouseDownTracked = false;
                selectionDragActive = false;
                priorityTargetDragActive = false;
                return;
            }

            leftMouseDownTracked = true;
            selectionDragActive = false;
            priorityTargetDragActive = IsPriorityTargetCommandPressed() && HasSelectedShips();
            leftMouseDownScreenPosition = pressPosition;
            leftMouseCurrentScreenPosition = pressPosition;
        }

        if (leftMouseDownTracked && IsLeftMousePressed() && TryReadMousePosition(out Vector2 dragPosition))
        {
            leftMouseCurrentScreenPosition = dragPosition;
            if (Vector2.Distance(leftMouseCurrentScreenPosition, leftMouseDownScreenPosition) >= SelectionDragPixels)
            {
                selectionDragActive = true;
            }
        }

        if (!WasLeftMouseReleased())
        {
            return;
        }

        bool hasMousePosition = TryReadMousePosition(out Vector2 mousePosition);
        leftMouseCurrentScreenPosition = hasMousePosition ? mousePosition : leftMouseCurrentScreenPosition;
        bool wasBoxSelection = selectionDragActive && hasMousePosition;
        bool wasTrackedClick = leftMouseDownTracked
            && hasMousePosition
            && Vector2.Distance(mousePosition, leftMouseDownScreenPosition) <= MouseClickMaxPixels;
        bool wasPriorityTargetGesture = priorityTargetDragActive;
        leftMouseDownTracked = false;
        selectionDragActive = false;
        priorityTargetDragActive = false;
        if (mainCamera == null)
        {
            return;
        }

        if (wasBoxSelection)
        {
            if (wasPriorityTargetGesture)
            {
                TryAssignPriorityTargetFromScreenRect(BuildScreenRect(leftMouseDownScreenPosition, mousePosition));
                return;
            }

            ToggleOrSelectShipsInScreenRect(BuildScreenRect(leftMouseDownScreenPosition, mousePosition), IsMultiSelectPressed());
            return;
        }

        if (!wasTrackedClick)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, SelectionRaycastMaxDistanceMeters, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!IsMultiSelectPressed())
            {
                ClearSelection();
            }

            return;
        }

        if (wasPriorityTargetGesture || IsPriorityTargetCommandPressed())
        {
            TryAssignPriorityTargetFromHit(hit);
            return;
        }

        CoreTacticalShipMotor ship = hit.collider != null ? hit.collider.GetComponentInParent<CoreTacticalShipMotor>() : null;
        if (!CanSelectShip(ship))
        {
            if (!IsMultiSelectPressed())
            {
                ClearSelection();
            }

            return;
        }

        if (IsMultiSelectPressed())
        {
            ship.SetSelected(!ship.IsSelected);
        }
        else
        {
            SelectOnly(ship);
        }
    }

    private void HandleCommandInput()
    {
        if (!TryReadMousePosition(out Vector2 mousePosition) || mainCamera == null)
        {
            HideGhosts();
            draftActive = false;
            rightMouseDownTracked = false;
            rightMouseCommandGesture = false;
            return;
        }

        if (WasRightMousePressed())
        {
            rightMouseDownTracked = true;
            rightMouseDownScreenPosition = mousePosition;
            rightMouseCommandGesture = IsHoldFacingCommandPressed();
            if (rightMouseCommandGesture && TryProjectMouseToCommandPlane(mousePosition, out Vector3 point))
            {
                BeginDraftCommand(point, true);
            }
        }

        if (rightMouseDownTracked && IsRightMousePressed())
        {
            bool holdFacingCommand = IsHoldFacingCommandPressed();
            if (rightMouseCommandGesture && draftActive)
            {
                draftHoldFacingMode = draftHoldFacingMode || holdFacingCommand;
                if (TryProjectMouseToCommandPlane(mousePosition, out Vector3 dragPoint))
                {
                    Vector3 drag = dragPoint - draftTarget;
                    drag.y = 0f;
                    if (drag.magnitude >= MinCommandDragMeters)
                    {
                        draftForward = drag.normalized;
                    }
                }

                UpdateDraftGhosts();
            }
        }

        if (WasRightMouseReleased())
        {
            bool wasTrackedClick = rightMouseDownTracked
                && Vector2.Distance(mousePosition, rightMouseDownScreenPosition) <= MouseClickMaxPixels;
            rightMouseDownTracked = false;
            rightMouseCommandGesture = false;
            if (draftActive)
            {
                draftHoldFacingMode = draftHoldFacingMode || IsHoldFacingCommandPressed();
                IssueDraftCommand();
                HideGhosts();
                draftActive = false;
                return;
            }

            if (wasTrackedClick && TryProjectMouseToCommandPlane(mousePosition, out Vector3 clickPoint))
            {
                draftTarget = clickPoint;
                draftForward = ResolveAverageSelectedForward();
                draftHoldFacingMode = false;
                IssueDraftCommand();
            }

            HideGhosts();
        }
    }

    private void BeginDraftCommand(Vector3 point, bool holdFacing)
    {
        draftActive = true;
        draftHoldFacingMode = holdFacing;
        draftTarget = point;
        draftForward = ResolveAverageSelectedForward();
        ShowDraftGhosts();
    }

    private void IssueDraftCommand()
    {
        List<CoreTacticalShipMotor> selectedShips = GetSelectedShips();
        for (int i = 0; i < selectedShips.Count; i++)
        {
            CoreTacticalShipMotor ship = selectedShips[i];
            if (ship == null)
            {
                continue;
            }

            Vector3 formationPosition = GetFormationPosition(i, selectedShips.Count, draftTarget, draftForward);
            Vector3 commandForward = draftForward;
            if (!draftHoldFacingMode)
            {
                commandForward = formationPosition - ship.transform.position;
                commandForward.y = 0f;
                if (commandForward.sqrMagnitude <= 0.0001f)
                {
                    commandForward = ship.transform.forward;
                }
            }

            ship.SetCommand(
                formationPosition,
                commandForward,
                draftHoldFacingMode);
        }
    }

    private void ApplyCommandAltitudeToShips()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                ships[i].SetCommandPlaneAltitude(commandPlaneAltitudeMeters);
            }
        }
    }

    private bool TryProjectMouseToCommandPlane(Vector2 mousePosition, out Vector3 point)
    {
        point = Vector3.zero;
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, commandPlaneAltitudeMeters, 0f));
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        point = ray.GetPoint(enter);
        point.y = commandPlaneAltitudeMeters;
        return true;
    }

    private Vector3 GetFormationPosition(int index, int count, Vector3 center, Vector3 forward)
    {
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 offset;
        if (count <= 1)
        {
            offset = Vector3.zero;
        }
        else if (count == 2)
        {
            offset = index == 0 ? right * -formationSpacingMeters * 0.5f : right * formationSpacingMeters * 0.5f;
        }
        else
        {
            if (index == 0) offset = Vector3.zero;
            else if (index == 1) offset = -right * formationSpacingMeters - forward * formationSpacingMeters;
            else if (index == 2) offset = right * formationSpacingMeters - forward * formationSpacingMeters;
            else offset = -forward * formationSpacingMeters * (index + 1);
        }

        Vector3 result = center + offset;
        result.y = commandPlaneAltitudeMeters;
        return result;
    }

    private List<CoreTacticalShipMotor> GetSelectedShips()
    {
        List<CoreTacticalShipMotor> selectedShips = new List<CoreTacticalShipMotor>();
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null && ships[i].IsSelected)
            {
                selectedShips.Add(ships[i]);
            }
        }

        return selectedShips;
    }

    private bool HasSelectedShips()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null && ships[i].IsSelected)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryAssignPriorityTargetFromHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        CoreTacticalShipMotor targetShip = hit.collider.GetComponentInParent<CoreTacticalShipMotor>();
        CoreTacticalCombatant targetCombatant = targetShip != null ? targetShip.GetComponent<CoreTacticalCombatant>() : null;
        if (targetShip == null || targetCombatant == null || !targetCombatant.IsAlive)
        {
            return false;
        }

        return AssignPriorityTargetToSelectedShips(targetShip);
    }

    private bool TryAssignPriorityTargetFromScreenRect(Rect selectionRect)
    {
        List<CoreTacticalShipMotor> selectedShips = GetSelectedShips();
        if (selectedShips.Count == 0)
        {
            return false;
        }

        CoreTacticalShipMotor aimOrigin = ResolvePriorityAimOrigin(selectedShips);
        if (aimOrigin == null)
        {
            return false;
        }

        CoreTacticalCombatant aimCombatant = aimOrigin.GetComponent<CoreTacticalCombatant>();
        CoreTacticalCombatTeam? ownTeam = aimCombatant != null ? aimCombatant.team : null;
        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        CoreTacticalShipMotor bestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 originPosition = aimOrigin.transform.position;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            CoreTacticalShipMotor candidate = combatant != null ? combatant.ship : null;
            if (candidate == null || !combatant.IsAlive || candidate == aimOrigin)
            {
                continue;
            }

            if (ownTeam.HasValue && combatant.team == ownTeam.Value)
            {
                continue;
            }

            if (!TryGetShipScreenRect(candidate, out Rect candidateRect) || !selectionRect.Overlaps(candidateRect, true))
            {
                continue;
            }

            Vector3 flatDelta = candidate.transform.position - originPosition;
            flatDelta.y = 0f;
            float distanceSqr = flatDelta.sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = candidate;
            }
        }

        return AssignPriorityTargetToSelectedShips(bestTarget);
    }

    private CoreTacticalShipMotor ResolvePriorityAimOrigin(List<CoreTacticalShipMotor> selectedShips)
    {
        if (TryGetSelectedWeaponControl(out CoreTacticalShipMotor weaponShip, out _) && weaponShip != null)
        {
            return weaponShip;
        }

        return selectedShips != null && selectedShips.Count > 0 ? selectedShips[0] : null;
    }

    private bool AssignPriorityTargetToSelectedShips(CoreTacticalShipMotor targetShip)
    {
        CoreTacticalCombatant targetCombatant = targetShip != null ? targetShip.GetComponent<CoreTacticalCombatant>() : null;
        if (targetShip == null || targetCombatant == null || !targetCombatant.IsAlive)
        {
            return false;
        }

        List<CoreTacticalShipMotor> selectedShips = GetSelectedShips();
        bool assigned = false;
        for (int i = 0; i < selectedShips.Count; i++)
        {
            CoreTacticalShipMotor selectedShip = selectedShips[i];
            if (selectedShip == null || selectedShip == targetShip)
            {
                continue;
            }

            CoreTacticalCombatant selectedCombatant = selectedShip.GetComponent<CoreTacticalCombatant>();
            if (selectedCombatant != null && selectedCombatant.team == targetCombatant.team)
            {
                continue;
            }

            CoreTacticalPriorityTargetControl priorityControl = selectedShip.GetComponent<CoreTacticalPriorityTargetControl>();
            if (priorityControl == null)
            {
                priorityControl = selectedShip.gameObject.AddComponent<CoreTacticalPriorityTargetControl>();
            }

            priorityControl.SetPriorityTarget(targetShip);
            assigned = true;
        }

        return assigned;
    }

    private static string GetPriorityTargetName(CoreTacticalShipMotor ship)
    {
        CoreTacticalPriorityTargetControl priorityControl = ship != null ? ship.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        if (priorityControl != null
            && priorityControl.TryGetPriorityTarget(CoreTacticalCombatTeam.Enemy, out CoreTacticalShipMotor priorityTarget)
            && priorityTarget != null)
        {
            return priorityTarget.displayName;
        }

        return "none";
    }

    private static void ClearPriorityTarget(CoreTacticalShipMotor ship)
    {
        CoreTacticalPriorityTargetControl priorityControl = ship != null ? ship.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        if (priorityControl != null)
        {
            priorityControl.ClearPriorityTarget();
        }
    }

    private void SelectAll()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                ships[i].SetSelected(CanSelectShip(ships[i]));
            }
        }
    }

    private void SelectOnly(CoreTacticalShipMotor selectedShip)
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                ships[i].SetSelected(ships[i] == selectedShip && CanSelectShip(ships[i]));
            }
        }
    }

    private void ToggleOrSelectShipsInScreenRect(Rect selectionRect, bool toggleTouched)
    {
        List<CoreTacticalShipMotor> touchedShips = new List<CoreTacticalShipMotor>();
        if (!toggleTouched)
        {
            ClearSelection();
        }

        for (int i = 0; i < ships.Count; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship == null || !TryGetShipScreenRect(ship, out Rect shipRect))
            {
                continue;
            }

            if (selectionRect.Overlaps(shipRect, true))
            {
                if (CanSelectShip(ship))
                {
                    touchedShips.Add(ship);
                }
            }
        }

        if (toggleTouched)
        {
            bool shouldSelectTouched = false;
            for (int i = 0; i < touchedShips.Count; i++)
            {
                if (!touchedShips[i].IsSelected)
                {
                    shouldSelectTouched = true;
                    break;
                }
            }

            for (int i = 0; i < touchedShips.Count; i++)
            {
                touchedShips[i].SetSelected(shouldSelectTouched);
            }
            return;
        }

        for (int i = 0; i < touchedShips.Count; i++)
        {
            touchedShips[i].SetSelected(true);
        }
    }

    private void ClearSelection()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                ships[i].SetSelected(false);
            }
        }
    }

    private bool CanSelectShip(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return false;
        }

        if (!restrictSelectionToTeam)
        {
            return true;
        }

        CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
        return combatant == null || combatant.team == selectableTeam;
    }

    private bool TryGetShipScreenRect(CoreTacticalShipMotor ship, out Rect rect)
    {
        rect = default;
        if (ship == null || mainCamera == null)
        {
            return false;
        }

        Renderer[] shipRenderers = ship.GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < shipRenderers.Length; i++)
        {
            Renderer targetRenderer = shipRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(ship.transform.position);
            if (screenPoint.z <= 0f)
            {
                return false;
            }

            rect = Rect.MinMaxRect(screenPoint.x - 8f, screenPoint.y - 8f, screenPoint.x + 8f, screenPoint.y + 8f);
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
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(corners[i]);
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

        const float paddingPixels = 4f;
        rect = Rect.MinMaxRect(
            minX - paddingPixels,
            minY - paddingPixels,
            maxX + paddingPixels,
            maxY + paddingPixels);
        return true;
    }

    private Vector3 ResolveAverageSelectedForward()
    {
        Vector3 forward = Vector3.zero;
        int count = 0;
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] == null || !ships[i].IsSelected)
            {
                continue;
            }

            Vector3 shipForward = ships[i].transform.forward;
            shipForward.y = 0f;
            forward += shipForward;
            count++;
        }

        if (count == 0 || forward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private void EnsureMaterials()
    {
        if (gridMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            gridMaterial = new Material(shader);
            gridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            SetMaterialColor(gridMaterial, new Color(0.16f, 0.62f, 0.72f, 1f));
        }

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            lineMaterial = new Material(shader);
            SetMaterialColor(lineMaterial, new Color(0.30f, 0.74f, 0.95f, 0.72f));
        }

        if (ghostMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            ghostMaterial = new Material(shader);
            ghostMaterial.SetFloat("_Surface", 1f);
            ghostMaterial.renderQueue = 3000;
            SetMaterialColor(ghostMaterial, new Color(0.95f, 0.78f, 0.26f, 0.34f));
        }
    }

    private void EnsureGrid()
    {
        EnsureMaterials();
        if (gridMeshFilter != null && gridMeshRenderer != null && gridMesh != null)
        {
            return;
        }

        GameObject gridObject = new GameObject("Command Plane Stable Grid");
        gridObject.transform.SetParent(transform, false);
        gridMeshFilter = gridObject.AddComponent<MeshFilter>();
        gridMeshRenderer = gridObject.AddComponent<MeshRenderer>();
        gridMeshRenderer.sharedMaterial = gridMaterial;
        gridMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gridMeshRenderer.receiveShadows = false;
        gridMesh = new Mesh { name = "Command Plane Grid Mesh" };
        gridMesh.MarkDynamic();
        gridMeshFilter.sharedMesh = gridMesh;

        UpdateGrid();
    }

    private void UpdateGrid()
    {
        if (gridMeshFilter == null || gridMeshRenderer == null || gridMesh == null)
        {
            EnsureGrid();
        }

        if (gridMeshFilter == null || gridMeshRenderer == null || gridMesh == null)
        {
            return;
        }

        float safeHalfSize = Mathf.Max(20f, gridHalfSizeMeters);
        float safeStep = Mathf.Max(5f, gridStepMeters);
        float safeLineWidth = Mathf.Clamp(gridLineWidthMeters, 0.05f, safeStep * 0.20f);
        bool rebuild = !Mathf.Approximately(safeHalfSize, lastGridHalfSize)
            || !Mathf.Approximately(safeStep, lastGridStep)
            || !Mathf.Approximately(safeLineWidth, lastGridLineWidth);
        if (rebuild)
        {
            BuildGridMesh(safeHalfSize, safeStep, safeLineWidth);
            lastGridHalfSize = safeHalfSize;
            lastGridStep = safeStep;
            lastGridLineWidth = safeLineWidth;
        }

        gridMeshFilter.transform.position = new Vector3(0f, commandPlaneAltitudeMeters, 0f);
    }

    private void BuildGridMesh(float halfSize, float step, float lineWidth)
    {
        gridMesh.Clear();
        int steps = Mathf.Max(1, Mathf.RoundToInt(halfSize / step));
        int lineCount = (steps * 2 + 1) * 2;
        Vector3[] vertices = new Vector3[lineCount * 4];
        int[] triangles = new int[lineCount * 6];
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = -steps; i <= steps; i++)
        {
            float offset = i * step;
            AddGridQuad(
                vertices,
                triangles,
                ref vertexIndex,
                ref triangleIndex,
                new Vector3(-halfSize, 0f, offset),
                new Vector3(halfSize, 0f, offset),
                lineWidth);
            AddGridQuad(
                vertices,
                triangles,
                ref vertexIndex,
                ref triangleIndex,
                new Vector3(offset, 0f, -halfSize),
                new Vector3(offset, 0f, halfSize),
                lineWidth);
        }

        gridMesh.vertices = vertices;
        gridMesh.triangles = triangles;
        gridMesh.RecalculateBounds();
    }

    private static void AddGridQuad(
        Vector3[] vertices,
        int[] triangles,
        ref int vertexIndex,
        ref int triangleIndex,
        Vector3 start,
        Vector3 end,
        float width)
    {
        Vector3 along = end - start;
        along.y = 0f;
        if (along.sqrMagnitude <= 0.0001f)
        {
            along = Vector3.forward;
        }

        along.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, along).normalized * (width * 0.5f);
        int baseIndex = vertexIndex;
        vertices[vertexIndex++] = start - side;
        vertices[vertexIndex++] = start + side;
        vertices[vertexIndex++] = end + side;
        vertices[vertexIndex++] = end - side;
        triangles[triangleIndex++] = baseIndex;
        triangles[triangleIndex++] = baseIndex + 2;
        triangles[triangleIndex++] = baseIndex + 1;
        triangles[triangleIndex++] = baseIndex;
        triangles[triangleIndex++] = baseIndex + 3;
        triangles[triangleIndex++] = baseIndex + 2;
    }

    private void EnsureCommandLineCapacity()
    {
        EnsureMaterials();
        while (commandLines.Count < ships.Count)
        {
            commandLines.Add(CreateLineRenderer("Ship Command Line " + commandLines.Count, 0.22f));
        }

        while (commandRoutePointBuffers.Count < ships.Count)
        {
            commandRoutePointBuffers.Add(new Vector3[RoutePreviewPointCapacity]);
        }
    }

    private void UpdateCommandLines()
    {
        EnsureCommandLineCapacity();
        for (int i = 0; i < commandLines.Count; i++)
        {
            LineRenderer line = commandLines[i];
            CoreTacticalShipMotor ship = i < ships.Count ? ships[i] : null;
            if (line == null)
            {
                continue;
            }

            bool visible = ship != null && ship.IsSelected;
            line.enabled = visible;
            if (visible)
            {
                Vector3[] routePoints = commandRoutePointBuffers[i];
                int pointCount = ship.BuildRoutePreview(routePoints);
                if (pointCount > 1)
                {
                    SetRoute(line, routePoints, pointCount);
                }
                else
                {
                    SetLine(line, ship.transform.position, ship.TargetPosition);
                }
            }
        }
    }

    private void EnsureGhostCapacity()
    {
        EnsureMaterials();
        while (ghostShips.Count < ships.Count)
        {
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "Core Tactical Command Ghost " + ghostShips.Count;
            Collider collider = ghost.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = ghost.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ghostMaterial;
            }

            ghost.SetActive(false);
            ghostShips.Add(ghost);
        }
    }

    private void ShowDraftGhosts()
    {
        EnsureGhostCapacity();
        UpdateDraftGhosts();
    }

    private void UpdateDraftGhosts()
    {
        List<CoreTacticalShipMotor> selectedShips = GetSelectedShips();
        EnsureGhostCapacity();
        for (int i = 0; i < ghostShips.Count; i++)
        {
            GameObject ghost = ghostShips[i];
            CoreTacticalShipMotor ship = i < selectedShips.Count ? selectedShips[i] : null;
            if (ghost == null)
            {
                continue;
            }

            ghost.SetActive(ship != null);
            if (ship == null)
            {
                continue;
            }

            ghost.transform.position = GetFormationPosition(i, selectedShips.Count, draftTarget, draftForward);
            ghost.transform.rotation = Quaternion.LookRotation(draftForward, Vector3.up);
            ghost.transform.localScale = ship.hullSizeMeters;
        }
    }

    private void HideGhosts()
    {
        for (int i = 0; i < ghostShips.Count; i++)
        {
            if (ghostShips[i] != null)
            {
                ghostShips[i].SetActive(false);
            }
        }
    }

    private LineRenderer CreateLineRenderer(string lineName, float width)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
        renderer.sharedMaterial = lineMaterial;
        renderer.positionCount = 2;
        renderer.useWorldSpace = true;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void SetLine(LineRenderer renderer, Vector3 start, Vector3 end)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.positionCount = 2;
        renderer.SetPosition(0, start);
        renderer.SetPosition(1, end);
    }

    private static void SetRoute(LineRenderer renderer, Vector3[] points, int pointCount)
    {
        if (renderer == null || points == null)
        {
            return;
        }

        int safePointCount = Mathf.Clamp(pointCount, 2, points.Length);
        renderer.positionCount = safePointCount;
        for (int i = 0; i < safePointCount; i++)
        {
            renderer.SetPosition(i, points[i]);
        }
    }

    private static void SetShipCollisionIgnored(CoreTacticalShipMotor first, CoreTacticalShipMotor second, bool ignored)
    {
        if (first == null || second == null || first == second)
        {
            return;
        }

        Collider[] firstColliders = first.GetComponentsInChildren<Collider>();
        Collider[] secondColliders = second.GetComponentsInChildren<Collider>();
        for (int firstIndex = 0; firstIndex < firstColliders.Length; firstIndex++)
        {
            Collider firstCollider = firstColliders[firstIndex];
            if (firstCollider == null)
            {
                continue;
            }

            for (int secondIndex = 0; secondIndex < secondColliders.Length; secondIndex++)
            {
                Collider secondCollider = secondColliders[secondIndex];
                if (secondCollider == null || firstCollider == secondCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(firstCollider, secondCollider, ignored);
            }
        }
    }

    private static Rect BuildScreenRect(Vector2 start, Vector2 end)
    {
        return Rect.MinMaxRect(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y));
    }

    private static Rect BuildGuiRect(Vector2 start, Vector2 end)
    {
        Vector2 guiStart = new Vector2(start.x, Screen.height - start.y);
        Vector2 guiEnd = new Vector2(end.x, Screen.height - end.y);
        return Rect.MinMaxRect(
            Mathf.Min(guiStart.x, guiEnd.x),
            Mathf.Min(guiStart.y, guiEnd.y),
            Mathf.Max(guiStart.x, guiEnd.x),
            Mathf.Max(guiStart.y, guiEnd.y));
    }

    private static void DrawSelectionRect(Rect rect, bool priorityTargetMode)
    {
        Color oldColor = GUI.color;
        Color fillColor = priorityTargetMode
            ? new Color(1f, 0.16f, 0.11f, 0.11f)
            : new Color(0.95f, 0.72f, 0.22f, 0.12f);
        Color borderColor = priorityTargetMode
            ? new Color(1f, 0.16f, 0.11f, 0.95f)
            : new Color(0.95f, 0.72f, 0.22f, 0.95f);
        GUI.color = fillColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = borderColor;
        const float border = 2f;
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - border, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, border, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - border, rect.yMin, border, rect.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorPropertyId)) material.SetColor(BaseColorPropertyId, color);
        if (material.HasProperty(ColorPropertyId)) material.SetColor(ColorPropertyId, color);
        material.color = color;
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

    private static bool IsLeftMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
#else
        return false;
#endif
    }

    private static bool WasLeftMouseReleased()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    private static bool WasRightMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private static bool IsRightMousePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.isPressed;
#else
        return false;
#endif
    }

    private static bool WasRightMouseReleased()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    private static bool IsRaiseAltitudePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.isPressed;
#else
        return false;
#endif
    }

    private static bool IsLowerAltitudePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.qKey.isPressed;
#else
        return false;
#endif
    }

    private static bool IsMultiSelectPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
        return false;
#endif
    }

    private static bool IsPriorityTargetCommandPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#else
        return false;
#endif
    }

    private static bool IsHoldFacingCommandPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
        return false;
#endif
    }

    private static bool WasSelectAllPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && keyboard.aKey.wasPressedThisFrame
            && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#else
        return false;
#endif
    }

    private static int ReadNumberSelection()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return -1;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
#endif
        return -1;
    }
}
