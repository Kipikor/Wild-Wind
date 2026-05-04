using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShipPhysics))]
public class ShipPhysicsEditor : Editor
{
    private SerializedProperty baseMassProp;
    private SerializedProperty liftEfficiencyProp;
    private SerializedProperty thrustEfficiencyProp;
    private SerializedProperty turnSpeedProp;
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
    private SerializedProperty maxManualSpeedMSProp;
    private SerializedProperty maxCruiseSpeedMSProp;
    private SerializedProperty targetAltitudeProp;
    private SerializedProperty altStiffnessProp;
    private SerializedProperty altDampingProp;
    private SerializedProperty altDriftToleranceProp;
    private SerializedProperty liftInputProp;
    private SerializedProperty thrustInputProp;
    private SerializedProperty turnInputProp;
    private SerializedProperty targetTrimMassProp;

    private void OnEnable()
    {
        baseMassProp = serializedObject.FindProperty("baseMass");
        liftEfficiencyProp = serializedObject.FindProperty("liftEfficiency");
        thrustEfficiencyProp = serializedObject.FindProperty("thrustEfficiency");
        turnSpeedProp = serializedObject.FindProperty("turnTorque"); // Исправлено: turnTorque
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
        maxManualSpeedMSProp = serializedObject.FindProperty("maxManualSpeedMS");
        maxCruiseSpeedMSProp = serializedObject.FindProperty("maxCruiseSpeedMS");
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
        EditorGUILayout.PropertyField(liftEfficiencyProp, new GUIContent("Эфф. подъемной силы"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerDiameter"), new GUIContent("Диаметр винта (м)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerEfficiency"), new GUIContent("КПД винта (0.7-0.85)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("propellerMaxPitchMeters"), new GUIContent("Геометр. шаг (м/об)"));
        EditorGUILayout.PropertyField(turnSpeedProp, new GUIContent("Сила поворота"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Аэродинамика", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(airDensityProp, new GUIContent("Плотность воздуха (rho)"));
        EditorGUILayout.PropertyField(dragCoefficientProp, new GUIContent("Коэф. формы (Cd)"));
        EditorGUILayout.PropertyField(frontalAreaProp, new GUIContent("Лобовая площадь (м²)"));
        EditorGUILayout.PropertyField(sideResistanceProp, new GUIContent("Боковое сопротивление"));
        EditorGUILayout.PropertyField(maxStructuralVerticalSpeedProp, new GUIContent("Конструкц. лимит (м/с)"));
        EditorGUILayout.PropertyField(maxAutoVerticalSpeedProp, new GUIContent("Лимит автопилота (м/с)"));

        EditorGUILayout.HelpBox(
            "Коэффициенты формы (Cd):\n" +
            "• 0.04 : Капля (идеально)\n" +
            "• 0.50 : Шар\n" +
            "• 1.10 : Плоский торец (как контейнер)\n" +
            "• 2.00+ : Раскрытый парашют или ковш", 
            MessageType.None);

        // Расчет и вывод максимальной скорости (л.с. + физика винта)
        float currentAirDensity = airDensityProp.floatValue;
        float currentDrag = 0.5f * currentAirDensity * dragCoefficientProp.floatValue * frontalAreaProp.floatValue;
        
        // Учитываем множитель баллона, как в ShipPhysics.cs
        float aeroMultiplier = (ship.balloonModule != null && ship.balloonModule.gameObject.activeSelf) ? 5.0f : 1.0f;
        float totalDrag = currentDrag * aeroMultiplier;

        float maxSpeedMS = 0f;
        
        if (totalDrag > 0 && ship.thrustEngine != null)
        {
            float pWattsMax = ship.thrustEngine.maxPower * 735.5f;
            float maxRPM_Val = ship.thrustEngine.maxRPM;
            float discArea = Mathf.PI * Mathf.Pow(ship.propellerDiameter * 0.5f, 2);
            
            // Идеальная тяга в статике
            float maxStaticT = Mathf.Pow(2f * currentAirDensity * discArea * (pWattsMax * pWattsMax), 1f/3f) * ship.propellerEfficiency;
            
            // Шаговая скорость винта (при максимальных оборотах и максимальном шаге 1.0)
            float screwSpeed = (maxRPM_Val / 60f) * ship.propellerMaxPitchMeters; 
            
            // Итеративный поиск реальной максимальной скорости (точка пересечения графиков Тяги и Сопротивления)
            float v = 0f;
            for (int i = 0; i < 200; i++)
            {
                v += 0.2f; 
                float thrustFactor = 1f - (v / screwSpeed);
                if (thrustFactor < 0) thrustFactor = 0;
                
                // Мощностная тяга
                float powerThrust = (pWattsMax * ship.propellerEfficiency) / Mathf.Max(v, 0.1f);
                // Итоговая расчетная тяга винта с учетом вырождения
                float thrust = Mathf.Min(maxStaticT, powerThrust) * thrustFactor;
                float drag = totalDrag * v * v;
                
                if (thrust < drag)
                {
                    maxSpeedMS = v - 0.2f;
                    break;
                }
            }
        }

        float balloonArea = (ship.balloonModule != null) ? ship.balloonModule.лобоваяПроекцияМ2 : 0f;

        EditorGUILayout.HelpBox(
            $"Итоговое сопротивление воздуха: {totalDrag:F2}\n" +
            $"Площадь баллона: {balloonArea:F1} м²\n" +
            $"Верт. лимит (Констр / Авто): {ship.maxStructuralVerticalSpeed} / {ship.maxAutoVerticalSpeed} м/с\n" +
            $"Реальный макс. потенциал: {maxSpeedMS:F1} м/с", 
            MessageType.Info);

        EditorGUILayout.Space(10);
        
        SerializedProperty hasCSUProp = serializedObject.FindProperty("hasCSU");
        if (hasCSUProp.boolValue)
        {
            EditorGUILayout.LabelField("Управление оборотами (CSU)", EditorStyles.boldLabel);
            
            string modeName = "Стоп";
            float rpm = ship.targetMainEngineRPM;
            if (rpm <= 0.1f) modeName = "Холостой ход";
            else if (rpm <= 0.3f) modeName = "Малый ход";
            else if (rpm <= 0.7f) modeName = "Крейсерский";
            else if (rpm <= 0.9f) modeName = "Номинал";
            else if (rpm <= 1.05f) modeName = "Взлетный";
            else modeName = "ФОРСАЖ";

            ship.targetMainEngineRPM = EditorGUILayout.Slider($"Целевые обороты: {modeName}", ship.targetMainEngineRPM, 0f, 1.2f);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Минимум", EditorStyles.miniButtonLeft)) ship.targetMainEngineRPM = 0.2f;
            if (GUILayout.Button("Крейсер", EditorStyles.miniButtonMid)) ship.targetMainEngineRPM = 0.6f;
            if (GUILayout.Button("Максимум", EditorStyles.miniButtonRight)) ship.targetMainEngineRPM = 1.0f;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("РУЧНОЕ УПРАВЛЕНИЕ (Нет CSU)", EditorStyles.boldLabel);
            ship.targetMainEngineRPM = EditorGUILayout.Slider("Газ (Мощность мотора)", ship.targetMainEngineRPM, 0f, 1.2f);
            EditorGUILayout.HelpBox("ВНИМАНИЕ: Балансируйте газ и шаг винта вручную, чтобы не заглушить мотор!", MessageType.Warning);
        }
        
        EditorGUILayout.Space(5);
        float currentRPM = ship.thrustEngine != null ? ship.thrustEngine.currentRPM : 0f;
        float targetVal = ship.targetMainEngineRPM;
        bool isOverloaded = ship.thrustEngine != null && ship.thrustEngine.isOverloaded;
        
        Rect rpmRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        Color oldGuiColor = GUI.color;
        if (isOverloaded) GUI.color = new Color(1f, 0.4f, 0.4f); // Красный фон при перегрузке
        
        string overloadText = isOverloaded ? " [ ENGINE OVERLOAD ]" : "";
        EditorGUI.ProgressBar(rpmRect, currentRPM / 1.15f, $"Обороты: {(currentRPM * 100):F1}% (Цель: {(targetVal * 100):F0}%){overloadText}");
        
        GUI.color = oldGuiColor;
        
        Rect pitchRect = GUILayoutUtility.GetRect(18, 18, "TextField");
        float pitchVisual = (ship.propellerPitch + 1f) / 2f; 
        EditorGUI.ProgressBar(pitchRect, pitchVisual, $"Реальный шаг винта: {ship.propellerPitch:F2}");
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Установленные модули", EditorStyles.boldLabel);
        ship.balloonModule = (ShipBalloon)EditorGUILayout.ObjectField("Модуль: Баллон", ship.balloonModule, typeof(ShipBalloon), true);
        ship.claudiumLoop = (ShipClaudiumLoop)EditorGUILayout.ObjectField("Модуль: Контур", ship.claudiumLoop, typeof(ShipClaudiumLoop), true);
        
        EditorGUILayout.Space(5);
        // Телеметрия Клавдия
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Состояние систем подъема", EditorStyles.miniBoldLabel);
            
            if (ship.claudiumLoop != null)
            {
                EditorGUILayout.LabelField(" Claudium Loop Status", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Solution Stock: {ship.claudiumLoop.запасРаствораЛ:F2} L ({ship.claudiumLoop.концентрацияКлавдия:F1}%)");
                EditorGUILayout.LabelField($"  Active Lift: {ship.activeLiftForce / 9.81f:F0} kgf");
            }
            else EditorGUILayout.LabelField("Контур: не установлен", EditorStyles.miniLabel);

            if (ship.balloonModule != null)
            {
                float gasGrams = ship.balloonModule.текущийГазКг * 1000f;
                EditorGUILayout.LabelField($"Газ в баллонах: {gasGrams:F1} г ({ship.balloonModule.процентЗаполнения:F1}%)", EditorStyles.label);
                EditorGUILayout.LabelField($"Подъемная сила газа: {ship.currentGasLift/9.81f:F0} кг", EditorStyles.label);
            }
            else EditorGUILayout.LabelField("Баллон: не установлен", EditorStyles.miniLabel);
            
            float activeLiftKg = ship.activeLiftForce / 9.81f;
            EditorGUILayout.LabelField($"Активный подъем (насос): {activeLiftKg:F0} кг", EditorStyles.label);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Автопилот и Системы", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hasCSU"), new GUIContent("Есть CSU (Автомат шага)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoStabilizeAtStart"), new GUIContent("Стабилизация при старте"));
        EditorGUILayout.PropertyField(altitudeHoldProp, new GUIContent("Удержание высоты"));
        EditorGUILayout.PropertyField(cruiseControlProp, new GUIContent("Круиз-контроль (скорость)"));
        EditorGUILayout.PropertyField(speedStiffnessProp, new GUIContent("Жесткость (P)"));
        EditorGUILayout.PropertyField(speedDampingProp, new GUIContent("Демпфирование (D)"));
        EditorGUILayout.PropertyField(maxManualSpeedMSProp, new GUIContent("Лимит ручной скорости (м/с)"));
        EditorGUILayout.PropertyField(maxCruiseSpeedMSProp, new GUIContent("Лимит круиз-скорости (м/с)"));
        EditorGUILayout.PropertyField(targetAltitudeProp, new GUIContent("Целевая высота (м)"));
        EditorGUILayout.PropertyField(altStiffnessProp, new GUIContent("Вертикальная жесткость (P)"));
        EditorGUILayout.PropertyField(altDampingProp, new GUIContent("Вертикальный демпфер (D)"));
        EditorGUILayout.PropertyField(altDriftToleranceProp, new GUIContent("Допуск дрейфа (м)"));

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
            float curRPM_Normalized = ship.thrustEngine != null ? ship.thrustEngine.currentRPM : 0f;
            float maxRPM_Val = ship.thrustEngine != null ? ship.thrustEngine.maxRPM : 2500f;
            
            // Определяем актуальную цель в зависимости от режима
            float targetRPM_Val = ship.targetMainEngineRPM;
            
            // Расчет текущей тяги для вывода
            float pWattsNominal = (ship.thrustEngine != null ? ship.thrustEngine.maxPower : 0f) * 735.5f;
            float discArea = Mathf.PI * Mathf.Pow(ship.propellerDiameter * 0.5f, 2);
            float maxStaticT = Mathf.Pow(2f * currentAirDensity * discArea * (pWattsNominal * pWattsNominal), 1f/3f) * ship.propellerEfficiency;
            float currentStaticT = maxStaticT * (curRPM_Normalized * curRPM_Normalized);
            
            float forwardSpeed = Vector3.Dot(ship.GetComponent<Rigidbody>().linearVelocity, ship.transform.forward);
            float screwSpeed = (curRPM_Normalized * maxRPM_Val / 60f) * ship.propellerPitch * ship.propellerMaxPitchMeters;
            float thrustFactor = (Mathf.Abs(screwSpeed) > 0.01f) ? ((screwSpeed > 0) ? (1f - forwardSpeed / screwSpeed) : (-1f + forwardSpeed / screwSpeed)) : 0f;
            float currentThrustKg = (currentStaticT * Mathf.Clamp(thrustFactor, -1.2f, 1.2f)) / 9.81f;

            EditorGUILayout.LabelField($"Текущая тяга винта: {currentThrustKg:F1} кг", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Фактические обороты: {(curRPM_Normalized * maxRPM_Val):F0} RPM ({(curRPM_Normalized * 100):F1}%)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Цель (Газ/CSU): {(targetRPM_Val * 100):F0}%", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Ожидание компонента Rigidbody...");
        }

        EditorGUILayout.EndVertical();

        // Применяем изменения
        serializedObject.ApplyModifiedProperties();
    }

    public override bool RequiresConstantRepaint()
    {
        bool isTyping = GUIUtility.keyboardControl != 0;
        return Application.isPlaying && !isTyping;
    }
}
