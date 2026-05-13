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
    private bool resourceCheatsExpanded = true;
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
            EditorGUILayout.LabelField($"Остаток на винт: {Mathf.Max(0f, ship.engineGeneratedPowerKw - ship.claudiumPowerDrawKw):F1} кВт", EditorStyles.label);
            EditorGUILayout.LabelField($"Топливо: {ship.engineFuelStockKg:F2} кг, энергоемкость {ship.engineFuelEnergyKwhPerKg:F1} кВт·ч/кг", EditorStyles.label);
            EditorGUILayout.LabelField($"Расход топлива: {ship.engineFuelConsumptionKgPerSecond:F4} кг/с", EditorStyles.label);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Автопилот и Системы", EditorStyles.boldLabel);
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
            float ax_deg = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.x * Mathf.Rad2Deg, 2));
            
            EditorGUILayout.LabelField("Y", GUILayout.Width(12));
            float ay_deg = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.y * Mathf.Rad2Deg, 2));
            
            EditorGUILayout.LabelField("Z", GUILayout.Width(12));
            float az_deg = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.z * Mathf.Rad2Deg, 2));
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                rb.linearVelocity = new Vector3(vx, vy, vz);
                rb.angularVelocity = new Vector3(ax_deg * Mathf.Deg2Rad, ay_deg * Mathf.Deg2Rad, az_deg * Mathf.Deg2Rad);
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
        customCheatAmount = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Количество, кг", "Точное значение, которое будет записано."), customCheatAmount));

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
        int newAmount = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent(label, "Точное количество в килограммах. 0 удаляет ресурс из списка."), currentAmount));
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
        ship.cargoMassKg = meta.progress.GetShipCargoMassKg();
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
