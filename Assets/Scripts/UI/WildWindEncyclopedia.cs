using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WildWindEncyclopedia : MonoBehaviour
{
    private const float LeftSafeMargin = 76f;
    private const float OuterPadding = 12f;
    private const float CategoryWidth = 174f;
    private const float ColumnGap = 10f;

    [Header("Данные")]
    [InspectorName("Папка CSV-конфигов внутри Assets")]
    public string configFolder = "Data/Config";

    [InspectorName("Загружать CSV")]
    public bool loadCsvConfigs = true;

    [Header("Вид")]
    [InspectorName("Максимум записей в списке")]
    [Range(40, 500)] public int maxVisibleEntries = 220;

    [InspectorName("Масштаб интерфейса")]
    [Range(0.75f, 1.4f)] public float uiScale = 1f;

    private readonly List<EncyclopediaEntry> entries = new List<EncyclopediaEntry>();
    private readonly List<CategoryDefinition> categories = new List<CategoryDefinition>();
    private readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();
    private readonly List<GameObject> spawnedEntryRows = new List<GameObject>();

    private RectTransform entryListContent;
    private RectTransform categoryListContent;
    private TMP_InputField searchInput;
    private TMP_Text detailTitle;
    private TMP_Text detailCategory;
    private TMP_Text detailBody;
    private Image detailIconPlate;
    private TMP_Text detailIconText;
    private TMP_Text counterText;
    private TMP_Text pageText;

    private string selectedCategoryId = "all";
    private string searchText = "";
    private EncyclopediaEntry selectedEntry;
    private int entryPage;
    private const int VisibleEntryRows = 8;

    private static readonly Color BackgroundColor = new Color(0.018f, 0.028f, 0.040f, 1f);
    private static readonly Color PanelColor = new Color(0.035f, 0.052f, 0.072f, 0.94f);
    private static readonly Color PanelSoftColor = new Color(0.055f, 0.078f, 0.104f, 0.88f);
    private static readonly Color AccentColor = new Color(0.95f, 0.66f, 0.28f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.84f, 0.70f, 1f);
    private static readonly Color MutedTextColor = new Color(0.62f, 0.66f, 0.70f, 1f);

    private void Awake()
    {
        BuildEntries();
        BuildInterface();
        SelectCategory("all");
    }

    [ContextMenu("Пересобрать энциклопедию")]
    public void RebuildNow()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        entries.Clear();
        categories.Clear();
        categoryButtons.Clear();
        spawnedEntryRows.Clear();
        BuildEntries();
        BuildInterface();
        SelectCategory("all");
    }

    private void BuildEntries()
    {
        RegisterCategories();
        AddDesignOverviewEntries();
        AddShipMatrixEntries();
        AddRoleTechnologyEntries();
        AddRoleModuleEntries();

        if (loadCsvConfigs)
        {
            AddCsvConfigEntries();
        }

        entries.Sort((a, b) =>
        {
            int category = string.Compare(GetCategoryName(a.CategoryId), GetCategoryName(b.CategoryId), StringComparison.Ordinal);
            if (category != 0) return category;
            return string.Compare(a.Title, b.Title, StringComparison.Ordinal);
        });
    }

    private void RegisterCategories()
    {
        categories.Add(new CategoryDefinition("all", "Все записи", "ALL", new Color(0.92f, 0.68f, 0.36f, 1f)));
        categories.Add(new CategoryDefinition("overview", "Обзор", "ОБ", new Color(0.74f, 0.86f, 1.00f, 1f)));
        categories.Add(new CategoryDefinition("ships", "Корабли", "КР", new Color(0.70f, 0.58f, 0.34f, 1f)));
        categories.Add(new CategoryDefinition("components", "Компоненты", "УЗ", new Color(0.55f, 0.72f, 0.82f, 1f)));
        categories.Add(new CategoryDefinition("tech", "Технологии", "ТХ", new Color(0.74f, 0.68f, 1.00f, 1f)));
        categories.Add(new CategoryDefinition("resources", "Ресурсы", "РС", new Color(0.58f, 0.82f, 0.62f, 1f)));
        categories.Add(new CategoryDefinition("islands", "Острова", "ОС", new Color(0.84f, 0.70f, 0.45f, 1f)));
        categories.Add(new CategoryDefinition("production", "Производства", "ПР", new Color(0.95f, 0.52f, 0.38f, 1f)));
        categories.Add(new CategoryDefinition("recipes", "Рецепты", "РЦ", new Color(0.85f, 0.78f, 0.55f, 1f)));
        categories.Add(new CategoryDefinition("world", "Мир", "МИ", new Color(0.42f, 0.68f, 0.95f, 1f)));
    }

    private void AddDesignOverviewEntries()
    {
        AddEntry("overview", "wiki_design_goal", "Зачем нужна энциклопедия", "ОБ",
            "Единая вики для игрока и разработчика.",
            "Энциклопедия должна объяснять не только предметы, но и связи: что открывает технологию, где производится компонент, какой корабль использует ресурс, какой остров нужен для следующего шага.\n\nВ будущем это станет игровым интерфейсом познания мира: неизвестные записи можно скрывать, показывать как слухи или открывать через разведку, исследования и рейсы.");

        AddEntry("overview", "ship_rank_ladder", "7 рангов кораблей", "R7",
            "Катер, малый, средний, большой, тяжёлый, capital, корабль-город.",
            "Ранг в Wild Wind - это физический и экономический порог.\n\nR1-R2: простые контуры и ручное обслуживание.\nR3: корабль требует экипаж, док и регулярный ремонт.\nR4: появляется дальняя логистика и опасность ветра.\nR5: без автоматизации процесс становится слишком тяжёлым.\nR6: корабль превращается в центр флота.\nR7: корабль-город является подвижной инфраструктурой цивилизации.");

        AddEntry("overview", "world_layer_model", "Высотные слои мира", "Y",
            "Мир читается по высоте: буря, обитание, разреженная зона, лёд.",
            "0 м: смертельная буря.\n0-1000 м: яростная буря, урон, плохая видимость.\n1000-2000 м: спокойная буря, умеренный риск.\n2000-10000 м: зона обитания, острова, основные облака и глыбы.\n10000-40000 м: разреженная зона, редкие ресурсы, сильные постоянные ветра.\n40000-100000 м: ледяная зона, айсберги и сублиматы.\n100000 м: клавдий теряет подъёмную силу.");

        AddEntry("overview", "content_graph_rule", "Правило связности контента", "СВ",
            "Новая штука должна иметь источник, применение и путь открытия.",
            "Для каждого ресурса, корабля, технологии и рецепта нужно ответить на четыре вопроса:\n\n1. Где это появляется?\n2. Что это открывает?\n3. Какой процесс игрок этим улучшает?\n4. Как большой тест поймёт, что связь не сломана?");
    }

    private void AddShipMatrixEntries()
    {
        RoleDefinition[] roles = GetRoleDefinitions();
        for (int r = 0; r < roles.Length; r++)
        {
            RoleDefinition role = roles[r];
            AddEntry("ships", "ship_role_" + role.Id, role.Name, role.Icon,
                role.Summary,
                role.Description + "\n\nГлавная цифра: " + role.MainStat + ".\nРиск роли: " + role.Risk + ".\n\nЛинейка кораблей:\n" + BuildRankList(role.Ships));

            for (int rank = 0; rank < role.Ships.Length; rank++)
            {
                int rankNumber = rank + 1;
                string title = role.Ships[rank];
                string id = "ship_" + role.Id + "_r" + rankNumber;
                AddEntry("ships", id, title, "R" + rankNumber,
                    "Ранг " + rankNumber + ". Роль: " + role.Name + ".",
                    "Корабль роли \"" + role.Name + "\".\n\nФункция ранга: " + GetRankFunction(rankNumber) +
                    "\nПрофильная цифра: " + role.MainStat +
                    "\nКлючевой риск: " + role.Risk +
                    "\nОткрывается через: tech_role_" + role.Id + "_" + rankNumber +
                    "\nРолевой модуль: comp_role_" + role.Id + "_r" + rankNumber + ".");
            }
        }
    }

    private void AddRoleTechnologyEntries()
    {
        RoleDefinition[] roles = GetRoleDefinitions();
        for (int r = 0; r < roles.Length; r++)
        {
            RoleDefinition role = roles[r];
            for (int rank = 1; rank <= 7; rank++)
            {
                AddEntry("tech", "tech_role_" + role.Id + "_" + rank, role.TechNames[rank - 1], "T" + rank,
                    "Технология роли \"" + role.Name + "\" ранга " + rank + ".",
                    "Открывает корабль: " + role.Ships[rank - 1] +
                    "\nУсиливает цифру: " + role.MainStat +
                    "\nЭкономический смысл: " + GetRankBottleneck(rank) +
                    "\nСвязанный модуль: comp_role_" + role.Id + "_r" + rank + ".");
            }
        }

        for (int rank = 1; rank <= 7; rank++)
        {
            AddEntry("tech", "tech_ship_rank_" + rank, "Корабли ранга " + rank, "R" + rank,
                "Фундаментальное открытие масштаба кораблей.",
                "Открывает сборку кораблей ранга " + rank + ".\n\n" +
                GetRankFunction(rank) + "\n\nНовый bottleneck: " + GetRankBottleneck(rank) + ".");
        }
    }

    private void AddRoleModuleEntries()
    {
        RoleDefinition[] roles = GetRoleDefinitions();
        for (int r = 0; r < roles.Length; r++)
        {
            RoleDefinition role = roles[r];
            for (int rank = 1; rank <= 7; rank++)
            {
                string id = "comp_role_" + role.Id + "_r" + rank;
                AddEntry("components", id, role.Name + " R" + rank + ": ролевой модуль", role.Icon,
                    "Ролевой модуль для корабля ранга " + rank + ".",
                    "Этот узел превращает базовый корпус ранга " + rank + " в корабль роли \"" + role.Name + "\".\n\n" +
                    "Главная цифра: " + role.MainStat +
                    "\nРиск: " + role.Risk +
                    "\nРецептурная надбавка: " + role.RecipeSurcharge +
                    "\nТехнология открытия: tech_role_" + role.Id + "_" + rank + ".");
            }
        }
    }

    private void AddCsvConfigEntries()
    {
        WorldConfigDatabase config = new WorldConfigDatabase();
        config.LoadFromAssetsConfigFolder(configFolder);
        if (!config.isLoaded)
        {
            AddEntry("overview", "csv_load_error", "CSV не загрузились", "!",
                "Энциклопедия не смогла прочитать Assets/" + configFolder + ".",
                config.lastError);
            return;
        }

        for (int i = 0; i < config.items.Count; i++)
        {
            ItemConfig item = config.items[i];
            AddEntry("resources", "item_" + item.id, DisplayName(item.localNameRu, item.id), "РС",
                "Предмет из Item.csv.",
                "id: " + item.id + "\nЭнергия: " + FormatFloat(item.energyKwhPerKg) + " кВт⋅ч/кг.\n\nПока это сырой ресурсный объект. Позже сюда добавим источники, рецепты и потребителей.");
        }

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            AddEntry("islands", "island_" + island.id, DisplayName(island.localNameRu, island.id), "ОС",
                "Остров из Island.csv.",
                "id: " + island.id +
                "\nПозиция: " + FormatVector(island.position) +
                "\nАрхетип: " + JoinOrDash(island.archetypeId) +
                "\nБазовое производство: " + island.productionId +
                "\nРадиус стыковки: " + FormatFloat(island.dockingRadius) + " м" +
                "\nСкорость загрузки: 1 предмет за " + FormatFloat(island.timeForOneItemLoadSeconds) + " сек.");
        }

        for (int i = 0; i < config.islandArchetypes.Count; i++)
        {
            IslandArchetypeConfig archetype = config.islandArchetypes[i];
            AddEntry("islands", "island_archetype_" + archetype.id, archetype.DisplayNameRu, "ТИ",
                "Архетип острова.",
                "id: " + archetype.id +
                "\nВысотная полоса: " + archetype.heightBand +
                "\nБазовый ресурс: " + config.GetItemNameRu(archetype.baseProductionItemId) +
                "\nСтартовая потребность: " + config.GetItemNameRu(archetype.startNeedItemId) +
                "\n\n" + archetype.descriptionRu);
        }

        for (int i = 0; i < config.islandArchetypeStages.Count; i++)
        {
            IslandArchetypeStageConfig stage = config.islandArchetypeStages[i];
            AddEntry("islands", "island_stage_" + stage.id, stage.DisplayNameRu, "СТ",
                "Стадия развития острова.",
                "id: " + stage.id +
                "\nАрхетип: " + stage.archetypeId +
                "\nНомер стадии: " + stage.stageIndex +
                "\nНужен ресурс: " + config.GetItemNameRu(stage.triggerNeedItemId) +
                "\nОткрывает ресурс: " + (string.IsNullOrWhiteSpace(stage.unlockedProductionItemId) ? "-" : config.GetItemNameRu(stage.unlockedProductionItemId)) +
                "\nМножитель базовой выработки: x" + FormatFloat(stage.productionMultiplier) +
                "\nОткрывает общественные потребности: " + (stage.opensSocialNeeds ? "да" : "нет") +
                "\n\n" + stage.descriptionRu);
        }

        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            AddEntry("islands", "island_need_" + need.id, need.DisplayNameRu, "ПО",
                "Общественная потребность острова.",
                "id: " + need.id +
                "\nТип: " + need.kind +
                "\nВосстанавливается: " + config.GetItemNameRu(need.recoveryItemId) +
                "\nМаксимум: " + FormatFloat(need.maxValue) +
                "\nВосстановление за предмет: " + FormatFloat(need.restorePerItem) +
                "\nБазовое падение в час: " + FormatFloat(need.baseDecayPerHour) +
                "\nПадение за нагрузку в час: " + FormatFloat(need.loadDecayPerHour) +
                "\nПорог удовлетворения: " + FormatFloat(need.satisfiedThreshold) +
                "\n\n" + need.descriptionRu);
        }

        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            AddEntry("tech", "csv_tech_" + technology.id, DisplayName(technology.localNameRu, technology.id), "ТХ",
                "Технология из Technology.csv.",
                "id: " + technology.id +
                "\nЦикл: " + technology.cycleTimeSeconds + " сек." +
                "\nЦиклов: " + technology.requiredCycles +
                "\nУсловия: " + JoinOrDash(technology.prerequisiteTechnologyIds) +
                "\nЦена цикла: " + FormatTechCosts(technology.cycleCost, config));
        }

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            AddEntry("production", "industry_" + industry.id, industry.DisplayNameRu, "ПР",
                "Производственная линия: " + industry.kind + ".",
                "id: " + industry.id +
                "\nОстров: " + industry.islandId +
                "\nТип: " + industry.kind +
                "\nРецепт: " + industry.recipeId +
                "\nЗдание: " + JoinOrDash(industry.buildingId));
        }

        for (int i = 0; i < config.islandBuildings.Count; i++)
        {
            IslandBuildingConfig building = config.islandBuildings[i];
            AddEntry("production", "building_" + building.id, building.DisplayNameRu, building.IsService ? "СВ" : "ЗД",
                building.IsService ? "Сервисное здание острова." : "Производственное здание острова.",
                "id: " + building.id +
                "\nКатегория: " + building.category +
                "\nТип производства: " + building.industryKind +
                "\nСервисная роль: " + JoinOrDash(building.serviceRole) +
                "\nТехнология: " + JoinOrDash(building.requiredTechnologyId) +
                "\nМаксимальный уровень: " + building.maxUpgradeLevel +
                "\nВосстановление острова после стройки: " + FormatFloat(building.constructionRecoveryHours) + " ч" +
                "\nНагрузки: рабочая сила " + building.workforceLoad +
                ", здоровье " + building.healthLoad +
                ", безопасность " + building.safetyLoad +
                ", комфорт " + building.comfortLoad +
                ", творчество " + building.creativityLoad +
                "\nСтройка: " + FormatAmounts(building.constructionInputs, config) +
                "\n\n" + building.descriptionRu);
        }

        for (int i = 0; i < config.industryRecipes.Count; i++)
        {
            IndustryRecipeConfig recipe = config.industryRecipes[i];
            AddEntry("recipes", "recipe_" + recipe.id, recipe.DisplayNameRu, "РЦ",
                "Рецепт производства: " + recipe.kind + ".",
                BuildRecipeBody(recipe, config));
        }

        for (int i = 0; i < config.gasCloudTypes.Count; i++)
        {
            GasCloudTypeConfig cloudType = config.gasCloudTypes[i];
            AddEntry("world", "gas_type_" + cloudType.id, DisplayName(cloudType.localNameRu, cloudType.id), "ГЗ",
                "Тип газового облака.",
                "id: " + cloudType.id +
                "\nКонденсат: " + config.GetItemNameRu(cloudType.condensateItemId) +
                "\nЛитров на м3: " + FormatFloat(cloudType.condensateLitersPerCubicMeter) +
                "\nСостав: " + FormatGasComposition(cloudType, config));
        }

        for (int i = 0; i < config.oreTypes.Count; i++)
        {
            OreTypeConfig ore = config.oreTypes[i];
            AddEntry("world", "ore_type_" + ore.id, DisplayName(ore.localNameRu, ore.id), "РУ",
                "Тип руды.",
                "id: " + ore.id +
                "\nПредмет руды: " + config.GetItemNameRu(ore.oreItemId) +
                "\nЦенность: " + FormatFloat(ore.baseValue) +
                "\nЕстественное осыпание: " + FormatFloat(ore.naturalShedKgPerMinute) + " кг/мин" +
                "\nСостав: " + FormatOreComposition(ore, config));
        }

        for (int i = 0; i < config.leviathanTypes.Count; i++)
        {
            LeviathanTypeConfig type = config.leviathanTypes[i];
            AddEntry("world", "leviathan_type_" + type.id, type.DisplayNameRu, "ЛВ",
                "Тип левиафана.",
                "id: " + type.id +
                "\nДлина: " + FormatFloat(type.bodyLengthMeters) + " м" +
                "\nМасса: " + FormatFloat(type.massKg) + " кг" +
                "\nЗдоровье: " + FormatFloat(type.maxHealth) +
                "\nКрейсерская скорость: " + FormatFloat(type.cruiseSpeedMS) + " м/с" +
                "\nТуша: " + config.GetItemNameRu(type.carcassItemId) + ", примерно " + type.CarcassMassKg + " кг.");
        }
    }

    private void AddEntry(string categoryId, string id, string title, string iconLabel, string summary, string body)
    {
        entries.Add(new EncyclopediaEntry
        {
            CategoryId = categoryId,
            Id = id,
            Title = title,
            IconLabel = iconLabel,
            Summary = summary,
            Body = body,
            SearchBlob = (id + " " + title + " " + summary + " " + body).ToLowerInvariant()
        });
    }

    private void BuildInterface()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.scaleFactor = uiScale;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = CreateRect("Root", transform);
        Stretch(root);
        CreateImage(root, "Background", BackgroundColor);

        RectTransform top = CreatePanel(root, "Top Bar", new Color(0.020f, 0.034f, 0.050f, 0.98f));
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(0f, -70f);
        top.offsetMax = Vector2.zero;

        TMP_Text title = CreateText(top, "Title", "Энциклопедия Wild Wind", 24, FontStyles.Bold, TextColor);
        title.rectTransform.anchorMin = new Vector2(0f, 0f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.offsetMin = new Vector2(LeftSafeMargin + 18f, 0f);
        title.rectTransform.offsetMax = new Vector2(LeftSafeMargin + 350f, 0f);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        counterText = CreateText(top, "Counter", "", 12, FontStyles.Normal, MutedTextColor);
        counterText.rectTransform.anchorMin = new Vector2(0f, 0f);
        counterText.rectTransform.anchorMax = new Vector2(0f, 1f);
        counterText.rectTransform.offsetMin = new Vector2(LeftSafeMargin + 356f, 0f);
        counterText.rectTransform.offsetMax = new Vector2(LeftSafeMargin + 700f, 0f);
        counterText.alignment = TextAlignmentOptions.MidlineLeft;

        searchInput = CreateInput(top, "Search Input", "Поиск по названию, id, роли, ресурсу...");
        searchInput.textComponent.fontSize = 15f;
        searchInput.placeholder.GetComponent<TMP_Text>().fontSize = 14f;
        RectTransform searchRect = searchInput.GetComponent<RectTransform>();
        searchRect.anchorMin = new Vector2(1f, 0.5f);
        searchRect.anchorMax = new Vector2(1f, 0.5f);
        searchRect.pivot = new Vector2(1f, 0.5f);
        searchRect.sizeDelta = new Vector2(460f, 40f);
        searchRect.anchoredPosition = new Vector2(-16f, 0f);
        searchInput.onValueChanged.AddListener(value =>
        {
            searchText = value ?? "";
            selectedEntry = null;
            entryPage = 0;
            RefreshEntryList();
        });

        RectTransform main = CreateRect("Main Area", root);
        main.anchorMin = new Vector2(0f, 0f);
        main.anchorMax = new Vector2(1f, 1f);
        main.offsetMin = new Vector2(LeftSafeMargin + OuterPadding, OuterPadding);
        main.offsetMax = new Vector2(-12f, -82f);

        RectTransform categoryPanel = CreatePanel(main, "Category Panel", PanelColor);
        SetLeftFixedPanel(categoryPanel, 0f, CategoryWidth);
        categoryListContent = categoryPanel;
        BuildCategoryButtons();

        RectTransform listPanel = CreatePanel(main, "Entry List Panel", PanelColor);
        listPanel.anchorMin = new Vector2(0f, 0f);
        listPanel.anchorMax = new Vector2(0.45f, 1f);
        listPanel.pivot = new Vector2(0f, 0.5f);
        listPanel.offsetMin = new Vector2(CategoryWidth + ColumnGap, 0f);
        listPanel.offsetMax = new Vector2(-ColumnGap, 0f);
        entryListContent = listPanel;
        BuildEntryPager(listPanel);

        RectTransform detailPanel = CreatePanel(main, "Detail Panel", PanelColor);
        detailPanel.anchorMin = new Vector2(0.45f, 0f);
        detailPanel.anchorMax = new Vector2(1f, 1f);
        detailPanel.pivot = new Vector2(0f, 0.5f);
        detailPanel.offsetMin = new Vector2(ColumnGap, 0f);
        detailPanel.offsetMax = Vector2.zero;
        BuildDetailPanel(detailPanel);
    }

    private void BuildCategoryButtons()
    {
        for (int i = 0; i < categories.Count; i++)
        {
            CategoryDefinition category = categories[i];
            Button button = CreateButton(categoryListContent, "Category " + category.Id, category.Name, category.IconLabel, category.Color, 34f);
            PlaceTopRow(button.GetComponent<RectTransform>(), i, 34f, 5f, 10f, 8f);
            string categoryId = category.Id;
            button.onClick.AddListener(() => SelectCategory(categoryId));
            categoryButtons[category.Id] = button;
        }
    }

    private void BuildEntryPager(RectTransform parent)
    {
        Button previous = CreateButton(parent, "Previous Page", "Назад", "<", AccentColor, 36f);
        RectTransform previousRect = previous.GetComponent<RectTransform>();
        previousRect.anchorMin = new Vector2(0f, 0f);
        previousRect.anchorMax = new Vector2(0f, 0f);
        previousRect.pivot = new Vector2(0f, 0f);
        previousRect.sizeDelta = new Vector2(100f, 36f);
        previousRect.anchoredPosition = new Vector2(10f, 10f);
        previous.onClick.AddListener(() =>
        {
            entryPage = Mathf.Max(0, entryPage - 1);
            selectedEntry = null;
            RefreshEntryList();
        });

        Button next = CreateButton(parent, "Next Page", "Дальше", ">", AccentColor, 36f);
        RectTransform nextRect = next.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0f);
        nextRect.anchorMax = new Vector2(1f, 0f);
        nextRect.pivot = new Vector2(1f, 0f);
        nextRect.sizeDelta = new Vector2(112f, 36f);
        nextRect.anchoredPosition = new Vector2(-10f, 10f);
        next.onClick.AddListener(() =>
        {
            entryPage++;
            selectedEntry = null;
            RefreshEntryList();
        });

        pageText = CreateText(parent, "Page Text", "", 12, FontStyles.Normal, MutedTextColor);
        pageText.rectTransform.anchorMin = new Vector2(0f, 0f);
        pageText.rectTransform.anchorMax = new Vector2(1f, 0f);
        pageText.rectTransform.pivot = new Vector2(0.5f, 0f);
        pageText.rectTransform.sizeDelta = new Vector2(0f, 36f);
        pageText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        pageText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildDetailPanel(RectTransform parent)
    {
        RectTransform header = CreateRect("Detail Header", parent);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(18f, -106f);
        header.offsetMax = new Vector2(-18f, -14f);

        detailIconPlate = CreateImage(header, "Detail Icon Plate", AccentColor);
        RectTransform iconRect = detailIconPlate.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(56f, 56f);
        iconRect.anchoredPosition = Vector2.zero;

        detailIconText = CreateText(iconRect, "Detail Icon Text", "", 16, FontStyles.Bold, new Color(0.02f, 0.03f, 0.04f, 1f));
        Stretch(detailIconText.rectTransform);
        detailIconText.alignment = TextAlignmentOptions.Center;

        detailTitle = CreateText(header, "Detail Title", "Выбери запись", 22, FontStyles.Bold, TextColor);
        detailTitle.rectTransform.anchorMin = new Vector2(0f, 0.42f);
        detailTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        detailTitle.rectTransform.offsetMin = new Vector2(72f, -8f);
        detailTitle.rectTransform.offsetMax = Vector2.zero;
        detailTitle.alignment = TextAlignmentOptions.BottomLeft;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;

        detailCategory = CreateText(header, "Detail Category", "", 12, FontStyles.Normal, MutedTextColor);
        detailCategory.rectTransform.anchorMin = new Vector2(0f, 0f);
        detailCategory.rectTransform.anchorMax = new Vector2(1f, 0.42f);
        detailCategory.rectTransform.offsetMin = new Vector2(72f, 5f);
        detailCategory.rectTransform.offsetMax = Vector2.zero;
        detailCategory.alignment = TextAlignmentOptions.TopLeft;

        RectTransform bodyViewport = CreatePanel(parent, "Detail Body Panel", PanelSoftColor);
        bodyViewport.anchorMin = new Vector2(0f, 0f);
        bodyViewport.anchorMax = new Vector2(1f, 1f);
        bodyViewport.offsetMin = new Vector2(18f, 18f);
        bodyViewport.offsetMax = new Vector2(-18f, -126f);

        detailBody = CreateText(bodyViewport, "Detail Body", "Слева список записей. Сверху поиск. Категории работают как фильтр.", 14, FontStyles.Normal, TextColor);
        Stretch(detailBody.rectTransform);
        detailBody.rectTransform.offsetMin = new Vector2(16f, 14f);
        detailBody.rectTransform.offsetMax = new Vector2(-16f, -14f);
        detailBody.alignment = TextAlignmentOptions.TopLeft;
        detailBody.textWrappingMode = TextWrappingModes.Normal;
        detailBody.overflowMode = TextOverflowModes.Truncate;
        detailBody.richText = true;
    }

    private void SelectCategory(string categoryId)
    {
        selectedCategoryId = categoryId;
        selectedEntry = null;
        entryPage = 0;

        foreach (KeyValuePair<string, Button> pair in categoryButtons)
        {
            Image image = pair.Value.GetComponent<Image>();
            if (image != null)
            {
                image.color = pair.Key == selectedCategoryId ? new Color(0.16f, 0.20f, 0.24f, 1f) : PanelSoftColor;
            }
        }

        RefreshEntryList();
    }

    private void RefreshEntryList()
    {
        for (int i = 0; i < spawnedEntryRows.Count; i++)
        {
            Destroy(spawnedEntryRows[i]);
        }
        spawnedEntryRows.Clear();

        List<EncyclopediaEntry> allFiltered = FilteredEntries().Take(maxVisibleEntries).ToList();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(allFiltered.Count / (float)VisibleEntryRows));
        entryPage = Mathf.Clamp(entryPage, 0, pageCount - 1);
        List<EncyclopediaEntry> filtered = allFiltered
            .Skip(entryPage * VisibleEntryRows)
            .Take(VisibleEntryRows)
            .ToList();
        bool selectedStillVisible = selectedEntry != null && filtered.Contains(selectedEntry);
        for (int i = 0; i < filtered.Count; i++)
        {
            EncyclopediaEntry entry = filtered[i];
            Button row = CreateEntryRow(entryListContent, entry);
            PlaceTopRow(row.GetComponent<RectTransform>(), i, 52f, 5f, 10f, 10f);
            spawnedEntryRows.Add(row.gameObject);
        }

        if (!selectedStillVisible && filtered.Count > 0)
        {
            SelectEntry(filtered[0]);
        }

        if (filtered.Count == 0)
        {
            TMP_Text empty = CreateText(entryListContent, "Empty List", "Ничего не найдено", 14, FontStyles.Normal, MutedTextColor);
            PlaceTopRow(empty.rectTransform, 0, 42f, 0f, 10f, 10f);
            empty.alignment = TextAlignmentOptions.Center;
            LayoutElement emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
            emptyLayout.minHeight = 48f;
            emptyLayout.preferredHeight = 48f;
            spawnedEntryRows.Add(empty.gameObject);
        }

        int total = allFiltered.Count;
        counterText.text = entries.Count + " записей в базе, показано " + Mathf.Min(total, VisibleEntryRows) + " из " + total;
        if (pageText != null)
        {
            pageText.text = "Страница " + (entryPage + 1) + " / " + pageCount;
        }
    }

    private IEnumerable<EncyclopediaEntry> FilteredEntries()
    {
        string normalizedSearch = (searchText ?? "").Trim().ToLowerInvariant();
        for (int i = 0; i < entries.Count; i++)
        {
            EncyclopediaEntry entry = entries[i];
            if (selectedCategoryId != "all" && entry.CategoryId != selectedCategoryId)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch) && !entry.SearchBlob.Contains(normalizedSearch))
            {
                continue;
            }

            yield return entry;
        }
    }

    private Button CreateEntryRow(RectTransform parent, EncyclopediaEntry entry)
    {
        Button button = CreateButton(parent, "Entry " + entry.Id, entry.Title, entry.IconLabel, GetCategoryColor(entry.CategoryId), 52f);
        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();
        if (texts.Length >= 2)
        {
            texts[1].text = entry.Title + "\n<size=78%><color=#9da8b2>" + entry.Summary + "</color></size>";
        }

        button.onClick.AddListener(() => SelectEntry(entry));
        return button;
    }

    private void SelectEntry(EncyclopediaEntry entry)
    {
        selectedEntry = entry;
        CategoryDefinition category = GetCategory(entry.CategoryId);
        detailTitle.text = entry.Title;
        detailCategory.text = GetCategoryName(entry.CategoryId) + " / " + entry.Id;
        detailBody.text = entry.Body;
        detailIconText.text = entry.IconLabel;
        detailIconPlate.color = category != null ? category.Color : AccentColor;
    }

    private Button CreateButton(RectTransform parent, string name, string label, string icon, Color iconColor, float height)
    {
        RectTransform root = CreatePanel(parent, name, PanelSoftColor);
        root.sizeDelta = new Vector2(0f, height);
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;

        Button button = root.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = PanelSoftColor;
        colors.highlightedColor = new Color(0.13f, 0.17f, 0.21f, 1f);
        colors.pressedColor = new Color(0.20f, 0.24f, 0.28f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Image iconPlate = CreateImage(root, "Icon Plate", iconColor);
        RectTransform iconRect = iconPlate.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        float iconSize = Mathf.Clamp(height - 12f, 22f, 42f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(8f, 0f);

        TMP_Text iconText = CreateText(iconRect, "Icon", icon, Mathf.RoundToInt(Mathf.Clamp(height * 0.28f, 10f, 13f)), FontStyles.Bold, new Color(0.03f, 0.04f, 0.05f, 1f));
        Stretch(iconText.rectTransform);
        iconText.alignment = TextAlignmentOptions.Center;

        TMP_Text text = CreateText(root, "Label", label, Mathf.RoundToInt(Mathf.Clamp(height * 0.28f, 11f, 15f)), FontStyles.Bold, TextColor);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(18f + iconSize, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;

        return button;
    }

    private static void PlaceTopRow(RectTransform rect, int index, float height, float spacing, float topPadding, float horizontalPadding)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-horizontalPadding * 2f, height);
        rect.anchoredPosition = new Vector2(0f, -topPadding - index * (height + spacing));
    }

    private static void SetLeftFixedPanel(RectTransform rect, float left, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);
        rect.anchoredPosition = new Vector2(left, 0f);
    }

    private RectTransform CreateVerticalScroll(RectTransform parent, string name, float padding, float spacing)
    {
        RectTransform scrollRoot = CreateRect(name, parent);
        Stretch(scrollRoot);
        scrollRoot.offsetMin = new Vector2(8f, 8f);
        scrollRoot.offsetMax = new Vector2(-8f, -8f);

        Image viewportImage = CreateImage(scrollRoot, "Viewport", new Color(0f, 0f, 0f, 0f));
        RectTransform viewport = viewportImage.rectTransform;
        Stretch(viewport);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(Mathf.RoundToInt(padding), Mathf.RoundToInt(padding), Mathf.RoundToInt(padding), Mathf.RoundToInt(padding));
        layout.spacing = spacing;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 38f;

        return content;
    }

    private TMP_InputField CreateInput(RectTransform parent, string name, string placeholder)
    {
        RectTransform root = CreatePanel(parent, name, PanelSoftColor);
        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();

        TMP_Text text = CreateText(root, "Text", "", 18, FontStyles.Normal, TextColor);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(16f, 4f);
        text.rectTransform.offsetMax = new Vector2(-16f, -4f);
        text.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text hint = CreateText(root, "Placeholder", placeholder, 18, FontStyles.Italic, MutedTextColor);
        Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(16f, 4f);
        hint.rectTransform.offsetMax = new Vector2(-16f, -4f);
        hint.alignment = TextAlignmentOptions.MidlineLeft;

        input.textComponent = text;
        input.placeholder = hint;
        input.caretColor = AccentColor;
        input.selectionColor = new Color(0.95f, 0.66f, 0.28f, 0.28f);
        return input;
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    private Image CreateImage(RectTransform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        Stretch(image.rectTransform);
        return image;
    }

    private TMP_Text CreateText(Transform parent, string name, string value, int size, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private CategoryDefinition GetCategory(string categoryId)
    {
        for (int i = 0; i < categories.Count; i++)
        {
            if (categories[i].Id == categoryId) return categories[i];
        }
        return null;
    }

    private string GetCategoryName(string categoryId)
    {
        CategoryDefinition category = GetCategory(categoryId);
        return category != null ? category.Name : categoryId;
    }

    private Color GetCategoryColor(string categoryId)
    {
        CategoryDefinition category = GetCategory(categoryId);
        return category != null ? category.Color : AccentColor;
    }

    private static RoleDefinition[] GetRoleDefinitions()
    {
        return new[]
        {
            new RoleDefinition("courier", "Курьер", "КР", "Быстрая доставка малых важных грузов.", "Курьерская линия нужна, чтобы маленькие срочные задачи не исчезали даже в большой игре.", "route_speed", "потеря срочности", "paper, wax, sealed cases", new[] { "Искра", "Ласточка", "Стриж", "Почтовый клипер", "Дальняя весть", "Архивный гонец", "Летучая канцелярия" }, new[] { "Маршрутные записки", "Срочные поручения", "Опечатанные грузы", "Межрегиональная почта", "Высотная связь", "Архивная перевозка", "Канцелярия рейда" }),
            new RoleDefinition("freight", "Грузовик", "ГР", "Массовая доставка ресурсов.", "Грузовики - кровь мира. Они делают островную сеть устойчивой.", "cargo_capacity", "топливо и износ", "crates, beams, containers, sorters", new[] { "Ручеёк", "МАУ-1 Веретено", "МАУ-2 Носильщик", "МАУ-3 Дальник", "Тяжёлый караван", "Грузовая матка", "Склад-город" }, new[] { "Малый трюм", "Стандартные ящики", "Весовая укладка", "Дальние караваны", "Контейнерная палуба", "Флотский склад", "Городская сортировка" }),
            new RoleDefinition("mining", "Рудодобыча", "РУ", "Добыча глыб и доставка руды.", "Добыча вводит сырьё, дробную переработку и риск работы рядом с глыбами.", "mining_rate", "аварии захвата", "tools, teeth, crushers, heavy cranes", new[] { "Камнеклюв", "Камнеклёв", "Скальный жнец", "Рудный буксир", "Глыбодёр", "Горная база", "Летучий рудный город" }, new[] { "Рудный захват", "Малые добывающие суда", "Средние рудные операции", "Буксировка глыб", "Тяжёлая разработка", "Capital-горная база", "Летучий рудный город" }),
            new RoleDefinition("gas", "Газосбор", "ГЗ", "Конденсация облаков и сбор газов.", "Газовая линия открывает облака как ресурс и связывает мир с химией.", "gas_harvest_rate", "утечки и порча партии", "valves, membranes, filters, inert chambers", new[] { "Баночка", "Облачная банка", "Сифон", "Газовый траулер", "Конденсаторная баржа", "Облачная фабрика", "Атмосферный комбинат" }, new[] { "Пробные конденсаторы", "Малые газовые банки", "Мембранная фракция", "Опасные облака", "Массовая конденсация", "Облачная фабрика", "Атмосферный комбинат" }),
            new RoleDefinition("leviathan", "Левиафаны", "ЛВ", "Наблюдение, охота и биоматериалы.", "Линия начинается с наблюдения и только потом становится охотой.", "observation_gain", "травмы и повреждения", "bait, harpoons, cold storage, bio labs", new[] { "Наблюдатель", "Крючник", "Линехват", "Гарпунный корвет", "Китобойная артель", "Биологическая база", "Левиафановый институт" }, new[] { "Полевые наблюдения", "Малые гарпуны", "Натяжение линей", "Поведенческие карты", "Разделочная артель", "Биологическая база", "Институт живых материалов" }),
            new RoleDefinition("repair", "Ремонт", "РМ", "Обслуживание кораблей в рейде.", "Ремонтные корабли превращают большие рейды из азартной вылазки в систему.", "repair_rate", "расход деталей", "tools, spare parts, diagnostics, repair automatons", new[] { "Заплатка", "Мастерок", "Заплатник", "Ремонтный тендер", "Полевая верфь", "Восстановительная станция", "Летучий ремонтный город" }, new[] { "Аварийная заплата", "Полевой инструмент", "Маршрутный ремонт", "Тендерные бригады", "Полевая верфь", "Capital-восстановление", "Ремонтный город" }),
            new RoleDefinition("escort", "Эскорт", "ЭС", "Защита рейсов и флота.", "Эскорт нужен для защиты долгих процессов, а не для превращения игры в чистый бой.", "threat_suppression", "боекомплект", "armor, turrets, fire-control, tactical matrices", new[] { "Сторожок", "Клинок", "Щитник", "Грозовой корвет", "Конвойный бастион", "Охранная платформа", "Крепость сопровождения" }, new[] { "Сторожевые посты", "Конвойные курсы", "Щитовое сопровождение", "Дальняя охрана", "Конвойный бастион", "Охранная платформа", "Крепость сопровождения" }),
            new RoleDefinition("scout", "Разведка", "РЗ", "Открытие карты и сбор знаний.", "Разведка превращает полёт в информацию, маршруты и новые цели.", "survey_speed", "потеря времени", "paper, optics, barometers, sensor cores", new[] { "Глазок", "Ветерок", "Дальний глаз", "Картограф", "Штормовой визир", "Небесная обсерватория", "Архив неба" }, new[] { "Ручная съёмка", "Маршрутные приметы", "Оптическая мачта", "Картографический стол", "Штормовой визир", "Небесная обсерватория", "Архив неба" }),
            new RoleDefinition("passenger", "Пассажиры и вахта", "ВА", "Перевозка экипажей и рабочих.", "Люди становятся таким же важным потоком, как еда, вода и металл.", "crew_transfer_rate", "усталость экипажа", "cabins, water, medicine, comfort salons", new[] { "Лавка", "Вахтовик", "Смена", "Рабочий перевозчик", "Жилой клипер", "Вахтовая база", "Летучая слобода" }, new[] { "Пассажирская лавка", "Вахтовый отсек", "Смена экипажа", "Рабочие перевозки", "Жилой клипер", "Вахтовая база", "Летучая слобода" }),
            new RoleDefinition("industrial", "Промышленный", "ЦХ", "Производство на борту.", "Промышленный корабль постепенно превращается в подвижный остров.", "onboard_production", "аварии производства", "tools, machine tools, boilers, factory controllers", new[] { "Верстак", "Малая мастерская", "Цеховик", "Фабричная палуба", "Промышленная баржа", "Фабричная станция", "Завод-город" }, new[] { "Бортовой верстак", "Малая мастерская", "Механический цех", "Фабричная палуба", "Промышленная баржа", "Фабричная станция", "Завод-город" }),
            new RoleDefinition("command", "Командный", "КМ", "Управление флотом и отчёты.", "Командные корабли позволяют игроку управлять системой, а не каждым катером вручную.", "command_slots", "перегруз диспетчеризации", "signal lamps, relays, command cores", new[] { "Сигнальщик", "Диспетчер", "Узел", "Дальний журнал", "Экспедиционный штаб", "Командная станция", "Горизонт" }, new[] { "Сигнальные порядки", "Островская диспетчерская", "Групповое командование", "Дальний журнал", "Экспедиционный штаб", "Capital-командование", "Городская власть рейда" }),
            new RoleDefinition("ice", "Лёд и сублиматы", "ЛД", "Высотные айсберги и редкие вещества.", "Ледовая линия открывает верхний мир, холод, гарпуны и сублиматы.", "ice_capture_rate", "обледенение", "hooks, heated holds, insulation, cold labs", new[] { "Ледомер", "Ледокрючник", "Холодный траулер", "Айсберговая баржа", "Сублиматный комбайн", "Ледяная база", "Высотный ледовый город" }, new[] { "Ледовые пробы", "Малый ледовый крюк", "Термотрюм", "Айсберговый гарпун", "Сублиматный комбайн", "Ледяная база", "Высотный ледовый город" }),
            new RoleDefinition("archaeology", "Руины и археология", "АР", "Предтечи, взлом и редкие рецепты.", "Руины дают знания, ограниченные рецепты и детали автоматонной эпохи.", "hacking_power", "активация защиты", "probes, access keys, cipher tools, precursor interfaces", new[] { "Щуп", "Взломщик", "Археолог", "Руинный корвет", "Предтечевый траулер", "Архивная станция", "Город-раскоп" }, new[] { "Архивный щуп", "Замковые цилиндры", "Дешифровальные столы", "Руинный протокол", "Предтечевый трал", "Архивная станция", "Город-раскоп" }),
            new RoleDefinition("supply", "Флотское снабжение", "СН", "Топливо, еда, ремонтные запасы.", "Снабженцы делают дальний рейд возможным без возвращения в столицу.", "fleet_endurance", "порча запасов", "canisters, fuel tanks, ration rooms, distribution grids", new[] { "Канистра", "Сухпай", "Топливщик", "Снабженец", "Караванная база", "Матка снабжения", "Тыловой город" }, new[] { "Канистры и сухпаи", "Малый снабженец", "Топливные рейсы", "Флотские нормы", "Караванная база", "Матка снабжения", "Тыловой город" }),
            new RoleDefinition("tug", "Буксир и док", "БК", "Стыковка, буксировка, монтаж.", "Буксиры нужны, когда объекты мира становятся слишком тяжёлыми для ручной возни.", "tow_power", "аварии стыковки", "hooks, winches, pressure blocks, mass calibrators", new[] { "Крючок", "Причальщик", "Доковый буксир", "Тяжёлый буксир", "Монтажный титан", "Верфевой тягач", "Двигатель города" }, new[] { "Причальные крюки", "Малые лебёдки", "Доковый буксир", "Тяжёлая буксировка", "Монтажные тяги", "Верфевой тягач", "Двигатель города" })
        };
    }

    private static string BuildRankList(string[] ships)
    {
        List<string> lines = new List<string>();
        for (int i = 0; i < ships.Length; i++)
        {
            lines.Add("R" + (i + 1) + ": " + ships[i]);
        }
        return string.Join("\n", lines);
    }

    private static string GetRankFunction(int rank)
    {
        switch (rank)
        {
            case 1: return "учебное ручное действие, короткий рейс, почти без инфраструктуры";
            case 2: return "первая специализация и малый док";
            case 3: return "рабочая единица островной сети, экипаж и подготовленный док";
            case 4: return "дальние маршруты, газ/химия, обслуживание команды";
            case 5: return "тяжёлая флотская платформа, требующая автоматизации";
            case 6: return "capital-корабль, вокруг которого строится рейд";
            case 7: return "корабль-город, подвижная инфраструктура цивилизации";
            default: return "неизвестный ранг";
        }
    }

    private static string GetRankBottleneck(int rank)
    {
        switch (rank)
        {
            case 1: return "базовые товары, дерево, ткань";
            case 2: return "металл, механизмы, клавдий";
            case 3: return "экипаж, уголь/пар, подготовленный док";
            case 4: return "газы, химия, дальние маршруты";
            case 5: return "автоматонные компоненты, риги, флотское снабжение";
            case 6: return "сублиматы, предтечевые интерфейсы, capital-доки";
            case 7: return "городская логистика, население, автономия, редкие ядра";
            default: return "неизвестный bottleneck";
        }
    }

    private static string BuildRecipeBody(IndustryRecipeConfig recipe, WorldConfigDatabase config)
    {
        return "id: " + recipe.id +
            "\nТип: " + recipe.kind +
            "\nДлительность: " + FormatFloat(recipe.durationSeconds) + " сек." +
            "\nВход: " + FormatAmounts(recipe.inputs, config) +
            "\nВыход: " + FormatAmounts(recipe.outputs, config) +
            "\nТопливо: " + (string.IsNullOrWhiteSpace(recipe.fuelItemId) ? "-" : config.GetItemNameRu(recipe.fuelItemId)) +
            "\nЭнергия: " + FormatFloat(recipe.energyCostKwh) + " кВт⋅ч" +
            "\nКатализаторы: " + FormatCatalysts(recipe.catalysts, config) +
            "\nЭтапов сборки: " + recipe.assemblySteps.Count;
    }

    private static string FormatAmounts(List<ProductionItemAmountConfig> amounts, WorldConfigDatabase config)
    {
        if (amounts == null || amounts.Count == 0) return "-";
        List<string> parts = new List<string>();
        for (int i = 0; i < amounts.Count; i++)
        {
            parts.Add(config.GetItemNameRu(amounts[i].itemId) + " x" + FormatFloat(amounts[i].amount));
        }
        return string.Join(", ", parts);
    }

    private static string FormatCatalysts(List<ProductionCatalystConfig> catalysts, WorldConfigDatabase config)
    {
        if (catalysts == null || catalysts.Count == 0) return "-";
        List<string> parts = new List<string>();
        for (int i = 0; i < catalysts.Count; i++)
        {
            parts.Add(config.GetItemNameRu(catalysts[i].itemId) + " x" + catalysts[i].amount + " +" + FormatFloat(catalysts[i].successBonus));
        }
        return string.Join(", ", parts);
    }

    private static string FormatTechCosts(List<TechnologyCostConfig> costs, WorldConfigDatabase config)
    {
        if (costs == null || costs.Count == 0) return "-";
        List<string> parts = new List<string>();
        for (int i = 0; i < costs.Count; i++)
        {
            parts.Add(config.GetItemNameRu(costs[i].itemId) + " x" + costs[i].amount);
        }
        return string.Join(", ", parts);
    }

    private static string FormatGasComposition(GasCloudTypeConfig type, WorldConfigDatabase config)
    {
        if (type.composition == null || type.composition.Count == 0) return "-";
        List<string> parts = new List<string>();
        for (int i = 0; i < type.composition.Count; i++)
        {
            parts.Add(config.GetItemNameRu(type.composition[i].itemId) + " " + FormatFloat(type.composition[i].share * 100f) + "%");
        }
        return string.Join(", ", parts);
    }

    private static string FormatOreComposition(OreTypeConfig ore, WorldConfigDatabase config)
    {
        if (ore.composition == null || ore.composition.Count == 0) return "-";
        List<string> parts = new List<string>();
        for (int i = 0; i < ore.composition.Count; i++)
        {
            parts.Add(config.GetItemNameRu(ore.composition[i].mineralItemId) + " " + FormatFloat(ore.composition[i].share * 100f) + "%");
        }
        return string.Join(", ", parts);
    }

    private static string JoinOrDash(List<string> values)
    {
        if (values == null || values.Count == 0) return "-";
        string joined = string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(joined) ? "-" : joined;
    }

    private static string JoinOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string DisplayName(string localNameRu, string fallback)
    {
        return string.IsNullOrWhiteSpace(localNameRu) ? fallback : localNameRu;
    }

    private static string FormatVector(Vector3 value)
    {
        return "(" + FormatFloat(value.x) + ", " + FormatFloat(value.y) + ", " + FormatFloat(value.z) + ")";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.##");
    }

    private class EncyclopediaEntry
    {
        public string Id;
        public string CategoryId;
        public string Title;
        public string IconLabel;
        public string Summary;
        public string Body;
        public string SearchBlob;
    }

    private class CategoryDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string IconLabel;
        public readonly Color Color;

        public CategoryDefinition(string id, string name, string iconLabel, Color color)
        {
            Id = id;
            Name = name;
            IconLabel = iconLabel;
            Color = color;
        }
    }

    private class RoleDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Icon;
        public readonly string Summary;
        public readonly string Description;
        public readonly string MainStat;
        public readonly string Risk;
        public readonly string RecipeSurcharge;
        public readonly string[] Ships;
        public readonly string[] TechNames;

        public RoleDefinition(string id, string name, string icon, string summary, string description, string mainStat, string risk, string recipeSurcharge, string[] ships, string[] techNames)
        {
            Id = id;
            Name = name;
            Icon = icon;
            Summary = summary;
            Description = description;
            MainStat = mainStat;
            Risk = risk;
            RecipeSurcharge = recipeSurcharge;
            Ships = ships;
            TechNames = techNames;
        }
    }
}
