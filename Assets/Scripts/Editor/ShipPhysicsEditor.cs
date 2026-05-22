using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShipPhysics))]
public class ShipPhysicsEditor : Editor
{
    private SerializedProperty baseMassProp;
    private SerializedProperty hullMaxTakeoffMassKgProp;
    private SerializedProperty thrustEfficiencyProp;
    private SerializedProperty airDensityProp;
    private SerializedProperty dragCoefficientProp;
    private SerializedProperty frontalAreaProp;
    private SerializedProperty sideResistanceProp;
    private SerializedProperty maxStructuralVerticalSpeedProp;
    private SerializedProperty maxAutoVerticalSpeedProp;
    private SerializedProperty altitudeHoldProp;
    private SerializedProperty cruiseControlProp;
    private SerializedProperty speedStiffnessProp;
    private SerializedProperty speedDampingProp;
    private SerializedProperty targetAltitudeProp;
    private SerializedProperty altStiffnessProp;
    private SerializedProperty altDampingProp;
    private SerializedProperty altDriftToleranceProp;
    private SerializedProperty liftInputProp;
    private SerializedProperty thrustInputProp;
    private SerializedProperty turnInputProp;
    private SerializedProperty targetTrimMassProp;
    private bool resourceCheatsExpanded = false;
    private WorldConfigDatabase resourceCheatConfig;
    private string resourceCheatConfigFolder = "";
    private string customCheatResourceId = "";
    private int customCheatAmount = 1;

    private void OnEnable()
    {
        baseMassProp = serializedObject.FindProperty("baseMass");
        hullMaxTakeoffMassKgProp = serializedObject.FindProperty("hullMaxTakeoffMassKg");
        thrustEfficiencyProp = serializedObject.FindProperty("thrustEfficiency");
        airDensityProp = serializedObject.FindProperty("airDensity");
        dragCoefficientProp = serializedObject.FindProperty("dragCoefficient");
        frontalAreaProp = serializedObject.FindProperty("frontalArea");
        sideResistanceProp = serializedObject.FindProperty("sideResistance");
        maxStructuralVerticalSpeedProp = serializedObject.FindProperty("maxStructuralVerticalSpeed");
        maxAutoVerticalSpeedProp = serializedObject.FindProperty("maxAutoVerticalSpeed");
        altitudeHoldProp = serializedObject.FindProperty("altitudeHold");
        cruiseControlProp = serializedObject.FindProperty("cruiseControl");
        speedStiffnessProp = serializedObject.FindProperty("speedStiffness");
        speedDampingProp = serializedObject.FindProperty("speedDamping");
        targetAltitudeProp = serializedObject.FindProperty("targetAltitude");
        altStiffnessProp = serializedObject.FindProperty("altStiffness");
        altDampingProp = serializedObject.FindProperty("altDamping");
        altDriftToleranceProp = serializedObject.FindProperty("altDriftTolerance");
        liftInputProp = serializedObject.FindProperty("liftInput");
        thrustInputProp = serializedObject.FindProperty("thrustInput");
        turnInputProp = serializedObject.FindProperty("turnInput");
        targetTrimMassProp = serializedObject.FindProperty("targetTrimMass");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ShipPhysics ship = (ShipPhysics)target;

        EditorGUILayout.LabelField("Базовые настройки", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(baseMassProp, new GUIContent("Базовая масса (кг)"));
        EditorGUILayout.PropertyField(hullMaxTakeoffMassKgProp, new GUIContent("Макс. взлетная масса корпуса (кг)", "Предельная полная масса корабля вместе с грузом, которую разрешает корпус."));
        EditorGUILayout.LabelField("Текущая масса с грузом", ship.GetTotalMassKg().ToString("F0") + " кг");
        DrawResourceCheats(ship);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerMaxSpeedMS"), new GUIContent("Макс. скорость винта (м/с)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerEfficiency"), new GUIContent("КПД мощности винта"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerMaxThrustKgf"), new GUIContent("Макс. тяга винта (кгс)"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Двигатель", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enginePowerKwAt100"), new GUIContent("Мощность на 100%, кВт"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineFuelId"), new GUIContent("Топливо"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("engineFuelEfficiency"), new GUIContent("КПД топлива"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Клавдиевый контур", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("claudiumResourceId"), new GUIContent("Ресурс клавдия"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("claudiumConsumptionPerTonSecond"), new GUIContent("Расход на тонну в секунду", "Сколько клавдия тратится в секунду на одну тонну поддерживаемой массы."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("claudiumLiftEfficiency"), new GUIContent("КПД подъема", "Сколько килограммов подъема дает один киловатт мощности двигателя."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("claudiumMaxLiftKg"), new GUIContent("Макс. подъем, кг", "Максимальная масса, которую контур может поддерживать."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("claudiumLiftSmoothing"), new GUIContent("Сглаживание подъема", "Как быстро контур выходит на запрошенную подъемную силу."));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Аэродинамика", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(airDensityProp, new GUIContent("Плотность воздуха (rho)"));
        EditorGUILayout.PropertyField(dragCoefficientProp, new GUIContent("Коэф. формы (Cd)"));
        EditorGUILayout.PropertyField(frontalAreaProp, new GUIContent("Лобовая площадь (м²)"));
        EditorGUILayout.PropertyField(sideResistanceProp, new GUIContent("Боковое сопротивление"));
        EditorGUILayout.PropertyField(maxStructuralVerticalSpeedProp, new GUIContent("Конструкц. лимит (м/с)"));
        EditorGUILayout.PropertyField(maxAutoVerticalSpeedProp, new GUIContent("Лимит автопилота (м/с)"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Окружающая среда", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("windVelocity"), new GUIContent("Ветер (м/с)"));
        EditorGUILayout.HelpBox(
            $"Эффективный ветер: {ship.EffectiveWindVelocity.magnitude:F1} м/с " +
            $"из {ship.windVelocity.magnitude:F1} м/с. " +
            $"Коэффициент формы корабля: x{ship.CurrentWindAerodynamicFactor:F2}.",
            MessageType.Info);

        EditorGUILayout.HelpBox(
            "Коэффициенты формы (Cd):\n" +
            "• 0.04 : Капля (идеально)\n" +
            "• 0.50 : Шар\n" +
            "• 1.10 : Плоский торец (как контейнер)\n" +
            "• 2.00+ : Раскрытый парашют или ковш", 
            MessageType.None);

        // Расчет и вывод максимальной скорости по новой кВт-модели винта
        float currentAirDensity = airDensityProp.floatValue;
        float currentDrag = 0.5f * currentAirDensity * dragCoefficientProp.floatValue * frontalAreaProp.floatValue;
        
        float totalDrag = currentDrag;

        float maxSpeedMS = 0f;
        
        if (totalDrag > 0 && ship.propellerMaxThrustKgf > 0f)
        {
            float thrustN = ship.propellerMaxThrustKgf * 9.81f;
            maxSpeedMS = Mathf.Sqrt(Mathf.Max(0f, thrustN) / Mathf.Max(0.001f, totalDrag));
            if (ship.propellerMaxSpeedMS > 0f)
            {
                maxSpeedMS = Mathf.Min(maxSpeedMS, ship.propellerMaxSpeedMS);
            }
        }

        EditorGUILayout.HelpBox(
            $"Итоговое сопротивление воздуха: {totalDrag:F2}\n" +
            $"Верт. лимит (Констр / Авто): {ship.maxStructuralVerticalSpeed} / {ship.maxAutoVerticalSpeed} м/с\n" +
            $"Грубый потенциал скорости: {maxSpeedMS:F1} м/с",
            MessageType.Info);

        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("Управление двигателем", EditorStyles.boldLabel);
        string modeName = "Стоп";
        float lever = ship.enginePowerLever;
        if (lever <= 0.1f) modeName = "минимум";
        else if (lever <= 0.5f) modeName = "малый ход";
        else if (lever <= 0.9f) modeName = "рабочий ход";
        else if (lever <= 1.0f) modeName = "номинал";
        else modeName = "перегруз";

        ship.enginePowerLever = EditorGUILayout.Slider($"Ручка мощности: {modeName}", ship.enginePowerLever, 0f, 1.2f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Только контур", EditorStyles.miniButtonLeft)) ship.enginePowerLever = ship.engineMinimumPowerLever;
        if (GUILayout.Button("88%", EditorStyles.miniButtonMid)) ship.enginePowerLever = 0.88f;
        if (GUILayout.Button("100%", EditorStyles.miniButtonMid)) ship.enginePowerLever = 1.0f;
        if (GUILayout.Button("120%", EditorStyles.miniButtonRight)) ship.enginePowerLever = 1.2f;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        float targetVal = ship.enginePowerLever;
        bool isOverloaded = false;
        
        Rect rpmRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        Color oldGuiColor = GUI.color;
        if (isOverloaded) GUI.color = new Color(1f, 0.4f, 0.4f);
        
        string overloadText = "";
        EditorGUI.ProgressBar(rpmRect, targetVal / 1.2f, $"Ручка: {(targetVal * 100):F0}%  Мощность: {ship.engineGeneratedPowerKw:F1} кВт  КПД: {ship.engineEfficiencyCurrent:P0}{overloadText}");
        
        GUI.color = oldGuiColor;
        
        Rect pitchRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        float pitchVisual = (ship.propellerPitch + 1f) / 2f; 
        EditorGUI.ProgressBar(pitchRect, pitchVisual, $"Задание тяги винта: {ship.propellerPitch:F2}");
        
        EditorGUILayout.Space(5);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Состояние клавдиевого контура", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Запас клавдия: {ship.claudiumStock:F2}", EditorStyles.label);
            EditorGUILayout.LabelField($"Запрошенный подъем: {ship.claudiumRequestedLiftKg:F0} кг", EditorStyles.label);
            EditorGUILayout.LabelField($"Фактический подъем: {ship.activeLiftForce / 9.81f:F0} кг", EditorStyles.label);
            EditorGUILayout.LabelField($"Забор мощности: {ship.claudiumPowerDrawKw:F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"Остаток на винт: {Mathf.Max(0f, ship.engineGeneratedPowerKw - ship.claudiumPowerDrawKw - ship.gasHarvesterPowerDrawActualKw):F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"Топливо: {ship.engineFuelStockKg:F2} кг, энергоемкость {ship.engineFuelEnergyKwhPerKg:F1} кВт·ч/кг", EditorStyles.label);
            EditorGUILayout.LabelField($"Расход топлива: {ship.engineFuelConsumptionKgPerSecond:F4} кг/с", EditorStyles.label);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Харвестеринг", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasHarvesterEnabled"), new GUIContent("Включить харвестер"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasHarvesterVolumeM3PerSecond"), new GUIContent("Производительность (м3/с)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasHarvesterPowerDrawKw"), new GUIContent("Забор мощности (кВт)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasHarvesterRadiusMeters"), new GUIContent("Радиус забора (м)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gasHarvesterCycleSeconds"), new GUIContent("Цикл добычи (с)"));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Состояние харвестера", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Забор мощности: {ship.gasHarvesterPowerDrawActualKw:F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"Прогресс цикла: {ship.gasHarvesterCycleProgressSeconds:F1}/{Mathf.Max(0.1f, ship.gasHarvesterCycleSeconds):F1} с", EditorStyles.label);
            EditorGUILayout.LabelField($"Буфер концентрата: {ship.gasHarvesterBufferKg:F2} кг", EditorStyles.label);
            EditorGUILayout.LabelField($"Облако: {(string.IsNullOrWhiteSpace(ship.gasHarvesterActiveCloudId) ? "-" : ship.gasHarvesterActiveCloudId)}", EditorStyles.label);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(ship.gasHarvesterLastMessage) ? "Готов." : ship.gasHarvesterLastMessage, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Автопилот и Системы", EditorStyles.boldLabel);
        DrawMiningPanel(ship);
        DrawLeviathanHuntingPanel(ship);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoStabilizeAtStart"), new GUIContent("Стабилизация при старте"));
        EditorGUILayout.PropertyField(altitudeHoldProp, new GUIContent("Удержание высоты"));
        EditorGUILayout.PropertyField(cruiseControlProp, new GUIContent("Круиз-контроль (скорость)"));
        EditorGUILayout.PropertyField(speedStiffnessProp, new GUIContent("Жесткость (P)"));
        EditorGUILayout.PropertyField(speedDampingProp, new GUIContent("Демпфирование (D)"));
        EditorGUILayout.PropertyField(targetAltitudeProp, new GUIContent("Целевая высота (м)"));
        EditorGUILayout.PropertyField(altStiffnessProp, new GUIContent("Вертикальная жесткость (P)"));
        EditorGUILayout.PropertyField(altDampingProp, new GUIContent("Вертикальный демпфер (D)"));
        EditorGUILayout.PropertyField(altDriftToleranceProp, new GUIContent("Допуск дрейфа (м)"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Удержание позиции", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionHold"), new GUIContent("Удерживать на месте", "Корабль запоминает текущие координаты при включении и разворачивается тягой против ветра или дрейфа."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetHoldPosition"), new GUIContent("Цель удержания", "Координата, около которой автопилот держит корабль по горизонтали."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionHoldRadius"), new GUIContent("Радиус удержания (м)", "Внутри этого радиуса автопилот старается гасить скорость и сопротивляться ветру."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionHoldMaxSpeedMS"), new GUIContent("Макс. скорость удержания (м/с)", "Предел скорости, которую удержание позиции может запросить для возврата к точке."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionHoldStiffness"), new GUIContent("Жесткость позиции", "Как сильно ошибка координат превращается в команду возврата."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionHoldDamping"), new GUIContent("Демпфирование дрейфа", "Как сильно текущая горизонтальная скорость гасится при удержании позиции."));
        if (GUILayout.Button("Запомнить текущую позицию", GUILayout.Height(22)))
        {
            foreach (Object selectedTarget in targets)
            {
                ShipPhysics selectedShip = (ShipPhysics)selectedTarget;
                Undo.RecordObject(selectedShip, "Remember Hold Position");
                selectedShip.targetHoldPosition = selectedShip.transform.position;
                EditorUtility.SetDirty(selectedShip);
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Автопилот курса", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("headingHold"), new GUIContent("Удержание курса"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetHeading"), new GUIContent("Целевой курс (0-360)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("headingStiffness"), new GUIContent("Курсовая жесткость (P)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("headingDamping"), new GUIContent("Курсовой демпфер (D)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAutoTurnRateDeg"), new GUIContent("Лимит поворота АП (°/с)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStructuralTurnRateDeg"), new GUIContent("Конструкц. лимит вращения (°/с)"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Путевая навигация", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("routeEnabled"), new GUIContent("Включить маршрут"));
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("waypoints"), new GUIContent("Точки маршрута"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("waypointRadius"), new GUIContent("Радиус точки (м)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("routeArrivalSpeedMS"), new GUIContent("Скорость прибытия (м/с)", "Точка засчитывается только если корабль находится рядом и горизонтальная скорость ниже этого значения."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("routeBrakeAccelerationMS2"), new GUIContent("Расчетное торможение (м/с²)", "Путевая машина заранее снижает скорость так, будто сможет тормозить с этим ускорением."));
        
        ShipPhysics sp = (ShipPhysics)target;
        if (GUILayout.Button("Сгенерировать тестовый маршрут", GUILayout.Height(25)))
        {
            Undo.RecordObject(sp, "Generate Test Route");
            Vector3 start = sp.transform.position;
            sp.waypoints.Clear();
            sp.waypoints.Add(start + new Vector3(0, 50, 200));   // Набор высоты, вперед
            sp.waypoints.Add(start + new Vector3(200, 70, 400)); // Направо и вверх
            sp.waypoints.Add(start + new Vector3(400, 100, 200));// Разворот
            sp.waypoints.Add(start + new Vector3(200, 50, 0));   // Возврат на базу со снижением
            EditorUtility.SetDirty(sp);
        }

        if (Application.isPlaying && serializedObject.FindProperty("routeEnabled").boolValue)
        {
            if (sp.waypoints != null && sp.waypoints.Count > 0)
            {
                EditorGUILayout.LabelField($"Текущая точка: {sp.currentWaypointIndex + 1} / {sp.waypoints.Count}", EditorStyles.helpBox);
            }

            DrawRouteEtaInfo(sp);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Гироскопический поворот", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gyroTurnTorque"), new GUIContent("Макс. усилие поворота (Н*м)", "Внутренний момент поворота корпуса в ньютон-метрах. Работает даже на месте и не тратит мощность двигателя."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gyroTurnDamping"), new GUIContent("Демпфирование поворота", "Как сильно корпус гасит лишнюю угловую скорость."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAutoTurnRateDeg"), new GUIContent("Лимит автопилота (°/с)", "Максимальная угловая скорость, которую может запросить автопилот курса."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStructuralTurnRateDeg"), new GUIContent("Конструкционный лимит (°/с)", "Если корабль вращается быстрее, поворот в ту же сторону ослабляется."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sideResistance"), new GUIContent("Сопротивление дрейфу", "Боковое сопротивление корпуса. Это не поворачивает корабль, а только гасит снос боком."));

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Панель управления рычагами", EditorStyles.boldLabel);
        
        GUIStyle controlBoxStyle = new GUIStyle(GUI.skin.box);
        controlBoxStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(controlBoxStyle);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(targetTrimMassProp, new GUIContent("Триммер (кг)"));
        if (GUILayout.Button("Авто", GUILayout.Width(50)))
        {
            Rigidbody rbComp = ship.GetComponent<Rigidbody>();
            targetTrimMassProp.floatValue = (rbComp != null) ? rbComp.mass : ship.baseMass;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Slider(liftInputProp, -1f, 1f, new GUIContent("Подъем точный (+-10%)"));
        EditorGUILayout.Slider(thrustInputProp, -1f, 1f, new GUIContent("Тяга (назад/вперед)"));
        EditorGUILayout.Slider(turnInputProp, -1f, 1f, new GUIContent("Поворот (ввод штурвала)"));
        
        Rect turnRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        Rigidbody turnRb = ship.GetComponent<Rigidbody>();
        float turnRate = turnRb != null ? turnRb.angularVelocity.y * Mathf.Rad2Deg : 0f;
        float maxTurnRate = ship.maxStructuralTurnRateDeg > 0f ? ship.maxStructuralTurnRateDeg : 1f;
        float turnVisual = Mathf.Clamp01((turnRate / maxTurnRate + 1f) / 2f);
        string dirText = "Центр";
        if (turnRate < -0.05f) dirText = "Влево";
        else if (turnRate > 0.05f) dirText = "Вправо";

        EditorGUI.ProgressBar(turnRect, turnVisual, $"Скорость поворота: {dirText} {Mathf.Abs(turnRate):F1}°/с, момент {ship.currentGyroTurnTorque:F0} Н*м");

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Сбросить рычаги на 0", GUILayout.Height(30)))
        {
            liftInputProp.floatValue = 0f;
            thrustInputProp.floatValue = 0f;
            turnInputProp.floatValue = 0f;
        }

        EditorGUILayout.EndVertical();

        // Вывод телеметрии (скорости)
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Телеметрия", EditorStyles.boldLabel);
        
        GUIStyle telemetryStyle = new GUIStyle(GUI.skin.box);
        telemetryStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(telemetryStyle);
        
        Rigidbody rb = ship.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float currentSpeedMS = rb.linearVelocity.magnitude;
            
            EditorGUILayout.LabelField($"Общая скорость: {currentSpeedMS:F2} м/с", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            
            // Линейная скорость
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Линейная (м/с)", GUILayout.Width(100));
            
            EditorGUILayout.LabelField("X", GUILayout.Width(12));
            float vx = EditorGUILayout.FloatField((float)System.Math.Round(rb.linearVelocity.x, 2));
            
            EditorGUILayout.LabelField("Y", GUILayout.Width(12));
            float vy = EditorGUILayout.FloatField((float)System.Math.Round(rb.linearVelocity.y, 2));
            
            EditorGUILayout.LabelField("Z", GUILayout.Width(12));
            float vz = EditorGUILayout.FloatField((float)System.Math.Round(rb.linearVelocity.z, 2));
            EditorGUILayout.EndHorizontal();

            // Угловая скорость
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Угловая (°/с)", GUILayout.Width(100));
            
            EditorGUILayout.LabelField("X", GUILayout.Width(12));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(0f);
            }
            
            EditorGUILayout.LabelField("Y", GUILayout.Width(12));
            float ay_deg = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.y * Mathf.Rad2Deg, 2));
            
            EditorGUILayout.LabelField("Z", GUILayout.Width(12));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(0f);
            }
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                rb.linearVelocity = new Vector3(vx, vy, vz);
                rb.angularVelocity = new Vector3(0f, ay_deg * Mathf.Deg2Rad, 0f);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Мощность двигателя: {ship.engineGeneratedPowerKw:F1} кВт", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Контур забрал: {ship.claudiumPowerDrawKw:F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"Винт получил: {ship.propellerInputPowerKw:F1} кВт, полезно {ship.propellerUsefulPowerKw:F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"КПД винта: {ship.propellerCalculatedEfficiency:P0}, лимит скорости: {ship.propellerMaxSpeedMS:F1} м/с", EditorStyles.label);
            EditorGUILayout.LabelField($"Текущая тяга винта: {ship.propellerThrustKgf:F1} кгс", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Ручка мощности: {(ship.enginePowerLever * 100):F0}%, минимум контура: {(ship.engineMinimumPowerLever * 100):F0}%", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Ожидание компонента Rigidbody...");
        }

        EditorGUILayout.EndVertical();

        // Применяем изменения
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMiningPanel(ShipPhysics ship)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Майнинг", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("miningImpactHoldCapacityKg"), new GUIContent("Противоударный кузов, кг"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("miningCatchRadiusMeters"), new GUIContent("Радиус сбора кусков, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("miningManualShotRangeMeters"), new GUIContent("Дальность выстрела, м"));
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Оружие", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponResourceId"), new GUIContent("Ресурс оружия"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponShotCostKg"), new GUIContent("Расход за выстрел, кг"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponShotAlarmRadiusMeters"), new GUIContent("Радиус тревоги выстрела, м"));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Состояние майнинга", EditorStyles.miniBoldLabel);

            MetaGameState meta = Application.isPlaying ? FindFirstObjectByType<MetaGameState>() : null;
            if (Application.isPlaying && meta != null)
            {
                meta.EnsureProgressInitialized();
                EditorGUILayout.LabelField("Груз", FormatCargo(meta.progress.shipCargo));
                EditorGUILayout.LabelField("Запас грузоподъемности", meta.GetRemainingShipCargoCapacityKg().ToString("0") + " кг");
                DrawWeaponStock(meta, ship);
                DrawCargoCapacityBreakdown(ship, meta);
            }
            else
            {
                EditorGUILayout.LabelField("Груз", "доступен в Play Mode");
            }

            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(ship.miningLastMessage) ? "Готов." : ship.miningLastMessage, EditorStyles.wordWrappedLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Выстрел"))
                {
                    Undo.RecordObject(ship, "Mining Shot");
                    ship.TryShootNearestMiningRock(out _);
                    EditorUtility.SetDirty(ship);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawLeviathanHuntingPanel(ShipPhysics ship)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Охота на левиафанов", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonRangeMeters"), new GUIContent("Дальность гарпуна, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonRopeLengthMeters"), new GUIContent("Длина троса, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonMaxTensionKg"), new GUIContent("Предел троса, кгс"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonMaxTensionLifetimeSeconds"), new GUIContent("Жизнь троса на пределе, сек"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonStiffnessNPerMeter"), new GUIContent("Жесткость троса, Н/м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonDampingNsPerMeter"), new GUIContent("Демпфер рывка, Н·с/м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonReelForceN"), new GUIContent("Подтяжка лебедки, Н"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonFatiguePerSecond"), new GUIContent("Урон здоровью в секунду"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanWeaponShotFlightDamage"), new GUIContent("Урон здоровью от выстрела"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonCarcassCollectionRadiusMeters"), new GUIContent("Радиус сбора туши, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harpoonCarcassWinchSpeedMS"), new GUIContent("Скорость лебедки, м/с"));
        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntAutopilotEnabled"), new GUIContent("Автоохота"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntSearchRangeMeters"), new GUIContent("Радиус поиска, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntEngageRangeMeters"), new GUIContent("Дистанция выстрела, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntApproachDistanceMeters"), new GUIContent("Дистанция подхода, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntMinimumAltitudeMeters"), new GUIContent("Мин. высота подхода, м"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntShotCooldownSeconds"), new GUIContent("Пауза выстрела, сек"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntBrokenTargetCooldownSeconds"), new GUIContent("Игнор после обрыва, сек"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntMaxTargetMassKg"), new GUIContent("Макс. масса цели, кг"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leviathanHuntMaxTetherDriftMeters"), new GUIContent("Макс. отход от захвата, м"));
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Состояние гарпуна", EditorStyles.miniBoldLabel);
            MetaGameState meta = Application.isPlaying ? FindFirstObjectByType<MetaGameState>() : null;
            if (Application.isPlaying && meta != null)
            {
                meta.EnsureProgressInitialized();
                DrawWeaponStock(meta, ship);
            }

            EditorGUILayout.LabelField(ship.GetHarpoonStatusRu(), EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrWhiteSpace(ship.leviathanHuntAutopilotMessage))
            {
                EditorGUILayout.LabelField("Автоохота", ship.leviathanHuntAutopilotMessage, EditorStyles.wordWrappedLabel);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Выстрел гарпуном"))
                {
                    Undo.RecordObject(ship, "Fire Harpoon");
                    ship.TryFireHarpoonAtNearestLeviathan(out _);
                    EditorUtility.SetDirty(ship);
                }

                if (GUILayout.Button("Отцепить"))
                {
                    Undo.RecordObject(ship, "Detach Harpoon");
                    ship.DetachHarpoon();
                    EditorUtility.SetDirty(ship);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Выстрел оружием"))
                {
                    Undo.RecordObject(ship, "Shoot Leviathan");
                    ship.TryShootLeviathan(out _);
                    EditorUtility.SetDirty(ship);
                }

                if (GUILayout.Button("Забрать тушу в груз"))
                {
                    Undo.RecordObject(ship, "Claim Leviathan Carcass");
                    ship.TryClaimHarpoonedLeviathan(out _);
                    EditorUtility.SetDirty(ship);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Кнопки охоты доступны в Play Mode.", EditorStyles.miniLabel);
            }
        }
    }

    private static string FormatCargo(System.Collections.Generic.List<ResourceStack> cargo)
    {
        if (cargo == null || cargo.Count == 0) return "пусто";

        string text = "";
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            if (text.Length > 0) text += ", ";
            text += stack.resourceId + "=" + stack.amount;
        }

        return text.Length > 0 ? text : "пусто";
    }

    private static void DrawWeaponStock(MetaGameState meta, ShipPhysics ship)
    {
        if (meta == null || meta.progress == null || ship == null) return;

        string resourceId = string.IsNullOrWhiteSpace(ship.weaponResourceId) ? "weapon" : ship.weaponResourceId;
        int stockKg = meta.progress.GetShipCargoAmount(resourceId);
        float availableKg = Mathf.Max(0f, stockKg - Mathf.Clamp(meta.progress.shipWeaponSpendBufferKg, 0f, 0.999f));
        float costKg = Mathf.Max(0.01f, ship.weaponShotCostKg);
        int shots = Mathf.FloorToInt(availableKg / costKg);
        EditorGUILayout.LabelField("Оружие", $"{availableKg:0.0} кг, выстрелов {shots}");
    }

    private static void DrawCargoCapacityBreakdown(ShipPhysics ship, MetaGameState meta)
    {
        if (ship == null || meta == null || meta.progress == null) return;

        float engineLoopLiftKg = Mathf.Max(0f, ship.enginePowerKwAt100) * Mathf.Max(0f, ship.claudiumLiftEfficiency);
        float allowedTakeoffKg = Mathf.Min(engineLoopLiftKg, Mathf.Min(Mathf.Max(0f, ship.claudiumMaxLiftKg), Mathf.Max(0f, ship.hullMaxTakeoffMassKg)));
        float maxCargoKg = Mathf.Max(0f, allowedTakeoffKg - Mathf.Max(0f, ship.baseMass));
        float currentCargoKg = meta.progress.GetShipCargoMassKg(meta.WorldConfig);

        EditorGUILayout.LabelField("Сухая масса", ship.baseMass.ToString("0") + " кг");
        EditorGUILayout.LabelField("Грузоподъемность", currentCargoKg.ToString("0") + " / " + maxCargoKg.ToString("0") + " кг");
        EditorGUILayout.LabelField(
            "Лимиты",
            $"двиг+контур {engineLoopLiftKg:0} кг, контур {ship.claudiumMaxLiftKg:0} кг, рама {ship.hullMaxTakeoffMassKg:0} кг");
    }

    private static void DrawRouteEtaInfo(ShipPhysics ship)
    {
        if (ship == null) return;

        RouteEtaInfo eta = ship.GetCurrentRouteEta();
        MessageType messageType = eta.canEstimate ? MessageType.Info : MessageType.Warning;
        EditorGUILayout.HelpBox(BuildRouteEtaText(eta), messageType);
    }

    private static string BuildRouteEtaText(RouteEtaInfo eta)
    {
        if (!eta.hasTarget)
        {
            return "ETA путевой машины: " + eta.status;
        }

        string etaText = eta.canEstimate ? FormatEtaSeconds(eta.etaSeconds) : "нет устойчивой оценки";
        return
            $"ETA до текущей точки: {etaText}\n" +
            $"Точка: {eta.waypointIndex + 1} / {eta.waypointCount}  X {eta.targetPosition.x:F0}  Y {eta.targetPosition.y:F0}  Z {eta.targetPosition.z:F0}\n" +
            $"До радиуса: {eta.horizontalRemaining:F0} м по горизонту, {eta.verticalRemaining:F0} м по высоте\n" +
            $"Скорость к точке: {eta.horizontalClosingSpeed:F1} м/с, вертикально: {eta.verticalClosingSpeed:F1} м/с\n" +
            eta.status;
    }

    private static string FormatEtaSeconds(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) return "-";

        int totalSeconds = Mathf.CeilToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int restSeconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours:D2}:{minutes:D2}:{restSeconds:D2}";
        }

        return $"{minutes:D2}:{restSeconds:D2}";
    }

    private void DrawResourceCheats(ShipPhysics ship)
    {
        EditorGUILayout.Space(8);
        resourceCheatsExpanded = EditorGUILayout.Foldout(
            resourceCheatsExpanded,
            new GUIContent("Читы ресурсов", "Позволяет в Play Mode поставить точное количество ресурсов в инвентаре, грузе корабля и складе текущего острова."),
            true);

        if (!resourceCheatsExpanded) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Читы ресурсов работают в Play Mode, чтобы значения менялись прямо в текущей сессии.", MessageType.Info);
                return;
            }

            MetaGameState meta = FindFirstObjectByType<MetaGameState>();
            if (meta == null)
            {
                EditorGUILayout.HelpBox("В сцене не найден MetaGameState.", MessageType.Warning);
                return;
            }

            meta.EnsureProgressInitialized();
            EnsureResourceCheatConfig(meta);

            if (resourceCheatConfig == null || !resourceCheatConfig.isLoaded)
            {
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(resourceCheatConfig?.lastError) ? "Конфиг Item.csv не загружен." : resourceCheatConfig.lastError, MessageType.Warning);
                if (GUILayout.Button("Перезагрузить конфиг ресурсов"))
                {
                    ReloadResourceCheatConfig(meta);
                }

                return;
            }

            IslandProductionState dockStorage = GetCurrentDockStorage(meta, out string dockName);
            EditorGUILayout.LabelField("Текущий склад", dockStorage != null ? dockName : "нет текущего острова из Island.csv");
            if (GUILayout.Button("Перезагрузить конфиг ресурсов", EditorStyles.miniButton))
            {
                ReloadResourceCheatConfig(meta);
            }

            for (int i = 0; i < resourceCheatConfig.items.Count; i++)
            {
                ItemConfig item = resourceCheatConfig.items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
                DrawResourceCheatRow(meta, ship, dockStorage, item.id, resourceCheatConfig.GetItemNameRu(item.id));
            }

            DrawCustomResourceCheat(meta, ship, dockStorage);
        }
    }

    private void DrawResourceCheatRow(MetaGameState meta, ShipPhysics ship, IslandProductionState dockStorage, string resourceId, string displayName)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(displayName + " (" + resourceId + ")", EditorStyles.boldLabel);
            DrawResourceAmountField("Инвентарь", meta, ship, null, resourceId, meta.progress.GetResourceAmount(resourceId), ResourceCheatTarget.Inventory);
            DrawResourceAmountField("Борт корабля", meta, ship, null, resourceId, meta.progress.GetShipCargoAmount(resourceId), ResourceCheatTarget.ShipCargo);

            if (dockStorage != null)
            {
                DrawResourceAmountField("Склад острова", meta, ship, dockStorage, resourceId, dockStorage.GetResourceAmount(resourceId), ResourceCheatTarget.DockStorage);
            }
        }
    }

    private void DrawCustomResourceCheat(MetaGameState meta, ShipPhysics ship, IslandProductionState dockStorage)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Любой ресурс по ID", EditorStyles.boldLabel);
        customCheatResourceId = EditorGUILayout.TextField(new GUIContent("ID ресурса", "Можно вписать любой технический ID, даже если его еще нет в Item.csv."), customCheatResourceId);
        customCheatAmount = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Количество", "Точное количество в единицах предмета: места для пассажиров, литры для жидкостей/газа/сыпучки, штуки для товаров."), customCheatAmount));

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrWhiteSpace(customCheatResourceId);
        if (GUILayout.Button("В инвентарь"))
        {
            SetResourceCheat(meta, ship, null, customCheatResourceId.Trim(), customCheatAmount, ResourceCheatTarget.Inventory);
        }

        if (GUILayout.Button("На борт"))
        {
            SetResourceCheat(meta, ship, null, customCheatResourceId.Trim(), customCheatAmount, ResourceCheatTarget.ShipCargo);
        }

        GUI.enabled = GUI.enabled && dockStorage != null;
        if (GUILayout.Button("На склад"))
        {
            SetResourceCheat(meta, ship, dockStorage, customCheatResourceId.Trim(), customCheatAmount, ResourceCheatTarget.DockStorage);
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResourceAmountField(string label, MetaGameState meta, ShipPhysics ship, IslandProductionState dockStorage, string resourceId, int currentAmount, ResourceCheatTarget target)
    {
        EditorGUI.BeginChangeCheck();
        int newAmount = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent(label, "Точное количество в единицах предмета: места для пассажиров, литры для жидкостей/газа/сыпучки, штуки для товаров. 0 удаляет ресурс из списка."), currentAmount));
        if (EditorGUI.EndChangeCheck())
        {
            SetResourceCheat(meta, ship, dockStorage, resourceId, newAmount, target);
        }
    }

    private void SetResourceCheat(MetaGameState meta, ShipPhysics ship, IslandProductionState dockStorage, string resourceId, int amount, ResourceCheatTarget target)
    {
        if (meta == null || meta.progress == null || string.IsNullOrWhiteSpace(resourceId)) return;

        Undo.RecordObject(meta, "Cheat Resource Amount");
        amount = Mathf.Max(0, amount);

        if (target == ResourceCheatTarget.Inventory)
        {
            meta.progress.SetResourceAmount(resourceId, amount);
        }
        else if (target == ResourceCheatTarget.ShipCargo)
        {
            meta.progress.SetShipCargoAmount(resourceId, amount);
            ApplyCargoCheatMass(ship, meta);
        }
        else if (target == ResourceCheatTarget.DockStorage && dockStorage != null)
        {
            dockStorage.SetResourceAmount(resourceId, amount);
        }

        meta.progress.Normalize();
        EditorUtility.SetDirty(meta);
        Repaint();
    }

    private static void ApplyCargoCheatMass(ShipPhysics ship, MetaGameState meta)
    {
        if (ship == null || meta == null || meta.progress == null) return;

        Undo.RecordObject(ship, "Apply Cargo Cheat Mass");
        ship.cargoMassKg = meta.progress.GetShipCargoMassKg(meta.WorldConfig);
        ship.engineFuelStockKg = string.IsNullOrWhiteSpace(ship.engineFuelId) ? 0f : meta.progress.GetShipCargoAmount(ship.engineFuelId);
        string claudiumResourceId = string.IsNullOrWhiteSpace(ship.claudiumResourceId) ? "claudium" : ship.claudiumResourceId;
        ship.claudiumStock = meta.progress.GetShipCargoAmount(claudiumResourceId);
        ship.RefreshRuntimeShipSettings();
        EditorUtility.SetDirty(ship);
    }

    private IslandProductionState GetCurrentDockStorage(MetaGameState meta, out string dockName)
    {
        dockName = "";
        if (meta == null || meta.progress == null || resourceCheatConfig == null) return null;
        if (meta.progress.currentDockKind != DockingLocationKind.Island) return null;

        IslandConfig island = resourceCheatConfig.GetIsland(meta.progress.currentDockId);
        if (island == null) return null;

        dockName = string.IsNullOrWhiteSpace(island.localNameRu) ? island.id : island.localNameRu;
        return meta.progress.GetIslandProductionState(island.id, true);
    }

    private void EnsureResourceCheatConfig(MetaGameState meta)
    {
        string folder = meta != null && !string.IsNullOrWhiteSpace(meta.worldConfigFolder) ? meta.worldConfigFolder : "Data/Config";
        if (resourceCheatConfig != null && resourceCheatConfig.isLoaded && resourceCheatConfigFolder == folder) return;
        ReloadResourceCheatConfig(meta);
    }

    private void ReloadResourceCheatConfig(MetaGameState meta)
    {
        resourceCheatConfigFolder = meta != null && !string.IsNullOrWhiteSpace(meta.worldConfigFolder) ? meta.worldConfigFolder : "Data/Config";
        resourceCheatConfig = new WorldConfigDatabase();
        resourceCheatConfig.LoadFromAssetsConfigFolder(resourceCheatConfigFolder);
    }

    private enum ResourceCheatTarget
    {
        Inventory,
        ShipCargo,
        DockStorage
    }

    public override bool RequiresConstantRepaint()
    {
        bool isTyping = GUIUtility.keyboardControl != 0;
        return Application.isPlaying && !isTyping;
    }
}

public static class LeviathanHuntingSetupEditor
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";

    [MenuItem("Wild Wind/Leviathans/Prepare Leviathan Test Scene")]
    public static void PrepareLeviathanTestSceneMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        PrepareStarterHunterShip(meta);
        Debug.Log("[Левиафаны] Тестовая сцена подготовлена: MetaGameState, ShipLoader, менеджер левиафанов и стартовый охотничий корабль настроены.");
    }

    [MenuItem("Wild Wind/Leviathans/Prepare Starter Hunter Ship")]
    public static void PrepareStarterHunterShipMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        PrepareStarterHunterShip(meta);
        Debug.Log("[Левиафаны] Стартовый корабль подготовлен для охоты: гарпун настроен, добавлены немного топлива и клавдия.");
    }

    [MenuItem("Wild Wind/Leviathans/Rebuild Leviathans From Configs")]
    public static void RebuildLeviathansFromConfigsMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        if (Application.isPlaying && meta.leviathanManager != null)
        {
            meta.leviathanManager.SpawnConfiguredLeviathans(true);
            Debug.Log("[Левиафаны] Левиафаны пересозданы из CSV-конфигов.");
        }
        else
        {
            Debug.Log("[Левиафаны] Менеджер готов. Пересоздание видимых левиафанов работает в Play Mode.");
        }
    }

    private static void PrepareStarterHunterShip(MetaGameState meta)
    {
        if (meta == null) return;

        EnsureSceneLinks(meta);
        Undo.RecordObject(meta, "Prepare starter leviathan hunter ship");

        meta.EnsureProgressInitialized();
        meta.progress.SelectHull("starter_hull");
        ShipAssemblyBuilder.AutoInstallRequiredModules(meta.CurrentCatalog, meta.techTree, meta.progress, out _);
        meta.progress.SetShipCargoAmount("charcoal", 18);
        meta.progress.SetShipCargoAmount("claudium", 8);
        meta.progress.SetShipCargoAmount("weapon", 5);
        meta.progress.shipWeaponSpendBufferKg = 0f;
        meta.ApplySelectedShip();

        ShipPhysics ship = meta.shipLoader != null ? meta.shipLoader.targetShip : UnityEngine.Object.FindFirstObjectByType<ShipPhysics>();
        if (ship != null)
        {
            Undo.RecordObject(ship, "Prepare leviathan hunter settings");
            ship.harpoonRangeMeters = 280f;
            ship.harpoonRopeLengthMeters = 160f;
            ship.harpoonMaxTensionKg = 4500f;
            ship.harpoonMaxTensionLifetimeSeconds = 5f;
            ship.harpoonStiffnessNPerMeter = 360f;
            ship.harpoonDampingNsPerMeter = 110f;
            ship.harpoonReelForceN = 600f;
            ship.harpoonFatiguePerSecond = 18f;
            ship.harpoonCarcassCollectionRadiusMeters = 14f;
            ship.harpoonCarcassWinchSpeedMS = 7f;
            ship.leviathanHuntSearchRangeMeters = 900f;
            ship.leviathanHuntEngageRangeMeters = 240f;
            ship.leviathanHuntApproachDistanceMeters = 90f;
            ship.leviathanHuntMinimumAltitudeMeters = 45f;
            ship.leviathanHuntShotCooldownSeconds = 4f;
            ship.leviathanHuntBrokenTargetCooldownSeconds = 25f;
            ship.leviathanHuntMaxTargetMassKg = 7000f;
            ship.leviathanHuntMaxTetherDriftMeters = 1500f;
            ship.weaponResourceId = "weapon";
            ship.weaponShotCostKg = 0.1f;
            ship.weaponShotAlarmRadiusMeters = 450f;
            ship.leviathanWeaponShotFlightDamage = 25f;
            ship.harpoonLastMessage = "Гарпун готов к тестовой охоте.";
            ship.RefreshRuntimeShipSettings();
            EditorUtility.SetDirty(ship);
        }

        EditorUtility.SetDirty(meta);
    }

    private static MetaGameState FindOrCreateMetaGameState()
    {
        MetaGameState meta = UnityEngine.Object.FindFirstObjectByType<MetaGameState>();
        if (meta != null) return meta;

        GameObject gameObject = new GameObject("MetaGameState");
        Undo.RegisterCreatedObjectUndo(gameObject, "Create MetaGameState");
        return gameObject.AddComponent<MetaGameState>();
    }

    private static void EnsureSceneLinks(MetaGameState meta)
    {
        if (meta == null) return;

        Undo.RecordObject(meta, "Prepare leviathan scene");
        meta.worldConfigFolder = "Data/Config";

        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        if (catalog != null)
        {
            meta.catalog = catalog;
        }

        TechTreeDefinitionSO techTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TechTreePath);
        if (techTree != null)
        {
            meta.techTree = techTree;
        }

        if (meta.shipLoader == null)
        {
            meta.shipLoader = UnityEngine.Object.FindFirstObjectByType<ShipLoader>();
        }

        if (meta.shipLoader == null)
        {
            GameObject loaderObject = new GameObject("ShipLoader");
            Undo.RegisterCreatedObjectUndo(loaderObject, "Create ShipLoader");
            meta.shipLoader = loaderObject.AddComponent<ShipLoader>();
        }

        meta.shipLoader.catalog = meta.catalog;
        if (meta.shipLoader.targetShip == null)
        {
            meta.shipLoader.targetShip = UnityEngine.Object.FindFirstObjectByType<ShipPhysics>();
        }

        if (meta.leviathanManager == null)
        {
            meta.leviathanManager = UnityEngine.Object.FindFirstObjectByType<LeviathanManager>();
        }

        if (meta.leviathanManager == null)
        {
            meta.leviathanManager = Undo.AddComponent<LeviathanManager>(meta.gameObject);
        }

        meta.leviathanManager.metaGameState = meta;
        meta.leviathanManager.spawnLeviathansOnPlay = true;
        meta.spawnConfigIslandsOnPlay = true;

        EditorUtility.SetDirty(meta);
        EditorUtility.SetDirty(meta.shipLoader);
        EditorUtility.SetDirty(meta.leviathanManager);
    }
}

[CustomEditor(typeof(LeviathanManager))]
public class LeviathanManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Связи");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Мета-игра", "MetaGameState, из которого берутся CSV-конфиги мира.");

        LocalizedInspector.Section("Спавн");
        LocalizedInspector.Property(serializedObject, "spawnLeviathansOnPlay", "Создавать в Play Mode", "Если включено, менеджер создает левиафанов из Leviathan_zone.csv и Leviathan_type.csv.");
        LocalizedInspector.Property(serializedObject, "syncIntervalSeconds", "Интервал синхронизации, сек", "Как часто менеджер проверяет, что нужные левиафаны существуют в сцене.");

        serializedObject.ApplyModifiedProperties();

        LeviathanManager manager = (LeviathanManager)target;
        if (Application.isPlaying && GUILayout.Button("Пересоздать левиафанов из конфигов"))
        {
            manager.SpawnConfiguredLeviathans(true);
            Debug.Log("[Левиафаны] Левиафаны пересозданы из CSV-конфигов.", manager);
        }
    }
}

[CustomEditor(typeof(Leviathan))]
public class LeviathanEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Leviathan leviathan = (Leviathan)target;

        LocalizedInspector.Section("Идентификация");
        LocalizedInspector.Property(serializedObject, "leviathanId", "id левиафана", "Уникальный id конкретного существа в сцене.");
        LocalizedInspector.Property(serializedObject, "typeId", "Тип", "id вида из Leviathan_type.csv.");
        LocalizedInspector.Property(serializedObject, "zoneId", "Зона", "id зоны появления из Leviathan_zone.csv.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название, которое показывается в отладке и логах.");
        LocalizedInspector.Property(serializedObject, "carcassItemId", "Ресурс туши", "id ресурса, который попадет в груз после успешной охоты.");

        LocalizedInspector.Section("Зона плавания");
        LocalizedInspector.Property(serializedObject, "homeCenter", "Центр зоны", "Точка, вокруг которой левиафан выбирает цели плавания.");
        LocalizedInspector.Property(serializedObject, "zoneRadiusMeters", "Радиус зоны, м", "Как далеко левиафан может уходить от центра.");
        LocalizedInspector.Property(serializedObject, "minY", "Нижняя высота, м", "Ниже этой высоты левиафан старается подниматься.");
        LocalizedInspector.Property(serializedObject, "maxY", "Верхняя высота, м", "Верхняя граница случайного выбора высоты.");

        LocalizedInspector.Section("Тело");
        LocalizedInspector.Property(serializedObject, "bodyLengthMeters", "Длина тела, м", "Размер визуала и капсульного коллайдера вдоль тела.");
        LocalizedInspector.Property(serializedObject, "bodyRadiusMeters", "Радиус тела, м", "Толщина тела и визуального объема.");
        LocalizedInspector.Property(serializedObject, "massKg", "Масса, кг", "Физическая масса живого левиафана.");
        LocalizedInspector.Property(serializedObject, "carcassMassKg", "Масса туши, кг", "Сколько килограммов ресурса туши попадет в груз корабля.");

        LocalizedInspector.Section("Живучесть");
        LocalizedInspector.Property(serializedObject, "maxHealth", "Макс. здоровье", "Полное здоровье существа.");
        LocalizedInspector.Property(serializedObject, "health", "Здоровье", "Текущее здоровье. При нуле левиафан становится добычей.");
        LocalizedInspector.Property(serializedObject, "claudiumLiftKg", "Клавдиевая компенсация, кг", "Сколько массы компенсирует природная клавдиевая система. У живого левиафана обычная гравитация выключена.");
        LocalizedInspector.Property(serializedObject, "isCarcass", "Туша", "Если включено, левиафан уже потерял здоровье.");

        LocalizedInspector.Section("Движение");
        LocalizedInspector.Property(serializedObject, "cruiseSpeedMS", "Крейсерская скорость, м/с", "Желаемая скорость поведения. Фактическая скорость ограничивается тягой и сопротивлением воздуха.");
        LocalizedInspector.Property(serializedObject, "wanderRadiusMeters", "Радиус блуждания, м", "Как далеко от центра зоны выбираются случайные цели.");
        LocalizedInspector.Property(serializedObject, "forwardThrustKgf", "Маршевая тяга, кгс", "Основная тяга строго вперед вдоль тела. Чем сильнее цель сбоку или сзади, тем меньше эта тяга помогает.");
        LocalizedInspector.Property(serializedObject, "omniThrustKgf", "Всенаправленная тяга, кгс", "Тяга для подъема, снижения, бокового сноса и торможения.");
        LocalizedInspector.Property(serializedObject, "turnTorqueNm", "Момент поворота, Н*м", "Сила поворота автопилота и ручного управления.");
        LocalizedInspector.Property(serializedObject, "turnDamping", "Демпфирование поворота", "Гасит лишнюю угловую скорость.");
        LocalizedInspector.Property(serializedObject, "stationaryTurnEffectiveness", "Поворот на месте", "Доля поворотного усилия, когда левиафан почти не движется. Делает поворот по дуге удобнее, чем разворот на месте.");
        LocalizedInspector.Property(serializedObject, "fullTurnEffectSpeedMS", "Полный поворот от скорости, м/с", "На этой скорости левиафан получает полный момент поворота.");
        LocalizedInspector.Property(serializedObject, "autoCalculateDragArea", "Авто площадь сопротивления", "Если включено, лобовая площадь считается из радиуса тела.");
        LocalizedInspector.Property(serializedObject, "airDensity", "Плотность воздуха", "Плотность среды для расчета сопротивления.");
        LocalizedInspector.Property(serializedObject, "dragCoefficient", "Коэф. сопротивления", "Насколько сильно тело тормозится воздухом.");
        LocalizedInspector.Property(serializedObject, "frontalArea", "Лобовая площадь, м²", "Площадь сопротивления при движении вперед.");
        LocalizedInspector.Property(serializedObject, "sideAreaMultiplier", "Боковая площадь x", "Во сколько раз боковое сопротивление больше лобового.");
        LocalizedInspector.Property(serializedObject, "verticalAreaMultiplier", "Вертикальная площадь x", "Во сколько раз сопротивление вверх/вниз больше лобового.");
        LocalizedInspector.Property(serializedObject, "bellyDownStabilization", "Пузом вниз", "Если включено, левиафан мягко прокручивает себя вокруг продольной оси, чтобы спина была сверху.");
        LocalizedInspector.Property(serializedObject, "bellyDownTorqueNm", "Момент выравнивания, Н*м", "Сила системы, которая возвращает пузо вниз без запрета свободных вращений.");
        LocalizedInspector.Property(serializedObject, "bellyDownDamping", "Демпфер выравнивания", "Гасит раскачку вокруг продольной оси.");
        LocalizedInspector.Property(serializedObject, "autopilotVelocityGain", "Жесткость автопилота", "Как резко автопилот добирает нужную скорость и тормозит перед точкой.");
        LocalizedInspector.Property(serializedObject, "autopilotArrivalRadiusMeters", "Радиус остановки, м", "Внутри этого радиуса автопилот старается остановиться.");
        LocalizedInspector.Property(serializedObject, "fishLookAheadSeconds", "Рыбье упреждение, сек", "На сколько секунд вперед автопилот смотрит по текущей скорости, чтобы заходить в точку дугой.");
        LocalizedInspector.Property(serializedObject, "fishFullArcAngleDeg", "Полная дуга от угла", "При таком угле до цели левиафан максимально сохраняет движение вперед и не пытается боком прыгнуть к точке.");
        LocalizedInspector.Property(serializedObject, "fishArcForwardBias", "Сохранение хода в дуге", "Чем выше значение, тем сильнее левиафан держит нос по текущему ходу при резком развороте.");
        LocalizedInspector.Property(serializedObject, "fishTurnSpeedRetention", "Скорость в развороте", "Минимальная доля крейсерской скорости, которую левиафан старается сохранить на широкой дуге.");
        LocalizedInspector.Property(serializedObject, "fishLateralCorrection", "Боковая коррекция", "Сколько боковой всенаправленной тяги автопилот разрешает во время резкого разворота.");
        LocalizedInspector.Property(serializedObject, "fishTurnBrakePermission", "Торможение в развороте", "Сколько тормозного усилия разрешено, пока левиафан еще не смотрит на цель.");
        LocalizedInspector.Property(serializedObject, "organicSwimMotion", "Живое виляние", "Включает плавную живую поправку к автопилоту и визуальному телу. Левиафан все еще идет к цели, но не летит идеально прямой линией.");
        LocalizedInspector.Property(serializedObject, "swimSwayFrequency", "Частота виляния, Гц", "Сколько полных покачиваний в секунду делает тело. Маленьким быстрым видам можно ставить больше, большим китам меньше.");
        LocalizedInspector.Property(serializedObject, "swimSwayAngleDeg", "Угол виляния курса", "На сколько градусов автопилот мягко отклоняет желаемое направление влево-вправо. Это не мгновенный поворот тела, а живая поправка к цели.");
        LocalizedInspector.Property(serializedObject, "swimSwaySideForceKgf", "Боковой толчок, кгс", "Небольшая физическая боковая тяга от движения тела. Делает траекторию не идеально рельсовой, но при больших значениях может мешать точному заходу.");
        LocalizedInspector.Property(serializedObject, "swimNoiseRadiusMeters", "Случайность курса, м", "Плавный шум цели по горизонтали. Значение задает радиус живого блуждания вокруг настоящей точки.");
        LocalizedInspector.Property(serializedObject, "swimNoiseFrequency", "Частота случайности", "Скорость изменения плавного шума. Низкие значения дают медленное дыхание траектории, высокие делают движение нервнее.");
        LocalizedInspector.Property(serializedObject, "swimVerticalNoiseMeters", "Вертикальное дыхание, м", "Насколько сильно левиафан слегка уходит выше-ниже относительно выбранной цели. Рядом с точкой эффект сам затухает.");
        LocalizedInspector.Property(serializedObject, "visualBodySwayDeg", "Визуальное виляние тела", "Только визуальный поворот модели влево-вправо. Не меняет физический коллайдер и не ломает попадания.");
        LocalizedInspector.Property(serializedObject, "visualTailSwayMeters", "Визуальное виляние хвоста", "Насколько сильно хвост и голова визуально смещаются при плавании. Это декоративное движение без физического урона и столкновений.");
        LocalizedInspector.Property(serializedObject, "failureGravityRampSeconds", "Отказ систем, сек", "За сколько секунд после смерти гравитация вырастает до полной.");
        LocalizedInspector.Property(serializedObject, "failureStartGravity01", "Начальная гравитация", "Доля полной гравитации сразу после отказа систем.");
        LocalizedInspector.Property(serializedObject, "harpoonStruggleForceMultiplier", "Сила рывков на гарпуне", "Множитель силы, с которой живой левиафан сопротивляется натянутому тросу.");
        LocalizedInspector.Property(serializedObject, "harpoonDiveBias", "Стремление вниз", "Насколько сильно пойманный левиафан пытается уйти вниз.");
        LocalizedInspector.Property(serializedObject, "harpoonPanicTurnIntervalSeconds", "Интервал рывков, сек", "Как часто левиафан меняет направление панического рывка.");

        LocalizedInspector.Section("Путевая машинка");
        LocalizedInspector.Property(serializedObject, "routeEnabled", "Маршрут включён", "Если включено, свободный левиафан идёт по заданным точкам вместо обычного блуждания.");
        LocalizedInspector.Property(serializedObject, "routeLoop", "Зациклить маршрут", "После последней точки снова идти к первой.");
        LocalizedInspector.Property(serializedObject, "routeWaypointRadiusMeters", "Радиус точки, м", "Точка считается достигнутой, когда левиафан вошёл в этот радиус.");
        LocalizedInspector.Property(serializedObject, "routeCruiseSpeedMS", "Скорость маршрута, м/с", "Желаемая скорость движения по точкам. Фактическая скорость зависит от тяги и сопротивления воздуха.");
        LocalizedInspector.Property(serializedObject, "routeRandomPointCount", "Сколько точек генерировать", "Количество случайных точек для кнопки генерации маршрута.");
        LocalizedInspector.Property(serializedObject, "routeRandomRadiusMeters", "Радиус генерации, м", "Случайные точки создаются вокруг текущей позиции левиафана в этом радиусе.");
        LocalizedInspector.Property(serializedObject, "routeWaypointIndex", "Текущая точка", "Индекс точки, к которой сейчас идёт путевая машинка.");
        LocalizedInspector.Property(serializedObject, "routeWaypoints", "Точки маршрута", "Список мировых координат маршрута левиафана.");
        LocalizedInspector.Property(serializedObject, "routeStatus", "Статус маршрута", "Последнее сообщение путевой машинки.");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Сгенерировать 1 км"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(leviathan, "Generate leviathan route");
            leviathan.GenerateRandomRoute(1000f, Mathf.Max(1, leviathan.routeRandomPointCount));
            EditorUtility.SetDirty(leviathan);
            SceneView.RepaintAll();
            serializedObject.Update();
        }

        if (GUILayout.Button("Сгенерировать по настройкам"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(leviathan, "Generate leviathan route");
            leviathan.GenerateRandomRoute(leviathan.routeRandomRadiusMeters, leviathan.routeRandomPointCount);
            EditorUtility.SetDirty(leviathan);
            SceneView.RepaintAll();
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Запустить"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(leviathan, "Start leviathan route");
            leviathan.RestartRoute();
            EditorUtility.SetDirty(leviathan);
            SceneView.RepaintAll();
            serializedObject.Update();
        }

        if (GUILayout.Button("Остановить"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(leviathan, "Stop leviathan route");
            leviathan.StopRoute();
            EditorUtility.SetDirty(leviathan);
            SceneView.RepaintAll();
            serializedObject.Update();
        }

        if (GUILayout.Button("Очистить"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(leviathan, "Clear leviathan route");
            leviathan.ClearRoute();
            EditorUtility.SetDirty(leviathan);
            SceneView.RepaintAll();
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        LocalizedInspector.Section("Питание");
        LocalizedInspector.Property(serializedObject, "satietyKg", "Сытость, кг", "Текущий запас сытости. Голодный левиафан ищет глыбы и падающие куски руды.");
        LocalizedInspector.Property(serializedObject, "maxSatietyKg", "Макс. сытость, кг", "Верхний предел сытости.");
        LocalizedInspector.Property(serializedObject, "hungerDecayKgPerSecond", "Расход сытости, кг/с", "Как быстро левиафан снова голодает.");
        LocalizedInspector.Property(serializedObject, "feedingSearchRadiusMeters", "Радиус поиска глыб, м", "На какой дистанции левиафан замечает глыбы для кормления.");
        LocalizedInspector.Property(serializedObject, "feedingBiteKg", "Укус глыбы, кг", "Сколько руды левиафан поглощает за один укус.");
        LocalizedInspector.Property(serializedObject, "feedingBiteIntervalSeconds", "Пауза укуса, сек", "Как часто он может грызть одну глыбу.");
        LocalizedInspector.Property(serializedObject, "feedingApproachDistanceMeters", "Дистанция укуса, м", "Насколько близко надо подойти к глыбе, чтобы начать есть.");
        LocalizedInspector.Property(serializedObject, "fragmentEatRadiusMeters", "Радиус перехвата кусков, м", "Падающие куски руды в этом радиусе левиафан может съесть раньше корабля.");

        LocalizedInspector.Section("Бой");
        LocalizedInspector.Property(serializedObject, "headArmorMm", "Броня головы, мм", "Пока справочная величина для будущей боевой модели.");
        LocalizedInspector.Property(serializedObject, "bodyArmorMm", "Броня тела, мм", "Пока справочная величина для будущей боевой модели.");
        LocalizedInspector.Property(serializedObject, "ramDamageMultiplier", "Множитель тарана", "Сколько урона левиафан наносит/получает тараном относительно базовой модели.");
        LocalizedInspector.Property(serializedObject, "passiveAggressionRangeMeters", "Радиус раздражения, м", "В этом радиусе корабль постепенно поднимает тревогу левиафана.");
        LocalizedInspector.Property(serializedObject, "ramChargeForceMultiplier", "Сила тарана", "Множитель силы плавания во время тарана.");
        LocalizedInspector.Property(serializedObject, "harpoonedRamMassRatio", "Таран на гарпуне от массы", "Если левиафан тяжелее корабля во столько раз, он на гарпуне пытается таранить, а не убегать.");
        LocalizedInspector.Property(serializedObject, "harpoonedRamRetreatDistanceMeters", "Отход на гарпуне, м", "На какую дистанцию левиафан старается отойти перед новым ударом.");
        LocalizedInspector.Property(serializedObject, "harpoonedRamChargeDistanceMeters", "Дистанция удара на гарпуне, м", "Когда при сближении дистанция меньше этой, левиафан считает заход завершенным и снова отходит.");
        LocalizedInspector.Property(serializedObject, "harpoonedRamChargeSeconds", "Рывок на гарпуне, сек", "Сколько длится один заход на таран, если контакт не случился раньше.");
        LocalizedInspector.Property(serializedObject, "harpoonedRamRetreatSeconds", "Отход на гарпуне, сек", "Минимальное время отхода после удара или сорванного захода.");
        LocalizedInspector.Property(serializedObject, "harpoonedSmallRamForceMultiplier", "Сила малого на гарпуне", "Множитель силы тарана для левиафана, который легче корабля. Он все равно пробует ударить, но слабее.");
        LocalizedInspector.Property(serializedObject, "harpoonAlarmAggressionRangeMultiplier", "Радиус тревоги гарпуна", "Во сколько раз увеличивается радиус агрессии свободных левиафанов, если кто-то пойман гарпуном.");
        LocalizedInspector.Property(serializedObject, "ramMinDamageSpeedMS", "Мин. скорость урона, м/с", "Ниже этой скорости столкновение считается мягким и урон не наносится.");
        LocalizedInspector.Property(serializedObject, "ramDamageScale", "Масштаб урона тарана", "Урон считается по корню из энергии столкновения, как в модели корабельного тарана.");

        LocalizedInspector.Section("Тревога");
        LocalizedInspector.Property(serializedObject, "alarm01", "Текущая тревога", "0 - спокойно, 1 - немедленный заход на таран. Во время тарана тревога сгорает и не растет.");
        LocalizedInspector.Property(serializedObject, "alarmNearGainPerSecond", "Рост рядом, /с", "Сколько тревоги в секунду добавляет корабль в радиусе раздражения.");
        LocalizedInspector.Property(serializedObject, "alarmHarpoonedGainPerSecond", "Рост от гарпуна, /с", "Базовый рост тревоги, пока левиафан на гарпуне.");
        LocalizedInspector.Property(serializedObject, "alarmTensionGainPerSecond", "Рост от натяжения, /с", "Добавка тревоги от натяжения троса. На пределе прочности добавляется полностью.");
        LocalizedInspector.Property(serializedObject, "alarmOtherHarpoonedGainPerSecond", "Рост от чужой охоты, /с", "Как быстро тревожится свободный левиафан, если рядом гарпуном поймали другого.");
        LocalizedInspector.Property(serializedObject, "alarmShotNearAdd", "Выстрел рядом", "Сколько тревоги добавляет выстрел рядом.");
        LocalizedInspector.Property(serializedObject, "alarmShotAtSelfAdd", "Выстрел по нему", "Дополнительная тревога, если выстрелили именно по этому левиафану.");
        LocalizedInspector.Property(serializedObject, "alarmRamDecaySeconds", "Сгорание тарана, сек", "За сколько секунд тревога падает от максимума до нуля. Это же окно, за которое левиафан пытается успеть ударить.");

        LocalizedInspector.Section("Отладка");
        LocalizedInspector.Property(serializedObject, "behaviorStatus", "Поведение", "Последнее поведенческое сообщение.");
        LocalizedInspector.Property(serializedObject, "debugLogging", "Писать лог", "Если включено, левиафан периодически пишет состояние в Console.");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(leviathan.GetStatusRu(), MessageType.Info);
    }
}

[CustomEditor(typeof(HarpoonTether))]
public class HarpoonTetherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Связи");
        LocalizedInspector.Property(serializedObject, "ownerShip", "Корабль", "Корабль, который выпустил гарпун.");
        LocalizedInspector.Property(serializedObject, "target", "Цель", "Левиафан, в которого попал гарпун.");

        LocalizedInspector.Section("Трос");
        LocalizedInspector.Property(serializedObject, "ropeLengthMeters", "Длина троса, м", "После этой дистанции трос начинает натягиваться.");
        LocalizedInspector.Property(serializedObject, "maxTensionKg", "Прочность троса, кгс", "Если натяжение выше этого значения, гарпун обрывается.");
        LocalizedInspector.Property(serializedObject, "maxTensionLifetimeSeconds", "Жизнь на пределе, сек", "Сколько секунд трос выдерживает натяжение ровно на пределе. При 10% натяжения живет примерно в 10 раз дольше, при 200% - вдвое меньше.");
        LocalizedInspector.Property(serializedObject, "wear01", "Износ троса", "0 = новый трос, 1 = обрыв.");
        LocalizedInspector.Property(serializedObject, "stiffnessNPerMeter", "Жесткость, Н/м", "Сколько силы добавляется за метр растяжения.");
        LocalizedInspector.Property(serializedObject, "dampingNsPerMeter", "Демпфер, Н·с/м", "Гасит рывки, когда корабль и цель расходятся.");
        LocalizedInspector.Property(serializedObject, "reelForceN", "Подтяжка лебедки, Н", "Постоянная сила подтягивания при натянутом тросе.");
        LocalizedInspector.Property(serializedObject, "fatiguePerSecond", "Урон здоровью в секунду", "Сколько здоровья цель теряет при полном натяжении.");
        LocalizedInspector.Property(serializedObject, "collectionRadiusMeters", "Радиус сбора туши, м", "Когда туша входит в этот радиус, ее можно погрузить в трюм.");
        LocalizedInspector.Property(serializedObject, "carcassWinchSpeedMetersPerSecond", "Скорость лебедки, м/с", "Как быстро лебедка укорачивает трос после смерти цели.");

        LocalizedInspector.Section("Состояние");
        LocalizedInspector.Property(serializedObject, "currentTensionN", "Натяжение, Н", "Текущее физическое натяжение троса.");
        LocalizedInspector.Property(serializedObject, "lastMessage", "Сообщение", "Последнее состояние или причина обрыва.");

        serializedObject.ApplyModifiedProperties();
    }
}
