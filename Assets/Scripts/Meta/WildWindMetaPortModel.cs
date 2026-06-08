using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum WildWindMetaPortTab
{
    Port,
    Ships,
    Fitting,
    Sorties,
    Processing,
    Production,
    Technologies
}

public sealed class WildWindMetaPortActionResult
{
    public bool success;
    public string message = "";
}

public sealed class WildWindMetaPortUiState
{
    private static readonly WildWindMetaPortTab[] OrderedTabs =
    {
        WildWindMetaPortTab.Port,
        WildWindMetaPortTab.Ships,
        WildWindMetaPortTab.Fitting,
        WildWindMetaPortTab.Sorties,
        WildWindMetaPortTab.Processing,
        WildWindMetaPortTab.Production,
        WildWindMetaPortTab.Technologies
    };

    private readonly HashSet<WildWindMetaPortTab> visitedTabs = new HashSet<WildWindMetaPortTab>();
    private const string RuntimeShipInstanceId = "runtime_current_ship";
    private const string Cruiser203HullId = "cruiser203_hull";
    private const string Cruiser203PortOptionInstanceId = "runtime_cruiser203_option";

    public WildWindMetaCatalog catalog;
    public MetaAccountState account;
    public MetaGameState runtimeMeta;
    public WildWindMetaPortTab selectedTab = WildWindMetaPortTab.Port;
    public int selectedShipIndex;
    public int selectedSortieIndex;
    public bool runtimeSnapshotApplied;
    public string selectedProcessingInputId = "ognejar_ore";
    public string lastMessage = "Port ready.";

    public bool IsReady => IsRuntimeBound && catalog != null && account != null && account.ships.Count > 0;
    public int TabCount => OrderedTabs.Length;
    public bool HasVisitedAllTabs => visitedTabs.Count >= OrderedTabs.Length;
    public bool IsRuntimeBound => runtimeMeta != null && runtimeMeta.progress != null;
    public string SourceLabel => IsRuntimeBound ? "runtime" : "runtime missing";

    public MetaShipInstance SelectedShip
    {
        get
        {
            if (account == null || account.ships.Count == 0) return null;
            selectedShipIndex = Mathf.Clamp(selectedShipIndex, 0, account.ships.Count - 1);
            return account.ships[selectedShipIndex];
        }
    }

    public static IReadOnlyList<WildWindMetaPortTab> Tabs => OrderedTabs;

    public static WildWindMetaPortUiState CreateFromRuntime(MetaGameState meta)
    {
        WildWindMetaPortUiState state = new WildWindMetaPortUiState();
        state.BindRuntimeMeta(meta);
        state.SelectTab(WildWindMetaPortTab.Port);
        return state;
    }

    public void BindRuntimeMeta(MetaGameState meta)
    {
        runtimeMeta = meta;
        if (runtimeMeta == null || runtimeMeta.progress == null)
        {
            runtimeSnapshotApplied = false;
            lastMessage = "Runtime account unavailable.";
            return;
        }

        EnsureRuntimeViewData();
        ApplyRuntimeSnapshot();
    }

    private void EnsureRuntimeViewData()
    {
        if (catalog == null)
        {
            catalog = WildWindMetaMechanics.CreateMinimalCatalog();
        }

        if (account == null)
        {
            account = WildWindMetaMechanics.CreateFreshAccount(catalog);
        }
    }

    public static string GetTabDisplayName(WildWindMetaPortTab tab)
    {
        return tab switch
        {
            WildWindMetaPortTab.Port => "PORT",
            WildWindMetaPortTab.Ships => "SHIPS",
            WildWindMetaPortTab.Fitting => "FITTING",
            WildWindMetaPortTab.Sorties => "SORTIES",
            WildWindMetaPortTab.Processing => "PROCESSING",
            WildWindMetaPortTab.Production => "PRODUCTION",
            WildWindMetaPortTab.Technologies => "TECH",
            _ => tab.ToString().ToUpperInvariant()
        };
    }

    public bool SelectTab(WildWindMetaPortTab tab)
    {
        selectedTab = tab;
        visitedTabs.Add(tab);
        return true;
    }

    public bool SelectNextShip()
    {
        if (account == null || account.ships.Count == 0) return false;
        selectedShipIndex = (selectedShipIndex + 1) % account.ships.Count;
        lastMessage = "Selected " + SelectedShipLabel + ".";
        return true;
    }

    public bool SelectPreviousShip()
    {
        if (account == null || account.ships.Count == 0) return false;
        selectedShipIndex--;
        if (selectedShipIndex < 0) selectedShipIndex = account.ships.Count - 1;
        lastMessage = "Selected " + SelectedShipLabel + ".";
        return true;
    }

    public string SelectedShipLabel
    {
        get
        {
            MetaShipInstance ship = SelectedShip;
            return ship == null ? "-" : FormatShipName(ship.shipId) + " T" + ship.tier;
        }
    }

    public string BuildResourceStrip()
    {
        if (account == null) return "-";
        return FormatStoredResource("charcoal", "charcoal")
            + " | " + FormatStoredResource("ferron", "ferron")
            + " | " + FormatStoredResource("silvate", "silvate")
            + " | " + FormatStoredResource(SessionExtractionConstants.StarterWeaponCargoItemId, "weapon")
            + " | ships " + account.ships.Count + "/" + account.portSlots
            + " | Source " + SourceLabel;
    }

    public string BuildShipCarousel()
    {
        if (account == null || account.ships.Count == 0) return "No ships.";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < account.ships.Count; i++)
        {
            MetaShipInstance ship = account.ships[i];
            if (i > 0) builder.Append("   ");
            builder.Append(i == selectedShipIndex ? "[ " : "  ");
            builder.Append(FormatShipName(ship.shipId));
            builder.Append(" T");
            builder.Append(ship.tier);
            builder.Append(" ");
            builder.Append(ship.remainingSorties);
            builder.Append("/");
            builder.Append(ship.maxSorties);
            builder.Append(i == selectedShipIndex ? " ]" : "  ");
        }

        return builder.ToString();
    }

    public string BuildSelectedShipSummary()
    {
        MetaShipInstance ship = SelectedShip;
        if (ship == null) return "No selected ship.";
        MetaShipDefinition definition = catalog != null && catalog.ships.TryGetValue(ship.shipId, out MetaShipDefinition found)
            ? found
            : null;
        string builtIn = ship.preinstalledModules.Count > 0 ? string.Join(", ", ship.preinstalledModules) : "-";
        string fitted = ship.fittedModules.Count > 0 ? string.Join(", ", ship.fittedModules) : "-";
        string rigs = ship.rigs.Count > 0 ? string.Join(", ", ship.rigs) : "-";
        return SelectedShipLabel
            + "\nSortie resource: " + ship.remainingSorties + "/" + ship.maxSorties
            + "\nSlots H/M/L/R: "
            + (definition != null ? definition.highSlots + "/" + definition.midSlots + "/" + definition.lowSlots + "/" + definition.rigSlots : "-")
            + "\nBuilt-in: " + builtIn
            + "\nFitted: " + fitted
            + "\nRigs: " + rigs;
    }

    public string BuildTabContent()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(GetTabDisplayName(selectedTab));
        if (IsRuntimeBound)
        {
            builder.AppendLine(BuildRuntimeHeaderLine());
        }

        builder.AppendLine(new string('-', 42));
        switch (selectedTab)
        {
            case WildWindMetaPortTab.Port:
                builder.AppendLine("Session hub: selected ship, base resources, sortie board and contextual actions.");
                builder.AppendLine("Port ships: " + account.ships.Count + "/" + account.portSlots + ".");
                builder.AppendLine("Starter Pioneer recovery: " + (account.ships.Exists(s => s.shipId == "pioneer") ? "present" : "will restore if missing"));
                builder.AppendLine("Cruiser 203: " + (account.ships.Exists(s => IsCruiser203ShipId(s.shipId)) ? "available in carousel" : "not in port"));
                if (IsRuntimeBound)
                {
                    builder.AppendLine("Runtime mode: " + runtimeMeta.CurrentMode + ", base dock: " + runtimeMeta.IsDockedAtCapital());
                }

                break;
            case WildWindMetaPortTab.Ships:
                builder.AppendLine("Session roster contains only playable sortie hulls.");
                builder.AppendLine("Available hulls: Pioneer and Cruiser 203.");
                builder.AppendLine("Current ship can be selected here and applied to the runtime hull chooser.");
                builder.AppendLine(BuildSelectedShipSummary());
                break;
            case WildWindMetaPortTab.Fitting:
                builder.AppendLine("High: crusher, guns, gas extractor, mining hold and salvage rig.");
                builder.AppendLine("Mid: observation, control and support modules.");
                builder.AppendLine("Low/Rig: protection, fuel, storage and hull support.");
                builder.AppendLine("Fire control: safe filters, known target lock and ammo policy are part of the selected fitting.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine(runtimeMeta.GetCoreFittingSummaryText());
                }

                builder.AppendLine(BuildSelectedShipSummary());
                break;
            case WildWindMetaPortTab.Sorties:
                builder.AppendLine("Sortie board selects one isolated session pocket outside the port location.");
                builder.AppendLine("Mining/gas/scanning policies are launch constraints on the selected sortie, not separate port pages.");
                builder.AppendLine("Boulders, clouds, visibility and scan exposure are validated through loadout requirements.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine("Runtime selected: " + runtimeMeta.GetSelectedSessionSortieDisplayName() + " (" + runtimeMeta.GetSelectedSessionSortieRequirementText() + ").");
                }

                builder.AppendLine("Loadout validation checks the selected hull, fittings and required resource branch.");
                break;
            case WildWindMetaPortTab.Processing:
                builder.AppendLine("Five base branches consume recovered sortie cargo and emit whole material units.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine(runtimeMeta.GetBaseProcessingOverviewText());
                }

                builder.AppendLine("Priority: " + selectedProcessingInputId + " first.");
                builder.AppendLine("Raw " + selectedProcessingInputId + ": " + WildWindMetaMechanics.GetStorage(account, selectedProcessingInputId));
                builder.AppendLine("Buffers: charcoal " + GetBuffer("charcoal").ToString("0.00") + ", ferron " + GetBuffer("ferron").ToString("0.00"));
                break;
            case WildWindMetaPortTab.Production:
                builder.AppendLine("Cascade production estimates cost, time and bottleneck before crafting.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine(runtimeMeta.GetBaseCascadeProductionOverviewText());
                    builder.AppendLine(runtimeMeta.GetNextBaseCascadeOrderOverviewText());
                    builder.AppendLine(runtimeMeta.GetNextBaseIndustryUpgradeOverviewText());
                }

                builder.AppendLine("Starter order: ferron + silvate + charcoal -> airframe/module/munition kits.");
                builder.AppendLine("Stored airframe kits: " + WildWindMetaMechanics.GetStorage(account, SessionExtractionConstants.StarterAirframeKitItemId));
                break;
            case WildWindMetaPortTab.Technologies:
                builder.AppendLine("Technologies are researched from base resources in the single account.");
                if (IsRuntimeBound)
                {
                    AppendRuntimeTechnologyOverview(builder);
                }
                else
                {
                    builder.AppendLine("Runtime account unavailable.");
                }
                break;
        }

        builder.AppendLine();
        builder.AppendLine("Primary: " + GetPrimaryActionLabel());
        builder.AppendLine("Secondary: " + GetSecondaryActionLabel());
        builder.AppendLine("Last: " + lastMessage);
        return builder.ToString();
    }

    public string BuildReport()
    {
        return "MetaPort tab=" + GetTabDisplayName(selectedTab)
            + " source=" + SourceLabel
            + " ship=" + SelectedShipLabel
            + " slots=" + (account != null ? account.ships.Count + "/" + account.portSlots : "-")
            + " visited=" + visitedTabs.Count + "/" + OrderedTabs.Length
            + " last=" + lastMessage;
    }

    public string GetPrimaryActionLabel()
    {
        return selectedTab switch
        {
            WildWindMetaPortTab.Port => "Refresh Port",
            WildWindMetaPortTab.Ships => "Next Ship",
            WildWindMetaPortTab.Fitting => "Install Upgrade",
            WildWindMetaPortTab.Sorties => "Select Sortie",
            WildWindMetaPortTab.Processing => "Process Batch",
            WildWindMetaPortTab.Production => "Run Cascade",
            WildWindMetaPortTab.Technologies => "Unlock Tech",
            _ => "Run"
        };
    }

    public string GetSecondaryActionLabel()
    {
        if (selectedTab == WildWindMetaPortTab.Port && IsRuntimeBound)
        {
            return GetRuntimePortShipActionLabel();
        }

        return selectedTab switch
        {
            WildWindMetaPortTab.Port => "Restore Pioneer",
            WildWindMetaPortTab.Ships => "Previous Ship",
            WildWindMetaPortTab.Fitting => "Rig Toggle",
            WildWindMetaPortTab.Sorties => "Validate Loadout",
            WildWindMetaPortTab.Processing => "Set Priority",
            WildWindMetaPortTab.Production => "Estimate Order",
            WildWindMetaPortTab.Technologies => "Upgrade Tech",
            _ => "More"
        };
    }

    public WildWindMetaPortActionResult RunPrimaryAction()
    {
        switch (selectedTab)
        {
            case WildWindMetaPortTab.Port:
                if (!IsRuntimeBound) return RuntimeAccountUnavailable();
                ApplyRuntimeSnapshot();
                return SetResult(true, "Port state refreshed.", "");
            case WildWindMetaPortTab.Ships:
                return SetResult(SelectNextShip(), lastMessage, "No ship to select.");
            case WildWindMetaPortTab.Fitting:
                return RunRuntimeFittingAction() ?? RuntimeAccountUnavailable();
            case WildWindMetaPortTab.Sorties:
                return RunRuntimeSortieSelectionAction() ?? RuntimeAccountUnavailable();
            case WildWindMetaPortTab.Processing:
                return RunRuntimeProcessingAction() ?? RuntimeAccountUnavailable();
            case WildWindMetaPortTab.Production:
                return RunRuntimeCascadeAction() ?? RuntimeAccountUnavailable();
            case WildWindMetaPortTab.Technologies:
                return RunRuntimeTechnologyAction() ?? RuntimeAccountUnavailable();
            default:
                return SetResult(false, "", "Unknown tab.");
        }
    }

    public WildWindMetaPortActionResult RunSecondaryAction()
    {
        switch (selectedTab)
        {
            case WildWindMetaPortTab.Port:
                return RunRuntimePortShipSelectionAction() ?? RuntimeAccountUnavailable();
            case WildWindMetaPortTab.Ships:
                return SetResult(SelectPreviousShip(), lastMessage, "No ship to select.");
            case WildWindMetaPortTab.Fitting:
                return ReviewRuntimeFittingAction();
            case WildWindMetaPortTab.Sorties:
                return ValidateLoadout();
            case WildWindMetaPortTab.Processing:
                selectedProcessingInputId = "ognejar_ore";
                return SetResult(true, "Processing priority set to ognejar_ore.", "");
            case WildWindMetaPortTab.Production:
                return SetResult(true, "Estimate: current starter cascade order bottleneck is shown in the runtime overview.", "");
            case WildWindMetaPortTab.Technologies:
                return RunRuntimeTechnologyAction() ?? RuntimeAccountUnavailable();
            default:
                return SetResult(false, "", "Unknown tab.");
        }
    }

    private WildWindMetaPortActionResult ValidateLoadout()
    {
        MetaShipInstance ship = SelectedShip;
        bool ok = ship != null && ship.remainingSorties > 0;
        return SetResult(ok, "Loadout valid for selected session sortie.", "Selected ship cannot launch.");
    }

    private WildWindMetaPortActionResult SetResult(bool success, string successMessage, string failureMessage)
    {
        string message = success ? successMessage : failureMessage;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = success ? "Done." : "Blocked.";
        }

        lastMessage = message;
        return new WildWindMetaPortActionResult { success = success, message = message };
    }

    private WildWindMetaPortActionResult RuntimeAccountUnavailable()
    {
        return SetResult(false, "", "Runtime account is not bound to the port.");
    }

    private void ApplyRuntimeSnapshot()
    {
        if (runtimeMeta == null || runtimeMeta.progress == null || account == null || catalog == null)
        {
            return;
        }

        runtimeMeta.EnsureProgressInitialized();
        PlayerProgress progress = runtimeMeta.progress;
        runtimeSnapshotApplied = true;

        CopyRuntimeStacks(progress.inventory);
        CopyRuntimeStacks(progress.shipCargo);
        PortStorageState storage = runtimeMeta.GetCapitalStorageState();
        if (storage != null)
        {
            CopyRuntimeStacks(storage.storage);
        }

        ApplyRuntimeShipSnapshot(progress);
        EnsureCruiser203PortOption();
    }

    private void CopyRuntimeStacks(List<ResourceStack> stacks)
    {
        if (stacks == null || account == null) return;
        for (int i = 0; i < stacks.Count; i++)
        {
            ResourceStack stack = stacks[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            account.storage.TryGetValue(stack.resourceId, out int current);
            if (stack.amount > current)
            {
                account.storage[stack.resourceId] = stack.amount;
            }
        }
    }

    private void ApplyRuntimeShipSnapshot(PlayerProgress progress)
    {
        if (progress == null || account == null || catalog == null) return;

        string hullId = progress.selectedHullId ?? "";
        ShipPartDefinitionSO hull = null;
        ShipCatalogSO shipCatalog = runtimeMeta != null ? runtimeMeta.catalog : null;
        if (shipCatalog != null)
        {
            hull = !string.IsNullOrWhiteSpace(hullId) ? shipCatalog.GetPartById(hullId) : null;
            if (hull == null || !hull.IsHull)
            {
                hull = shipCatalog.GetStarterHull();
            }
        }

        if (hull != null && hull.IsHull)
        {
            hullId = hull.partId;
        }

        if (string.IsNullOrWhiteSpace(hullId))
        {
            return;
        }

        if (!catalog.ships.TryGetValue(hullId, out MetaShipDefinition definition))
        {
            definition = new MetaShipDefinition
            {
                shipId = hullId,
                tier = 1,
                maxSorties = 7,
                highSlots = 2,
                midSlots = 1,
                lowSlots = 1,
                rigSlots = 1
            };
            catalog.ships[hullId] = definition;
        }

        CountRuntimeSlots(hull, definition);

        MetaShipInstance ship = account.ships.Find(s => s != null && s.instanceId == RuntimeShipInstanceId);
        if (ship == null)
        {
            ship = new MetaShipInstance { instanceId = RuntimeShipInstanceId };
            account.ships.Insert(0, ship);
            selectedShipIndex = 0;
        }

        ship.shipId = hullId;
        ship.tier = definition.tier;
        ship.maxSorties = Mathf.Max(1, definition.maxSorties);
        ship.remainingSorties = Mathf.Clamp(ship.remainingSorties <= 0 ? ship.maxSorties : ship.remainingSorties, 1, ship.maxSorties);
        ship.preinstalledModules.Clear();
        ship.preinstalledModules.Add("runtime_hull");
        ship.fittedModules.Clear();
        if (progress.installedModules != null)
        {
            for (int i = 0; i < progress.installedModules.Count; i++)
            {
                InstalledModuleState installed = progress.installedModules[i];
                if (installed == null || string.IsNullOrWhiteSpace(installed.moduleId)) continue;
                ship.fittedModules.Add(installed.moduleId);
            }
        }
    }

    private void EnsureCruiser203PortOption()
    {
        if (account == null || catalog == null) return;
        if (!catalog.ships.TryGetValue(Cruiser203HullId, out MetaShipDefinition definition)) return;

        MetaShipInstance activeRuntimeShip = account.ships.Find(s => s != null && s.instanceId == RuntimeShipInstanceId);
        bool activeCruiser = activeRuntimeShip != null && IsCruiser203ShipId(activeRuntimeShip.shipId);
        for (int i = account.ships.Count - 1; i >= 0; i--)
        {
            MetaShipInstance ship = account.ships[i];
            if (ship == null || ship.instanceId != Cruiser203PortOptionInstanceId) continue;
            if (activeCruiser || i > 0 && account.ships.Exists(s => s != ship && s != null && s.instanceId != Cruiser203PortOptionInstanceId && IsCruiser203ShipId(s.shipId)))
            {
                account.ships.RemoveAt(i);
                if (selectedShipIndex >= account.ships.Count)
                {
                    selectedShipIndex = Mathf.Max(0, account.ships.Count - 1);
                }
            }
        }

        if (activeCruiser || account.ships.Exists(s => s != null && IsCruiser203ShipId(s.shipId)))
        {
            return;
        }

        MetaShipInstance cruiser = new MetaShipInstance
        {
            instanceId = Cruiser203PortOptionInstanceId,
            shipId = Cruiser203HullId,
            tier = definition.tier,
            remainingSorties = definition.maxSorties,
            maxSorties = definition.maxSorties
        };
        cruiser.preinstalledModules.Add("triple_203mm_main_battery");
        cruiser.preinstalledModules.Add("aft_crusher");
        account.ships.Add(cruiser);
    }

    private string GetRuntimePortShipActionLabel()
    {
        MetaShipInstance ship = SelectedShip;
        if (ship == null) return "Use Selected Ship";
        string targetHullId = ResolveRuntimeHullId(ship.shipId);
        if (IsCruiser203ShipId(targetHullId)) return "Use Cruiser 203";
        if (targetHullId == GameplaySessionAccountData.DefaultStarterHullId) return "Use Pioneer";
        return "Use Selected Ship";
    }

    private WildWindMetaPortActionResult RunRuntimePortShipSelectionAction()
    {
        if (!IsRuntimeBound) return null;

        MetaShipInstance ship = SelectedShip;
        if (ship == null)
        {
            return SetResult(false, "", "No selected ship.");
        }

        string targetHullId = ResolveRuntimeHullId(ship.shipId);
        if (string.IsNullOrWhiteSpace(targetHullId))
        {
            return SetResult(false, "", FormatShipName(ship.shipId) + " is not a session hull.");
        }

        if (!runtimeMeta.TrySelectSessionCoreHull(targetHullId, out string message))
        {
            return SetResult(false, "", message);
        }

        ApplyRuntimeSnapshot();
        SelectRuntimeCurrentShip();
        return SetResult(true, message, "");
    }

    private void SelectRuntimeCurrentShip()
    {
        if (account == null) return;
        int index = account.ships.FindIndex(s => s != null && s.instanceId == RuntimeShipInstanceId);
        if (index >= 0)
        {
            selectedShipIndex = index;
        }
    }

    private static string ResolveRuntimeHullId(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return "";
        if (shipId == GameplaySessionAccountData.DefaultStarterHullId || string.Equals(shipId, "pioneer", StringComparison.OrdinalIgnoreCase))
        {
            return GameplaySessionAccountData.DefaultStarterHullId;
        }

        if (IsCruiser203ShipId(shipId))
        {
            return Cruiser203HullId;
        }

        return "";
    }

    private static void CountRuntimeSlots(ShipPartDefinitionSO hull, MetaShipDefinition definition)
    {
        if (hull == null || definition == null || hull.slots == null) return;

        int high = 0;
        int mid = 0;
        int low = 0;
        int rig = 0;
        for (int i = 0; i < hull.slots.Count; i++)
        {
            ShipSlotDefinition slot = hull.slots[i];
            if (slot == null) continue;
            if (slot.slotTypeId == SessionExtractionConstants.HighSlotTypeId) high++;
            else if (slot.slotTypeId == SessionExtractionConstants.MidSlotTypeId) mid++;
            else if (slot.slotTypeId == SessionExtractionConstants.LowSlotTypeId) low++;
            else if (slot.slotTypeId == SessionExtractionConstants.RigSlotTypeId) rig++;
        }

        if (high + mid + low + rig <= 0) return;
        definition.highSlots = high;
        definition.midSlots = mid;
        definition.lowSlots = low;
        definition.rigSlots = Mathf.Max(definition.rigSlots, rig);
    }

    private string BuildRuntimeHeaderLine()
    {
        if (!IsRuntimeBound) return "Source: runtime account unavailable.";
        string sortie = runtimeMeta.GetSelectedSessionSortieDisplayName();
        return "Source: runtime MetaGameState | mode " + runtimeMeta.CurrentMode + " | sortie " + sortie;
    }

    private void AppendRuntimeTechnologyOverview(StringBuilder builder)
    {
        if (runtimeMeta == null)
        {
            builder.AppendLine("Runtime account unavailable.");
            return;
        }

        IReadOnlyList<TechnologyConfig> technologies = runtimeMeta.GetTechnologyConfigs();
        if (technologies == null || technologies.Count == 0)
        {
            builder.AppendLine("No technology config loaded.");
            return;
        }

        int shown = 0;
        for (int i = 0; i < technologies.Count && shown < 4; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;

            builder.AppendLine(runtimeMeta.GetTechnologyDisplayName(technology)
                + ": "
                + runtimeMeta.GetTechnologyStatusText(technology));
            shown++;
        }
    }

    private WildWindMetaPortActionResult RunRuntimeSortieSelectionAction()
    {
        if (!IsRuntimeBound) return null;

        bool changed = runtimeMeta.SelectNextSessionSortie(out string message);
        return SetResult(true, changed ? "Runtime sortie selected: " + message : "Runtime sortie board checked: " + message, "");
    }

    private WildWindMetaPortActionResult RunRuntimeFittingAction()
    {
        if (!IsRuntimeBound) return null;

        if (runtimeMeta.CanInstallNextStarterFittingUpgrade(out string canMessage)
            && runtimeMeta.TryInstallNextStarterFittingUpgrade(out string installMessage))
        {
            ApplyRuntimeSnapshot();
            return SetResult(true, installMessage, "");
        }

        return SetResult(true, "Runtime fitting checked: " + canMessage, "");
    }

    private WildWindMetaPortActionResult ReviewRuntimeFittingAction()
    {
        if (!IsRuntimeBound)
        {
            return SetResult(true, "Fitting bands checked: High/Mid/Low/Rig.", "");
        }

        string nextAction = runtimeMeta.GetNextStarterFittingUpgradeActionLabel();
        return SetResult(true, "Runtime fitting review: " + nextAction + ". " + runtimeMeta.GetCoreFittingSummaryText(), "");
    }

    private WildWindMetaPortActionResult RunRuntimeProcessingAction()
    {
        if (!IsRuntimeBound) return null;

        bool processed = runtimeMeta.TryProcessNextBaseBatch(out string message);
        ApplyRuntimeSnapshot();
        return SetResult(true, processed ? message : "Runtime processing queue checked: " + message, "");
    }

    private WildWindMetaPortActionResult RunRuntimeCascadeAction()
    {
        if (!IsRuntimeBound) return null;

        bool completed = runtimeMeta.TryRunNextBaseCascadeOrder(out string message);
        ApplyRuntimeSnapshot();
        return SetResult(true, completed ? message : "Runtime cascade checked: " + runtimeMeta.GetNextBaseCascadeOrderOverviewText() + " " + message, "");
    }

    private WildWindMetaPortActionResult RunRuntimeTechnologyAction()
    {
        if (!IsRuntimeBound) return null;

        IReadOnlyList<TechnologyConfig> technologies = runtimeMeta.GetTechnologyConfigs();
        if (technologies == null || technologies.Count == 0)
        {
            return SetResult(true, "Runtime technology board checked: no technology config loaded.", "");
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;
            if (runtimeMeta.IsTechnologyCompleted(technology.id)) continue;

            if (runtimeMeta.TrySelectResearchTechnology(technology.id))
            {
                ApplyRuntimeSnapshot();
                return SetResult(true, "Runtime research selected: " + runtimeMeta.GetTechnologyDisplayName(technology) + ".", "");
            }
        }

        return SetResult(true, "Runtime technology board checked: all visible technologies are complete or blocked.", "");
    }

    private float GetBuffer(string itemId)
    {
        if (account == null) return 0f;
        return account.processingBuffers.TryGetValue("buffer:" + itemId, out float value) ? value : 0f;
    }

    private string FormatStoredResource(string itemId, string label)
    {
        return label + " " + WildWindMetaMechanics.GetStorage(account, itemId);
    }

    private static string FormatShipName(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return "-";
        if (IsCruiser203ShipId(shipId)) return "Cruiser 203";
        if (string.Equals(shipId, "pioneer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(shipId, GameplaySessionAccountData.DefaultStarterHullId, StringComparison.OrdinalIgnoreCase))
        {
            return "Pioneer";
        }

        return shipId.Replace('_', ' ');
    }

    private static bool IsCruiser203ShipId(string shipId)
    {
        return string.Equals(shipId, Cruiser203HullId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(shipId, "cruiser203", StringComparison.OrdinalIgnoreCase);
    }
}
