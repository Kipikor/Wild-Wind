using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionController))]
public class MissionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Связи");
        LocalizedInspector.Property(serializedObject, "mission", "Миссия", "Описание миссии: маршрут, награды и настройки выполнения.");
        LocalizedInspector.Property(serializedObject, "targetShip", "Корабль игрока", "Корабль, который участвует в миссии.");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Состояние мета-игры", "Компонент, который управляет режимами стыковки, вылета и сохранениями.");
        LocalizedInspector.Property(serializedObject, "startPoint", "Точка старта", "Точка сцены, куда ставится корабль при начале миссии.");
        LocalizedInspector.Property(serializedObject, "destinationPoint", "Точка назначения", "Точка сцены, где миссия считается доставленной.");

        LocalizedInspector.Section("Запуск");
        LocalizedInspector.Property(serializedObject, "startMissionOnPlay", "Запускать миссию при старте", "Если включено, миссия начнется автоматически при запуске сцены.");
        LocalizedInspector.Property(serializedObject, "placeShipAtStart", "Ставить корабль на старт", "Если включено, корабль переносится в точку старта при начале миссии.");

        if (Application.isPlaying)
        {
            MissionController controller = (MissionController)target;
            LocalizedInspector.Section("Состояние во время игры");
            EditorGUILayout.LabelField("Миссия активна", controller.IsActive ? "Да" : "Нет");
            EditorGUILayout.LabelField("Миссия завершена", controller.IsCompleted ? "Да" : "Нет");
            EditorGUILayout.LabelField("Расстояние до цели", controller.DistanceToDestination.ToString("0.0") + " м");
        }

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(DockingPort))]
public class DockingPortEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Связи");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Состояние мета-игры", "Компонент, который принимает решение о завершении вылета и сохранении.");
        LocalizedInspector.Property(serializedObject, "targetShip", "Корабль игрока", "Корабль, который должен войти в радиус стыковки.");

        LocalizedInspector.Section("Стыковка");
        LocalizedInspector.Property(serializedObject, "dockId", "Идентификатор дока", "Технический идентификатор точки стыковки. Используется в сохранениях.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Человеческое название дока для интерфейса.");
        LocalizedInspector.Property(serializedObject, "kind", "Тип дока", "Остров или корабль. Используется в прогрессе и интерфейсе.");
        LocalizedInspector.Property(serializedObject, "dockingRadius", "Радиус стыковки", "Если корабль в полете входит в этот радиус, точка может завершить вылет.");
        LocalizedInspector.Property(serializedObject, "canEndSession", "Можно завершить сессию", "Если включено, стыковка в этой точке может завершить текущий вылет.");
        LocalizedInspector.Property(serializedObject, "autoDockWhenInRange", "Автостыковка в радиусе", "Если включено, корабль автоматически перейдет в режим стыковки при входе в радиус.");
        LocalizedInspector.Property(serializedObject, "requireLeaveBeforeRedocking", "Ждать выхода из текущего дока", "Если корабль начал вылет из этого дока, автостыковка сработает только после того, как корабль сначала покинет радиус.");
        LocalizedInspector.Property(serializedObject, "snapPoint", "Точка привязки", "Позиция, куда будет поставлен корабль после стыковки. Если пусто, используется позиция этого объекта.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(MetaGameState))]
public class MetaGameStateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Связи");
        LocalizedInspector.Property(serializedObject, "catalog", "Каталог кораблей", "Каталог корпусов и модулей для сборки корабля.");
        LocalizedInspector.Property(serializedObject, "techTree", "Древо техники", "Данные исследований и покупок техники.");
        LocalizedInspector.Property(serializedObject, "shipLoader", "Загрузчик корабля", "Компонент, который создает корпус-префаб и применяет текущую сборку.");
        LocalizedInspector.Property(serializedObject, "missionController", "Контроллер миссии", "Активная миссия сцены. Может быть пусто, если миссий в сцене нет.");
        LocalizedInspector.Property(serializedObject, "logisticsFleet", "Логистический флот", "Контроллер виртуальных грузовиков и маршрутов.");
        LocalizedInspector.Property(serializedObject, "gasCloudManager", "Газовые облака", "Создает и синхронизирует исчерпаемые облака из CSV-конфигов.");
        LocalizedInspector.Property(serializedObject, "gasHarvesterFleet", "Газовые автопилоты", "Виртуальные сборщики газа, привязанные к островам.");
        LocalizedInspector.Property(serializedObject, "startingMoney", "Стартовые деньги", "Сколько денег получает новая игра.");
        LocalizedInspector.DrawPlayerProgress(serializedObject.FindProperty("progress"), "Прогресс игрока");

        LocalizedInspector.Section("Сессия");
        LocalizedInspector.Property(serializedObject, "startingMode", "Стартовый режим", "Режим новой игры: стыковка или вылет.");
        LocalizedInspector.Property(serializedObject, "startingDockId", "Стартовый док", "Идентификатор дока, с которого начинается новая игра.");
        LocalizedInspector.Property(serializedObject, "startingDockKind", "Тип стартового дока", "Тип стартовой стыковки: остров или корабль.");
        LocalizedInspector.Property(serializedObject, "autoSaveOnDock", "Автосохранение при стыковке", "Ручное сохранение разрешено только в режиме стыковки. При выходе из игры текущий вылет сохраняется отдельно.");
        LocalizedInspector.Property(serializedObject, "loadSavedGameOnAwake", "Загружать сохранение при старте", "Если прошлый запуск был прерван в полете, загрузка вернет корабль в сохраненную точку вылета.");
        LocalizedInspector.Property(serializedObject, "saveFileName", "Имя файла сохранения", "Имя JSON-файла в папке постоянных данных Unity.");

        LocalizedInspector.Section("Стартовые ресурсы");
        LocalizedInspector.Property(serializedObject, "startingOre", "Стартовая руда", "Сколько руды получает новая игра.");
        LocalizedInspector.Property(serializedObject, "startingIron", "Стартовое железо", "Сколько железа получает новая игра.");

        LocalizedInspector.Section("Процессы реального времени");
        LocalizedInspector.Property(serializedObject, "processRealTimeWhilePlaying", "Обновлять процессы во время игры", "Если включено, добыча, производство, миссии и магазин обновляются во время Play Mode.");
        LocalizedInspector.Property(serializedObject, "idleMiningIntervalSeconds", "Интервал добычи руды, сек", "Как часто пассивная добыча добавляет руду.");
        LocalizedInspector.Property(serializedObject, "idleMiningOrePerCycle", "Руды за цикл добычи", "Сколько руды добавляется за один цикл пассивной добычи.");
        LocalizedInspector.Property(serializedObject, "ironSmeltingDurationSeconds", "Длительность плавки железа, сек", "Сколько реальных секунд длится переплавка руды в железо.");
        LocalizedInspector.Property(serializedObject, "ironSmeltingOreCost", "Цена плавки, руда", "Сколько руды тратится на один запуск плавки.");
        LocalizedInspector.Property(serializedObject, "ironSmeltingIronOutput", "Выход плавки, железо", "Сколько железа добавляется после завершения плавки.");
        LocalizedInspector.Property(serializedObject, "defaultTimedMissionDurationSeconds", "Длительность миссии по умолчанию, сек", "Запасная длительность для миссий, где не задана своя длительность.");
        LocalizedInspector.Property(serializedObject, "shopRefreshIntervalSeconds", "Интервал обновления магазина, сек", "Через этот интервал меняется зерно магазина. Ассортимент можно строить от этого числа.");

        LocalizedInspector.Section("Ускорение времени");
        LocalizedInspector.Property(serializedObject, "gameTimeScale", "Множитель времени", "Ускоряет мета-процессы, исследования, погрузку и виртуальную логистику.");
        LocalizedInspector.Property(serializedObject, "accelerateUnityTimeScale", "Ускорять физику Unity", "Если включено, полет игрока тоже ускоряется через Time.timeScale, но не выше лимита.");
        LocalizedInspector.Property(serializedObject, "maxUnityTimeScale", "Лимит физики Unity", "Безопасный потолок физического ускорения, чтобы Rigidbody и автопилот не получали слишком крупные скачки.");
        LocalizedInspector.Property(serializedObject, "maxAcceleratedProcessStepSeconds", "Шаг мета-времени, сек", "Ускоренное время нарезается на такие шаги перед обновлением производств, исследований и логистики.");
        LocalizedInspector.Property(serializedObject, "processOfflineProgressOnLoad", "Offline-прогресс", "Если включено, при загрузке сохранения симуляция догоняет время, прошедшее пока игра была выключена.");
        LocalizedInspector.Property(serializedObject, "maxOfflineCatchUpHours", "Лимит offline-догонки, часов", "Защита от слишком больших скачков системных часов. Для тестовой перемотки на 100 часов значение должно быть выше 100.");

        LocalizedInspector.Section("Конфиги мира");
        LocalizedInspector.Property(serializedObject, "worldConfigFolder", "Папка конфигов от Assets", "CSV-конфиги ресурсов, островов и производств. По умолчанию Data/Config.");
        LocalizedInspector.Property(serializedObject, "capitalIslandId", "Остров столицы", "Идентификатор острова-лаборатории из Island.csv. Новая игра стартует здесь.");
        LocalizedInspector.Property(serializedObject, "islandProductionEnabled", "Производство островов", "Если включено, склады островов обновляются по CSV-конфигам в реальном времени.");
        LocalizedInspector.Property(serializedObject, "spawnConfigIslandsOnPlay", "Создавать острова из конфигов", "Если включено, при запуске Play Mode острова из Island.csv появляются в сцене как DockingPort.");
        LocalizedInspector.Property(serializedObject, "configIslandVisualRadius", "Визуальный радиус острова", "Размер временной модели острова. Радиус стыковки берется из Island.csv.");

        LocalizedInspector.Section("Отладочный интерфейс стыковки");
        LocalizedInspector.Property(serializedObject, "showDockingDebugUI", "Показывать интерфейс", "Показывает временное окно управления мета-игрой во время Play Mode.");
        LocalizedInspector.Property(serializedObject, "debugUiWidth", "Ширина интерфейса", "Ширина временного отладочного окна в пикселях.");

        LocalizedInspector.Section("Аварии");
        LocalizedInspector.Property(serializedObject, "autoInstallCrashDetector", "Автоматически добавить детектор крушений", "Если включено, на корабль будет добавлен детектор крушений: при аварии текущий корабль и груз теряются, игрок возвращается в город.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShipLoader))]
public class ShipLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Загрузка корабля");
        LocalizedInspector.Property(serializedObject, "catalog", "Каталог кораблей", "Каталог, из которого выбираются корпуса и модули.");
        LocalizedInspector.Property(serializedObject, "targetShip", "Активный корабль", "Текущий корабль игрока. При новой сборке сюда автоматически попадет созданный корпус-префаб.");
        LocalizedInspector.Property(serializedObject, "spawnPoint", "Точка создания корпуса", "Позиция и поворот, где будет создан префаб корпуса. Если пусто, используется старый сценовый корабль или объект загрузчика.");
        LocalizedInspector.Property(serializedObject, "spawnedParent", "Родитель созданного корпуса", "Куда поместить созданный корпус в иерархии сцены. Можно оставить пустым.");
        LocalizedInspector.Property(serializedObject, "destroySpawnedShipOnRebuild", "Удалять старый корпус", "Если включено, при выборе другого корпуса старый созданный корпус будет удален.");
        LocalizedInspector.Property(serializedObject, "disableSceneShipWhenSpawning", "Отключать сценовый корабль", "Если в сцене уже был запасной корабль, он будет отключен после создания корпуса-префаба.");
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShipCrashDetector))]
public class ShipCrashDetectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Крушение");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Состояние мета-игры", "Компонент, который обработает потерю корабля и возврат в город.");
        LocalizedInspector.Property(serializedObject, "crashRelativeSpeed", "Скорость удара для крушения", "Если относительная скорость столкновения выше этого значения, вылет считается потерянным.");
        LocalizedInspector.Property(serializedObject, "crashBelowAltitude", "Высота крушения", "Если корабль опустится ниже этой высоты в вылете, корабль и груз потеряются, а игрок вернется в город.");
        LocalizedInspector.Property(serializedObject, "crashWhenBelowAltitude", "Крушение ниже высоты", "Если включено, высота ниже порога считается аварией.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(MissionDefinitionSO))]
public class MissionDefinitionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Основное");
        LocalizedInspector.Property(serializedObject, "missionId", "Идентификатор миссии", "Технический идентификатор миссии. Используется в сохранениях, поэтому после релиза миссии его лучше не менять.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название миссии для интерфейса.");
        LocalizedInspector.Property(serializedObject, "description", "Описание", "Текстовое описание миссии.");

        LocalizedInspector.Section("Маршрут");
        LocalizedInspector.Property(serializedObject, "startPosition", "Стартовая точка", "Запасная позиция старта, если в сцене не задан Transform старта.");
        LocalizedInspector.Property(serializedObject, "destinationPosition", "Точка назначения", "Запасная позиция назначения, если в сцене не задан Transform назначения.");
        LocalizedInspector.Property(serializedObject, "arrivalRadius", "Радиус прибытия", "Расстояние до точки назначения, на котором миссия считается доставленной.");

        LocalizedInspector.Section("Стыковка");
        LocalizedInspector.Property(serializedObject, "destinationDockId", "Идентификатор дока назначения", "Идентификатор стыковки, куда игрок попадает после успешного завершения миссии.");
        LocalizedInspector.Property(serializedObject, "destinationDockKind", "Тип дока назначения", "Тип стыковки после завершения миссии.");

        LocalizedInspector.Section("Награда");
        LocalizedInspector.Property(serializedObject, "rewardMoney", "Деньги", "Сколько денег получает игрок после завершения миссии.");
        LocalizedInspector.Property(serializedObject, "rewardExperience", "Опыт корабля", "Сколько опыта получает выбранный корабль.");

        LocalizedInspector.Section("Реальное время");
        LocalizedInspector.Property(serializedObject, "canRunAsTimedMission", "Можно выполнять без вылета", "Если включено, миссию можно отправить как процесс в реальном времени из интерфейса стыковки.");
        LocalizedInspector.Property(serializedObject, "realTimeDurationSeconds", "Длительность, сек", "Сколько реальных секунд длится выполнение миссии без вылета.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShipCatalogSO))]
public class ShipCatalogSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Каталог");
        LocalizedInspector.Property(serializedObject, "starterHullId", "Стартовый корпус", "Идентификатор корпуса, который выбирается у новой игры, если в сохранении еще нет сборки корабля.");
        LocalizedInspector.DrawObjectList(serializedObject.FindProperty("parts"), "Детали корабля", "Корпуса и модули, которые можно исследовать, купить и ставить в сборку корабля.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShipPartDefinitionSO))]
public class ShipPartDefinitionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Основное");
        LocalizedInspector.Property(serializedObject, "partId", "Идентификатор детали", "Технический идентификатор корпуса или модуля. Используется в сохранении, древе техники и сборке корабля.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название детали для интерфейса стыковки и списков сборки.");
        LocalizedInspector.Property(serializedObject, "description", "Описание", "Короткое описание детали для будущего интерфейса дока, магазина или подсказок.");
        LocalizedInspector.Property(serializedObject, "completedTechId", "Технология доступа", "Если заполнено, деталь можно ставить после завершения этой технологии.");
        LocalizedInspector.Property(serializedObject, "kind", "Тип детали", "Корпус задает основу корабля и слоты. Модуль ставится в слот корпуса или другого модуля.");
        LocalizedInspector.Property(serializedObject, "prefab", "Префаб", "Для корпуса это основной префаб с физикой, коллайдерами и сокетами. Для модуля это визуальный префаб, который вставляется в сокет.");

        LocalizedInspector.Section("Слоты корпуса");
        LocalizedInspector.Property(serializedObject, "slots", "Слоты", "Слоты, которые дает корпус. Обязательность задается у каждого слота отдельно и не зависит от типа слота.");

        LocalizedInspector.Section("Совместимость модуля");
        LocalizedInspector.Property(serializedObject, "compatibleSlotTypeIds", "Подходит к типам слотов", "Типы слотов, в которые можно поставить этот модуль.");
        LocalizedInspector.Property(serializedObject, "grantedSlots", "Дополнительные слоты", "Слоты, которые появятся после установки этого модуля.");

        LocalizedInspector.Section("Характеристики");
        LocalizedInspector.Property(serializedObject, "statModifiers", "Изменения характеристик", "Перезапись задает точное значение. Изменение прибавляет или вычитает. Множитель умножает уже собранное значение.");

        serializedObject.ApplyModifiedProperties();
    }
}

public static class LocalizedInspector
{
    public static void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    public static void Property(SerializedObject serializedObject, string propertyName, string label, string tooltip = "")
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;
        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static void Property(SerializedProperty parent, string propertyName, string label, string tooltip = "")
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null) return;
        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    public static void DrawObjectList(SerializedProperty list, string label, string tooltip)
    {
        DrawList(list, new GUIContent(label, tooltip), (element, index) =>
        {
            EditorGUILayout.PropertyField(element, new GUIContent("Ассет", "Ссылка на ассет списка."), true);
        });
    }

    public static void DrawTechTreeNodeList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Узлы исследований и покупок в древе техники."), (element, index) =>
        {
            DrawTechTreeNodeProperties(element);
        });
    }

    public static void DrawTechTreeNodeProperties(SerializedProperty element)
    {
        if (element == null) return;

        Property(element, "nodeId", "Идентификатор узла", "Технический идентификатор узла. Используется в сохранениях и зависимостях.");
        Property(element, "displayName", "Название", "Название узла для интерфейса.");
        Property(element, "kind", "Тип узла", "Корпус, модуль или фундаментальное исследование.");
        Property(element, "tier", "Уровень техники", "Уровень прогрессии.");
        Property(element, "partDefinition", "Деталь корабля", "Корпус или модуль, который открывает этот узел. Для фундаментального исследования можно оставить пустым.");
        Property(element, "partId", "Идентификатор детали", "Запасной идентификатор корпуса или модуля, если ассет детали не указан.");
        Property(element, "isPremium", "Премиумная техника", "Пометка для будущей логики премиумной техники.");
        Property(element, "moduleKind", "Тип модуля", "Какой модуль открывает этот узел.");
        Property(element, "researchCostXp", "Стоимость исследования, опыт", "Сколько опыта нужно потратить на исследование.");
        Property(element, "purchasePrice", "Цена покупки, деньги", "Сколько денег нужно потратить после исследования. Для фундаментальных исследований поле игнорируется.");
        Property(element, "startsResearched", "Исследован с начала", "Если включено, новая игра считает узел уже исследованным.");
        Property(element, "startsPurchased", "Куплен с начала", "Если включено, новая игра считает узел уже купленным.");
        DrawStringList(element.FindPropertyRelative("prerequisiteNodeIds"), "Условия доступа", "Достаточно любого одного идентификатора из списка.");
        DrawStringList(element.FindPropertyRelative("experienceShipIds"), "Корпуса для траты опыта", "С каких корпусов можно тратить опыт на этот узел.");
        Property(element, "editorPosition", "Позиция в редакторе", "Позиция узла в визуальном редакторе древа.");
    }

    public static void DrawFlightTuning(SerializedProperty flight, string label)
    {
        if (!BeginFoldout(flight, label, "Настройки физики и автопилотов корабля.")) return;
        Property(flight, "baseMass", "Базовая масса", "Сухая масса корабля.");
        Property(flight, "targetTrimMass", "Масса триммирования", "Масса, под которую система подъема старается сбалансировать корабль.");
        Property(flight, "propellerMaxSpeedMS", "Макс. скорость винта", "Скорость, после которой винт больше не разгоняет корабль.");
        Property(flight, "propellerEfficiency", "КПД винта", "Эффективность передачи мощности в тягу.");
        Property(flight, "propellerMaxThrustKgf", "Макс. тяга винта", "Максимальная статическая тяга винта в кгс.");
        Property(flight, "maxStructuralVerticalSpeed", "Конструкционный лимит вертикальной скорости", "Вертикальная скорость, выше которой корабль считается перегруженным.");
        Property(flight, "maxAutoVerticalSpeed", "Лимит вертикальной скорости автопилота", "Максимальная вертикальная скорость, которую просит автопилот.");
        Property(flight, "airDensity", "Плотность воздуха", "Плотность воздуха для расчета сопротивления.");
        Property(flight, "dragCoefficient", "Коэффициент сопротивления", "Коэффициент формы корпуса.");
        Property(flight, "frontalArea", "Лобовая площадь", "Площадь передней проекции корпуса.");
        Property(flight, "sideResistance", "Боковое сопротивление", "Сопротивление боковому сносу.");
        Property(flight, "verticalAreaFactor", "Множитель вертикальной площади", "Во сколько раз вертикальная площадь больше лобовой.");
        Property(flight, "gyroTurnTorque", "Макс. усилие поворота (Н*м)", "Внутренний момент поворота корпуса в ньютон-метрах, будто внутри стоит гироскоп. Не тратит мощность двигателя.");
        Property(flight, "gyroTurnDamping", "Демпфирование поворота", "Насколько быстро гироскопическая система гасит лишнюю угловую скорость.");
        Property(flight, "maxAutoTurnRateDeg", "Макс. скорость поворота автопилота", "Скорость поворота, которую может запросить автопилот.");
        Property(flight, "maxStructuralTurnRateDeg", "Конструкционный лимит поворота", "Предел угловой скорости для безопасного полета.");
        Property(flight, "autoStabilizeAtStart", "Автостабилизация при старте", "Включать ли стабилизацию автоматически.");
        Property(flight, "altitudeHold", "Удержание высоты", "Стартовое состояние удержания высоты.");
        Property(flight, "altStiffness", "Жесткость высоты", "P-настройка автопилота высоты.");
        Property(flight, "altDamping", "Демпфирование высоты", "D-настройка автопилота высоты.");
        Property(flight, "altDriftTolerance", "Допуск дрейфа высоты", "Мертвая зона ошибки высоты.");
        Property(flight, "cruiseControl", "Круиз-контроль", "Стартовое состояние круиз-контроля.");
        Property(flight, "headingHold", "Удержание курса", "Стартовое состояние удержания курса.");
        Property(flight, "headingStiffness", "Жесткость курса", "P-настройка автопилота курса.");
        Property(flight, "headingDamping", "Демпфирование курса", "D-настройка автопилота курса.");
        Property(flight, "waypointRadius", "Радиус точки маршрута", "На каком расстоянии точка маршрута считается достигнутой.");
        Property(flight, "speedStiffness", "Жесткость скорости", "P-настройка круиз-контроля.");
        Property(flight, "speedDamping", "Демпфирование скорости", "D-настройка круиз-контроля.");
        EndFoldout();
    }

    public static void DrawEngineTuning(SerializedProperty engine, string label)
    {
        if (!BeginFoldout(engine, label, "Настройки двигателя.")) return;
        Property(engine, "engineName", "Название двигателя", "Название двигателя для интерфейса.");
        Property(engine, "powerKwAt100", "Мощность на 100%, кВт", "Сколько полезной работы двигатель выдает при ручке мощности 100%.");
        Property(engine, "startingPowerLever", "Стартовая ручка мощности", "Начальное положение ручки мощности: 1 означает 100%, 1.2 означает 120%.");
        Property(engine, "fuelId", "Топливо", "Идентификатор топлива для интерфейса и будущей экономики.");
        Property(engine, "fuelEnergyKwhPerKg", "Энергоемкость топлива", "Сколько кВт·ч энергии содержит один килограмм топлива.");
        Property(engine, "fuelStockKg", "Запас топлива, кг", "Стартовый запас топлива в килограммах.");
        Property(engine, "consumesFuel", "Тратить топливо", "Если включено, двигатель расходует топливо во время работы.");
        Property(engine, "efficiencyByPower", "Кривая КПД", "По горизонтали ручка мощности 0..1.2, по вертикали доля энергии топлива, превращенная в работу.");
        EndFoldout();
    }

    public static void DrawPlayerProgress(SerializedProperty progress, string label)
    {
        if (progress == null) return;

        progress.isExpanded = EditorGUILayout.Foldout(progress.isExpanded, new GUIContent(label, "Текущее сохраненное состояние игрока."), true);
        if (!progress.isExpanded) return;

        EditorGUI.indentLevel++;
        Property(progress, "money", "Деньги", "Текущие деньги игрока.");
        Property(progress, "selectedHullId", "Выбранный корпус", "Идентификатор корпуса, который выбран в сборке корабля.");
        DrawInstalledModuleList(progress.FindPropertyRelative("installedModules"), "Установленные модули");
        DrawStringList(progress.FindPropertyRelative("researchedNodeIds"), "Исследованные узлы", "Идентификаторы уже исследованных узлов древа техники.");
        DrawStringList(progress.FindPropertyRelative("purchasedNodeIds"), "Купленные узлы", "Идентификаторы купленных узлов древа техники.");
        Property(progress, "activeResearchTechnologyId", "Активная технология", "Технология, выбранная в лаборатории столицы.");
        EditorGUILayout.PropertyField(progress.FindPropertyRelative("technologyResearchProgress"), new GUIContent("Прогресс технологий", "Сколько циклов завершено и идет ли текущий цикл."), true);
        EditorGUILayout.PropertyField(progress.FindPropertyRelative("logisticsShips"), new GUIContent("Логистические корабли", "Состояния виртуальных грузовиков, их ETA, груз и ошибки."), true);
        DrawShipExperienceList(progress.FindPropertyRelative("shipExperience"), "Опыт корпусов");

        Property(progress, "currentMode", "Текущий режим", "Стыковка разрешает ручное сохранение. Выход из игры в вылете сохраняет позицию корабля.");
        Property(progress, "currentDockKind", "Тип текущего дока", "Где сейчас сохранен игрок: остров или корабль.");
        Property(progress, "currentDockId", "Текущий док", "Идентификатор последней стыковки.");
        Property(progress, "hasCurrentDockPosition", "Есть позиция текущего дока", "Если включено, сохранение хранит точную позицию дока.");
        Property(progress, "currentDockPosition", "Позиция текущего дока", "Позиция текущей стыковки или городского возврата после аварии.");
        Property(progress, "hasCurrentFlightPose", "Есть позиция вылета", "Если включено, сохранение хранит точку и поворот корабля в полете.");
        Property(progress, "currentFlightPosition", "Позиция вылета", "Позиция корабля, куда он будет возвращен после загрузки сохраненного вылета.");
        Property(progress, "currentFlightRotation", "Поворот вылета", "Поворот корабля, который будет восстановлен после загрузки сохраненного вылета.");
        Property(progress, "activeFlightMissionId", "Активная миссия в вылете", "Идентификатор миссии, ради которой начат текущий вылет.");
        Property(progress, "lastSavedUtcTicks", "Время последнего сохранения", "Техническое время UTC в тиках .NET.");
        Property(progress, "lastProcessUtcTicks", "Время последней обработки процессов", "Техническое время UTC, когда последний раз обновлялись процессы реального времени.");
        Property(progress, "nextShopRefreshUtcTicks", "Следующее обновление магазина", "Техническое время UTC, когда магазин должен обновить ассортимент.");
        Property(progress, "shopSeed", "Зерно магазина", "Число для генерации ассортимента магазина.");
        Property(progress, "receivedStartingInventory", "Стартовые ресурсы выданы", "Защищает от повторной выдачи стартовой руды и железа.");

        DrawResourceList(progress.FindPropertyRelative("inventory"), "Инвентарь");
        DrawResourceList(progress.FindPropertyRelative("shipCargo"), "Груз на борту");
        DrawCargoTransferState(progress.FindPropertyRelative("cargoTransfer"), "Погрузка");
        DrawIslandProductionList(progress.FindPropertyRelative("islandProductions"), "Склады островов");
        DrawProcessList(progress.FindPropertyRelative("activeProcesses"), "Активные процессы");
        DrawStringList(progress.FindPropertyRelative("acceptedMissionIds"), "Принятые миссии", "Идентификаторы миссий, взятых игроком.");
        DrawStringList(progress.FindPropertyRelative("completedMissionIds"), "Завершенные миссии", "Идентификаторы выполненных миссий.");
        EditorGUI.indentLevel--;
    }

    private static void DrawStringList(SerializedProperty list, string label, string tooltip)
    {
        DrawList(list, new GUIContent(label, tooltip), (element, index) =>
        {
            element.stringValue = EditorGUILayout.TextField(new GUIContent("Идентификатор", "Технический идентификатор элемента."), element.stringValue);
        });
    }

    private static void DrawShipExperienceList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Опыт, накопленный на каждом корпусе."), (element, index) =>
        {
            Property(element, "shipId", "Идентификатор корпуса", "Корпус, на котором накоплен опыт.");
            Property(element, "experience", "Опыт", "Количество опыта этого корпуса.");
        });
    }

    private static void DrawInstalledModuleList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Какие модули поставлены в слоты текущего корпуса."), (element, index) =>
        {
            Property(element, "slotId", "Идентификатор слота", "Технический идентификатор слота корпуса или модуля.");
            Property(element, "moduleId", "Идентификатор модуля", "Технический идентификатор установленного модуля.");
        });
    }

    private static void DrawResourceList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Ресурсы игрока."), (element, index) =>
        {
            Property(element, "resourceId", "Идентификатор ресурса", "Например: ore или iron.");
            Property(element, "amount", "Количество", "Сколько этого ресурса есть у игрока.");
        });
    }

    private static void DrawCargoTransferState(SerializedProperty transfer, string label)
    {
        if (transfer == null) return;

        transfer.isExpanded = EditorGUILayout.Foldout(transfer.isExpanded, new GUIContent(label, "Текущая отложенная погрузка между складом острова и кораблем."), true);
        if (!transfer.isExpanded) return;

        EditorGUI.indentLevel++;
        Property(transfer, "active", "Активна", "Если включено, погрузка выполняется по одной единице товара за операцию.");
        Property(transfer, "islandId", "Остров", "Остров, на складе которого идет погрузка.");
        Property(transfer, "startedUtcTicks", "Время старта", "Техническое время UTC в тиках .NET.");
        Property(transfer, "nextOperationUtcTicks", "Следующая операция", "Техническое время UTC, когда будет перенесен следующий килограмм товара.");
        Property(transfer, "secondsPerItem", "Секунд на 1 кг", "Сколько секунд занимает перенос одной единицы товара.");
        Property(transfer, "currentOperationIndex", "Текущая операция", "Индекс текущей операции в очереди.");
        DrawCargoTransferOperationList(transfer.FindPropertyRelative("operations"), "Очередь операций");
        EditorGUI.indentLevel--;
    }

    private static void DrawCargoTransferOperationList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Сначала идут выгрузки с борта, затем загрузки на борт."), (element, index) =>
        {
            Property(element, "itemId", "Товар", "Идентификатор товара из Item.csv.");
            Property(element, "loadToShip", "Загрузка на корабль", "Если включено, товар идет со склада на борт. Если выключено, с борта на склад.");
            Property(element, "remainingAmount", "Осталось, кг", "Сколько килограммов еще нужно перенести в этой операции.");
        });
    }

    private static void DrawIslandProductionList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Сохраненные склады и производственные счетчики каждого острова."), (element, index) =>
        {
            Property(element, "islandId", "Идентификатор острова", "Остров из CSV-конфига Island.");
            Property(element, "productionProgress", "Прогресс производства", "Дробная часть уже произведенной единицы товара.");
            DrawResourceList(element.FindPropertyRelative("storage"), "Склад");
            DrawIslandConsumptionList(element.FindPropertyRelative("consumptions"), "Потребление");
        });
    }

    private static void DrawIslandConsumptionList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Счетчики потребления ресурсов островом."), (element, index) =>
        {
            Property(element, "itemId", "Ресурс", "Какой ресурс потребляет остров.");
            Property(element, "consumptionProgress", "Прогресс потребления", "Дробный прогресс до следующего поглощения одной целой единицы.");
            Property(element, "isSatisfied", "Потребление выполнено", "Если последний цикл смог поглотить ресурс, это потребление усиливает производство.");
        });
    }

    private static void DrawProcessList(SerializedProperty list, string label)
    {
        DrawList(list, new GUIContent(label, "Добыча, производство, миссии и обновления магазина, которые идут в реальном времени."), (element, index) =>
        {
            Property(element, "processId", "Идентификатор процесса", "Уникальный технический идентификатор процесса.");
            Property(element, "displayName", "Название", "Название процесса для интерфейса.");
            Property(element, "kind", "Тип процесса", "Какая система завершит этот процесс.");
            Property(element, "startedUtcTicks", "Время старта", "Техническое время UTC в тиках .NET.");
            Property(element, "nextCompletionUtcTicks", "Следующее завершение", "Техническое время UTC, когда процесс сработает.");
            Property(element, "durationSeconds", "Длительность, сек", "Длительность одного цикла процесса.");
            Property(element, "repeat", "Повторять", "Если включено, процесс запускает следующий цикл после завершения.");
            Property(element, "remainingCycles", "Оставшиеся циклы", "-1 означает бесконечный повтор.");
            Property(element, "inputResourceId", "Входной ресурс", "Ресурс, который был потрачен при старте процесса.");
            Property(element, "inputAmount", "Входное количество", "Сколько ресурса было потрачено.");
            Property(element, "outputResourceId", "Выходной ресурс", "Ресурс, который будет добавлен после завершения.");
            Property(element, "outputAmount", "Выходное количество", "Сколько ресурса будет добавлено.");
            Property(element, "missionId", "Идентификатор миссии", "Миссия, которую выполняет этот процесс.");
            Property(element, "rewardMoney", "Награда деньгами", "Деньги, которые добавятся после завершения миссии.");
            Property(element, "rewardExperience", "Награда опытом", "Опыт корабля за завершение миссии.");
            Property(element, "experienceShipId", "Корабль для опыта", "Корабль, которому начислится опыт.");
        });
    }

    private static void DrawList(SerializedProperty list, GUIContent label, System.Action<SerializedProperty, int> drawElement)
    {
        if (list == null || !list.isArray) return;

        list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, label, true);
        if (!list.isExpanded) return;

        EditorGUI.indentLevel++;
        int newSize = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Размер", "Количество элементов в списке."), list.arraySize));
        if (newSize != list.arraySize)
        {
            list.arraySize = newSize;
        }

        if (list.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Список пуст.", MessageType.Info);
        }

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Элемент " + (i + 1), EditorStyles.boldLabel);
                    if (GUILayout.Button("Удалить", GUILayout.Width(80f)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUI.indentLevel++;
                drawElement(element, i);
                EditorGUI.indentLevel--;
            }
        }

        if (GUILayout.Button("Добавить элемент"))
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty added = list.GetArrayElementAtIndex(index);
            if (added.propertyType == SerializedPropertyType.String)
            {
                added.stringValue = "";
            }
        }

        EditorGUI.indentLevel--;
    }

    private static bool BeginFoldout(SerializedProperty property, string label, string tooltip)
    {
        if (property == null) return false;

        property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, new GUIContent(label, tooltip), true);
        if (!property.isExpanded) return false;

        EditorGUI.indentLevel++;
        return true;
    }

    private static void EndFoldout()
    {
        EditorGUI.indentLevel--;
    }
}
