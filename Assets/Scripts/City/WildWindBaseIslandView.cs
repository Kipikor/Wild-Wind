using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
public sealed class WildWindBaseIslandView : MonoBehaviour
{
    private const string ObjectName = "Wild Wind Base Island";
    private const string CityRootName = "Runtime Iso Grid City";
    private const string WindowCanvasName = "Base Island Empty Window Canvas";
    private const string EventSystemName = "Base Island EventSystem";
    private const string BuildingPrefabResourceRoot = "BaseIsland/Buildings/";
    private const string BuildingCatalogResourcePath = "BaseIsland/City_building";
    private const string CourierServiceBuildingId = "courier_service";
    private const string FreightItemId = "freight";
    private const int LegacySeedOriginOffsetXCells = 0;
    private const int LegacySeedOriginYCells = 0;
    private const string CoreIslandRegionId = "core_open_area";
    private const string FrontIslandLipRegionId = "front_open_lip";
    private const int CoreIslandOriginX = 0;
    private const int CoreIslandOriginY = 2;
    private const int CoreIslandWidth = 20;
    private const int CoreIslandHeight = 18;
    private const int FrontIslandLipOriginX = 0;
    private const int FrontIslandLipOriginY = 0;
    private const int FrontIslandLipWidth = 20;
    private const int FrontIslandLipHeight = 2;
    private const int ExpectedInitialOpenIslandCellCount =
        CoreIslandWidth * CoreIslandHeight + FrontIslandLipWidth * FrontIslandLipHeight;
    private const int ExternalDockContactWidthCells = 5;
    private const int ExternalDockProtrusionLengthCells = 8;
    private const int ExpectedExternalDockSlotCount = 8;
    private const int WindowSortingOrder = 875;
    private const float CollectBubbleHeight = 126f;
    private const float CollectBubbleDataRefreshSeconds = 0.25f;
    private const float ExpansionRegionRefreshSeconds = 0.25f;
    private const float ProcessingWindowRefreshSeconds = 0.2f;
    private const float ExpansionWindowRefreshSeconds = 0.2f;
    private const float CourierWindowRefreshSeconds = 0.2f;

    private static BaseBuildingDefinition[] cachedBuildingDefinitions;
    private static Sprite cachedCollectBubbleSprite;
    private static readonly BaseBuildingDefinition[] FallbackBuildingDefinitions =
    {
        new BaseBuildingDefinition("anchor_house", "Дом якоря", "BaseBuilding_AnchorHouse", 4, 4, 3, 1, 1, 2, 2, new Color(0.64f, 0.70f, 0.78f, 1f), "hq", "core"),
        new BaseBuildingDefinition("refinery", "Рефайнери", "BaseBuilding_Refinery", 3, 4, 5, 2, 1, 7, 2, new Color(0.62f, 0.46f, 0.33f, 1f), "refinery", "processing"),
        new BaseBuildingDefinition("gas_separator", "Газоразделитель", "BaseBuilding_GasSeparator", 4, 2, 4, 2, 1, 12, 2, new Color(0.50f, 0.64f, 0.68f, 1f), "separator", "processing"),
        new BaseBuildingDefinition("workshop", "Мастерская", "BaseBuilding_Workshop", 3, 3, 4, 5, 1, 12, 7, new Color(0.66f, 0.54f, 0.38f, 1f), "workshop", "production"),
        new BaseBuildingDefinition("scrapyard", "Разборочный двор", "BaseBuilding_Scrapyard", 3, 4, 3, 2, 1, 7, 12, new Color(0.50f, 0.50f, 0.46f, 1f), "scrapyard", "processing"),
        new BaseBuildingDefinition("butchery", "Бойня левиафанов", "", 3, 4, 1, 2, 1, 11, 12, new Color(0.55f, 0.42f, 0.45f, 1f), "butchery", "processing"),
        new BaseBuildingDefinition("laboratory", "Лаборатория", "BaseBuilding_Laboratory", 3, 3, 2, 2, 1, 4, 9, new Color(0.54f, 0.68f, 0.70f, 1f), "laboratory", "processing"),
        new BaseBuildingDefinition("archive", "Архивы", "BaseBuilding_Archive", 4, 4, 1, 2, 1, 0, 13, new Color(0.44f, 0.50f, 0.64f, 1f), "archive", "knowledge"),
        new BaseBuildingDefinition("trader_pavilion", "Торговый павильон", "", 3, 3, 1, 1, 1, 14, 12, new Color(0.76f, 0.60f, 0.33f, 1f), "trader", "service"),
        new BaseBuildingDefinition(CourierServiceBuildingId, "Курьерская служба", "", 4, 3, 1, 1, 1, 16, 4, new Color(0.18f, 0.56f, 0.58f, 1f), "courier", "service")
    };
    private static readonly string[] BuildingActionLabels =
    {
        "Сведения",
        "Работы",
        "Улучшить"
    };

    private static readonly string[] CascadeCatalogRubrics =
    {
        "Все",
        "Корабли",
        "Модули",
        "Боеприпасы",
        "Здания"
    };

    private static readonly BaseIslandExpansionDefinition[] ExpansionDefinitions =
    {
        new BaseIslandExpansionDefinition("front_east_cap", 20, 0, 10, 10, 3, 2000, 25f),
        new BaseIslandExpansionDefinition("front_east_slope", 30, 0, 10, 10, 3, 2500, 35f),
        new BaseIslandExpansionDefinition("east_mid", 20, 10, 10, 10, 3, 4000, 60f),
        new BaseIslandExpansionDefinition("back_east", 30, 10, 10, 10, 5, 9000, 145f),
        new BaseIslandExpansionDefinition("west_mid", 0, 20, 10, 10, 3, 3000, 45f),
        new BaseIslandExpansionDefinition("back_west", 10, 20, 10, 10, 5, 7500, 120f),
        new BaseIslandExpansionDefinition("back_center", 20, 20, 10, 10, 5, 6500, 100f),
        new BaseIslandExpansionDefinition("north_low_arc", 30, 20, 10, 10, 8, 12000, 190f),
        new BaseIslandExpansionDefinition("north_mid_arc", 0, 30, 20, 10, 8, 14500, 230f),
        new BaseIslandExpansionDefinition("north_tip", 20, 30, 20, 10, 10, 17000, 270f)
    };

    private readonly Dictionary<Renderer, bool> suppressedRendererStates = new Dictionary<Renderer, bool>();

    private MetaGameState meta;
    private WildWindGameplaySession gameplaySession;
    private WildWindGameplayHud gameplayHud;
    private WildWindSessionCameraController sessionCameraController;
    private IsoGridCityPrototype city;
    private Camera sceneCamera;
    private Canvas windowCanvas;
    private RectTransform overlayRoot;
    private Button buildingSelectionBackdropButton;
    private RectTransform buildingInfoPanel;
    private Text buildingInfoNameText;
    private Text buildingInfoLevelText;
    private RectTransform buildingActionPanel;
    private Button[] buildingActionButtons;
    private Text[] buildingActionButtonTexts;
    private Transform processingCollectBubbleRoot;
    private readonly Dictionary<string, ProcessingCollectBubbleView> processingCollectBubbles = new Dictionary<string, ProcessingCollectBubbleView>();
    private readonly HashSet<string> visibleProcessingCollectBubbleKeys = new HashSet<string>();
    private RectTransform buildingCatalogPanel;
    private RectTransform buildingCatalogContent;
    private Text buildingCatalogStatusText;
    private readonly Dictionary<string, Button> buildingCatalogButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Text> buildingCatalogButtonTexts = new Dictionary<string, Text>();
    private string buildingCatalogStatus = "";
    private RectTransform windowPanel;
    private Button windowFadeButton;
    private Text windowTitleText;
    private Text windowBodyText;
    private Button windowExitButton;
    private Text windowExitButtonText;
    private Button windowPrimaryButton;
    private Text windowPrimaryButtonText;
    private RectTransform processingPanel;
    private Text processingStatsText;
    private Text processingStorageText;
    private Text processingBunkerText;
    private Text processingOutputsText;
    private Text processingStatusText;
    private Button processingLoadAllButton;
    private Button processingClearButton;
    private Button processingCollectButton;
    private Button[] processingInputButtons;
    private Text[] processingInputButtonTexts;
    private RectTransform cascadeCatalogPanel;
    private Text cascadeCatalogHeaderText;
    private Text cascadeCatalogStorageToggleText;
    private Text cascadeCatalogRecipeListTitleText;
    private Text cascadeCatalogRecipeTitleText;
    private Text cascadeCatalogRecipeMetaText;
    private Text cascadeCatalogCompositionText;
    private Text cascadeCatalogBottleneckText;
    private Text cascadeCatalogResultText;
    private Text cascadeCatalogStatusText;
    private Button[] cascadeCatalogRubricButtons;
    private Text[] cascadeCatalogRubricButtonTexts;
    private Button[] cascadeCatalogRecipeButtons;
    private Text[] cascadeCatalogRecipeButtonTexts;
    private Button[] cascadeCatalogBottleneckButtons;
    private Text[] cascadeCatalogBottleneckButtonTexts;
    private Button cascadeCatalogStorageToggleButton;
    private Button cascadeCatalogStartButton;
    private Text cascadeCatalogStartButtonText;
    private bool cascadeCatalogWindowOpen;
    private int selectedCascadeRubricIndex;
    private int selectedCascadeRecipeIndex;
    private bool cascadeCatalogAvoidStorage;
    private RectTransform courierPanel;
    private Text courierHeaderText;
    private Text courierListText;
    private Text courierDetailText;
    private Text courierStatusText;
    private Button[] courierOrderButtons;
    private Text[] courierOrderButtonTexts;
    private Button courierSendButton;
    private Text courierSendButtonText;
    private Button courierCancelButton;
    private Text courierCancelButtonText;
    private Button courierCancelAllButton;
    private Text courierCancelAllButtonText;
    private bool courierWindowOpen;
    private int selectedCourierSlotIndex;
    private string courierStatusMessage = "";
    private bool courierWindowDirty = true;
    private float nextCourierWindowRefreshTime;
    private string hoveredBuildingId = "";
    private string forcedHoverBuildingIdForTests = "";
    private string selectedBuildingId = "";
    private string openBuildingId = "";
    private int openActionIndex = -1;
    private BaseProcessingBranch openProcessingBranch;
    private bool processingWindowOpen;
    private string processingStatusMessage = "";
    private string openExpansionRegionId = "";
    private bool expansionWindowOpen;
    private string expansionStatusMessage = "";
    private bool built;
    private bool wasCityVisible;
    private bool defaultBuildingsSeeded;
    private bool hasInitializedCityCameraView;
    private bool hasStoredCameraControllerState;
    private bool storedCameraControllerEnabled;
    private bool processingCollectBubbleDataDirty = true;
    private float nextProcessingCollectBubbleDataRefreshTime;
    private bool sessionWorldRenderersSuppressed;
    private Transform suppressedActiveShipRoot;
    private bool expansionRegionsDirty = true;
    private float nextExpansionRegionRefreshTime;
    private bool processingWindowDirty = true;
    private float nextProcessingWindowRefreshTime;
    private bool expansionWindowDirty = true;
    private float nextExpansionWindowRefreshTime;
    private bool hasStoredCityCameraRenderState;
    private bool storedCityCameraAllowHDR;
    private bool storedCityCameraAllowMSAA;
    private bool storedCityCameraRenderPostProcessing;
    private bool storedCityCameraRequiresDepthTexture;
    private bool storedCityCameraRequiresColorTexture;
    private static Font cachedDefaultFont;

    public bool IsReadyForTests => built
        && city != null
        && city.IsReadyForTests
        && city.BuildingCountForTests >= GetInitialBuildingCountForTests()
        && windowCanvas != null;
    public int BuildingCountForTests => city != null ? city.BuildingCountForTests : 0;
    public bool IsWindowOpenForTests => windowCanvas != null
        && windowPanel != null
        && windowPanel.gameObject.activeSelf
        && (!string.IsNullOrWhiteSpace(openBuildingId) || expansionWindowOpen);
    public bool IsWindowModalForTests => windowCanvas != null
        && overlayRoot != null
        && windowFadeButton != null
        && windowPanel != null
        && windowFadeButton.targetGraphic != null
        && windowFadeButton.transform.parent == overlayRoot
        && windowPanel.transform.parent == overlayRoot
        && windowFadeButton.transform.GetSiblingIndex() < windowPanel.transform.GetSiblingIndex()
        && windowFadeButton.GetComponent<RectTransform>().anchorMin == Vector2.zero
        && windowFadeButton.GetComponent<RectTransform>().anchorMax == Vector2.one;
    public string OpenWindowTitleForTests => windowTitleText != null ? windowTitleText.text : "";
    public string WindowContentForTests => IsCascadeCatalogWindowOpenForTests
        ? CascadeCatalogContentForTests
        : IsCourierWindowOpenForTests
        ? CourierWindowContentForTests
        : IsProcessingWindowOpenForTests
        ? ProcessingWindowContentForTests
        : windowBodyText != null ? windowBodyText.text : "";
    public string WindowExitButtonLabelForTests => windowExitButtonText != null ? windowExitButtonText.text : "";
    public bool IsProcessingWindowOpenForTests => IsWindowOpenForTests
        && processingPanel != null
        && processingPanel.gameObject.activeSelf;
    public bool IsCascadeCatalogWindowOpenForTests => IsWindowOpenForTests
        && cascadeCatalogWindowOpen
        && cascadeCatalogPanel != null
        && cascadeCatalogPanel.gameObject.activeSelf;
    public bool IsCourierWindowOpenForTests => IsWindowOpenForTests
        && courierWindowOpen
        && courierPanel != null
        && courierPanel.gameObject.activeSelf;
    public bool IsExpansionWindowOpenForTests => IsWindowOpenForTests && expansionWindowOpen;
    public int CityGridWidthForTests => city != null ? city.GridWidthForTests : 0;
    public int CityGridHeightForTests => city != null ? city.GridHeightForTests : 0;
    public int CityOpenCellCountForTests => city != null ? city.OpenCellCountForTests : 0;
    public int CityFogCellCountForTests => city != null ? city.FogCellCountForTests : 0;
    public int CityDebrisCellCountForTests => city != null ? city.DebrisCellCountForTests : 0;
    public int CityTileColliderCountForTests => city != null ? city.TileColliderCountForTests : 0;
    public int CityTileBatchRendererCountForTests => city != null ? city.TileBatchRendererCountForTests : 0;
    public int CityCellDataCountForTests => city != null ? city.CellDataCountForTests : 0;
    public int CityRaycastLayerForTests => city != null ? city.CityRaycastLayerForTests : -1;
    public int CityInteractiveColliderLayerMismatchCountForTests => city != null ? city.InteractiveColliderLayerMismatchCountForTests : -1;
    public int ExpansionRegionCountForTests => ExpansionDefinitions.Length;
    public int ExpansionMarkerCountForTests => city != null ? city.ExpansionMarkerCountForTests : 0;
    public int ExternalDockSlotCountForTests => city != null ? city.ExternalDockSlotCountForTests : 0;
    public int ExternalDockPlacedCountForTests => city != null ? city.ExternalDockPlacedCountForTests : 0;
    public int ExpectedExternalDockSlotCountForTests => ExpectedExternalDockSlotCount;
    public string ProcessingWindowContentForTests => string.Join("\n", new[]
    {
        processingStatsText != null ? processingStatsText.text : "",
        processingStorageText != null ? processingStorageText.text : "",
        processingBunkerText != null ? processingBunkerText.text : "",
        processingOutputsText != null ? processingOutputsText.text : "",
        processingStatusText != null ? processingStatusText.text : ""
    });
    public string CascadeCatalogContentForTests => string.Join("\n", new[]
    {
        cascadeCatalogHeaderText != null ? cascadeCatalogHeaderText.text : "",
        cascadeCatalogRecipeListTitleText != null ? cascadeCatalogRecipeListTitleText.text : "",
        cascadeCatalogRecipeTitleText != null ? cascadeCatalogRecipeTitleText.text : "",
        cascadeCatalogRecipeMetaText != null ? cascadeCatalogRecipeMetaText.text : "",
        cascadeCatalogCompositionText != null ? cascadeCatalogCompositionText.text : "",
        cascadeCatalogBottleneckText != null ? cascadeCatalogBottleneckText.text : "",
        cascadeCatalogResultText != null ? cascadeCatalogResultText.text : "",
        cascadeCatalogStatusText != null ? cascadeCatalogStatusText.text : ""
    });
    public string CourierWindowContentForTests => string.Join("\n", new[]
    {
        courierHeaderText != null ? courierHeaderText.text : "",
        courierListText != null ? courierListText.text : "",
        courierDetailText != null ? courierDetailText.text : "",
        courierStatusText != null ? courierStatusText.text : ""
    });
    public bool IsBuildingHoverLabelVisibleForTests => buildingInfoPanel != null && buildingInfoPanel.gameObject.activeSelf;
    public bool IsBuildingActionMenuOpenForTests => !string.IsNullOrWhiteSpace(selectedBuildingId)
        && buildingActionPanel != null
        && buildingActionPanel.gameObject.activeSelf
        && buildingSelectionBackdropButton != null
        && buildingSelectionBackdropButton.gameObject.activeSelf
        && !IsWindowOpenForTests;
    public bool IsBuildingCatalogVisibleForTests => buildingCatalogPanel != null && buildingCatalogPanel.gameObject.activeSelf;
    public bool IsBuildingCatalogReadyForTests => buildingCatalogPanel != null
        && buildingCatalogContent != null
        && buildingCatalogStatusText != null
        && buildingCatalogButtons.Count == BuildingDefinitions.Length;
    public int BuildingDefinitionCountForTests => BuildingDefinitions.Length;
    public string BuildingCatalogStatusForTests => buildingCatalogStatusText != null ? buildingCatalogStatusText.text : "";
    public int VisibleBuildingActionButtonCountForTests
    {
        get
        {
            if (buildingActionButtons == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < buildingActionButtons.Length; i++)
            {
                if (buildingActionButtons[i] != null && buildingActionButtons[i].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public string BuildingInfoNameForTests => buildingInfoNameText != null ? buildingInfoNameText.text : "";
    public string BuildingInfoLevelForTests => buildingInfoLevelText != null ? buildingInfoLevelText.text : "";
    public int ProcessingCollectBubbleCountForTests
    {
        get
        {
            RefreshBuildingOverlay(true);
            int count = 0;
            foreach (ProcessingCollectBubbleView bubble in processingCollectBubbles.Values)
            {
                if (bubble != null && bubble.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public bool UsesIsoCityCameraForTests => city != null && city.HasCityCameraForTests;
    public bool IsCityVisibleForTests => city != null && city.gameObject.activeSelf;
    public bool PrototypeToolbarHiddenForTests => city != null && !city.IsPrototypeToolbarVisibleForTests;
    public int SuppressedSessionRendererCountForTests => suppressedRendererStates.Count;
    public int SuppressedActiveShipRendererCountForTests => CountSuppressedActiveShipRenderers();
    public bool IsCityCameraLeanRenderStateActiveForTests => IsCityCameraLeanRenderStateActive();
    public bool IsCityCameraUsingLeanRendererForTests => IsCityCameraUsingLeanRenderer();
    public string CityCameraRendererNameForTests => GetCameraRendererName(sceneCamera != null ? sceneCamera : Camera.main);

    public bool HasBuildingDefinitionForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out _);
    }

    public bool TryGetBuildingDefinitionFootprintForTests(string buildingId, out int width, out int height)
    {
        if (TryGetDefinition(buildingId, out BaseBuildingDefinition definition))
        {
            width = definition.width;
            height = definition.height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    public int GetBuildingDefinitionMaxCountForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition) ? GetBuildingMaxCount(definition) : 0;
    }

    public int GetPlacedBuildingCountForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            ? CountPlacedBuildings(definition)
            : 0;
    }

    public int GetExactBuildingKeyCountForTests(string buildingId)
    {
        if (city == null)
        {
            return 0;
        }

        if (TryGetDefinition(buildingId, out BaseBuildingDefinition definition) && IsDockDefinition(definition))
        {
            return city.CountExactExternalDockKeyForTests(buildingId);
        }

        return city.CountExactBuildingKeyForTests(buildingId);
    }

    public bool IsBuildingUsingProceduralVisualForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            && !string.IsNullOrWhiteSpace(definition.visualKind)
            && string.IsNullOrWhiteSpace(definition.prefabName);
    }

    public bool BuildingDefinitionHasPrefabForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            && !string.IsNullOrWhiteSpace(definition.prefabName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallBaseIslandBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForCurrentSessionScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForCurrentSessionScene();
    }

    public static WildWindBaseIslandView EnsureForCurrentSessionScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return null;
        }

        WildWindBaseIslandView existing = FindFirstObjectByType<WildWindBaseIslandView>();
        if (existing != null)
        {
            existing.EnsureBuilt();
            return existing;
        }

        GameObject viewObject = new GameObject(ObjectName);
        WildWindBaseIslandView view = viewObject.AddComponent<WildWindBaseIslandView>();
        view.EnsureBuilt();
        return view;
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnDisable()
    {
        RestoreSessionWorldRenderers();
        RestoreSessionCameraController();
    }

    private void OnDestroy()
    {
        if (city != null)
        {
            city.BuildingClicked -= HandleCityBuildingClicked;
            city.ExpansionRegionClicked -= HandleExpansionRegionClicked;
            city.ExternalDockClicked -= HandleCityExternalDockClicked;
        }

        RestoreSessionWorldRenderers();
        RestoreSessionCameraController();
    }

    private void Update()
    {
        if (!built)
        {
            EnsureBuilt();
            if (!built)
            {
                return;
            }
        }
        else
        {
            ResolveReferences();
        }

        RefreshCityMode();
        RefreshExpansionRegionsIfDue();
        RefreshBuildingOverlay();
        HandleProcessingCollectBubblePointerInput();
        RefreshProcessingWindowIfDue();
        RefreshExpansionWindowIfDue();
        RefreshCourierWindowIfDue();
    }

    public bool HasBuildingForTests(string buildingId)
    {
        if (city == null)
        {
            return false;
        }

        return city.HasBuildingKeyForTests(buildingId) || city.HasExternalDockKeyForTests(buildingId);
    }

    public bool BuildingPrefabExistsForTests(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            && Resources.Load<GameObject>(definition.PrefabResourcePath) != null;
    }

    public bool BuildingUsedPrefabForTests(string buildingId)
    {
        return city != null && city.BuildingUsesPrefabForTests(buildingId);
    }

    public bool BuildingHasColliderForTests(string buildingId)
    {
        return city != null && city.BuildingHasColliderForTests(buildingId);
    }

    public bool BuildingHasVisualForTests(string buildingId)
    {
        return city != null && city.BuildingHasRendererForTests(buildingId);
    }

    public bool BuildingPrefabHasPhysicalMaterialsForTests(string buildingId)
    {
        if (!TryGetDefinition(buildingId, out BaseBuildingDefinition definition))
        {
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>(definition.PrefabResourcePath);
        if (prefab == null)
        {
            return false;
        }

        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
            {
                return false;
            }

            for (int materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; materialIndex++)
            {
                Material material = renderer.sharedMaterials[materialIndex];
                if (!IsUsableAuthoredMaterial(material))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool TryOpenBuildingForTests(string buildingId)
    {
        return TrySelectBuilding(buildingId);
    }

    public bool ShowBuildingHoverForTests(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId) || city == null || !city.HasBuildingKeyForTests(buildingId))
        {
            return false;
        }

        forcedHoverBuildingIdForTests = buildingId;
        hoveredBuildingId = buildingId;
        RefreshBuildingOverlay();
        bool visible = IsBuildingHoverLabelVisibleForTests;
        forcedHoverBuildingIdForTests = "";
        return visible;
    }

    public bool SelectBuildingForTests(string buildingId)
    {
        return TrySelectBuilding(buildingId);
    }

    public bool DismissBuildingActionMenuForTests()
    {
        ClearBuildingFocus();
        RefreshBuildingOverlay();
        return !IsBuildingActionMenuOpenForTests;
    }

    public bool OpenBuildingActionForTests(int actionIndex)
    {
        return OpenSelectedBuildingAction(actionIndex);
    }

    public bool OpenProcessingWindowForTests(string buildingId)
    {
        return TrySelectBuilding(buildingId) && OpenSelectedBuildingAction(1) && IsProcessingWindowOpenForTests;
    }

    public bool OpenCascadeCatalogWindowForTests(string buildingId)
    {
        return TrySelectBuilding(buildingId)
            && OpenSelectedBuildingAction(1)
            && IsCascadeCatalogWindowOpenForTests;
    }

    public bool OpenCourierServiceWindowForTests()
    {
        return TrySelectBuilding(CourierServiceBuildingId)
            && OpenSelectedBuildingAction(1)
            && IsCourierWindowOpenForTests;
    }

    public bool SelectCourierOrderForTests(int slotIndex)
    {
        if (!IsCourierWindowOpenForTests)
        {
            return false;
        }

        selectedCourierSlotIndex = Mathf.Clamp(slotIndex, 0, MetaGameState.CourierOrderSlotCount - 1);
        courierWindowDirty = true;
        RefreshCourierWindow();
        return true;
    }

    public bool SendSelectedCourierOrderForTests()
    {
        return HandleCourierSend();
    }

    public bool CancelSelectedCourierOrderForTests()
    {
        return HandleCourierCancel();
    }

    public bool CancelAllCourierOrdersForTests()
    {
        return HandleCourierCancelAll();
    }

    public bool LoadAllProcessingInputsForTests()
    {
        return HandleProcessingLoadAll();
    }

    public bool CollectProcessingOutputsForTests()
    {
        return HandleProcessingCollect();
    }

    public bool IsProcessingCollectBubbleVisibleForTests(string buildingId)
    {
        RefreshBuildingOverlay(true);
        return processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView bubble)
            && bubble != null
            && bubble.IsVisible;
    }

    public string ProcessingCollectBubbleItemIdForTests(string buildingId)
    {
        RefreshBuildingOverlay(true);
        return processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView bubble) && bubble != null
            ? bubble.itemId
            : "";
    }

    public int ProcessingCollectBubbleAmountForTests(string buildingId)
    {
        RefreshBuildingOverlay(true);
        return processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView bubble) && bubble != null
            ? bubble.totalReadyAmount
            : 0;
    }

    public bool ClickProcessingCollectBubbleForTests(string buildingId)
    {
        RefreshBuildingOverlay(true);
        if (!processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView bubble)
            || bubble == null
            || !bubble.IsVisible)
        {
            return false;
        }

        return HandleProcessingCollectBubble(buildingId);
    }

    public void ToggleBuildingCatalogForRuntime()
    {
        EnsureBuilt();
        EnsureWindow();
        if (buildingCatalogPanel == null)
        {
            return;
        }

        bool visible = !buildingCatalogPanel.gameObject.activeSelf;
        buildingCatalogPanel.gameObject.SetActive(visible);
        if (visible)
        {
            SetBuildingCatalogStatus("Выберите здание.");
            RefreshBuildingCatalogButtons();
        }
    }

    public bool OpenBuildingCatalogForTests()
    {
        EnsureBuilt();
        EnsureWindow();
        if (buildingCatalogPanel == null)
        {
            return false;
        }

        buildingCatalogPanel.gameObject.SetActive(true);
        SetBuildingCatalogStatus("Выберите здание.");
        RefreshBuildingCatalogButtons();
        return IsBuildingCatalogVisibleForTests && IsBuildingCatalogReadyForTests;
    }

    public bool PlaceCatalogBuildingForTests(string buildingId)
    {
        return TryPlaceCatalogBuilding(buildingId);
    }

    public bool MoveExternalDockToFirstFreeSlotForTests(string dockKey)
    {
        return city != null && city.TryMoveExternalDockToFirstFreeSlotForTests(dockKey);
    }

    public bool OpenExternalDockForTests(string dockKey)
    {
        return city != null && city.HasExternalDockKeyForTests(dockKey) && OpenDockScreenFromExternalDock();
    }

    public bool ShowExternalDockHoverForTests(string dockKey)
    {
        return city != null && city.ShowExternalDockHoverForTests(dockKey);
    }

    public void FlyCityCameraHomeForRuntime()
    {
        EnsureBuilt();
        city?.FlyCameraToDefaultViewForRuntime();
    }

    public bool SnapCityCameraHomeForTests()
    {
        EnsureBuilt();
        if (city == null)
        {
            return false;
        }

        city.SnapCameraToCityViewForRuntime();
        return true;
    }

    public bool OffsetCityCameraForTests(float pivotX, float pivotZ, float yawDelta, float pitchDelta, float orthographicSizeDelta)
    {
        EnsureBuilt();
        return city != null && city.OffsetCameraForTests(pivotX, pivotZ, yawDelta, pitchDelta, orthographicSizeDelta);
    }

    public string CityCameraSignatureForTests => city != null ? city.CameraSignatureForTests : "";
    public bool IsCityCameraHomeBlendActiveForTests => city != null && city.IsCameraHomeBlendActiveForTests;
    public bool IsCityCameraAtDefaultViewForTests => city != null && city.IsCameraAtDefaultViewForTests;

    public bool OpenExpansionRegionForTests(string regionId)
    {
        EnsureBuilt();
        RefreshExpansionRegions();
        return OpenExpansionWindow(regionId);
    }

    public bool BeginExpansionClearingForTests(string regionId)
    {
        EnsureBuilt();
        RefreshExpansionRegions();
        return TryBeginExpansionClearing(regionId, out expansionStatusMessage);
    }

    public bool ForceCompleteExpansionClearingForTests(string regionId)
    {
        EnsureBuilt();
        if (meta == null || meta.progress == null || !TryGetExpansionDefinition(regionId, out BaseIslandExpansionDefinition definition))
        {
            return false;
        }

        BaseIslandExpansionRegionState state = meta.progress.GetBaseIslandExpansionRegionState(definition.id, true);
        state.status = BaseIslandExpansionRegionStatus.Open;
        state.clearingCompleteUtcTicks = 0;
        RefreshExpansionRegions();
        return CityOpenCellCountForTests >= ExpectedInitialOpenIslandCellCount + definition.width * definition.height;
    }

    public int GetExpansionRegionClearCostForTests(string regionId)
    {
        return TryGetExpansionDefinition(regionId, out BaseIslandExpansionDefinition definition)
            ? definition.clearCostFreight
            : 0;
    }

    public bool MoveBuildingWithRuntimeRefreshForTests(
        string buildingId,
        int originX,
        int originY,
        out int exactCountWhileMoving,
        out int exactCountAfterMove,
        out int totalBefore,
        out int totalAfter)
    {
        exactCountWhileMoving = 0;
        exactCountAfterMove = 0;
        totalBefore = 0;
        totalAfter = 0;

        EnsureBuilt();
        if (city == null)
        {
            return false;
        }

        totalBefore = city.BuildingCountForTests;
        if (!city.BeginMoveBuildingForTests(buildingId))
        {
            totalAfter = city.BuildingCountForTests;
            return false;
        }

        EnsureBuilt();
        exactCountWhileMoving = city.CountExactBuildingKeyForTests(buildingId);
        bool placed = city.PlaceMovingBuildingForTests(originX, originY);
        if (!placed)
        {
            city.CancelMoveForTests();
        }

        EnsureBuilt();
        exactCountAfterMove = city.CountExactBuildingKeyForTests(buildingId);
        totalAfter = city.BuildingCountForTests;
        return placed;
    }

    public bool ExitBuildingActionWindowForTests()
    {
        if (windowExitButton == null || !windowExitButton.gameObject.activeSelf)
        {
            return false;
        }

        windowExitButton.onClick.Invoke();
        return !IsWindowOpenForTests && !IsBuildingActionMenuOpenForTests;
    }

    public bool CloseWindowForTests()
    {
        CloseWindow();
        return !IsWindowOpenForTests;
    }

    public bool CloseWindowByBackdropForTests()
    {
        if (windowFadeButton == null)
        {
            return false;
        }

        CloseWindow();
        return !IsWindowOpenForTests;
    }

    private void EnsureBuilt()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        gameObject.name = ObjectName;
        ResolveReferences();
        EnsureCity();
        EnsureWindow();
        built = city != null && windowCanvas != null;
        RefreshCityMode();
    }

    private void ResolveReferences()
    {
        if (meta == null)
        {
            meta = FindFirstObjectByType<MetaGameState>();
        }

        if (gameplaySession == null)
        {
            gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
        }

        if (gameplayHud == null)
        {
            gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
        }

        if (sessionCameraController == null)
        {
            sessionCameraController = FindFirstObjectByType<WildWindSessionCameraController>();
        }

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }
    }

    private void EnsureCity()
    {
        if (city != null)
        {
            city.SetPrototypeToolbarVisibleForRuntime(false);
            city.BuildingClicked -= HandleCityBuildingClicked;
            city.BuildingClicked += HandleCityBuildingClicked;
            city.ExpansionRegionClicked -= HandleExpansionRegionClicked;
            city.ExpansionRegionClicked += HandleExpansionRegionClicked;
            city.ExternalDockClicked -= HandleCityExternalDockClicked;
            city.ExternalDockClicked += HandleCityExternalDockClicked;
            RefreshExpansionRegions();
            EnsureDefaultBuildingsSeeded();
            return;
        }

        Transform existingCity = transform.Find(CityRootName);
        GameObject cityObject = existingCity != null ? existingCity.gameObject : new GameObject(CityRootName);
        cityObject.transform.SetParent(transform, false);
        city = cityObject.GetComponent<IsoGridCityPrototype>();
        if (city == null)
        {
            city = cityObject.AddComponent<IsoGridCityPrototype>();
        }

        city.SetPrototypeToolbarVisibleForRuntime(false);
        city.BuildingClicked -= HandleCityBuildingClicked;
        city.BuildingClicked += HandleCityBuildingClicked;
        city.ExpansionRegionClicked -= HandleExpansionRegionClicked;
        city.ExpansionRegionClicked += HandleExpansionRegionClicked;
        city.ExternalDockClicked -= HandleCityExternalDockClicked;
        city.ExternalDockClicked += HandleCityExternalDockClicked;
        RefreshExpansionRegions();
        EnsureDefaultBuildingsSeeded();
        city.SelectFreeMouseModeForRuntime();
    }

    private void EnsureDefaultBuildingsSeeded()
    {
        if (city == null || city.IsMovingBuildingForRuntime)
        {
            return;
        }

        if (defaultBuildingsSeeded && city.BuildingCountForTests >= GetInitialBuildingCountForTests())
        {
            return;
        }

        SeedDefaultBuildings();
        defaultBuildingsSeeded = city.BuildingCountForTests >= GetInitialBuildingCountForTests();
    }

    private void SeedDefaultBuildings()
    {
        if (city == null)
        {
            return;
        }

        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            BaseBuildingDefinition definition = definitions[i];
            for (int instanceIndex = 1; instanceIndex <= definition.initialCount; instanceIndex++)
            {
                string key = GetInstanceKey(definition, instanceIndex);
                if (IsDockDefinition(definition))
                {
                    city.TryPlaceExternalDockAtFirstFreeForRuntime(
                        key,
                        definition.color,
                        definition.visualKind,
                        out _);
                    continue;
                }

                int originX = instanceIndex == 1 ? definition.originX : -1;
                int originY = instanceIndex == 1 ? definition.originY : -1;
                if (originX >= 0 && originY >= 0)
                {
                    originX += LegacySeedOriginOffsetXCells;
                    originY += LegacySeedOriginYCells;
                    city.TryPlacePrefabBuildingForRuntime(
                        key,
                        definition.PrefabResourcePath,
                        definition.width,
                        definition.height,
                        originX,
                        originY,
                        definition.color,
                        definition.visualKind);
                }
                else
                {
                    city.TryPlacePrefabBuildingAtFirstFreeForRuntime(
                        key,
                        definition.PrefabResourcePath,
                        definition.width,
                        definition.height,
                        definition.color,
                        definition.visualKind,
                        out _,
                        out _);
                }
            }
        }
    }

    private void RefreshCityMode()
    {
        bool cityVisible = ShouldShowCity();
        bool hiddenByDockScreen = gameplayHud != null && gameplayHud.IsMetaDockScreenActiveForRuntime;
        if (city != null && city.gameObject.activeSelf != cityVisible)
        {
            city.gameObject.SetActive(cityVisible);
        }

        if (cityVisible && !wasCityVisible)
        {
            SuppressSessionCameraController();
            ApplyCityCameraLeanRenderState();
            bool restoredCamera = city != null && city.RestorePreservedCameraStateForRuntime();
            if (!restoredCamera || !hasInitializedCityCameraView)
            {
                city?.SnapCameraToCityViewForRuntime();
            }

            hasInitializedCityCameraView = true;
            city?.SelectFreeMouseModeForRuntime();
        }
        else if (!cityVisible && wasCityVisible)
        {
            if (hiddenByDockScreen)
            {
                city?.PreserveCameraStateForRuntime();
            }
            else
            {
                hasInitializedCityCameraView = false;
            }

            CloseWindow();
            ClearBuildingFocus();
            RestoreSessionWorldRenderers();
            RestoreSessionCameraController();
            if (!hiddenByDockScreen)
            {
                RestoreCityCameraRenderState();
            }

            if (sessionCameraController != null && gameplaySession != null && gameplaySession.CurrentMode == GameSessionMode.Flight)
            {
                sessionCameraController.FocusOnPlayerShipAfterUndocking();
            }
        }

        if (cityVisible)
        {
            SuppressSessionCameraController();
            SuppressSessionWorldRenderers();
            ApplyCityCameraLeanRenderState();
        }
        else if (hiddenByDockScreen)
        {
            ApplyCityCameraLeanRenderState();
        }
        else
        {
            hoveredBuildingId = "";
            RefreshBuildingOverlay();
            RestoreCityCameraRenderState();
        }

        wasCityVisible = cityVisible;
    }

    private bool ShouldShowCity()
    {
        if (gameplayHud != null && gameplayHud.IsMetaDockScreenActiveForRuntime)
        {
            return false;
        }

        if (meta != null)
        {
            return meta.CurrentMode == GameSessionMode.Docked;
        }

        return gameplaySession != null && gameplaySession.CurrentMode == GameSessionMode.Docked;
    }

    private void SuppressSessionCameraController()
    {
        if (sessionCameraController == null)
        {
            return;
        }

        if (!hasStoredCameraControllerState)
        {
            storedCameraControllerEnabled = sessionCameraController.enabled;
            hasStoredCameraControllerState = true;
        }

        sessionCameraController.enabled = false;
    }

    private void RestoreSessionCameraController()
    {
        if (sessionCameraController != null && hasStoredCameraControllerState)
        {
            sessionCameraController.enabled = storedCameraControllerEnabled;
        }

        hasStoredCameraControllerState = false;
    }

    private void ApplyCityCameraLeanRenderState()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
        if (!hasStoredCityCameraRenderState)
        {
            storedCityCameraAllowHDR = sceneCamera.allowHDR;
            storedCityCameraAllowMSAA = sceneCamera.allowMSAA;
            storedCityCameraRenderPostProcessing = cameraData != null && cameraData.renderPostProcessing;
            storedCityCameraRequiresDepthTexture = cameraData != null && cameraData.requiresDepthTexture;
            storedCityCameraRequiresColorTexture = cameraData != null && cameraData.requiresColorTexture;
            hasStoredCityCameraRenderState = true;
        }

        sceneCamera.allowHDR = false;
        sceneCamera.allowMSAA = false;

        if (cameraData == null)
        {
            return;
        }

        cameraData.renderPostProcessing = false;
        cameraData.requiresDepthTexture = false;
        cameraData.requiresColorTexture = false;
        cameraData.SetRenderer(0);
    }

    private void RestoreCityCameraRenderState()
    {
        if (!hasStoredCityCameraRenderState)
        {
            return;
        }

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera != null)
        {
            sceneCamera.allowHDR = storedCityCameraAllowHDR;
            sceneCamera.allowMSAA = storedCityCameraAllowMSAA;

            UniversalAdditionalCameraData cameraData = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = storedCityCameraRenderPostProcessing;
                cameraData.requiresDepthTexture = storedCityCameraRequiresDepthTexture;
                cameraData.requiresColorTexture = storedCityCameraRequiresColorTexture;
            }
        }

        hasStoredCityCameraRenderState = false;
    }

    private bool IsCityCameraLeanRenderStateActive()
    {
        Camera camera = sceneCamera != null ? sceneCamera : Camera.main;
        if (camera == null || camera.allowHDR || camera.allowMSAA)
        {
            return false;
        }

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        return cameraData == null
            || (!cameraData.renderPostProcessing
                && !cameraData.requiresDepthTexture
                && !cameraData.requiresColorTexture);
    }

    private bool IsCityCameraUsingLeanRenderer()
    {
        string rendererName = GetCameraRendererName(sceneCamera != null ? sceneCamera : Camera.main);
        return rendererName.IndexOf("Renderer2D", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetCameraRendererName(Camera camera)
    {
        if (camera == null)
        {
            return "";
        }

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null || cameraData.scriptableRenderer == null)
        {
            return "";
        }

        return cameraData.scriptableRenderer.GetType().Name;
    }

    private void SuppressSessionWorldRenderers()
    {
        Transform activeShipRoot = GetActiveShipRoot();
        if (sessionWorldRenderersSuppressed && suppressedActiveShipRoot == activeShipRoot)
        {
            return;
        }

        if (sessionWorldRenderersSuppressed)
        {
            RestoreSessionWorldRenderers();
        }

        suppressedActiveShipRoot = activeShipRoot;
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!suppressedRendererStates.ContainsKey(renderer))
            {
                suppressedRendererStates.Add(renderer, renderer.enabled);
            }

            renderer.enabled = false;
        }

        sessionWorldRenderersSuppressed = true;
    }

    private Transform GetActiveShipRoot()
    {
        ShipPhysics ship = meta != null && meta.shipLoader != null
            ? meta.shipLoader.targetShip
            : null;
        if (ship != null)
        {
            return ship.transform;
        }

        return gameplaySession != null ? gameplaySession.PlayerShipRoot : null;
    }

    private int CountSuppressedActiveShipRenderers()
    {
        Transform activeShipRoot = GetActiveShipRoot();
        if (activeShipRoot == null)
        {
            return 0;
        }

        int count = 0;
        foreach (KeyValuePair<Renderer, bool> pair in suppressedRendererStates)
        {
            if (pair.Key != null && pair.Key.transform.IsChildOf(activeShipRoot))
            {
                count++;
            }
        }

        return count;
    }

    private void RestoreSessionWorldRenderers()
    {
        foreach (KeyValuePair<Renderer, bool> pair in suppressedRendererStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        suppressedRendererStates.Clear();
        sessionWorldRenderersSuppressed = false;
        suppressedActiveShipRoot = null;
    }

    private void HandleCityBuildingClicked(string buildingId)
    {
        TrySelectBuilding(buildingId);
    }

    private void HandleCityExternalDockClicked(string dockKey)
    {
        if (string.IsNullOrWhiteSpace(dockKey))
        {
            return;
        }

        OpenDockScreenFromExternalDock();
    }

    private bool OpenDockScreenFromExternalDock()
    {
        EnsureBuilt();
        ClearBuildingFocus();
        SetActiveIfNotNull(buildingCatalogPanel, false);
        if (gameplayHud == null)
        {
            gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
        }

        return gameplayHud != null && gameplayHud.OpenMetaDockScreenForRuntime();
    }

    private bool TrySelectBuilding(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId) || !CanInteractWithBase() || city == null || !city.HasBuildingKeyForTests(buildingId))
        {
            return false;
        }

        if (IsWindowOpenForTests)
        {
            return false;
        }

        EnsureWindow();
        if (windowCanvas == null)
        {
            return false;
        }

        hoveredBuildingId = buildingId;
        forcedHoverBuildingIdForTests = "";
        selectedBuildingId = buildingId;
        openBuildingId = "";
        openActionIndex = -1;
        SetActiveIfNotNull(buildingCatalogPanel, false);
        SetHudBuildingFocus(true);
        RefreshBuildingOverlay();
        return true;
    }

    private bool OpenSelectedBuildingAction(int actionIndex)
    {
        if (string.IsNullOrWhiteSpace(selectedBuildingId)
            || actionIndex < 0
            || actionIndex >= BuildingActionLabels.Length)
        {
            return false;
        }

        EnsureWindow();
        if (windowCanvas == null || windowPanel == null || windowFadeButton == null)
        {
            return false;
        }

        openBuildingId = selectedBuildingId;
        openActionIndex = actionIndex;
        processingWindowOpen = false;
        processingStatusMessage = "";
        cascadeCatalogWindowOpen = false;
        courierWindowOpen = false;
        courierStatusMessage = "";
        openExpansionRegionId = "";
        expansionWindowOpen = false;
        expansionStatusMessage = "";

        if (actionIndex == 1 && IsKnowledgeBuilding(openBuildingId))
        {
            return OpenKnowledgeTechnologyScreen();
        }

        if (actionIndex == 1 && TryGetProcessingBranch(openBuildingId, out BaseProcessingBranch branch))
        {
            return OpenProcessingWindow(branch);
        }

        if (actionIndex == 1 && IsCascadeRecipeBuilding(openBuildingId))
        {
            return OpenCascadeRecipeCatalogWindow();
        }

        if (actionIndex == 1 && IsCourierServiceBuilding(openBuildingId))
        {
            return OpenCourierServiceWindow();
        }

        ConfigureSmallWindow();
        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(cascadeCatalogPanel, false);
        SetActiveIfNotNull(courierPanel, false);
        SetActiveIfNotNull(windowPrimaryButton, false);
        SetActiveIfNotNull(windowBodyText, true);
        string buildingName = GetBuildingDisplayName(openBuildingId);
        SetText(windowTitleText, buildingName + ": " + GetBuildingActionLabel(openBuildingId, actionIndex));
        SetText(windowBodyText, "");
        SetText(windowExitButtonText, "Выйти");
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(windowFadeButton, true);
        windowPanel.gameObject.SetActive(true);
        return true;
    }

    private bool OpenKnowledgeTechnologyScreen()
    {
        if (gameplayHud == null)
        {
            gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
        }

        if (gameplayHud == null || !gameplayHud.OpenKnowledgeScreenForRuntime())
        {
            return false;
        }

        ClearBuildingFocus();
        return true;
    }

    private bool OpenProcessingWindow(BaseProcessingBranch branch)
    {
        if (string.IsNullOrWhiteSpace(openBuildingId) || windowPanel == null)
        {
            return false;
        }

        ConfigureProcessingWindow();
        openProcessingBranch = branch;
        processingWindowOpen = true;
        expansionWindowOpen = false;
        openExpansionRegionId = "";
        processingStatusMessage = "Загрузите сырье в бункер. Цикл идет сам, когда в бункере хватает единиц.";
        SetText(windowTitleText, GetBuildingDisplayName(openBuildingId));
        SetText(windowExitButtonText, "Выйти");
        SetActiveIfNotNull(windowBodyText, false);
        SetActiveIfNotNull(processingPanel, true);
        SetActiveIfNotNull(courierPanel, false);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(windowFadeButton, true);
        windowPanel.gameObject.SetActive(true);
        RefreshProcessingWindow();
        return true;
    }

    private bool OpenCascadeRecipeCatalogWindow()
    {
        if (string.IsNullOrWhiteSpace(openBuildingId) || windowPanel == null)
        {
            return false;
        }

        ConfigureCascadeCatalogWindow();
        processingWindowOpen = false;
        cascadeCatalogWindowOpen = true;
        expansionWindowOpen = false;
        openExpansionRegionId = "";
        SetText(windowTitleText, "Каталог рецептов");
        SetText(windowExitButtonText, "Выйти");
        SetActiveIfNotNull(windowBodyText, false);
        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(cascadeCatalogPanel, true);
        SetActiveIfNotNull(courierPanel, false);
        SetActiveIfNotNull(windowPrimaryButton, false);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(windowFadeButton, true);
        windowPanel.gameObject.SetActive(true);
        selectedCascadeRubricIndex = Mathf.Clamp(selectedCascadeRubricIndex, 0, CascadeCatalogRubrics.Length - 1);
        selectedCascadeRecipeIndex = 0;
        RefreshCascadeRecipeCatalogWindow();
        return true;
    }

    private bool OpenCourierServiceWindow()
    {
        if (string.IsNullOrWhiteSpace(openBuildingId) || windowPanel == null)
        {
            return false;
        }

        ConfigureCourierWindow();
        processingWindowOpen = false;
        cascadeCatalogWindowOpen = false;
        courierWindowOpen = true;
        expansionWindowOpen = false;
        openExpansionRegionId = "";
        selectedCourierSlotIndex = Mathf.Clamp(selectedCourierSlotIndex, 0, MetaGameState.CourierOrderSlotCount - 1);
        courierStatusMessage = "Выберите площадку, загрузите товары со склада и отправьте курьера.";
        SetText(windowTitleText, "Курьерская служба");
        SetText(windowExitButtonText, "Выйти");
        SetActiveIfNotNull(windowBodyText, false);
        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(cascadeCatalogPanel, false);
        SetActiveIfNotNull(courierPanel, true);
        SetActiveIfNotNull(windowPrimaryButton, false);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(windowFadeButton, true);
        windowPanel.gameObject.SetActive(true);
        RefreshCourierWindow();
        return true;
    }

    private bool CanInteractWithBase()
    {
        return meta == null || meta.IsDockedAtCapital();
    }

    private void EnsureWindow()
    {
        if (windowCanvas != null)
        {
            return;
        }

        EnsureEventSystem();
        GameObject canvasObject = new GameObject(WindowCanvasName);
        canvasObject.transform.SetParent(transform, false);
        windowCanvas = canvasObject.AddComponent<Canvas>();
        windowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        windowCanvas.sortingOrder = WindowSortingOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        overlayRoot = CreateRect("Base Building Overlay Root", canvasObject.transform, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });

        RectTransform selectionBackdrop = CreateRect("Building Selection Backdrop", overlayRoot, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });
        Image selectionBackdropImage = selectionBackdrop.gameObject.AddComponent<Image>();
        selectionBackdropImage.color = new Color(0f, 0f, 0f, 0f);
        buildingSelectionBackdropButton = selectionBackdrop.gameObject.AddComponent<Button>();
        buildingSelectionBackdropButton.targetGraphic = selectionBackdropImage;
        buildingSelectionBackdropButton.transition = Selectable.Transition.None;
        buildingSelectionBackdropButton.onClick.AddListener(ClearBuildingFocus);

        buildingInfoPanel = CreateRect("Building Info Label", overlayRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(300f, 92f)
        });
        Image infoImage = buildingInfoPanel.gameObject.AddComponent<Image>();
        infoImage.color = new Color(0.06f, 0.07f, 0.055f, 0.92f);
        infoImage.raycastTarget = false;
        buildingInfoNameText = CreateText(buildingInfoPanel, "", 28, new Vector2(20f, -10f), new Vector2(260f, 42f), TextAnchor.MiddleCenter);
        buildingInfoNameText.fontStyle = FontStyle.Bold;
        buildingInfoLevelText = CreateText(buildingInfoPanel, "", 22, new Vector2(20f, -50f), new Vector2(260f, 34f), TextAnchor.MiddleCenter);

        buildingActionPanel = CreateRect("Building Action Buttons", overlayRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(410f, 86f)
        });
        buildingActionButtons = new Button[BuildingActionLabels.Length];
        buildingActionButtonTexts = new Text[BuildingActionLabels.Length];
        for (int i = 0; i < BuildingActionLabels.Length; i++)
        {
            int actionIndex = i;
            buildingActionButtons[i] = CreateButton(
                buildingActionPanel,
                BuildingActionLabels[i],
                new Vector2(i * 140f, 0f),
                new Vector2(130f, 72f),
                () => OpenSelectedBuildingAction(actionIndex),
                out buildingActionButtonTexts[i]);
        }

        BuildBuildingCatalogPanel();

        GameObject collectBubbleRootObject = new GameObject("Processing Collect Bubbles");
        collectBubbleRootObject.transform.SetParent(transform, false);
        processingCollectBubbleRoot = collectBubbleRootObject.transform;

        RectTransform fade = CreateRect("Building Window Modal Fade", overlayRoot, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });
        Image fadeImage = fade.gameObject.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0.58f);
        windowFadeButton = fade.gameObject.AddComponent<Button>();
        windowFadeButton.targetGraphic = fadeImage;
        windowFadeButton.transition = Selectable.Transition.None;
        windowFadeButton.onClick.AddListener(CloseWindow);

        windowPanel = CreateRect("Empty Building Window", overlayRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(520f, 300f)
        });
        Image panelImage = windowPanel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.073f, 0.072f, 0.985f);

        windowTitleText = CreateText(windowPanel, "", 26, new Vector2(26f, -24f), new Vector2(360f, 42f), TextAnchor.MiddleLeft);
        windowBodyText = CreateText(windowPanel, "", 18, new Vector2(26f, -84f), new Vector2(468f, 120f), TextAnchor.UpperLeft);
        windowExitButton = CreateButton(windowPanel, "Выйти", new Vector2(168f, -222f), new Vector2(184f, 48f), CloseWindow, out windowExitButtonText);
        windowPrimaryButton = CreateButton(windowPanel, "Расчистить", new Vector2(294f, -222f), new Vector2(184f, 48f), HandleExpansionClearButton, out windowPrimaryButtonText);
        SetActiveIfNotNull(windowPrimaryButton, false);
        BuildProcessingPanel();
        BuildCascadeCatalogPanel();
        BuildCourierPanel();

        windowCanvas.gameObject.SetActive(true);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingCatalogPanel, false);
        SetActiveIfNotNull(windowFadeButton, false);
        SetActiveIfNotNull(windowPanel, false);
        SetActiveIfNotNull(cascadeCatalogPanel, false);
        SetActiveIfNotNull(courierPanel, false);
    }

    private void BuildProcessingPanel()
    {
        processingPanel = CreateRect("Processing Window Content", windowPanel, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });

        CreateProcessingSection("Storage Section", new Vector2(26f, -132f), new Vector2(372f, 566f));
        CreateProcessingSection("Bunker Section", new Vector2(424f, -132f), new Vector2(374f, 566f));
        CreateProcessingSection("Output Section", new Vector2(824f, -132f), new Vector2(416f, 566f));

        processingStatsText = CreateText(processingPanel, "", 18, new Vector2(32f, -74f), new Vector2(1020f, 44f), TextAnchor.UpperLeft, new Color(0.92f, 0.88f, 0.68f, 1f));
        processingStorageText = CreateText(processingPanel, "", 17, new Vector2(48f, -150f), new Vector2(326f, 56f), TextAnchor.UpperLeft);
        processingBunkerText = CreateText(processingPanel, "", 18, new Vector2(448f, -150f), new Vector2(326f, 332f), TextAnchor.UpperLeft);
        processingOutputsText = CreateText(processingPanel, "", 17, new Vector2(848f, -150f), new Vector2(364f, 430f), TextAnchor.UpperLeft);
        processingStatusText = CreateText(processingPanel, "", 17, new Vector2(48f, -710f), new Vector2(1160f, 34f), TextAnchor.MiddleLeft, new Color(0.96f, 0.82f, 0.54f, 1f));

        processingInputButtons = new Button[24];
        processingInputButtonTexts = new Text[processingInputButtons.Length];
        for (int i = 0; i < processingInputButtons.Length; i++)
        {
            int index = i;
            float x = 48f;
            float y = -214f - i * 24f;
            processingInputButtons[i] = CreateButton(
                processingPanel,
                "",
                new Vector2(x, y),
                new Vector2(326f, 22f),
                () => HandleProcessingInputButton(index),
                out processingInputButtonTexts[i]);
            processingInputButtonTexts[i].fontSize = 12;
            processingInputButtonTexts[i].alignment = TextAnchor.MiddleLeft;
            processingInputButtonTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        processingLoadAllButton = CreateButton(processingPanel, "Загрузить всё", new Vector2(448f, -610f), new Vector2(176f, 42f), () => HandleProcessingLoadAll(), out _);
        processingClearButton = CreateButton(processingPanel, "Очистить", new Vector2(638f, -610f), new Vector2(136f, 42f), () => HandleProcessingClear(), out _);
        processingCollectButton = CreateButton(processingPanel, "Забрать всё", new Vector2(972f, -610f), new Vector2(220f, 46f), () => HandleProcessingCollect(), out _);

        SetActiveIfNotNull(processingPanel, false);
    }

    private void CreateProcessingSection(string name, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform section = CreateRect(name, processingPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Image image = section.gameObject.AddComponent<Image>();
        image.color = new Color(0.032f, 0.055f, 0.058f, 0.90f);
        image.raycastTarget = false;
    }

    private void BuildCascadeCatalogPanel()
    {
        cascadeCatalogPanel = CreateRect("Cascade Recipe Catalog Content", windowPanel, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });

        Image background = cascadeCatalogPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.96f, 0.92f, 0.84f, 0.99f);
        background.raycastTarget = false;

        CreateCascadeSection("Cascade Header", new Vector2(18f, -16f), new Vector2(1284f, 82f), new Color(0.99f, 0.96f, 0.88f, 0.98f));
        cascadeCatalogHeaderText = CreateText(cascadeCatalogPanel, "", 28, new Vector2(38f, -26f), new Vector2(720f, 42f), TextAnchor.MiddleLeft, new Color(0.13f, 0.14f, 0.13f, 1f));
        cascadeCatalogHeaderText.fontStyle = FontStyle.Bold;
        cascadeCatalogStorageToggleButton = CreateButton(cascadeCatalogPanel, "", new Vector2(978f, -32f), new Vector2(218f, 40f), ToggleCascadeCatalogStorageMode, out cascadeCatalogStorageToggleText);
        StyleCascadeButton(cascadeCatalogStorageToggleButton, false);

        CreateCascadeSection("Cascade Rubrics", new Vector2(18f, -112f), new Vector2(170f, 594f), new Color(0.92f, 0.88f, 0.78f, 0.98f));
        CreateText(cascadeCatalogPanel, "Рубрики", 19, new Vector2(34f, -126f), new Vector2(138f, 28f), TextAnchor.MiddleLeft, new Color(0.15f, 0.14f, 0.12f, 1f)).fontStyle = FontStyle.Bold;
        cascadeCatalogRubricButtons = new Button[CascadeCatalogRubrics.Length];
        cascadeCatalogRubricButtonTexts = new Text[CascadeCatalogRubrics.Length];
        for (int i = 0; i < CascadeCatalogRubrics.Length; i++)
        {
            int index = i;
            cascadeCatalogRubricButtons[i] = CreateButton(
                cascadeCatalogPanel,
                CascadeCatalogRubrics[i],
                new Vector2(34f, -166f - i * 50f),
                new Vector2(138f, 38f),
                () => SelectCascadeRubric(index),
                out cascadeCatalogRubricButtonTexts[i]);
            StyleCascadeButton(cascadeCatalogRubricButtons[i], false);
        }

        CreateCascadeSection("Cascade Recipes", new Vector2(204f, -112f), new Vector2(296f, 594f), new Color(0.98f, 0.94f, 0.86f, 0.98f));
        cascadeCatalogRecipeListTitleText = CreateText(cascadeCatalogPanel, "", 19, new Vector2(222f, -126f), new Vector2(252f, 28f), TextAnchor.MiddleLeft, new Color(0.15f, 0.14f, 0.12f, 1f));
        cascadeCatalogRecipeListTitleText.fontStyle = FontStyle.Bold;
        cascadeCatalogRecipeButtons = new Button[6];
        cascadeCatalogRecipeButtonTexts = new Text[cascadeCatalogRecipeButtons.Length];
        for (int i = 0; i < cascadeCatalogRecipeButtons.Length; i++)
        {
            int index = i;
            cascadeCatalogRecipeButtons[i] = CreateButton(
                cascadeCatalogPanel,
                "",
                new Vector2(222f, -166f - i * 74f),
                new Vector2(258f, 62f),
                () => SelectCascadeRecipe(index),
                out cascadeCatalogRecipeButtonTexts[i]);
            cascadeCatalogRecipeButtonTexts[i].fontSize = 16;
            cascadeCatalogRecipeButtonTexts[i].alignment = TextAnchor.MiddleLeft;
            StyleCascadeButton(cascadeCatalogRecipeButtons[i], false);
        }

        CreateCascadeSection("Cascade Plan", new Vector2(516f, -112f), new Vector2(424f, 594f), new Color(0.99f, 0.96f, 0.90f, 0.99f));
        cascadeCatalogRecipeTitleText = CreateText(cascadeCatalogPanel, "", 26, new Vector2(540f, -130f), new Vector2(360f, 36f), TextAnchor.MiddleLeft, new Color(0.12f, 0.13f, 0.13f, 1f));
        cascadeCatalogRecipeTitleText.fontStyle = FontStyle.Bold;
        cascadeCatalogRecipeMetaText = CreateText(cascadeCatalogPanel, "", 17, new Vector2(540f, -172f), new Vector2(360f, 56f), TextAnchor.UpperLeft, new Color(0.24f, 0.31f, 0.31f, 1f));
        cascadeCatalogCompositionText = CreateText(cascadeCatalogPanel, "", 17, new Vector2(540f, -242f), new Vector2(360f, 222f), TextAnchor.UpperLeft, new Color(0.15f, 0.14f, 0.12f, 1f));
        cascadeCatalogBottleneckText = CreateText(cascadeCatalogPanel, "", 17, new Vector2(540f, -488f), new Vector2(266f, 166f), TextAnchor.UpperLeft, new Color(0.16f, 0.15f, 0.13f, 1f));
        cascadeCatalogBottleneckButtons = new Button[3];
        cascadeCatalogBottleneckButtonTexts = new Text[cascadeCatalogBottleneckButtons.Length];
        for (int i = 0; i < cascadeCatalogBottleneckButtons.Length; i++)
        {
            int index = i;
            cascadeCatalogBottleneckButtons[i] = CreateButton(
                cascadeCatalogPanel,
                "Перейти",
                new Vector2(812f, -524f - i * 42f),
                new Vector2(92f, 30f),
                () => FocusCascadeBottleneck(index),
                out cascadeCatalogBottleneckButtonTexts[i]);
            cascadeCatalogBottleneckButtonTexts[i].fontSize = 13;
            StyleCascadeButton(cascadeCatalogBottleneckButtons[i], false);
        }

        CreateCascadeSection("Cascade Result", new Vector2(956f, -112f), new Vector2(346f, 594f), new Color(0.98f, 0.94f, 0.86f, 0.98f));
        CreateText(cascadeCatalogPanel, "Результат", 19, new Vector2(980f, -130f), new Vector2(280f, 30f), TextAnchor.MiddleLeft, new Color(0.15f, 0.14f, 0.12f, 1f)).fontStyle = FontStyle.Bold;
        cascadeCatalogResultText = CreateText(cascadeCatalogPanel, "", 18, new Vector2(980f, -178f), new Vector2(284f, 250f), TextAnchor.UpperLeft, new Color(0.14f, 0.14f, 0.12f, 1f));
        cascadeCatalogStatusText = CreateText(cascadeCatalogPanel, "", 16, new Vector2(980f, -454f), new Vector2(284f, 96f), TextAnchor.UpperLeft, new Color(0.42f, 0.30f, 0.12f, 1f));
        cascadeCatalogStartButton = CreateButton(cascadeCatalogPanel, "В очередь", new Vector2(1056f, -642f), new Vector2(170f, 44f), HandleCascadeCatalogStartButton, out cascadeCatalogStartButtonText);
        StyleCascadeButton(cascadeCatalogStartButton, true);

        SetActiveIfNotNull(cascadeCatalogPanel, false);
    }

    private void BuildCourierPanel()
    {
        courierPanel = CreateRect("Courier Service Content", windowPanel, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });

        Image background = courierPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.045f, 0.085f, 0.09f, 0.99f);
        background.raycastTarget = false;

        CreateCourierSection("Courier Header", new Vector2(18f, -16f), new Vector2(1284f, 82f), new Color(0.05f, 0.14f, 0.15f, 0.96f));
        courierHeaderText = CreateText(courierPanel, "", 26, new Vector2(38f, -24f), new Vector2(910f, 52f), TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 0.94f, 1f));
        courierHeaderText.fontStyle = FontStyle.Bold;

        CreateCourierSection("Courier Slots", new Vector2(18f, -112f), new Vector2(426f, 594f), new Color(0.035f, 0.12f, 0.13f, 0.98f));
        CreateText(courierPanel, "Площадки", 19, new Vector2(38f, -126f), new Vector2(180f, 28f), TextAnchor.MiddleLeft, new Color(0.78f, 0.95f, 0.92f, 1f)).fontStyle = FontStyle.Bold;
        courierListText = CreateText(courierPanel, "", 15, new Vector2(238f, -126f), new Vector2(182f, 28f), TextAnchor.MiddleRight, new Color(0.64f, 0.82f, 0.80f, 1f));
        courierOrderButtons = new Button[MetaGameState.CourierOrderSlotCount];
        courierOrderButtonTexts = new Text[courierOrderButtons.Length];
        for (int i = 0; i < courierOrderButtons.Length; i++)
        {
            int index = i;
            courierOrderButtons[i] = CreateButton(
                courierPanel,
                "",
                new Vector2(38f, -166f - i * 56f),
                new Vector2(382f, 46f),
                () => SelectCourierSlot(index),
                out courierOrderButtonTexts[i]);
            courierOrderButtonTexts[i].fontSize = 15;
            courierOrderButtonTexts[i].alignment = TextAnchor.MiddleLeft;
            StyleCourierButton(courierOrderButtons[i], false);
        }

        CreateCourierSection("Courier Detail", new Vector2(468f, -112f), new Vector2(834f, 594f), new Color(0.06f, 0.10f, 0.105f, 0.98f));
        courierDetailText = CreateText(courierPanel, "", 19, new Vector2(494f, -136f), new Vector2(760f, 388f), TextAnchor.UpperLeft, new Color(0.88f, 0.90f, 0.82f, 1f));
        courierStatusText = CreateText(courierPanel, "", 17, new Vector2(494f, -546f), new Vector2(760f, 48f), TextAnchor.UpperLeft, new Color(0.98f, 0.78f, 0.42f, 1f));
        courierSendButton = CreateButton(courierPanel, "Отправить", new Vector2(520f, -646f), new Vector2(190f, 48f), () => HandleCourierSend(), out courierSendButtonText);
        courierCancelButton = CreateButton(courierPanel, "Отменить", new Vector2(734f, -646f), new Vector2(190f, 48f), () => HandleCourierCancel(), out courierCancelButtonText);
        courierCancelAllButton = CreateButton(courierPanel, "Отменить все", new Vector2(1048f, -646f), new Vector2(210f, 48f), () => HandleCourierCancelAll(), out courierCancelAllButtonText);
        StyleCourierButton(courierSendButton, true);
        StyleCourierButton(courierCancelButton, false);
        StyleCourierButton(courierCancelAllButton, false);

        SetActiveIfNotNull(courierPanel, false);
    }

    private RectTransform CreateCascadeSection(string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform section = CreateRect(name, cascadeCatalogPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Image image = section.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return section;
    }

    private RectTransform CreateCourierSection(string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform section = CreateRect(name, courierPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Image image = section.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return section;
    }

    private void ConfigureCascadeCatalogWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.sizeDelta = new Vector2(1320f, 760f);
        }

        SetRect(windowTitleText, new Vector2(26f, -18f), new Vector2(640f, 42f));
        SetRect(windowExitButton, new Vector2(1110f, -24f), new Vector2(184f, 48f));
        SetActiveIfNotNull(windowPrimaryButton, false);
    }

    private void RefreshCascadeRecipeCatalogWindow()
    {
        if (!cascadeCatalogWindowOpen || cascadeCatalogPanel == null)
        {
            return;
        }

        int buildingLevel = GetBuildingLevelValue(openBuildingId);
        string buildingName = GetBuildingDisplayName(openBuildingId);
        SetText(cascadeCatalogHeaderText, buildingName + " - каталог рецептов, " + buildingLevel + " уровень");
        SetText(cascadeCatalogStorageToggleText, (cascadeCatalogAvoidStorage ? "[x] " : "[ ] ") + "Не брать со склада");

        List<CascadeCatalogRecipeView> recipes = BuildCascadeCatalogRecipes();
        List<CascadeCatalogRecipeView> filtered = FilterCascadeCatalogRecipes(recipes, selectedCascadeRubricIndex);
        if (selectedCascadeRecipeIndex >= filtered.Count)
        {
            selectedCascadeRecipeIndex = Mathf.Max(0, filtered.Count - 1);
        }

        RefreshCascadeRubricButtons();
        RefreshCascadeRecipeButtons(filtered, buildingLevel);

        if (filtered.Count == 0)
        {
            SetText(cascadeCatalogRecipeTitleText, "Нет рецептов");
            SetText(cascadeCatalogRecipeMetaText, "");
            SetText(cascadeCatalogCompositionText, "");
            SetText(cascadeCatalogBottleneckText, "");
            SetText(cascadeCatalogResultText, "");
            SetText(cascadeCatalogStatusText, "В этой рубрике пока нет доступных рецептов.");
            SetInteractable(cascadeCatalogStartButton, false);
            RefreshCascadeBottleneckButtons(null);
            return;
        }

        CascadeCatalogRecipeView selected = filtered[selectedCascadeRecipeIndex];
        RefreshCascadeRecipeDetails(selected, buildingLevel);
    }

    private void RefreshCascadeRubricButtons()
    {
        if (cascadeCatalogRubricButtons == null) return;
        for (int i = 0; i < cascadeCatalogRubricButtons.Length; i++)
        {
            bool selected = i == selectedCascadeRubricIndex;
            StyleCascadeButton(cascadeCatalogRubricButtons[i], selected);
        }
    }

    private void RefreshCascadeRecipeButtons(List<CascadeCatalogRecipeView> recipes, int buildingLevel)
    {
        SetText(cascadeCatalogRecipeListTitleText, CascadeCatalogRubrics[Mathf.Clamp(selectedCascadeRubricIndex, 0, CascadeCatalogRubrics.Length - 1)]);
        if (cascadeCatalogRecipeButtons == null) return;

        for (int i = 0; i < cascadeCatalogRecipeButtons.Length; i++)
        {
            bool visible = recipes != null && i < recipes.Count;
            SetActiveIfNotNull(cascadeCatalogRecipeButtons[i], visible);
            if (!visible) continue;

            CascadeCatalogRecipeView recipe = recipes[i];
            bool selected = i == selectedCascadeRecipeIndex;
            bool unlocked = buildingLevel >= recipe.requiredLevel;
            SetText(cascadeCatalogRecipeButtonTexts[i],
                recipe.title + "\n" + (unlocked ? "Доступно" : "Нужен уровень " + recipe.requiredLevel));
            StyleCascadeButton(cascadeCatalogRecipeButtons[i], selected);
        }
    }

    private void RefreshCascadeRecipeDetails(CascadeCatalogRecipeView recipe, int buildingLevel)
    {
        CascadeProductionEstimate estimate = recipe.order != null && meta != null
            ? meta.EstimateBaseCascadeOrder(recipe.order)
            : null;
        bool unlocked = buildingLevel >= recipe.requiredLevel;
        bool canQueue = unlocked && (recipe.order == null || estimate == null || estimate.canRun);

        SetText(cascadeCatalogRecipeTitleText, recipe.title);
        SetText(cascadeCatalogRecipeMetaText,
            recipe.category + "  |  базовое время " + recipe.baseSeconds.ToString("0") + " сек"
            + "\nСкорость здания: " + GetCascadeBuildingSpeedPercent(buildingLevel).ToString("0") + "%"
            + "  |  расчетное время " + GetCascadeRecipeSeconds(recipe, estimate, buildingLevel).ToString("0.#") + " сек");

        SetText(cascadeCatalogCompositionText, BuildCascadeRecipeCompositionText(recipe));
        SetText(cascadeCatalogBottleneckText, BuildCascadeBottleneckText(recipe));
        SetText(cascadeCatalogResultText, BuildCascadeResultText(recipe));
        string status = !unlocked
            ? "Рецепт закрыт: нужен " + recipe.requiredLevel + " уровень здания."
            : estimate != null && !estimate.canRun
            ? estimate.blockedReason
            : "План готов. Очередь производства подключается следующим шагом.";
        SetText(cascadeCatalogStatusText, status);
        SetInteractable(cascadeCatalogStartButton, canQueue);
        SetText(cascadeCatalogStartButtonText, recipe.order == null ? "К улучшению" : "В очередь");
        RefreshCascadeBottleneckButtons(recipe.order);
    }

    private List<CascadeCatalogRecipeView> BuildCascadeCatalogRecipes()
    {
        List<CascadeCatalogRecipeView> recipes = new List<CascadeCatalogRecipeView>();
        List<CascadeProductionOrderDefinition> orders = meta != null
            ? meta.CreateStarterCascadeOrders()
            : SessionExtractionIndustry.CreateStarterCascadeOrders();

        for (int i = 0; i < orders.Count; i++)
        {
            CascadeProductionOrderDefinition order = orders[i];
            if (order == null) continue;
            string category = GetCascadeOrderCategory(order);
            recipes.Add(new CascadeCatalogRecipeView(
                category,
                order.displayName,
                GetCascadeOrderDescription(order),
                1,
                10f + i * 4f,
                order));
        }

        recipes.Add(new CascadeCatalogRecipeView(
            "Здания",
            "Улучшение: " + GetBuildingDisplayName(openBuildingId),
            "+1 уровень здания, больше рецептов и выше скорость.",
            1,
            18f,
            null));
        return recipes;
    }

    private static List<CascadeCatalogRecipeView> FilterCascadeCatalogRecipes(List<CascadeCatalogRecipeView> recipes, int rubricIndex)
    {
        List<CascadeCatalogRecipeView> filtered = new List<CascadeCatalogRecipeView>();
        string rubric = CascadeCatalogRubrics[Mathf.Clamp(rubricIndex, 0, CascadeCatalogRubrics.Length - 1)];
        for (int i = 0; i < recipes.Count; i++)
        {
            CascadeCatalogRecipeView recipe = recipes[i];
            if (rubric == "Все" || recipe.category == rubric)
            {
                filtered.Add(recipe);
            }
        }

        return filtered;
    }

    private string BuildCascadeRecipeCompositionText(CascadeCatalogRecipeView recipe)
    {
        if (recipe.order == null)
        {
            return "Состав\n" + (meta != null ? meta.GetNextBaseIndustryUpgradeOverviewText() : "Стоимость будет рассчитана из уровня здания.");
        }

        PortStorageState storage = meta != null ? meta.GetCapitalStorageState() : null;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append("Состав").AppendLine(cascadeCatalogAvoidStorage ? "  (склад не учитывается)" : "  (с учетом склада)");
        for (int i = 0; i < recipe.order.inputs.Count; i++)
        {
            CascadeItemAmount input = recipe.order.inputs[i];
            int available = storage != null && !cascadeCatalogAvoidStorage ? storage.GetResourceAmount(input.itemId) : 0;
            builder.Append("- ")
                .Append(GetItemDisplayName(input.itemId))
                .Append(" x").Append(input.amount);
            if (!cascadeCatalogAvoidStorage)
            {
                builder.Append("  | склад: ").Append(available);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildCascadeBottleneckText(CascadeCatalogRecipeView recipe)
    {
        List<CascadeBottleneckView> bottlenecks = BuildCascadeBottlenecks(recipe.order);
        if (bottlenecks.Count == 0)
        {
            return "Топ-3 узких места\n1. " + GetBuildingDisplayName(openBuildingId) + "\n2. Склад ресурсов\n3. Очередь проектов";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Топ-3 узких места");
        for (int i = 0; i < Mathf.Min(3, bottlenecks.Count); i++)
        {
            CascadeBottleneckView bottleneck = bottlenecks[i];
            builder.Append(i + 1)
                .Append(". ")
                .Append(SessionExtractionIndustry.GetProductionDisplayName(bottleneck.type))
                .Append(" - ")
                .Append(bottleneck.minutes.ToString("0.#"))
                .AppendLine(" мин");
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildCascadeResultText(CascadeCatalogRecipeView recipe)
    {
        if (recipe.order == null)
        {
            return "+1 уровень: " + GetBuildingDisplayName(openBuildingId)
                + "\n\nДает:\n- новые рецепты\n- выше скорость производства\n- меньше время каскадов";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine(recipe.description).AppendLine();
        builder.AppendLine("Получим:");
        for (int i = 0; i < recipe.order.outputs.Count; i++)
        {
            CascadeItemAmount output = recipe.order.outputs[i];
            builder.Append("+")
                .Append(output.amount)
                .Append(" ")
                .AppendLine(GetItemDisplayName(output.itemId));
        }

        return builder.ToString().TrimEnd();
    }

    private List<CascadeBottleneckView> BuildCascadeBottlenecks(CascadeProductionOrderDefinition order)
    {
        List<CascadeBottleneckView> bottlenecks = new List<CascadeBottleneckView>();
        if (order == null)
        {
            return bottlenecks;
        }

        for (int i = 0; i < order.loads.Count; i++)
        {
            CascadeProductionLoad load = order.loads[i];
            float capacity = SessionExtractionIndustry.GetDefaultProductionCapacity(load.type);
            if (meta != null && meta.progress != null && meta.progress.baseIndustry != null)
            {
                CascadeProductionLineState line = meta.progress.baseIndustry.GetProduction(load.type);
                if (line != null)
                {
                    capacity = line.capacityUnitsPerMinute;
                }
            }

            bottlenecks.Add(new CascadeBottleneckView(load.type, load.loadUnits / Mathf.Max(0.1f, capacity)));
        }

        bottlenecks.Sort((left, right) => right.minutes.CompareTo(left.minutes));
        return bottlenecks;
    }

    private void RefreshCascadeBottleneckButtons(CascadeProductionOrderDefinition order)
    {
        List<CascadeBottleneckView> bottlenecks = BuildCascadeBottlenecks(order);
        for (int i = 0; i < cascadeCatalogBottleneckButtons.Length; i++)
        {
            bool visible = i < bottlenecks.Count || order == null && i == 0;
            SetActiveIfNotNull(cascadeCatalogBottleneckButtons[i], visible);
            if (visible)
            {
                StyleCascadeButton(cascadeCatalogBottleneckButtons[i], false);
            }
        }
    }

    private float GetCascadeRecipeSeconds(CascadeCatalogRecipeView recipe, CascadeProductionEstimate estimate, int buildingLevel)
    {
        float baseSeconds = recipe.baseSeconds;
        if (estimate != null && estimate.bottleneckMinutes > 0f)
        {
            baseSeconds = estimate.bottleneckMinutes * 60f;
        }

        return baseSeconds / Mathf.Max(0.1f, GetCascadeBuildingSpeedPercent(buildingLevel) / 100f);
    }

    private static float GetCascadeBuildingSpeedPercent(int level)
    {
        return 100f + Mathf.Max(0, level - 1) * 20f;
    }

    private void SelectCascadeRubric(int index)
    {
        selectedCascadeRubricIndex = Mathf.Clamp(index, 0, CascadeCatalogRubrics.Length - 1);
        selectedCascadeRecipeIndex = 0;
        RefreshCascadeRecipeCatalogWindow();
    }

    private void SelectCascadeRecipe(int index)
    {
        selectedCascadeRecipeIndex = Mathf.Max(0, index);
        RefreshCascadeRecipeCatalogWindow();
    }

    private void ToggleCascadeCatalogStorageMode()
    {
        cascadeCatalogAvoidStorage = !cascadeCatalogAvoidStorage;
        RefreshCascadeRecipeCatalogWindow();
    }

    private void FocusCascadeBottleneck(int index)
    {
        List<CascadeCatalogRecipeView> recipes = FilterCascadeCatalogRecipes(BuildCascadeCatalogRecipes(), selectedCascadeRubricIndex);
        if (recipes.Count == 0) return;

        CascadeCatalogRecipeView recipe = recipes[Mathf.Clamp(selectedCascadeRecipeIndex, 0, recipes.Count - 1)];
        List<CascadeBottleneckView> bottlenecks = BuildCascadeBottlenecks(recipe.order);
        string lineName = bottlenecks.Count > index
            ? SessionExtractionIndustry.GetProductionDisplayName(bottlenecks[index].type)
            : GetBuildingDisplayName(openBuildingId);
        SetText(cascadeCatalogStatusText, "Переход к улучшению: " + lineName + ". Окно улучшений подключим следующим шагом.");
    }

    private void HandleCascadeCatalogStartButton()
    {
        List<CascadeCatalogRecipeView> recipes = FilterCascadeCatalogRecipes(BuildCascadeCatalogRecipes(), selectedCascadeRubricIndex);
        if (recipes.Count == 0) return;

        CascadeCatalogRecipeView recipe = recipes[Mathf.Clamp(selectedCascadeRecipeIndex, 0, recipes.Count - 1)];
        if (recipe.order != null && meta != null)
        {
            bool queued = meta.TryQueueBaseCascadeOrder(recipe.order, 1, out string queueMessage);
            SetText(cascadeCatalogStatusText, queueMessage);
            if (queued)
            {
                RefreshCascadeRecipeCatalogWindow();
            }

            return;
        }

        SetText(cascadeCatalogStatusText, "Рецепт готов к постановке в производственную очередь. Саму очередь подключим следующим шагом.");
    }

    private static string GetCascadeOrderCategory(CascadeProductionOrderDefinition order)
    {
        string id = order != null ? order.orderId : "";
        if (id.IndexOf("airframe", StringComparison.OrdinalIgnoreCase) >= 0) return "Корабли";
        if (id.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0) return "Модули";
        if (id.IndexOf("munition", StringComparison.OrdinalIgnoreCase) >= 0) return "Боеприпасы";
        return "Все";
    }

    private static string GetCascadeOrderDescription(CascadeProductionOrderDefinition order)
    {
        string category = GetCascadeOrderCategory(order);
        if (category == "Корабли") return "Набор для сборки корабельного корпуса.";
        if (category == "Модули") return "Набор деталей для корабельных модулей.";
        if (category == "Боеприпасы") return "Партия боеприпасов и расходников.";
        return "Каскадный производственный заказ.";
    }

    private void StyleCascadeButton(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Color normal = selected
            ? new Color(0.52f, 0.82f, 0.80f, 1f)
            : new Color(0.91f, 0.86f, 0.75f, 1f);
        Color highlighted = selected
            ? new Color(0.62f, 0.90f, 0.88f, 1f)
            : new Color(0.98f, 0.93f, 0.80f, 1f);
        if (image != null)
        {
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = new Color(0.76f, 0.66f, 0.48f, 1f);
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(0.72f, 0.69f, 0.62f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = new Color(0.16f, 0.14f, 0.11f, button.interactable ? 1f : 0.58f);
        }
    }

    private void StyleCourierButton(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        Color normal = selected
            ? new Color(0.10f, 0.72f, 0.76f, 1f)
            : new Color(0.10f, 0.28f, 0.30f, 1f);
        Color highlighted = selected
            ? new Color(0.16f, 0.86f, 0.90f, 1f)
            : new Color(0.16f, 0.38f, 0.40f, 1f);
        if (image != null)
        {
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = new Color(0.06f, 0.20f, 0.22f, 1f);
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(0.08f, 0.12f, 0.13f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = new Color(0.88f, 0.98f, 0.95f, button.interactable ? 1f : 0.50f);
        }
    }

    private void BuildBuildingCatalogPanel()
    {
        buildingCatalogPanel = CreateRect("Building Catalog Panel", overlayRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0f),
            anchorMax = new Vector2(0.5f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 30f),
            sizeDelta = new Vector2(1240f, 184f)
        });
        Image panelImage = buildingCatalogPanel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.018f, 0.040f, 0.046f, 0.96f);

        Text title = CreateText(buildingCatalogPanel, "Здания", 20, new Vector2(18f, -10f), new Vector2(180f, 30f), TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;
        buildingCatalogStatusText = CreateText(buildingCatalogPanel, "", 15, new Vector2(180f, -12f), new Vector2(780f, 28f), TextAnchor.MiddleLeft, new Color(0.92f, 0.84f, 0.62f, 1f));

        RectTransform viewport = CreateRect("Building Catalog Viewport", buildingCatalogPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 16f),
            sizeDelta = new Vector2(-32f, 118f)
        });
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.006f, 0.012f, 0.014f, 0.70f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        buildingCatalogContent = CreateRect("Building Catalog Content", viewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        });

        ScrollRect scroll = buildingCatalogPanel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;
        scroll.content = buildingCatalogContent;

        BuildBuildingCatalogButtons();
    }

    private void BuildBuildingCatalogButtons()
    {
        if (buildingCatalogContent == null)
        {
            return;
        }

        buildingCatalogButtons.Clear();
        buildingCatalogButtonTexts.Clear();
        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        const float buttonWidth = 136f;
        const float buttonGap = 10f;
        const float buttonHeight = 92f;
        buildingCatalogContent.sizeDelta = new Vector2(definitions.Length * (buttonWidth + buttonGap) + 8f, 0f);
        for (int i = 0; i < definitions.Length; i++)
        {
            BaseBuildingDefinition definition = definitions[i];
            int index = i;
            Button button = CreateButton(
                buildingCatalogContent,
                "",
                new Vector2(8f + index * (buttonWidth + buttonGap), 0f),
                new Vector2(buttonWidth, buttonHeight),
                () => TryPlaceCatalogBuilding(definitions[index].id),
                out Text labelText);
            buildingCatalogButtons[definition.id] = button;
            buildingCatalogButtonTexts[definition.id] = labelText;
        }

        RefreshBuildingCatalogButtons();
    }

    private bool TryPlaceCatalogBuilding(string buildingId)
    {
        if (!TryGetDefinition(buildingId, out BaseBuildingDefinition definition) || city == null)
        {
            return false;
        }

        int currentCount = CountPlacedBuildings(definition);
        int maxCount = GetBuildingMaxCount(definition);
        if (currentCount >= maxCount)
        {
            SetBuildingCatalogStatus(definition.displayName + ": лимит уже выбран.");
            RefreshBuildingCatalogButtons();
            return false;
        }

        string key = GetNextAvailableInstanceKey(definition);
        if (IsDockDefinition(definition))
        {
            bool dockPlaced = city.TryPlaceExternalDockAtFirstFreeForRuntime(
                key,
                definition.color,
                definition.visualKind,
                out _);
            if (!dockPlaced)
            {
                SetBuildingCatalogStatus("Нет свободного док-слота для " + definition.displayName + ".");
                RefreshBuildingCatalogButtons();
                return false;
            }

            SetBuildingCatalogStatus(definition.displayName + " поставлен в док-слот.");
            RefreshBuildingCatalogButtons();
            return true;
        }

        bool placed = city.TryPlacePrefabBuildingAtFirstFreeForRuntime(
            key,
            definition.PrefabResourcePath,
            definition.width,
            definition.height,
            definition.color,
            definition.visualKind,
            out _,
            out _);
        if (!placed)
        {
            SetBuildingCatalogStatus("Нет пустого места для " + definition.displayName + " " + definition.width + "x" + definition.height + ".");
            RefreshBuildingCatalogButtons();
            return false;
        }

        SetBuildingCatalogStatus(definition.displayName + " поставлено.");
        RefreshBuildingCatalogButtons();
        return true;
    }

    private void RefreshBuildingCatalogButtons()
    {
        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            BaseBuildingDefinition definition = definitions[i];
            int placed = CountPlacedBuildings(definition);
            int maxCount = GetBuildingMaxCount(definition);
            int remaining = Mathf.Max(0, maxCount - placed);
            if (buildingCatalogButtonTexts.TryGetValue(definition.id, out Text text))
            {
                SetText(text,
                    definition.displayName
                    + "\n" + definition.width + "x" + definition.height
                    + "\n" + remaining + "/" + maxCount);
            }

            if (buildingCatalogButtons.TryGetValue(definition.id, out Button button))
            {
                SetInteractable(button, remaining > 0);
            }
        }
    }

    private void SetBuildingCatalogStatus(string message)
    {
        buildingCatalogStatus = message ?? "";
        SetText(buildingCatalogStatusText, buildingCatalogStatus);
    }

    private void CloseWindow()
    {
        openBuildingId = "";
        openActionIndex = -1;
        processingWindowOpen = false;
        processingStatusMessage = "";
        cascadeCatalogWindowOpen = false;
        courierWindowOpen = false;
        courierStatusMessage = "";
        openExpansionRegionId = "";
        expansionWindowOpen = false;
        expansionStatusMessage = "";
        SetActiveIfNotNull(windowFadeButton, false);
        SetActiveIfNotNull(windowPanel, false);
        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(cascadeCatalogPanel, false);
        SetActiveIfNotNull(courierPanel, false);
        SetActiveIfNotNull(windowPrimaryButton, false);
        ClearBuildingFocus();
    }

    private void ClearBuildingFocus()
    {
        selectedBuildingId = "";
        forcedHoverBuildingIdForTests = "";
        openBuildingId = "";
        openActionIndex = -1;
        processingWindowOpen = false;
        processingStatusMessage = "";
        cascadeCatalogWindowOpen = false;
        courierWindowOpen = false;
        courierStatusMessage = "";
        openExpansionRegionId = "";
        expansionWindowOpen = false;
        expansionStatusMessage = "";
        SetHudBuildingFocus(false);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(windowFadeButton, false);
        SetActiveIfNotNull(windowPanel, false);
        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(courierPanel, false);
        SetActiveIfNotNull(windowPrimaryButton, false);
        RefreshBuildingOverlay();
    }

    private void ConfigureSmallWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.sizeDelta = new Vector2(520f, 300f);
        }

        SetRect(windowTitleText, new Vector2(26f, -24f), new Vector2(360f, 42f));
        SetRect(windowBodyText, new Vector2(26f, -84f), new Vector2(468f, 120f));
        SetRect(windowExitButton, new Vector2(168f, -222f), new Vector2(184f, 48f));
        SetActiveIfNotNull(windowPrimaryButton, false);
    }

    private void ConfigureProcessingWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.sizeDelta = new Vector2(1320f, 760f);
        }

        SetRect(windowTitleText, new Vector2(26f, -20f), new Vector2(860f, 46f));
        SetRect(windowExitButton, new Vector2(1110f, -24f), new Vector2(184f, 48f));
        SetActiveIfNotNull(windowPrimaryButton, false);
    }

    private void ConfigureCourierWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.sizeDelta = new Vector2(1320f, 760f);
        }

        SetRect(windowTitleText, new Vector2(26f, -20f), new Vector2(860f, 46f));
        SetRect(windowExitButton, new Vector2(1110f, -24f), new Vector2(184f, 48f));
        SetActiveIfNotNull(windowPrimaryButton, false);
    }

    private void RefreshProcessingWindowIfDue()
    {
        if (!processingWindowOpen)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!processingWindowDirty && now < nextProcessingWindowRefreshTime)
        {
            return;
        }

        RefreshProcessingWindow();
    }

    private void RefreshProcessingWindow()
    {
        if (!processingWindowOpen
            || processingPanel == null
            || !processingPanel.gameObject.activeSelf
            || meta == null
            || string.IsNullOrWhiteSpace(openBuildingId))
        {
            return;
        }

        int level = GetBuildingLevelValue(openBuildingId);
        BaseProcessingFacilityState facility = meta.GetBaseProcessingFacilityState(openBuildingId, openProcessingBranch, level);
        PortStorageState storage = meta.GetCapitalStorageState();
        List<string> inputs = meta.GetBaseProcessingInputItemIds(openProcessingBranch);
        List<string> outputs = meta.GetBaseProcessingOutputItemIds(openProcessingBranch);

        SetText(processingStatsText,
            "Уровень " + facility.level
            + "    КПД " + (facility.efficiency * 100f).ToString("0.#") + "%"
            + "    Объем " + facility.cycleInputUnits + " ед./цикл"
            + "    Бункер " + facility.BunkerLoadUnits + "/" + facility.bunkerCapacityUnits
            + "    Цикл " + facility.cycleElapsedSeconds.ToString("0.0") + " / " + facility.cycleDurationSeconds.ToString("0") + " сек");

        SetText(processingStorageText, BuildProcessingStorageText(storage, inputs));
        SetText(processingBunkerText, BuildProcessingBunkerText(facility));
        SetText(processingOutputsText, BuildProcessingOutputsText(facility, outputs));
        SetText(processingStatusText, processingStatusMessage);
        RefreshProcessingInputButtons(storage, facility, inputs);

        SetInteractable(processingLoadAllButton, facility.BunkerFreeUnits > 0 && HasAnyStoredInput(storage, inputs));
        SetInteractable(processingClearButton, facility.BunkerLoadUnits > 0);
        SetInteractable(processingCollectButton, HasAnyReadyOutput(facility));
        processingWindowDirty = false;
        nextProcessingWindowRefreshTime = Time.unscaledTime + ProcessingWindowRefreshSeconds;
    }

    private void RefreshCourierWindowIfDue()
    {
        if (!courierWindowOpen)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!courierWindowDirty && now < nextCourierWindowRefreshTime)
        {
            return;
        }

        RefreshCourierWindow();
    }

    private void RefreshCourierWindow()
    {
        if (!courierWindowOpen
            || courierPanel == null
            || !courierPanel.gameObject.activeSelf
            || meta == null)
        {
            return;
        }

        meta.GetCourierOrderSlots();
        selectedCourierSlotIndex = Mathf.Clamp(selectedCourierSlotIndex, 0, MetaGameState.CourierOrderSlotCount - 1);
        CourierOrderSlotState selectedSlot = meta.GetCourierOrderSlot(selectedCourierSlotIndex);
        PortStorageState storage = meta.GetCapitalStorageState();

        int activeCount = 0;
        int coolingCount = 0;
        for (int i = 0; i < MetaGameState.CourierOrderSlotCount; i++)
        {
            CourierOrderSlotState slot = meta.GetCourierOrderSlot(i);
            if (slot == null)
            {
                continue;
            }

            if (slot.HasActiveOrder)
            {
                activeCount++;
            }
            else if (meta.GetCourierOrderCooldownRemainingSeconds(i) > 0)
            {
                coolingCount++;
            }
        }

        SetText(courierHeaderText,
            "Ветровые Дома  |  Заказы: 1-3 ресурса  |  Отмена: 15 мин  |  Активно: "
            + activeCount + "/" + MetaGameState.CourierOrderSlotCount);
        SetText(courierListText, coolingCount > 0 ? "обновляется: " + coolingCount : "все площадки готовы");
        RefreshCourierOrderButtons();
        SetText(courierDetailText, BuildCourierDetailText(selectedSlot, storage));
        SetText(courierStatusText, courierStatusMessage);

        bool canSend = selectedSlot != null && meta.CanSendCourierOrder(selectedCourierSlotIndex, out _);
        bool canCancel = selectedSlot != null && selectedSlot.HasActiveOrder;
        SetInteractable(courierSendButton, canSend);
        SetInteractable(courierCancelButton, canCancel);
        SetInteractable(courierCancelAllButton, activeCount > 0);
        SetText(courierSendButtonText, "Отправить");
        SetText(courierCancelButtonText, "Отменить");
        SetText(courierCancelAllButtonText, "Отменить все");

        courierWindowDirty = false;
        nextCourierWindowRefreshTime = Time.unscaledTime + CourierWindowRefreshSeconds;
    }

    private void RefreshCourierOrderButtons()
    {
        if (courierOrderButtons == null || meta == null)
        {
            return;
        }

        for (int i = 0; i < courierOrderButtons.Length; i++)
        {
            CourierOrderSlotState slot = meta.GetCourierOrderSlot(i);
            bool selected = i == selectedCourierSlotIndex;
            bool visible = i < MetaGameState.CourierOrderSlotCount;
            SetActiveIfNotNull(courierOrderButtons[i], visible);
            if (!visible)
            {
                continue;
            }

            string label;
            int remaining = meta.GetCourierOrderCooldownRemainingSeconds(i);
            if (slot == null)
            {
                label = (i + 1) + ". Площадка недоступна";
            }
            else if (remaining > 0)
            {
                label = (i + 1) + ". Обновление через " + FormatSecondsShort(remaining);
            }
            else
            {
                bool canSend = meta.CanSendCourierOrder(i, out _);
                label = (i + 1) + ". "
                    + (string.IsNullOrWhiteSpace(slot.clientName) ? "Курьер" : slot.clientName)
                    + "  |  " + slot.inputs.Count + " рес.  |  "
                    + (canSend ? "готов" : "не хватает");
            }

            SetText(courierOrderButtonTexts[i], label);
            SetInteractable(courierOrderButtons[i], true);
            StyleCourierButton(courierOrderButtons[i], selected);
        }
    }

    private string BuildCourierDetailText(CourierOrderSlotState slot, PortStorageState storage)
    {
        if (slot == null)
        {
            return "Площадка не найдена.";
        }

        int remaining = meta != null ? meta.GetCourierOrderCooldownRemainingSeconds(slot.slotIndex) : 0;
        if (remaining > 0)
        {
            return "Площадка " + (slot.slotIndex + 1)
                + "\n\nЗаявка отменена. Новый курьерский кораблик прибудет через "
                + FormatSecondsShort(remaining)
                + ".";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Площадка " + (slot.slotIndex + 1) + ": " + (string.IsNullOrWhiteSpace(slot.clientName) ? "Курьер Ветровых Домов" : slot.clientName));
        builder.AppendLine("Фракция: Ветровые Дома");
        builder.AppendLine();
        builder.AppendLine("Нужно загрузить:");
        if (slot.inputs == null || slot.inputs.Count == 0)
        {
            builder.AppendLine("- заявка обновляется");
        }
        else
        {
            for (int i = 0; i < slot.inputs.Count; i++)
            {
                CascadeItemAmount input = slot.inputs[i];
                if (input == null) continue;
                int available = storage != null ? storage.GetResourceAmount(input.itemId) : 0;
                builder.Append("- ")
                    .Append(GetItemDisplayName(input.itemId))
                    .Append(" x").Append(input.amount)
                    .Append("  |  склад: ").Append(available);
                if (available < input.amount)
                {
                    builder.Append("  |  не хватает ").Append(input.amount - available);
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine("Награда:");
        builder.AppendLine("- Фрахт x" + slot.freightReward);
        builder.AppendLine("- Очки освоения x" + slot.designExperienceReward);
        builder.AppendLine();
        builder.AppendLine("Заявка ждёт без дедлайна. Отмена поставит площадку на 15 минут обновления.");
        return builder.ToString().TrimEnd();
    }

    private void SelectCourierSlot(int index)
    {
        selectedCourierSlotIndex = Mathf.Clamp(index, 0, MetaGameState.CourierOrderSlotCount - 1);
        courierWindowDirty = true;
        RefreshCourierWindow();
    }

    private bool HandleCourierSend()
    {
        if (meta == null)
        {
            courierStatusMessage = "Мета-состояние не найдено.";
            RefreshCourierWindow();
            return false;
        }

        bool sent = meta.TrySendCourierOrder(selectedCourierSlotIndex, out courierStatusMessage);
        courierWindowDirty = true;
        RefreshCourierWindow();
        return sent;
    }

    private bool HandleCourierCancel()
    {
        if (meta == null)
        {
            courierStatusMessage = "Мета-состояние не найдено.";
            RefreshCourierWindow();
            return false;
        }

        bool cancelled = meta.TryCancelCourierOrder(selectedCourierSlotIndex, out courierStatusMessage);
        courierWindowDirty = true;
        RefreshCourierWindow();
        return cancelled;
    }

    private bool HandleCourierCancelAll()
    {
        if (meta == null)
        {
            courierStatusMessage = "Мета-состояние не найдено.";
            RefreshCourierWindow();
            return false;
        }

        bool cancelled = meta.TryCancelAllCourierOrders(out courierStatusMessage);
        courierWindowDirty = true;
        RefreshCourierWindow();
        return cancelled;
    }

    private void RefreshExpansionRegionsIfDue()
    {
        if (city == null || !city.gameObject.activeSelf)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!expansionRegionsDirty && now < nextExpansionRegionRefreshTime)
        {
            return;
        }

        RefreshExpansionRegions();
    }

    private void RefreshExpansionRegions()
    {
        if (city == null)
        {
            return;
        }

        PlayerProgress progress = meta != null ? meta.progress : null;
        if (meta != null)
        {
            meta.EnsureProgressInitialized();
            progress = meta.progress;
        }

        int anchorLevel = GetBuildingLevelValue("anchor_house");
        long nowTicks = DateTime.UtcNow.Ticks;
        List<BaseIslandExpansionRegionView> views = new List<BaseIslandExpansionRegionView>(ExpansionDefinitions.Length + 1)
        {
            new BaseIslandExpansionRegionView(
                CoreIslandRegionId,
                CoreIslandOriginX,
                CoreIslandOriginY,
                CoreIslandWidth,
                CoreIslandHeight,
                1,
                0,
                0f,
                0f,
                BaseIslandExpansionVisualState.Open),
            new BaseIslandExpansionRegionView(
                FrontIslandLipRegionId,
                FrontIslandLipOriginX,
                FrontIslandLipOriginY,
                FrontIslandLipWidth,
                FrontIslandLipHeight,
                1,
                0,
                0f,
                0f,
                BaseIslandExpansionVisualState.Open)
        };

        for (int i = 0; i < ExpansionDefinitions.Length; i++)
        {
            BaseIslandExpansionDefinition definition = ExpansionDefinitions[i];
            BaseIslandExpansionVisualState visualState = GetExpansionVisualState(definition, progress, anchorLevel, nowTicks, out float remainingSeconds);
            views.Add(new BaseIslandExpansionRegionView(
                definition.id,
                definition.originX,
                definition.originY,
                definition.width,
                definition.height,
                definition.requiredAnchorLevel,
                definition.clearCostFreight,
                definition.clearSeconds,
                remainingSeconds,
                visualState));
        }

        city.ApplyExpansionRegions(views);
        city.ApplyExternalDockSlots(BuildExternalDockSlots(views));
        expansionRegionsDirty = false;
        nextExpansionRegionRefreshTime = Time.unscaledTime + ExpansionRegionRefreshSeconds;
    }

    private List<BaseIslandExternalDockSlotView> BuildExternalDockSlots(IReadOnlyList<BaseIslandExpansionRegionView> regions)
    {
        int width = city != null ? Mathf.Max(1, city.GridWidthForTests) : 40;
        int height = city != null ? Mathf.Max(1, city.GridHeightForTests) : 40;
        bool[,] islandMask = new bool[width, height];

        if (regions != null)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                BaseIslandExpansionRegionView region = regions[i];
                if (region.state != BaseIslandExpansionVisualState.Open)
                {
                    continue;
                }

                FillIslandMask(islandMask, region.originX, region.originY, region.width, region.height);
            }
        }

        List<ExternalDockSlotCandidate> candidates = new List<ExternalDockSlotCandidate>();
        AddHorizontalExternalDockSlots(islandMask, true, candidates);
        AddHorizontalExternalDockSlots(islandMask, false, candidates);
        AddVerticalExternalDockSlots(islandMask, true, candidates);
        AddVerticalExternalDockSlots(islandMask, false, candidates);
        return SelectExternalDockSlots(candidates);
    }

    private static void FillIslandMask(bool[,] mask, int originX, int originY, int width, int height)
    {
        int gridWidth = mask.GetLength(0);
        int gridHeight = mask.GetLength(1);
        int minX = Mathf.Max(0, originX);
        int minY = Mathf.Max(0, originY);
        int maxX = Mathf.Min(gridWidth, originX + width);
        int maxY = Mathf.Min(gridHeight, originY + height);
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                mask[x, y] = true;
            }
        }
    }

    private static void AddHorizontalExternalDockSlots(
        bool[,] islandMask,
        bool south,
        List<ExternalDockSlotCandidate> candidates)
    {
        int width = islandMask.GetLength(0);
        int height = islandMask.GetLength(1);
        BaseIslandExternalDockOrientation orientation = south
            ? BaseIslandExternalDockOrientation.South
            : BaseIslandExternalDockOrientation.North;

        for (int y = 0; y < height; y++)
        {
            int runStart = -1;
            for (int x = 0; x <= width; x++)
            {
                bool exposed = x < width
                    && islandMask[x, y]
                    && (south
                        ? y == 0
                        : y == height - 1);

                if (exposed && runStart < 0)
                {
                    runStart = x;
                }

                if ((!exposed || x == width) && runStart >= 0)
                {
                    AddHorizontalExternalDockRun(candidates, orientation, y, runStart, x - runStart);
                    runStart = -1;
                }
            }
        }
    }

    private static void AddHorizontalExternalDockRun(
        List<ExternalDockSlotCandidate> candidates,
        BaseIslandExternalDockOrientation orientation,
        int y,
        int runStart,
        int runLength)
    {
        int dockCount = runLength / ExternalDockContactWidthCells;
        for (int i = 0; i < dockCount; i++)
        {
            int slotStartX = runStart + i * ExternalDockContactWidthCells;
            float centerX = slotStartX + (ExternalDockContactWidthCells - 1) * 0.5f;
            float centerY = orientation == BaseIslandExternalDockOrientation.South
                ? y - 0.5f - ExternalDockProtrusionLengthCells * 0.5f
                : y + 0.5f + ExternalDockProtrusionLengthCells * 0.5f;
            string idPrefix = orientation == BaseIslandExternalDockOrientation.South ? "south" : "north";
            AddExternalDockCandidate(
                candidates,
                new BaseIslandExternalDockSlotView(
                    idPrefix + "_" + y + "_" + slotStartX,
                    centerX,
                    centerY,
                    ExternalDockContactWidthCells,
                    ExternalDockProtrusionLengthCells,
                    orientation),
                runLength);
        }
    }

    private static void AddVerticalExternalDockSlots(
        bool[,] islandMask,
        bool west,
        List<ExternalDockSlotCandidate> candidates)
    {
        int width = islandMask.GetLength(0);
        int height = islandMask.GetLength(1);
        BaseIslandExternalDockOrientation orientation = west
            ? BaseIslandExternalDockOrientation.West
            : BaseIslandExternalDockOrientation.East;

        for (int x = 0; x < width; x++)
        {
            int runStart = -1;
            for (int y = 0; y <= height; y++)
            {
                bool exposed = y < height
                    && islandMask[x, y]
                    && (west
                        ? x == 0
                        : x == width - 1);

                if (exposed && runStart < 0)
                {
                    runStart = y;
                }

                if ((!exposed || y == height) && runStart >= 0)
                {
                    AddVerticalExternalDockRun(candidates, orientation, x, runStart, y - runStart);
                    runStart = -1;
                }
            }
        }
    }

    private static void AddVerticalExternalDockRun(
        List<ExternalDockSlotCandidate> candidates,
        BaseIslandExternalDockOrientation orientation,
        int x,
        int runStart,
        int runLength)
    {
        int dockCount = runLength / ExternalDockContactWidthCells;
        for (int i = 0; i < dockCount; i++)
        {
            int slotStartY = runStart + i * ExternalDockContactWidthCells;
            float centerX = orientation == BaseIslandExternalDockOrientation.West
                ? x - 0.5f - ExternalDockProtrusionLengthCells * 0.5f
                : x + 0.5f + ExternalDockProtrusionLengthCells * 0.5f;
            float centerY = slotStartY + (ExternalDockContactWidthCells - 1) * 0.5f;
            string idPrefix = orientation == BaseIslandExternalDockOrientation.West ? "west" : "east";
            AddExternalDockCandidate(
                candidates,
                new BaseIslandExternalDockSlotView(
                    idPrefix + "_" + x + "_" + slotStartY,
                    centerX,
                    centerY,
                    ExternalDockContactWidthCells,
                    ExternalDockProtrusionLengthCells,
                    orientation),
                runLength);
        }
    }

    private static void AddExternalDockCandidate(
        List<ExternalDockSlotCandidate> candidates,
        BaseIslandExternalDockSlotView slot,
        int shorelineRunLength)
    {
        bool northSouth = slot.orientation == BaseIslandExternalDockOrientation.North
            || slot.orientation == BaseIslandExternalDockOrientation.South;
        float halfX = (northSouth ? slot.contactWidth : slot.protrusionLength) * 0.5f;
        float halfY = (northSouth ? slot.protrusionLength : slot.contactWidth) * 0.5f;
        candidates.Add(new ExternalDockSlotCandidate(
            slot,
            shorelineRunLength,
            slot.centerX - halfX,
            slot.centerX + halfX,
            slot.centerY - halfY,
            slot.centerY + halfY));
    }

    private static List<BaseIslandExternalDockSlotView> SelectExternalDockSlots(List<ExternalDockSlotCandidate> candidates)
    {
        candidates.Sort((a, b) =>
        {
            int runCompare = b.shorelineRunLength.CompareTo(a.shorelineRunLength);
            if (runCompare != 0)
            {
                return runCompare;
            }

            int orientationCompare = GetExternalDockOrientationPriority(a.slot.orientation)
                .CompareTo(GetExternalDockOrientationPriority(b.slot.orientation));
            return orientationCompare != 0 ? orientationCompare : string.CompareOrdinal(a.slot.dockId, b.slot.dockId);
        });

        List<ExternalDockSlotCandidate> selected = new List<ExternalDockSlotCandidate>(ExpectedExternalDockSlotCount);
        for (int i = 0; i < candidates.Count; i++)
        {
            ExternalDockSlotCandidate candidate = candidates[i];
            selected.Add(candidate);
        }

        selected.Sort((a, b) => string.CompareOrdinal(a.slot.dockId, b.slot.dockId));
        List<BaseIslandExternalDockSlotView> slots = new List<BaseIslandExternalDockSlotView>(selected.Count);
        for (int i = 0; i < selected.Count; i++)
        {
            slots.Add(selected[i].slot);
        }

        return slots;
    }

    private static bool HasPerpendicularDockConflict(ExternalDockSlotCandidate candidate, List<ExternalDockSlotCandidate> selected)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            ExternalDockSlotCandidate other = selected[i];
            if (candidate.slot.orientation == other.slot.orientation)
            {
                continue;
            }

            if (candidate.minX <= other.maxX
                && candidate.maxX >= other.minX
                && candidate.minY <= other.maxY
                && candidate.maxY >= other.minY)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetExternalDockOrientationPriority(BaseIslandExternalDockOrientation orientation)
    {
        switch (orientation)
        {
            case BaseIslandExternalDockOrientation.South:
                return 0;
            case BaseIslandExternalDockOrientation.North:
                return 1;
            case BaseIslandExternalDockOrientation.West:
                return 2;
            default:
                return 3;
        }
    }

    private BaseIslandExpansionVisualState GetExpansionVisualState(
        BaseIslandExpansionDefinition definition,
        PlayerProgress progress,
        int anchorLevel,
        long nowTicks,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        BaseIslandExpansionRegionState state = progress != null
            ? progress.GetBaseIslandExpansionRegionState(definition.id, true)
            : null;

        if (state != null && state.status == BaseIslandExpansionRegionStatus.Clearing)
        {
            if (state.clearingCompleteUtcTicks > 0 && nowTicks >= state.clearingCompleteUtcTicks)
            {
                state.status = BaseIslandExpansionRegionStatus.Open;
                state.clearingCompleteUtcTicks = 0;
            }
            else
            {
                remainingSeconds = state.clearingCompleteUtcTicks > nowTicks
                    ? (float)TimeSpan.FromTicks(state.clearingCompleteUtcTicks - nowTicks).TotalSeconds
                    : definition.clearSeconds;
                return BaseIslandExpansionVisualState.Clearing;
            }
        }

        if (state != null && state.status == BaseIslandExpansionRegionStatus.Open)
        {
            return BaseIslandExpansionVisualState.Open;
        }

        return anchorLevel >= definition.requiredAnchorLevel
            ? BaseIslandExpansionVisualState.Debris
            : BaseIslandExpansionVisualState.Fog;
    }

    private void HandleExpansionRegionClicked(string regionId)
    {
        if (IsWindowOpenForTests || string.IsNullOrWhiteSpace(regionId))
        {
            return;
        }

        OpenExpansionWindow(regionId);
    }

    private bool OpenExpansionWindow(string regionId)
    {
        if (!TryGetExpansionDefinition(regionId, out _))
        {
            return false;
        }

        EnsureWindow();
        if (windowCanvas == null || windowPanel == null || windowFadeButton == null)
        {
            return false;
        }

        ConfigureExpansionWindow();
        openExpansionRegionId = regionId;
        expansionWindowOpen = true;
        expansionStatusMessage = "";
        openBuildingId = "";
        openActionIndex = -1;
        processingWindowOpen = false;
        processingStatusMessage = "";

        SetActiveIfNotNull(processingPanel, false);
        SetActiveIfNotNull(windowBodyText, true);
        SetActiveIfNotNull(windowPrimaryButton, true);
        SetActiveIfNotNull(buildingSelectionBackdropButton, false);
        SetActiveIfNotNull(buildingActionPanel, false);
        SetActiveIfNotNull(buildingInfoPanel, false);
        SetActiveIfNotNull(windowFadeButton, true);
        windowPanel.gameObject.SetActive(true);
        RefreshExpansionWindow();
        return true;
    }

    private void ConfigureExpansionWindow()
    {
        if (windowPanel != null)
        {
            windowPanel.sizeDelta = new Vector2(640f, 430f);
        }

        SetRect(windowTitleText, new Vector2(28f, -24f), new Vector2(584f, 48f));
        SetRect(windowBodyText, new Vector2(34f, -90f), new Vector2(572f, 230f));
        SetRect(windowExitButton, new Vector2(82f, -350f), new Vector2(200f, 52f));
        SetRect(windowPrimaryButton, new Vector2(358f, -350f), new Vector2(200f, 52f));
    }

    private void RefreshExpansionWindowIfDue()
    {
        if (!expansionWindowOpen)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!expansionWindowDirty && now < nextExpansionWindowRefreshTime)
        {
            return;
        }

        RefreshExpansionWindow();
    }

    private void RefreshExpansionWindow()
    {
        if (!expansionWindowOpen || string.IsNullOrWhiteSpace(openExpansionRegionId))
        {
            return;
        }

        if (!TryGetExpansionDefinition(openExpansionRegionId, out BaseIslandExpansionDefinition definition))
        {
            CloseWindow();
            return;
        }

        RefreshExpansionRegionsIfDue();
        PlayerProgress progress = meta != null ? meta.progress : null;
        int anchorLevel = GetBuildingLevelValue("anchor_house");
        long nowTicks = DateTime.UtcNow.Ticks;
        BaseIslandExpansionVisualState visualState = GetExpansionVisualState(definition, progress, anchorLevel, nowTicks, out float remainingSeconds);
        PortStorageState storage = meta != null ? meta.GetCapitalStorageState() : null;
        int availableFreight = storage != null ? storage.GetResourceAmount(FreightItemId) : 0;

        SetText(windowTitleText, "Расчистить завалы");
        SetText(windowExitButtonText, "Отмена");
        SetText(windowPrimaryButtonText, visualState == BaseIslandExpansionVisualState.Clearing ? "Идёт" : "Расчистить");
        SetText(windowBodyText, BuildExpansionWindowText(definition, visualState, anchorLevel, availableFreight, remainingSeconds));

        bool canClear = visualState == BaseIslandExpansionVisualState.Debris
            && availableFreight >= definition.clearCostFreight;
        SetInteractable(windowPrimaryButton, canClear);
        expansionWindowDirty = false;
        nextExpansionWindowRefreshTime = Time.unscaledTime + ExpansionWindowRefreshSeconds;
    }

    private string BuildExpansionWindowText(
        BaseIslandExpansionDefinition definition,
        BaseIslandExpansionVisualState visualState,
        int anchorLevel,
        int availableFreight,
        float remainingSeconds)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Участок: " + definition.width + "x" + definition.height);

        if (visualState == BaseIslandExpansionVisualState.Fog)
        {
            builder.AppendLine("Состояние: туман.");
            builder.AppendLine("Нужно: Якорный дом " + definition.requiredAnchorLevel + " уровня.");
            builder.AppendLine("Сейчас: " + anchorLevel + " уровень.");
            return builder.ToString();
        }

        if (visualState == BaseIslandExpansionVisualState.Clearing)
        {
            builder.AppendLine("Состояние: идут работы.");
            builder.AppendLine("Осталось: " + FormatLongTime(remainingSeconds) + ".");
            return builder.ToString();
        }

        if (visualState == BaseIslandExpansionVisualState.Open)
        {
            builder.AppendLine("Состояние: участок уже свободен.");
            return builder.ToString();
        }

        builder.AppendLine("Состояние: завалы.");
        builder.AppendLine("Стоимость: " + definition.clearCostFreight + " фрахта.");
        builder.AppendLine("У вас: " + availableFreight + " фрахта.");
        builder.AppendLine("Время работ: " + FormatLongTime(definition.clearSeconds) + ".");
        if (!string.IsNullOrWhiteSpace(expansionStatusMessage))
        {
            builder.AppendLine();
            builder.Append(expansionStatusMessage);
        }

        return builder.ToString();
    }

    private void HandleExpansionClearButton()
    {
        if (!expansionWindowOpen || string.IsNullOrWhiteSpace(openExpansionRegionId))
        {
            return;
        }

        TryBeginExpansionClearing(openExpansionRegionId, out expansionStatusMessage);
        RefreshExpansionRegions();
        RefreshExpansionWindow();
    }

    private bool TryBeginExpansionClearing(string regionId, out string message)
    {
        message = "";
        if (meta == null || meta.progress == null)
        {
            message = "Нет прогресса города.";
            return false;
        }

        if (!TryGetExpansionDefinition(regionId, out BaseIslandExpansionDefinition definition))
        {
            message = "Участок не найден.";
            return false;
        }

        meta.EnsureProgressInitialized();
        PlayerProgress progress = meta.progress;
        int anchorLevel = GetBuildingLevelValue("anchor_house");
        BaseIslandExpansionVisualState visualState = GetExpansionVisualState(definition, progress, anchorLevel, DateTime.UtcNow.Ticks, out _);
        if (visualState == BaseIslandExpansionVisualState.Fog)
        {
            message = "Сначала надо развеять туман.";
            return false;
        }

        if (visualState == BaseIslandExpansionVisualState.Open)
        {
            message = "Участок уже свободен.";
            return false;
        }

        if (visualState == BaseIslandExpansionVisualState.Clearing)
        {
            message = "Работы уже идут.";
            return false;
        }

        PortStorageState storage = meta.GetCapitalStorageState();
        if (storage == null || storage.GetResourceAmount(FreightItemId) < definition.clearCostFreight)
        {
            message = "Не хватает фрахта.";
            return false;
        }

        if (!storage.TrySpendResource(FreightItemId, definition.clearCostFreight))
        {
            message = "Не удалось списать фрахт.";
            return false;
        }

        BaseIslandExpansionRegionState state = progress.GetBaseIslandExpansionRegionState(definition.id, true);
        state.status = BaseIslandExpansionRegionStatus.Clearing;
        state.clearingCompleteUtcTicks = DateTime.UtcNow.AddSeconds(definition.clearSeconds).Ticks;
        message = "Расчистка началась.";
        return true;
    }

    private static bool TryGetExpansionDefinition(string regionId, out BaseIslandExpansionDefinition definition)
    {
        string normalizedId = string.IsNullOrWhiteSpace(regionId) ? "" : regionId.Trim();
        for (int i = 0; i < ExpansionDefinitions.Length; i++)
        {
            if (ExpansionDefinitions[i].id == normalizedId)
            {
                definition = ExpansionDefinitions[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static string FormatLongTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes > 0
            ? minutes + " мин " + remainder.ToString("00") + " сек"
            : remainder + " сек";
    }

    private static string FormatSecondsShort(int seconds)
    {
        int totalSeconds = Mathf.Max(0, seconds);
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes > 0
            ? minutes + " мин " + remainder.ToString("00") + " сек"
            : remainder + " сек";
    }

    private string BuildProcessingStorageText(PortStorageState storage, List<string> inputs)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Склад");
        if (storage == null || inputs == null || inputs.Count == 0)
        {
            builder.Append("Нет подходящего сырья.");
            return builder.ToString();
        }

        int totalStored = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            totalStored += storage.GetResourceAmount(inputs[i]);
        }

        if (totalStored <= 0)
        {
            builder.Append("Подходящего сырья нет.");
        }
        else
        {
            builder.Append("Нажмите строку ресурса, чтобы переложить 1 ед. в бункер.");
        }

        return builder.ToString();
    }

    private string BuildProcessingBunkerText(BaseProcessingFacilityState facility)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Загружено в здании");
        if (facility == null || facility.bunker == null || facility.bunker.Count == 0)
        {
            builder.Append("Бункер пуст.");
            return builder.ToString();
        }

        for (int i = 0; i < facility.bunker.Count; i++)
        {
            ResourceStack stack = facility.bunker[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            builder.AppendLine(GetItemDisplayName(stack.resourceId) + ": " + stack.amount);
        }

        return builder.ToString();
    }

    private string BuildProcessingOutputsText(BaseProcessingFacilityState facility, List<string> outputs)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Выходы");
        if (outputs == null || outputs.Count == 0)
        {
            builder.Append("Выходы не настроены.");
            return builder.ToString();
        }

        for (int i = 0; i < outputs.Count; i++)
        {
            string itemId = outputs[i];
            BaseProcessingOutputBufferState buffer = facility != null ? facility.GetOutputBuffer(itemId, false) : null;
            int ready = buffer != null ? buffer.readyAmount : 0;
            float fraction = buffer != null ? buffer.fractionalAmount : 0f;
            builder.AppendLine(GetItemDisplayName(itemId) + ": " + ready + " шт.  " + (fraction * 100f).ToString("0.#") + "%");
        }

        return builder.ToString();
    }

    private void RefreshProcessingInputButtons(PortStorageState storage, BaseProcessingFacilityState facility, List<string> inputs)
    {
        if (processingInputButtons == null || processingInputButtonTexts == null)
        {
            return;
        }

        for (int i = 0; i < processingInputButtons.Length; i++)
        {
            bool visible = inputs != null && i < inputs.Count;
            SetActiveIfNotNull(processingInputButtons[i], visible);
            if (!visible) continue;

            string itemId = inputs[i];
            int amount = storage != null ? storage.GetResourceAmount(itemId) : 0;
            bool canLoad = amount > 0 && facility != null && facility.BunkerFreeUnits > 0;
            SetText(processingInputButtonTexts[i], "+1  " + ShortenItemName(GetItemDisplayName(itemId), 24) + "    " + amount);
            processingInputButtonTexts[i].color = canLoad
                ? new Color(0.94f, 0.90f, 0.73f, 1f)
                : new Color(0.48f, 0.56f, 0.56f, 1f);
            SetInteractable(processingInputButtons[i], canLoad);
        }
    }

    private bool HandleProcessingInputButton(int index)
    {
        if (!processingWindowOpen || meta == null)
        {
            return false;
        }

        List<string> inputs = meta.GetBaseProcessingInputItemIds(openProcessingBranch);
        if (inputs == null || index < 0 || index >= inputs.Count)
        {
            return false;
        }

        bool result = meta.TryLoadBaseProcessingInput(openBuildingId, openProcessingBranch, GetBuildingLevelValue(openBuildingId), inputs[index], 1, out processingStatusMessage);
        RefreshProcessingWindow();
        return result;
    }

    private bool HandleProcessingLoadAll()
    {
        if (!processingWindowOpen || meta == null)
        {
            return false;
        }

        bool result = meta.TryLoadAllBaseProcessingInputs(openBuildingId, openProcessingBranch, GetBuildingLevelValue(openBuildingId), out processingStatusMessage);
        processingCollectBubbleDataDirty = true;
        RefreshProcessingWindow();
        return result;
    }

    private bool HandleProcessingClear()
    {
        if (!processingWindowOpen || meta == null)
        {
            return false;
        }

        bool result = meta.TryClearBaseProcessingBunker(openBuildingId, openProcessingBranch, GetBuildingLevelValue(openBuildingId), out processingStatusMessage);
        processingCollectBubbleDataDirty = true;
        RefreshProcessingWindow();
        return result;
    }

    private bool HandleProcessingCollect()
    {
        if (!processingWindowOpen || meta == null)
        {
            return false;
        }

        bool result = meta.TryCollectBaseProcessingOutputs(openBuildingId, openProcessingBranch, GetBuildingLevelValue(openBuildingId), out processingStatusMessage);
        processingCollectBubbleDataDirty = true;
        RefreshProcessingWindow();
        return result;
    }

    private bool HandleProcessingCollectBubble(string buildingId)
    {
        if (meta == null || string.IsNullOrWhiteSpace(buildingId) || !TryGetProcessingBranch(buildingId, out BaseProcessingBranch branch))
        {
            return false;
        }

        bool result = meta.TryCollectBaseProcessingOutputs(buildingId, branch, GetBuildingLevelValue(buildingId), out processingStatusMessage);
        processingCollectBubbleDataDirty = true;
        if (processingWindowOpen && openBuildingId == buildingId)
        {
            RefreshProcessingWindow();
        }

        RefreshBuildingOverlay(true);
        return result;
    }

    private void HandleProcessingCollectBubblePointerInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (sceneCamera == null
            || IsWindowOpenForTests
            || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            return;
        }

        BaseIslandProcessingCollectBubbleHandle handle = hit.collider != null
            ? hit.collider.GetComponentInParent<BaseIslandProcessingCollectBubbleHandle>()
            : null;
        if (handle == null || handle.owner != this || string.IsNullOrWhiteSpace(handle.buildingId))
        {
            return;
        }

        HandleProcessingCollectBubble(handle.buildingId);
#endif
    }

    private bool HasAnyStoredInput(PortStorageState storage, List<string> inputs)
    {
        if (storage == null || inputs == null) return false;
        for (int i = 0; i < inputs.Count; i++)
        {
            if (storage.GetResourceAmount(inputs[i]) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnyReadyOutput(BaseProcessingFacilityState facility)
    {
        if (facility == null || facility.outputBuffers == null) return false;
        for (int i = 0; i < facility.outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = facility.outputBuffers[i];
            if (buffer != null && buffer.readyAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void SetHudBuildingFocus(bool active)
    {
        if (gameplayHud == null)
        {
            gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
        }

        if (gameplayHud != null)
        {
            gameplayHud.SetBaseBuildingFocusActive(active);
        }
    }

    private void RefreshBuildingOverlay(bool forceCollectBubbleRefresh = false)
    {
        if (windowCanvas == null || overlayRoot == null)
        {
            return;
        }

        bool cityVisible = city != null && city.gameObject.activeSelf;
        SetGameObjectActiveIfChanged(windowCanvas.gameObject, cityVisible);
        if (!cityVisible)
        {
            SetActiveIfNotNull(processingCollectBubbleRoot, false);
            HideAllProcessingCollectBubbles();
            return;
        }

        bool windowOpen = IsWindowOpenForTests;
        bool selectionOpen = !string.IsNullOrWhiteSpace(selectedBuildingId) && !windowOpen;
        RefreshProcessingCollectBubbles(cityVisible, windowOpen, selectionOpen, forceCollectBubbleRefresh);
        if (!selectionOpen && !windowOpen)
        {
            if (!string.IsNullOrWhiteSpace(forcedHoverBuildingIdForTests))
            {
                hoveredBuildingId = forcedHoverBuildingIdForTests;
            }
            else
            {
                hoveredBuildingId = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()
                    ? ""
                    : city != null ? city.HoveredBuildingKeyForRuntime : "";
            }
        }

        string labelBuildingId = selectionOpen
            ? selectedBuildingId
            : hoveredBuildingId;
        bool showLabel = !windowOpen
            && !string.IsNullOrWhiteSpace(labelBuildingId)
            && city != null
            && city.HasBuildingKeyForTests(labelBuildingId);

        if (showLabel && TryGetOverlayPosition(labelBuildingId, out Vector2 labelPosition))
        {
            Vector2 infoPosition = labelPosition;
            if (!selectionOpen && IsProcessingCollectBubbleVisibleInternal(labelBuildingId))
            {
                infoPosition += new Vector2(0f, CollectBubbleHeight + 16f);
            }

            SetAnchoredPositionIfChanged(buildingInfoPanel, ClampOverlayPosition(infoPosition, buildingInfoPanel.sizeDelta));
            SetText(buildingInfoNameText, GetBuildingDisplayName(labelBuildingId));
            SetText(buildingInfoLevelText, GetBuildingLevelText(labelBuildingId));
            SetActiveIfNotNull(buildingInfoPanel, true);

            if (selectionOpen)
            {
                SetAnchoredPositionIfChanged(buildingActionPanel, ClampOverlayPosition(labelPosition + new Vector2(0f, -124f), buildingActionPanel.sizeDelta));
            }
        }
        else
        {
            SetActiveIfNotNull(buildingInfoPanel, false);
        }

        SetActiveIfNotNull(buildingSelectionBackdropButton, selectionOpen);
        SetActiveIfNotNull(buildingActionPanel, selectionOpen);
        if (buildingActionButtons != null)
        {
            for (int i = 0; i < buildingActionButtons.Length; i++)
            {
                if (buildingActionButtonTexts != null && i < buildingActionButtonTexts.Length)
                {
                    SetText(buildingActionButtonTexts[i], GetBuildingActionLabel(selectedBuildingId, i));
                }

                SetActiveIfNotNull(buildingActionButtons[i], selectionOpen);
            }
        }
    }

    private void RefreshProcessingCollectBubbles(bool cityVisible, bool windowOpen, bool selectionOpen, bool forceDataRefresh)
    {
        bool catalogOpen = buildingCatalogPanel != null && buildingCatalogPanel.gameObject.activeSelf;
        bool canShow = cityVisible
            && !windowOpen
            && !selectionOpen
            && !catalogOpen
            && processingCollectBubbleRoot != null
            && city != null
            && CanInteractWithBase();
        SetActiveIfNotNull(processingCollectBubbleRoot, canShow);
        if (!canShow)
        {
            HideAllProcessingCollectBubbles();
            return;
        }

        float now = Time.unscaledTime;
        bool refreshData = forceDataRefresh
            || processingCollectBubbleDataDirty
            || now >= nextProcessingCollectBubbleDataRefreshTime;
        if (!refreshData)
        {
            RefreshVisibleProcessingCollectBubbleBillboards();
            return;
        }

        visibleProcessingCollectBubbleKeys.Clear();
        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
        {
            BaseBuildingDefinition definition = definitions[definitionIndex];
            if (!TryGetProcessingBranch(definition.id, out BaseProcessingBranch branch))
            {
                continue;
            }

            int maxCount = Mathf.Max(1, GetBuildingMaxCount(definition));
            for (int instanceIndex = 1; instanceIndex <= maxCount; instanceIndex++)
            {
                string buildingKey = GetInstanceKey(definition, instanceIndex);
                if (!city.HasBuildingKeyForTests(buildingKey)
                    || !TryGetProcessingCollectBubbleData(buildingKey, branch, out string itemId, out int totalReady)
                    || !TryGetProcessingCollectBubbleWorldAnchor(buildingKey, out Vector3 worldAnchor))
                {
                    continue;
                }

                ProcessingCollectBubbleView bubble = EnsureProcessingCollectBubble(buildingKey);
                bubble.Set(itemId, totalReady);
                bubble.UpdateBillboard(sceneCamera, worldAnchor, CollectBubbleHeight);
                bubble.SetVisible(true);
                visibleProcessingCollectBubbleKeys.Add(buildingKey);
            }
        }

        foreach (KeyValuePair<string, ProcessingCollectBubbleView> pair in processingCollectBubbles)
        {
            if (pair.Value != null && !visibleProcessingCollectBubbleKeys.Contains(pair.Key))
            {
                pair.Value.SetVisible(false);
            }
        }

        processingCollectBubbleDataDirty = false;
        nextProcessingCollectBubbleDataRefreshTime = now + CollectBubbleDataRefreshSeconds;
    }

    private void RefreshVisibleProcessingCollectBubbleBillboards()
    {
        foreach (KeyValuePair<string, ProcessingCollectBubbleView> pair in processingCollectBubbles)
        {
            ProcessingCollectBubbleView bubble = pair.Value;
            if (bubble == null || !bubble.IsVisible)
            {
                continue;
            }

            if (TryGetProcessingCollectBubbleWorldAnchor(pair.Key, out Vector3 worldAnchor))
            {
                bubble.UpdateBillboard(sceneCamera, worldAnchor, CollectBubbleHeight);
            }
            else
            {
                bubble.SetVisible(false);
            }
        }
    }

    private bool TryGetProcessingCollectBubbleWorldAnchor(string buildingId, out Vector3 worldPosition)
    {
        worldPosition = default;
        return city != null
            && sceneCamera != null
            && city.TryGetBuildingWorldAnchorForRuntime(buildingId, out worldPosition);
    }

    private bool TryGetProcessingCollectBubbleData(string buildingId, BaseProcessingBranch branch, out string itemId, out int totalReady)
    {
        itemId = "";
        totalReady = 0;
        BaseProcessingFacilityState facility = FindExistingProcessingFacility(buildingId);
        if (facility == null || facility.branch != branch || facility.outputBuffers == null)
        {
            return false;
        }

        int topAmount = 0;
        for (int i = 0; i < facility.outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = facility.outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId) || buffer.readyAmount <= 0)
            {
                continue;
            }

            int ready = Mathf.Max(0, buffer.readyAmount);
            totalReady += ready;
            if (ready > topAmount)
            {
                topAmount = ready;
                itemId = buffer.itemId.Trim();
            }
        }

        return totalReady > 0 && !string.IsNullOrWhiteSpace(itemId);
    }

    private BaseProcessingFacilityState FindExistingProcessingFacility(string buildingId)
    {
        if (meta == null
            || meta.progress == null
            || meta.progress.baseIndustry == null
            || meta.progress.baseIndustry.processingFacilities == null
            || string.IsNullOrWhiteSpace(buildingId))
        {
            return null;
        }

        string normalizedId = buildingId.Trim();
        List<BaseProcessingFacilityState> facilities = meta.progress.baseIndustry.processingFacilities;
        for (int i = 0; i < facilities.Count; i++)
        {
            BaseProcessingFacilityState facility = facilities[i];
            if (facility != null && facility.facilityId == normalizedId)
            {
                return facility;
            }
        }

        return null;
    }

    private ProcessingCollectBubbleView EnsureProcessingCollectBubble(string buildingId)
    {
        if (processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView existing) && existing != null)
        {
            return existing;
        }

        ProcessingCollectBubbleView created = CreateProcessingCollectBubble(buildingId);
        processingCollectBubbles[buildingId] = created;
        return created;
    }

    private ProcessingCollectBubbleView CreateProcessingCollectBubble(string buildingId)
    {
        GameObject bubbleObject = new GameObject("Collect Bubble " + buildingId);
        bubbleObject.transform.SetParent(processingCollectBubbleRoot, false);

        SpriteRenderer background = bubbleObject.AddComponent<SpriteRenderer>();
        background.sprite = GetCollectBubbleSprite();
        background.color = new Color(0.96f, 0.91f, 0.78f, 0.96f);
        background.sortingOrder = 5000;

        BoxCollider collider = bubbleObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.78f, 0f);
        collider.size = new Vector3(1.22f, 1.48f, 0.08f);

        BaseIslandProcessingCollectBubbleHandle handle = bubbleObject.AddComponent<BaseIslandProcessingCollectBubbleHandle>();
        handle.owner = this;
        handle.buildingId = buildingId;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(bubbleObject.transform, false);
        iconObject.transform.localPosition = new Vector3(0f, 1.02f, -0.01f);
        SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
        icon.sortingOrder = 5001;

        GameObject fallbackObject = new GameObject("Fallback");
        fallbackObject.transform.SetParent(bubbleObject.transform, false);
        fallbackObject.transform.localPosition = new Vector3(0f, 1.02f, -0.02f);
        TextMesh fallback = fallbackObject.AddComponent<TextMesh>();
        ConfigureBubbleText(fallback, "?", 0.34f);

        GameObject amountObject = new GameObject("Amount");
        amountObject.transform.SetParent(bubbleObject.transform, false);
        amountObject.transform.localPosition = new Vector3(0f, 0.58f, -0.02f);
        TextMesh amount = amountObject.AddComponent<TextMesh>();
        ConfigureBubbleText(amount, "", 0.22f);

        ProcessingCollectBubbleView view = new ProcessingCollectBubbleView(bubbleObject.transform, background, icon, fallback, amount);
        view.SetVisible(false);
        return view;
    }

    private static void ConfigureBubbleText(TextMesh text, string value, float characterSize)
    {
        if (text == null)
        {
            return;
        }

        text.text = value ?? "";
        text.font = GetDefaultFont();
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.10f, 0.11f, 0.10f, 1f);

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 5002;
        }
    }

    private bool IsProcessingCollectBubbleVisibleInternal(string buildingId)
    {
        return processingCollectBubbles.TryGetValue(buildingId, out ProcessingCollectBubbleView bubble)
            && bubble != null
            && bubble.IsVisible;
    }

    private void HideAllProcessingCollectBubbles()
    {
        foreach (ProcessingCollectBubbleView bubble in processingCollectBubbles.Values)
        {
            if (bubble != null)
            {
                bubble.SetVisible(false);
            }
        }
    }

    private static Sprite GetCollectBubbleSprite()
    {
        if (cachedCollectBubbleSprite != null)
        {
            return cachedCollectBubbleSprite;
        }

        const int width = 128;
        const int height = 160;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Runtime_ProcessingCollectBubble";
        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2(width * 0.5f, height * 0.61f);
        float radius = 49f;
        Vector2 point = new Vector2(width * 0.5f, 8f);
        Vector2 left = new Vector2(width * 0.36f, height * 0.30f);
        Vector2 right = new Vector2(width * 0.64f, height * 0.30f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 pixel = new Vector2(x + 0.5f, y + 0.5f);
                float circleDistance = Vector2.Distance(pixel, center);
                bool inCircle = circleDistance <= radius;
                bool inPoint = IsPointInTriangle(pixel, left, right, point);
                texture.SetPixel(x, y, inCircle || inPoint ? fill : transparent);
            }
        }

        texture.Apply(false, true);
        cachedCollectBubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
        cachedCollectBubbleSprite.name = "Sprite_ProcessingCollectBubble";
        return cachedCollectBubbleSprite;
    }

    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        float alpha = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
        float beta = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
        float gamma = 1f - alpha - beta;
        return alpha >= 0f && beta >= 0f && gamma >= 0f;
    }

    private static string FormatCompactAmount(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount >= 1000000)
        {
            return TrimCompactNumber(amount / 1000000f) + "M";
        }

        if (amount >= 1000)
        {
            return TrimCompactNumber(amount / 1000f) + "K";
        }

        return amount.ToString();
    }

    private static string TrimCompactNumber(float value)
    {
        string formatted = value >= 100f ? value.ToString("0") : value >= 10f ? value.ToString("0.#") : value.ToString("0.##");
        return formatted.TrimEnd('0').TrimEnd('.');
    }

    private bool TryGetOverlayPosition(string buildingId, out Vector2 anchoredPosition)
    {
        anchoredPosition = default;
        if (city == null || overlayRoot == null || !city.TryGetBuildingScreenAnchorForRuntime(buildingId, out Vector2 screenPosition))
        {
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screenPosition, null, out anchoredPosition);
    }

    private Vector2 ClampOverlayPosition(Vector2 anchoredPosition, Vector2 size)
    {
        Rect rect = overlayRoot != null ? overlayRoot.rect : new Rect(-960f, -540f, 1920f, 1080f);
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, rect.xMin + halfWidth + 16f, rect.xMax - halfWidth - 16f);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, rect.yMin + halfHeight + 16f, rect.yMax - halfHeight - 16f);
        return anchoredPosition;
    }

    private static void SetActiveIfNotNull(Button button, bool active)
    {
        if (button != null)
        {
            SetGameObjectActiveIfChanged(button.gameObject, active);
        }
    }

    private static void SetActiveIfNotNull(Component component, bool active)
    {
        if (component != null)
        {
            SetGameObjectActiveIfChanged(component.gameObject, active);
        }
    }

    private static void SetGameObjectActiveIfChanged(GameObject gameObject, bool active)
    {
        if (gameObject != null && gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null)
        {
            selectable.interactable = interactable;
        }
    }

    private static RectTransform CreateRect(string name, Transform parent, RectTransformSpec spec)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
        return rect;
    }

    private static Text CreateText(Transform parent, string value, int size, Vector2 anchoredPosition, Vector2 rectSize, TextAnchor anchor)
    {
        RectTransform rect = CreateRect("Text", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = rectSize
        });
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(0.88f, 0.92f, 0.90f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value ?? "";
        text.raycastTarget = false;
        return text;
    }

    private static Text CreateText(Transform parent, string value, int size, Vector2 anchoredPosition, Vector2 rectSize, TextAnchor anchor, Color color)
    {
        Text text = CreateText(parent, value, size, anchoredPosition, rectSize, anchor);
        text.color = color;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        return CreateButton(parent, label, anchoredPosition, size, action, out _);
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, out Text labelText)
    {
        RectTransform rect = CreateRect(label + " Button", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.18f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        labelText = CreateText(rect, label, 18, Vector2.zero, size, TextAnchor.MiddleCenter);
        return button;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            value ??= "";
            if (text.text != value)
            {
                text.text = value;
            }
        }
    }

    private static void SetAnchoredPositionIfChanged(RectTransform rect, Vector2 anchoredPosition, float epsilon = 0.5f)
    {
        if (rect == null)
        {
            return;
        }

        if ((rect.anchoredPosition - anchoredPosition).sqrMagnitude > epsilon * epsilon)
        {
            rect.anchoredPosition = anchoredPosition;
        }
    }

    private static void SetRect(Component component, Vector2 anchoredPosition, Vector2 size)
    {
        if (component == null)
        {
            return;
        }

        RectTransform rect = component.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont != null)
        {
            return cachedDefaultFont;
        }

        cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return cachedDefaultFont;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            ConfigureEventSystemInput(existing.gameObject);
            return;
        }

        GameObject eventSystemObject = new GameObject(EventSystemName, typeof(EventSystem));
        ConfigureEventSystemInput(eventSystemObject);
    }

    private static void ConfigureEventSystemInput(GameObject eventSystemObject)
    {
#if ENABLE_INPUT_SYSTEM
        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    private static bool TryGetDefinition(string buildingId, out BaseBuildingDefinition definition)
    {
        string normalizedId = GetDefinitionId(buildingId);
        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == normalizedId)
            {
                definition = definitions[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static BaseBuildingDefinition[] BuildingDefinitions
    {
        get
        {
            if (cachedBuildingDefinitions == null || cachedBuildingDefinitions.Length == 0)
            {
                cachedBuildingDefinitions = LoadBuildingDefinitions();
            }

            return cachedBuildingDefinitions;
        }
    }

    private static BaseBuildingDefinition[] LoadBuildingDefinitions()
    {
        TextAsset asset = Resources.Load<TextAsset>(BuildingCatalogResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            return FallbackBuildingDefinitions;
        }

        List<BaseBuildingDefinition> definitions = new List<BaseBuildingDefinition>();
        string[] lines = asset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] columns = SplitCsvLine(lines[i]);
            if (columns.Length < 13)
            {
                continue;
            }

            string id = columns[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            definitions.Add(new BaseBuildingDefinition(
                id,
                columns[1].Trim(),
                columns[2].Trim(),
                ParseInt(columns[3], 2),
                ParseInt(columns[4], 2),
                ParseInt(columns[5], 1),
                ParseInt(columns[6], 1),
                ParseInt(columns[7], 0),
                ParseInt(columns[8], -1),
                ParseInt(columns[9], -1),
                ParseColor(columns[10], Color.gray),
                columns[11].Trim(),
                columns[12].Trim()));
        }

        return definitions.Count > 0 ? definitions.ToArray() : FallbackBuildingDefinitions;
    }

    private static string[] SplitCsvLine(string line)
    {
        List<string> columns = new List<string>();
        if (line == null)
        {
            return columns.ToArray();
        }

        bool quoted = false;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (c == ',' && !quoted)
            {
                columns.Add(builder.ToString());
                builder.Length = 0;
                continue;
            }

            builder.Append(c);
        }

        columns.Add(builder.ToString());
        return columns.ToArray();
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return ColorUtility.TryParseHtmlString(value.Trim(), out Color color)
            ? color
            : fallback;
    }

    private static string GetDefinitionId(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId))
        {
            return "";
        }

        int marker = buildingId.IndexOf("__", StringComparison.Ordinal);
        return marker >= 0 ? buildingId.Substring(0, marker) : buildingId.Trim();
    }

    private static string GetInstanceKey(BaseBuildingDefinition definition, int instanceIndex)
    {
        return instanceIndex <= 1
            ? definition.id
            : definition.id + "__" + instanceIndex.ToString();
    }

    private int CountPlacedBuildings(BaseBuildingDefinition definition)
    {
        if (city == null)
        {
            return 0;
        }

        return IsDockDefinition(definition)
            ? city.CountExternalDockInstancesForRuntime(definition.id)
            : city.CountBuildingInstancesForRuntime(definition.id);
    }

    private int GetBuildingMaxCount(BaseBuildingDefinition definition)
    {
        return IsDockDefinition(definition)
            ? Mathf.Min(Mathf.Max(0, definition.maxCount), city != null ? city.ExternalDockSlotCountForTests : ExpectedExternalDockSlotCount)
            : definition.maxCount;
    }

    private string GetNextAvailableInstanceKey(BaseBuildingDefinition definition)
    {
        if (city == null)
        {
            return GetInstanceKey(definition, 1);
        }

        int maxCount = GetBuildingMaxCount(definition);
        for (int i = 1; i <= maxCount; i++)
        {
            string key = GetInstanceKey(definition, i);
            bool occupied = IsDockDefinition(definition)
                ? city.HasExternalDockKeyForTests(key)
                : city.HasBuildingKeyForTests(key);
            if (!occupied)
            {
                return key;
            }
        }

        return GetInstanceKey(definition, maxCount + 1);
    }

    private static bool IsDockDefinition(BaseBuildingDefinition definition)
    {
        return string.Equals(definition.category, "dock", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCascadeRecipeBuilding(string buildingId)
    {
        if (!TryGetDefinition(buildingId, out BaseBuildingDefinition definition))
        {
            return false;
        }

        return string.Equals(definition.category, "industry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.category, "production", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCourierServiceBuilding(string buildingId)
    {
        return string.Equals(GetDefinitionId(buildingId), CourierServiceBuildingId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnowledgeBuilding(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            && string.Equals(definition.category, "knowledge", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBuildingActionLabel(string buildingId, int actionIndex)
    {
        if (actionIndex == 1 && IsKnowledgeBuilding(buildingId))
        {
            return "Знания";
        }

        if (actionIndex >= 0 && actionIndex < BuildingActionLabels.Length)
        {
            return BuildingActionLabels[actionIndex];
        }

        return "";
    }

    private static int GetInitialBuildingCountForTests()
    {
        BaseBuildingDefinition[] definitions = BuildingDefinitions;
        int count = 0;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (!IsDockDefinition(definitions[i]))
            {
                count += Mathf.Max(0, definitions[i].initialCount);
            }
        }

        return count;
    }

    private static bool IsUsableAuthoredMaterial(Material material)
    {
        if (material == null || material.shader == null || material.shader.name.Contains("Error"))
        {
            return false;
        }

        Texture texture = material.mainTexture;
        if (texture == null && material.HasProperty("_BaseMap"))
        {
            texture = material.GetTexture("_BaseMap");
        }

        return texture != null;
    }

    private static string GetBuildingDisplayName(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            ? definition.displayName
            : buildingId;
    }

    private static string GetBuildingLevelText(string buildingId)
    {
        int level = TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            ? definition.level
            : 1;
        return level.ToString() + " уровень";
    }

    private static int GetBuildingLevelValue(string buildingId)
    {
        return TryGetDefinition(buildingId, out BaseBuildingDefinition definition)
            ? definition.level
            : 1;
    }

    private static bool TryGetProcessingBranch(string buildingId, out BaseProcessingBranch branch)
    {
        switch (GetDefinitionId(buildingId))
        {
            case "refinery":
                branch = BaseProcessingBranch.Ore;
                return true;
            case "gas_separator":
                branch = BaseProcessingBranch.Gas;
                return true;
            case "scrapyard":
                branch = BaseProcessingBranch.AutomatonDismantling;
                return true;
            case "butchery":
                branch = BaseProcessingBranch.LeviathanProcessing;
                return true;
            default:
                branch = BaseProcessingBranch.Ore;
                return false;
        }
    }

    private string GetItemDisplayName(string itemId)
    {
        SessionConfigDatabase config = meta != null ? meta.SessionConfig : null;
        return config != null ? config.GetItemNameRu(itemId) : itemId ?? "";
    }

    private static string ShortenItemName(string value, int maxLength = 12)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        value = value.Trim();
        maxLength = Mathf.Max(2, maxLength);
        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + ".";
    }

    private sealed class ProcessingCollectBubbleView
    {
        private readonly Transform root;
        private readonly SpriteRenderer backgroundRenderer;
        private readonly SpriteRenderer iconRenderer;
        private readonly TextMesh fallbackText;
        private readonly TextMesh amountText;
        public string itemId = "";
        public int totalReadyAmount;

        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public ProcessingCollectBubbleView(Transform root, SpriteRenderer backgroundRenderer, SpriteRenderer iconRenderer, TextMesh fallbackText, TextMesh amountText)
        {
            this.root = root;
            this.backgroundRenderer = backgroundRenderer;
            this.iconRenderer = iconRenderer;
            this.fallbackText = fallbackText;
            this.amountText = amountText;
        }

        public void Set(string nextItemId, int nextTotalReadyAmount)
        {
            string normalizedItemId = string.IsNullOrWhiteSpace(nextItemId) ? "" : nextItemId.Trim();
            int normalizedAmount = Mathf.Max(0, nextTotalReadyAmount);
            if (itemId == normalizedItemId && totalReadyAmount == normalizedAmount)
            {
                return;
            }

            itemId = normalizedItemId;
            totalReadyAmount = normalizedAmount;

            Sprite sprite = WildWindResourceIconCatalog.LoadSprite(itemId);
            if (iconRenderer != null)
            {
                if (iconRenderer.sprite != sprite)
                {
                    iconRenderer.sprite = sprite;
                }

                bool iconEnabled = sprite != null;
                if (iconRenderer.enabled != iconEnabled)
                {
                    iconRenderer.enabled = iconEnabled;
                }

                if (sprite != null)
                {
                    Vector2 size = sprite.bounds.size;
                    float maxSide = Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
                    float scale = 0.46f / maxSide;
                    iconRenderer.transform.localScale = new Vector3(scale, scale, scale);
                }
            }

            if (fallbackText != null)
            {
                SetGameObjectActiveIfChanged(fallbackText.gameObject, sprite == null);
            }

            if (amountText != null)
            {
                string formattedAmount = FormatCompactAmount(totalReadyAmount);
                if (amountText.text != formattedAmount)
                {
                    amountText.text = formattedAmount;
                }
            }
        }

        public void UpdateBillboard(Camera camera, Vector3 worldAnchor, float desiredPixelHeight)
        {
            if (root == null || camera == null)
            {
                return;
            }

            root.position = worldAnchor;
            root.rotation = camera.transform.rotation;

            float desiredWorldHeight;
            if (camera.orthographic)
            {
                desiredWorldHeight = camera.orthographicSize * 2f * Mathf.Max(1f, desiredPixelHeight) / Mathf.Max(1f, Screen.height);
            }
            else
            {
                desiredWorldHeight = Vector3.Distance(camera.transform.position, worldAnchor) * 0.08f;
            }

            float spriteHeight = backgroundRenderer != null && backgroundRenderer.sprite != null
                ? Mathf.Max(0.001f, backgroundRenderer.sprite.bounds.size.y)
                : 1.6f;
            float scale = Mathf.Clamp(desiredWorldHeight / spriteHeight, 0.25f, 12f);
            root.localScale = new Vector3(scale, scale, scale);
        }

        public void SetVisible(bool visible)
        {
            SetGameObjectActiveIfChanged(root != null ? root.gameObject : null, visible);
        }
    }

    private readonly struct BaseBuildingDefinition
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string prefabName;
        public readonly int width;
        public readonly int height;
        public readonly int level;
        public readonly int maxCount;
        public readonly int initialCount;
        public readonly int originX;
        public readonly int originY;
        public readonly Color color;
        public readonly string visualKind;
        public readonly string category;
        public string PrefabResourcePath => string.IsNullOrWhiteSpace(prefabName) ? "" : BuildingPrefabResourceRoot + prefabName;

        public BaseBuildingDefinition(
            string id,
            string displayName,
            string prefabName,
            int width,
            int height,
            int level,
            int maxCount,
            int initialCount,
            int originX,
            int originY,
            Color color,
            string visualKind,
            string category)
        {
            this.id = id;
            this.displayName = displayName;
            this.prefabName = prefabName;
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            this.level = Mathf.Max(1, level);
            this.maxCount = Mathf.Max(1, maxCount);
            this.initialCount = Mathf.Clamp(initialCount, 0, this.maxCount);
            this.originX = originX;
            this.originY = originY;
            this.color = color;
            this.visualKind = visualKind ?? "";
            this.category = category ?? "";
        }
    }

    private readonly struct CascadeCatalogRecipeView
    {
        public readonly string category;
        public readonly string title;
        public readonly string description;
        public readonly int requiredLevel;
        public readonly float baseSeconds;
        public readonly CascadeProductionOrderDefinition order;

        public CascadeCatalogRecipeView(
            string category,
            string title,
            string description,
            int requiredLevel,
            float baseSeconds,
            CascadeProductionOrderDefinition order)
        {
            this.category = string.IsNullOrWhiteSpace(category) ? "Все" : category;
            this.title = string.IsNullOrWhiteSpace(title) ? "Рецепт" : title;
            this.description = description ?? "";
            this.requiredLevel = Mathf.Max(1, requiredLevel);
            this.baseSeconds = Mathf.Max(1f, baseSeconds);
            this.order = order;
        }
    }

    private readonly struct CascadeBottleneckView
    {
        public readonly CascadeProductionType type;
        public readonly float minutes;

        public CascadeBottleneckView(CascadeProductionType type, float minutes)
        {
            this.type = type;
            this.minutes = Mathf.Max(0f, minutes);
        }
    }

    private readonly struct ExternalDockSlotCandidate
    {
        public readonly BaseIslandExternalDockSlotView slot;
        public readonly int shorelineRunLength;
        public readonly float minX;
        public readonly float maxX;
        public readonly float minY;
        public readonly float maxY;

        public ExternalDockSlotCandidate(
            BaseIslandExternalDockSlotView slot,
            int shorelineRunLength,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            this.slot = slot;
            this.shorelineRunLength = shorelineRunLength;
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }
    }

    private readonly struct BaseIslandExpansionDefinition
    {
        public readonly string id;
        public readonly int originX;
        public readonly int originY;
        public readonly int width;
        public readonly int height;
        public readonly int requiredAnchorLevel;
        public readonly int clearCostFreight;
        public readonly float clearSeconds;

        public BaseIslandExpansionDefinition(
            string id,
            int originX,
            int originY,
            int width,
            int height,
            int requiredAnchorLevel,
            int clearCostFreight,
            float clearSeconds)
        {
            this.id = id ?? "";
            this.originX = Mathf.Max(0, originX);
            this.originY = Mathf.Max(0, originY);
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            this.requiredAnchorLevel = Mathf.Max(1, requiredAnchorLevel);
            this.clearCostFreight = Mathf.Max(0, clearCostFreight);
            this.clearSeconds = Mathf.Max(0f, clearSeconds);
        }
    }

    private struct RectTransformSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }
}

public sealed class BaseIslandProcessingCollectBubbleHandle : MonoBehaviour
{
    public WildWindBaseIslandView owner;
    public string buildingId = "";
}
