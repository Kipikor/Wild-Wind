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
        LocalizedInspector.Property(serializedObject, "catalog", "Каталог кораблей", "Список кораблей, доступных для покупки, выбора и выдачи игроку.");
        LocalizedInspector.Property(serializedObject, "techTree", "Древо техники", "Данные исследований и покупок техники.");
        LocalizedInspector.Property(serializedObject, "shipLoader", "Загрузчик корабля", "Компонент, который применяет выбранный паспорт корабля к сценовому кораблю.");
        LocalizedInspector.Property(serializedObject, "missionController", "Контроллер миссии", "Активная миссия сцены. Может быть пусто, если миссий в сцене нет.");
        LocalizedInspector.Property(serializedObject, "startingMoney", "Стартовые деньги", "Сколько денег получает новая игра.");
        LocalizedInspector.DrawPlayerProgress(serializedObject.FindProperty("progress"), "Прогресс игрока");

        LocalizedInspector.Section("Сессия");
        LocalizedInspector.Property(serializedObject, "startingMode", "Стартовый режим", "Режим новой игры: стыковка или вылет.");
        LocalizedInspector.Property(serializedObject, "startingDockId", "Стартовый док", "Идентификатор дока, с которого начинается новая игра.");
        LocalizedInspector.Property(serializedObject, "startingDockKind", "Тип стартового дока", "Тип стартовой стыковки: остров или корабль.");
        LocalizedInspector.Property(serializedObject, "autoSaveOnDock", "Автосохранение при стыковке", "Сохранение разрешено только в режиме стыковки. Перед вылетом создается чекпоинт последней стыковки.");
        LocalizedInspector.Property(serializedObject, "loadSavedGameOnAwake", "Загружать сохранение при старте", "Если прошлый запуск был прерван в полете, загрузка вернет игрока к последней стыковке.");
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

        LocalizedInspector.Section("Отладочный интерфейс стыковки");
        LocalizedInspector.Property(serializedObject, "showDockingDebugUI", "Показывать интерфейс", "Показывает временное окно управления мета-игрой во время Play Mode.");
        LocalizedInspector.Property(serializedObject, "debugUiWidth", "Ширина интерфейса", "Ширина временного отладочного окна в пикселях.");

        LocalizedInspector.Section("Аварии");
        LocalizedInspector.Property(serializedObject, "autoInstallCrashDetector", "Автоматически добавить детектор крушений", "Если включено, на корабль будет добавлен детектор крушений, который откатывает полет при аварии.");

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
        LocalizedInspector.Property(serializedObject, "catalog", "Каталог кораблей", "Каталог, из которого выбирается паспорт корабля.");
        LocalizedInspector.Property(serializedObject, "targetShip", "Корабль в сцене", "Корабль, к которому применяются характеристики из паспорта.");
        LocalizedInspector.Property(serializedObject, "fallbackShipId", "Запасной идентификатор корабля", "Если выбранный корабль не найден, можно указать запасной идентификатор.");
        LocalizedInspector.Property(serializedObject, "applyOnStart", "Применять при старте", "Если включено, паспорт корабля применяется при запуске сцены.");

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
        LocalizedInspector.Property(serializedObject, "metaGameState", "Состояние мета-игры", "Компонент, который выполнит откат к последней стыковке.");
        LocalizedInspector.Property(serializedObject, "crashRelativeSpeed", "Скорость удара для крушения", "Если относительная скорость столкновения выше этого значения, вылет считается потерянным.");
        LocalizedInspector.Property(serializedObject, "crashBelowAltitude", "Высота крушения", "Если корабль опустится ниже этой высоты в вылете, прогресс откатится к последней стыковке.");
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
        LocalizedInspector.Property(serializedObject, "starterShipId", "Идентификатор стартового корабля", "Идентификатор корабля, который игрок получает при первом запуске новой игры.");
        LocalizedInspector.DrawObjectList(serializedObject.FindProperty("ships"), "Корабли", "Все корабли, которые могут быть открыты, куплены или выбраны через мета-прогресс.");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(TechTreeDefinitionSO))]
public class TechTreeDefinitionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Древо техники");
        LocalizedInspector.DrawTechTreeNodeList(serializedObject.FindProperty("nodes"), "Узлы древа");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShipDefinitionSO))]
public class ShipDefinitionSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Мета");
        LocalizedInspector.Property(serializedObject, "shipId", "Идентификатор корабля", "Технический идентификатор корабля. Используется в сохранениях и древе техники.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название корабля для интерфейса.");
        LocalizedInspector.Property(serializedObject, "description", "Описание", "Описание корабля для будущего интерфейса дока или магазина.");
        LocalizedInspector.Property(serializedObject, "tier", "Уровень", "Уровень корабля в прогрессии.");
        LocalizedInspector.Property(serializedObject, "purchasePrice", "Цена покупки", "Сколько денег стоит купить корабль.");
        LocalizedInspector.Property(serializedObject, "unlockCost", "Стоимость открытия", "Резервное поле для будущей логики открытия корабля отдельно от покупки.");

        LocalizedInspector.DrawFlightTuning(serializedObject.FindProperty("flight"), "Летная модель");
        LocalizedInspector.DrawEngineTuning(serializedObject.FindProperty("thrustEngine"), "Маршевый двигатель");
        LocalizedInspector.DrawEngineTuning(serializedObject.FindProperty("liftEngine"), "Подъемный двигатель");
        LocalizedInspector.DrawBalloonTuning(serializedObject.FindProperty("balloon"), "Баллон");
        LocalizedInspector.DrawClaudiumLoopTuning(serializedObject.FindProperty("claudiumLoop"), "Клавдиевый контур");

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
            Property(element, "nodeId", "Идентификатор узла", "Технический идентификатор узла. Используется в сохранениях и зависимостях.");
            Property(element, "displayName", "Название", "Название узла для интерфейса.");
            Property(element, "kind", "Тип узла", "Корабль или модуль.");
            Property(element, "tier", "Уровень техники", "Уровень прогрессии.");
            Property(element, "shipDefinition", "Паспорт корабля", "Паспорт корабля, если узел открывает или продает корабль.");
            Property(element, "shipId", "Идентификатор корабля", "Запасной идентификатор корабля, если паспорт не указан.");
            Property(element, "isPremium", "Премиумная техника", "Пометка для будущей логики премиумной техники.");
            Property(element, "parentShipId", "Идентификатор корабля-владельца", "Корабль, к которому относится модуль.");
            Property(element, "moduleKind", "Тип модуля", "Какой модуль открывает этот узел.");
            Property(element, "researchCostXp", "Стоимость исследования, опыт", "Сколько опыта нужно потратить на исследование.");
            Property(element, "purchasePrice", "Цена покупки, деньги", "Сколько денег нужно потратить на покупку.");
            Property(element, "startsResearched", "Исследован с начала", "Если включено, новая игра считает узел уже исследованным.");
            Property(element, "startsPurchased", "Куплен с начала", "Если включено, новая игра считает узел уже купленным.");
            DrawStringList(element.FindPropertyRelative("prerequisiteNodeIds"), "Условия доступа", "Достаточно любого одного идентификатора из списка.");
            DrawStringList(element.FindPropertyRelative("experienceShipIds"), "Корабли для траты опыта", "С каких кораблей можно тратить опыт на этот узел.");
            Property(element, "editorPosition", "Позиция в редакторе", "Позиция узла в визуальном редакторе древа.");
        });
    }

    public static void DrawFlightTuning(SerializedProperty flight, string label)
    {
        if (!BeginFoldout(flight, label, "Настройки физики и автопилотов корабля.")) return;
        Property(flight, "baseMass", "Базовая масса", "Сухая масса корабля.");
        Property(flight, "targetTrimMass", "Масса триммирования", "Масса, под которую система подъема старается сбалансировать корабль.");
        Property(flight, "propellerDiameter", "Диаметр винта", "Диаметр маршевого винта в метрах.");
        Property(flight, "propellerEfficiency", "КПД винта", "Эффективность передачи мощности в тягу.");
        Property(flight, "propellerMaxPitchMeters", "Максимальный шаг винта, м", "Сколько метров винт проходит за один оборот при максимальном шаге.");
        Property(flight, "initialMainEngineRPM", "Стартовые обороты маршевого двигателя", "Начальная нормализованная цель оборотов маршевого двигателя.");
        Property(flight, "hasCSU", "Есть автомат шага винта", "Автомат шага винта управляет шагом сам и удерживает целевые обороты двигателя.");
        Property(flight, "liftEfficiency", "Эффективность подъема", "Сколько подъемной силы дает контур на единицу мощности.");
        Property(flight, "maxStructuralVerticalSpeed", "Конструкционный лимит вертикальной скорости", "Вертикальная скорость, выше которой корабль считается перегруженным.");
        Property(flight, "maxAutoVerticalSpeed", "Лимит вертикальной скорости автопилота", "Максимальная вертикальная скорость, которую просит автопилот.");
        Property(flight, "airDensity", "Плотность воздуха", "Плотность воздуха для расчета сопротивления.");
        Property(flight, "dragCoefficient", "Коэффициент сопротивления", "Коэффициент формы корпуса.");
        Property(flight, "frontalArea", "Лобовая площадь", "Площадь передней проекции корпуса.");
        Property(flight, "sideResistance", "Боковое сопротивление", "Сопротивление боковому сносу.");
        Property(flight, "verticalAreaFactor", "Множитель вертикальной площади", "Во сколько раз вертикальная площадь больше лобовой.");
        Property(flight, "rudderArea", "Площадь рулей", "Площадь рулевых поверхностей.");
        Property(flight, "rudderDistance", "Плечо рулей", "Расстояние от центра масс до рулей.");
        Property(flight, "rudderMaxLiftCoeff", "Макс. коэффициент подъемной силы руля", "Максимальная эффективность руля при полном отклонении.");
        Property(flight, "maxRudderAngleDeg", "Макс. угол руля", "Максимальный угол отклонения руля.");
        Property(flight, "rudderTurnSpeedDeg", "Скорость перекладки руля", "Как быстро руль меняет угол.");
        Property(flight, "maxAutoTurnRateDeg", "Макс. скорость поворота автопилота", "Скорость поворота, которую может запросить автопилот.");
        Property(flight, "maxStructuralTurnRateDeg", "Конструкционный лимит поворота", "Предел угловой скорости для безопасного полета.");
        Property(flight, "autoStabilizeAtStart", "Автостабилизация при старте", "Включать ли стабилизацию автоматически.");
        Property(flight, "altitudeHold", "Удержание высоты", "Стартовое состояние удержания высоты.");
        Property(flight, "altStiffness", "Жесткость высоты", "P-настройка автопилота высоты.");
        Property(flight, "altDamping", "Демпфирование высоты", "D-настройка автопилота высоты.");
        Property(flight, "altDriftTolerance", "Допуск дрейфа высоты", "Мертвая зона ошибки высоты.");
        Property(flight, "cruiseControl", "Круиз-контроль", "Стартовое состояние круиз-контроля.");
        Property(flight, "maxCruiseSpeedMS", "Макс. скорость круиза", "Предельная скорость для круиз-контроля.");
        Property(flight, "maxManualSpeedMS", "Макс. ручная скорость", "Предельная скорость ручного управления.");
        Property(flight, "headingHold", "Удержание курса", "Стартовое состояние удержания курса.");
        Property(flight, "headingStiffness", "Жесткость курса", "P-настройка автопилота курса.");
        Property(flight, "headingDamping", "Демпфирование курса", "D-настройка автопилота курса.");
        Property(flight, "waypointRadius", "Радиус точки маршрута", "На каком расстоянии точка маршрута считается достигнутой.");
        Property(flight, "minNavSpeed", "Минимальная скорость навигации", "Минимальная маршевая скорость для следования маршруту.");
        Property(flight, "speedStiffness", "Жесткость скорости", "P-настройка круиз-контроля.");
        Property(flight, "speedDamping", "Демпфирование скорости", "D-настройка круиз-контроля.");
        EndFoldout();
    }

    public static void DrawEngineTuning(SerializedProperty engine, string label)
    {
        if (!BeginFoldout(engine, label, "Настройки двигателя.")) return;
        Property(engine, "engineName", "Название двигателя", "Название двигателя для интерфейса.");
        Property(engine, "maxPower", "Максимальная мощность", "Максимальная мощность двигателя.");
        Property(engine, "maxRPM", "Максимальные обороты", "Максимальные обороты двигателя.");
        Property(engine, "responsiveness", "Отзывчивость", "Как быстро двигатель набирает обороты.");
        Property(engine, "startingRPM", "Стартовые обороты", "Начальные нормализованные обороты.");
        Property(engine, "efficiency", "КПД", "Эффективность двигателя.");
        Property(engine, "isClaudium", "Клавдиевый двигатель", "Если включено, двигатель считается клавдиевым.");
        EndFoldout();
    }

    public static void DrawBalloonTuning(SerializedProperty balloon, string label)
    {
        if (!BeginFoldout(balloon, label, "Настройки баллона.")) return;
        Property(balloon, "balloonName", "Название баллона", "Название баллона для интерфейса.");
        Property(balloon, "diameterM", "Диаметр, м", "Диаметр баллона в метрах.");
        Property(balloon, "lengthM", "Длина, м", "Длина баллона в метрах.");
        Property(balloon, "fillPercent", "Заполнение, %", "Процент заполнения баллона.");
        Property(balloon, "leakM3PerHour", "Утечка, м3/час", "Сколько газа утекает за час.");
        Property(balloon, "valveFlowRate", "Скорость клапана", "Скорость выпуска газа через клапан.");
        EndFoldout();
    }

    public static void DrawClaudiumLoopTuning(SerializedProperty loop, string label)
    {
        if (!BeginFoldout(loop, label, "Настройки клавдиевого контура.")) return;
        Property(loop, "loopName", "Название контура", "Название контура для интерфейса.");
        Property(loop, "systemVolumeL", "Объем системы, л", "Общий объем раствора в системе.");
        Property(loop, "loopLengthM", "Длина контура, м", "Длина трубопровода контура.");
        Property(loop, "concentration", "Концентрация, %", "Текущая концентрация клавдия.");
        Property(loop, "targetConcentration", "Целевая концентрация, %", "Концентрация, к которой стремится контур.");
        Property(loop, "solutionStockL", "Запас раствора, л", "Сколько раствора доступно для контура.");
        Property(loop, "solutionDensity", "Плотность раствора", "Масса одного литра раствора.");
        Property(loop, "crystalStockKg", "Запас кристаллов, кг", "Сколько кристаллов доступно для растворения.");
        Property(loop, "dissolutionSpeedKgPerMinute", "Скорость растворения, кг/мин", "Как быстро кристаллы переходят в раствор.");
        Property(loop, "initialTemperatureC", "Начальная температура", "Температура контура при старте.");
        Property(loop, "externalHeatWatts", "Внешний подогрев, Вт", "Дополнительная мощность нагрева.");
        Property(loop, "useEngineWasteHeat", "Забирать тепло от двигателя", "Если включено, контур использует отходящее тепло двигателя.");
        Property(loop, "heatLoss", "Теплопотери", "Как быстро контур теряет тепло.");
        Property(loop, "maxPressureBar", "Макс. давление, бар", "Порог давления для безопасной работы.");
        Property(loop, "efficiency", "КПД контура", "Эффективность преобразования работы контура в подъемную силу.");
        EndFoldout();
    }

    public static void DrawPlayerProgress(SerializedProperty progress, string label)
    {
        if (progress == null) return;

        progress.isExpanded = EditorGUILayout.Foldout(progress.isExpanded, new GUIContent(label, "Текущее сохраненное состояние игрока."), true);
        if (!progress.isExpanded) return;

        EditorGUI.indentLevel++;
        Property(progress, "money", "Деньги", "Текущие деньги игрока.");
        Property(progress, "selectedShipId", "Выбранный корабль", "Идентификатор выбранного корабля.");
        DrawStringList(progress.FindPropertyRelative("unlockedShipIds"), "Открытые корабли", "Идентификаторы кораблей, доступных игроку.");
        DrawStringList(progress.FindPropertyRelative("researchedNodeIds"), "Исследованные узлы", "Идентификаторы уже исследованных узлов древа техники.");
        DrawStringList(progress.FindPropertyRelative("purchasedNodeIds"), "Купленные узлы", "Идентификаторы купленных узлов древа техники.");
        DrawShipExperienceList(progress.FindPropertyRelative("shipExperience"), "Опыт кораблей");

        Property(progress, "currentMode", "Текущий режим", "Стыковка разрешает сохранение. Вылет откатывается к последней стыковке при аварии или выходе.");
        Property(progress, "currentDockKind", "Тип текущего дока", "Где сейчас сохранен игрок: остров или корабль.");
        Property(progress, "currentDockId", "Текущий док", "Идентификатор последней стыковки.");
        Property(progress, "hasCurrentDockPosition", "Есть позиция текущего дока", "Если включено, сохранение хранит точную позицию дока.");
        Property(progress, "currentDockPosition", "Позиция текущего дока", "Позиция, куда вернется игрок при откате к последней стыковке.");
        Property(progress, "activeFlightMissionId", "Активная миссия в вылете", "Идентификатор миссии, ради которой начат текущий вылет.");
        Property(progress, "lastSavedUtcTicks", "Время последнего сохранения", "Техническое время UTC в тиках .NET.");
        Property(progress, "lastProcessUtcTicks", "Время последней обработки процессов", "Техническое время UTC, когда последний раз обновлялись процессы реального времени.");
        Property(progress, "nextShopRefreshUtcTicks", "Следующее обновление магазина", "Техническое время UTC, когда магазин должен обновить ассортимент.");
        Property(progress, "shopSeed", "Зерно магазина", "Число для генерации ассортимента магазина.");
        Property(progress, "receivedStartingInventory", "Стартовые ресурсы выданы", "Защищает от повторной выдачи стартовой руды и железа.");

        DrawResourceList(progress.FindPropertyRelative("inventory"), "Инвентарь");
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
        DrawList(list, new GUIContent(label, "Опыт, накопленный на каждом корабле."), (element, index) =>
        {
            Property(element, "shipId", "Идентификатор корабля", "Корабль, на котором накоплен опыт.");
            Property(element, "experience", "Опыт", "Количество опыта этого корабля.");
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
