using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipPhysics))]
public class ShipPhysicsEditor : Editor
{
    SerializedProperty baseMassProp;
    SerializedProperty thrustEngineProp;
    SerializedProperty liftEngineProp;
    SerializedProperty thrustEfficiencyProp;
    SerializedProperty liftEfficiencyProp;
    SerializedProperty turnTorqueProp;
    SerializedProperty airDensityProp;
    SerializedProperty dragCoefficientProp;
    SerializedProperty frontalAreaProp;
    SerializedProperty sideResistanceProp;
    SerializedProperty verticalAreaFactorProp;
    SerializedProperty maxVerticalSpeedProp;
    
    SerializedProperty thrustInputProp;
    SerializedProperty turnInputProp;
    SerializedProperty liftInputProp;
    SerializedProperty altitudeHoldProp;
    SerializedProperty targetAltitudeProp;
    SerializedProperty cruiseControlProp;
    SerializedProperty targetSpeedMSProp;
    SerializedProperty speedDampingProp;
    SerializedProperty maxCruiseSpeedMSProp;
    SerializedProperty maxManualSpeedProp;
    SerializedProperty targetTrimMassProp;

    void OnEnable()
    {
        baseMassProp = serializedObject.FindProperty("baseMass");
        thrustEngineProp = serializedObject.FindProperty("thrustEngine");
        liftEngineProp = serializedObject.FindProperty("liftEngine");
        thrustEfficiencyProp = serializedObject.FindProperty("thrustEfficiency");
        liftEfficiencyProp = serializedObject.FindProperty("liftEfficiency");
        turnTorqueProp = serializedObject.FindProperty("turnTorque");
        
        airDensityProp = serializedObject.FindProperty("airDensity");
        dragCoefficientProp = serializedObject.FindProperty("dragCoefficient");
        frontalAreaProp = serializedObject.FindProperty("frontalArea");
        sideResistanceProp = serializedObject.FindProperty("sideResistance");
        verticalAreaFactorProp = serializedObject.FindProperty("verticalAreaFactor");
        maxVerticalSpeedProp = serializedObject.FindProperty("maxVerticalSpeed");
        
        thrustInputProp = serializedObject.FindProperty("thrustInput");
        turnInputProp = serializedObject.FindProperty("turnInput");
        liftInputProp = serializedObject.FindProperty("liftInput");
        targetTrimMassProp = serializedObject.FindProperty("targetTrimMass");
        altitudeHoldProp = serializedObject.FindProperty("altitudeHold");
        targetAltitudeProp = serializedObject.FindProperty("targetAltitude");
        cruiseControlProp = serializedObject.FindProperty("cruiseControl");
        targetSpeedMSProp = serializedObject.FindProperty("targetSpeedMS");
        speedDampingProp = serializedObject.FindProperty("speedDamping");
        maxCruiseSpeedMSProp = serializedObject.FindProperty("maxCruiseSpeedMS");
        maxManualSpeedProp = serializedObject.FindProperty("maxManualSpeedMS");
    }

    public override void OnInspectorGUI()
    {
        // Получаем ссылку на сам скрипт корабля
        ShipPhysics ship = (ShipPhysics)target;
        Rigidbody rb = ship.GetComponent<Rigidbody>();

        // Обновляем значения из скрипта
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Параметры корабля", EditorStyles.boldLabel);
        
        // Отрисовка базовых параметров с принудительными русскими названиями
        EditorGUILayout.PropertyField(baseMassProp, new GUIContent("Стартовая масса (кг)"));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Силовые установки", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(thrustEngineProp, new GUIContent("Маршевый двигатель"));
        EditorGUILayout.PropertyField(liftEngineProp, new GUIContent("Двигатель подъема"));
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Эффективность систем", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(thrustEfficiencyProp, new GUIContent("Эфф. винта (кгс/лс)"));
        EditorGUILayout.PropertyField(liftEfficiencyProp, new GUIContent("Эфф. контура (кгс/лс)"));
        EditorGUILayout.PropertyField(turnTorqueProp, new GUIContent("Сила поворота (кгс*м)"));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Аэродинамика", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(airDensityProp, new GUIContent("Плотность воздуха"));
        EditorGUILayout.PropertyField(dragCoefficientProp, new GUIContent("Коэф. формы (Cd)"));
        EditorGUILayout.PropertyField(frontalAreaProp, new GUIContent("Лобовая площадь (м²)"));
        EditorGUILayout.PropertyField(sideResistanceProp, new GUIContent("Сопротивление сносу"));
        EditorGUILayout.PropertyField(verticalAreaFactorProp, new GUIContent("Коэф. удлинения (вертикаль)"));
        EditorGUILayout.PropertyField(maxVerticalSpeedProp, new GUIContent("Лимит верт. скорости (м/с)"));

        EditorGUILayout.HelpBox(
            "Памятка по коэф. формы (Cd):\n" +
            "• 0.04 : Идеальная обтекаемая капля\n" +
            "• 0.50 : Сфера / Округлый корпус\n" +
            "• 1.10 : Плоский торец (как контейнер)\n" +
            "• 2.00+ : Раскрытый парашют или ковш", 
            MessageType.None);

        // Расчет и вывод максимальной скорости
        float currentDrag = 0.5f * airDensityProp.floatValue * dragCoefficientProp.floatValue * frontalAreaProp.floatValue;
        

        // Расчет максимальной теоретической скорости (когда Тяга = Сопротивлению)
        float maxSpeedMS = 0f;
        if (currentDrag > 0 && ship.thrustEngine != null)
        {
            float maxThrustNewtons = ship.thrustEngine.maxPower * thrustEfficiencyProp.floatValue * Mathf.Abs(Physics.gravity.y);
            maxSpeedMS = Mathf.Sqrt(maxThrustNewtons / currentDrag);
        }

        EditorGUILayout.HelpBox(
            $"Итоговое сопротивление воздуха: {currentDrag:F2}\n" +
            $"Верт. лимит: {ship.maxVerticalSpeed} м/с\n" +
            $"Расчетная макс. скорость: {maxSpeedMS:F1} м/с", 
            MessageType.Info);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Режим маршевого двигателя (CSU)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Малый", GUILayout.Height(25))) ship.targetMainEngineRPM = 0.2f;
        if (GUILayout.Button("Крейсер", GUILayout.Height(25))) ship.targetMainEngineRPM = 0.6f;
        if (GUILayout.Button("Номинал", GUILayout.Height(25))) ship.targetMainEngineRPM = 0.85f;
        if (GUILayout.Button("Взлет", GUILayout.Height(25))) ship.targetMainEngineRPM = 1.0f;
        if (GUILayout.Button("ФОРСАЖ", GUILayout.Height(25))) ship.targetMainEngineRPM = 1.15f;
        EditorGUILayout.EndHorizontal();

        // Список расчетных скоростей
        float drag = ship.CurrentAeroDrag;
        float efficiency = serializedObject.FindProperty("thrustEfficiency").floatValue;
        float g = Mathf.Abs(Physics.gravity.y);
        float maxPowerNewtons = ship.thrustEngine != null ? ship.thrustEngine.maxPower * efficiency * g : 0f;

        float CalcSpeed(float rpm) => (drag > 0 && maxPowerNewtons > 0) ? Mathf.Sqrt((maxPowerNewtons * rpm) / drag) : 0f;

        string speedList = 
            $"• Малый (20%): {CalcSpeed(0.2f):F1} м/с\n" +
            $"• Крейсер (60%): {CalcSpeed(0.6f):F1} м/с\n" +
            $"• Номинал (85%): {CalcSpeed(0.85f):F1} м/с\n" +
            $"• Взлет (100%): {CalcSpeed(1.0f):F1} м/с\n" +
            $"• ФОРСАЖ (115%): {CalcSpeed(1.15f):F1} м/с";

        EditorGUILayout.HelpBox($"Макс. скорость для режимов:\n{speedList}", MessageType.None);
        
        float currentRPM = ship.thrustEngine != null ? ship.thrustEngine.currentRPM : 0f;
        float currentLoad = ship.thrustEngine != null ? ship.thrustEngine.currentLoad : 0f;
        
        Rect rpmRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(rpmRect, currentRPM / 1.15f, $"Обороты: {(currentRPM * 100):F0}% (Цель: {(ship.targetMainEngineRPM * 100):F0}%)");
        
        Rect loadRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(loadRect, currentLoad, $"Нагрузка на винт: {(currentLoad * 100):F0}%");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Автопилот", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(altitudeHoldProp, new GUIContent("Удержание высоты"));
        EditorGUILayout.PropertyField(cruiseControlProp, new GUIContent("Круиз-контроль (скорость)"));
        EditorGUILayout.PropertyField(speedDampingProp, new GUIContent("   Демпфирование (D)"));
        EditorGUILayout.PropertyField(maxManualSpeedProp, new GUIContent("Лимит ручной скорости (м/с)"));
        EditorGUILayout.PropertyField(maxCruiseSpeedMSProp, new GUIContent("Лимит круиз-скорости (м/с)"));
        
        if (altitudeHoldProp.boolValue || cruiseControlProp.boolValue)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (altitudeHoldProp.boolValue)
                EditorGUILayout.PropertyField(targetAltitudeProp, new GUIContent("Целевая высота (м)"));
            if (cruiseControlProp.boolValue)
                EditorGUILayout.PropertyField(targetSpeedMSProp, new GUIContent("Целевая скорость (м/с)"));
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space(10);
        
        // Отрисовка панели управления
        EditorGUILayout.LabelField("Панель управления рычагами", EditorStyles.boldLabel);
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        EditorGUILayout.BeginVertical(boxStyle);

        float maxLift = 0f;
        if (ship.liftEngine != null) maxLift = ship.liftEngine.maxPower * ship.liftEfficiency;
        float maxTrim = maxLift * 0.9f;

        EditorGUILayout.BeginHorizontal();
        float newTrim = EditorGUILayout.Slider("Триммер (кг)", ship.targetTrimMass, 0f, maxTrim);
        if (newTrim != ship.targetTrimMass) {
            Undo.RecordObject(ship, "Change Trim Mass");
            ship.targetTrimMass = newTrim;
        }
        if (GUILayout.Button("Авто", GUILayout.Width(50))) {
            ship.targetTrimMass = ship.baseMass;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Slider(liftInputProp, -1f, 1f, new GUIContent("Подъем точный (+-10%)"));
        EditorGUILayout.Slider(thrustInputProp, -1f, 1f, new GUIContent("Тяга (назад/вперед)"));
        EditorGUILayout.Slider(turnInputProp, -1f, 1f, new GUIContent("Руль (влево/вправо)"));

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
            EditorGUILayout.LabelField("Угловая (р/с)", GUILayout.Width(100));
            
            EditorGUILayout.LabelField("X", GUILayout.Width(12));
            float ax = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.x, 2));
            
            EditorGUILayout.LabelField("Y", GUILayout.Width(12));
            float ay = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.y, 2));
            
            EditorGUILayout.LabelField("Z", GUILayout.Width(12));
            float az = EditorGUILayout.FloatField((float)System.Math.Round(rb.angularVelocity.z, 2));
            EditorGUILayout.EndHorizontal();
            
            if (EditorGUI.EndChangeCheck())
            {
                rb.linearVelocity = new Vector3(vx, vy, vz);
                rb.angularVelocity = new Vector3(ax, ay, az);
            }

            EditorGUILayout.Space(5);
            float currentRPMTelemetry = ship.thrustEngine != null ? ship.thrustEngine.currentRPM * 100f : 0f;
            EditorGUILayout.LabelField($"Фактические обороты: {currentRPMTelemetry:F2}%", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Ожидание компонента Rigidbody...");
        }

        EditorGUILayout.EndVertical();

        // Применяем изменения, это автоматом сохранит undo/redo и префабы
        serializedObject.ApplyModifiedProperties();
    }

    // Заставляем инспектор перерисовываться каждый кадр в режиме игры, 
    // чтобы цифры скорости бегали в реальном времени.
    public override bool RequiresConstantRepaint()
    {
        // Если пользователь кликнул в поле и вводит число, 
        // мы приостанавливаем перерисовку, чтобы его ввод не перезаписывался
        bool isTyping = GUIUtility.keyboardControl != 0;
        
        return Application.isPlaying && !isTyping;
    }
}
