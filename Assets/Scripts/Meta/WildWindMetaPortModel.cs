using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum WildWindMetaPortTab
{
    Port,
    ShipTree,
    Fitting,
    Sorties,
    Missions,
    Mining,
    Gas,
    Scanning,
    Hacking,
    Threat,
    FireControl,
    Drones,
    Leviathans,
    Salvage,
    Sublikats,
    Relics,
    Environment,
    Processing,
    Production,
    Recipes,
    Traders,
    Shop,
    Containers,
    BattlePass,
    Events,
    Technologies,
    Commander
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
        WildWindMetaPortTab.ShipTree,
        WildWindMetaPortTab.Fitting,
        WildWindMetaPortTab.Sorties,
        WildWindMetaPortTab.Missions,
        WildWindMetaPortTab.Mining,
        WildWindMetaPortTab.Gas,
        WildWindMetaPortTab.Scanning,
        WildWindMetaPortTab.Hacking,
        WildWindMetaPortTab.Threat,
        WildWindMetaPortTab.FireControl,
        WildWindMetaPortTab.Drones,
        WildWindMetaPortTab.Leviathans,
        WildWindMetaPortTab.Salvage,
        WildWindMetaPortTab.Sublikats,
        WildWindMetaPortTab.Relics,
        WildWindMetaPortTab.Environment,
        WildWindMetaPortTab.Processing,
        WildWindMetaPortTab.Production,
        WildWindMetaPortTab.Recipes,
        WildWindMetaPortTab.Traders,
        WildWindMetaPortTab.Shop,
        WildWindMetaPortTab.Containers,
        WildWindMetaPortTab.BattlePass,
        WildWindMetaPortTab.Events,
        WildWindMetaPortTab.Technologies,
        WildWindMetaPortTab.Commander
    };

    private readonly HashSet<WildWindMetaPortTab> visitedTabs = new HashSet<WildWindMetaPortTab>();
    private readonly HashSet<string> unlockedTechnologies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> technologyLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly List<VoucherState> sortieVouchers = new List<VoucherState>();
    private readonly List<string> consumables = new List<string>();
    private const string RuntimeShipInstanceId = "runtime_current_ship";

    public WildWindMetaCatalog catalog;
    public MetaAccountState account;
    public MetaGameState runtimeMeta;
    public WildWindMetaPortTab selectedTab = WildWindMetaPortTab.Port;
    public int selectedShipIndex;
    public int selectedSortieIndex;
    public int containerSeed = 19;
    public int eventSpinCount;
    public bool runtimeSnapshotApplied;
    public string selectedProcessingInputId = "ognejar_ore";
    public string selectedRecipeId = "trader_t2_crusher";
    public string missionOrder = "Pin sortie objective, then daily silver quest, then trader task";
    public string miningOrder = "Safe ore: scan first, crusher only, avoid relics";
    public string gasOrder = "Harvest center concentration, avoid hot/electric/ichor clouds";
    public string scanOrder = "Directional boulders first, passive sweep always on";
    public string hackingOrder = "Expose object first, then ship hacker minigame; failures raise threat";
    public string threatOrder = "Quiet profile: avoid explosive chains, loud guns and failed hacks";
    public string fireControlOrder = "Safe mode: drones only, resource fire locked";
    public string droneOrder = "Catchers + tugs autonomous, syringes on ichor only";
    public string leviathanOrder = "Scan, bait through hazards, harpoon only when armor can be pierced";
    public string salvageOrder = "Recover drone wrecks on islands; catch falling hulls before the storm";
    public string sublikatOrder = "High-altitude ice deposit: cold loadout, crane or protected drone only";
    public string relicOrder = "No fire, no ichor, no gunfire, no ram; wait for half health then crane";
    public string environmentOrder = "Avoid storm floor, cold insulation on, claudium fields marked";
    public string lastMessage = "Port ready.";
    public string lastContainerDrop = "-";
    public string lastEventReward = "-";
    public int dailyQuestProgress = 700;
    public int dailyQuestTarget = 1000;
    public int threatStars;

    public bool IsReady => catalog != null && account != null && account.ships.Count > 0;
    public int TabCount => OrderedTabs.Length;
    public IReadOnlyList<VoucherState> SortieVouchers => sortieVouchers;
    public IReadOnlyList<string> Consumables => consumables;
    public bool HasVisitedAllTabs => visitedTabs.Count >= OrderedTabs.Length;
    public bool IsRuntimeBound => runtimeMeta != null && runtimeMeta.progress != null;
    public string SourceLabel => IsRuntimeBound ? "runtime" : "demo";

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

    public static WildWindMetaPortUiState CreateDemo()
    {
        WildWindMetaCatalog catalog = WildWindMetaMechanics.CreateMinimalCatalog();
        MetaAccountState account = WildWindMetaMechanics.CreateFreshAccount(catalog);
        account.silver = 5200;
        account.gold = 900;
        account.constructionXp = 260;
        account.industrialXp = 180;
        account.militaryXp = 140;
        account.researchXp = 220;
        account.commanderXp = 840;
        account.commanderLevel = 4;
        account.commanderTalentPoints = 5;
        account.unlockedShips.Add("hauler_t1");
        account.unlockedT1Modules.Add("crusher");
        account.unlockedT1Modules.Add("flamethrower");
        WildWindMetaMechanics.AddStorage(account, "ognejar_ore", 90);
        WildWindMetaMechanics.AddStorage(account, "ferron", 35);
        WildWindMetaMechanics.AddStorage(account, "charcoal", 30);
        WildWindMetaMechanics.AddStorage(account, "datacore_industrial", 5);
        WildWindMetaMechanics.AddStorage(account, "datacore_precursor", 5);
        WildWindMetaMechanics.AddStorage(account, "precursor_fragment", 4);
        WildWindMetaMechanics.AddStorage(account, "event_token", 3);
        WildWindMetaMechanics.LearnRecipe(account, "trader_t2_crusher", RecipeTier.T2);

        WildWindMetaPortUiState state = new WildWindMetaPortUiState
        {
            catalog = catalog,
            account = account
        };
        state.sortieVouchers.Add(new VoucherState { type = VoucherType.Coins, rarity = VoucherRarity.Rare, multiplier = 1.5f });
        state.sortieVouchers.Add(new VoucherState { type = VoucherType.Experience, rarity = VoucherRarity.Uncommon, multiplier = 1.25f });
        state.consumables.Add("good_oil");
        state.consumables.Add("extra_rations");
        state.consumables.Add("coolant");
        state.SelectTab(WildWindMetaPortTab.Port);
        return state;
    }

    public static WildWindMetaPortUiState CreateFromRuntime(MetaGameState meta)
    {
        WildWindMetaPortUiState state = CreateDemo();
        state.BindRuntimeMeta(meta);
        return state;
    }

    public void BindRuntimeMeta(MetaGameState meta)
    {
        runtimeMeta = meta;
        if (runtimeMeta == null || runtimeMeta.progress == null || account == null || catalog == null)
        {
            return;
        }

        ApplyRuntimeSnapshot();
    }

    public static string GetTabDisplayName(WildWindMetaPortTab tab)
    {
        return tab switch
        {
            WildWindMetaPortTab.Port => "PORT",
            WildWindMetaPortTab.ShipTree => "SHIPS",
            WildWindMetaPortTab.Fitting => "FITTING",
            WildWindMetaPortTab.Sorties => "SORTIES",
            WildWindMetaPortTab.Missions => "MISSIONS",
            WildWindMetaPortTab.Mining => "MINING",
            WildWindMetaPortTab.Gas => "GAS",
            WildWindMetaPortTab.Scanning => "SCANNING",
            WildWindMetaPortTab.Hacking => "HACKING",
            WildWindMetaPortTab.Threat => "THREAT",
            WildWindMetaPortTab.FireControl => "FIRE CTRL",
            WildWindMetaPortTab.Drones => "DRONES",
            WildWindMetaPortTab.Leviathans => "LEVIATHANS",
            WildWindMetaPortTab.Salvage => "SALVAGE",
            WildWindMetaPortTab.Sublikats => "SUBLIKATS",
            WildWindMetaPortTab.Relics => "RELICS",
            WildWindMetaPortTab.Environment => "ENVIRONMENT",
            WildWindMetaPortTab.Processing => "PROCESSING",
            WildWindMetaPortTab.Production => "PRODUCTION",
            WildWindMetaPortTab.Recipes => "RECIPES",
            WildWindMetaPortTab.Traders => "TRADERS",
            WildWindMetaPortTab.Shop => "SHOP",
            WildWindMetaPortTab.Containers => "CONTAINERS",
            WildWindMetaPortTab.BattlePass => "BATTLE PASS",
            WildWindMetaPortTab.Events => "EVENTS",
            WildWindMetaPortTab.Technologies => "TECH",
            WildWindMetaPortTab.Commander => "COMMANDER",
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
        return "Silver " + account.silver
            + " | Gold " + account.gold
            + " | CXP " + account.constructionXp
            + " | IND " + account.industrialXp
            + " | MIL " + account.militaryXp
            + " | R&D " + account.researchXp
            + " | Cmd XP " + account.commanderXp
            + " | Cmd pts " + account.commanderTalentPoints
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
                builder.AppendLine("WoWS-style hub: selected ship, resources, carousel and contextual actions.");
                builder.AppendLine("Port slots: " + account.ships.Count + "/" + account.portSlots + "  next slot " + WildWindMetaMechanics.PortSlotGoldCost + " gold.");
                builder.AppendLine("Fallback Pioneer: " + (account.ships.Exists(s => s.shipId == "pioneer") ? "present" : "will be restored"));
                if (IsRuntimeBound)
                {
                    builder.AppendLine("Runtime mode: " + runtimeMeta.CurrentMode + ", base dock: " + runtimeMeta.IsDockedAtCapital());
                }
                break;
            case WildWindMetaPortTab.ShipTree:
                builder.AppendLine("Tree path: Pioneer -> Hauler T1 -> Pioneer Mining T2 -> Precursor T3.");
                builder.AppendLine("Hauler T1 unlocked: " + account.unlockedShips.Contains("hauler_t1") + "  cost 1200 silver.");
                builder.AppendLine("T2/T3 ships use modernization, trader recipes, event rewards or precursor fragments.");
                break;
            case WildWindMetaPortTab.Fitting:
                builder.AppendLine("High: crusher, guns, vibro-ram, flamethrower, harpoon, scanners.");
                builder.AppendLine("Mid: crane, hacker, active ram shield, temperature control.");
                builder.AppendLine("Low/Rig: passive armor, insulation, ichor protection, economy and hull rigs.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine(runtimeMeta.GetCoreFittingSummaryText());
                }

                builder.AppendLine(BuildSelectedShipSummary());
                break;
            case WildWindMetaPortTab.Sorties:
                builder.AppendLine("Sortie card combines destination and objective: ore, gas, drones, leviathan, survey.");
                if (IsRuntimeBound)
                {
                    builder.AppendLine("Runtime selected: " + runtimeMeta.GetSelectedSessionSortieDisplayName() + " (" + runtimeMeta.GetSelectedSessionSortieRequirementText() + ").");
                }

                builder.AppendLine("Vouchers: " + FormatVouchers() + ".");
                builder.AppendLine("Consumables: " + string.Join(", ", consumables) + ".");
                break;
            case WildWindMetaPortTab.Missions:
                builder.AppendLine("Combat-mission board: sortie objective, trader task, daily silver quest and event task chain.");
                builder.AppendLine("Daily quest: earn " + dailyQuestProgress + "/" + dailyQuestTarget + " silver for a one-lot container.");
                builder.AppendLine("Trader tasks teach mechanics, raise shop reputation and unlock T1 access or T2 recipes.");
                builder.AppendLine("Active order: " + missionOrder + ".");
                break;
            case WildWindMetaPortTab.Mining:
                builder.AppendLine("Mining orders configure how the ship and drones treat boulders before launch.");
                builder.AppendLine("Crusher collection keeps useful ore, throws waste away and still resolves ram damage.");
                builder.AppendLine("Boulders: normal, armored crust, ice, explosive gas, ichor, claudium and relic-bearing.");
                builder.AppendLine("Active order: " + miningOrder + ".");
                break;
            case WildWindMetaPortTab.Gas:
                builder.AppendLine("Gas plans pick cloud handling: harvest, avoid, burn, or lure enemies through hazards.");
                builder.AppendLine("Clouds have center concentration, wind drift, water/gas mixes and hot/cold/electric/ichor flags.");
                builder.AppendLine("Drone harvesters need matching thermal, electric or ichor protection.");
                builder.AppendLine("Active order: " + gasOrder + ".");
                break;
            case WildWindMetaPortTab.Scanning:
                builder.AppendLine("Scanning exposure unlocks boulder, cloud, leviathan, drone and relic passports.");
                builder.AppendLine("High slot: directional long range. Mid: omni. Low: passive slow exposure.");
                builder.AppendLine("Visibility, fog and cloud opacity reduce exposure and fire-control lead quality.");
                builder.AppendLine("Active order: " + scanOrder + ".");
                break;
            case WildWindMetaPortTab.Hacking:
                builder.AppendLine("Hacking is a two-phase job: fill exposure, then connect with the ship hacker.");
                builder.AppendLine("Only the ship runs the minigame; drones can scan but cannot open the object.");
                builder.AppendLine("Failed hacking no longer damages the ship, it raises threat and attracts drones or leviathans.");
                builder.AppendLine("Active order: " + hackingOrder + ".");
                break;
            case WildWindMetaPortTab.Threat:
                builder.AppendLine("Threat is the session wanted level raised by noisy modules, shots, failed hacks, rams and explosions.");
                builder.AppendLine("Higher stars attract rebel drone groups and can also draw leviathan attention.");
                builder.AppendLine("Quiet weapons, controlled hacking and avoiding explosive boulder cascades slow the rise.");
                builder.AppendLine("Stars: " + threatStars + "  Active order: " + threatOrder + ".");
                break;
            case WildWindMetaPortTab.FireControl:
                builder.AppendLine("Gun automation uses filters like safe, dangerous-only and everything.");
                builder.AppendLine("Ammo is split by delivery and element: AP/HE plus physical, fire, electric or ichor.");
                builder.AppendLine("Unknown resource targets, relic risk, no ammo and bad sector can block shots.");
                builder.AppendLine("Active order: " + fireControlOrder + ".");
                break;
            case WildWindMetaPortTab.Drones:
                builder.AppendLine("Drone groups cover catchers, grabbers, burners, tugs, disarmers, openers, syringes and salvagers.");
                builder.AppendLine("They can fetch chunks, mine exposed ore, tow boulders, open crusts, drain ichor or recover wrecks.");
                builder.AppendLine("Ichor protection tiers decide whether drones take 500%, 100%, 50% or no acid damage.");
                builder.AppendLine("Active order: " + droneOrder + ".");
                break;
            case WildWindMetaPortTab.Leviathans:
                builder.AppendLine("Leviathan hunt prep combines passports, bait routes, harpoons, guns and hazard lures.");
                builder.AppendLine("Leviathans ignore ichor blood, resist heat, but still take ram, explosion and projectile damage.");
                builder.AppendLine("Carcass recovery feeds processing after a successful return; bad fights raise threat fast.");
                builder.AppendLine("Active order: " + leviathanOrder + ".");
                break;
            case WildWindMetaPortTab.Salvage:
                builder.AppendLine("Salvage covers destroyed drone wrecks, outposts, turrets and falling hull remains.");
                builder.AppendLine("Island landing means recovery; storm fall means loss unless a catcher drone intercepts it.");
                builder.AppendLine("Small wrecks can be carried, heavy wrecks need crane or salvager drones to dismantle on site.");
                builder.AppendLine("Active order: " + salvageOrder + ".");
                break;
            case WildWindMetaPortTab.Sublikats:
                builder.AppendLine("Sublikats are high-altitude icy dust deposits trapped in ice boulders.");
                builder.AppendLine("They do not shed chunks; damage destroys value, while crane or drones extract them cleanly.");
                builder.AppendLine("Prep checks cold, wind, altitude, weak claudium lift and enough coal for heaters.");
                builder.AppendLine("Active order: " + sublikatOrder + ".");
                break;
            case WildWindMetaPortTab.Relics:
                builder.AppendLine("Relics require a boulder below half health before crane or catcher extraction.");
                builder.AppendLine("Any fire, ichor, gunfire or ram ruins the relic; passive shedding and careful drones are safe.");
                builder.AppendLine("Recovered relics go to decoding for precursor technology and rare recipes.");
                builder.AppendLine("Active order: " + relicOrder + ".");
                break;
            case WildWindMetaPortTab.Environment:
                builder.AppendLine("Region passport summarizes ambient temperature, wind, visibility, storm floor and altitude limits.");
                builder.AppendLine("Claudium fields stack, raise drag, drain claudium and disable slipstream.");
                builder.AppendLine("Temperature drifts bodies toward ambient; heat increases fragility and can burn ore.");
                builder.AppendLine("Active order: " + environmentOrder + ".");
                break;
            case WildWindMetaPortTab.Processing:
                builder.AppendLine("Five branches consume raw cargo continuously and emit whole units.");
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

                builder.AppendLine("Frame order: ferron 10 + charcoal 8 -> pioneer_mining_t2_frame.");
                builder.AppendLine("Stored frame kits: " + WildWindMetaMechanics.GetStorage(account, "pioneer_mining_t2_frame"));
                break;
            case WildWindMetaPortTab.Recipes:
                builder.AppendLine("T1 recipes do not exist; T1 is bought. T2 comes from traders, T3 from precursor fragments.");
                builder.AppendLine(FormatRecipe("trader_t2_crusher"));
                builder.AppendLine(FormatRecipe("precursor_t3_frame"));
                builder.AppendLine("Datacores: industrial " + WildWindMetaMechanics.GetStorage(account, "datacore_industrial")
                    + ", precursor " + WildWindMetaMechanics.GetStorage(account, "datacore_precursor"));
                break;
            case WildWindMetaPortTab.Traders:
                builder.AppendLine("Trader cards expose tasks, reputation, recipes, modules and weekly limited ships.");
                builder.AppendLine("Geologists: ore tasks, scanner modules, crusher recipes.");
                builder.AppendLine("Drone Archive: drone parts, hacking, salvager and syringe tech.");
                builder.AppendLine("Reputation: " + WildWindMetaMechanics.GetStorage(account, "geology_reputation"));
                break;
            case WildWindMetaPortTab.Shop:
                builder.AppendLine("Bundle shop uses internal gold only. No real-money path exists.");
                builder.AppendLine("Visible offers: starter supplies, datacore packs, expedition subscriptions.");
                builder.AppendLine("Gold balance: " + account.gold);
                break;
            case WildWindMetaPortTab.Containers:
                builder.AppendLine("Daily containers drop exactly one configured lot.");
                builder.AppendLine("Lots may share item id but differ by quantity and chance.");
                builder.AppendLine("Last drop: " + lastContainerDrop);
                break;
            case WildWindMetaPortTab.BattlePass:
                builder.AppendLine("Weekly pass has free and paid track bought with internal gold.");
                builder.AppendLine("Points: " + account.weeklyBattlePassPoints + "  paid: " + account.weeklyBattlePassPaidTrack);
                builder.AppendLine("Small contract: " + (string.IsNullOrWhiteSpace(account.activeContractId) ? "-" : account.activeContractId));
                break;
            case WildWindMetaPortTab.Events:
                builder.AppendLine("Monthly event: task board, reward track, roulette and limited ships/blueprints.");
                builder.AppendLine("Storm Season spins: " + eventSpinCount + "  last reward: " + lastEventReward);
                builder.AppendLine("Fragments: " + WildWindMetaMechanics.GetStorage(account, "precursor_fragment"));
                break;
            case WildWindMetaPortTab.Technologies:
                builder.AppendLine("Technologies are the large passive book, separate from commander talents.");
                builder.AppendLine("Unlocked: " + (unlockedTechnologies.Count == 0 ? "-" : string.Join(", ", unlockedTechnologies)));
                builder.AppendLine("Vibro Resonance level: " + GetTechnologyLevel("vibro_resonance"));
                break;
            case WildWindMetaPortTab.Commander:
                builder.AppendLine("Commander has level, points and flexible perks with respec.");
                builder.AppendLine("Level " + account.commanderLevel + "  XP " + account.commanderXp + "  free points " + account.commanderTalentPoints);
                builder.AppendLine("Active talents: " + (account.activeCommanderTalents.Count == 0 ? "-" : string.Join(", ", account.activeCommanderTalents)));
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
            WildWindMetaPortTab.Port => "Buy Port Slot",
            WildWindMetaPortTab.ShipTree => "Buy Hauler T1",
            WildWindMetaPortTab.Fitting => "Fit Crusher",
            WildWindMetaPortTab.Sorties => "Run Sortie Prep",
            WildWindMetaPortTab.Missions => "Pin Mission",
            WildWindMetaPortTab.Mining => "Set Mining Order",
            WildWindMetaPortTab.Gas => "Set Gas Plan",
            WildWindMetaPortTab.Scanning => "Set Scan Focus",
            WildWindMetaPortTab.Hacking => "Set Hack Plan",
            WildWindMetaPortTab.Threat => "Set Quiet Profile",
            WildWindMetaPortTab.FireControl => "Set Fire Filter",
            WildWindMetaPortTab.Drones => "Set Drone Task",
            WildWindMetaPortTab.Leviathans => "Set Hunt Plan",
            WildWindMetaPortTab.Salvage => "Set Salvage Plan",
            WildWindMetaPortTab.Sublikats => "Set High Plan",
            WildWindMetaPortTab.Relics => "Set Relic Plan",
            WildWindMetaPortTab.Environment => "Set Region Protocol",
            WildWindMetaPortTab.Processing => "Process Ore",
            WildWindMetaPortTab.Production => "Craft T2 Frame",
            WildWindMetaPortTab.Recipes => "Upgrade Recipe",
            WildWindMetaPortTab.Traders => "Accept Trader Task",
            WildWindMetaPortTab.Shop => "Buy Bundle",
            WildWindMetaPortTab.Containers => "Open Container",
            WildWindMetaPortTab.BattlePass => "Buy Paid Track",
            WildWindMetaPortTab.Events => "Event Spin",
            WildWindMetaPortTab.Technologies => "Unlock Tech",
            WildWindMetaPortTab.Commander => "Activate Talent",
            _ => "Run"
        };
    }

    public string GetSecondaryActionLabel()
    {
        return selectedTab switch
        {
            WildWindMetaPortTab.Port => "Restore Pioneer",
            WildWindMetaPortTab.ShipTree => "Modernize T2",
            WildWindMetaPortTab.Fitting => "Rig Toggle",
            WildWindMetaPortTab.Sorties => "Validate Loadout",
            WildWindMetaPortTab.Missions => "Claim Daily",
            WildWindMetaPortTab.Mining => "Mark Relic Safe",
            WildWindMetaPortTab.Gas => "Burn Hazard Cloud",
            WildWindMetaPortTab.Scanning => "Passive Sweep",
            WildWindMetaPortTab.Hacking => "Quiet Failure Rule",
            WildWindMetaPortTab.Threat => "Loud Lure",
            WildWindMetaPortTab.FireControl => "Relic Safety Lock",
            WildWindMetaPortTab.Drones => "Syringe Priority",
            WildWindMetaPortTab.Leviathans => "Hazard Lure",
            WildWindMetaPortTab.Salvage => "Catcher Intercept",
            WildWindMetaPortTab.Sublikats => "Cold Loadout",
            WildWindMetaPortTab.Relics => "No-Damage Lock",
            WildWindMetaPortTab.Environment => "Heat/Cold Setup",
            WildWindMetaPortTab.Processing => "Set Priority",
            WildWindMetaPortTab.Production => "Estimate Order",
            WildWindMetaPortTab.Recipes => "Learn T3",
            WildWindMetaPortTab.Traders => "Buy T2 Recipe",
            WildWindMetaPortTab.Shop => "Buy Subscription",
            WildWindMetaPortTab.Containers => "Show Odds",
            WildWindMetaPortTab.BattlePass => "Contract Toggle",
            WildWindMetaPortTab.Events => "Claim Fragment",
            WildWindMetaPortTab.Technologies => "Upgrade Tech",
            WildWindMetaPortTab.Commander => "Respec Talent",
            _ => "More"
        };
    }

    public WildWindMetaPortActionResult RunPrimaryAction()
    {
        switch (selectedTab)
        {
            case WildWindMetaPortTab.Port:
                return FromMeta(WildWindMetaMechanics.BuyPortSlot(account));
            case WildWindMetaPortTab.ShipTree:
                return BuyHauler();
            case WildWindMetaPortTab.Fitting:
                return RunRuntimeFittingAction() ?? FitModule("crusher");
            case WildWindMetaPortTab.Sorties:
                return RunRuntimeSortieSelectionAction() ?? PrepareSortie();
            case WildWindMetaPortTab.Missions:
                return SetMissionOrder();
            case WildWindMetaPortTab.Mining:
                return SetMiningOrder();
            case WildWindMetaPortTab.Gas:
                return SetGasOrder();
            case WildWindMetaPortTab.Scanning:
                return SetScanOrder();
            case WildWindMetaPortTab.Hacking:
                return SetHackingOrder();
            case WildWindMetaPortTab.Threat:
                return SetThreatOrder();
            case WildWindMetaPortTab.FireControl:
                return SetFireControlOrder();
            case WildWindMetaPortTab.Drones:
                return SetDroneOrder();
            case WildWindMetaPortTab.Leviathans:
                return SetLeviathanOrder();
            case WildWindMetaPortTab.Salvage:
                return SetSalvageOrder();
            case WildWindMetaPortTab.Sublikats:
                return SetSublikatOrder();
            case WildWindMetaPortTab.Relics:
                return SetRelicOrder();
            case WildWindMetaPortTab.Environment:
                return SetEnvironmentOrder();
            case WildWindMetaPortTab.Processing:
                return RunRuntimeProcessingAction() ?? FromMeta(WildWindMetaMechanics.ProcessOneCycle(account, catalog.processing[selectedProcessingInputId]));
            case WildWindMetaPortTab.Production:
                return RunRuntimeCascadeAction() ?? CraftT2Frame();
            case WildWindMetaPortTab.Recipes:
                return FromMeta(WildWindMetaMechanics.UpgradeRecipe(account, selectedRecipeId));
            case WildWindMetaPortTab.Traders:
                return AcceptTraderTask();
            case WildWindMetaPortTab.Shop:
                return BuyBundle();
            case WildWindMetaPortTab.Containers:
                return OpenContainer();
            case WildWindMetaPortTab.BattlePass:
                return SetResult(WildWindMetaMechanics.BuyWeeklyBattlePassPaidTrack(account), "Paid track bought.", "Paid track blocked.");
            case WildWindMetaPortTab.Events:
                return SpinEvent();
            case WildWindMetaPortTab.Technologies:
                return UnlockTechnology("vibro_resonance", 80);
            case WildWindMetaPortTab.Commander:
                return SetResult(WildWindMetaMechanics.ActivateCommanderTalent(account, "steady_hands", 2), "Talent activated.", "Talent blocked.");
            default:
                return SetResult(false, "", "Unknown tab.");
        }
    }

    public WildWindMetaPortActionResult RunSecondaryAction()
    {
        switch (selectedTab)
        {
            case WildWindMetaPortTab.Port:
                bool restored = WildWindMetaMechanics.EnsurePioneerFallback(account, catalog);
                return SetResult(true, restored ? "Pioneer restored." : "Pioneer fallback verified.", "");
            case WildWindMetaPortTab.ShipTree:
                return ModernizeT2();
            case WildWindMetaPortTab.Fitting:
                return ToggleRig();
            case WildWindMetaPortTab.Sorties:
                return ValidateLoadout();
            case WildWindMetaPortTab.Missions:
                return ClaimDailyQuest();
            case WildWindMetaPortTab.Mining:
                miningOrder = "Relic-safe mining: no fire, no ram, no guns; drones/crane only below half health";
                return SetResult(true, "Mining order switched to relic-safe extraction.", "");
            case WildWindMetaPortTab.Gas:
                gasOrder = "Burn marked hazard cloud before harvesting nearby boulders";
                return SetResult(true, "Gas plan marked one hazard cloud for flamethrower burn-off.", "");
            case WildWindMetaPortTab.Scanning:
                scanOrder = "Passive sweep armed: low-slot scanner accumulates exposure while idle";
                return SetResult(true, "Passive scanner sweep enabled.", "");
            case WildWindMetaPortTab.Hacking:
                hackingOrder = "Quiet hacking: abort failed minigame after second miss and raise only controlled threat";
                return SetResult(true, "Hacking rule set to quiet failure containment.", "");
            case WildWindMetaPortTab.Threat:
                threatOrder = "Loud lure: intentionally spike threat to pull rebel drones into prepared hazards";
                threatStars = Mathf.Min(5, threatStars + 2);
                return SetResult(true, "Threat profile switched to loud lure for prepared ambushes.", "");
            case WildWindMetaPortTab.FireControl:
                fireControlOrder = "Relic safety lock: block boulders with unknown or known relic risk";
                return SetResult(true, "Fire-control relic lock enabled.", "");
            case WildWindMetaPortTab.Drones:
                droneOrder = "Syringes prioritize ichor boulders, openers avoid acid without protection";
                return SetResult(true, "Drone priority switched to ichor syringe work.", "");
            case WildWindMetaPortTab.Leviathans:
                leviathanOrder = "Hazard lure: kite leviathan through explosive boulders and electric clouds, then extract";
                return SetResult(true, "Leviathan hunt switched to hazard-lure route.", "");
            case WildWindMetaPortTab.Salvage:
                salvageOrder = "Catcher intercepts valuable wrecks before storm fall; heavy hulls dismantled on island";
                return SetResult(true, "Salvage catcher intercept protocol enabled.", "");
            case WildWindMetaPortTab.Sublikats:
                sublikatOrder = "Cold loadout: insulation, heater, high-altitude coal reserve and protected crane drones";
                return SetResult(true, "Sublikat cold/high-altitude loadout prepared.", "");
            case WildWindMetaPortTab.Relics:
                relicOrder = "Relic lock: no fire, ichor, gunfire, ram or explosive chain on marked boulders";
                return SetResult(true, "Relic no-damage lock enabled.", "");
            case WildWindMetaPortTab.Environment:
                environmentOrder = "Thermal plan: insulation on, heater/cooler counters ambient before sortie";
                return SetResult(true, "Environment protocol prepared heater/cooler and insulation checks.", "");
            case WildWindMetaPortTab.Processing:
                selectedProcessingInputId = "ognejar_ore";
                return SetResult(true, "Processing priority set to ognejar_ore.", "");
            case WildWindMetaPortTab.Production:
                return SetResult(true, "Estimate: Construction line bottleneck ~0.6 min, cost ferron 10 + charcoal 8.", "");
            case WildWindMetaPortTab.Recipes:
                return LearnT3Recipe();
            case WildWindMetaPortTab.Traders:
                return BuyTraderRecipe();
            case WildWindMetaPortTab.Shop:
                return BuySubscription();
            case WildWindMetaPortTab.Containers:
                return SetResult(true, "Odds shown: charcoal x10 common, charcoal x50 rare, datacore uncommon, ship ultra rare.", "");
            case WildWindMetaPortTab.BattlePass:
                return ToggleContract();
            case WildWindMetaPortTab.Events:
                WildWindMetaMechanics.AddStorage(account, "precursor_fragment", 1);
                return SetResult(true, "Event fragment claimed.", "");
            case WildWindMetaPortTab.Technologies:
                return UpgradeTechnology("vibro_resonance", 40);
            case WildWindMetaPortTab.Commander:
                return SetResult(WildWindMetaMechanics.DeactivateCommanderTalent(account, "steady_hands", 2), "Talent respecced.", "Talent was not active.");
            default:
                return SetResult(false, "", "Unknown tab.");
        }
    }

    private WildWindMetaPortActionResult BuyHauler()
    {
        if (!account.unlockedShips.Contains("hauler_t1"))
        {
            if (account.constructionXp < 80)
            {
                return SetResult(false, "", "Need 80 construction XP to unlock Hauler T1.");
            }

            account.constructionXp -= 80;
            account.unlockedShips.Add("hauler_t1");
        }

        return FromMeta(WildWindMetaMechanics.BuyT1Ship(account, catalog, "hauler_t1"));
    }

    private WildWindMetaPortActionResult ModernizeT2()
    {
        if (account.ships.Count >= account.portSlots)
        {
            return SetResult(false, "", "No port slot for T2 modernization.");
        }

        if (WildWindMetaMechanics.GetStorage(account, "pioneer_mining_t2_frame") <= 0)
        {
            WildWindMetaMechanics.AddStorage(account, "pioneer_mining_t2_frame", 1);
        }

        MetaShipDefinition t2 = catalog.ships["pioneer_mining_t2"];
        account.ships.Add(new MetaShipInstance
        {
            shipId = t2.shipId,
            tier = t2.tier,
            remainingSorties = t2.maxSorties,
            maxSorties = t2.maxSorties
        });
        WildWindMetaMechanics.AddStorage(account, "pioneer_mining_t2_frame", -1);
        selectedShipIndex = account.ships.Count - 1;
        return SetResult(true, "T1 hull modernized into Pioneer Mining T2.", "");
    }

    private WildWindMetaPortActionResult FitModule(string moduleId)
    {
        MetaShipInstance ship = SelectedShip;
        if (ship == null) return SetResult(false, "", "No selected ship.");
        if (ship.preinstalledModules.Contains(moduleId) || ship.fittedModules.Contains(moduleId))
        {
            return SetResult(false, "", moduleId + " already fitted.");
        }

        MetaShipDefinition definition = catalog.ships.TryGetValue(ship.shipId, out MetaShipDefinition found) ? found : null;
        int slotLimit = definition != null ? definition.highSlots + definition.midSlots + definition.lowSlots : 1;
        if (ship.fittedModules.Count >= slotLimit)
        {
            return SetResult(false, "", "No free fitting slot.");
        }

        ship.fittedModules.Add(moduleId);
        return SetResult(true, "Fitted " + moduleId + " to " + FormatShipName(ship.shipId) + ".", "");
    }

    private WildWindMetaPortActionResult ToggleRig()
    {
        MetaShipInstance ship = SelectedShip;
        if (ship == null) return SetResult(false, "", "No selected ship.");
        if (ship.rigs.Count == 0)
        {
            ship.rigs.Add("reinforced_keel_rig");
            return SetResult(true, "Rig installed. Free removal would destroy it.", "");
        }

        if (account.gold < WildWindMetaMechanics.RigSafeRemoveGoldCost)
        {
            return SetResult(false, "", "Need 20 gold to remove rig intact.");
        }

        account.gold -= WildWindMetaMechanics.RigSafeRemoveGoldCost;
        string rig = ship.rigs[0];
        ship.rigs.RemoveAt(0);
        WildWindMetaMechanics.AddStorage(account, rig, 1);
        return SetResult(true, "Rig removed intact for 20 gold.", "");
    }

    private WildWindMetaPortActionResult PrepareSortie()
    {
        MetaShipInstance ship = SelectedShip;
        if (ship == null) return SetResult(false, "", "No selected ship.");
        if (!WildWindMetaMechanics.ConsumeShipSortie(ship))
        {
            return SetResult(false, "", "Ship sortie resource depleted.");
        }

        float coins = WildWindMetaMechanics.ApplyVoucherSet(sortieVouchers, VoucherType.Coins, 100f);
        float xp = WildWindMetaMechanics.ApplyVoucherSet(sortieVouchers, VoucherType.Experience, 40f);
        account.silver += Mathf.FloorToInt(coins);
        account.industrialXp += Mathf.FloorToInt(xp);
        WildWindMetaMechanics.AddRepeatableBattlePassProgress(account, Mathf.FloorToInt(xp * 25f), 0);
        return SetResult(true, "Sortie prepared: vouchers projected " + coins.ToString("0") + " silver and " + xp.ToString("0") + " XP.", "");
    }

    private WildWindMetaPortActionResult ValidateLoadout()
    {
        bool ok = WildWindMetaMechanics.ValidateConsumableLoadout(consumables, out string reason);
        return SetResult(ok, "Consumable loadout valid: " + consumables.Count + "/5.", reason);
    }

    private WildWindMetaPortActionResult SetMissionOrder()
    {
        missionOrder = "Priority board: sortie objective -> daily silver quest -> trader reputation -> event chain";
        dailyQuestProgress = Mathf.Min(dailyQuestTarget, dailyQuestProgress + 250);
        WildWindMetaMechanics.AddStorage(account, "mission_pin", 1);
        return SetResult(true, "Mission board pinned sortie, daily, trader and event objectives.", "");
    }

    private WildWindMetaPortActionResult ClaimDailyQuest()
    {
        dailyQuestProgress = dailyQuestTarget;
        ContainerLot lot = WildWindMetaMechanics.OpenContainer(catalog.dailyContainerLots, containerSeed++);
        if (lot != null)
        {
            WildWindMetaMechanics.AddStorage(account, lot.itemId, lot.amount);
            lastContainerDrop = lot.itemId + " x" + lot.amount;
        }

        WildWindMetaMechanics.AddRepeatableBattlePassProgress(account, 250, 0);
        return SetResult(true, "Daily quest completed and one configured container lot claimed.", "");
    }

    private WildWindMetaPortActionResult SetMiningOrder()
    {
        miningOrder = "Armored boulders: scan -> break crust with AP/vibro-ram -> catch chunks with crusher";
        account.unlockedT1Modules.Add("crusher");
        account.unlockedT1Modules.Add("vibro_ram");
        return SetResult(true, "Mining order set for armored ore extraction.", "");
    }

    private WildWindMetaPortActionResult SetGasOrder()
    {
        gasOrder = "Harvest center concentration with protected drone, avoid electric pulses near explosive boulders";
        account.unlockedT1Modules.Add("gas_harvester");
        return SetResult(true, "Gas plan set for protected center harvesting.", "");
    }

    private WildWindMetaPortActionResult SetScanOrder()
    {
        scanOrder = "Boulder passport first, then relic scan, then cloud and drone sweep";
        account.unlockedT1Modules.Add("directional_scanner");
        return SetResult(true, "Scan focus set to boulder/relic passports.", "");
    }

    private WildWindMetaPortActionResult SetHackingOrder()
    {
        hackingOrder = "Exposure gate: scan object to full passport, run ship hacker, failed attempts raise threat";
        account.unlockedT1Modules.Add("hacker");
        WildWindMetaMechanics.AddStorage(account, "intel_cache", 1);
        return SetResult(true, "Hacking plan set for exposure-first object opening.", "");
    }

    private WildWindMetaPortActionResult SetThreatOrder()
    {
        threatOrder = "Quiet profile: suppressed guns, no failed hacks, no rams, avoid explosive boulder chains";
        threatStars = Mathf.Max(0, threatStars - 1);
        account.unlockedT1Modules.Add("quiet_cannon");
        WildWindMetaMechanics.AddStorage(account, "threat_protocol", 1);
        return SetResult(true, "Threat protocol set to quiet profile.", "");
    }

    private WildWindMetaPortActionResult SetFireControlOrder()
    {
        fireControlOrder = "Dangerous-only: drones, kamikazes and explosive chain threats; resource fire locked";
        account.unlockedT1Modules.Add("cannon");
        return SetResult(true, "Fire filter set to dangerous-only automation.", "");
    }

    private WildWindMetaPortActionResult SetDroneOrder()
    {
        droneOrder = "Catchers recover chunks, tugs isolate explosive rocks, salvagers recover wrecks on islands";
        WildWindMetaMechanics.AddStorage(account, "drone_task_tokens", 1);
        return SetResult(true, "Drone task board updated for catcher/tug/salvage work.", "");
    }

    private WildWindMetaPortActionResult SetLeviathanOrder()
    {
        leviathanOrder = "Hunt prep: scan leviathan, bait hazard route, arm harpoon and fire-control priority";
        account.unlockedT1Modules.Add("harpoon");
        account.unlockedT1Modules.Add("heavy_cannon");
        WildWindMetaMechanics.AddStorage(account, "leviathan_bait_marker", 1);
        return SetResult(true, "Leviathan hunt plan set with scan, bait, harpoon and hazard route.", "");
    }

    private WildWindMetaPortActionResult SetSalvageOrder()
    {
        salvageOrder = "Island salvage: crane small wrecks, salvager drones dismantle heavy hulls, catcher intercepts falls";
        account.unlockedT1Modules.Add("crane");
        WildWindMetaMechanics.AddStorage(account, "salvage_beacon", 1);
        return SetResult(true, "Salvage plan set for wreck recovery and on-site dismantling.", "");
    }

    private WildWindMetaPortActionResult SetSublikatOrder()
    {
        sublikatOrder = "Sublikat route: high-altitude ice field, avoid damage, extract with crane or protected drones";
        account.unlockedT1Modules.Add("thermal_insulation");
        WildWindMetaMechanics.AddStorage(account, "sublikat_marker", 1);
        return SetResult(true, "Sublikat plan set for high-altitude cold extraction.", "");
    }

    private WildWindMetaPortActionResult SetRelicOrder()
    {
        relicOrder = "Relic extraction: wait below half health, no damage tools, crane or catcher drone only";
        account.unlockedT1Modules.Add("crane");
        WildWindMetaMechanics.AddStorage(account, "relic_care_protocol", 1);
        return SetResult(true, "Relic plan set for no-damage extraction.", "");
    }

    private WildWindMetaPortActionResult SetEnvironmentOrder()
    {
        environmentOrder = "Scout marks fog, cold, storm floor, wind and claudium fields before route commit";
        WildWindMetaMechanics.AddStorage(account, "region_passport", 1);
        return SetResult(true, "Region protocol set to scout passport before launch.", "");
    }

    private WildWindMetaPortActionResult CraftT2Frame()
    {
        if (WildWindMetaMechanics.GetStorage(account, "ferron") < 10 || WildWindMetaMechanics.GetStorage(account, "charcoal") < 8)
        {
            return SetResult(false, "", "Need ferron 10 and charcoal 8.");
        }

        WildWindMetaMechanics.AddStorage(account, "ferron", -10);
        WildWindMetaMechanics.AddStorage(account, "charcoal", -8);
        WildWindMetaMechanics.AddStorage(account, "pioneer_mining_t2_frame", 1);
        WildWindMetaMechanics.AddRepeatableBattlePassProgress(account, 0, 1);
        return SetResult(true, "Cascade crafted Pioneer Mining T2 frame; battle pass repeatable progressed.", "");
    }

    private WildWindMetaPortActionResult LearnT3Recipe()
    {
        if (account.recipes.ContainsKey("precursor_t3_frame"))
        {
            return SetResult(false, "", "T3 recipe already learned.");
        }

        if (WildWindMetaMechanics.GetStorage(account, "precursor_fragment") < 3)
        {
            return SetResult(false, "", "Need 3 precursor fragments.");
        }

        WildWindMetaMechanics.AddStorage(account, "precursor_fragment", -3);
        bool learned = WildWindMetaMechanics.LearnRecipe(account, "precursor_t3_frame", RecipeTier.T3);
        selectedRecipeId = "precursor_t3_frame";
        return SetResult(learned, "T3 precursor recipe assembled from fragments.", "Recipe learn blocked.");
    }

    private WildWindMetaPortActionResult AcceptTraderTask()
    {
        account.unlockedT1Modules.Add("crane");
        WildWindMetaMechanics.AddStorage(account, "geology_reputation", 10);
        WildWindMetaMechanics.AddStorage(account, "datacore_industrial", 1);
        return SetResult(true, "Trader task accepted: gather ore, reward reputation and datacore.", "");
    }

    private WildWindMetaPortActionResult BuyTraderRecipe()
    {
        bool learned = account.recipes.ContainsKey("trader_t2_harpoon")
            || WildWindMetaMechanics.LearnRecipe(account, "trader_t2_harpoon", RecipeTier.T2);
        return SetResult(learned, "Trader T2 harpoon recipe unlocked.", "Recipe already owned.");
    }

    private WildWindMetaPortActionResult BuyBundle()
    {
        Dictionary<string, int> rewards = new Dictionary<string, int>
        {
            { "datacore_industrial", 1 },
            { "event_token", 1 },
            { "good_oil", 2 }
        };
        return SetResult(WildWindMetaMechanics.BuyInternalGoldBundle(account, 75, rewards), "Bundle bought for internal gold.", "Bundle blocked.");
    }

    private WildWindMetaPortActionResult BuySubscription()
    {
        if (account.gold < 40)
        {
            return SetResult(false, "", "Need 40 gold for supply contract.");
        }

        account.gold -= 40;
        WildWindMetaMechanics.AddStorage(account, "supply_contract_days", 7);
        return SetResult(true, "Supply subscription bought with internal gold for 7 days.", "");
    }

    private WildWindMetaPortActionResult OpenContainer()
    {
        ContainerLot lot = WildWindMetaMechanics.OpenContainer(catalog.dailyContainerLots, containerSeed++);
        if (lot == null) return SetResult(false, "", "No container lots configured.");
        WildWindMetaMechanics.AddStorage(account, lot.itemId, lot.amount);
        lastContainerDrop = lot.itemId + " x" + lot.amount;
        return SetResult(true, "Container opened: " + lastContainerDrop + ".", "");
    }

    private WildWindMetaPortActionResult ToggleContract()
    {
        if (string.IsNullOrWhiteSpace(account.activeContractId))
        {
            return SetResult(WildWindMetaMechanics.ActivateBattlePassContract(account, "small_contract_ore"), "Small contract activated.", "Contract blocked.");
        }

        WildWindMetaMechanics.AbandonBattlePassContract(account);
        return SetResult(true, "Small contract abandoned; slot is free.", "");
    }

    private WildWindMetaPortActionResult SpinEvent()
    {
        if (account.gold < 50)
        {
            return SetResult(false, "", "Need 50 internal gold for event spin.");
        }

        account.gold -= 50;
        eventSpinCount++;
        string reward = eventSpinCount % 3 == 0 ? "unique_t2_gun" : "precursor_fragment";
        WildWindMetaMechanics.AddStorage(account, reward, 1);
        lastEventReward = reward + " x1";
        return SetResult(true, "Event roulette reward: " + lastEventReward + ".", "");
    }

    private WildWindMetaPortActionResult UnlockTechnology(string techId, int cost)
    {
        if (unlockedTechnologies.Contains(techId))
        {
            return SetResult(false, "", "Technology already unlocked.");
        }

        if (account.researchXp < cost)
        {
            return SetResult(false, "", "Need " + cost + " research XP.");
        }

        account.researchXp -= cost;
        unlockedTechnologies.Add(techId);
        technologyLevels[techId] = 1;
        return SetResult(true, "Technology unlocked: " + techId + ".", "");
    }

    private WildWindMetaPortActionResult UpgradeTechnology(string techId, int cost)
    {
        if (!unlockedTechnologies.Contains(techId))
        {
            unlockedTechnologies.Add(techId);
            technologyLevels[techId] = 1;
        }

        if (account.researchXp < cost)
        {
            return SetResult(false, "", "Need " + cost + " research XP.");
        }

        account.researchXp -= cost;
        technologyLevels.TryGetValue(techId, out int level);
        technologyLevels[techId] = Mathf.Max(1, level) + 1;
        return SetResult(true, "Technology upgraded: " + techId + " L" + technologyLevels[techId] + ".", "");
    }

    private WildWindMetaPortActionResult FromMeta(MetaOperationResult result)
    {
        if (result == null) return SetResult(false, "", "No result.");
        return SetResult(result.success, result.message, result.message);
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

    private void ApplyRuntimeSnapshot()
    {
        if (runtimeMeta == null || runtimeMeta.progress == null || account == null || catalog == null)
        {
            return;
        }

        runtimeMeta.EnsureProgressInitialized();
        PlayerProgress progress = runtimeMeta.progress;
        runtimeSnapshotApplied = true;

        account.silver = Mathf.Max(account.silver, progress.money);
        account.constructionXp = Mathf.Max(account.constructionXp, progress.researchedNodeIds != null ? progress.researchedNodeIds.Count * 25 : 0);
        account.researchXp = Mathf.Max(account.researchXp, progress.GetResourceAmount(SessionExtractionConstants.FundamentalExperienceItemId));
        account.industrialXp = Mathf.Max(account.industrialXp, progress.GetResourceAmount("industrial_experience"));
        account.militaryXp = Mathf.Max(account.militaryXp, progress.GetResourceAmount("military_experience"));

        CopyRuntimeStacks(progress.inventory);
        CopyRuntimeStacks(progress.shipCargo);
        CopyRuntimeStacks(progress.shipImpactCargo);
        IslandProductionState storage = runtimeMeta.GetCapitalStorageState();
        if (storage != null)
        {
            CopyRuntimeStacks(storage.storage);
        }

        ApplyRuntimeShipSnapshot(progress);
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
                silverCost = 0,
                constructionXpUnlockCost = 0,
                maxSorties = 7,
                highSlots = 2,
                midSlots = 1,
                lowSlots = 1,
                rigSlots = 1
            };
            catalog.ships[hullId] = definition;
        }

        CountRuntimeSlots(hull, definition);
        account.unlockedShips.Add(hullId);

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
        if (!IsRuntimeBound) return "Source: demo.";
        string sortie = runtimeMeta.IsSessionExtractionCoreMode ? runtimeMeta.GetSelectedSessionSortieDisplayName() : "-";
        return "Source: runtime MetaGameState | mode " + runtimeMeta.CurrentMode + " | sortie " + sortie;
    }

    private WildWindMetaPortActionResult RunRuntimeSortieSelectionAction()
    {
        if (!IsRuntimeBound || !runtimeMeta.IsSessionExtractionCoreMode) return null;

        bool changed = runtimeMeta.SelectNextSessionSortie(out string message);
        return SetResult(true, changed ? "Runtime sortie selected: " + message : "Runtime sortie board checked: " + message, "");
    }

    private WildWindMetaPortActionResult RunRuntimeFittingAction()
    {
        if (!IsRuntimeBound || !runtimeMeta.IsSessionExtractionCoreMode) return null;

        if (runtimeMeta.CanInstallNextStarterFittingUpgrade(out string canMessage)
            && runtimeMeta.TryInstallNextStarterFittingUpgrade(out string installMessage))
        {
            ApplyRuntimeSnapshot();
            return SetResult(true, installMessage, "");
        }

        return SetResult(true, "Runtime fitting checked: " + canMessage, "");
    }

    private WildWindMetaPortActionResult RunRuntimeProcessingAction()
    {
        if (!IsRuntimeBound || !runtimeMeta.IsSessionExtractionCoreMode) return null;

        bool processed = runtimeMeta.TryProcessNextBaseBatch(out string message);
        ApplyRuntimeSnapshot();
        return SetResult(true, processed ? message : "Runtime processing queue checked: " + message, "");
    }

    private WildWindMetaPortActionResult RunRuntimeCascadeAction()
    {
        if (!IsRuntimeBound || !runtimeMeta.IsSessionExtractionCoreMode) return null;

        bool completed = runtimeMeta.TryRunNextBaseCascadeOrder(out string message);
        ApplyRuntimeSnapshot();
        return SetResult(true, completed ? message : "Runtime cascade checked: " + runtimeMeta.GetNextBaseCascadeOrderOverviewText() + " " + message, "");
    }

    private string FormatRecipe(string recipeId)
    {
        if (account.recipes.TryGetValue(recipeId, out MetaRecipeState recipe))
        {
            return recipeId + ": " + recipe.tier + " L" + recipe.level
                + " yield x" + recipe.yieldMultiplier.ToString("0.00")
                + " speed x" + recipe.speedMultiplier.ToString("0.00");
        }

        return recipeId + ": not learned";
    }

    private float GetBuffer(string itemId)
    {
        return account.processingBuffers.TryGetValue("buffer:" + itemId, out float value) ? value : 0f;
    }

    private int GetTechnologyLevel(string techId)
    {
        return technologyLevels.TryGetValue(techId, out int level) ? level : 0;
    }

    private string FormatVouchers()
    {
        if (sortieVouchers.Count == 0) return "-";
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < sortieVouchers.Count; i++)
        {
            VoucherState voucher = sortieVouchers[i];
            if (i > 0) builder.Append(", ");
            builder.Append(voucher.type);
            builder.Append(" ");
            builder.Append(voucher.rarity);
            builder.Append(" x");
            builder.Append(voucher.multiplier.ToString("0.00"));
        }

        return builder.ToString();
    }

    private static string FormatShipName(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return "-";
        return shipId.Replace('_', ' ');
    }
}
