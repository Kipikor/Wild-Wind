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
        
        // Максимальная тяга = Макс. мощность движка * его эффективность
        float maxThrustKgf = 0f;
        if (ship.thrustEngine != null)
        {
            maxThrustKgf = ship.thrustEngine.maxPower * thrustEfficiencyProp.floatValue;
        }
        
        float thrustNewtons = maxThrustKgf * Mathf.Abs(Physics.gravity.y);
        
        float maxSpeed = 0f;
        if (currentDrag > 0 && rb != null)
        {
            // На высокой скорости линейный демпфер равен 0, поэтому считаем только воздух
            maxSpeed = Mathf.Sqrt(thrustNewtons / currentDrag);
        }
        else if (currentDrag > 0) // Если Rigidbody вдруг нет
        {
            maxSpeed = Mathf.Sqrt(thrustNewtons / currentDrag);
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox($"Итоговое сопротивление воздуха: {currentDrag:F2}\nРасчетная макс. скорость: {maxSpeed:F1} м/с ({(maxSpeed * 3.6f):F0} км/ч)", MessageType.Info);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Автопилот", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(altitudeHoldProp, new GUIContent("Удержание высоты"));
        if (altitudeHoldProp.boolValue)
        {
            EditorGUILayout.LabelField($"Целевая высота: {targetAltitudeProp.floatValue:F1} м");
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
            float currentSpeedKMH = currentSpeedMS * 3.6f;
            
            EditorGUILayout.LabelField($"Текущая скорость: {currentSpeedMS:F1} м/с ({currentSpeedKMH:F0} км/ч)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            
            Vector3 newVelocity = EditorGUILayout.Vector3Field("Скорость (м/с)", rb.linearVelocity);
            Vector3 newAngularVelocity = EditorGUILayout.Vector3Field("Угловая скорость", rb.angularVelocity);
            
            if (EditorGUI.EndChangeCheck())
            {
                // Если мы ручками поменяли цифры в инспекторе, применяем их к Rigidbody
                rb.linearVelocity = newVelocity;
                rb.angularVelocity = newAngularVelocity;
            }
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
